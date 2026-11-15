using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Smelting;

/// <summary>
/// Stateful 3-slot Furnace block-entity tracking input, fuel, output, flame burn timer, and cook progress.
/// </summary>
public sealed class FurnaceBlockEntity
{
    /// <summary>Initializes a new instance of the <see cref="FurnaceBlockEntity"/> class at <paramref name="position"/>.</summary>
    /// <param name="position">World block coordinate of the furnace.</param>
    public FurnaceBlockEntity(BlockPos position)
    {
        Position = position;
    }

    /// <summary>Gets the world block coordinate of this furnace.</summary>
    public BlockPos Position { get; }

    /// <summary>Gets or sets the top input ingredient stack.</summary>
    public ItemStack InputSlot { get; set; }

    /// <summary>Gets or sets the bottom fuel stack.</summary>
    public ItemStack FuelSlot { get; set; }

    /// <summary>Gets or sets the right smelted output stack.</summary>
    public ItemStack OutputSlot { get; set; }

    /// <summary>Gets remaining burn time in seconds.</summary>
    public float BurnTimeRemaining { get; private set; }

    /// <summary>Gets total burn duration of the currently burning fuel item.</summary>
    public float CurrentFuelTotalSeconds { get; private set; }

    /// <summary>Gets elapsed smelting progress for the current input item in seconds.</summary>
    public float CookProgressSeconds { get; private set; }

    /// <summary>Gets whether the furnace fire is currently lit (emits block light <c>13</c>).</summary>
    public bool IsLit => BurnTimeRemaining > 0.001f;

    /// <summary>Gets normalized flame height in <c>[0.0, 1.0]</c> for UI rendering.</summary>
    public float NormalizedBurnRemaining => CurrentFuelTotalSeconds > 0.001f
        ? Math.Clamp(BurnTimeRemaining / CurrentFuelTotalSeconds, 0.0f, 1.0f)
        : 0.0f;

    /// <summary>Gets normalized smelting arrow progress in <c>[0.0, 1.0]</c> for UI rendering.</summary>
    public float NormalizedCookProgress =>
        Math.Clamp(CookProgressSeconds / FurnaceRecipes.CookDurationSeconds, 0.0f, 1.0f);

    /// <summary>
    /// Advances furnace fuel consumption and smelting progress by <paramref name="deltaSeconds"/>.
    /// Returns <see langword="true"/> if <see cref="IsLit"/> toggled during this step (requiring block-state update).
    /// </summary>
    public bool Tick(float deltaSeconds, FurnaceRecipes recipes, IRegistry<ItemDefinition> items)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(items);

        if (deltaSeconds <= 0.0f)
        {
            return false;
        }

        var wasLit = IsLit;
        if (BurnTimeRemaining > 0.0f)
        {
            BurnTimeRemaining = MathF.Max(0.0f, BurnTimeRemaining - deltaSeconds);
        }

        var canSmelt = CanSmeltCurrentInput(recipes, items, out var expectedOutput);
        if (!IsLit && canSmelt && !FuelSlot.IsEmpty)
        {
            var fuelBurn = recipes.GetFuelBurnSeconds(FuelSlot.ItemId);
            if (fuelBurn > 0.0f)
            {
                FuelSlot = FuelSlot.WithCount(FuelSlot.Count - 1);
                BurnTimeRemaining = fuelBurn;
                CurrentFuelTotalSeconds = fuelBurn;
            }
        }

        if (IsLit && canSmelt)
        {
            CookProgressSeconds += deltaSeconds;
            while (CookProgressSeconds >= FurnaceRecipes.CookDurationSeconds && CanSmeltCurrentInput(recipes, items, out expectedOutput))
            {
                CookProgressSeconds -= FurnaceRecipes.CookDurationSeconds;
                InputSlot = InputSlot.WithCount(InputSlot.Count - 1);
                OutputSlot = OutputSlot.IsEmpty
                    ? expectedOutput
                    : OutputSlot.WithCount(OutputSlot.Count + expectedOutput.Count);
            }
        }
        else if (!canSmelt)
        {
            CookProgressSeconds = 0.0f;
        }
        else if (!IsLit && CookProgressSeconds > 0.0f)
        {
            CookProgressSeconds = MathF.Max(0.0f, CookProgressSeconds - (deltaSeconds * 2.0f));
        }

        return wasLit != IsLit;
    }

    private bool CanSmeltCurrentInput(FurnaceRecipes recipes, IRegistry<ItemDefinition> items, out ItemStack output)
    {
        output = ItemStack.Empty;
        if (InputSlot.IsEmpty)
        {
            return false;
        }

        output = recipes.GetSmeltResult(InputSlot.ItemId);
        if (output.IsEmpty)
        {
            return false;
        }

        if (OutputSlot.IsEmpty)
        {
            return true;
        }

        if (!OutputSlot.CanMergeWith(output))
        {
            return false;
        }

        var maxStack = items.TryGetById(output.ItemId, out var def) ? Math.Max(1, def.MaxStackSize) : 64;
        return OutputSlot.Count + output.Count <= maxStack;
    }
}
