using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Client;

/// <summary>
/// Manages the 9-slot quick-build block hotbar (<c>1..9</c> keys and mouse scroll wheel).
/// </summary>
public sealed class ClientHotbar
{
    private static readonly ResourceKey[] DefaultSlotKeys =
    [
        BuiltInBlocks.Cobblestone,
        BuiltInBlocks.Stone,
        BuiltInBlocks.Dirt,
        BuiltInBlocks.GrassBlock,
        BuiltInBlocks.OakPlanks,
        BuiltInBlocks.OakLog,
        BuiltInBlocks.Sand,
        BuiltInBlocks.OakLeaves,
        BuiltInBlocks.Glowstone,
    ];

    private static readonly Vector3[] DefaultSlotColors =
    [
        new Vector3(0.46f, 0.46f, 0.48f), // 1: Cobblestone
        new Vector3(0.56f, 0.56f, 0.58f), // 2: Stone
        new Vector3(0.53f, 0.37f, 0.24f), // 3: Dirt
        new Vector3(0.36f, 0.69f, 0.26f), // 4: Grass Block
        new Vector3(0.72f, 0.57f, 0.35f), // 5: Oak Planks
        new Vector3(0.42f, 0.31f, 0.19f), // 6: Oak Log
        new Vector3(0.88f, 0.83f, 0.60f), // 7: Sand
        new Vector3(0.22f, 0.53f, 0.18f), // 8: Oak Leaves
        new Vector3(0.98f, 0.86f, 0.42f), // 9: Glowstone (lamp)
    ];

    private readonly BlockState[] _slotStates;
    private int _selectedSlot;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientHotbar"/> class.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    public ClientHotbar(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        _slotStates = new BlockState[DefaultSlotKeys.Length];
        for (var i = 0; i < DefaultSlotKeys.Length; i++)
        {
            _slotStates[i] = blocks.GetDefaultState(DefaultSlotKeys[i]);
        }
    }

    /// <summary>Gets the 9 RGB color swatches rendered in the 2D HUD hotbar.</summary>
    public static ReadOnlyMemory<Vector3> SlotColors => DefaultSlotColors;

    /// <summary>Gets the number of slots in the hotbar (<c>9</c>).</summary>
    public int SlotCount => _slotStates.Length;

    /// <summary>Gets the active zero-based slot index (<c>0..8</c>).</summary>
    public int SelectedSlot => _selectedSlot;

    /// <summary>Gets the <see cref="ResourceKey"/> of the currently selected building block.</summary>
    public ResourceKey SelectedBlockKey => DefaultSlotKeys[_selectedSlot];

    /// <summary>Gets the default <see cref="BlockState"/> of the currently selected building block.</summary>
    public BlockState SelectedBlockState => _slotStates[_selectedSlot];

    /// <summary>
    /// Selects a specific zero-based hotbar slot (<c>0..8</c>).
    /// </summary>
    /// <param name="slotIndex">Zero-based slot index.</param>
    public void SelectSlot(int slotIndex) =>
        _selectedSlot = Math.Clamp(slotIndex, 0, _slotStates.Length - 1);

    /// <summary>
    /// Cycles the active hotbar slot by <paramref name="delta"/> with wrap-around.
    /// </summary>
    /// <param name="delta">Signed step offset.</param>
    public void CycleSlot(int delta)
    {
        var next = (_selectedSlot + delta) % _slotStates.Length;
        if (next < 0)
        {
            next += _slotStates.Length;
        }

        _selectedSlot = next;
    }
}
