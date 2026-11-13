using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Celestial sky dome (Sun, Moon, uniformly distributed night Stars), 6-face 3D volumetric Clouds,
/// and deep-obsidian Void abyss geometry builders for <see cref="SilkOpenGlOverlayRenderer"/>.
/// </summary>
internal sealed partial class SilkOpenGlOverlayRenderer
{
    internal static void AppendCelestialBodies(List<OverlayVertex> dst, long worldTimeTicks, double cameraY)
    {
        var frac = (((worldTimeTicks % 24000L) + 24000L) % 24000L) / 24000.0f;
        var angle = (frac - 0.25f) * MathF.Tau;
        const float radius = 220.0f;
        // At Noon (worldTimeTicks = 6000, frac = 0.25, angle = 0), Cos(0) = +1 puts the Sun overhead at zenith (+Y);
        // at Midnight (18000, angle = pi), Cos(pi) = -1 puts the Moon overhead at zenith!
        var sunDir = Vector3.Normalize(new Vector3(-MathF.Sin(angle), MathF.Cos(angle), -0.14f));
        var moonDir = -sunDir;

        if (sunDir.Y > -0.28f)
        {
            EmitBillboardQuad(dst, sunDir * (radius + 2.0f), 38.0f, new Vector4(1.0f, 0.86f, 0.44f, 0.28f));
            EmitBillboardQuad(dst, sunDir * radius, 22.0f, new Vector4(1.0f, 0.97f, 0.74f, 0.98f));
        }

        if (moonDir.Y > -0.28f)
        {
            EmitBillboardQuad(dst, moonDir * (radius + 2.0f), 26.0f, new Vector4(0.62f, 0.74f, 0.95f, 0.22f));
            EmitBillboardQuad(dst, moonDir * radius, 17.0f, new Vector4(0.88f, 0.93f, 1.0f, 0.96f));
            EmitBillboardQuad(dst, moonDir * (radius - 0.5f), 11.0f, new Vector4(0.68f, 0.76f, 0.90f, 0.55f));
        }

        if (sunDir.Y < 0.14f)
        {
            var baseAlpha = Math.Clamp((-sunDir.Y + 0.14f) * 2.4f, 0.0f, 0.94f);
            var celestialSpin = Matrix4x4.CreateRotationZ(angle * 0.35f) * Matrix4x4.CreateRotationY(0.42f);

            // True spherical surface-area sampling using two independent SplitMix32 hashes -> zero horizon "skirt" band!
            for (var s = 0; s < 180; s++)
            {
                var hAz = SplitMix32((uint)(s * 2 + 101));
                var hEl = SplitMix32((uint)(s * 2 + 102));
                var hSz = SplitMix32((uint)(s * 3 + 777));

                var azimuth = (hAz / (float)uint.MaxValue) * MathF.Tau;
                var cosTheta = -0.18f + ((hEl / (float)uint.MaxValue) * 1.18f); // uniform on [-0.18, +1.0]
                var sinTheta = MathF.Sqrt(MathF.Max(0.0f, 1.0f - (cosTheta * cosTheta)));

                var localDir = new Vector3(MathF.Cos(azimuth) * sinTheta, cosTheta, MathF.Sin(azimuth) * sinTheta);
                var worldDir = Vector3.TransformNormal(localDir, celestialSpin);
                if (worldDir.Y < 0.02f) continue;

                var horizonFade = Math.Clamp(worldDir.Y * 4.5f, 0.15f, 1.0f);
                var twinkle = 0.76f + (0.24f * MathF.Sin((worldTimeTicks * 0.08f) + (s * 1.7f)));
                var size = 0.72f + ((hSz & 255U) / 255.0f * 0.72f);
                var tintR = 0.90f + (((hSz >> 8) & 15U) / 150.0f);
                var tintB = 0.92f + (((hSz >> 12) & 15U) / 190.0f);
                EmitBillboardQuad(dst, worldDir * 244.0f, size, new Vector4(tintR, 0.95f, tintB, baseAlpha * horizonFade * twinkle));
            }
        }

        if (cameraY < 32.0)
        {
            // Dark lower hemisphere cap so looking down near Bedrock never shows bright sky
            EmitCloudQuad(
                dst,
                new Vector3(-240.0f, -18.0f, -240.0f),
                new Vector3(240.0f, -18.0f, -240.0f),
                new Vector3(240.0f, -18.0f, 240.0f),
                new Vector3(-240.0f, -18.0f, 240.0f),
                new Vector4(0.012f, 0.012f, 0.018f, 1.0f));
        }
    }

