using System.Collections.Frozen;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Blocks;

/// <summary>
/// Immutable flyweight metadata associated with an interned <see cref="BlockState"/>.
/// </summary>
public sealed class BlockStateMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlockStateMetadata"/> class.
    /// </summary>
    /// <param name="state">Interned state handle.</param>
    /// <param name="block">Owning block definition.</param>
    /// <param name="blockRawId">Dense raw ID of the owning block.</param>
    /// <param name="properties">Frozen map of property names to values for this state.</param>
    public BlockStateMetadata(
        BlockState state,
        BlockDefinition block,
        ushort blockRawId,
        FrozenDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(properties);

        State = state;
        Block = block;
        BlockKey = block.Key;
        BlockRawId = blockRawId;
        Properties = properties;
    }

    /// <summary>Gets the interned <see cref="BlockState"/> handle.</summary>
    public BlockState State { get; }

    /// <summary>Gets the owning <see cref="BlockDefinition"/>.</summary>
    public BlockDefinition Block { get; }

    /// <summary>Gets the owning block's <see cref="ResourceKey"/>.</summary>
    public ResourceKey BlockKey { get; }

    /// <summary>Gets the dense block registry ID.</summary>
    public ushort BlockRawId { get; }

    /// <summary>Gets the frozen property map for this specific state.</summary>
    public FrozenDictionary<string, string> Properties { get; }

    /// <summary>Gets a value indicating whether the block has a solid collision volume.</summary>
    public bool IsSolid => Block.IsSolid;

    /// <summary>Gets a value indicating whether the block fully occludes sky and block light.</summary>
    public bool IsOpaque => Block.IsOpaque;

    /// <summary>Gets a value indicating whether the block behaves as a fluid.</summary>
    public bool IsFluid => Block.IsFluid;

    /// <summary>Gets the intrinsic block light emission in <c>[0, 15]</c>.</summary>
    public byte LightEmission => Block.LightEmission;

    /// <summary>Gets the mining hardness (<c>-1</c> for indestructible blocks).</summary>
    public float Hardness => Block.Hardness;

    /// <summary>
    /// Formats a canonical textual representation such as <c>overhorizon:grass_block[snowy=false]</c>.
    /// </summary>
    /// <returns>Canonical state string.</returns>
    public override string ToString()
    {
        if (Properties.Count == 0)
        {
            return BlockKey.ToString();
        }

        var pairs = Properties
            .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
            .Select(static kv => $"{kv.Key}={kv.Value}");
        return $"{BlockKey}[{string.Join(',', pairs)}]";
    }
}
