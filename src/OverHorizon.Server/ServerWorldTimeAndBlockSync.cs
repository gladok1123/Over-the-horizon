using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Sessions;
using OverHorizon.World;

namespace OverHorizon.Server;

/// <summary>
/// Server-side helper applying attachment support detachment, Redstone/Piston propagation,
/// and batched <see cref="MultiBlockUpdatePacket"/> / <see cref="BlockUpdatePacket"/> broadcasting.
/// </summary>
internal static class ServerWorldTimeAndBlockSync
{
    private static readonly Direction[] SixNeighbors =
    [
        Direction.Down, Direction.Up, Direction.North, Direction.South, Direction.West, Direction.East,
    ];

    public static void ApplyMutationAndBroadcastCascade(
        IWorldSubsystem world,
        IBlockRegistry blocks,
        RedstoneEngine redstone,
        BlockPos primaryPos,
        BlockState primaryState,
        long tickNumber,
        INetworkBroadcaster broadcaster)
    {
        if (!world.SetBlockState(primaryPos, primaryState, tickNumber))
        {
            return;
        }

        var changed = new List<(BlockPos Position, ushort BlockStateId)>(8)
        {
            (primaryPos, primaryState.Id),
        };

        // 1. Detach any unsupported neighbor blocks (Torches, Ladders, Redstone dust, Rails, Beds)
        var checkQueue = new Queue<BlockPos>();
        checkQueue.Enqueue(primaryPos);
        while (checkQueue.Count > 0)
        {
            var cur = checkQueue.Dequeue();
            for (var i = 0; i < SixNeighbors.Length; i++)
            {
                var nPos = cur.Neighbor(SixNeighbors[i]);
                var nState = world.GetBlockState(nPos);
                if (nState.IsAir) continue;
                if (!BlockAttachmentAndShapeRules.IsStillSupported(blocks, nPos, nState, world.GetBlockState))
                {
                    if (world.SetBlockState(nPos, BlockState.Air, tickNumber))
                    {
                        changed.Add((nPos, BlockState.Air.Id));
                        redstone.NotifyBlockChanged(nPos);
                        checkQueue.Enqueue(nPos);
                    }
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

        if (changed.Count == 1)
        {
            broadcaster.BroadcastToPlaySessions(new BlockUpdatePacket(changed[0].Position, changed[0].BlockStateId));
        }
        else
        {
            broadcaster.BroadcastToPlaySessions(new MultiBlockUpdatePacket(changed));
        }
    }
}