    internal static void AppendWorldClouds(List<OverlayVertex> dst, Vec3d cameraPos, long worldTimeTicks, float driftOffset, float skyBrightness = 1.0f)
    {
        var tod = (int)(((worldTimeTicks % 24000L) + 24000L) % 24000L);
        var frac = tod / 24000.0f;
        var sunY = MathF.Cos((frac - 0.25f) * MathF.Tau);
        var solarFactor = Math.Clamp(0.065f + (MathF.Max(0.0f, sunY + 0.16f) * 0.88f), 0.065f, 1.0f);
        var dayFactor = Math.Clamp(MathF.Min(solarFactor, MathF.Max(0.065f, skyBrightness * 1.04f)), 0.065f, 1.0f);

        var sunsetFactor = 0.0f;
        if (tod is >= 11600 and <= 13800)
        {
            sunsetFactor = 1.0f - (MathF.Abs(tod - 12700.0f) / 1100.0f);
        }
        else if (tod is >= 22200 or <= 400)
        {
            var wrapped = tod >= 22200 ? tod - 24000 : tod;
            sunsetFactor = 1.0f - (MathF.Abs(wrapped + 700.0f) / 1100.0f);
        }

        sunsetFactor = Math.Clamp(sunsetFactor, 0.0f, 1.0f) * 0.52f;
        var nightFactor = Math.Clamp(1.0f - ((dayFactor - 0.065f) / 0.30f), 0.0f, 1.0f);
        var tintR = (1.0f - (nightFactor * 0.24f)) + (sunsetFactor * 0.30f);
        var tintG = (1.0f - (nightFactor * 0.14f)) - (sunsetFactor * 0.10f);
        var tintB = (1.0f + (nightFactor * 0.16f)) - (sunsetFactor * 0.28f);

        var topCol = new Vector4(0.99f * dayFactor * tintR, 0.99f * dayFactor * tintG, 1.0f * dayFactor * tintB, 0.84f);
        var botCol = new Vector4(0.74f * dayFactor * tintR, 0.78f * dayFactor * tintG, 0.88f * dayFactor * tintB, 0.82f);
        var nsCol = new Vector4(0.89f * dayFactor * tintR, 0.91f * dayFactor * tintG, 0.97f * dayFactor * tintB, 0.83f);
        var ewCol = new Vector4(0.82f * dayFactor * tintR, 0.85f * dayFactor * tintG, 0.93f * dayFactor * tintB, 0.83f);

        const double cellSize = 16.0;
        var worldXWithDrift = cameraPos.X - driftOffset;
        var baseCellX = (int)Math.Floor(worldXWithDrift / cellSize);
        var baseCellZ = (int)Math.Floor(cameraPos.Z / cellSize);
        var fracX = (float)(worldXWithDrift - (baseCellX * cellSize));
        var fracZ = (float)(cameraPos.Z - (baseCellZ * cellSize));
        var yBot = (float)(144.0 - cameraPos.Y);
        var yTop = yBot + 4.5f;

        for (var gz = -13; gz <= 13; gz++)
        {
            for (var gx = -13; gx <= 13; gx++)
            {
                var wx = baseCellX + gx;
                var wz = baseCellZ + gz;
                if (!IsCloudCellFilled(wx, wz)) continue;

                var minX = (gx * 16.0f) - fracX;
                var maxX = minX + 16.0f;
                var minZ = (gz * 16.0f) - fracZ;
                var maxZ = minZ + 16.0f;

                // Top & bottom faces
                EmitCloudQuad(dst, new Vector3(minX, yTop, minZ), new Vector3(minX, yTop, maxZ), new Vector3(maxX, yTop, maxZ), new Vector3(maxX, yTop, minZ), topCol);
                EmitCloudQuad(dst, new Vector3(minX, yBot, minZ), new Vector3(maxX, yBot, minZ), new Vector3(maxX, yBot, maxZ), new Vector3(minX, yBot, maxZ), botCol);

                // Cull internal vertical faces between contiguous 3D cloud voxels so clouds form solid volumetric masses!
                if (!IsCloudCellFilled(wx, wz - 1))
                {
                    EmitCloudQuad(dst, new Vector3(maxX, yBot, minZ), new Vector3(minX, yBot, minZ), new Vector3(minX, yTop, minZ), new Vector3(maxX, yTop, minZ), nsCol);
                }

                if (!IsCloudCellFilled(wx, wz + 1))
                {
                    EmitCloudQuad(dst, new Vector3(minX, yBot, maxZ), new Vector3(maxX, yBot, maxZ), new Vector3(maxX, yTop, maxZ), new Vector3(minX, yTop, maxZ), nsCol);
                }

                if (!IsCloudCellFilled(wx - 1, wz))
                {
                    EmitCloudQuad(dst, new Vector3(minX, yBot, minZ), new Vector3(minX, yBot, maxZ), new Vector3(minX, yTop, maxZ), new Vector3(minX, yTop, minZ), ewCol);
                }

                if (!IsCloudCellFilled(wx + 1, wz))
                {
                    EmitCloudQuad(dst, new Vector3(maxX, yBot, maxZ), new Vector3(maxX, yBot, minZ), new Vector3(maxX, yTop, minZ), new Vector3(maxX, yTop, maxZ), ewCol);
                }
            }
        }
    }

