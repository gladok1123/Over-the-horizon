using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Physics;

/// <summary>
/// Smooth <see cref="FallingBlockEntity"/> physics, batched zero-lag fluid stepping, bounded <c>Fire</c> aging/spread,
/// attachment cascading, and grass spread/decay routines for <see cref="PrimedTntAndWorldTicker"/>.
/// </summary>
public sealed partial class PrimedTntAndWorldTicker
{
    private const int MaxFallingBlockEntities = 96;
    private const int MaxGravityDetachmentsPerStep = 24;
    private const int MaxFluidUpdatesPerStep = 48;
    private const double FallingBlockGravity = 24.0;

    private int DetachDestabilizedGravityBlocks(
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock)
    {
        var snapshot = _activeGravityBlocks.OrderBy(static p => p.Y).ToArray();
        _activeGravityBlocks.Clear();
        var spawned = 0;

        for (var i = 0; i < snapshot.Length; i++)
        {
            var pos = snapshot[i];
            if (pos.Y <= EngineConstants.WorldMinY) continue;

            var cur = getBlock(pos);
            if (!IsGravityAffected(cur)) continue;

            var belowState = getBlock(pos.Offset(Direction.Down));
            if (!CanGravityFallInto(belowState)) continue;

            if (spawned >= MaxGravityDetachmentsPerStep || _fallingBlocks.Count >= MaxFallingBlockEntities)
            {
                _ = _activeGravityBlocks.Add(pos);
                continue;
            }

            setBlock(pos, BlockState.Air);
            _fallingBlocks.Add(new FallingBlockEntity(_nextFallingId++, cur, pos));
            EnqueueFrontierNeighbors(pos, getBlock);
            spawned++;
        }

        return spawned;
    }

    private void StepFallingBlockEntities(
        double dt,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<(BlockPos Pos, BlockState BrokenState)>? brokenFragileDrops)
    {
        for (var i = _fallingBlocks.Count - 1; i >= 0; i--)
        {
            var fb = _fallingBlocks[i];
            fb.VelocityY = Math.Max(-36.0, fb.VelocityY - (FallingBlockGravity * dt));
            var nextY = fb.CurrentY + (fb.VelocityY * dt);

            if (nextY < EngineConstants.WorldMinY - 16)
            {
                _fallingBlocks.RemoveAt(i);
                continue;
            }

            var currentCellY = (int)Math.Floor(fb.CurrentY + 0.001);
            var targetCellY = (int)Math.Floor(nextY);
            var landed = false;

            for (var checkY = currentCellY; checkY >= targetCellY; checkY--)
            {
                if (checkY <= EngineConstants.WorldMinY)
                {
                    PlaceOrDropFallingBlock(fb, new BlockPos(fb.BlockX, EngineConstants.WorldMinY, fb.BlockZ), getBlock, setBlock, brokenFragileDrops);
                    landed = true;
                    break;
                }

                var belowPos = new BlockPos(fb.BlockX, checkY - 1, fb.BlockZ);
                var belowState = getBlock(belowPos);
                if (!CanGravityFallInto(belowState))
                {
                    PlaceOrDropFallingBlock(fb, new BlockPos(fb.BlockX, checkY, fb.BlockZ), getBlock, setBlock, brokenFragileDrops);
                    landed = true;
                    break;
                }
            }

            if (landed)
            {
                _fallingBlocks.RemoveAt(i);
            }
            else
            {
                fb.CurrentY = nextY;
            }
        }
    }

    private void PlaceOrDropFallingBlock(
        FallingBlockEntity fb,
        BlockPos landPos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<(BlockPos Pos, BlockState BrokenState)>? brokenFragileDrops)
    {
        var existing = getBlock(landPos);
        var existingMeta = _blocks.GetStateMetadata(existing);

        // Vanilla rule: Sand/Gravel landing on a non-solid fragile block (Torch, Rail, Repeater, Wire) pops into a dropped item!
        if (!existing.IsAir && !existingMeta.IsFluid && existingMeta.BlockKey != BuiltInBlocks.Fire && !existingMeta.IsSolid)
        {
            brokenFragileDrops?.Add((landPos, fb.State));
            return;
        }

        setBlock(landPos, fb.State);
        EnqueueFrontierNeighbors(landPos, getBlock);
    }

