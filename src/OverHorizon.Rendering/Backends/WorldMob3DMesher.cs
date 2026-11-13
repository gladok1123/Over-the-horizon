using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Builds articulated 3D Minecraft models mapped to real <c>23w13a__or__b</c> entity skins
/// (<c>steve.png</c>, <c>pig.png</c>, <c>cow.png</c>, <c>sheep.png</c> + <c>sheep_fur.png</c>, <c>chicken.png</c>,
/// <c>zombie.png</c>, <c>skeleton.png</c>, <c>spider.png</c>, <c>creeper.png</c>), burning mob flames, and physics <c>PrimedTnt</c>.
/// </summary>
public static class WorldMob3DMesher
{
    /// <summary>Appends 3D textured articulated geometry for all visible peaceful and hostile mobs.</summary>
    public static void AppendWorldMobs(
        List<OverlayVertex> dst,
        Vec3d cameraPos,
        IReadOnlyList<WorldMobVisual>? mobs)
    {
        if (mobs is null || mobs.Count == 0) return;

        for (var i = 0; i < mobs.Count; i++)
        {
            var m = mobs[i];
            var relFeet = new Vector3(
                (float)(m.FeetPosition.X - cameraPos.X),
                (float)(m.FeetPosition.Y - cameraPos.Y),
                (float)(m.FeetPosition.Z - cameraPos.Z));

            if (relFeet.LengthSquared() > 96.0f * 96.0f) continue;

            var swell = m.CreeperFuse > 0.01f ? 1.0f + (m.CreeperFuse * 0.16f) : 1.0f;
            var root = Matrix4x4.CreateScale(swell) *
                       Matrix4x4.CreateRotationY(m.YawDegrees * (MathF.PI / 180.0f)) *
                       Matrix4x4.CreateTranslation(relFeet);

            var swing = MathF.Sin(m.WalkPhase) * 0.55f;
            var light = m.IsOnFire ? 1.0f : Math.Clamp(m.LightBrightness, 0.06f, 1.0f);
            var tint = ComputeTint(light, m.HurtFlash, m.CreeperFuse, m.IsOnFire);

            switch (m.SpeciesIndex)
            {
                case 0:
                    AppendPig(dst, root, swing, tint);
                    break;
                case 1:
                    AppendCow(dst, root, swing, tint);
                    break;
                case 2:
                    AppendSheep(dst, root, swing, tint, m.IsSheared);
                    break;
                case 3:
                    AppendChicken(dst, root, swing, tint);
                    break;
                case 4:
                    AppendZombie(dst, root, swing, tint);
                    break;
                case 5:
                    AppendSkeleton(dst, root, swing, tint);
                    break;
                case 6:
                    AppendSpider(dst, root, swing, tint);
                    break;
                default:
                    AppendCreeper(dst, root, swing, tint);
                    break;
            }

            if (m.IsOnFire)
            {
                AppendMobBurningFlames(dst, root);
            }
        }
    }

