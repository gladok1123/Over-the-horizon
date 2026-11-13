using System.Numerics;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Builds authentic Minecraft first-person 3D held viewmodels:
/// 6-face textured 3D blocks, 3D torches/lanterns, <c>16x16</c> pixel-extruded 3D tools/swords/items,
/// and angled right-arm empty hand — all shaded by <c>0..15</c> world illumination (<see cref="FrameOverlayState.HeldLightBrightness"/>).
/// </summary>
public static class HeldItem3DMesher
{
    /// <summary>Appends the first-person 3D held block, torch, or pixel-extruded item mesh into <paramref name="dst"/>.</summary>
    /// <param name="dst">Destination overlay vertex buffer.</param>
    /// <param name="state">Current frame overlay state.</param>
    /// <param name="atlas">Active <c>256x256</c> block &amp; item texture atlas.</param>
    public static void AppendHeldViewmodel(
        List<OverlayVertex> dst,
        FrameOverlayState state,
        TextureAtlasData? atlas)
    {
        var bobX = MathF.Sin(state.WalkBobPhase * 0.5f) * 0.032f;
        var bobY = MathF.Abs(MathF.Cos(state.WalkBobPhase * 0.5f)) * 0.030f;

        var s = Math.Clamp(state.HandSwingProgress, 0.0f, 1.0f);
        var swingSin = MathF.Sin(s * MathF.PI);
        var swingSinSq = MathF.Sin(MathF.Sqrt(s) * MathF.PI);

        var light = Math.Clamp(state.HeldLightBrightness, 0.045f, 1.0f);

        if (state.HeldIsEmpty)
        {
            AppendEmptyHandArm(dst, bobX, bobY, swingSin, swingSinSq, light);
            return;
        }

        if (state.HeldIsTorchStyle && state.HeldItemSpriteTile >= 0)
        {
            AppendHeldTorchOrLantern(dst, state.HeldItemSpriteTile, bobX, bobY, swingSin, swingSinSq, MathF.Max(light, 0.92f));
            return;
        }

        if (state.HeldIsBlock3D && state.HeldSideTile >= 0)
        {
            AppendTextured3DBlock(dst, state, bobX, bobY, swingSin, swingSinSq, light);
            return;
        }

        if (state.HeldItemSpriteTile >= 0 && atlas is not null)
        {
            AppendExtruded16x16Item(
                dst,
                atlas,
                state.HeldItemSpriteTile,
                state.HeldIsToolOrWeapon,
                bobX,
                bobY,
                swingSin,
                swingSinSq,
                light);
            return;
        }

        AppendEmptyHandArm(dst, bobX, bobY, swingSin, swingSinSq, light);
    }

    private static void AppendEmptyHandArm(
        List<OverlayVertex> dst,
        float bobX,
        float bobY,
        float swingSin,
        float swingSinSq,
        float light)
    {
        var pivot = new Vector3(0.56f + bobX - (swingSinSq * 0.22f), -0.52f - bobY + (swingSin * 0.14f), -0.72f - (swingSin * 0.18f));
        var rot = Matrix4x4.CreateRotationZ(0.42f + (swingSin * 0.35f)) *
                  Matrix4x4.CreateRotationX(-0.92f - (swingSinSq * 0.95f)) *
                  Matrix4x4.CreateRotationY(-0.38f + (swingSin * 0.45f)) *
                  Matrix4x4.CreateTranslation(pivot);

        var skin = new Vector4(0.86f * light, 0.67f * light, 0.53f * light, 1.0f);
        var sleeve = new Vector4(0.20f * light, 0.56f * light, 0.72f * light, 1.0f);

        EmitUntexturedBox(dst, new Vector3(-0.09f, -0.26f, -0.09f), new Vector3(0.18f, 0.30f, 0.18f), rot, sleeve);
        EmitUntexturedBox(dst, new Vector3(-0.085f, 0.04f, -0.085f), new Vector3(0.17f, 0.28f, 0.17f), rot, skin);
    }

