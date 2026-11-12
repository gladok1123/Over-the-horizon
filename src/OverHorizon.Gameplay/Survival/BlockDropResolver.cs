using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Computes authentic Survival block break durations and harvested item drops based on block hardness and held tool tier.
/// </summary>
public static class BlockDropResolver
{
    /// <summary>
    /// Computes the time in seconds required to mine <paramref name="block"/> while holding <paramref name="heldTool"/>.
    /// Returns <see cref="float.PositiveInfinity"/> for unbreakable blocks such as Bedrock.
    /// </summary>
    /// <param name="block">Target block definition.</param>
    /// <param name="heldTool">Held item definition (or <see langword="null"/> for bare hand).</param>
    /// <param name="isEyeUnderwater">Whether the miner's eyes are submerged in water.</param>
    /// <param name="onGround">Whether the miner is standing on solid ground.</param>
    /// <returns>Required mining duration in seconds.</returns>
    public static float ComputeBreakSeconds(
        BlockDefinition block,
        ItemDefinition? heldTool,
        bool isEyeUnderwater = false,
        bool onGround = true)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Hardness < 0.0f || block.IsFluid)
        {
            return float.PositiveInfinity;
        }

        if (block.Hardness <= 0.05f)
        {
            return 0.05f;
        }

        var preferredTool = GetPreferredTool(block.Key);
        var requiresPickaxe = RequiresPickaxeForFastHarvest(block.Key);

        var speed = 1.0f;
        var hasMatchingTool = heldTool is not null && heldTool.ToolType != ToolKind.None && heldTool.ToolType == preferredTool;
        if (hasMatchingTool && heldTool is not null)
        {
            speed = Math.Max(1.5f, heldTool.MiningSpeedMultiplier);
        }
        else if (heldTool is { ToolType: ToolKind.Sword } && block.Key == BuiltInBlocks.OakLeaves)
        {
            speed = 2.0f;
        }

        if (isEyeUnderwater)
        {
            speed *= 0.25f;
        }

        if (!onGround)
        {
            speed *= 0.5f;
        }

        var baseMultiplier = (requiresPickaxe && !hasMatchingTool) ? 3.5f : 1.35f;
        return Math.Max(0.06f, (block.Hardness * baseMultiplier) / Math.Max(0.1f, speed));
    }

    /// <summary>
    /// Resolves the Survival item stack dropped when <paramref name="blockKey"/> is broken with <paramref name="heldTool"/>.
    /// </summary>
    /// <param name="blockKey">Broken block resource key.</param>
    /// <param name="heldTool">Held tool definition, if any.</param>
    /// <param name="items">Frozen item registry.</param>
    /// <param name="dropSequence">Monotonic counter used for deterministic bonus drops (e.g. Apples from Oak Leaves).</param>
    /// <returns>Dropped <see cref="ItemStack"/> (or <see cref="ItemStack.Empty"/>).</returns>
    public static ItemStack ResolveDrop(
        ResourceKey blockKey,
        ItemDefinition? heldTool,
        IRegistry<ItemDefinition> items,
        int dropSequence = 0)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (blockKey == BuiltInBlocks.Air || blockKey == BuiltInBlocks.Bedrock || blockKey == BuiltInBlocks.Water)
        {
            return ItemStack.Empty;
        }

        if (blockKey == BuiltInBlocks.Glass)
        {
            return heldTool is { ToolType: ToolKind.Shears }
                ? MakeStack(items, BuiltInBlocks.Glass, 1)
                : ItemStack.Empty;
        }

        if (blockKey == BuiltInBlocks.OakLeaves)
        {
            if (heldTool is { ToolType: ToolKind.Shears })
            {
                return MakeStack(items, BuiltInBlocks.OakLeaves, 1);
            }

            return (dropSequence & 3) == 0
                ? MakeStack(items, BuiltInItems.Apple, 1)
                : ItemStack.Empty;
        }

        if (RequiresPickaxeForFastHarvest(blockKey) && heldTool is not { ToolType: ToolKind.Pickaxe })
        {
            return ItemStack.Empty;
        }

        if (blockKey == BuiltInBlocks.Stone)
        {
            return MakeStack(items, BuiltInBlocks.Cobblestone, 1);
        }

        if (blockKey == BuiltInBlocks.GrassBlock)
        {
            return MakeStack(items, BuiltInBlocks.Dirt, 1);
        }

        if (blockKey == BuiltInBlocks.CoalOre)
        {
            return MakeStack(items, BuiltInItems.Coal, 1);
        }

        if (blockKey == BuiltInBlocks.DiamondOre)
        {
            return MakeStack(items, BuiltInItems.Diamond, 1);
        }

        if (blockKey == BuiltInBlocks.Bookshelf)
        {
            return MakeStack(items, BuiltInBlocks.OakPlanks, 3);
        }

        return MakeStack(items, blockKey, 1);
    }

    private static ItemStack MakeStack(IRegistry<ItemDefinition> items, ResourceKey key, int count)
    {
        var id = items.GetId(key);
        return id > 0 ? new ItemStack((ushort)id, count) : ItemStack.Empty;
    }

    private static bool RequiresPickaxeForFastHarvest(ResourceKey key) =>
        key == BuiltInBlocks.Stone || key == BuiltInBlocks.Cobblestone || key == BuiltInBlocks.MossyCobblestone ||
        key == BuiltInBlocks.StoneBricks || key == BuiltInBlocks.Bricks || key == BuiltInBlocks.Sandstone ||
        key == BuiltInBlocks.CoalOre || key == BuiltInBlocks.IronOre || key == BuiltInBlocks.GoldOre ||
        key == BuiltInBlocks.DiamondOre || key == BuiltInBlocks.Obsidian;

    private static ToolKind GetPreferredTool(ResourceKey key)
    {
        if (RequiresPickaxeForFastHarvest(key))
        {
            return ToolKind.Pickaxe;
        }

        if (key == BuiltInBlocks.OakLog || key == BuiltInBlocks.OakPlanks ||
            key == BuiltInBlocks.CraftingTable || key == BuiltInBlocks.Bookshelf)
        {
            return ToolKind.Axe;
        }

        if (key == BuiltInBlocks.Dirt || key == BuiltInBlocks.GrassBlock ||
            key == BuiltInBlocks.Sand || key == BuiltInBlocks.Gravel || key == BuiltInBlocks.SnowBlock)
        {
            return ToolKind.Shovel;
        }

        if (key == BuiltInBlocks.OakLeaves || key == BuiltInBlocks.CrimsonWool ||
            key == BuiltInBlocks.WhiteWool || key == BuiltInBlocks.BlueWool)
        {
            return ToolKind.Shears;
        }

        return ToolKind.None;
    }
}