    /// <summary>Appends a full 3D <c>steve.png</c>-textured player model (used for both Remote Players and the 3D Inventory Preview).</summary>
    public static void AppendSteveModel(
        List<OverlayVertex> dst,
        Matrix4x4 root,
        float headYawRad,
        float headPitchRad,
        float walkSwing,
        Vector4 baseTint)
    {
        const int c = EntityTextureAtlasGenerator.CellSteve;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 16, 48, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), Limb(root, new Vector3(-0.125f, 0.75f, 0.0f), walkSwing), baseTint);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 16, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), Limb(root, new Vector3(0.125f, 0.75f, 0.0f), -walkSwing), baseTint);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 16, 16, 8, 12, 4, new Vector3(-0.25f, 0.75f, -0.125f), new Vector3(0.50f, 0.75f, 0.25f), root, baseTint);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 32, 48, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), Limb(root, new Vector3(-0.375f, 1.50f, 0.0f), -walkSwing), baseTint);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 40, 16, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), Limb(root, new Vector3(0.375f, 1.50f, 0.0f), walkSwing), baseTint);

        var headXform = Matrix4x4.CreateRotationX(headPitchRad) *
                        Matrix4x4.CreateRotationY(headYawRad) *
                        Matrix4x4.CreateTranslation(0.0f, 1.50f, 0.0f) *
                        root;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 8, 8, 8, new Vector3(-0.25f, 0.0f, -0.25f), new Vector3(0.50f, 0.50f, 0.50f), headXform, baseTint);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 32, 0, 8, 8, 8, new Vector3(-0.268f, -0.018f, -0.268f), new Vector3(0.536f, 0.536f, 0.536f), headXform, baseTint);
    }

    /// <summary>Appends live physics-simulated 3D <c>PrimedTnt</c> cubes with bright white fuse flashing.</summary>
    public static void AppendPrimedTnts(
        List<OverlayVertex> dst,
        Vec3d cameraPos,
        IReadOnlyList<PrimedTntVisual>? tnts)
    {
        if (tnts is null || tnts.Count == 0) return;

        for (var i = 0; i < tnts.Count; i++)
        {
            var t = tnts[i];
            var rel = new Vector3(
                (float)(t.FeetPosition.X - cameraPos.X),
                (float)(t.FeetPosition.Y - cameraPos.Y) + 0.49f,
                (float)(t.FeetPosition.Z - cameraPos.Z));

            var scale = t.IsFlashingWhite ? 0.52f : 0.49f;
            var m = Matrix4x4.CreateTranslation(rel);
            if (t.IsFlashingWhite)
            {
                var white = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                EmitSolidCube(dst, scale, m, white);
            }
            else
            {
                var l = Math.Clamp(t.LightBrightness, 0.25f, 1.0f);
                HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-scale, scale, -scale), new Vector3(-scale, scale, scale), new Vector3(scale, scale, scale), new Vector3(scale, scale, -scale), m, ProceduralAtlasGenerator.TileTntTop, l);
                HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-scale, -scale, -scale), new Vector3(scale, -scale, -scale), new Vector3(scale, -scale, scale), new Vector3(-scale, -scale, scale), m, ProceduralAtlasGenerator.TileTntBottom, l * 0.6f);
                HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-scale, -scale, scale), new Vector3(scale, -scale, scale), new Vector3(scale, scale, scale), new Vector3(-scale, scale, scale), m, ProceduralAtlasGenerator.TileTntSide, l * 0.88f);
                HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(scale, -scale, -scale), new Vector3(-scale, -scale, -scale), new Vector3(-scale, scale, -scale), new Vector3(scale, scale, -scale), m, ProceduralAtlasGenerator.TileTntSide, l * 0.88f);
                HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-scale, -scale, -scale), new Vector3(-scale, -scale, scale), new Vector3(-scale, scale, scale), new Vector3(-scale, scale, -scale), m, ProceduralAtlasGenerator.TileTntSide, l * 0.76f);
                HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(scale, -scale, scale), new Vector3(scale, -scale, -scale), new Vector3(scale, scale, -scale), new Vector3(scale, scale, scale), m, ProceduralAtlasGenerator.TileTntSide, l * 0.76f);
            }
        }
    }

    private static void AppendPig(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 t)
    {
        const int c = EntityTextureAtlasGenerator.CellPig;
        AppendQuadLegs(dst, c, root, swing, 0.25f, 0.375f, 0.19f, 0.34f, 4, 6, 4, t);
        var bodyRot = Matrix4x4.CreateRotationX(MathF.PI * 0.5f) * Matrix4x4.CreateTranslation(0.0f, 0.62f, 0.0f) * root;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 28, 8, 10, 16, 8, new Vector3(-0.3125f, -0.50f, -0.25f), new Vector3(0.625f, 1.0f, 0.50f), bodyRot, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 8, 8, 8, new Vector3(-0.25f, 0.50f, 0.375f), new Vector3(0.50f, 0.50f, 0.50f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 16, 16, 4, 3, 1, new Vector3(-0.125f, 0.56f, 0.875f), new Vector3(0.25f, 0.1875f, 0.0625f), root, t);
    }

    private static void AppendCow(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 t)
    {
        const int c = EntityTextureAtlasGenerator.CellCow;
        AppendQuadLegs(dst, c, root, swing, 0.25f, 0.75f, 0.24f, 0.42f, 4, 12, 4, t);
        var bodyRot = Matrix4x4.CreateRotationX(MathF.PI * 0.5f) * Matrix4x4.CreateTranslation(0.0f, 1.05f, 0.0f) * root;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 18, 4, 12, 18, 10, new Vector3(-0.375f, -0.5625f, -0.3125f), new Vector3(0.75f, 1.125f, 0.625f), bodyRot, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 8, 8, 6, new Vector3(-0.25f, 1.00f, 0.44f), new Vector3(0.50f, 0.50f, 0.375f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 22, 0, 1, 3, 1, new Vector3(-0.3125f, 1.38f, 0.56f), new Vector3(0.0625f, 0.1875f, 0.0625f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 22, 0, 1, 3, 1, new Vector3(0.25f, 1.38f, 0.56f), new Vector3(0.0625f, 0.1875f, 0.0625f), root, t);
    }

    private static void AppendSheep(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 t, bool sheared)
    {
        const int skin = EntityTextureAtlasGenerator.CellSheep;
        const int fur = EntityTextureAtlasGenerator.CellSheepFur;
        AppendQuadLegs(dst, skin, root, swing, 0.25f, 0.75f, 0.20f, 0.36f, 4, 12, 4, t);
        var bodyRot = Matrix4x4.CreateRotationX(MathF.PI * 0.5f) * Matrix4x4.CreateTranslation(0.0f, 0.94f, 0.0f) * root;
        EntityBoxUvBuilder.EmitEntityBox(dst, skin, 28, 8, 8, 16, 6, new Vector3(-0.25f, -0.50f, -0.1875f), new Vector3(0.50f, 1.0f, 0.375f), bodyRot, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, skin, 0, 0, 6, 6, 8, new Vector3(-0.1875f, 0.94f, 0.38f), new Vector3(0.375f, 0.375f, 0.50f), root, t);

        if (!sheared)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, fur, 28, 8, 8, 16, 6, new Vector3(-0.34f, -0.56f, -0.28f), new Vector3(0.68f, 1.12f, 0.56f), bodyRot, t);
            EntityBoxUvBuilder.EmitEntityBox(dst, fur, 0, 0, 6, 6, 6, new Vector3(-0.22f, 0.92f, 0.36f), new Vector3(0.44f, 0.42f, 0.40f), root, t);
        }
    }

    private static void AppendChicken(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 t)
    {
        const int c = EntityTextureAtlasGenerator.CellChicken;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 26, 0, 3, 5, 3, new Vector3(-0.06f, -0.31f, -0.06f), new Vector3(0.12f, 0.31f, 0.12f), Limb(root, new Vector3(-0.09f, 0.31f, 0.0f), swing), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 26, 0, 3, 5, 3, new Vector3(-0.06f, -0.31f, -0.06f), new Vector3(0.12f, 0.31f, 0.12f), Limb(root, new Vector3(0.09f, 0.31f, 0.0f), -swing), t);
        var bodyRot = Matrix4x4.CreateRotationX(MathF.PI * 0.5f) * Matrix4x4.CreateTranslation(0.0f, 0.46f, 0.0f) * root;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 9, 6, 8, 6, new Vector3(-0.1875f, -0.25f, -0.1875f), new Vector3(0.375f, 0.50f, 0.375f), bodyRot, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 24, 13, 1, 4, 6, new Vector3(-0.24f, 0.34f, -0.18f), new Vector3(0.06f, 0.25f, 0.36f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 24, 13, 1, 4, 6, new Vector3(0.18f, 0.34f, -0.18f), new Vector3(0.06f, 0.25f, 0.36f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 4, 6, 3, new Vector3(-0.125f, 0.52f, 0.16f), new Vector3(0.25f, 0.375f, 0.1875f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 14, 0, 4, 2, 2, new Vector3(-0.125f, 0.64f, 0.34f), new Vector3(0.25f, 0.125f, 0.125f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 14, 4, 2, 2, 2, new Vector3(-0.0625f, 0.52f, 0.32f), new Vector3(0.125f, 0.125f, 0.10f), root, t);
    }

    private static void AppendZombie(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 t)
    {
        const int c = EntityTextureAtlasGenerator.CellZombie;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 16, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), Limb(root, new Vector3(-0.125f, 0.75f, 0.0f), swing), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 16, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), Limb(root, new Vector3(0.125f, 0.75f, 0.0f), -swing), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 16, 16, 8, 12, 4, new Vector3(-0.25f, 0.75f, -0.125f), new Vector3(0.50f, 0.75f, 0.25f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 8, 8, 8, new Vector3(-0.25f, 1.50f, -0.25f), new Vector3(0.50f, 0.50f, 0.50f), root, t);
        var armL = Matrix4x4.CreateRotationX(-1.42f + (swing * 0.12f)) * Matrix4x4.CreateTranslation(-0.375f, 1.42f, 0.0f) * root;
        var armR = Matrix4x4.CreateRotationX(-1.42f - (swing * 0.12f)) * Matrix4x4.CreateTranslation(0.375f, 1.42f, 0.0f) * root;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 40, 16, 4, 12, 4, new Vector3(-0.125f, -0.72f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), armL, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 40, 16, 4, 12, 4, new Vector3(-0.125f, -0.72f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), armR, t);
    }

    private static void AppendSkeleton(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 t)
    {
        const int c = EntityTextureAtlasGenerator.CellSkeleton;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 16, 2, 12, 2, new Vector3(-0.0625f, -0.75f, -0.0625f), new Vector3(0.125f, 0.75f, 0.125f), Limb(root, new Vector3(-0.125f, 0.75f, 0.0f), swing), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 16, 2, 12, 2, new Vector3(-0.0625f, -0.75f, -0.0625f), new Vector3(0.125f, 0.75f, 0.125f), Limb(root, new Vector3(0.125f, 0.75f, 0.0f), -swing), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 16, 16, 8, 12, 4, new Vector3(-0.25f, 0.75f, -0.125f), new Vector3(0.50f, 0.75f, 0.25f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 8, 8, 8, new Vector3(-0.25f, 1.50f, -0.25f), new Vector3(0.50f, 0.50f, 0.50f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 40, 16, 2, 12, 2, new Vector3(-0.0625f, -0.72f, -0.0625f), new Vector3(0.125f, 0.75f, 0.125f), Limb(root, new Vector3(-0.3125f, 1.44f, 0.0f), -swing * 0.7f), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 40, 16, 2, 12, 2, new Vector3(-0.0625f, -0.72f, -0.0625f), new Vector3(0.125f, 0.75f, 0.125f), Matrix4x4.CreateRotationX(-1.15f) * Matrix4x4.CreateTranslation(0.3125f, 1.44f, 0.0f) * root, t);
    }

    private static void AppendSpider(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 t)
    {
        const int c = EntityTextureAtlasGenerator.CellSpider;
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 12, 10, 8, 12, new Vector3(-0.3125f, 0.28f, -0.68f), new Vector3(0.625f, 0.50f, 0.75f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 6, 6, 6, new Vector3(-0.1875f, 0.30f, -0.02f), new Vector3(0.375f, 0.375f, 0.375f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 32, 4, 8, 8, 8, new Vector3(-0.25f, 0.28f, 0.28f), new Vector3(0.50f, 0.50f, 0.50f), root, t);
        for (var leg = 0; leg < 4; leg++)
        {
            var zOff = -0.22f + (leg * 0.18f);
            var phase = (leg & 1) == 0 ? swing * 0.35f : -swing * 0.35f;
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 18, 0, 16, 2, 2, new Vector3(-0.62f, -0.06f, -0.06f), new Vector3(0.62f, 0.12f, 0.12f), Matrix4x4.CreateRotationZ(0.48f + phase) * Matrix4x4.CreateTranslation(-0.20f, 0.40f, zOff) * root, t);
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 18, 0, 16, 2, 2, new Vector3(0.0f, -0.06f, -0.06f), new Vector3(0.62f, 0.12f, 0.12f), Matrix4x4.CreateRotationZ(-0.48f - phase) * Matrix4x4.CreateTranslation(0.20f, 0.40f, zOff) * root, t);
        }
    }

    private static void AppendCreeper(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 t)
    {
        const int c = EntityTextureAtlasGenerator.CellCreeper;
        AppendQuadLegs(dst, c, root, swing, 0.25f, 0.375f, 0.16f, 0.24f, 4, 6, 4, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 16, 16, 8, 12, 4, new Vector3(-0.25f, 0.375f, -0.125f), new Vector3(0.50f, 0.75f, 0.25f), root, t);
        EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 8, 8, 8, new Vector3(-0.25f, 1.125f, -0.25f), new Vector3(0.50f, 0.50f, 0.50f), root, t);
    }

    private static void AppendQuadLegs(List<OverlayVertex> dst, int cell, Matrix4x4 root, float swing, float legW, float legH, float xSpread, float zSpread, int dx, int dy, int dz, Vector4 t)
    {
        var hw = legW * 0.5f;
        EntityBoxUvBuilder.EmitEntityBox(dst, cell, 0, 16, dx, dy, dz, new Vector3(-hw, -legH, -hw), new Vector3(legW, legH, legW), Limb(root, new Vector3(-xSpread, legH, zSpread), swing), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, cell, 0, 16, dx, dy, dz, new Vector3(-hw, -legH, -hw), new Vector3(legW, legH, legW), Limb(root, new Vector3(xSpread, legH, zSpread), -swing), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, cell, 0, 16, dx, dy, dz, new Vector3(-hw, -legH, -hw), new Vector3(legW, legH, legW), Limb(root, new Vector3(-xSpread, legH, -zSpread), -swing), t);
        EntityBoxUvBuilder.EmitEntityBox(dst, cell, 0, 16, dx, dy, dz, new Vector3(-hw, -legH, -hw), new Vector3(legW, legH, legW), Limb(root, new Vector3(xSpread, legH, -zSpread), swing), t);
    }

    private static void AppendMobBurningFlames(List<OverlayVertex> dst, Matrix4x4 root)
    {
        var (u0, v0, u1, v1) = HeldItem3DMesher.TileUvBounds(ProceduralAtlasGenerator.TileFire, 0.01f, 0.01f, 0.99f, 0.99f);
        var col = new Vector4(1.0f, 0.92f, 0.72f, 0.95f);
        var a0 = Vector3.Transform(new Vector3(-0.34f, 0.15f, -0.34f), root);
        var a1 = Vector3.Transform(new Vector3(0.34f, 0.15f, 0.34f), root);
        var a2 = Vector3.Transform(new Vector3(0.34f, 1.55f, 0.34f), root);
        var a3 = Vector3.Transform(new Vector3(-0.34f, 1.55f, -0.34f), root);
        HeldItem3DMesher.EmitTexturedQuad(dst, a0, a1, a2, a3, new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), col);
        var b0 = Vector3.Transform(new Vector3(0.34f, 0.15f, -0.34f), root);
        var b1 = Vector3.Transform(new Vector3(-0.34f, 0.15f, 0.34f), root);
        var b2 = Vector3.Transform(new Vector3(-0.34f, 1.55f, 0.34f), root);
        var b3 = Vector3.Transform(new Vector3(0.34f, 1.55f, -0.34f), root);
        HeldItem3DMesher.EmitTexturedQuad(dst, b0, b1, b2, b3, new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), col);
    }

    private static void EmitSolidCube(List<OverlayVertex> dst, float hs, Matrix4x4 m, Vector4 col)
    {
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
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

        Q(new Vector3(-hs, hs, -hs), new Vector3(-hs, hs, hs), new Vector3(hs, hs, hs), new Vector3(hs, hs, -hs));
        Q(new Vector3(-hs, -hs, -hs), new Vector3(hs, -hs, -hs), new Vector3(hs, -hs, hs), new Vector3(-hs, -hs, hs));
        Q(new Vector3(-hs, -hs, hs), new Vector3(hs, -hs, hs), new Vector3(hs, hs, hs), new Vector3(-hs, hs, hs));
        Q(new Vector3(hs, -hs, -hs), new Vector3(-hs, -hs, -hs), new Vector3(-hs, hs, -hs), new Vector3(hs, hs, -hs));
        Q(new Vector3(-hs, -hs, -hs), new Vector3(-hs, -hs, hs), new Vector3(-hs, hs, hs), new Vector3(-hs, hs, -hs));
        Q(new Vector3(hs, -hs, hs), new Vector3(hs, -hs, -hs), new Vector3(hs, hs, -hs), new Vector3(hs, hs, hs));
    }

    private static Matrix4x4 Limb(Matrix4x4 root, Vector3 pivot, float angle) =>
        Matrix4x4.CreateRotationX(angle) * Matrix4x4.CreateTranslation(pivot) * root;

    private static Vector4 ComputeTint(float light, float hurtFlash, float creeperFuse, bool onFire)
    {
        var c = new Vector4(light, light, light, 1.0f);
        if (onFire) c = Vector4.Lerp(c, new Vector4(1.0f, 0.72f, 0.42f, 1.0f), 0.35f);
        if (hurtFlash > 0.01f) c = Vector4.Lerp(c, new Vector4(1.0f, 0.24f, 0.22f, 1.0f), Math.Clamp(hurtFlash, 0.0f, 1.0f) * 0.76f);
        if (creeperFuse > 0.01f && ((int)(creeperFuse * 12.0f) & 1) == 1) c = Vector4.Lerp(c, Vector4.One, 0.82f);
        return c;
    }
}
