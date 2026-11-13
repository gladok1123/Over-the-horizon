using System.Numerics;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Builds authentic Minecraft first-person 3D held viewmodels for both Right Hand (Main Hand) and Left Hand (Offhand):
/// 6-face textured 3D blocks, 3D torches/lanterns, 3D shields, <c>16x16</c> pixel-extruded 3D tools/swords/items
/// (handle in hand, blade upright-forward!), and Steve right-arm empty hand.
/// </summary>
public static class HeldItem3DMesher
{
    /// <summary>Appends the first-person Right-Hand and Left-Hand (Offhand) 3D viewmodels into <paramref name="dst"/>.</summary>
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
        var light = Math.Clamp(state.HeldLightBrightness, 0.055f, 1.0f);

        // 1. Right Hand (Main Hand) — always renders item or empty Steve right arm, animated by HandSwingProgress!
        var activeSpriteTile = state.HeldItemSpriteTile;
        var bowCharge = state.HeldIsBow ? Math.Clamp(state.BowChargeNormalized, 0.0f, 1.0f) : 0.0f;
        if (state.HeldIsBow && bowCharge > 0.01f)
        {
            if (bowCharge >= 0.90f)
            {
                activeSpriteTile = ProceduralItemTileManifest.TileBowPulling2;
            }
            else if (bowCharge >= 0.55f)
            {
                activeSpriteTile = ProceduralItemTileManifest.TileBowPulling1;
            }
            else
            {
                activeSpriteTile = ProceduralItemTileManifest.TileBowPulling0;
            }
        }

        AppendSingleHand(
            dst,
            atlas,
            isOffhand: false,
            isEmpty: state.HeldIsEmpty,
            isShield: state.HeldIsShield,
            isTorchStyle: state.HeldIsTorchStyle,
            isBlock3D: state.HeldIsBlock3D,
            isToolOrWeapon: state.HeldIsToolOrWeapon,
            isBow: state.HeldIsBow,
            bowCharge: bowCharge,
            topTile: state.HeldTopTile,
            bottomTile: state.HeldBottomTile,
            frontTile: state.HeldFrontTile,
            sideTile: state.HeldSideTile,
            spriteTile: activeSpriteTile,
            bobX: bobX,
            bobY: bobY,
            swingSin: swingSin,
            swingSinSq: swingSinSq,
            light: light);

