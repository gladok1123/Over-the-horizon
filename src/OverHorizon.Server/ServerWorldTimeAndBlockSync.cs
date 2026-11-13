using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Items;
using OverHorizon.Gameplay.Physics;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Sessions;
using OverHorizon.World;

namespace OverHorizon.Server;

/// <summary>
/// Server-side helper applying attachment support detachment (spawning 3D dropped items),
/// Redstone/Piston propagation, TNT ignition, and batched <see cref="MultiBlockUpdatePacket"/> broadcasting.
/// </summary>
internal static class ServerWorldTimeAndBlockSync
{
    private static readonly Direction[] SixNeighbors =
    [
        Direction.Down, Direction.Up, Direction.North, Direction.South, Direction.West, Direction.East,
    ];

    public static void ApplyMutationAndBroadcastCascade(
        IWorldSubsystem world,
        CoreRegistries registries,
        RedstoneEngine redstone,
        BlockPos primaryPos,
        BlockState primaryState,
        long tickNumber,
        INetworkBroadcaster broadcaster,
        DroppedItemManager? droppedItems = null,
        PrimedTntAndWorldTicker? tntTicker = null)
    {
        if (!world.SetBlockState(primaryPos, primaryState, tickNumber))
        {
            return;
        }

        var changed = new List<(BlockPos Position, ushort BlockStateId)>(8)
        {
            (primaryPos, primaryState.Id),
        };

        // 1. Detach any unsupported neighbor blocks and spawn 3D item drops
        var checkQueue = new Queue<BlockPos>();
        checkQueue.Enqueue(primaryPos);
        while (checkQueue.Count > 0)
        {
            var cur = checkQueue.Dequeue();
            for (var i = 0; i < SixNeighbors.Length; i++)
            {
                var nPos = cur.Offset(SixNeighbors[i]);
                var nState = world.GetBlockState(nPos);
                if (nState.IsAir) continue;
                if (!BlockAttachmentAndShapeRules.IsStillSupported(registries.Blocks, world.GetBlockState, nPos, nState) &&
                    world.SetBlockState(nPos, BlockState.Air, tickNumber))
                {
                    changed.Add((nPos, BlockState.Air.Id));
                    SpawnDropForDetachedBlock(registries, droppedItems, nPos, nState);
                    redstone.NotifyBlockChanged(nPos);
                    checkQueue.Enqueue(nPos);
                }
            }
        }

        // 2. Evaluate immediate Redstone & Piston propagation
        redstone.NotifyBlockChanged(primaryPos);
        var ignited = new List<BlockPos>();
        var sounds = new List<(BlockPos Pos, RedstoneSoundKind Sound)>();
        redstone.EvaluateImmediate(
            world.GetBlockState,
            (pos, st) =>
            {
                if (world.SetBlockState(pos, st, tickNumber))
                {
                    changed.Add((pos, st.Id));
                }
            },
            ignited,
            sounds);

        for (var i = 0; i < ignited.Count; i++)
        {
            var tntPos = ignited[i];
            if (tntTicker is not null && tntTicker.IgniteBlock(world.GetBlockState, (p, s) => world.SetBlockState(p, s, tickNumber), tntPos))
            {
                changed.Add((tntPos, BlockState.Air.Id));
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, EventKind: 3, new Vec3d(tntPos.X + 0.5, tntPos.Y + 0.5, tntPos.Z + 0.5)));
            }
        }

        BroadcastBlockChanges(changed, broadcaster);
    }

    public static void BroadcastBlockChanges(
        IReadOnlyList<(BlockPos Position, ushort BlockStateId)> changed,
        INetworkBroadcaster broadcaster)
    {
        if (changed.Count == 0)
        {
            return;
        }

        if (changed.Count == 1)
        {
            broadcaster.BroadcastToPlaySessions(new BlockUpdatePacket(changed[0].Position, changed[0].BlockStateId));
        }
        else
        {
            broadcaster.BroadcastToPlaySessions(new MultiBlockUpdatePacket(changed));
        }
    }

    private static void SpawnDropForDetachedBlock(CoreRegistries registries, DroppedItemManager? droppedItems, BlockPos pos, BlockState state)
    {
        if (droppedItems is null)
        {
            return;
        }

        var drop = BlockDropResolver.ResolveExplosionDrop(registries, state);
        if (!drop.IsEmpty)
        {
            _ = droppedItems.SpawnDrop(drop, new Vec3d(pos.X + 0.5, pos.Y + 0.35, pos.Z + 0.5), pickupDelay: 0.25f);
        }
    }
}
