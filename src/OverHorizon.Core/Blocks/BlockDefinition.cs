using System.Collections.Immutable;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Blocks;

/// <summary>
/// Immutable descriptor of a block type and its state property schema.
/// </summary>
public sealed class BlockDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlockDefinition"/> class.
    /// </summary>
    /// <param name="key">Unique resource identifier.</param>
    /// <param name="isSolid">Whether the block has solid collision bounds.</param>
    /// <param name="isOpaque">Whether the block blocks light propagation.</param>
    /// <param name="isFluid">Whether the block is a fluid.</param>
    /// <param name="lightEmission">Light emission level in <c>[0, 15]</c>.</param>
    /// <param name="hardness">Mining hardness (<c>-1</c> for indestructible).</param>
    /// <param name="properties">Optional state properties.</param>
    public BlockDefinition(
        ResourceKey key,
        bool isSolid = true,
        bool isOpaque = true,
        bool isFluid = false,
        byte lightEmission = 0,
        float hardness = 1.0f,
        IEnumerable<BlockProperty>? properties = null)
    {
        if (lightEmission > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(lightEmission), "Light emission must be in [0, 15].");
        }

        Key = key;
        IsSolid = isSolid;
        IsOpaque = isOpaque;
        IsFluid = isFluid;
        LightEmission = lightEmission;
        Hardness = hardness;
        Properties = properties is null ? [] : [.. properties];
    }

    /// <summary>Gets the block's unique <see cref="ResourceKey"/>.</summary>
    public ResourceKey Key { get; }

    /// <summary>Gets a value indicating whether the block has solid collision bounds.</summary>
    public bool IsSolid { get; }

    /// <summary>Gets a value indicating whether the block occludes light.</summary>
    public bool IsOpaque { get; }

    /// <summary>Gets a value indicating whether the block is a fluid.</summary>
    public bool IsFluid { get; }

    /// <summary>Gets the intrinsic light emission in <c>[0, 15]</c>.</summary>
    public byte LightEmission { get; }

    /// <summary>Gets the mining hardness (<c>-1</c> for indestructible).</summary>
    public float Hardness { get; }

    /// <summary>Gets the schema of state properties declared by this block.</summary>
    public ImmutableArray<BlockProperty> Properties { get; }

    /// <summary>Gets the interned default <see cref="BlockState"/> assigned upon registry registration.</summary>
    public BlockState DefaultState { get; internal set; }

    /// <summary>Gets all interned <see cref="BlockState"/> variants belonging to this block.</summary>
    public ImmutableArray<BlockState> States { get; internal set; } = [];
}
