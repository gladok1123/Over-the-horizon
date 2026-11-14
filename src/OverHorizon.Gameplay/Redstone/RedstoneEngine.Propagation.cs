using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;

namespace OverHorizon.Gameplay.Redstone;

/// <summary>
/// Zero-allocation pooled Dijkstra <c>0..15</c> <c>RedstoneWire</c> signal solver, directional block power queries,
/// and momentary button/plate timers for <see cref="RedstoneEngine"/>.
/// </summary>
public sealed partial class RedstoneEngine
{
    private readonly HashSet<BlockPos> _steppedPlatesScratch = [];

    private void PropagateWireNetwork(HashSet<BlockPos> region, Func<BlockPos, BlockState> getBlock, Action<BlockPos, BlockState> setBlock)
    {
        _wiresScratch.Clear();
        _bfsScratch.Clear();

        foreach (var p in region)
        {
            var st = getBlock(p);
            if (!st.IsAir && _blocks.GetStateMetadata(st).BlockKey == BuiltInBlocks.RedstoneWire && _wiresScratch.Add(p))
            {
                _bfsScratch.Enqueue(p);
            }
        }

        if (_bfsScratch.Count == 0)
        {
            return;
        }

        Span<BlockPos> neighbors = stackalloc BlockPos[12];
        while (_bfsScratch.Count > 0 && _wiresScratch.Count < 512)
        {
            var cur = _bfsScratch.Dequeue();
            var nCount = FillConnectedWires(cur, getBlock, neighbors);
            for (var i = 0; i < nCount; i++)
            {
                if (_wiresScratch.Add(neighbors[i]))
                {
                    _bfsScratch.Enqueue(neighbors[i]);
                }
            }
        }

        _powerMapScratch.Clear();
        _pqScratch.Clear();

        foreach (var w in _wiresScratch)
        {
            var seedPower = ComputeExternalSourcePowerForWire(w, getBlock);
            _powerMapScratch[w] = seedPower;
            if (seedPower > 0)
            {
                _pqScratch.Enqueue(w, -seedPower);
            }
        }

        while (_pqScratch.Count > 0)
        {
            var cur = _pqScratch.Dequeue();
            var curPow = _powerMapScratch[cur];
            if (curPow <= 1)
            {
                continue;
            }

            var nextPow = curPow - 1;
            var nCount = FillConnectedWires(cur, getBlock, neighbors);
            for (var i = 0; i < nCount; i++)
            {
                var nw = neighbors[i];
                if (_wiresScratch.Contains(nw) && nextPow > _powerMapScratch[nw])
                {
                    _powerMapScratch[nw] = nextPow;
                    _pqScratch.Enqueue(nw, -nextPow);
                }
            }
        }

        foreach (var (wPos, targetPow) in _powerMapScratch)
        {
            var curState = getBlock(wPos);
            var curMeta = _blocks.GetStateMetadata(curState);
            if (curMeta.BlockKey == BuiltInBlocks.RedstoneWire && ParseInt(curMeta, "power", 0) != targetPow)
            {
                setBlock(wPos, _blocks.WithProperty(curState, "power", PowerLevelTokens[Math.Clamp(targetPow, 0, 15)]));
                var dirs = DirectionExtensions.All;
                for (var i = 0; i < dirs.Length; i++)
                {
                    _ = region.Add(wPos.Offset(dirs[i]));
                }
            }
        }
    }

    private int FillConnectedWires(BlockPos pos, Func<BlockPos, BlockState> getBlock, Span<BlockPos> outNeighbors)
    {
        var count = 0;
        var upState = getBlock(pos.Offset(Direction.Up));
        var aboveBlocked = !upState.IsAir && _blocks.GetStateMetadata(upState).IsOpaque;

        for (var i = 0; i < 4; i++)
        {
            var dir = i switch
            {
                0 => Direction.North,
                1 => Direction.South,
                2 => Direction.West,
                _ => Direction.East,
            };
            var flat = pos.Offset(dir);
            var flatState = getBlock(flat);
            var flatIsOpaque = false;
            if (!flatState.IsAir)
            {
                var flatMeta = _blocks.GetStateMetadata(flatState);
                flatIsOpaque = flatMeta.IsOpaque;
                if (flatMeta.BlockKey == BuiltInBlocks.RedstoneWire)
                {
                    outNeighbors[count++] = flat;
                }
            }

            if (!aboveBlocked)
            {
                var upDiag = flat.Offset(Direction.Up);
                if (upDiag.Y < EngineConstants.WorldMaxY)
                {
                    var upDiagState = getBlock(upDiag);
                    if (!upDiagState.IsAir && _blocks.GetStateMetadata(upDiagState).BlockKey == BuiltInBlocks.RedstoneWire)
                    {
                        outNeighbors[count++] = upDiag;
                    }
                }
            }

            if (!flatIsOpaque)
            {
                var dnDiag = flat.Offset(Direction.Down);
                if (dnDiag.Y >= EngineConstants.WorldMinY)
                {
                    var dnDiagState = getBlock(dnDiag);
                    if (!dnDiagState.IsAir && _blocks.GetStateMetadata(dnDiagState).BlockKey == BuiltInBlocks.RedstoneWire)
                    {
                        outNeighbors[count++] = dnDiag;
                    }
                }
            }
        }

        return count;
    }

