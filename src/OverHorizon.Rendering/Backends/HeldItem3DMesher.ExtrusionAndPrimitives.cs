using System.Numerics;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

public static partial class HeldItem3DMesher
{
    internal static void EmitExtruded16x16Mesh(
        List<OverlayVertex> dst,
        TextureAtlasData atlas,
        int tileIndex,
        float scale,
        Matrix4x4 rot,
        float light)
    {
        var halfThick = scale * (0.5f / 16.0f);
        var pxSize = scale / 16.0f;
        var (u0, v0, u1, v1) = TileUvBounds(tileIndex, 0.002f, 0.002f, 0.998f, 0.998f);
        var half = scale * 0.5f;
        var frontCol = new Vector4(light * 0.98f, light * 0.98f, light * 0.98f, 1.0f);
        var backCol = new Vector4(light * 0.88f, light * 0.88f, light * 0.88f, 1.0f);

        EmitTexturedQuad(
            dst,
            Vector3.Transform(new Vector3(-half, -half, halfThick), rot),
            Vector3.Transform(new Vector3(half, -half, halfThick), rot),
            Vector3.Transform(new Vector3(half, half, halfThick), rot),
            Vector3.Transform(new Vector3(-half, half, halfThick), rot),
            new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0),
            frontCol);

        EmitTexturedQuad(
            dst,
            Vector3.Transform(new Vector3(half, -half, -halfThick), rot),
            Vector3.Transform(new Vector3(-half, -half, -halfThick), rot),
            Vector3.Transform(new Vector3(-half, half, -halfThick), rot),
            Vector3.Transform(new Vector3(half, half, -halfThick), rot),
            new Vector2(u1, v1), new Vector2(u0, v1), new Vector2(u0, v0), new Vector2(u1, v0),
            backCol);

        var pixels = atlas.RgbaPixels.Span;
        var tileOriginX = (tileIndex & 15) * 16;
        var tileOriginY = (tileIndex >> 4) * 16;

