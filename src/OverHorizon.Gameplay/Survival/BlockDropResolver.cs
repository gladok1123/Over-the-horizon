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
        else if (heldTool is { ToolType: ToolKind.Sword } &&
                 (block.Key == BuiltInBlocks.OakLeaves || block.Key == BuiltInBlocks.BirchLeaves || block.Key == BuiltInBlocks.Cobweb))
        {
            speed = block.Key == BuiltInBlocks.Cobweb ? 15.0f : 2.5f;
        }
        else if (heldTool is { ToolType: ToolKind.Hoe } &&
                 (block.Key == BuiltInBlocks.OakLeaves || block.Key == BuiltInBlocks.BirchLeaves || block.Key == BuiltInBlocks.HayBlock))
        {
            speed = Math.Max(2.5f, heldTool.MiningSpeedMultiplier);
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
    public static ItemStack ResolveDrop(
        ResourceKey blockKey,
        ItemDefinition? heldTool,
        IRegistry<ItemDefinition> items,
        int dropSequence = 0)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (blockKey == BuiltInBlocks.Air || blockKey == BuiltInBlocks.Bedrock || blockKey == BuiltInBlocks.Water ||
            blockKey == BuiltInBlocks.Fire || blockKey == BuiltInBlocks.PistonHead)
        {
            return ItemStack.Empty;
        }

        if (blockKey == BuiltInBlocks.RedstoneWire)
        {
            return MakeStack(items, BuiltInItems.Redstone, 1);
        }

        if (blockKey == BuiltInBlocks.RedstoneTorchOff)
        {
            return MakeStack(items, BuiltInBlocks.RedstoneTorch, 1);
        }

        if (blockKey == BuiltInBlocks.LitRedstoneLamp)
        {
            return MakeStack(items, BuiltInBlocks.RedstoneLamp, 1);
        }

        if (blockKey == BuiltInBlocks.Glass)
        {
            return heldTool is { ToolType: ToolKind.Shears }
                ? MakeStack(items, BuiltInBlocks.Glass, 1)
                : ItemStack.Empty;
        }

        if (blockKey == BuiltInBlocks.Cobweb)
        {
            if (heldTool is { ToolType: ToolKind.Shears })
            {
                return MakeStack(items, BuiltInBlocks.Cobweb, 1);
            }

            return heldTool is { ToolType: ToolKind.Sword }
                ? MakeStack(items, BuiltInItems.String, 1)
                : ItemStack.Empty;
        }

        if (blockKey == BuiltInBlocks.OakLeaves || blockKey == BuiltInBlocks.BirchLeaves)
        {
            if (heldTool is { ToolType: ToolKind.Shears })
            {
                return MakeStack(items, blockKey, 1);
            }

            if (blockKey == BuiltInBlocks.OakLeaves && (dropSequence & 3) == 0)
            {
                return MakeStack(items, BuiltInItems.Apple, 1);
            }

            return (dropSequence & 3) == 1
                ? MakeStack(items, BuiltInItems.Stick, 1)
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

        if (blockKey == BuiltInBlocks.Deepslate)
        {
            return MakeStack(items, BuiltInBlocks.CobbledDeepslate, 1);
        }

        if (blockKey == BuiltInBlocks.GrassBlock || blockKey == BuiltInBlocks.Farmland)
        {
            return MakeStack(items, BuiltInBlocks.Dirt, 1);
        }

        if (blockKey == BuiltInBlocks.LitFurnace)
        {
            return MakeStack(items, BuiltInBlocks.Furnace, 1);
        }

        if (blockKey == BuiltInBlocks.Gravel)
        {
            return (dropSequence & 3) == 0
                ? MakeStack(items, BuiltInItems.Flint, 1)
                : MakeStack(items, BuiltInBlocks.Gravel, 1);
        }

        if (blockKey == BuiltInBlocks.CoalOre)
        {
            return MakeStack(items, BuiltInItems.Coal, 1);
        }

        if (blockKey == BuiltInBlocks.IronOre)
        {
            return MakeStack(items, BuiltInItems.RawIron, 1);
        }

        if (blockKey == BuiltInBlocks.GoldOre)
        {
            return MakeStack(items, BuiltInItems.RawGold, 1);
        }

        if (blockKey == BuiltInBlocks.CopperOre)
        {
            return MakeStack(items, BuiltInItems.RawCopper, 2);
        }

        if (blockKey == BuiltInBlocks.RedstoneOre)
        {
            return MakeStack(items, BuiltInItems.Redstone, 4);
        }

        if (blockKey == BuiltInBlocks.LapisOre)
        {
            return MakeStack(items, BuiltInItems.LapisLazuli, 4);
        }

        if (blockKey == BuiltInBlocks.DiamondOre)
        {
            return MakeStack(items, BuiltInItems.Diamond, 1);
        }

        if (blockKey == BuiltInBlocks.EmeraldOre)
        {
            return MakeStack(items, BuiltInItems.Emerald, 1);
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
        key == BuiltInBlocks.Stone || key == BuiltInBlocks.Deepslate || key == BuiltInBlocks.CobbledDeepslate ||
        key == BuiltInBlocks.Cobblestone || key == BuiltInBlocks.MossyCobblestone ||
        key == BuiltInBlocks.StoneBricks || key == BuiltInBlocks.Bricks || key == BuiltInBlocks.Sandstone ||
        key == BuiltInBlocks.Furnace || key == BuiltInBlocks.LitFurnace || key == BuiltInBlocks.Lantern ||
        key == BuiltInBlocks.CoalOre || key == BuiltInBlocks.IronOre || key == BuiltInBlocks.GoldOre ||
        key == BuiltInBlocks.CopperOre || key == BuiltInBlocks.RedstoneOre || key == BuiltInBlocks.LapisOre ||
        key == BuiltInBlocks.DiamondOre || key == BuiltInBlocks.EmeraldOre || key == BuiltInBlocks.Obsidian ||
        key == BuiltInBlocks.CoalBlock || key == BuiltInBlocks.IronBlock || key == BuiltInBlocks.GoldBlock ||
        key == BuiltInBlocks.DiamondBlock || key == BuiltInBlocks.EmeraldBlock || key == BuiltInBlocks.LapisBlock ||
        key == BuiltInBlocks.CopperBlock || key == BuiltInBlocks.RedstoneBlock;

    private static ToolKind GetPreferredTool(ResourceKey key)
    {
        if (RequiresPickaxeForFastHarvest(key) || key == BuiltInBlocks.Rail)
        {
            return ToolKind.Pickaxe;
        }

        if (key == BuiltInBlocks.OakLog || key == BuiltInBlocks.BirchLog ||
            key == BuiltInBlocks.OakPlanks || key == BuiltInBlocks.BirchPlanks ||
            key == BuiltInBlocks.OakFence || key == BuiltInBlocks.Chest ||
            key == BuiltInBlocks.Barrel || key == BuiltInBlocks.Ladder ||
            key == BuiltInBlocks.CraftingTable || key == BuiltInBlocks.Bookshelf)
        {
            return ToolKind.Axe;
        }

        if (key == BuiltInBlocks.Dirt || key == BuiltInBlocks.GrassBlock || key == BuiltInBlocks.Farmland ||
            key == BuiltInBlocks.Sand || key == BuiltInBlocks.Gravel || key == BuiltInBlocks.SnowBlock)
        {
            return ToolKind.Shovel;
        }

        if (key == BuiltInBlocks.OakLeaves || key == BuiltInBlocks.BirchLeaves ||
            key == BuiltInBlocks.Cobweb || key == BuiltInBlocks.CrimsonWool ||
            key == BuiltInBlocks.WhiteWool || key == BuiltInBlocks.BlueWool)
        {
            return ToolKind.Shears;
        }

        if (key == BuiltInBlocks.HayBlock)
        {
            return ToolKind.Hoe;
        }

        return ToolKind.None;
    }
}
