using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.World.Storage;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Bundles a target <see cref="ChunkSnapshot"/> with its 4 optional horizontal neighbor snapshots
/// for lock-free cross-chunk face culling and smooth ambient occlusion sampling.
/// </summary>
/// <param name="Center">Center chunk column snapshot to mesh.</param>
/// <param name="North">Optional neighbor at <c>Z - 1</c>.</param>
/// <param name="South">Optional neighbor at <c>Z + 1</c>.</param>
/// <param name="West">Optional neighbor at <c>X - 1</c>.</param>
/// <param name="East">Optional neighbor at <c>X + 1</c>.</param>
public readonly record struct ChunkNeighborhood(
    ChunkSnapshot Center,
    ChunkSnapshot? North = null,
    ChunkSnapshot? South = null,
    ChunkSnapshot? West = null,
    ChunkSnapshot? East = null)
{
    /// <summary>
    /// Samples a <see cref="BlockState"/> at center-relative X/Z (<c>-1..16</c>) and world Y.
    /// Out-of-bounds Y or unloaded neighbor columns return <see cref="BlockState.Air"/>.
    /// </summary>
    /// <param name="relX">Center-relative X in <c>[-1, 16]</c>.</param>
    /// <param name="worldY">World Y coordinate.</param>
    /// <param name="relZ">Center-relative Z in <c>[-1, 16]</c>.</param>
    /// <returns>Sampled <see cref="BlockState"/>.</returns>
    public BlockState GetBlockState(int relX, int worldY, int relZ)
    {
        if (worldY < EngineConstants.WorldMinY || worldY >= EngineConstants.WorldMaxY)
        {
            return BlockState.Air;
        }

        if ((uint)relX < 16U && (uint)relZ < 16U)
        {
            return Center.GetBlockState(relX, worldY, relZ);
        }

        var (neighbor, nx, nz) = ResolveNeighbor(relX, relZ);
        return neighbor is not null ? neighbor.GetBlockState(nx, worldY, nz) : BlockState.Air;
    }

    /// <summary>
    /// Samples the combined sky and block light (<c>0..15</c>) at center-relative X/Z and world Y.
    /// </summary>
    /// <param name="relX">Center-relative X in <c>[-1, 16]</c>.</param>
    /// <param name="worldY">World Y coordinate.</param>
    /// <param name="relZ">Center-relative Z in <c>[-1, 16]</c>.</param>
    /// <returns>Tuple of <c>(SkyLight, BlockLight)</c> in <c>[0, 15]</c>.</returns>
    public (byte SkyLight, byte BlockLight) GetLight(int relX, int worldY, int relZ)
    {
        if (worldY >= EngineConstants.WorldMaxY)
        {
            return (15, 0);
        }

        if (worldY < EngineConstants.WorldMinY)
        {
            return (0, 0);
        }

        if ((uint)relX < 16U && (uint)relZ < 16U)
        {
            return (Center.GetSkyLight(relX, worldY, relZ), Center.GetBlockLight(relX, worldY, relZ));
        }

        var (neighbor, nx, nz) = ResolveNeighbor(relX, relZ);
        return neighbor is not null
            ? (neighbor.GetSkyLight(nx, worldY, nz), neighbor.GetBlockLight(nx, worldY, nz))
            : ((byte)15, (byte)0);
    }

    private (ChunkSnapshot? Target, int LocalX, int LocalZ) ResolveNeighbor(int relX, int relZ)
    {
        // Orthogonal neighbors cover face culling; diagonal corners outside loaded data safely fall back to air/sky.
        if (relX < 0 && (uint)relZ < 16U)
        {
            return (West, relX + 16, relZ);
        }

        if (relX >= 16 && (uint)relZ < 16U)
        {
            return (East, relX - 16, relZ);
        }

        if (relZ < 0 && (uint)relX < 16U)
        {
            return (North, relX, relZ + 16);
        }

        if (relZ >= 16 && (uint)relX < 16U)
        {
            return (South, relX, relZ - 16);
        }

        return (null, 0, 0);
    }
}
