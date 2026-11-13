using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Builds articulated 3D Minecraft-style models for Peaceful (<c>Pig</c>, <c>Cow</c>, <c>Sheep</c>, <c>Chicken</c>)
/// and Hostile (<c>Zombie</c>, <c>Skeleton</c>, <c>Spider</c>, <c>Creeper</c>) mobs with walking limb animation,
/// <c>0..15</c> world illumination, red sword-hit flash, and Creeper fuse flash.
/// </summary>
public static class WorldMob3DMesher
{
    /// <summary>Appends 3D articulated geometry for all visible peaceful and hostile mobs into <paramref name="dst"/>.</summary>
    /// <param name="dst">Destination overlay vertex buffer.</param>
    /// <param name="cameraPos">World-space camera eye coordinate.</param>
    /// <param name="mobs">Active world mob visual snapshots.</param>
    public static void AppendWorldMobs(
        List<OverlayVertex> dst,
        Vec3d cameraPos,
        IReadOnlyList<WorldMobVisual>? mobs)
    {
        if (mobs is null || mobs.Count == 0)
        {
            return;
        }

        for (var i = 0; i < mobs.Count; i++)
        {
            var m = mobs[i];
            var relFeet = new Vector3(
                (float)(m.FeetPosition.X - cameraPos.X),
                (float)(m.FeetPosition.Y - cameraPos.Y),
                (float)(m.FeetPosition.Z - cameraPos.Z));

            if (relFeet.LengthSquared() > 96.0f * 96.0f)
            {
                continue;
            }

            var swell = m.CreeperFuse > 0.01f ? 1.0f + (m.CreeperFuse * 0.16f) : 1.0f;
            var root = Matrix4x4.CreateScale(swell) *
                       Matrix4x4.CreateRotationY(m.YawDegrees * (MathF.PI / 180.0f)) *
                       Matrix4x4.CreateTranslation(relFeet);

            var swing = MathF.Sin(m.WalkPhase) * 0.55f;
            var light = Math.Clamp(m.LightBrightness, 0.055f, 1.0f);

            switch (m.SpeciesIndex)
            {
                case 0:
                    AppendPig(dst, root, swing, light, m.HurtFlash);
                    break;
                case 1:
                    AppendCow(dst, root, swing, light, m.HurtFlash);
                    break;
                case 2:
                    AppendSheep(dst, root, swing, light, m.HurtFlash, m.IsSheared);
                    break;
                case 3:
                    AppendChicken(dst, root, swing, light, m.HurtFlash);
                    break;
                case 4:
                    AppendZombie(dst, root, swing, light, m.HurtFlash);
                    break;
                case 5:
                    AppendSkeleton(dst, root, swing, light, m.HurtFlash);
                    break;
                case 6:
                    AppendSpider(dst, root, swing, light, m.HurtFlash);
                    break;
                default:
                    AppendCreeper(dst, root, swing, light, m.HurtFlash, m.CreeperFuse);
                    break;
            }
        }
    }

    private static void AppendPig(List<OverlayVertex> dst, Matrix4x4 root, float swing, float l, float hurt)
    {
        var pink = Tint(new Vector4(0.95f, 0.65f, 0.66f, 1.0f), l, hurt);
        var snout = Tint(new Vector4(0.86f, 0.50f, 0.54f, 1.0f), l, hurt);
        var eye = Tint(new Vector4(0.10f, 0.10f, 0.12f, 1.0f), l, hurt);

        AppendQuadrupedLegs(dst, root, swing, legW: 0.20f, legH: 0.34f, xSpread: 0.19f, zSpread: 0.32f, pink);
        Box(dst, new Vector3(-0.31f, 0.32f, -0.46f), new Vector3(0.62f, 0.48f, 0.92f), root, pink);
        Box(dst, new Vector3(-0.24f, 0.46f, 0.38f), new Vector3(0.48f, 0.44f, 0.44f), root, pink);
        Box(dst, new Vector3(-0.12f, 0.52f, 0.80f), new Vector3(0.24f, 0.16f, 0.10f), root, snout);
        Box(dst, new Vector3(-0.18f, 0.68f, 0.81f), new Vector3(0.07f, 0.07f, 0.03f), root, eye);
        Box(dst, new Vector3(0.11f, 0.68f, 0.81f), new Vector3(0.07f, 0.07f, 0.03f), root, eye);
    }

