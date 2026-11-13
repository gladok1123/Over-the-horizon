using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;

namespace OverHorizon.Gameplay.Redstone;

/// <summary>
/// Clean-room Redstone signal propagation (<c>0..15</c>), <c>RedstoneTorch</c> inverter, <c>RedstoneRepeater</c> diode,
/// <c>Lever</c> / <c>StoneButton</c> / <c>StonePressurePlate</c> inputs, <c>RedstoneLamp</c>, Pistons, and Redstone <c>TNT</c> ignition.
/// </summary>
public sealed class RedstoneEngine
{
    /// <summary>Maximum number of blocks a Piston or Sticky Piston can push.</summary>
    public const int MaxPistonPushCount = PistonMechanics.MaxPistonPushCount;

    private readonly IBlockRegistry _blocks;
    private readonly HashSet<BlockPos> _dirtyNodes = [];
    private readonly Dictionary<BlockPos, double> _activeButtons = [];
    private readonly HashSet<BlockPos> _activePlates = [];
    private double _redstoneTickAccumulator;

    /// <summary>Initializes a new instance of the <see cref="RedstoneEngine"/> class.</summary>
    public RedstoneEngine(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        _blocks = blocks;
    }

    /// <summary>Marks <paramref name="pos"/> and its neighborhood for Redstone evaluation.</summary>
    public void NotifyBlockChanged(BlockPos pos)
    {
        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                for (var dz = -2; dz <= 2; dz++)
                {
                    var p = new BlockPos(pos.X + dx, pos.Y + dy, pos.Z + dz);
                    if (p.Y >= EngineConstants.WorldMinY && p.Y < EngineConstants.WorldMaxY)
                    {
                        _ = _dirtyNodes.Add(p);
                    }
                }
            }
        }
    }

    /// <summary>Toggles a <c>Lever</c>, presses a <c>StoneButton</c>, or cycles a <c>RedstoneRepeater</c> delay (<c>1..4</c>).</summary>
    public bool TryInteractComponent(BlockPos pos, Func<BlockPos, BlockState> getBlock, Action<BlockPos, BlockState> setBlock)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        var cur = getBlock(pos);
        if (cur.IsAir) return false;
        var meta = _blocks.GetStateMetadata(cur);

        if (meta.BlockKey == BuiltInBlocks.Lever)
        {
            var nextPow = !IsTrue(meta, "powered");
            setBlock(pos, _blocks.WithProperty(cur, "powered", nextPow ? "true" : "false"));
            NotifyBlockChanged(pos);
            return true;
        }

        if (meta.BlockKey == BuiltInBlocks.StoneButton)
        {
            setBlock(pos, _blocks.WithProperty(cur, "powered", "true"));
            _activeButtons[pos] = 1.0;
            NotifyBlockChanged(pos);
            return true;
        }

        if (meta.BlockKey == BuiltInBlocks.RedstoneRepeater)
        {
            var nextDelay = (ParseInt(meta, "delay", 1) % 4) + 1;
            setBlock(pos, _blocks.WithProperty(cur, "delay", nextDelay.ToString(CultureInfo.InvariantCulture)));
            NotifyBlockChanged(pos);
            return true;
        }

        return false;
    }

    /// <summary>Advances button timers, pressure plates, wire signal propagation, torches, repeaters, lamps, pistons, and TNT triggers.</summary>
    public void Step(
        double deltaSeconds,
        ReadOnlySpan<Vec3d> entityFeetPositions,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<BlockPos> ignitedTntPositions,
        List<(BlockPos Pos, RedstoneSoundKind Sound)> soundEvents)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);
        ArgumentNullException.ThrowIfNull(ignitedTntPositions);
        ArgumentNullException.ThrowIfNull(soundEvents);

        UpdateButtonsAndPressurePlates(deltaSeconds, entityFeetPositions, getBlock, setBlock, soundEvents);

        _redstoneTickAccumulator += Math.Clamp(deltaSeconds, 0.0, 0.25);
        if (_redstoneTickAccumulator < 0.05 && _dirtyNodes.Count == 0) return;

        _redstoneTickAccumulator = 0.0;
        if (_dirtyNodes.Count > 0)
        {
            EvaluateImmediate(getBlock, setBlock, ignitedTntPositions, soundEvents);
        }
    }

    /// <summary>Runs a deterministic Redstone &amp; Piston convergence pass immediately.</summary>
    public void EvaluateImmediate(
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<BlockPos> ignitedTntPositions,
        List<(BlockPos Pos, RedstoneSoundKind Sound)> soundEvents)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);
        ArgumentNullException.ThrowIfNull(ignitedTntPositions);
        ArgumentNullException.ThrowIfNull(soundEvents);

        for (var pass = 0; pass < 3 && _dirtyNodes.Count > 0; pass++)
        {
            var region = new HashSet<BlockPos>(_dirtyNodes);
            _dirtyNodes.Clear();

            PropagateWireNetwork(region, getBlock, setBlock);

            var snapshot = region.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                var pos = snapshot[i];
                var state = getBlock(pos);
                if (state.IsAir) continue;

                var meta = _blocks.GetStateMetadata(state);
                var key = meta.BlockKey;

                if (key == BuiltInBlocks.RedstoneTorch || key == BuiltInBlocks.RedstoneTorchOff)
                {
                    var facing = BlockAttachmentAndShapeRules.GetFacing(meta, "up");
                    var supportDir = facing == "up" ? Direction.Down : BlockAttachmentAndShapeRules.ParseDirection(facing).Opposite();
                    var supportPowered = GetPowerReceivedAt(pos.Offset(supportDir), getBlock, ignoreSource: pos) > 0;
                    if (key == BuiltInBlocks.RedstoneTorch && supportPowered)
                    {
                        setBlock(pos, _blocks.WithProperty(_blocks.GetDefaultState(BuiltInBlocks.RedstoneTorchOff), "facing", facing));
                        soundEvents.Add((pos, RedstoneSoundKind.TorchFizz));
                        NotifyBlockChanged(pos);
                    }
                    else if (key == BuiltInBlocks.RedstoneTorchOff && !supportPowered)
                    {
                        setBlock(pos, _blocks.WithProperty(_blocks.GetDefaultState(BuiltInBlocks.RedstoneTorch), "facing", facing));
                        NotifyBlockChanged(pos);
                    }
                }
                else if (key == BuiltInBlocks.RedstoneRepeater)
                {
                    var facingDir = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(meta, "north"));
                    var shouldPower = GetSignalFromNeighbor(pos.Offset(facingDir.Opposite()), facingDir, getBlock) > 0;
                    if (IsTrue(meta, "powered") != shouldPower)
                    {
                        setBlock(pos, _blocks.WithProperty(state, "powered", shouldPower ? "true" : "false"));
                        NotifyBlockChanged(pos.Offset(facingDir));
                    }
                }
                else if (key == BuiltInBlocks.RedstoneLamp || key == BuiltInBlocks.LitRedstoneLamp)
                {
                    var powered = GetPowerReceivedAt(pos, getBlock, ignoreSource: null) > 0;
                    if (powered && key == BuiltInBlocks.RedstoneLamp) setBlock(pos, _blocks.GetDefaultState(BuiltInBlocks.LitRedstoneLamp));
                    else if (!powered && key == BuiltInBlocks.LitRedstoneLamp) setBlock(pos, _blocks.GetDefaultState(BuiltInBlocks.RedstoneLamp));
                }
                else if (key == BuiltInBlocks.Piston || key == BuiltInBlocks.StickyPiston)
                {
                    PistonMechanics.EvaluatePiston(_blocks, this, pos, state, meta, getBlock, setBlock, soundEvents);
                }
                else if (key == BuiltInBlocks.Tnt && GetPowerReceivedAt(pos, getBlock, ignoreSource: null) > 0)
                {
                    setBlock(pos, BlockState.Air);
                    ignitedTntPositions.Add(pos);
                }
            }
        }
    }

    /// <summary>Computes the <c>0..15</c> Redstone signal received by the block at <paramref name="targetPos"/>.</summary>
    public int GetPowerReceivedAt(BlockPos targetPos, Func<BlockPos, BlockState> getBlock, BlockPos? ignoreSource = null)
    {
        var best = 0;
        foreach (var dir in DirectionExtensions.All)
        {
            var nPos = targetPos.Offset(dir);
            if (ignoreSource == nPos) continue;
            var sig = GetSignalFromNeighbor(nPos, dir.Opposite(), getBlock);
            if (sig > best) best = sig;
        }

        return best;
    }

    private void PropagateWireNetwork(HashSet<BlockPos> region, Func<BlockPos, BlockState> getBlock, Action<BlockPos, BlockState> setBlock)
    {
        var wires = new HashSet<BlockPos>();
        var bfs = new Queue<BlockPos>();

        foreach (var p in region)
        {
            if (_blocks.GetStateMetadata(getBlock(p)).BlockKey == BuiltInBlocks.RedstoneWire && wires.Add(p)) bfs.Enqueue(p);
        }

        while (bfs.Count > 0 && wires.Count < 512)
        {
            var cur = bfs.Dequeue();
            foreach (var n in EnumerateConnectedWires(cur, getBlock))
            {
                if (wires.Add(n)) bfs.Enqueue(n);
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
        foreach (var dir in DirectionExtensions.Horizontal)
        {
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
        if (meta.BlockKey == BuiltInBlocks.RedstoneWire) return directionTowardTarget == Direction.Up ? 0 : ParseInt(meta, "power", 0);

        var direct = GetDirectSourcePower(meta, directionTowardTarget);
        if (direct > 0) return direct;

        return meta.IsSolid && meta.BlockKey != BuiltInBlocks.Piston && meta.BlockKey != BuiltInBlocks.StickyPiston
            ? GetStrongPowerIntoSolidBlock(neighborPos, getBlock)
            : 0;
    }

    private int GetStrongPowerIntoSolidBlock(BlockPos blockPos, Func<BlockPos, BlockState> getBlock)
    {
        if (_blocks.GetStateMetadata(getBlock(blockPos.Offset(Direction.Down))).BlockKey == BuiltInBlocks.RedstoneTorch) return 15;

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
        }

        return 0;
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
