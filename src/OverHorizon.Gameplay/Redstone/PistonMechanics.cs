using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;

namespace OverHorizon.Gameplay.Redstone;

/// <summary>
/// Evaluates <c>Piston</c> (<c>12</c>-block push) and <c>StickyPiston</c> (<c>12</c>-block push + <c>1</c>-block pull) mechanics.
/// </summary>
internal static class PistonMechanics
{
    public const int MaxPistonPushCount = 12;

    public static void EvaluatePiston(
        IBlockRegistry blocks,
        RedstoneEngine engine,
        BlockPos pistonPos,
        BlockState pistonState,
        BlockStateMetadata meta,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<(BlockPos Pos, RedstoneSoundKind Sound)> soundEvents)
    {
        var isSticky = meta.BlockKey == BuiltInBlocks.StickyPiston;
        var facingStr = BlockAttachmentAndShapeRules.GetFacing(meta, "north");
        var pushDir = BlockAttachmentAndShapeRules.ParseDirection(facingStr);
        var headPos = pistonPos.Offset(pushDir);
        var isExtended = IsExtended(meta);
        var powered = engine.GetPowerReceivedAt(pistonPos, getBlock, ignoreSource: headPos) > 0;

        if (powered && !isExtended)
        {
            var chain = new List<BlockPos>(MaxPistonPushCount);
            var cursor = headPos;
            while (true)
            {
                if (cursor.Y < EngineConstants.WorldMinY || cursor.Y >= EngineConstants.WorldMaxY)
                {
                    return;
                }

                var cs = getBlock(cursor);
                var cMeta = blocks.GetStateMetadata(cs);
                if (cs.IsAir || cMeta.IsFluid || cMeta.BlockKey == BuiltInBlocks.Fire)
                {
                    break;
                }

                if (!IsPistonPushable(cMeta) || chain.Count >= MaxPistonPushCount)
                {
                    return;
                }

                chain.Add(cursor);
                cursor = cursor.Offset(pushDir);
            }

            if (cursor.Y < EngineConstants.WorldMinY || cursor.Y >= EngineConstants.WorldMaxY)
            {
                return;
            }

            for (var i = chain.Count - 1; i >= 0; i--)
            {
                var from = chain[i];
                var to = from.Offset(pushDir);
                setBlock(to, getBlock(from));
            }

            var headState = blocks.WithProperty(
                blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.PistonHead), "facing", facingStr),
                "sticky",
                isSticky ? "true" : "false");
            setBlock(headPos, headState);
            setBlock(pistonPos, blocks.WithProperty(pistonState, "extended", "true"));
            soundEvents.Add((pistonPos, RedstoneSoundKind.PistonExtend));
            engine.NotifyBlockChanged(headPos);
        }
        else if (!powered && isExtended)
        {
            if (blocks.GetStateMetadata(getBlock(headPos)).BlockKey == BuiltInBlocks.PistonHead)
            {
                setBlock(headPos, BlockState.Air);
            }

            if (isSticky)
            {
                var pullFrom = headPos.Offset(pushDir);
                if (pullFrom.Y >= EngineConstants.WorldMinY && pullFrom.Y < EngineConstants.WorldMaxY)
                {
                    var pulled = getBlock(pullFrom);
                    var pulledMeta = blocks.GetStateMetadata(pulled);
                    if (!pulled.IsAir && !pulledMeta.IsFluid && IsPistonPushable(pulledMeta))
                    {
                        setBlock(headPos, pulled);
                        setBlock(pullFrom, BlockState.Air);
                        engine.NotifyBlockChanged(pullFrom);
                    }
                }
            }

            setBlock(pistonPos, blocks.WithProperty(pistonState, "extended", "false"));
            soundEvents.Add((pistonPos, RedstoneSoundKind.PistonRetract));
            engine.NotifyBlockChanged(headPos);
        }
    }

    private static bool IsPistonPushable(BlockStateMetadata meta) =>
        meta.Hardness >= 0.0f &&
        meta.BlockKey != BuiltInBlocks.Obsidian &&
        meta.BlockKey != BuiltInBlocks.Bedrock &&
        meta.BlockKey != BuiltInBlocks.PistonHead &&
        !(meta.BlockKey == BuiltInBlocks.Piston && IsExtended(meta)) &&
        !(meta.BlockKey == BuiltInBlocks.StickyPiston && IsExtended(meta));

    private static bool IsExtended(BlockStateMetadata meta) =>
        meta.Properties.TryGetValue("extended", out var v) && string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
}
