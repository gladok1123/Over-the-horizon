using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Client;

/// <summary>
/// Manages the 9-slot quick-build hotbar and the 22-block Creative Block Palette (<c>E</c> key).
/// </summary>
public sealed class ClientHotbar
{
    private static readonly (ResourceKey Key, string DisplayName, Vector3 Color)[] PaletteEntries =
    [
        (BuiltInBlocks.Cobblestone, "Cobblestone", new Vector3(0.46f, 0.46f, 0.48f)),           // 0
        (BuiltInBlocks.Stone, "Stone", new Vector3(0.56f, 0.56f, 0.58f)),                       // 1
        (BuiltInBlocks.Dirt, "Dirt", new Vector3(0.53f, 0.37f, 0.24f)),                         // 2
        (BuiltInBlocks.GrassBlock, "Grass Block", new Vector3(0.36f, 0.69f, 0.26f)),            // 3
        (BuiltInBlocks.OakPlanks, "Oak Planks", new Vector3(0.72f, 0.57f, 0.35f)),              // 4
        (BuiltInBlocks.OakLog, "Oak Log", new Vector3(0.42f, 0.31f, 0.19f)),                    // 5
        (BuiltInBlocks.Sand, "Sand", new Vector3(0.88f, 0.83f, 0.60f)),                         // 6
        (BuiltInBlocks.OakLeaves, "Oak Leaves", new Vector3(0.22f, 0.53f, 0.18f)),              // 7
        (BuiltInBlocks.Glowstone, "Glowstone Lamp", new Vector3(0.98f, 0.86f, 0.42f)),          // 8
        (BuiltInBlocks.StoneBricks, "Stone Bricks", new Vector3(0.52f, 0.52f, 0.55f)),          // 9
        (BuiltInBlocks.Bricks, "Clay Bricks", new Vector3(0.66f, 0.31f, 0.25f)),                // 10
        (BuiltInBlocks.Glass, "Glass Block", new Vector3(0.72f, 0.90f, 0.96f)),                 // 11
        (BuiltInBlocks.Sandstone, "Sandstone", new Vector3(0.85f, 0.79f, 0.58f)),               // 12
        (BuiltInBlocks.Bookshelf, "Bookshelf", new Vector3(0.62f, 0.44f, 0.28f)),               // 13
        (BuiltInBlocks.CraftingTable, "Crafting Table", new Vector3(0.64f, 0.47f, 0.28f)),      // 14
        (BuiltInBlocks.MossyCobblestone, "Mossy Cobblestone", new Vector3(0.36f, 0.52f, 0.34f)), // 15
        (BuiltInBlocks.SnowBlock, "Snow Block", new Vector3(0.94f, 0.96f, 0.98f)),              // 16
        (BuiltInBlocks.CrimsonWool, "Crimson Wool", new Vector3(0.74f, 0.18f, 0.22f)),          // 17
        (BuiltInBlocks.CoalOre, "Coal Ore", new Vector3(0.24f, 0.24f, 0.26f)),                  // 18
        (BuiltInBlocks.IronOre, "Iron Ore", new Vector3(0.78f, 0.66f, 0.56f)),                  // 19
        (BuiltInBlocks.GoldOre, "Gold Ore", new Vector3(0.95f, 0.82f, 0.24f)),                  // 20
        (BuiltInBlocks.DiamondOre, "Diamond Ore", new Vector3(0.32f, 0.92f, 0.88f)),            // 21
    ];

    private readonly BlockState[] _paletteStates;
    private readonly int[] _hotbarPaletteIndices = [0, 1, 2, 3, 4, 5, 6, 7, 8];
    private readonly Vector3[] _hotbarColors = new Vector3[9];
    private int _selectedSlot;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientHotbar"/> class.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    public ClientHotbar(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        _paletteStates = new BlockState[PaletteEntries.Length];
        for (var i = 0; i < PaletteEntries.Length; i++)
        {
            _paletteStates[i] = blocks.GetDefaultState(PaletteEntries[i].Key);
        }

        RefreshSlotColors();
    }

    /// <summary>Gets the total number of blocks in the Creative Block Palette.</summary>
    public static int PaletteCount => PaletteEntries.Length;

    /// <summary>Gets the number of slots in the hotbar (<c>9</c>).</summary>
    public int SlotCount => _hotbarPaletteIndices.Length;

    /// <summary>Gets the active zero-based slot index (<c>0..8</c>).</summary>
    public int SelectedSlot => _selectedSlot;

    /// <summary>Gets the <see cref="ResourceKey"/> of the currently selected building block.</summary>
    public ResourceKey SelectedBlockKey => PaletteEntries[_hotbarPaletteIndices[_selectedSlot]].Key;

    /// <summary>Gets the display name of the currently selected building block.</summary>
    public string SelectedBlockDisplayName => PaletteEntries[_hotbarPaletteIndices[_selectedSlot]].DisplayName;

    /// <summary>Gets the representative RGB color of the currently selected building block.</summary>
    public Vector3 SelectedBlockColor => PaletteEntries[_hotbarPaletteIndices[_selectedSlot]].Color;

    /// <summary>Gets the default <see cref="BlockState"/> of the currently selected building block.</summary>
    public BlockState SelectedBlockState => _paletteStates[_hotbarPaletteIndices[_selectedSlot]];

    /// <summary>Gets the 9 RGB color swatches currently equipped in the 2D HUD hotbar.</summary>
    public ReadOnlyMemory<Vector3> ActiveSlotColors => _hotbarColors;

    /// <summary>Gets metadata for a specific Creative Palette entry.</summary>
    public static (ResourceKey Key, string DisplayName, Vector3 Color) GetPaletteEntry(int paletteIndex) =>
        PaletteEntries[Math.Clamp(paletteIndex, 0, PaletteEntries.Length - 1)];

    /// <summary>
    /// Selects a specific zero-based hotbar slot (<c>0..8</c>).
    /// </summary>
    public void SelectSlot(int slotIndex) =>
        _selectedSlot = Math.Clamp(slotIndex, 0, _hotbarPaletteIndices.Length - 1);

    /// <summary>
    /// Equips a block from the Creative Palette into the currently selected hotbar slot.
    /// </summary>
    public void EquipPaletteBlock(int paletteIndex)
    {
        _hotbarPaletteIndices[_selectedSlot] = Math.Clamp(paletteIndex, 0, PaletteEntries.Length - 1);
        RefreshSlotColors();
    }

    /// <summary>
    /// Cycles the active hotbar slot by <paramref name="delta"/> with wrap-around.
    /// </summary>
    public void CycleSlot(int delta)
    {
        var next = (_selectedSlot + delta) % _hotbarPaletteIndices.Length;
        if (next < 0)
        {
            next += _hotbarPaletteIndices.Length;
        }

        _selectedSlot = next;
    }

    private void RefreshSlotColors()
    {
        for (var i = 0; i < _hotbarPaletteIndices.Length; i++)
        {
            _hotbarColors[i] = PaletteEntries[_hotbarPaletteIndices[i]].Color;
        }
    }
}
