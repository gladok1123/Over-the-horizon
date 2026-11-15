using System.Numerics;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Builds authentic Minecraft first-person 3D held viewmodels for both Right Hand (Main Hand) and Left Hand (Offhand):
/// 6-face textured 3D blocks, 3D torches/lanterns, 3D shields, <c>16x16</c> pixel-extruded 3D tools/swords/items
/// (handle in hand, blade upright-forward!), and Steve right-arm empty hand.
/// </summary>
public static partial class HeldItem3DMesher
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

        var os = Math.Clamp(state.OffhandSwingProgress, 0.0f, 1.0f);
        var offSwingSin = MathF.Sin(os * MathF.PI);
        var offSwingSinSq = MathF.Sin(MathF.Sqrt(os) * MathF.PI);

        var shieldRaise = state.IsBlockingShield ? Math.Clamp(state.ShieldRaiseNormalized, 0.25f, 1.0f) : Math.Clamp(state.ShieldRaiseNormalized, 0.0f, 1.0f);
        var mainShieldRaise = state.HeldIsShield ? shieldRaise : 0.0f;
        var offShieldRaise = (!state.HeldIsShield && state.OffhandIsShield) ? shieldRaise : 0.0f;
        var light = Math.Clamp(state.HeldLightBrightness, 0.055f, 1.0f);

        // 1. Right Hand (Main Hand) — renders item or empty Steve right arm (unless Spectator)
        if (state.IsSpectatorMode)
        {
            return;
        }

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
            shieldRaise: mainShieldRaise,
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

        // 2. Left Hand (Offhand) — renders in bottom-left and swings independently via OffhandSwingProgress!
        if (!state.OffhandIsEmpty)
        {
            AppendSingleHand(
                dst,
                atlas,
                isOffhand: true,
                isEmpty: false,
                isShield: state.OffhandIsShield,
                shieldRaise: offShieldRaise,
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
                swingSin: offSwingSin,
                swingSinSq: offSwingSinSq,
                light: light);
        }
    }

    private static void AppendSingleHand(
        List<OverlayVertex> dst,
        TextureAtlasData? atlas,
        bool isOffhand,
        bool isEmpty,
        bool isShield,
        float shieldRaise,
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
            AppendHeldShield(dst, xSign, shieldRaise, bobX, bobY, swingSin, swingSinSq, light);
            return;
        }

        if (isTorchStyle && spriteTile >= 0)
        {
            AppendHeldTorchOrLantern(dst, xSign, spriteTile, bobX, bobY, swingSin, swingSinSq, MathF.Max(light, 0.94f));
            return;
        }

        if (spriteTile == ProceduralItemTileManifest.TileOakFenceItem)
        {
            AppendHeldFenceModel(dst, xSign, bobX, bobY, swingSin, swingSinSq, light);
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

    private static void AppendHeldShield(List<OverlayVertex> dst, float xSign, float raise, float bobX, float bobY, float swingSin, float swingSinSq, float light)
    {
        var bobScale = 1.0f - (raise * 0.72f);
        var pivot = new Vector3(
            ((0.54f - (raise * 0.30f)) * xSign) + (bobX * bobScale) - (swingSinSq * 0.14f * xSign),
            -0.46f + (raise * 0.20f) - (bobY * bobScale) + (swingSin * 0.06f),
            -0.78f + (raise * 0.12f) - (swingSin * 0.10f));
        var rot = Matrix4x4.CreateRotationY((-0.42f + (raise * 0.36f)) * xSign) *
                  Matrix4x4.CreateRotationX(0.08f - (raise * 0.06f) - (swingSinSq * 0.35f)) *
                  Matrix4x4.CreateTranslation(pivot);

        var tint = new Vector4(light, light, light, 1.0f);
        EntityBoxUvBuilder.EmitEntityBox(dst, EntityTextureAtlasGenerator.CellShield, 0, 0, 12, 22, 1, new Vector3(-0.20f, -0.35f, -0.025f), new Vector3(0.40f, 0.70f, 0.05f), rot, tint);
        EntityBoxUvBuilder.EmitEntityBox(dst, EntityTextureAtlasGenerator.CellShield, 26, 0, 2, 6, 6, new Vector3(-0.04f, -0.10f, 0.025f), new Vector3(0.08f, 0.20f, 0.12f), rot, tint * new Vector4(0.86f, 0.86f, 0.86f, 1.0f));
    }

    private static void AppendHeldTorchOrLantern(List<OverlayVertex> dst, float xSign, int tile, float bobX, float bobY, float swingSin, float swingSinSq, float light)
    {
        var pivot = new Vector3((0.50f * xSign) + bobX - (swingSinSq * 0.20f * xSign), -0.46f - bobY + (swingSin * 0.10f), -0.74f - (swingSin * 0.16f));
        var rot = Matrix4x4.CreateRotationZ(0.16f * xSign) *
                  Matrix4x4.CreateRotationX(-0.22f - (swingSinSq * 0.85f)) *
                  Matrix4x4.CreateRotationY(0.45f * xSign) *
                  Matrix4x4.CreateTranslation(pivot);

        if (tile is ProceduralAtlasGenerator.TileLantern or 197)
        {
            const int lanternTile = ProceduralAtlasGenerator.TileLantern;
            var (su0, sv0, su1, sv1) = TileUvBounds(lanternTile, 0.0f, 2.0f / 16.0f, 6.0f / 16.0f, 9.0f / 16.0f);
            var (cu0, cv0, cu1, cv1) = TileUvBounds(lanternTile, 1.0f / 16.0f, 0.0f, 5.0f / 16.0f, 2.0f / 16.0f);
            var (capU0, capV0, capU1, capV1) = TileUvBounds(lanternTile, 0.0f, 9.0f / 16.0f, 6.0f / 16.0f, 15.0f / 16.0f);
            var (topU0, topV0, topU1, topV1) = TileUvBounds(lanternTile, 1.0f / 16.0f, 10.0f / 16.0f, 5.0f / 16.0f, 14.0f / 16.0f);
            var (ru0, rv0, ru1, rv1) = TileUvBounds(lanternTile, 11.0f / 16.0f, 1.0f / 16.0f, 14.0f / 16.0f, 4.0f / 16.0f);

            EmitLanternBoxWithCapUv(dst, new Vector3(-0.115f, -0.11f, -0.115f), new Vector3(0.23f, 0.27f, 0.23f), rot, su0, sv0, su1, sv1, capU0, capV0, capU1, capV1, light);
            EmitLanternBoxWithCapUv(dst, new Vector3(-0.078f, 0.16f, -0.078f), new Vector3(0.156f, 0.076f, 0.156f), rot, cu0, cv0, cu1, cv1, topU0, topV0, topU1, topV1, light * 0.94f);

            // Crossed iron loop at top of held lantern
            var loopCol = new Vector4(light * 0.88f, light * 0.88f, light * 0.90f, 1.0f);
            EmitDoubleSidedUvQuad(dst, rot, new Vector3(-0.045f, 0.236f, -0.045f), new Vector3(0.045f, 0.236f, 0.045f), new Vector3(0.045f, 0.305f, 0.045f), new Vector3(-0.045f, 0.305f, -0.045f), ru0, rv0, ru1, rv1, loopCol);
            EmitDoubleSidedUvQuad(dst, rot, new Vector3(0.045f, 0.236f, -0.045f), new Vector3(-0.045f, 0.236f, 0.045f), new Vector3(-0.045f, 0.305f, 0.045f), new Vector3(0.045f, 0.305f, -0.045f), ru0, rv0, ru1, rv1, loopCol);
            return;
        }

        var (tu0, tv0, tu1, tv1) = TileUvBounds(tile, 0.4375f, 0.375f, 0.5625f, 1.0f);
        EmitTexturedBoxSingleUv(dst, new Vector3(-0.045f, -0.14f, -0.045f), new Vector3(0.09f, 0.46f, 0.09f), rot, tu0, tv0, tu1, tv1, light);
    }

    private static void AppendHeldFenceModel(List<OverlayVertex> dst, float xSign, float bobX, float bobY, float swingSin, float swingSinSq, float light)
    {
        var pivot = new Vector3((0.53f * xSign) + bobX - (swingSinSq * 0.22f * xSign), -0.44f - bobY + (swingSin * 0.12f), -0.82f - (swingSin * 0.18f));
        var rot = Matrix4x4.CreateRotationY((0.72f + (swingSin * 0.40f)) * xSign) *
                  Matrix4x4.CreateRotationX(0.20f - (swingSinSq * 0.80f)) *
                  Matrix4x4.CreateTranslation(pivot);
        EmitMiniFenceMesh(dst, rot, 0.38f, light);
    }

    internal static void EmitMiniFenceMesh(List<OverlayVertex> dst, Matrix4x4 m, float scale, float light)
    {
        const int planks = ProceduralAtlasGenerator.TileOakPlanks;
        var postW = 0.22f * scale;
        var postH = 0.96f * scale;
        var postX = 0.34f * scale;
        var railW = 0.72f * scale;
        var railH = 0.17f * scale;
        var railD = 0.13f * scale;

        EmitBox6Face(dst, new Vector3(-postX - (postW * 0.5f), -postH * 0.5f, -postW * 0.5f), new Vector3(postW, postH, postW), m, planks, light);
        EmitBox6Face(dst, new Vector3(postX - (postW * 0.5f), -postH * 0.5f, -postW * 0.5f), new Vector3(postW, postH, postW), m, planks, light);
        EmitBox6Face(dst, new Vector3(-railW * 0.5f, 0.16f * scale, -railD * 0.5f), new Vector3(railW, railH, railD), m, planks, light * 0.95f);
        EmitBox6Face(dst, new Vector3(-railW * 0.5f, -0.22f * scale, -railD * 0.5f), new Vector3(railW, railH, railD), m, planks, light * 0.92f);
    }

    private static void EmitBox6Face(List<OverlayVertex> dst, Vector3 min, Vector3 size, Matrix4x4 m, int tile, float light)
    {
        var max = min + size;
        EmitBlockCubeFace(dst, new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), m, tile, light);
        EmitBlockCubeFace(dst, new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), m, tile, light * 0.58f);
        EmitBlockCubeFace(dst, new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), m, tile, light * 0.86f);
        EmitBlockCubeFace(dst, new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), m, tile, light * 0.82f);
        EmitBlockCubeFace(dst, new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z), m, tile, light * 0.72f);
        EmitBlockCubeFace(dst, new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), m, tile, light * 0.70f);
    }

    private static void EmitLanternBoxWithCapUv(List<OverlayVertex> dst, Vector3 min, Vector3 size, Matrix4x4 m, float su0, float sv0, float su1, float sv1, float cu0, float cv0, float cu1, float cv1, float light)
    {
        var max = min + size;
        var sideCol = new Vector4(light * 0.96f, light * 0.96f, light * 0.96f, 1.0f);
        var topCol = new Vector4(light, light * 0.98f, light * 0.94f, 1.0f);
        var botCol = new Vector4(light * 0.78f, light * 0.78f, light * 0.78f, 1.0f);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(min.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, max.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, max.Z), m), new Vector2(su0, sv1), new Vector2(su1, sv1), new Vector2(su1, sv0), new Vector2(su0, sv0), sideCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(max.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(min.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, min.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, min.Z), m), new Vector2(su0, sv1), new Vector2(su1, sv1), new Vector2(su1, sv0), new Vector2(su0, sv0), sideCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(min.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(min.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, max.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, min.Z), m), new Vector2(su0, sv1), new Vector2(su1, sv1), new Vector2(su1, sv0), new Vector2(su0, sv0), sideCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(max.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, min.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, max.Z), m), new Vector2(su0, sv1), new Vector2(su1, sv1), new Vector2(su1, sv0), new Vector2(su0, sv0), sideCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(min.X, max.Y, min.Z), m), Vector3.Transform(new Vector3(min.X, max.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, max.Z), m), Vector3.Transform(new Vector3(max.X, max.Y, min.Z), m), new Vector2(cu0, cv0), new Vector2(cu0, cv1), new Vector2(cu1, cv1), new Vector2(cu1, cv0), topCol);
        EmitTexturedQuad(dst, Vector3.Transform(new Vector3(min.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(max.X, min.Y, min.Z), m), Vector3.Transform(new Vector3(max.X, min.Y, max.Z), m), Vector3.Transform(new Vector3(min.X, min.Y, max.Z), m), new Vector2(cu0, cv1), new Vector2(cu1, cv1), new Vector2(cu1, cv0), new Vector2(cu0, cv0), botCol);
    }

    private static void EmitDoubleSidedUvQuad(List<OverlayVertex> dst, Matrix4x4 m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u0, float v0, float u1, float v1, Vector4 col)
    {
        var q0 = Vector3.Transform(a, m);
        var q1 = Vector3.Transform(b, m);
        var q2 = Vector3.Transform(c, m);
        var q3 = Vector3.Transform(d, m);
        EmitTexturedQuad(dst, q0, q1, q2, q3, new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), col);
        EmitTexturedQuad(dst, q1, q0, q3, q2, new Vector2(u1, v1), new Vector2(u0, v1), new Vector2(u0, v0), new Vector2(u1, v0), col);
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
            // RotationZ(+45 deg) * RotationY(-85 deg) maps top-left arrow tip (-1,+1) to forward (-Z),
            // bottom-right string (+1,-1) to back (+Z), and bow limbs vertically (+Y / -Y)!
            var jitter = bowCharge >= 0.98f ? MathF.Sin(bobX * 140.0f) * 0.004f : 0.0f;
            var pivot = new Vector3(
                (0.46f - (bowCharge * 0.13f)) * xSign + (bobX * (1.0f - (bowCharge * 0.65f))) + jitter,
                -0.34f + (bowCharge * 0.06f) - (bobY * (1.0f - (bowCharge * 0.65f))),
                -0.72f + (bowCharge * 0.10f) - (swingSin * 0.16f));
            rot = Matrix4x4.CreateRotationZ(0.7854f) *
                  Matrix4x4.CreateRotationY((-1.48f + (bowCharge * 0.14f)) * xSign) *
                  Matrix4x4.CreateRotationX(0.04f - (swingSinSq * 0.50f)) *
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
        EmitExtruded16x16Mesh(dst, atlas, tileIndex, scale, rot, light);
    }
}