    private static void AppendCow(List<OverlayVertex> dst, Matrix4x4 root, float swing, float l, float hurt)
    {
        var hide = Tint(new Vector4(0.32f, 0.22f, 0.16f, 1.0f), l, hurt);
        var patch = Tint(new Vector4(0.88f, 0.86f, 0.82f, 1.0f), l, hurt);
        var horn = Tint(new Vector4(0.76f, 0.74f, 0.66f, 1.0f), l, hurt);

        AppendQuadrupedLegs(dst, root, swing, legW: 0.22f, legH: 0.56f, xSpread: 0.22f, zSpread: 0.36f, hide);
        Box(dst, new Vector3(-0.36f, 0.54f, -0.52f), new Vector3(0.72f, 0.62f, 1.04f), root, hide);
        Box(dst, new Vector3(-0.37f, 0.72f, -0.18f), new Vector3(0.74f, 0.32f, 0.38f), root, patch);
        Box(dst, new Vector3(-0.25f, 0.78f, 0.44f), new Vector3(0.50f, 0.46f, 0.42f), root, hide);
        Box(dst, new Vector3(-0.31f, 1.16f, 0.56f), new Vector3(0.07f, 0.16f, 0.07f), root, horn);
        Box(dst, new Vector3(0.24f, 1.16f, 0.56f), new Vector3(0.07f, 0.16f, 0.07f), root, horn);
    }

    private static void AppendSheep(List<OverlayVertex> dst, Matrix4x4 root, float swing, float l, float hurt, bool sheared)
    {
        var wool = Tint(new Vector4(0.94f, 0.94f, 0.95f, 1.0f), l, hurt);
        var skin = Tint(new Vector4(0.86f, 0.72f, 0.66f, 1.0f), l, hurt);

        AppendQuadrupedLegs(dst, root, swing, legW: 0.19f, legH: 0.52f, xSpread: 0.20f, zSpread: 0.32f, skin);
        if (sheared)
        {
            Box(dst, new Vector3(-0.28f, 0.50f, -0.44f), new Vector3(0.56f, 0.48f, 0.88f), root, skin);
        }
        else
        {
            Box(dst, new Vector3(-0.36f, 0.46f, -0.50f), new Vector3(0.72f, 0.60f, 1.00f), root, wool);
            Box(dst, new Vector3(-0.25f, 0.74f, 0.40f), new Vector3(0.50f, 0.44f, 0.34f), root, wool);
        }

        Box(dst, new Vector3(-0.20f, 0.72f, 0.46f), new Vector3(0.40f, 0.38f, 0.38f), root, skin);
    }

    private static void AppendChicken(List<OverlayVertex> dst, Matrix4x4 root, float swing, float l, float hurt)
    {
        var feather = Tint(new Vector4(0.96f, 0.96f, 0.98f, 1.0f), l, hurt);
        var beak = Tint(new Vector4(0.92f, 0.68f, 0.18f, 1.0f), l, hurt);
        var wattle = Tint(new Vector4(0.88f, 0.16f, 0.18f, 1.0f), l, hurt);

        Box(dst, new Vector3(-0.11f, 0.0f, -0.04f), new Vector3(0.06f, 0.24f, 0.08f), Limb(root, new Vector3(-0.08f, 0.24f, 0.0f), swing), beak);
        Box(dst, new Vector3(0.05f, 0.0f, -0.04f), new Vector3(0.06f, 0.24f, 0.08f), Limb(root, new Vector3(0.08f, 0.24f, 0.0f), -swing), beak);
        Box(dst, new Vector3(-0.18f, 0.22f, -0.22f), new Vector3(0.36f, 0.30f, 0.44f), root, feather);
        Box(dst, new Vector3(-0.12f, 0.46f, 0.14f), new Vector3(0.24f, 0.26f, 0.22f), root, feather);
        Box(dst, new Vector3(-0.08f, 0.54f, 0.35f), new Vector3(0.16f, 0.08f, 0.12f), root, beak);
        Box(dst, new Vector3(-0.05f, 0.46f, 0.33f), new Vector3(0.10f, 0.08f, 0.08f), root, wattle);
    }

