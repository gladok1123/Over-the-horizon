using System.Numerics;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Emits 3D cuboid parts mapped with standard Minecraft <c>(texU, texV, dx, dy, dz)</c> pixel UV coordinates
/// into <see cref="EntityTextureAtlasGenerator"/> (<c>512x256</c>, encoded via <c>uv.x = -2.0 - u</c>).
/// </summary>
internal static class EntityBoxUvBuilder
{
    public static void EmitEntityBox(
        List<OverlayVertex> dst,
        int cell,
        int u,
        int v,
        int dx,
        int dy,
        int dz,
        Vector3 min,
        Vector3 size,
        Matrix4x4 transform,
        Vector4 baseTint)
    {
        var max = min + size;
        var c000 = Vector3.Transform(new Vector3(min.X, min.Y, min.Z), transform);
        var c100 = Vector3.Transform(new Vector3(max.X, min.Y, min.Z), transform);
        var c010 = Vector3.Transform(new Vector3(min.X, max.Y, min.Z), transform);
        var c110 = Vector3.Transform(new Vector3(max.X, max.Y, min.Z), transform);
        var c001 = Vector3.Transform(new Vector3(min.X, min.Y, max.Z), transform);
        var c101 = Vector3.Transform(new Vector3(max.X, min.Y, max.Z), transform);
        var c011 = Vector3.Transform(new Vector3(min.X, max.Y, max.Z), transform);
        var c111 = Vector3.Transform(new Vector3(max.X, max.Y, max.Z), transform);

        // Front (+Z): [u + dz, v + dz] .. [u + dz + dx, v + dz + dy]
        EmitEntityFace(dst, cell, c001, c101, c111, c011, u + dz, v + dz, u + dz + dx, v + dz + dy, Shade(baseTint, 0.92f));

        // Back (-Z): [u + 2*dz + dx, v + dz] .. [u + 2*dz + 2*dx, v + dz + dy]
        EmitEntityFace(dst, cell, c100, c000, c010, c110, u + (2 * dz) + dx, v + dz, u + (2 * dz) + (2 * dx), v + dz + dy, Shade(baseTint, 0.80f));

        // Right (-X): [u, v + dz] .. [u + dz, v + dz + dy]
        EmitEntityFace(dst, cell, c000, c001, c011, c010, u, v + dz, u + dz, v + dz + dy, Shade(baseTint, 0.74f));

        // Left (+X): [u + dz + dx, v + dz] .. [u + 2*dz + dx, v + dz + dy]
        EmitEntityFace(dst, cell, c101, c100, c110, c111, u + dz + dx, v + dz, u + (2 * dz) + dx, v + dz + dy, Shade(baseTint, 0.74f));

        // Top (+Y): [u + dz, v] .. [u + dz + dx, v + dz]
        EmitEntityFace(dst, cell, c010, c011, c111, c110, u + dz, v, u + dz + dx, v + dz, Shade(baseTint, 1.00f));

        // Bottom (-Y): [u + dz + dx, v] .. [u + dz + 2*dx, v + dz]
        EmitEntityFace(dst, cell, c000, c100, c101, c001, u + dz + dx, v, u + dz + (2 * dx), v + dz, Shade(baseTint, 0.56f));
    }

    internal static void EmitEntityFace(
        List<OverlayVertex> dst,
        int cell,
        Vector3 pBL,
        Vector3 pBR,
        Vector3 pTR,
        Vector3 pTL,
        float px0,
        float py0,
        float px1,
        float py1,
        Vector4 color)
    {
        var cellX = (cell & 7) * 64.0f;
        var cellY = (cell >> 3) * 64.0f;

        var u0 = (cellX + px0 + 0.04f) / EntityTextureAtlasGenerator.Width;
        var u1 = (cellX + px1 - 0.04f) / EntityTextureAtlasGenerator.Width;
        var v0 = (cellY + py0 + 0.04f) / EntityTextureAtlasGenerator.Height;
        var v1 = (cellY + py1 - 0.04f) / EntityTextureAtlasGenerator.Height;

        var uvBL = new Vector2(-2.0f - u0, v1);
        var uvBR = new Vector2(-2.0f - u1, v1);
        var uvTR = new Vector2(-2.0f - u1, v0);
        var uvTL = new Vector2(-2.0f - u0, v0);

        dst.Add(new OverlayVertex(pBL, color, uvBL));
        dst.Add(new OverlayVertex(pBR, color, uvBR));
        dst.Add(new OverlayVertex(pTR, color, uvTR));
        dst.Add(new OverlayVertex(pBL, color, uvBL));
        dst.Add(new OverlayVertex(pTR, color, uvTR));
        dst.Add(new OverlayVertex(pTL, color, uvTL));
    }

    private static Vector4 Shade(Vector4 c, float factor) =>
        new(c.X * factor, c.Y * factor, c.Z * factor, c.W);
}