    private static void AppendHeldTorchOrLantern(
        List<OverlayVertex> dst,
        int tile,
        float bobX,
        float bobY,
        float swingSin,
        float swingSinSq,
        float light)
    {
        var pivot = new Vector3(0.50f + bobX - (swingSinSq * 0.20f), -0.46f - bobY + (swingSin * 0.10f), -0.74f - (swingSin * 0.16f));
        var rot = Matrix4x4.CreateRotationZ(0.18f) *
                  Matrix4x4.CreateRotationX(-0.24f - (swingSinSq * 0.85f)) *
                  Matrix4x4.CreateRotationY(0.52f + (swingSin * 0.35f)) *
                  Matrix4x4.CreateTranslation(pivot);

        var (tu0, tv0, tu1, tv1) = TileUvBounds(tile, 0.4375f, 0.375f, 0.5625f, 1.0f);
        EmitTexturedBoxSingleUv(dst, new Vector3(-0.045f, -0.14f, -0.045f), new Vector3(0.09f, 0.46f, 0.09f), rot, tu0, tv0, tu1, tv1, light);
    }

    private static void AppendTextured3DBlock(
        List<OverlayVertex> dst,
        FrameOverlayState state,
        float bobX,
        float bobY,
        float swingSin,
        float swingSinSq,
        float light)
    {
        var pivot = new Vector3(0.54f + bobX - (swingSinSq * 0.22f), -0.44f - bobY + (swingSin * 0.12f), -0.82f - (swingSin * 0.18f));
        var rot = Matrix4x4.CreateRotationY(0.785f + (swingSin * 0.42f)) *
                  Matrix4x4.CreateRotationX(0.22f - (swingSinSq * 0.82f)) *
                  Matrix4x4.CreateRotationZ(-0.08f * swingSin) *
                  Matrix4x4.CreateTranslation(pivot);

        const float hs = 0.185f;
        var topT = state.HeldTopTile >= 0 ? state.HeldTopTile : state.HeldSideTile;
        var botT = state.HeldBottomTile >= 0 ? state.HeldBottomTile : state.HeldSideTile;
        var frtT = state.HeldFrontTile >= 0 ? state.HeldFrontTile : state.HeldSideTile;
        var sidT = state.HeldSideTile;

        EmitBlockCubeFace(dst, new Vector3(-hs, hs, -hs), new Vector3(-hs, hs, hs), new Vector3(hs, hs, hs), new Vector3(hs, hs, -hs), rot, topT, light * 1.00f);
        EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(hs, -hs, -hs), new Vector3(hs, -hs, hs), new Vector3(-hs, -hs, hs), rot, botT, light * 0.55f);
        EmitBlockCubeFace(dst, new Vector3(-hs, -hs, hs), new Vector3(hs, -hs, hs), new Vector3(hs, hs, hs), new Vector3(-hs, hs, hs), rot, frtT, light * 0.84f);
        EmitBlockCubeFace(dst, new Vector3(hs, -hs, -hs), new Vector3(-hs, -hs, -hs), new Vector3(-hs, hs, -hs), new Vector3(hs, hs, -hs), rot, sidT, light * 0.80f);
        EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(-hs, -hs, hs), new Vector3(-hs, hs, hs), new Vector3(-hs, hs, -hs), rot, sidT, light * 0.70f);
        EmitBlockCubeFace(dst, new Vector3(hs, -hs, hs), new Vector3(hs, -hs, -hs), new Vector3(hs, hs, -hs), new Vector3(hs, hs, hs), rot, sidT, light * 0.68f);
    }

    private static void AppendExtruded16x16Item(
        List<OverlayVertex> dst,
        TextureAtlasData atlas,
        int tileIndex,
        bool isToolOrWeapon,
        float bobX,
        float bobY,
        float swingSin,
        float swingSinSq,
        float light)
    {
        // Authentic Minecraft first-person grip: tools/swords point upward-forward from right hand; items/food face slightly inward!
        Matrix4x4 rot;
        if (isToolOrWeapon)
        {
            var pivot = new Vector3(0.52f + bobX - (swingSinSq * 0.28f), -0.44f - bobY + (swingSin * 0.08f), -0.76f - (swingSin * 0.24f));
            rot = Matrix4x4.CreateRotationZ(-0.62f - (swingSinSq * 0.55f)) *
                  Matrix4x4.CreateRotationY(1.38f + (swingSin * 0.35f)) *
                  Matrix4x4.CreateRotationX(-0.18f - (swingSinSq * 0.88f)) *
                  Matrix4x4.CreateTranslation(pivot);
        }
        else
        {
            var pivot = new Vector3(0.50f + bobX - (swingSinSq * 0.20f), -0.42f - bobY + (swingSin * 0.10f), -0.76f - (swingSin * 0.16f));
            rot = Matrix4x4.CreateRotationY(0.45f + (swingSin * 0.35f)) *
                  Matrix4x4.CreateRotationX(0.12f - (swingSinSq * 0.75f)) *
                  Matrix4x4.CreateTranslation(pivot);
        }

        var scale = isToolOrWeapon ? 0.56f : 0.42f;
        var halfThick = scale * (0.5f / 16.0f);
        var pxSize = scale / 16.0f;

        // 1. Front (+Z) and Back (-Z) full 16x16 textured faces (alpha-tested in shader!)
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

        // 2. True 3D pixel-edge extrusion along every opaque texel boundary!
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
                if (pixels[idx + 3] < 24)
                {
                    continue;
                }

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

    private static bool IsTransparent(ReadOnlySpan<byte> atlasRgba, int ox, int oy, int px, int py)
    {
        if ((uint)px >= 16U || (uint)py >= 16U)
        {
            return true;
        }

        return atlasRgba[((((oy + py) * 256) + (ox + px)) * 4) + 3] < 24;
    }

    private static (float U0, float V0, float U1, float V1) TileUvBounds(int tile, float lu0, float lv0, float lu1, float lv1)
    {
        var col = tile & 15;
        var row = tile >> 4;
        return ((col + lu0) / 16.0f, (row + lv0) / 16.0f, (col + lu1) / 16.0f, (row + lv1) / 16.0f);
    }

    private static void EmitBlockCubeFace(List<OverlayVertex> dst, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Matrix4x4 m, int tile, float shade)
    {
        var (u0, v0, u1, v1) = TileUvBounds(tile, 0.004f, 0.004f, 0.996f, 0.996f);
        var col = new Vector4(shade, shade, shade, 1.0f);
        EmitTexturedQuad(dst, Vector3.Transform(a, m), Vector3.Transform(b, m), Vector3.Transform(c, m), Vector3.Transform(d, m), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), col);
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

    private static void EmitTexturedQuad(List<OverlayVertex> dst, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector4 col)
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
        var p0 = Vector3.Transform(a, m);
        var p1 = Vector3.Transform(b, m);
        var p2 = Vector3.Transform(c, m);
        var p3 = Vector3.Transform(d, m);
        dst.Add(new OverlayVertex(p0, col));
        dst.Add(new OverlayVertex(p1, col));
        dst.Add(new OverlayVertex(p2, col));
        dst.Add(new OverlayVertex(p0, col));
        dst.Add(new OverlayVertex(p2, col));
        dst.Add(new OverlayVertex(p3, col));
    }

    private static void EmitUntexturedBox(List<OverlayVertex> dst, Vector3 min, Vector3 size, Matrix4x4 m, Vector4 col)
    {
        var max = min + size;
        EmitUntexturedQuad(dst, m, new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), col);
        EmitUntexturedQuad(dst, m, new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), col * new Vector4(0.6f, 0.6f, 0.6f, 1.0f));
        EmitUntexturedQuad(dst, m, new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), col * new Vector4(0.85f, 0.85f, 0.85f, 1.0f));
        EmitUntexturedQuad(dst, m, new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), col * new Vector4(0.80f, 0.80f, 0.80f, 1.0f));
        EmitUntexturedQuad(dst, m, new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z), col * new Vector4(0.72f, 0.72f, 0.72f, 1.0f));
        EmitUntexturedQuad(dst, m, new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), col * new Vector4(0.72f, 0.72f, 0.72f, 1.0f));
    }
}