    private static void AppendZombie(List<OverlayVertex> dst, Matrix4x4 root, float swing, float l, float hurt)
    {
        var greenSkin = Tint(new Vector4(0.34f, 0.56f, 0.30f, 1.0f), l, hurt);
        var shirt = Tint(new Vector4(0.16f, 0.52f, 0.58f, 1.0f), l, hurt);
        var pants = Tint(new Vector4(0.22f, 0.20f, 0.48f, 1.0f), l, hurt);
        var eye = Tint(new Vector4(0.06f, 0.08f, 0.06f, 1.0f), l, hurt);

        AppendBipedLegsAndTorso(dst, root, swing, pants, shirt);
        Box(dst, new Vector3(-0.24f, 1.40f, -0.24f), new Vector3(0.48f, 0.48f, 0.48f), root, greenSkin);
        Box(dst, new Vector3(-0.16f, 1.60f, 0.23f), new Vector3(0.10f, 0.06f, 0.03f), root, eye);
        Box(dst, new Vector3(0.06f, 1.60f, 0.23f), new Vector3(0.10f, 0.06f, 0.03f), root, eye);

        // Outstretched zombie arms!
        var armMatL = Matrix4x4.CreateRotationX(-1.42f + (swing * 0.12f)) * Matrix4x4.CreateTranslation(-0.34f, 1.34f, 0.0f) * root;
        var armMatR = Matrix4x4.CreateRotationX(-1.42f - (swing * 0.12f)) * Matrix4x4.CreateTranslation(0.34f, 1.34f, 0.0f) * root;
        Box(dst, new Vector3(-0.10f, -0.66f, -0.10f), new Vector3(0.20f, 0.68f, 0.20f), armMatL, greenSkin);
        Box(dst, new Vector3(-0.10f, -0.66f, -0.10f), new Vector3(0.20f, 0.68f, 0.20f), armMatR, greenSkin);
    }

    private static void AppendSkeleton(List<OverlayVertex> dst, Matrix4x4 root, float swing, float l, float hurt)
    {
        var bone = Tint(new Vector4(0.84f, 0.83f, 0.79f, 1.0f), l, hurt);
        var dark = Tint(new Vector4(0.14f, 0.14f, 0.16f, 1.0f), l, hurt);

        Box(dst, new Vector3(-0.06f, -0.72f, -0.06f), new Vector3(0.12f, 0.72f, 0.12f), Limb(root, new Vector3(-0.12f, 0.72f, 0.0f), swing), bone);
        Box(dst, new Vector3(-0.06f, -0.72f, -0.06f), new Vector3(0.12f, 0.72f, 0.12f), Limb(root, new Vector3(0.12f, 0.72f, 0.0f), -swing), bone);
        Box(dst, new Vector3(-0.22f, 0.72f, -0.10f), new Vector3(0.44f, 0.68f, 0.20f), root, bone);
        Box(dst, new Vector3(-0.24f, 1.40f, -0.24f), new Vector3(0.48f, 0.48f, 0.48f), root, bone);
        Box(dst, new Vector3(-0.16f, 1.58f, 0.23f), new Vector3(0.11f, 0.09f, 0.03f), root, dark);
        Box(dst, new Vector3(0.05f, 1.58f, 0.23f), new Vector3(0.11f, 0.09f, 0.03f), root, dark);
        Box(dst, new Vector3(-0.12f, 1.46f, 0.23f), new Vector3(0.24f, 0.05f, 0.03f), root, dark);

        var armL = Limb(root, new Vector3(-0.30f, 1.36f, 0.0f), -swing * 0.7f);
        var armR = Matrix4x4.CreateRotationX(-1.15f) * Matrix4x4.CreateTranslation(0.30f, 1.36f, 0.0f) * root;
        Box(dst, new Vector3(-0.06f, -0.68f, -0.06f), new Vector3(0.12f, 0.68f, 0.12f), armL, bone);
        Box(dst, new Vector3(-0.06f, -0.68f, -0.06f), new Vector3(0.12f, 0.68f, 0.12f), armR, bone);
    }

