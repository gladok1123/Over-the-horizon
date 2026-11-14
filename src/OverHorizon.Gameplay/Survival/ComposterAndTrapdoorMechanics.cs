using System.Globalization;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Implements authentic <c>Composter</c> organic filling (<c>level = 0..8</c>) and <c>BoneMeal</c> harvest,
/// as well as <c>OakTrapdoor</c> manual toggling.
/// </summary>
public static class ComposterAndTrapdoorMechanics
{
    /// <summary>
    /// Interacts with a <c>Composter</c> at <paramref name="pos"/>: harvests <c>BoneMeal</c> when full (<c>level &gt;= 7</c>),
    /// or consumes 1 compostable organic item from <paramref name="heldItem"/> to raise fill level.
    /// </summary>
    public static bool TryInteractComposter(
        IBlockRegistry blocks,
        IRegistry<ItemDefinition> items,
        BlockPos pos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        ItemDefinition? heldItem,
        out bool consumedHeldItem,
        out ItemStack harvestedBoneMeal)
    {
        consumedHeldItem = false;
        harvestedBoneMeal = ItemStack.Empty;

        var cur = getBlock(pos);
        var meta = blocks.GetStateMetadata(cur);
        if (meta.BlockKey != BuiltInBlocks.Composter)
        {
            return false;
        }

        var level = meta.Properties.TryGetValue("level", out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

        if (level >= 7)
        {
            setBlock(pos, blocks.WithProperty(cur, "level", "0"));
            var bmId = items.GetId(BuiltInItems.BoneMeal);
            if (bmId > 0)
            {
                harvestedBoneMeal = new ItemStack((ushort)bmId, 1);
            }

            return true;
        }

        if (heldItem is null || !IsCompostable(heldItem.Key))
        {
            return false;
        }

        consumedHeldItem = true;
        var nextLevel = Math.Min(8, level + (heldItem.Key == BuiltInBlocks.HayBlock || heldItem.Key == BuiltInItems.Bread ? 2 : 1));
        if (nextLevel >= 7)
        {
            nextLevel = 8; // Ready-to-harvest white Bone Meal top!
        }

        setBlock(pos, blocks.WithProperty(cur, "level", nextLevel.ToString(CultureInfo.InvariantCulture)));
        return true;
    }

    /// <summary>Toggles an <c>OakTrapdoor</c> or 2-block <c>OakDoor</c> open/closed on right-click (Iron doors/trapdoors require Redstone).</summary>
    public static bool TryToggleTrapdoor(
        IBlockRegistry blocks,
        BlockPos pos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock) =>
        TryToggleDoorOrTrapdoor(blocks, pos, getBlock, setBlock, out _, out _);

    /// <summary>
    /// Toggles a wooden Door (synchronizing both <c>lower</c> and <c>upper</c> halves) or <c>OakTrapdoor</c> on hand right-click.
    /// Returns <see langword="false"/> for <c>IronDoor</c> and <c>IronTrapdoor</c> (which open exclusively via Redstone).
    /// </summary>
    public static bool TryToggleDoorOrTrapdoor(
        IBlockRegistry blocks,
        BlockPos pos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        out bool nowOpen,
        out bool isDoor)
    {
        nowOpen = false;
        isDoor = false;
        var cur = getBlock(pos);
        if (cur.IsAir)
        {
            return false;
        }

        var meta = blocks.GetStateMetadata(cur);
        if (meta.BlockKey == BuiltInBlocks.OakTrapdoor)
        {
            var wasOpen = meta.Properties.TryGetValue("open", out var ov) && string.Equals(ov, "true", StringComparison.OrdinalIgnoreCase);
            nowOpen = !wasOpen;
            setBlock(pos, blocks.WithProperty(cur, "open", nowOpen ? "true" : "false"));
            return true;
        }

        if (meta.BlockKey == BuiltInBlocks.OakDoor)
        {
            isDoor = true;
            var wasOpen = meta.Properties.TryGetValue("open", out var ov) && string.Equals(ov, "true", StringComparison.OrdinalIgnoreCase);
            nowOpen = !wasOpen;
            var openStr = nowOpen ? "true" : "false";
            setBlock(pos, blocks.WithProperty(cur, "open", openStr));

            if (BlockAttachmentAndShapeRules.TryGetPartnerDoorPosition(meta, pos, out var partnerPos, out _))
            {
                var partnerState = getBlock(partnerPos);
                if (!partnerState.IsAir && blocks.GetStateMetadata(partnerState).BlockKey == BuiltInBlocks.OakDoor)
                {
                    setBlock(partnerPos, blocks.WithProperty(partnerState, "open", openStr));
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="key"/> is an organic plant/food item accepted by a Composter.</summary>
    public static bool IsCompostable(ResourceKey key) =>
        key == BuiltInBlocks.OakLeaves || key == BuiltInBlocks.BirchLeaves ||
        key == BuiltInBlocks.CherryLeaves || key == BuiltInBlocks.SpruceLeaves ||
        key == BuiltInItems.Wheat || key == BuiltInItems.SugarCane ||
        key == BuiltInItems.Apple || key == BuiltInItems.Bread ||
        key == BuiltInBlocks.HayBlock || key == BuiltInItems.Paper;
}