        // 2. Left Hand (Offhand) — renders in bottom-left whenever Offhand slot (40) holds an item!
        if (!state.OffhandIsEmpty)
        {
            AppendSingleHand(
                dst,
                atlas,
                isOffhand: true,
                isEmpty: false,
                isShield: state.OffhandIsShield,
                isTorchStyle: state.OffhandIsTorchStyle,
                isBlock3D: state.OffhandIsBlock3D,
                isToolOrWeapon: state.OffhandIsToolOrWeapon,
                isBow: false,
                bowCharge: 0.0f,
                topTile: state.OffhandTopTile,
                bottomTile: state.OffhandBottomTile,
                frontTile: state.OffhandFrontTile,
                sideTile: state.OffhandSideTile,
                spriteTile: state.OffhandItemSpriteTile,
                bobX: -bobX * 0.85f,
                bobY: bobY * 0.85f,
                swingSin: 0.0f,
                swingSinSq: 0.0f,
                light: light);
        }
    }

    private static void AppendSingleHand(
        List<OverlayVertex> dst,
        TextureAtlasData? atlas,
        bool isOffhand,
        bool isEmpty,
        bool isShield,
        bool isTorchStyle,
        bool isBlock3D,
        bool isToolOrWeapon,
        bool isBow,
        float bowCharge,
        int topTile,
        int bottomTile,
        int frontTile,
        int sideTile,
        int spriteTile,
        float bobX,
        float bobY,
        float swingSin,
        float swingSinSq,
        float light)
    {
        var xSign = isOffhand ? -1.0f : 1.0f;

        if (isEmpty)
        {
            AppendEmptyHandArm(dst, xSign, bobX, bobY, swingSin, swingSinSq, light);
            return;
        }

        if (isShield)
        {
            AppendHeldShield(dst, xSign, bobX, bobY, swingSin, swingSinSq, light);
            return;
        }

        if (isTorchStyle && spriteTile >= 0)
        {
            AppendHeldTorchOrLantern(dst, xSign, spriteTile, bobX, bobY, swingSin, swingSinSq, MathF.Max(light, 0.94f));
            return;
        }

        if (isBlock3D && sideTile >= 0)
        {
            AppendTextured3DBlock(dst, xSign, topTile, bottomTile, frontTile, sideTile, bobX, bobY, swingSin, swingSinSq, light);
            return;
        }

        if (spriteTile >= 0 && atlas is not null)
        {
            AppendExtruded16x16Item(dst, atlas, xSign, spriteTile, isToolOrWeapon, isBow, bowCharge, bobX, bobY, swingSin, swingSinSq, light);
            return;
        }

        AppendEmptyHandArm(dst, xSign, bobX, bobY, swingSin, swingSinSq, light);
    }

    private static void AppendEmptyHandArm(List<OverlayVertex> dst, float xSign, float bobX, float bobY, float swingSin, float swingSinSq, float light)
    {
        // EntityBoxUvBuilder places shoulder (cyan sleeve v=16..20) at +Y and hand/fist (tan skin v=28..32) at -Y.
        // RotationX(2.18f) = RotationX(pi - 0.96f) flips the box so -Y (tan skin fist!) points UP-AND-FORWARD (-Z) into screen center,
        // while +Y (cyan sleeve!) anchors DOWN-AND-BACK at the bottom-right shoulder!
        var pivot = new Vector3(
            (0.56f * xSign) + bobX - (swingSinSq * 0.24f * xSign),
            -0.50f - bobY + (swingSin * 0.14f),
            -0.72f - (swingSin * 0.22f));
        var rot = Matrix4x4.CreateRotationX(2.18f - (swingSinSq * 0.68f)) *
                  Matrix4x4.CreateRotationZ((0.40f + (swingSin * 0.34f)) * xSign) *
                  Matrix4x4.CreateRotationY((-0.34f + (swingSin * 0.42f)) * xSign) *
                  Matrix4x4.CreateTranslation(pivot);

        var tint = new Vector4(light, light, light, 1.0f);
        EntityBoxUvBuilder.EmitEntityBox(
            dst,
            EntityTextureAtlasGenerator.CellSteve,
            40, 16, 4, 12, 4,
            new Vector3(-0.085f, -0.28f, -0.085f),
            new Vector3(0.17f, 0.56f, 0.17f),
            rot,
            tint);
    }

    private static void AppendHeldShield(List<OverlayVertex> dst, float xSign, float bobX, float bobY, float swingSin, float swingSinSq, float light)
    {
        var pivot = new Vector3((0.52f * xSign) + bobX - (swingSinSq * 0.16f * xSign), -0.44f - bobY + (swingSin * 0.08f), -0.78f - (swingSin * 0.12f));
        var rot = Matrix4x4.CreateRotationY(-0.34f * xSign) *
                  Matrix4x4.CreateRotationX(0.08f - (swingSinSq * 0.45f)) *
                  Matrix4x4.CreateTranslation(pivot);

        var wood = new Vector4(0.64f * light, 0.47f * light, 0.27f * light, 1.0f);
        var rim = new Vector4(0.72f * light, 0.75f * light, 0.79f * light, 1.0f);
        EmitUntexturedBox(dst, new Vector3(-0.18f, -0.32f, -0.025f), new Vector3(0.36f, 0.64f, 0.05f), rot, wood);
        EmitUntexturedBox(dst, new Vector3(-0.195f, 0.28f, -0.032f), new Vector3(0.39f, 0.05f, 0.064f), rot, rim);
        EmitUntexturedBox(dst, new Vector3(-0.195f, -0.33f, -0.032f), new Vector3(0.39f, 0.05f, 0.064f), rot, rim);
        EmitUntexturedBox(dst, new Vector3(-0.05f, -0.05f, 0.02f), new Vector3(0.10f, 0.10f, 0.03f), rot, rim);
    }

    private static void AppendHeldTorchOrLantern(List<OverlayVertex> dst, float xSign, int tile, float bobX, float bobY, float swingSin, float swingSinSq, float light)
    {
        var pivot = new Vector3((0.50f * xSign) + bobX - (swingSinSq * 0.20f * xSign), -0.46f - bobY + (swingSin * 0.10f), -0.74f - (swingSin * 0.16f));
        var rot = Matrix4x4.CreateRotationZ(0.16f * xSign) *
                  Matrix4x4.CreateRotationX(-0.22f - (swingSinSq * 0.85f)) *
                  Matrix4x4.CreateRotationY(0.45f * xSign) *
                  Matrix4x4.CreateTranslation(pivot);

        if (tile == ProceduralAtlasGenerator.TileLantern)
        {
            var (su0, sv0, su1, sv1) = TileUvBounds(tile, 0.0f, 2.0f / 16.0f, 6.0f / 16.0f, 9.0f / 16.0f);
            var (cu0, cv0, cu1, cv1) = TileUvBounds(tile, 1.0f / 16.0f, 0.0f, 5.0f / 16.0f, 2.0f / 16.0f);
            EmitTexturedBoxSingleUv(dst, new Vector3(-0.11f, -0.10f, -0.11f), new Vector3(0.22f, 0.26f, 0.22f), rot, su0, sv0, su1, sv1, light);
            EmitTexturedBoxSingleUv(dst, new Vector3(-0.075f, 0.16f, -0.075f), new Vector3(0.15f, 0.075f, 0.15f), rot, cu0, cv0, cu1, cv1, light * 0.92f);
            return;
        }

        var (tu0, tv0, tu1, tv1) = TileUvBounds(tile, 0.4375f, 0.375f, 0.5625f, 1.0f);
        EmitTexturedBoxSingleUv(dst, new Vector3(-0.045f, -0.14f, -0.045f), new Vector3(0.09f, 0.46f, 0.09f), rot, tu0, tv0, tu1, tv1, light);
    }

    private static void AppendTextured3DBlock(
        List<OverlayVertex> dst,
        float xSign,
        int topTile,
        int bottomTile,
        int frontTile,
        int sideTile,
        float bobX,
        float bobY,
        float swingSin,
        float swingSinSq,
        float light)
    {
        var pivot = new Vector3((0.54f * xSign) + bobX - (swingSinSq * 0.22f * xSign), -0.44f - bobY + (swingSin * 0.12f), -0.82f - (swingSin * 0.18f));
        var rot = Matrix4x4.CreateRotationY((0.785f + (swingSin * 0.42f)) * xSign) *
                  Matrix4x4.CreateRotationX(0.22f - (swingSinSq * 0.82f)) *
                  Matrix4x4.CreateRotationZ(-0.08f * swingSin * xSign) *
                  Matrix4x4.CreateTranslation(pivot);

        const float hs = 0.185f;
        var topT = topTile >= 0 ? topTile : sideTile;
        var botT = bottomTile >= 0 ? bottomTile : sideTile;
        var frtT = frontTile >= 0 ? frontTile : sideTile;

        EmitBlockCubeFace(dst, new Vector3(-hs, hs, -hs), new Vector3(-hs, hs, hs), new Vector3(hs, hs, hs), new Vector3(hs, hs, -hs), rot, topT, light * 1.00f);
        EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(hs, -hs, -hs), new Vector3(hs, -hs, hs), new Vector3(-hs, -hs, hs), rot, botT, light * 0.55f);
        EmitBlockCubeFace(dst, new Vector3(-hs, -hs, hs), new Vector3(hs, -hs, hs), new Vector3(hs, hs, hs), new Vector3(-hs, hs, hs), rot, frtT, light * 0.84f);
        EmitBlockCubeFace(dst, new Vector3(hs, -hs, -hs), new Vector3(-hs, -hs, -hs), new Vector3(-hs, hs, -hs), new Vector3(hs, hs, -hs), rot, sideTile, light * 0.80f);
        EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(-hs, -hs, hs), new Vector3(-hs, hs, hs), new Vector3(-hs, hs, -hs), rot, sideTile, light * 0.70f);
        EmitBlockCubeFace(dst, new Vector3(hs, -hs, hs), new Vector3(hs, -hs, -hs), new Vector3(hs, hs, -hs), new Vector3(hs, hs, hs), rot, sideTile, light * 0.68f);
    }

    private static void AppendExtruded16x16Item(
        List<OverlayVertex> dst,
        TextureAtlasData atlas,
        float xSign,
        int tileIndex,
        bool isToolOrWeapon,
        bool isBow,
        float bowCharge,
        float bobX,
        float bobY,
        float swingSin,
        float swingSinSq,
        float light)
    {
        Matrix4x4 rot;
        if (isBow)
        {
            var jitter = bowCharge >= 0.98f ? MathF.Sin(bobX * 140.0f) * 0.005f : 0.0f;
            var pivot = new Vector3(
                (0.46f - (bowCharge * 0.12f)) * xSign + (bobX * (1.0f - (bowCharge * 0.6f))) + jitter,
                -0.36f + (bowCharge * 0.06f) - (bobY * (1.0f - (bowCharge * 0.6f))),
                -0.72f + (bowCharge * 0.11f) - (swingSin * 0.16f));
            rot = Matrix4x4.CreateRotationZ(-0.74f + (bowCharge * 0.08f)) *
                  Matrix4x4.CreateRotationY((-1.32f + (bowCharge * 0.18f)) * xSign) *
                  Matrix4x4.CreateRotationX(0.08f - (swingSinSq * 0.55f)) *
                  Matrix4x4.CreateTranslation(pivot);
        }
        else if (isToolOrWeapon)
        {
            // Verified handheld basis: Z(+65 deg) -> Y(-90 deg) maps grip (-1,-1) to bottom-hand and blade (+1,+1) to upright-forward (-Z, +Y),
            // with axe chopping blade & front pickaxe prong facing forward (-Z) away from the player!
            var pivot = new Vector3((0.50f * xSign) + bobX - (swingSinSq * 0.26f * xSign), -0.38f - bobY + (swingSin * 0.06f), -0.74f - (swingSin * 0.24f));
            var yYaw = xSign > 0.0f ? -1.44f : -1.70f;
            rot = Matrix4x4.CreateRotationZ(1.134f) *
                  Matrix4x4.CreateRotationY(yYaw + (swingSin * 0.28f * xSign)) *
                  Matrix4x4.CreateRotationX(-swingSinSq * 1.05f) *
                  Matrix4x4.CreateTranslation(pivot);
        }
        else
        {
            var pivot = new Vector3((0.48f * xSign) + bobX - (swingSinSq * 0.18f * xSign), -0.40f - bobY + (swingSin * 0.10f), -0.74f - (swingSin * 0.16f));
            rot = Matrix4x4.CreateRotationY((-0.42f * xSign) + (swingSin * 0.30f * xSign)) *
                  Matrix4x4.CreateRotationX(0.10f - (swingSinSq * 0.75f)) *
                  Matrix4x4.CreateTranslation(pivot);
        }

        var scale = (isToolOrWeapon || isBow) ? 0.56f : 0.40f;
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
