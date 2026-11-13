using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;

namespace OverHorizon.Gameplay.Redstone;

/// <summary>
/// Clean-room Redstone signal propagation (<c>0..15</c>), <c>RedstoneTorch</c> inverter, scheduled <c>RedstoneRepeater</c>
/// (with <c>1..4</c> tick delay &amp; side-locking), analog <c>RedstoneComparator</c> (compare &amp; subtract modes),
/// <c>Lever</c> / <c>StoneButton</c> / <c>StonePressurePlate</c> inputs, <c>RedstoneLamp</c>, <c>OakTrapdoor</c>, Pistons, Powered Rails, and <c>TNT</c>.
/// </summary>
public sealed partial class RedstoneEngine
{
    /// <summary>Maximum number of blocks a Piston or Sticky Piston can push.</summary>
    public const int MaxPistonPushCount = PistonMechanics.MaxPistonPushCount;

    private readonly IBlockRegistry _blocks;
    private readonly HashSet<BlockPos> _dirtyNodes = [];
    private readonly Dictionary<BlockPos, double> _activeButtons = [];
    private readonly Dictionary<BlockPos, (double RemainingSeconds, int TargetPower)> _scheduledDiodes = [];
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

    /// <summary>Toggles a <c>Lever</c>, presses a <c>StoneButton</c>, cycles a <c>RedstoneRepeater</c> delay (<c>1..4</c>), or switches a <c>RedstoneComparator</c> mode.</summary>
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

        if (meta.BlockKey == BuiltInBlocks.RedstoneComparator)
        {
            var nextSub = !IsTrue(meta, "subtract");
            setBlock(pos, _blocks.WithProperty(cur, "subtract", nextSub ? "true" : "false"));
            NotifyBlockChanged(pos);
            return true;
        }