    private int ComputeExternalSourcePowerForWire(BlockPos wirePos, Func<BlockPos, BlockState> getBlock)
    {
        var best = 0;
        var dirs = DirectionExtensions.All;
        for (var i = 0; i < dirs.Length; i++)
        {
            var dir = dirs[i];
            var nPos = wirePos.Offset(dir);
            var nState = getBlock(nPos);
            if (nState.IsAir) continue;
            var meta = _blocks.GetStateMetadata(nState);
            if (meta.BlockKey == BuiltInBlocks.RedstoneWire) continue;

            var sig = GetDirectSourcePower(meta, dir.Opposite());
            if (sig == 0 && meta.IsOpaque && meta.BlockKey != BuiltInBlocks.Piston && meta.BlockKey != BuiltInBlocks.StickyPiston)
            {
                sig = GetStrongPowerIntoSolidBlock(nPos, getBlock);
            }

            if (sig > best) best = sig;
        }

        return best;
    }

    private int GetSignalFromNeighbor(BlockPos neighborPos, Direction directionTowardTarget, Func<BlockPos, BlockState> getBlock)
    {
        var nState = getBlock(neighborPos);
        if (nState.IsAir) return 0;
        var meta = _blocks.GetStateMetadata(nState);
        if (meta.BlockKey == BuiltInBlocks.RedstoneWire)
        {
            if (directionTowardTarget == Direction.Up) return 0;
            var wirePower = ParseInt(meta, "power", 0);
            if (wirePower <= 0 || directionTowardTarget == Direction.Down) return wirePower;

            var (n, s, w, e, _, _, _, _) = RedstoneDiodeAndWireRules.GetWireConnections(_blocks, neighborPos, getBlock);
            var pointsToward = directionTowardTarget switch
            {
                Direction.North => n,
                Direction.South => s,
                Direction.West => w,
                Direction.East => e,
                _ => true,
            };
            var isolatedDot = !n && !s && !w && !e;
            return (pointsToward || isolatedDot) ? wirePower : 0;
        }

        var direct = GetDirectSourcePower(meta, directionTowardTarget);
        if (direct > 0) return direct;

        return meta.IsOpaque && meta.BlockKey != BuiltInBlocks.Piston && meta.BlockKey != BuiltInBlocks.StickyPiston
            ? GetStrongPowerIntoSolidBlock(neighborPos, getBlock)
            : 0;
    }

