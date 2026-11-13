using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Renders articulated multiplayer Remote Players (with fixed vertical head pitch, world light, 3D armor,
/// 3D Shield/Bow/Weapon poses, and left/right hand swings), 3D Dropped Items, smooth Falling Blocks, and 3D Particles.
/// </summary>
public static partial class WorldMob3DMesher
{
    /// <summary>Appends articulated 3D multiplayer avatars with world lighting, armor, held items, Bow draw, and Shield block.</summary>
    public static void AppendRemotePlayers(
        List<OverlayVertex> dst,
        Vec3d cameraPos,
        IReadOnlyList<RemotePlayerAvatar>? avatars)
    {
        if (avatars is null || avatars.Count == 0) return;
        const int c = EntityTextureAtlasGenerator.CellSteve;

        for (var i = 0; i < avatars.Count; i++)
        {
            var a = avatars[i];
            var poseOffsetY = 0.0f;
            if (a.IsRidingMinecart)
            {
                poseOffsetY = 0.30f;
            }
            else if (a.IsSneaking)
            {
                poseOffsetY = 0.14f;
            }

            var relFeet = new Vector3(
                (float)(a.FeetPosition.X - cameraPos.X),
                (float)(a.FeetPosition.Y - cameraPos.Y) - poseOffsetY,
                (float)(a.FeetPosition.Z - cameraPos.Z));
            if (relFeet.LengthSquared() > 128.0f * 128.0f) continue;

            var yawRad = a.YawDegrees * (MathF.PI / 180.0f);
            // Fix inverted head pitch: positive PitchDegrees (look up) requires negative X-rotation so +Z nose tilts +Y!
            var headPitchRad = -Math.Clamp(a.PitchDegrees, -89.0f, 89.0f) * (MathF.PI / 180.0f);
            var root = Matrix4x4.CreateRotationY(-yawRad + MathF.PI) * Matrix4x4.CreateTranslation(relFeet);
            var light = Math.Clamp(a.LightBrightness, 0.06f, 1.0f);
            var tint = ComputeTint(light, a.HurtFlash, 0.0f, onFire: false);

            var headXform = Matrix4x4.CreateRotationX(headPitchRad) *
                            Matrix4x4.CreateTranslation(0.0f, 1.50f, 0.0f) *
                            root;

            if (a.IsSpectator)
            {
                var ghostTint = new Vector4(tint.X, tint.Y, tint.Z, 0.45f);
                EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 8, 8, 8, new Vector3(-0.25f, 0.0f, -0.25f), new Vector3(0.50f, 0.50f, 0.50f), headXform, ghostTint);
                EntityBoxUvBuilder.EmitEntityBox(dst, c, 32, 0, 8, 8, 8, new Vector3(-0.268f, -0.018f, -0.268f), new Vector3(0.536f, 0.536f, 0.536f), headXform, ghostTint);
                continue;
            }

            var walkSwing = MathF.Sin(a.WalkPhase) * 0.55f;
            var atkSin = a.HandSwingProgress > 0.001f ? MathF.Sin(MathF.Sqrt(Math.Clamp(a.HandSwingProgress, 0.0f, 1.0f)) * MathF.PI) : 0.0f;

            // Right arm is at X = -0.375f; Left arm (Offhand) is at X = +0.375f
            var rightArmPitch = -walkSwing - (a.LeftHandSwing ? 0.0f : atkSin * 1.35f);
            var leftArmPitch = walkSwing - (a.LeftHandSwing ? atkSin * 1.35f : 0.0f);
            var rightArmInwardYaw = 0.0f;
            var leftArmInwardYaw = 0.0f;

            if (a.IsDrawingBow)
            {
                rightArmPitch = -1.45f + (headPitchRad * 0.85f);
                leftArmPitch = -1.40f + (headPitchRad * 0.85f);
                rightArmInwardYaw = -0.12f;
                leftArmInwardYaw = 0.32f;
            }
            else if (a.IsBlockingShield)
            {
                if (a.MainHandIsShield || !a.OffhandIsShield)
                {
                    rightArmPitch = -0.95f;
                    rightArmInwardYaw = -0.52f;
                }
                else
                {
                    leftArmPitch = -0.95f;
                    leftArmInwardYaw = 0.52f;
                }
            }
            else if (a.MainHandSpriteTile >= 0 || a.MainHandBlockTile >= 0 || a.MainHandIsShield)
            {
                rightArmPitch -= 0.28f;
            }

            EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 16, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), Limb(root, new Vector3(-0.125f, 0.75f, 0.0f), walkSwing), tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 16, 48, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), Limb(root, new Vector3(0.125f, 0.75f, 0.0f), -walkSwing), tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 16, 16, 8, 12, 4, new Vector3(-0.25f, 0.75f, -0.125f), new Vector3(0.50f, 0.75f, 0.25f), root, tint);

            var rightArmXform = Matrix4x4.CreateRotationX(rightArmPitch) * Matrix4x4.CreateRotationY(rightArmInwardYaw) * Matrix4x4.CreateTranslation(-0.375f, 1.50f, 0.0f) * root;
            var leftArmXform = Matrix4x4.CreateRotationX(leftArmPitch) * Matrix4x4.CreateRotationY(leftArmInwardYaw) * Matrix4x4.CreateTranslation(0.375f, 1.50f, 0.0f) * root;

            EntityBoxUvBuilder.EmitEntityBox(dst, c, 40, 16, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), rightArmXform, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 32, 48, 4, 12, 4, new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.25f, 0.75f, 0.25f), leftArmXform, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 0, 0, 8, 8, 8, new Vector3(-0.25f, 0.0f, -0.25f), new Vector3(0.50f, 0.50f, 0.50f), headXform, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, c, 32, 0, 8, 8, 8, new Vector3(-0.268f, -0.018f, -0.268f), new Vector3(0.536f, 0.536f, 0.536f), headXform, tint);

            if (a.Armor.HasAny)
            {
                AppendRemoteArmorWithCustomArms(dst, root, headXform, rightArmXform, leftArmXform, walkSwing, tint, a.Armor);
            }

            AppendThirdPersonHandAttachment(dst, rightArmXform, isLeftArm: false, a.MainHandIsShield, a.IsBlockingShield && (a.MainHandIsShield || !a.OffhandIsShield), a.MainHandIsBlock3D, a.MainHandBlockTile, a.MainHandSpriteTile, tint);
            AppendThirdPersonHandAttachment(dst, leftArmXform, isLeftArm: true, a.OffhandIsShield, a.IsBlockingShield && !a.MainHandIsShield && a.OffhandIsShield, false, -1, a.OffhandSpriteTile, tint);
        }
    }

    /// <summary>Appends spinning, bobbing 3D dropped item entities onto the world overlay mesh.</summary>
    public static void AppendDroppedItems(
        List<OverlayVertex> dst,
        Vec3d cameraPos,
        IReadOnlyList<DroppedItemVisual>? drops)
    {
        if (drops is null || drops.Count == 0) return;

        for (var i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            var rel = new Vector3(
                (float)(d.Position.X - cameraPos.X),
                (float)(d.Position.Y - cameraPos.Y),
                (float)(d.Position.Z - cameraPos.Z));
            if (rel.LengthSquared() > 72.0f * 72.0f) continue;

            var copies = 1;
            if (d.Count > 16)
            {
                copies = 3;
            }
            else if (d.Count > 1)
            {
                copies = 2;
            }

            var light = Math.Clamp(d.LightBrightness, 0.12f, 1.0f);

            for (var copy = 0; copy < copies; copy++)
            {
                var off = copy switch
                {
                    0 => Vector3.Zero,
                    1 => new Vector3(0.055f, 0.025f, -0.045f),
                    _ => new Vector3(-0.05f, copy * 0.025f, 0.05f),
                };
                var m = Matrix4x4.CreateRotationY(d.SpinRadians) * Matrix4x4.CreateTranslation(rel + off);

                if (d.IsBlock3D && d.SideTile >= 0)
                {
                    const float hs = 0.135f;
                    var top = d.TopTile >= 0 ? d.TopTile : d.SideTile;
                    HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, hs, -hs), new Vector3(-hs, hs, hs), new Vector3(hs, hs, hs), new Vector3(hs, hs, -hs), m, top, light);
                    HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(hs, -hs, -hs), new Vector3(hs, -hs, hs), new Vector3(-hs, -hs, hs), m, d.SideTile, light * 0.6f);
                    HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, -hs, hs), new Vector3(hs, -hs, hs), new Vector3(hs, hs, hs), new Vector3(-hs, hs, hs), m, d.SideTile, light * 0.86f);
                    HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(hs, -hs, -hs), new Vector3(-hs, -hs, -hs), new Vector3(-hs, hs, -hs), new Vector3(hs, hs, -hs), m, d.SideTile, light * 0.84f);
                    HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(-hs, -hs, hs), new Vector3(-hs, hs, hs), new Vector3(-hs, hs, -hs), m, d.SideTile, light * 0.72f);
                    HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(hs, -hs, hs), new Vector3(hs, -hs, -hs), new Vector3(hs, hs, -hs), new Vector3(hs, hs, hs), m, d.SideTile, light * 0.72f);
                }
                else if (d.SpriteTile >= 0)
                {
                    EmitDoubleSidedSpriteCard(dst, m, d.SpriteTile, 0.20f, light);
                }
            }
        }
    }

    /// <summary>Appends smooth continuous-Y 3D FallingBlock entities (Sand / Gravel).</summary>
    public static void AppendFallingBlocks(
        List<OverlayVertex> dst,
        Vec3d cameraPos,
        IReadOnlyList<FallingBlockVisual>? falling)
    {
        if (falling is null || falling.Count == 0) return;

        for (var i = 0; i < falling.Count; i++)
        {
            var fb = falling[i];
            var relCenter = new Vector3(
                (float)(fb.FeetPosition.X - cameraPos.X),
                (float)(fb.FeetPosition.Y + 0.5 - cameraPos.Y),
                (float)(fb.FeetPosition.Z - cameraPos.Z));
            if (relCenter.LengthSquared() > 96.0f * 96.0f) continue;

            const float hs = 0.495f;
            var m = Matrix4x4.CreateTranslation(relCenter);
            var l = Math.Clamp(fb.LightBrightness, 0.10f, 1.0f);
            var top = fb.TopTile >= 0 ? fb.TopTile : fb.SideTile;
            var bot = fb.BottomTile >= 0 ? fb.BottomTile : fb.SideTile;
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, hs, -hs), new Vector3(-hs, hs, hs), new Vector3(hs, hs, hs), new Vector3(hs, hs, -hs), m, top, l);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(hs, -hs, -hs), new Vector3(hs, -hs, hs), new Vector3(-hs, -hs, hs), m, bot, l * 0.55f);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, -hs, hs), new Vector3(hs, -hs, hs), new Vector3(hs, hs, hs), new Vector3(-hs, hs, hs), m, fb.SideTile, l * 0.82f);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(hs, -hs, -hs), new Vector3(-hs, -hs, -hs), new Vector3(-hs, hs, -hs), new Vector3(hs, hs, -hs), m, fb.SideTile, l * 0.80f);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(-hs, -hs, hs), new Vector3(-hs, hs, hs), new Vector3(-hs, hs, -hs), m, fb.SideTile, l * 0.70f);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(hs, -hs, hs), new Vector3(hs, -hs, -hs), new Vector3(hs, hs, -hs), new Vector3(hs, hs, hs), m, fb.SideTile, l * 0.70f);
        }
    }

    /// <summary>Appends camera-facing 3D billboard particles (block break debris, flames, smoke, splashes, sparks, void motes).</summary>
    public static void AppendWorldParticles(
        List<OverlayVertex> dst,
        FirstPersonCamera camera,
        IReadOnlyList<WorldParticleVisual>? particles)
    {
        if (particles is null || particles.Count == 0) return;

        var camFwd = camera.Forward;
        var right = Vector3.Normalize(Vector3.Cross(camFwd, Vector3.UnitY));
        if (!float.IsFinite(right.X)) right = Vector3.UnitX;
        var up = Vector3.Normalize(Vector3.Cross(right, camFwd));

        for (var i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            var rel = new Vector3(
                (float)(p.Position.X - camera.Position.X),
                (float)(p.Position.Y - camera.Position.Y),
                (float)(p.Position.Z - camera.Position.Z));
            if (rel.LengthSquared() > 64.0f * 64.0f) continue;

            var r = right * (p.Size * 0.5f);
            var u = up * (p.Size * 0.5f);
            var p0 = rel - r - u;
            var p1 = rel + r - u;
            var p2 = rel + r + u;
            var p3 = rel - r + u;

            if (p.TileIndex >= 0)
            {
                var (u0, v0, u1, v1) = HeldItem3DMesher.TileUvBounds(p.TileIndex, p.UvOffsetX, p.UvOffsetY, p.UvOffsetX + 0.25f, p.UvOffsetY + 0.25f);
                HeldItem3DMesher.EmitTexturedQuad(dst, p0, p1, p2, p3, new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), p.Color);
            }
            else
            {
                dst.Add(new OverlayVertex(p0, p.Color));
                dst.Add(new OverlayVertex(p1, p.Color));
                dst.Add(new OverlayVertex(p2, p.Color));
                dst.Add(new OverlayVertex(p0, p.Color));
                dst.Add(new OverlayVertex(p2, p.Color));
                dst.Add(new OverlayVertex(p3, p.Color));
            }
        }
    }

    private static void AppendRemoteArmorWithCustomArms(
        List<OverlayVertex> dst,
        Matrix4x4 root,
        Matrix4x4 headXform,
        Matrix4x4 rightArmXform,
        Matrix4x4 leftArmXform,
        float walkSwing,
        Vector4 tint,
        PlayerArmorVisual armor)
    {
        var legCell = EntityTextureAtlasGenerator.GetArmorCell(armor.LeggingsTier, isLeggingsLayer2: true);
        if (legCell >= 0)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, legCell, 16, 16, 8, 12, 4, new Vector3(-0.272f, 0.728f, -0.147f), new Vector3(0.544f, 0.794f, 0.294f), root, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, legCell, 0, 16, 4, 12, 4, new Vector3(-0.147f, -0.772f, -0.147f), new Vector3(0.294f, 0.794f, 0.294f), Limb(root, new Vector3(-0.125f, 0.75f, 0.0f), walkSwing), tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, legCell, 0, 16, 4, 12, 4, new Vector3(-0.147f, -0.772f, -0.147f), new Vector3(0.294f, 0.794f, 0.294f), Limb(root, new Vector3(0.125f, 0.75f, 0.0f), -walkSwing), tint);
        }

        var bootCell = EntityTextureAtlasGenerator.GetArmorCell(armor.BootsTier, isLeggingsLayer2: false);
        if (bootCell >= 0)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, bootCell, 0, 16, 4, 12, 4, new Vector3(-0.163f, -0.788f, -0.163f), new Vector3(0.326f, 0.826f, 0.326f), Limb(root, new Vector3(-0.125f, 0.75f, 0.0f), walkSwing), tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, bootCell, 0, 16, 4, 12, 4, new Vector3(-0.163f, -0.788f, -0.163f), new Vector3(0.326f, 0.826f, 0.326f), Limb(root, new Vector3(0.125f, 0.75f, 0.0f), -walkSwing), tint);
        }

        var chestCell = EntityTextureAtlasGenerator.GetArmorCell(armor.ChestplateTier, isLeggingsLayer2: false);
        if (chestCell >= 0)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, chestCell, 16, 16, 8, 12, 4, new Vector3(-0.288f, 0.712f, -0.163f), new Vector3(0.576f, 0.826f, 0.326f), root, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, chestCell, 40, 16, 4, 12, 4, new Vector3(-0.163f, -0.788f, -0.163f), new Vector3(0.326f, 0.826f, 0.326f), rightArmXform, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, chestCell, 40, 16, 4, 12, 4, new Vector3(-0.163f, -0.788f, -0.163f), new Vector3(0.326f, 0.826f, 0.326f), leftArmXform, tint);
        }

        var helmCell = EntityTextureAtlasGenerator.GetArmorCell(armor.HelmetTier, isLeggingsLayer2: false);
        if (helmCell >= 0)
        {
            EntityBoxUvBuilder.EmitEntityBox(dst, helmCell, 0, 0, 8, 8, 8, new Vector3(-0.294f, -0.044f, -0.294f), new Vector3(0.588f, 0.588f, 0.588f), headXform, tint);
            EntityBoxUvBuilder.EmitEntityBox(dst, helmCell, 32, 0, 8, 8, 8, new Vector3(-0.312f, -0.062f, -0.312f), new Vector3(0.624f, 0.624f, 0.624f), headXform, tint);
        }
    }

    private static void AppendThirdPersonHandAttachment(
        List<OverlayVertex> dst,
        Matrix4x4 armXform,
        bool isLeftArm,
        bool isShield,
        bool isBlockingShield,
        bool isBlock3D,
        int blockTile,
        int spriteTile,
        Vector4 tint)
    {
        if (isShield)
        {
            var side = isLeftArm ? 1.0f : -1.0f;
            var shieldM = isBlockingShield
                ? Matrix4x4.CreateRotationX(0.95f) * Matrix4x4.CreateTranslation(-0.08f * side, -0.62f, 0.24f) * armXform
                : Matrix4x4.CreateRotationY(side * 1.45f) * Matrix4x4.CreateTranslation(0.14f * side, -0.62f, 0.04f) * armXform;
            EntityBoxUvBuilder.EmitEntityBox(dst, EntityTextureAtlasGenerator.CellShield, 0, 0, 12, 22, 1, new Vector3(-0.20f, -0.34f, -0.025f), new Vector3(0.40f, 0.68f, 0.05f), shieldM, tint);
            return;
        }

        if (isBlock3D && blockTile >= 0)
        {
            const float hs = 0.14f;
            var blkM = Matrix4x4.CreateRotationY(0.785f) * Matrix4x4.CreateTranslation(0.0f, -0.68f, 0.12f) * armXform;
            var l = tint.X;
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, hs, -hs), new Vector3(-hs, hs, hs), new Vector3(hs, hs, hs), new Vector3(hs, hs, -hs), blkM, blockTile, l);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, -hs, hs), new Vector3(hs, -hs, hs), new Vector3(hs, hs, hs), new Vector3(-hs, hs, hs), blkM, blockTile, l * 0.85f);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(hs, -hs, -hs), new Vector3(-hs, -hs, -hs), new Vector3(-hs, hs, -hs), new Vector3(hs, hs, -hs), blkM, blockTile, l * 0.80f);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(-hs, -hs, -hs), new Vector3(-hs, -hs, hs), new Vector3(-hs, hs, hs), new Vector3(-hs, hs, -hs), blkM, blockTile, l * 0.72f);
            HeldItem3DMesher.EmitBlockCubeFace(dst, new Vector3(hs, -hs, hs), new Vector3(hs, -hs, -hs), new Vector3(hs, hs, -hs), new Vector3(hs, hs, hs), blkM, blockTile, l * 0.72f);
            return;
        }

        if (spriteTile >= 0)
        {
            var itemM = Matrix4x4.CreateRotationZ(-0.78f) * Matrix4x4.CreateRotationY(1.57f) * Matrix4x4.CreateTranslation(0.0f, -0.66f, 0.16f) * armXform;
            EmitDoubleSidedSpriteCard(dst, itemM, spriteTile, 0.24f, tint.X);
        }
    }

    private static void EmitDoubleSidedSpriteCard(List<OverlayVertex> dst, Matrix4x4 m, int tile, float hs, float light)
    {
        var (u0, v0, u1, v1) = HeldItem3DMesher.TileUvBounds(tile, 0.004f, 0.004f, 0.996f, 0.996f);
        var colF = new Vector4(light, light, light, 1.0f);
        var colB = new Vector4(light * 0.88f, light * 0.88f, light * 0.88f, 1.0f);
        HeldItem3DMesher.EmitTexturedQuad(dst, Vector3.Transform(new Vector3(-hs, -hs, 0.015f), m), Vector3.Transform(new Vector3(hs, -hs, 0.015f), m), Vector3.Transform(new Vector3(hs, hs, 0.015f), m), Vector3.Transform(new Vector3(-hs, hs, 0.015f), m), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0), colF);
        HeldItem3DMesher.EmitTexturedQuad(dst, Vector3.Transform(new Vector3(hs, -hs, -0.015f), m), Vector3.Transform(new Vector3(-hs, -hs, -0.015f), m), Vector3.Transform(new Vector3(-hs, hs, -0.015f), m), Vector3.Transform(new Vector3(hs, hs, -0.015f), m), new Vector2(u1, v1), new Vector2(u0, v1), new Vector2(u0, v0), new Vector2(u1, v0), colB);
    }
}
