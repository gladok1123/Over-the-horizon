using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;

namespace OverHorizon.Gameplay.Redstone;

/// <summary>
/// Dijkstra <c>0..15</c> <c>RedstoneWire</c> signal solver, directional block power queries, and momentary button/plate timers for <see cref="RedstoneEngine"/>.
/// </summary>
public sealed partial class RedstoneEngine
{
    private void PropagateWireNetwork(HashSet<BlockPos> region, Func<BlockPos, BlockState> getBlock, Action<BlockPos, BlockState> setBlock)
    {
        var wires = new HashSet<BlockPos>();
        var bfs = new Queue<BlockPos>();

        foreach (var p in region.Where(p => _blocks.GetStateMetadata(getBlock(p)).BlockKey == BuiltInBlocks.RedstoneWire && wires.Add(p)))
        {
            bfs.Enqueue(p);
        }

        while (bfs.Count > 0 && wires.Count < 512)
        {
            var cur = bfs.Dequeue();
            foreach (var n in EnumerateConnectedWires(cur, getBlock).Where(wires.Add))
            {
                bfs.Enqueue(n);
            }
        }

        if (wires.Count == 0) return;

        var powerMap = new Dictionary<BlockPos, int>(wires.Count);
        var pq = new PriorityQueue<BlockPos, int>();

        foreach (var w in wires)
        {
            var seedPower = ComputeExternalSourcePowerForWire(w, getBlock);
            powerMap[w] = seedPower;
            if (seedPower > 0) pq.Enqueue(w, -seedPower);
        }

        while (pq.Count > 0)
        {
            var cur = pq.Dequeue();
            var curPow = powerMap[cur];
            if (curPow <= 1) continue;
            var nextPow = curPow - 1;
            foreach (var nw in EnumerateConnectedWires(cur, getBlock))
            {
                if (wires.Contains(nw) && nextPow > powerMap[nw])
                {
                    powerMap[nw] = nextPow;
                    pq.Enqueue(nw, -nextPow);
                }
            }
        }

        foreach (var (wPos, targetPow) in powerMap)
        {
            var curState = getBlock(wPos);
            var curMeta = _blocks.GetStateMetadata(curState);
            if (curMeta.BlockKey == BuiltInBlocks.RedstoneWire && ParseInt(curMeta, "power", 0) != targetPow)
            {
                setBlock(wPos, _blocks.WithProperty(curState, "power", targetPow.ToString(CultureInfo.InvariantCulture)));
                foreach (var dir in DirectionExtensions.All) _ = region.Add(wPos.Offset(dir));
            }
        }
    }

    private IEnumerable<BlockPos> EnumerateConnectedWires(BlockPos pos, Func<BlockPos, BlockState> getBlock)
    {
        var aboveBlocked = _blocks.GetStateMetadata(getBlock(pos.Offset(Direction.Up))).IsOpaque;
        for (var i = 0; i < 4; i++)
        {
            var dir = i switch { 0 => Direction.North, 1 => Direction.South, 2 => Direction.West, _ => Direction.East };
            var flat = pos.Offset(dir);
            if (_blocks.GetStateMetadata(getBlock(flat)).BlockKey == BuiltInBlocks.RedstoneWire) yield return flat;

            if (!aboveBlocked)
            {
                var upDiag = flat.Offset(Direction.Up);
                if (upDiag.Y < EngineConstants.WorldMaxY && _blocks.GetStateMetadata(getBlock(upDiag)).BlockKey == BuiltInBlocks.RedstoneWire)
                {
                    yield return upDiag;
                }
            }

            if (!_blocks.GetStateMetadata(getBlock(flat)).IsOpaque)
            {
                var dnDiag = flat.Offset(Direction.Down);
                if (dnDiag.Y >= EngineConstants.WorldMinY && _blocks.GetStateMetadata(getBlock(dnDiag)).BlockKey == BuiltInBlocks.RedstoneWire)
                {
                    yield return dnDiag;
                }
            }
        }
    }

    private int ComputeExternalSourcePowerForWire(BlockPos wirePos, Func<BlockPos, BlockState> getBlock)
    {
        var best = 0;
        foreach (var dir in DirectionExtensions.All)
        {
            var nPos = wirePos.Offset(dir);
            var nState = getBlock(nPos);
            if (nState.IsAir) continue;
            var meta = _blocks.GetStateMetadata(nState);
            if (meta.BlockKey == BuiltInBlocks.RedstoneWire) continue;

            var sig = GetDirectSourcePower(meta, dir.Opposite());
            if (sig == 0 && meta.IsSolid && meta.BlockKey != BuiltInBlocks.Piston && meta.BlockKey != BuiltInBlocks.StickyPiston)
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

        return meta.IsSolid && meta.BlockKey != BuiltInBlocks.Piston && meta.BlockKey != BuiltInBlocks.StickyPiston
            ? GetStrongPowerIntoSolidBlock(neighborPos, getBlock)
            : 0;
    }

    private int GetStrongPowerIntoSolidBlock(BlockPos blockPos, Func<BlockPos, BlockState> getBlock)
    {
        var best = 0;
        if (_blocks.GetStateMetadata(getBlock(blockPos.Offset(Direction.Down))).BlockKey == BuiltInBlocks.RedstoneTorch)
        {
            return 15;
        }

        foreach (var dir in DirectionExtensions.All)
        {
            var adj = blockPos.Offset(dir);
            var m = _blocks.GetStateMetadata(getBlock(adj));
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
            var expired = new List<BlockPos>();
            foreach (var (bPos, rem) in _activeButtons)
            {
                if (rem - dt <= 0.0) expired.Add(bPos);
                else _activeButtons[bPos] = rem - dt;
            }

            for (var i = 0; i < expired.Count; i++)
            {
                var p = expired[i];
                _ = _activeButtons.Remove(p);
                var s = getBlock(p);
                if (_blocks.GetStateMetadata(s).BlockKey == BuiltInBlocks.StoneButton)
                {
                    setBlock(p, _blocks.WithProperty(s, "powered", "false"));
                    soundEvents.Add((p, RedstoneSoundKind.Click));
                    NotifyBlockChanged(p);
                }
            }
        }

        var steppedPlates = new HashSet<BlockPos>();
        for (var i = 0; i < entityFeetPositions.Length; i++)
        {
            var bp = entityFeetPositions[i].ToBlockPos();
            var st = getBlock(bp);
            if (_blocks.GetStateMetadata(st).BlockKey == BuiltInBlocks.StonePressurePlate)
            {
                _ = steppedPlates.Add(bp);
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
            var released = _activePlates.Where(plate => !steppedPlates.Contains(plate)).ToArray();
            for (var i = 0; i < released.Length; i++)
            {
                var p = released[i];
                _ = _activePlates.Remove(p);
                var s = getBlock(p);
                if (_blocks.GetStateMetadata(s).BlockKey == BuiltInBlocks.StonePressurePlate)
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
