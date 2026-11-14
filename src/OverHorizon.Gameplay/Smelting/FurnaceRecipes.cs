using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Smelting;

/// <summary>
/// Clean-room furnace smelting output and fuel burn-duration registry.
/// </summary>
public sealed class FurnaceRecipes
{
    private readonly Dictionary<ushort, ItemStack> _smeltOutputs = [];
    private readonly Dictionary<ushort, float> _fuelBurnSeconds = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="FurnaceRecipes"/> class from <paramref name="items"/>.
    /// </summary>
    public FurnaceRecipes(IRegistry<ItemDefinition> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        ushort Id(ResourceKey key) => (ushort)items.GetId(key);

        // Ore & Raw Metal Smelting
        AddSmelt(Id(BuiltInItems.RawIron), Id(BuiltInItems.IronIngot));
        AddSmelt(Id(BuiltInBlocks.IronOre), Id(BuiltInItems.IronIngot));
        AddSmelt(Id(BuiltInItems.RawGold), Id(BuiltInItems.GoldIngot));
        AddSmelt(Id(BuiltInBlocks.GoldOre), Id(BuiltInItems.GoldIngot));
        AddSmelt(Id(BuiltInItems.RawCopper), Id(BuiltInItems.CopperIngot));
        AddSmelt(Id(BuiltInBlocks.CopperOre), Id(BuiltInItems.CopperIngot));
        AddSmelt(Id(BuiltInBlocks.CoalOre), Id(BuiltInItems.Coal));
        AddSmelt(Id(BuiltInBlocks.DiamondOre), Id(BuiltInItems.Diamond));
        AddSmelt(Id(BuiltInBlocks.EmeraldOre), Id(BuiltInItems.Emerald));

        // Stone, Glass & Charcoal
        AddSmelt(Id(BuiltInBlocks.Cobblestone), Id(BuiltInBlocks.Stone));
        AddSmelt(Id(BuiltInBlocks.CobbledDeepslate), Id(BuiltInBlocks.Deepslate));
        AddSmelt(Id(BuiltInBlocks.Sand), Id(BuiltInBlocks.Glass));
        AddSmelt(Id(BuiltInBlocks.OakLog), Id(BuiltInItems.Charcoal));
        AddSmelt(Id(BuiltInBlocks.BirchLog), Id(BuiltInItems.Charcoal));

        // Food Cooking
        AddSmelt(Id(BuiltInItems.RawPorkchop), Id(BuiltInItems.CookedPorkchop));
        AddSmelt(Id(BuiltInItems.RawBeef), Id(BuiltInItems.CookedBeef));
        AddSmelt(Id(BuiltInItems.RawChicken), Id(BuiltInItems.CookedChicken));
        AddSmelt(Id(BuiltInItems.RawMutton), Id(BuiltInItems.CookedMutton));

        // Fuels (duration in seconds; 1 item smelts in 5.0s, so Coal smelts 8 items = 40.0s!)
        AddFuel(Id(BuiltInBlocks.CoalBlock), 360.0f);
        AddFuel(Id(BuiltInItems.Coal), 40.0f);
        AddFuel(Id(BuiltInItems.Charcoal), 40.0f);
        AddFuel(Id(BuiltInBlocks.OakLog), 10.0f);
        AddFuel(Id(BuiltInBlocks.BirchLog), 10.0f);
        AddFuel(Id(BuiltInBlocks.OakPlanks), 10.0f);
        AddFuel(Id(BuiltInBlocks.BirchPlanks), 10.0f);
        AddFuel(Id(BuiltInBlocks.CraftingTable), 10.0f);
        AddFuel(Id(BuiltInBlocks.OakFence), 10.0f);
        AddFuel(Id(BuiltInBlocks.Chest), 10.0f);
        AddFuel(Id(BuiltInBlocks.Barrel), 10.0f);
        AddFuel(Id(BuiltInBlocks.Ladder), 7.5f);
        AddFuel(Id(BuiltInItems.Stick), 3.0f);
    }

    /// <summary>Standard time in seconds required to smelt 1 item.</summary>
    public const float CookDurationSeconds = 5.0f;

    /// <summary>Looks up the smelted output for <paramref name="inputItemId"/>, or <see cref="ItemStack.Empty"/>.</summary>
    public ItemStack GetSmeltResult(ushort inputItemId) =>
        _smeltOutputs.TryGetValue(inputItemId, out var result) ? result : ItemStack.Empty;

    /// <summary>Gets fuel burn duration in seconds (<c>0</c> if not valid fuel).</summary>
    public float GetFuelBurnSeconds(ushort fuelItemId) =>
        _fuelBurnSeconds.TryGetValue(fuelItemId, out var seconds) ? seconds : 0.0f;

    /// <summary>Returns <see langword="true"/> when <paramref name="stack"/> can be burned as fuel.</summary>
    public bool IsFuel(ItemStack stack) => !stack.IsEmpty && GetFuelBurnSeconds(stack.ItemId) > 0.0f;

    private void AddSmelt(ushort inputId, ushort outputId, int count = 1)
    {
        if (inputId > 0 && outputId > 0)
        {
            _smeltOutputs[inputId] = new ItemStack(outputId, count);
        }
    }

    private void AddFuel(ushort fuelId, float seconds)
    {
        if (fuelId > 0 && seconds > 0.0f)
        {
            _fuelBurnSeconds[fuelId] = seconds;
        }
    }
}