        for (var py = 0; py < 16; py++)
        {
            var yTop = half - (py * pxSize);
            var yBot = yTop - pxSize;
            for (var px = 0; px < 16; px++)
            {
                var idx = (((tileOriginY + py) * 256) + (tileOriginX + px)) * 4;
                if (pixels[idx + 3] < 24) continue;

                var r = (pixels[idx + 0] / 255.0f) * light;
                var g = (pixels[idx + 1] / 255.0f) * light;
                var b = (pixels[idx + 2] / 255.0f) * light;
                var xLeft = -half + (px * pxSize);
                var xRight = xLeft + pxSize;

                if (IsTransparent(pixels, tileOriginX, tileOriginY, px - 1, py))
                {
                    EmitUntexturedQuad(dst, rot, new Vector3(xLeft, yBot, -halfThick), new Vector3(xLeft, yBot, halfThick), new Vector3(xLeft, yTop, halfThick), new Vector3(xLeft, yTop, -halfThick), new Vector4(r * 0.72f, g * 0.72f, b * 0.72f, 1.0f));
                }

                if (IsTransparent(pixels, tileOriginX, tileOriginY, px + 1, py))
                {
                    EmitUntexturedQuad(dst, rot, new Vector3(xRight, yBot, halfThick), new Vector3(xRight, yBot, -halfThick), new Vector3(xRight, yTop, -halfThick), new Vector3(xRight, yTop, halfThick), new Vector4(r * 0.76f, g * 0.76f, b * 0.76f, 1.0f));
                }

                if (IsTransparent(pixels, tileOriginX, tileOriginY, px, py - 1))
                {
                    EmitUntexturedQuad(dst, rot, new Vector3(xLeft, yTop, halfThick), new Vector3(xRight, yTop, halfThick), new Vector3(xRight, yTop, -halfThick), new Vector3(xLeft, yTop, -halfThick), new Vector4(r * 0.96f, g * 0.96f, b * 0.96f, 1.0f));
                }

                if (IsTransparent(pixels, tileOriginX, tileOriginY, px, py + 1))
                {
                    EmitUntexturedQuad(dst, rot, new Vector3(xLeft, yBot, -halfThick), new Vector3(xRight, yBot, -halfThick), new Vector3(xRight, yBot, halfThick), new Vector3(xLeft, yBot, halfThick), new Vector4(r * 0.60f, g * 0.60f, b * 0.60f, 1.0f));
                }
            }
        }
    }

    private static bool IsTransparent(ReadOnlySpan<byte> atlasRgba, int ox, int oy, int px, int py) =>
        (uint)px >= 16U || (uint)py >= 16U || atlasRgba[((((oy + py) * 256) + (ox + px)) * 4) + 3] < 24;

    internal static (float U0, float V0, float U1, float V1) TileUvBounds(int tile, float lu0, float lv0, float lu1, float lv1)
    {
        var col = tile & 15;
        var row = tile >> 4;
        return ((col + lu0) / 16.0f, (row + lv0) / 16.0f, (col + lu1) / 16.0f, (row + lv1) / 16.0f);
    }

    internal static void EmitBlockCubeFace(List<OverlayVertex> dst, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Matrix4x4 m, int tile, float shade) =>
        EmitBlockCubeFaceTinted(dst, a, b, c, d, m, tile, new Vector4(shade, shade, shade, 1.0f));

    internal static void EmitBlockCubeFaceTinted(List<OverlayVertex> dst, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Matrix4x4 m, int tile, Vector4 tint)
    {
        var (u0, v0, u1, v1) = TileUvBounds(tile, 0.004f, 0.004f, 0.996f, 0.996f);
        EmitTexturedQuad(dst, Vector3.Transform(a, m), Vector3.Transform(b, m), Vector3.Transform(c, m), Vector3.Transform(d, m), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), tint);
    }

    private static void EmitTexturedBoxSingleUv(List<OverlayVertex> dst, Vector3 min, Vector3 size, Matrix4x4 m, float u0, float v0, float u1, float v1, float light)
    {
        var max = min + size;
        var sideCol = new Vector4(light * 0.92f, light * 0.92f, light * 0.92f, 1.0f);
        var topCol = new Vector4(1.0f, 0.96f, 0.85f, 1.0f);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(min.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, max.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, max.Z), m), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), sideCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(max.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(min.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, min.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, min.Z), m), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), sideCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(min.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(min.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, max.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, min.Z), m), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), sideCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(max.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, min.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, max.Z), m), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), sideCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(min.X, max.Y, min.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, min.Z), m), new Vector2(u0, v0), new Vector2(u0, v0 + 0.01f), new Vector2(u1, v0 + 0.01f), new Vector2(u1, v0), topCol);
    }

    internal static void EmitTexturedQuad(List<OverlayVertex> dst, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector4 col)
    {
        dst.Add(new OverlayVertex(p0, col, uv0));
        dst.Add(new OverlayVertex(p1, col, uv1));
        dst.Add(new OverlayVertex(p2, col, uv2));
        dst.Add(new OverlayVertex(p0, col, uv0));
        dst.Add(new OverlayVertex(p2, col, uv2));
        dst.Add(new OverlayVertex(p3, col, uv3));
    }

    private static void EmitUntexturedQuad(List<OverlayVertex> dst, Matrix4x4 m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 col)
    {
        dst.Add(new OverlayVertex(Vector3.Transform(a, m), col));
        dst.Add(new OverlayVertex(Vector3.Transform(b, m), col));
        dst.Add(new OverlayVertex(Vector3.Transform(c, m), col));
        dst.Add(new OverlayVertex(Vector3.Transform(a, m), col));
        dst.Add(new OverlayVertex(Vector3.Transform(c, m), col));
        dst.Add(new OverlayVertex(Vector3.Transform(d, m), col));
    }
}
