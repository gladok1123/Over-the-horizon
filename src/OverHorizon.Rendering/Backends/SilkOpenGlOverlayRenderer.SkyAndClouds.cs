using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Celestial sky dome (Sun, Moon, Stars) and world-anchored volumetric Cloud geometry builders for <see cref="SilkOpenGlOverlayRenderer"/>.
/// </summary>
internal sealed partial class SilkOpenGlOverlayRenderer
{
    private static void AppendCelestialBodies(List<OverlayVertex> dst, long worldTimeTicks)
    {
        var frac = (((worldTimeTicks % 24000L) + 24000L) % 24000L) / 24000.0f;
        var angle = (frac - 0.25f) * MathF.Tau;
        const float radius = 220.0f;
        var sunDir = Vector3.Normalize(new Vector3(MathF.Cos(angle), -MathF.Sin(angle), -0.14f));
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

        if (sunDir.Y < 0.12f)
        {
            var starAlpha = Math.Clamp((-sunDir.Y + 0.12f) * 2.4f, 0.0f, 0.92f);
            for (var s = 0; s < 72; s++)
            {
                var h1 = (uint)((s * 747796405) + 2891336453);
                var yaw = ((h1 & 1023) / 1024.0f) * MathF.Tau;
                var elev = 0.08f + ((((h1 >> 10) & 511) / 512.0f) * 1.38f);
                var dir = new Vector3(MathF.Cos(yaw) * MathF.Cos(elev), MathF.Sin(elev), MathF.Sin(yaw) * MathF.Cos(elev));
                EmitBillboardQuad(dst, dir * 240.0f, 1.15f, new Vector4(0.95f, 0.97f, 1.0f, starAlpha));
            }
        }
    }

    private static void AppendWorldClouds(List<OverlayVertex> dst, Vec3d cameraPos, long worldTimeTicks, float driftOffset)
    {
        var frac = (((worldTimeTicks % 24000L) + 24000L) % 24000L) / 24000.0f;
        var sunY = -MathF.Sin((frac - 0.25f) * MathF.Tau);
        var dayFactor = Math.Clamp(0.22f + ((sunY + 0.25f) * 0.85f), 0.16f, 1.0f);
        var topCol = new Vector4(0.98f * dayFactor, 0.99f * dayFactor, 1.0f * dayFactor, 0.78f);
        var botCol = new Vector4(0.84f * dayFactor, 0.87f * dayFactor, 0.93f * dayFactor, 0.74f);

        var worldXWithDrift = cameraPos.X - driftOffset;
        var baseCellX = (int)Math.Floor(worldXWithDrift / 48.0);
        var baseCellZ = (int)Math.Floor(cameraPos.Z / 48.0);
        var fracX = (float)(worldXWithDrift - (baseCellX * 48.0));
        var fracZ = (float)(cameraPos.Z - (baseCellZ * 48.0));
        var yBot = (float)(142.0 - cameraPos.Y);
        var yTop = yBot + 3.5f;

        for (var gz = -5; gz <= 5; gz++)
        {
            for (var gx = -5; gx <= 5; gx++)
            {
                var wx = baseCellX + gx;
                var wz = baseCellZ + gz;
                var hash = (uint)((wx * 73856093) ^ (wz * 19349663));
                if ((hash & 7U) > 2U) continue;

                var minX = (gx * 48.0f) - fracX;
                var maxX = minX + 28.0f + (((hash >> 3) & 1U) * 14.0f);
                var minZ = (gz * 48.0f) - fracZ;
                var maxZ = minZ + 22.0f + (((hash >> 4) & 3U) * 6.0f);

                EmitCloudQuad(dst, new Vector3(minX, yBot, minZ), new Vector3(maxX, yBot, minZ), new Vector3(maxX, yBot, maxZ), new Vector3(minX, yBot, maxZ), botCol);
                EmitCloudQuad(dst, new Vector3(minX, yTop, minZ), new Vector3(minX, yTop, maxZ), new Vector3(maxX, yTop, maxZ), new Vector3(maxX, yTop, minZ), topCol);
            }
        }
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