        return false;
    }

    /// <summary>Advances button timers, pressure plates, scheduled repeater/comparator ticks, wire propagation, pistons, rails, trapdoors, and TNT.</summary>
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

        var clampedDt = Math.Clamp(deltaSeconds, 0.0, 0.25);
        UpdateButtonsAndPressurePlates(clampedDt, entityFeetPositions, getBlock, setBlock, soundEvents);
        AdvanceScheduledDiodes(clampedDt, getBlock, setBlock);

        _redstoneTickAccumulator += clampedDt;
        if (_redstoneTickAccumulator < 0.05 && _dirtyNodes.Count == 0) return;

        _redstoneTickAccumulator = 0.0;
        if (_dirtyNodes.Count > 0)
        {
            EvaluateCore(getBlock, setBlock, ignitedTntPositions, soundEvents, useTimedDiodeQueue: true);
        }
    }

    /// <summary>Runs a deterministic Redstone &amp; Piston convergence pass immediately (including instant diode convergence for synchronous callers).</summary>
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

        EvaluateCore(getBlock, setBlock, ignitedTntPositions, soundEvents, useTimedDiodeQueue: false);
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

    internal int GetDirectOrWireSignalFromNeighbor(BlockPos neighborPos, Direction directionTowardTarget, Func<BlockPos, BlockState> getBlock)
    {
        var nState = getBlock(neighborPos);
        if (nState.IsAir) return 0;
        var meta = _blocks.GetStateMetadata(nState);
        if (meta.BlockKey == BuiltInBlocks.RedstoneWire)
        {
            return directionTowardTarget == Direction.Up ? 0 : ParseInt(meta, "power", 0);
        }

        return GetDirectSourcePower(meta, directionTowardTarget);
    }

    private void EvaluateCore(
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<BlockPos> ignitedTntPositions,
        List<(BlockPos Pos, RedstoneSoundKind Sound)> soundEvents,
        bool useTimedDiodeQueue)
    {
        for (var pass = 0; pass < 6 && _dirtyNodes.Count > 0; pass++)
        {
            var region = new HashSet<BlockPos>(_dirtyNodes);
            _dirtyNodes.Clear();

            PropagateWireNetwork(region, getBlock, setBlock);

            var snapshot = region.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                EvaluateComponentAt(snapshot[i], getBlock, setBlock, ignitedTntPositions, soundEvents, useTimedDiodeQueue);
            }
        }
    }

    private void EvaluateComponentAt(
        BlockPos pos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<BlockPos> ignitedTntPositions,
        List<(BlockPos Pos, RedstoneSoundKind Sound)> soundEvents,
        bool useTimedDiodeQueue)
    {
        var state = getBlock(pos);
        if (state.IsAir) return;

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
            EvaluateRepeater(pos, state, meta, getBlock, setBlock, useTimedDiodeQueue);
        }
        else if (key == BuiltInBlocks.RedstoneComparator)
        {
            EvaluateComparator(pos, state, meta, getBlock, setBlock, useTimedDiodeQueue);
        }
        else if (key == BuiltInBlocks.RedstoneLamp || key == BuiltInBlocks.LitRedstoneLamp)
        {
            var powered = GetPowerReceivedAt(pos, getBlock, ignoreSource: null) > 0;
            if (powered && key == BuiltInBlocks.RedstoneLamp) setBlock(pos, _blocks.GetDefaultState(BuiltInBlocks.LitRedstoneLamp));
            else if (!powered && key == BuiltInBlocks.LitRedstoneLamp) setBlock(pos, _blocks.GetDefaultState(BuiltInBlocks.RedstoneLamp));
        }
        else if (key == BuiltInBlocks.OakTrapdoor)
        {
            var powered = GetPowerReceivedAt(pos, getBlock, ignoreSource: null) > 0;
            if (IsTrue(meta, "powered") != powered)
            {
                var next = _blocks.WithProperty(_blocks.WithProperty(state, "powered", powered ? "true" : "false"), "open", powered ? "true" : "false");
                setBlock(pos, next);
                soundEvents.Add((pos, RedstoneSoundKind.Click));
            }
        }
        else if (key == BuiltInBlocks.Piston || key == BuiltInBlocks.StickyPiston)
        {
            PistonMechanics.EvaluatePiston(_blocks, this, pos, state, meta, getBlock, setBlock, soundEvents);
        }
        else if (key == BuiltInBlocks.PoweredRail)
        {
            var shouldPowerRail = RailPlacementRules.IsPoweredRailEnergized(_blocks, this, getBlock, pos);
            if (IsTrue(meta, "powered") != shouldPowerRail)
            {
                setBlock(pos, _blocks.WithProperty(state, "powered", shouldPowerRail ? "true" : "false"));
                NotifyBlockChanged(pos);
            }
        }
        else if (key == BuiltInBlocks.Tnt && GetPowerReceivedAt(pos, getBlock, ignoreSource: null) > 0)
        {
            setBlock(pos, BlockState.Air);
            ignitedTntPositions.Add(pos);
        }
    }

    private void EvaluateRepeater(
        BlockPos pos,
        BlockState state,
        BlockStateMetadata meta,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        bool useTimedDiodeQueue)
    {
        var facingDir = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(meta, "north"));
        var locked = RedstoneDiodeAndWireRules.IsRepeaterSideLocked(_blocks, pos, facingDir, getBlock);
        var currentState = state;
        if (IsTrue(meta, "locked") != locked)
        {
            currentState = _blocks.WithProperty(currentState, "locked", locked ? "true" : "false");
            setBlock(pos, currentState);
        }

        if (locked)
        {
            _ = _scheduledDiodes.Remove(pos);
            return;
        }

        var shouldPower = RedstoneDiodeAndWireRules.ComputeDiodeRearInput(_blocks, this, pos, facingDir, isComparator: false, getBlock) > 0;
        if (IsTrue(meta, "powered") == shouldPower)
        {
            return;
        }

        if (useTimedDiodeQueue)
        {
            if (!_scheduledDiodes.ContainsKey(pos))
            {
                var delaySec = Math.Clamp(ParseInt(meta, "delay", 1), 1, 4) * 0.10;
                _scheduledDiodes[pos] = (delaySec, shouldPower ? 15 : 0);
            }
        }
        else
        {
            setBlock(pos, _blocks.WithProperty(currentState, "powered", shouldPower ? "true" : "false"));
            NotifyBlockChanged(pos.Offset(facingDir));
        }
    }

    private void EvaluateComparator(
        BlockPos pos,
        BlockState state,
        BlockStateMetadata meta,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        bool useTimedDiodeQueue)
    {
        var facingDir = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(meta, "north"));
        var targetPower = RedstoneDiodeAndWireRules.ComputeComparatorOutput(_blocks, this, pos, meta, getBlock);
        if (ParseInt(meta, "power", 0) == targetPower)
        {
            return;
        }

        if (useTimedDiodeQueue)
        {
            if (!_scheduledDiodes.ContainsKey(pos))
            {
                _scheduledDiodes[pos] = (0.10, targetPower);
            }
        }
        else
        {
            setBlock(pos, _blocks.WithProperty(state, "power", targetPower.ToString(CultureInfo.InvariantCulture)));
            NotifyBlockChanged(pos.Offset(facingDir));
        }
    }

    private void AdvanceScheduledDiodes(double dt, Func<BlockPos, BlockState> getBlock, Action<BlockPos, BlockState> setBlock)
    {
        if (_scheduledDiodes.Count == 0) return;

        var keys = _scheduledDiodes.Keys.ToArray();
        for (var i = 0; i < keys.Length; i++)
        {
            var pos = keys[i];
            var (rem, targetPow) = _scheduledDiodes[pos];
            var nextRem = rem - dt;
            if (nextRem > 0.0)
            {
                _scheduledDiodes[pos] = (nextRem, targetPow);
                continue;
            }

            _ = _scheduledDiodes.Remove(pos);
            var cur = getBlock(pos);
            if (cur.IsAir) continue;
            var meta = _blocks.GetStateMetadata(cur);
            var facingDir = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(meta, "north"));

            if (meta.BlockKey == BuiltInBlocks.RedstoneRepeater)
            {
                if (RedstoneDiodeAndWireRules.IsRepeaterSideLocked(_blocks, pos, facingDir, getBlock)) continue;
                var liveRear = RedstoneDiodeAndWireRules.ComputeDiodeRearInput(_blocks, this, pos, facingDir, isComparator: false, getBlock) > 0;
                var nextPow = targetPow > 0 || liveRear;
                if (targetPow == 0) nextPow = liveRear;
                setBlock(pos, _blocks.WithProperty(cur, "powered", nextPow ? "true" : "false"));
                NotifyBlockChanged(pos);
                NotifyBlockChanged(pos.Offset(facingDir));
            }
            else if (meta.BlockKey == BuiltInBlocks.RedstoneComparator)
            {
                var liveOut = RedstoneDiodeAndWireRules.ComputeComparatorOutput(_blocks, this, pos, meta, getBlock);
                setBlock(pos, _blocks.WithProperty(cur, "power", liveOut.ToString(CultureInfo.InvariantCulture)));
                NotifyBlockChanged(pos);
                NotifyBlockChanged(pos.Offset(facingDir));
            }
        }
    }
}