    private static void AppendVoidAbyssFloor(List<OverlayVertex> dst, Vec3d cameraPos)
    {
        var voidY = (float)((EngineConstants.WorldMinY - 1.5) - cameraPos.Y);
        var deepVoidY = (float)((EngineConstants.WorldMinY - 36.0) - cameraPos.Y);
        var voidCol = new Vector4(0.010f, 0.010f, 0.016f, 0.98f);

        // Render deep dark Void plane below broken Bedrock (only visible from above, or surrounding abyss when falling)
        if (cameraPos.Y >= EngineConstants.WorldMinY - 1.0)
        {
            EmitCloudQuad(dst, new Vector3(-160.0f, voidY, -160.0f), new Vector3(-160.0f, voidY, 160.0f), new Vector3(160.0f, voidY, 160.0f), new Vector3(160.0f, voidY, -160.0f), voidCol);
        }

        EmitCloudQuad(dst, new Vector3(-220.0f, deepVoidY, -220.0f), new Vector3(-220.0f, deepVoidY, 220.0f), new Vector3(220.0f, deepVoidY, 220.0f), new Vector3(220.0f, deepVoidY, -220.0f), voidCol);
    }

    private static bool IsCloudCellFilled(int wx, int wz)
    {
        // Coherent value-noise cluster over 16m cloud cells -> realistic blocky Minecraft cloud islands!
        var gx0 = wx >> 1;
        var gz0 = wz >> 1;
        var fx = (wx & 1) * 0.5f;
        var fz = (wz & 1) * 0.5f;
        var n00 = (SplitMix32((uint)((gx0 * 73856093) ^ (gz0 * 19349663))) & 1023U) / 1023.0f;
        var n10 = (SplitMix32((uint)(((gx0 + 1) * 73856093) ^ (gz0 * 19349663))) & 1023U) / 1023.0f;
        var n01 = (SplitMix32((uint)((gx0 * 73856093) ^ ((gz0 + 1) * 19349663))) & 1023U) / 1023.0f;
        var n11 = (SplitMix32((uint)(((gx0 + 1) * 73856093) ^ ((gz0 + 1) * 19349663))) & 1023U) / 1023.0f;
        var nx0 = n00 + ((n10 - n00) * fx);
        var nx1 = n01 + ((n11 - n01) * fx);
        return (nx0 + ((nx1 - nx0) * fz)) > 0.64f;
    }

    private static uint SplitMix32(uint x)
    {
        x += 0x9E3779B9U;
        x = (x ^ (x >> 16)) * 0x85EBCA6BU;
        x = (x ^ (x >> 13)) * 0xC2B2AE35U;
        return x ^ (x >> 16);
    }

    private static void EmitCloudQuad(List<OverlayVertex> dst, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 col)
    {
        dst.Add(new OverlayVertex(a, col));
        dst.Add(new OverlayVertex(b, col));
        dst.Add(new OverlayVertex(c, col));
        dst.Add(new OverlayVertex(a, col));
        dst.Add(new OverlayVertex(c, col));
        dst.Add(new OverlayVertex(d, col));
    }

    private static void EmitBillboardQuad(List<OverlayVertex> dst, Vector3 center, float halfSize, Vector4 col)
    {
        var right = Vector3.Normalize(Vector3.Cross(center, MathF.Abs(center.Y) > 200.0f ? Vector3.UnitX : Vector3.UnitY)) * halfSize;
        var up = Vector3.Normalize(Vector3.Cross(right, center)) * halfSize;
        EmitCloudQuad(dst, center - right - up, center + right - up, center + right + up, center - right + up, col);
    }
}