    private int StepFluidFrontier(
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        Action<IReadOnlyList<(BlockPos Pos, BlockState State)>>? setBlockBatch,
        List<BlockPos>? extinguishedFirePositions)
    {
        var candidates = _activeFluidFrontier
            .Where(collisionWorld.IsColumnLoaded)
            .Take(96)
            .ToArray();
        if (candidates.Length == 0)
        {
            return 0;
        }

        for (var i = 0; i < candidates.Length; i++)
        {
            _ = _activeFluidFrontier.Remove(candidates[i]);
        }

        var updates = FluidSimulator.EvaluateStep(collisionWorld, candidates);
        if (updates.Count == 0)
        {
            return 0;
        }

        var baseWater = _blocks.GetDefaultState(BuiltInBlocks.Water);
        var count = Math.Min(updates.Count, MaxFluidUpdatesPerStep);
        var batch = new List<(BlockPos Pos, BlockState State)>(count);
        for (var i = 0; i < count; i++)
        {
            var change = updates[i];
            var pos = change.Position;
            var nextState = change.NewLevel == FluidChange.RemoveFluidSentinel
                ? BlockState.Air
                : _blocks.WithProperty(baseWater, "level", change.NewLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var prev = getBlock(pos);
            if (_blocks.GetStateMetadata(prev).BlockKey == BuiltInBlocks.Fire)
            {
                _ = _activeFires.Remove(pos);
                extinguishedFirePositions?.Add(pos);
            }

            batch.Add((pos, nextState));
        }

        ApplyMutations(batch, setBlock, setBlockBatch);

        for (var i = 0; i < batch.Count; i++)
        {
            EnqueueFluidOnlyNeighbors(batch[i].Pos);
        }

        for (var i = count; i < updates.Count; i++)
        {
            _ = _activeFluidFrontier.Add(updates[i].Position);
        }

        return batch.Count;
    }

    private void StepBoundedFireFrontier(
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<BlockPos>? extinguishedFirePositions)
    {
        var fires = _activeFires.Take(48).ToArray();
        for (var i = 0; i < fires.Length; i++)
        {
            var pos = fires[i];
            var cur = getBlock(pos);
            var meta = _blocks.GetStateMetadata(cur);
            if (meta.BlockKey != BuiltInBlocks.Fire)
            {
                _ = _activeFires.Remove(pos);
                continue;
            }

            // Extinguish immediately if adjacent to Water or missing floor support!
            var touchesWater = false;
            foreach (var dir in DirectionExtensions.All)
            {
                if (_blocks.GetStateMetadata(getBlock(pos.Offset(dir))).IsFluid)
                {
                    touchesWater = true;
                    break;
                }
            }

            var belowKey = _blocks.GetStateMetadata(getBlock(pos.Offset(Direction.Down))).BlockKey;
            if (touchesWater || belowKey == BuiltInBlocks.Air)
            {
                setBlock(pos, BlockState.Air);
                _ = _activeFires.Remove(pos);
                if (touchesWater) extinguishedFirePositions?.Add(pos);
                continue;
            }

            // Eternal flame on Obsidian or Bedrock (never spreads endlessly, stays warm!)
            if (belowKey == BuiltInBlocks.Obsidian || belowKey == BuiltInBlocks.Bedrock)
            {
                continue;
            }

            var age = meta.Properties.TryGetValue("age", out var ageStr) && int.TryParse(ageStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
            var nextAge = age + 2 + (int)(NextRandom() & 1U);

            // Controlled bounded spread: only young fire (age <= 7) has a small chance to hop 1 cell with child age >= 9!
            if (age <= 7 && (NextRandom() % 5U) == 0U && _activeFires.Count < 24)
            {
                var offX = (int)(NextRandom() % 3U) - 1;
                var offY = (int)(NextRandom() % 2U);
                var offZ = (int)(NextRandom() % 3U) - 1;
                var target = new BlockPos(pos.X + offX, pos.Y + offY, pos.Z + offZ);
                if (target != pos && target.Y >= EngineConstants.WorldMinY + 1 && target.Y < EngineConstants.WorldMaxY - 1 &&
                    getBlock(target).IsAir && HasAdjacentFlammable(target, getBlock) &&
                    _blocks.GetStateMetadata(getBlock(target.Offset(Direction.Down))).IsSolid)
                {
                    var childAge = Math.Min(15, age + 6);
                    var childFire = _blocks.WithProperty(_blocks.GetDefaultState(BuiltInBlocks.Fire), "age", childAge.ToString(CultureInfo.InvariantCulture));
                    setBlock(target, childFire);
                    _ = _activeFires.Add(target);
                }
            }

            // Rarely consume directly underlying flammable block when fire reaches maturity
            if (nextAge >= 14 && IsFlammableBlock(belowKey) && (NextRandom() % 4U) == 0U)
            {
                setBlock(pos.Offset(Direction.Down), BlockState.Air);
            }

            if (nextAge >= 15)
            {
                setBlock(pos, BlockState.Air);
                _ = _activeFires.Remove(pos);
            }
            else
            {
                setBlock(pos, _blocks.WithProperty(cur, "age", nextAge.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    private bool HasAdjacentFlammable(BlockPos pos, Func<BlockPos, BlockState> getBlock)
    {
        var dirs = DirectionExtensions.All;
        for (var i = 0; i < dirs.Length; i++)
        {
            if (IsFlammableBlock(_blocks.GetStateMetadata(getBlock(pos.Offset(dirs[i]))).BlockKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFlammableBlock(ResourceKey key) =>
        key == BuiltInBlocks.OakPlanks || key == BuiltInBlocks.BirchPlanks ||
        key == BuiltInBlocks.OakLog || key == BuiltInBlocks.BirchLog ||
        key == BuiltInBlocks.CherryLog || key == BuiltInBlocks.SpruceLog ||
        key == BuiltInBlocks.OakLeaves || key == BuiltInBlocks.BirchLeaves ||
        key == BuiltInBlocks.CherryLeaves || key == BuiltInBlocks.SpruceLeaves ||
        key == BuiltInBlocks.OakFence || key == BuiltInBlocks.Bookshelf ||
        key == BuiltInBlocks.HayBlock || key == BuiltInBlocks.CrimsonWool ||
        key == BuiltInBlocks.WhiteWool || key == BuiltInBlocks.BlueWool;

    private readonly Queue<BlockPos> _cascadeQueueScratch = new();

    private int StepGrassSpreadAndDecay(
        Vec3d center,
        Func<BlockPos, BlockState> getBlock,
        Func<BlockPos, (byte Sky, byte Block)> getLight,
        Action<BlockPos, BlockState> setBlock,
        int sampleCount)
    {
        var mutations = 0;
        var baseX = (int)Math.Floor(center.X);
        var baseY = (int)Math.Floor(center.Y);
        var baseZ = (int)Math.Floor(center.Z);

        var grassDefault = _blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        var dirtDefault = _blocks.GetDefaultState(BuiltInBlocks.Dirt);

        for (var s = 0; s < sampleCount; s++)
        {
            var rx = baseX + (int)(NextRandom() % 33U) - 16;
            var ry = Math.Clamp(baseY + (int)(NextRandom() % 17U) - 8, EngineConstants.WorldMinY + 1, EngineConstants.WorldMaxY - 2);
            var rz = baseZ + (int)(NextRandom() % 33U) - 16;
            var pos = new BlockPos(rx, ry, rz);

            var state = getBlock(pos);
            if (state.IsAir) continue;

            var meta = _blocks.GetStateMetadata(state);
            if (meta.BlockKey == BuiltInBlocks.BuddingAmethyst)
            {
                var dirs = DirectionExtensions.All;
                var growDir = dirs[NextRandom() % (uint)dirs.Length];
                var crystalPos = pos.Offset(growDir);
                if (crystalPos.Y >= EngineConstants.WorldMinY && crystalPos.Y < EngineConstants.WorldMaxY && getBlock(crystalPos).IsAir)
                {
                    var clusterState = _blocks.WithProperty(
                        _blocks.GetDefaultState(BuiltInBlocks.AmethystCluster),
                        "facing",
                        BlockAttachmentAndShapeRules.DirectionName(growDir));
                    setBlock(crystalPos, clusterState);
                    mutations++;
                }

                continue;
            }

            if (meta.BlockKey == BuiltInBlocks.DirtPath)
            {
                var pathAbove = _blocks.GetStateMetadata(getBlock(pos.Offset(Direction.Up)));
                if (pathAbove.IsOpaque || pathAbove.IsFluid)
                {
                    setBlock(pos, dirtDefault);
                    mutations++;
                }

                continue;
            }

            if (meta.BlockKey != BuiltInBlocks.GrassBlock) continue;

            var abovePos = pos.Offset(Direction.Up);
            var aboveMeta = _blocks.GetStateMetadata(getBlock(abovePos));
            if (aboveMeta.IsOpaque || aboveMeta.IsFluid)
            {
                setBlock(pos, dirtDefault);
                mutations++;
                continue;
            }

            var (sky, blk) = getLight(abovePos);
            if (Math.Max(sky, blk) < 9) continue;

            var tx = pos.X + (int)(NextRandom() % 3U) - 1;
            var ty = Math.Clamp(pos.Y + (int)(NextRandom() % 5U) - 3, EngineConstants.WorldMinY + 1, EngineConstants.WorldMaxY - 2);
            var tz = pos.Z + (int)(NextRandom() % 3U) - 1;
            var target = new BlockPos(tx, ty, tz);

            if (_blocks.GetStateMetadata(getBlock(target)).BlockKey != BuiltInBlocks.Dirt) continue;

            var targetAbove = target.Offset(Direction.Up);
            var targetAboveMeta = _blocks.GetStateMetadata(getBlock(targetAbove));
            if (!targetAboveMeta.IsOpaque && !targetAboveMeta.IsFluid)
            {
                var (tSky, tBlk) = getLight(targetAbove);
                if (Math.Max(tSky, tBlk) >= 4)
                {
                    setBlock(target, grassDefault);
                    mutations++;
                }
            }
        }

        return mutations;
    }

    private void CascadeUnsupportedAttachments(
        BlockPos changedPos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<(BlockPos Pos, BlockState DetachedState)>? detachedBlocks)
    {
        _cascadeQueueScratch.Clear();
        _cascadeQueueScratch.Enqueue(changedPos);
        var safety = 0;

        while (_cascadeQueueScratch.Count > 0 && safety++ < 64)
        {
            var origin = _cascadeQueueScratch.Dequeue();
            var dirs = DirectionExtensions.All;
            for (var i = 0; i < dirs.Length; i++)
            {
                var neighbor = origin.Offset(dirs[i]);
                if (neighbor.Y < EngineConstants.WorldMinY || neighbor.Y >= EngineConstants.WorldMaxY) continue;

                var ns = getBlock(neighbor);
                if (!ns.IsAir && !BlockAttachmentAndShapeRules.IsStillSupported(_blocks, getBlock, neighbor, ns))
                {
                    detachedBlocks?.Add((neighbor, ns));
                    setBlock(neighbor, BlockState.Air);
                    _cascadeQueueScratch.Enqueue(neighbor);
                    EnqueueFrontierNeighbors(neighbor, getBlock);
                }
            }
        }
    }

    private void EnqueueFrontierNeighbors(BlockPos center, Func<BlockPos, BlockState> getBlock)
    {
        AddCandidate(center, getBlock);
        foreach (var dir in DirectionExtensions.All)
        {
            var n = center.Offset(dir);
            if (n.Y >= EngineConstants.WorldMinY && n.Y < EngineConstants.WorldMaxY)
            {
                AddCandidate(n, getBlock);
            }
        }
    }

    private void EnqueueFluidOnlyNeighbors(BlockPos center)
    {
        _ = _activeFluidFrontier.Add(center);
        foreach (var dir in DirectionExtensions.All)
        {
            var n = center.Offset(dir);
            if (n.Y >= EngineConstants.WorldMinY && n.Y < EngineConstants.WorldMaxY)
            {
                _ = _activeFluidFrontier.Add(n);
            }
        }
    }

    private void AddCandidate(BlockPos pos, Func<BlockPos, BlockState> getBlock)
    {
        _ = _activeFluidFrontier.Add(pos);
        var st = getBlock(pos);
        if (IsGravityAffected(st))
        {
            _ = _activeGravityBlocks.Add(pos);
        }
        else if (_blocks.GetStateMetadata(st).BlockKey == BuiltInBlocks.Fire)
        {
            _ = _activeFires.Add(pos);
        }
    }

    private bool IsGravityAffected(BlockState state)
    {
        if (state.IsAir) return false;
        var key = _blocks.GetStateMetadata(state).BlockKey;
        return key == BuiltInBlocks.Sand || key == BuiltInBlocks.RedSand || key == BuiltInBlocks.Gravel;
    }

    private bool CanGravityFallInto(BlockState below)
    {
        if (below.IsAir) return true;
        var meta = _blocks.GetStateMetadata(below);
        return meta.IsFluid || meta.BlockKey == BuiltInBlocks.Fire;
    }
}