    private int GetStrongPowerIntoSolidBlock(BlockPos blockPos, Func<BlockPos, BlockState> getBlock)
    {
        var best = 0;
        var belowState = getBlock(blockPos.Offset(Direction.Down));
        if (!belowState.IsAir && _blocks.GetStateMetadata(belowState).BlockKey == BuiltInBlocks.RedstoneTorch)
        {
            return 15;
        }

        var dirs = DirectionExtensions.All;
        for (var i = 0; i < dirs.Length; i++)
        {
            var dir = dirs[i];
            var adj = blockPos.Offset(dir);
            var adjState = getBlock(adj);
            if (adjState.IsAir) continue;
            var m = _blocks.GetStateMetadata(adjState);
            if ((m.BlockKey == BuiltInBlocks.Lever || m.BlockKey == BuiltInBlocks.StoneButton) && IsTrue(m, "powered"))
            {
                var f = BlockAttachmentAndShapeRules.GetFacing(m, "up");
                var attachedTo = f == "up" ? adj.Offset(Direction.Down) : adj.Offset(BlockAttachmentAndShapeRules.ParseDirection(f).Opposite());
                if (attachedTo == blockPos) return 15;
            }
            else if (m.BlockKey == BuiltInBlocks.StonePressurePlate && dir == Direction.Up && IsTrue(m, "powered"))
            {
                return 15;
            }
            else if (m.BlockKey == BuiltInBlocks.RedstoneRepeater && IsTrue(m, "powered"))
            {
                var repDir = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(m, "north"));
                if (adj.Offset(repDir) == blockPos) return 15;
            }
            else if (m.BlockKey == BuiltInBlocks.RedstoneComparator)
            {
                var compDir = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(m, "north"));
                var pow = ParseInt(m, "power", 0);
                if (pow > best && adj.Offset(compDir) == blockPos) best = pow;
            }
        }

        return best;
    }

    private static int GetDirectSourcePower(BlockStateMetadata meta, Direction towardTarget)
    {
        if (meta.BlockKey == BuiltInBlocks.RedstoneBlock) return 15;
        if (meta.BlockKey == BuiltInBlocks.RedstoneTorch)
        {
            var f = BlockAttachmentAndShapeRules.GetFacing(meta, "up");
            var supportDir = f == "up" ? Direction.Down : BlockAttachmentAndShapeRules.ParseDirection(f).Opposite();
            return towardTarget == supportDir ? 0 : 15;
        }

        if ((meta.BlockKey == BuiltInBlocks.Lever || meta.BlockKey == BuiltInBlocks.StoneButton || meta.BlockKey == BuiltInBlocks.StonePressurePlate) &&
            IsTrue(meta, "powered"))
        {
            return 15;
        }

        if (meta.BlockKey == BuiltInBlocks.RedstoneRepeater && IsTrue(meta, "powered"))
        {
            return towardTarget == BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(meta, "north")) ? 15 : 0;
        }

        if (meta.BlockKey == BuiltInBlocks.RedstoneComparator)
        {
            var pow = ParseInt(meta, "power", 0);
            return pow > 0 && towardTarget == BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(meta, "north")) ? pow : 0;
        }

        return 0;
    }

    private void UpdateButtonsAndPressurePlates(
        double dt,
        ReadOnlySpan<Vec3d> entityFeetPositions,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<(BlockPos Pos, RedstoneSoundKind Sound)> soundEvents)
    {
        if (_activeButtons.Count > 0)
        {
            _expiredButtonsScratch.Clear();
            foreach (var (bPos, rem) in _activeButtons)
            {
                if (rem - dt <= 0.0) _expiredButtonsScratch.Add(bPos);
                else _activeButtons[bPos] = rem - dt;
            }

            for (var i = 0; i < _expiredButtonsScratch.Count; i++)
            {
                var p = _expiredButtonsScratch[i];
                _ = _activeButtons.Remove(p);
                var s = getBlock(p);
                if (!s.IsAir && _blocks.GetStateMetadata(s).BlockKey == BuiltInBlocks.StoneButton)
                {
                    setBlock(p, _blocks.WithProperty(s, "powered", "false"));
                    soundEvents.Add((p, RedstoneSoundKind.Click));
                    NotifyBlockChanged(p);
                }
            }
        }

        _steppedPlatesScratch.Clear();
        for (var i = 0; i < entityFeetPositions.Length; i++)
        {
            var bp = entityFeetPositions[i].ToBlockPos();
            var st = getBlock(bp);
            if (!st.IsAir && _blocks.GetStateMetadata(st).BlockKey == BuiltInBlocks.StonePressurePlate)
            {
                _ = _steppedPlatesScratch.Add(bp);
                if (_activePlates.Add(bp))
                {
                    setBlock(bp, _blocks.WithProperty(st, "powered", "true"));
                    soundEvents.Add((bp, RedstoneSoundKind.Click));
                    NotifyBlockChanged(bp);
                }
            }
        }

        if (_activePlates.Count > 0)
        {
            _expiredButtonsScratch.Clear();
            foreach (var plate in _activePlates)
            {
                if (!_steppedPlatesScratch.Contains(plate))
                {
                    _expiredButtonsScratch.Add(plate);
                }
            }

            for (var i = 0; i < _expiredButtonsScratch.Count; i++)
            {
                var p = _expiredButtonsScratch[i];
                _ = _activePlates.Remove(p);
                var s = getBlock(p);
                if (!s.IsAir && _blocks.GetStateMetadata(s).BlockKey == BuiltInBlocks.StonePressurePlate)
                {
                    setBlock(p, _blocks.WithProperty(s, "powered", "false"));
                    soundEvents.Add((p, RedstoneSoundKind.Click));
                    NotifyBlockChanged(p);
                }
            }
        }
    }

    private static bool IsTrue(BlockStateMetadata meta, string prop) =>
        meta.Properties.TryGetValue(prop, out var v) && string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(BlockStateMetadata meta, string prop, int fallback) =>
        meta.Properties.TryGetValue(prop, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
}