    private static void AppendSpider(List<OverlayVertex> dst, Matrix4x4 root, float swing, float l, float hurt)
    {
        var chitin = Tint(new Vector4(0.18f, 0.16f, 0.19f, 1.0f), l, hurt);
        var redEye = Tint(new Vector4(0.95f, 0.12f, 0.14f, 1.0f), MathF.Max(l, 0.78f), hurt);

        Box(dst, new Vector3(-0.28f, 0.28f, -0.58f), new Vector3(0.56f, 0.38f, 0.56f), root, chitin);
        Box(dst, new Vector3(-0.22f, 0.28f, -0.02f), new Vector3(0.44f, 0.32f, 0.38f), root, chitin);
        Box(dst, new Vector3(-0.20f, 0.30f, 0.32f), new Vector3(0.40f, 0.32f, 0.34f), root, chitin);
        Box(dst, new Vector3(-0.15f, 0.44f, 0.65f), new Vector3(0.08f, 0.08f, 0.03f), root, redEye);
        Box(dst, new Vector3(0.07f, 0.44f, 0.65f), new Vector3(0.08f, 0.08f, 0.03f), root, redEye);

        for (var leg = 0; leg < 4; leg++)
        {
            var zOff = -0.24f + (leg * 0.18f);
            var phase = (leg & 1) == 0 ? swing * 0.35f : -swing * 0.35f;
            var leftLeg = Matrix4x4.CreateRotationZ(0.52f + phase) * Matrix4x4.CreateTranslation(-0.22f, 0.36f, zOff) * root;
            var rightLeg = Matrix4x4.CreateRotationZ(-0.52f - phase) * Matrix4x4.CreateTranslation(0.22f, 0.36f, zOff) * root;
            Box(dst, new Vector3(-0.52f, -0.05f, -0.05f), new Vector3(0.52f, 0.10f, 0.10f), leftLeg, chitin);
            Box(dst, new Vector3(0.0f, -0.05f, -0.05f), new Vector3(0.52f, 0.10f, 0.10f), rightLeg, chitin);
        }
    }

    private static void AppendCreeper(List<OverlayVertex> dst, Matrix4x4 root, float swing, float l, float hurt, float fuse)
    {
        var green = Tint(new Vector4(0.28f, 0.72f, 0.26f, 1.0f), l, hurt);
        var dark = Tint(new Vector4(0.08f, 0.12f, 0.08f, 1.0f), l, hurt);
        if (fuse > 0.05f && ((int)(fuse * 14.0f) & 1) == 0)
        {
            green = Vector4.Lerp(green, Vector4.One, 0.75f);
        }

        AppendQuadrupedLegs(dst, root, swing, legW: 0.22f, legH: 0.38f, xSpread: 0.16f, zSpread: 0.22f, green);
        Box(dst, new Vector3(-0.22f, 0.38f, -0.14f), new Vector3(0.44f, 0.76f, 0.28f), root, green);
        Box(dst, new Vector3(-0.24f, 1.14f, -0.24f), new Vector3(0.48f, 0.48f, 0.48f), root, green);

        // Iconic Creeper face!
        Box(dst, new Vector3(-0.16f, 1.38f, 0.23f), new Vector3(0.10f, 0.10f, 0.03f), root, dark);
        Box(dst, new Vector3(0.06f, 1.38f, 0.23f), new Vector3(0.10f, 0.10f, 0.03f), root, dark);
        Box(dst, new Vector3(-0.08f, 1.22f, 0.23f), new Vector3(0.16f, 0.12f, 0.03f), root, dark);
    }

