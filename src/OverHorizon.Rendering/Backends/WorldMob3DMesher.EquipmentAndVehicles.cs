using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Renders 3D worn player Armor layers (<c>models/armor/*_layer_{1,2}.png</c>), 3D Iron Minecart vehicles (<c>entity/minecart.png</c>),
/// 3D ballistic Arrows (<c>entity/projectiles/arrow.png</c>), and Skeleton held bows.
/// </summary>
public static partial class WorldMob3DMesher
{
    /// <summary>Appends 3D Iron Minecart vehicles in world space relative to <paramref name="cameraPos"/>.</summary>
    public static void AppendMinecarts(
        List<OverlayVertex> dst,
        Vec3d cameraPos,
        IReadOnlyList<MinecartVisual>? carts)
    {
        if (carts is null || carts.Count == 0) return;
        const int c = EntityTextureAtlasGenerator.CellMinecart;

        for (var i = 0; i < carts.Count; i++)
        {
            var cart = carts[i];
            var rel = new Vector3(
                (float)(cart.Position.X - cameraPos.X),
                (float)(cart.Position.Y - cameraPos.Y),
                (float)(cart.Position.Z - cameraPos.Z));
            if (rel.LengthSquared() > 110.0f * 110.0f) continue;

            var wobbleZ = cart.HurtWobble > 0.01f ? MathF.Sin(cart.HurtWobble * 22.0f) * 0.12f * cart.HurtWobble : 0.0f;
            var root = Matrix4x4.CreateRotationZ(wobbleZ) *
                       Matrix4x4.CreateRotationX(cart.SlopePitchDegrees * (MathF.PI / 180.0f)) *
                       Matrix4x4.CreateRotationY(cart.YawDegrees * (MathF.PI / 180.0f)) *
                       Matrix4x4.CreateTranslation(rel);

            var l = Math.Clamp(cart.LightBrightness, 0.10f, 1.0f);
            var tint = new Vector4(l, l, l, 1.0f);

            // Open-topped iron tub: bottom floor + 4 thick metallic walls mapped to entity/minecart.png!
            var floorRot = Matrix4x4.CreateRotationX(MathF.PI * 0.5f) * Matrix4x4.CreateTranslation(0.0f, 0.09f, 0.0f) * root;
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 10, 20, 16, 2, new Vector3(-0.48f, -0.60f, -0.06f), new Vector3(0.96f, 1.20f, 0.12f), floorRot, tint);

            // Left & right side walls (length along Z = 1.20, height = 0.52, thickness = 0.10)
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 2, 8, 20, new Vector3(-0.48f, 0.09f, -0.60f), new Vector3(0.10f, 0.52f, 1.20f), root, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 2, 8, 20, new Vector3(0.38f, 0.09f, -0.60f), new Vector3(0.10f, 0.52f, 1.20f), root, tint);

            // Front (+Z) & back (-Z) end walls
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 16, 8, 2, new Vector3(-0.38f, 0.09f, 0.50f), new Vector3(0.76f, 0.52f, 0.10f), root, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 16, 8, 2, new Vector3(-0.38f, 0.09f, -0.60f), new Vector3(0.76f, 0.52f, 0.10f), root, tint);
        }
    }

    /// <summary>Appends 3D ballistic Arrow projectiles in flight or stuck in blocks.</summary>
    public static void AppendArrows(
        List<OverlayVertex> dst,
        Vec3d cameraPos,
        IReadOnlyList<ArrowProjectileVisual>? arrows)
    {
        if (arrows is null || arrows.Count == 0) return;
        const int cell = EntityTextureAtlasGenerator.CellArrow;

        for (var i = 0; i < arrows.Count; i++)
        {
            var a = arrows[i];
            var rel = new Vector3(
                (float)(a.Position.X - cameraPos.X),
                (float)(a.Position.Y - cameraPos.Y),
                (float)(a.Position.Z - cameraPos.Z));
            if (rel.LengthSquared() > 110.0f * 110.0f) continue;

            var shake = a.ShakeStrength > 0.005f ? MathF.Sin(a.ShakeStrength * 42.0f) * 0.10f * a.ShakeStrength : 0.0f;
            var xform = Matrix4x4.CreateRotationX((-a.PitchDegrees * (MathF.PI / 180.0f)) + shake) *
                        Matrix4x4.CreateRotationY(a.YawDegrees * (MathF.PI / 180.0f)) *
                        Matrix4x4.CreateTranslation(rel);

            var l = Math.Clamp(a.LightBrightness, 0.14f, 1.0f);
            var tint = a.IsCritical ? new Vector4(1.0f, 0.94f, 0.78f, 1.0f) : new Vector4(l, l, l, 1.0f);

            // Crossed shaft planes along local Z [-0.45 .. +0.45] (+Z is tip direction when Yaw=0 -> Sin(0)=0, Cos(0)=1!)
            // Wait: let's check ArrowProjectileManager Yaw convention: Yaw = atan2(vx, vz), so +Z in local space transforms by RotationY(Yaw) to (+sin(Yaw), 0, +cos(Yaw)) = velocity direction!
            // In entity/projectiles/arrow.png (32x32), tip is at px=0 and feather tail is at px=16 along y=0..5!
            EmitDoubleSidedArrowPlane(dst, cell, xform, new Vector3(0.0f, -0.15f, -0.45f), new Vector3(0.0f, -0.15f, 0.45f), new Vector3(0.0f, 0.15f, 0.45f), new Vector3(0.0f, 0.15f, -0.45f), 16.0f, 0.0f, 0.0f, 5.0f, tint);
            EmitDoubleSidedArrowPlane(dst, cell, xform, new Vector3(-0.15f, 0.0f, -0.45f), new Vector3(-0.15f, 0.0f, 0.45f), new Vector3(0.15f, 0.0f, 0.45f), new Vector3(0.15f, 0.0f, -0.45f), 16.0f, 0.0f, 0.0f, 5.0f, tint);

            // Tail feather cross-cap at z = -0.38f (px = 0..5, py = 5..10)
            EmitDoubleSidedArrowPlane(dst, cell, xform, new Vector3(-0.15f, -0.15f, -0.38f), new Vector3(0.15f, -0.15f, -0.38f), new Vector3(0.15f, 0.15f, -0.38f), new Vector3(-0.15f, 0.15f, -0.38f), 0.0f, 5.0f, 5.0f, 10.0f, tint);
        }
    }

    private static void EmitDoubleSidedArrowPlane(
        List<OverlayVertex> dst,
        int cell,
        Matrix4x4 m,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        float px0,
        float py0,
        float px1,
        float py1,
        Vector4 tint)
    {
        var p0 = Vector3.Transform(a, m);
        var p1 = Vector3.Transform(b, m);
        var p2 = Vector3.Transform(c, m);
        var p3 = Vector3.Transform(d, m);
        EntityBoxUvBuilder.EmitEntityFace(dst, cell, p0, p1, p2, p3, px0, py0, px1, py1, tint);
        EntityBoxUvBuilder.EmitEntityFace(dst, cell, p1, p0, p3, p2, px1, py0, px0, py1, tint * new Vector4(0.92f, 0.92f, 0.92f, 1.0f));
    }

    private static void AppendPlayerArmorLayers(
        List<OverlayVertex> dst,
        Matrix4x4 root,
        Matrix4x4 headXform,
        float walkSwing,
        Vector4 baseTint,
        PlayerArmorVisual armor)
    {
        // 1. Leggings (layer_2: pelvis body [16,16,8,12,4] + left/right legs [0,16,4,12,4], inflated by +0.022f)
        var legCell = EntityTextureAtlasGenerator.GetArmorCell(armor.LeggingsTier, isLeggingsLayer2: true);
        if (legCell >= 0)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, legCell, 16, 16, 8, 12, 4, new Vector3(-0.272f, 0.728f, -0.147f), new Vector3(0.544f, 0.794f, 0.294f), root, baseTint);
            EntityBoxUvBuilder.EmitEntityBox(dst, legCell, 0, 16, 4, 12, 4, new Vector3(-0.147f, -0.772f, -0.147f), new Vector3(0.294f, 0.794f, 0.294f), Limb(root, new Vector3(-0.125f, 0.75f, 0.0f), walkSwing), baseTint);
            EntityBoxUvBuilder.EmitEntityBox(dst, legCell, 0, 16, 4, 12, 4, new Vector3(-0.147f, -0.772f, -0.147f), new Vector3(0.294f, 0.794f, 0.294f), Limb(root, new Vector3(0.125f, 0.75f, 0.0f), -walkSwing), baseTint);
        }

        // 2. Boots (layer_1: legs [0,16,4,12,4], inflated by +0.038f so boots cuff sits outside leggings!)
        var bootCell = EntityTextureAtlasGenerator.GetArmorCell(armor.BootsTier, isLeggingsLayer2: false);
        if (bootCell >= 0)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, bootCell, 0, 16, 4, 12, 4, new Vector3(-0.163f, -0.788f, -0.163f), new Vector3(0.326f, 0.826f, 0.326f), Limb(root, new Vector3(-0.125f, 0.75f, 0.0f), walkSwing), baseTint);
            EntityBoxUvBuilder.EmitEntityBox(dst, bootCell, 0, 16, 4, 12, 4, new Vector3(-0.163f, -0.788f, -0.163f), new Vector3(0.326f, 0.826f, 0.326f), Limb(root, new Vector3(0.125f, 0.75f, 0.0f), -walkSwing), baseTint);
        }

        // 3. Chestplate (layer_1: torso [16,16,8,12,4] + shoulder pauldrons [40,16,4,12,4], inflated by +0.038f)
        var chestCell = EntityTextureAtlasGenerator.GetArmorCell(armor.ChestplateTier, isLeggingsLayer2: false);
        if (chestCell >= 0)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, chestCell, 16, 16, 8, 12, 4, new Vector3(-0.288f, 0.712f, -0.163f), new Vector3(0.576f, 0.826f, 0.326f), root, baseTint);
            EntityBoxUvBuilder.EmitEntityBox(dst, chestCell, 40, 16, 4, 12, 4, new Vector3(-0.163f, -0.788f, -0.163f), new Vector3(0.326f, 0.826f, 0.326f), Limb(root, new Vector3(-0.375f, 1.50f, 0.0f), -walkSwing), baseTint);
            EntityBoxUvBuilder.EmitEntityBox(dst, chestCell, 40, 16, 4, 12, 4, new Vector3(-0.163f, -0.788f, -0.163f), new Vector3(0.326f, 0.826f, 0.326f), Limb(root, new Vector3(0.375f, 1.50f, 0.0f), walkSwing), baseTint);
        }

        // 4. Helmet (layer_1: head [0,0,8,8,8] + outer visor [32,0,8,8,8], inflated by +0.044f)
        var helmCell = EntityTextureAtlasGenerator.GetArmorCell(armor.HelmetTier, isLeggingsLayer2: false);
        if (helmCell >= 0)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, helmCell, 0, 0, 8, 8, 8, new Vector3(-0.294f, -0.044f, -0.294f), new Vector3(0.588f, 0.588f, 0.588f), headXform, baseTint);
            EntityBoxUvBuilder.EmitEntityBox(dst, helmCell, 32, 0, 8, 8, 8, new Vector3(-0.312f, -0.062f, -0.312f), new Vector3(0.624f, 0.624f, 0.624f), headXform, baseTint);
        }
    }

    private static void AppendSkeletonHeldBow(List<OverlayVertex> dst, Matrix4x4 rightArmXform, Vector4 tint)
    {
        var bowXform = Matrix4x4.CreateRotationX(1.15f) * Matrix4x4.CreateTranslation(0.0f, -0.64f, 0.04f) * rightArmXform;
        var wood = tint * new Vector4(0.52f, 0.36f, 0.18f, 1.0f);
        var str = tint * new Vector4(0.84f, 0.84f, 0.86f, 1.0f);
        EmitSolidBox(dst, new Vector3(-0.025f, -0.26f, 0.08f), new Vector3(0.05f, 0.52f, 0.05f), bowXform, wood);
        EmitSolidBox(dst, new Vector3(-0.015f, -0.25f, -0.06f), new Vector3(0.03f, 0.50f, 0.02f), bowXform, str);
    }

    private static void EmitSolidBox(List<OverlayVertex> dst, Vector3 min, Vector3 size, Matrix4x4 m, Vector4 col)
    {
        var max = min + size;
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float s)
        {
            var sc = new Vector4(col.X * s, col.Y * s, col.Z * s, col.W);
            var p0 = Vector3.Transform(a, m);
            var p1 = Vector3.Transform(b, m);
            var p2 = Vector3.Transform(c, m);
            var p3 = Vector3.Transform(d, m);
            dst.Add(new OverlayVertex(p0, sc));
            dst.Add(new OverlayVertex(p1, sc));
            dst.Add(new OverlayVertex(p2, sc));
            dst.Add(new OverlayVertex(p0, sc));
            dst.Add(new OverlayVertex(p2, sc));
            dst.Add(new OverlayVertex(p3, sc));
        }

        Q(new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), 1.0f);
        Q(new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), 0.6f);
        Q(new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), 0.9f);
        Q(new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), 0.85f);
        Q(new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z), 0.76f);
        Q(new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), 0.76f);
    }
}