    private static void AppendQuadrupedLegs(List<OverlayVertex> dst, Matrix4x4 root, float swing, float legW, float legH, float xSpread, float zSpread, Vector4 col)
    {
        var hw = legW * 0.5f;
        Box(dst, new Vector3(-hw, -legH, -hw), new Vector3(legW, legH, legW), Limb(root, new Vector3(-xSpread, legH, zSpread), swing), col);
        Box(dst, new Vector3(-hw, -legH, -hw), new Vector3(legW, legH, legW), Limb(root, new Vector3(xSpread, legH, zSpread), -swing), col);
        Box(dst, new Vector3(-hw, -legH, -hw), new Vector3(legW, legH, legW), Limb(root, new Vector3(-xSpread, legH, -zSpread), -swing), col);
        Box(dst, new Vector3(-hw, -legH, -hw), new Vector3(legW, legH, legW), Limb(root, new Vector3(xSpread, legH, -zSpread), swing), col);
    }

    private static void AppendBipedLegsAndTorso(List<OverlayVertex> dst, Matrix4x4 root, float swing, Vector4 pants, Vector4 shirt)
    {
        Box(dst, new Vector3(-0.11f, -0.72f, -0.11f), new Vector3(0.22f, 0.72f, 0.22f), Limb(root, new Vector3(-0.12f, 0.72f, 0.0f), swing), pants);
        Box(dst, new Vector3(-0.11f, -0.72f, -0.11f), new Vector3(0.22f, 0.72f, 0.22f), Limb(root, new Vector3(0.12f, 0.72f, 0.0f), -swing), pants);
        Box(dst, new Vector3(-0.24f, 0.72f, -0.13f), new Vector3(0.48f, 0.68f, 0.26f), root, shirt);
    }

    private static Matrix4x4 Limb(Matrix4x4 root, Vector3 pivot, float pitchRad) =>
        Matrix4x4.CreateRotationX(pitchRad) * Matrix4x4.CreateTranslation(pivot) * root;

    private static Vector4 Tint(Vector4 baseCol, float light, float hurt)
    {
        var lit = new Vector4(baseCol.X * light, baseCol.Y * light, baseCol.Z * light, 1.0f);
        return hurt > 0.01f ? Vector4.Lerp(lit, new Vector4(0.95f, 0.16f, 0.16f, 1.0f), Math.Clamp(hurt, 0.0f, 1.0f) * 0.72f) : lit;
    }

    private static void Box(List<OverlayVertex> dst, Vector3 min, Vector3 size, Matrix4x4 m, Vector4 col)
    {
        var max = min + size;
        Quad(dst, m, new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), col);
        Quad(dst, m, new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), col * new Vector4(0.55f, 0.55f, 0.55f, 1.0f));
        Quad(dst, m, new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), col * new Vector4(0.86f, 0.86f, 0.86f, 1.0f));
        Quad(dst, m, new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), col * new Vector4(0.80f, 0.80f, 0.80f, 1.0f));
        Quad(dst, m, new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z), col * new Vector4(0.70f, 0.70f, 0.70f, 1.0f));
        Quad(dst, m, new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), col * new Vector4(0.70f, 0.70f, 0.70f, 1.0f));
    }

    private static void Quad(List<OverlayVertex> dst, Matrix4x4 m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 col)
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
}
