using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Camera;

/// <summary>
/// Six-plane view frustum extracted from a view-projection matrix for fast chunk-section AABB culling.
/// </summary>
public readonly struct Frustum
{
    private readonly Plane _left;
    private readonly Plane _right;
    private readonly Plane _bottom;
    private readonly Plane _top;
    private readonly Plane _near;
    private readonly Plane _far;
    private readonly Vec3d _cameraOrigin;

    private Frustum(
        Plane left,
        Plane right,
        Plane bottom,
        Plane top,
        Plane near,
        Plane far,
        Vec3d cameraOrigin)
    {
        _left = left;
        _right = right;
        _bottom = bottom;
        _top = top;
        _near = near;
        _far = far;
        _cameraOrigin = cameraOrigin;
    }

    /// <summary>
    /// Extracts a normalized 6-plane <see cref="Frustum"/> from <paramref name="camera"/>.
    /// </summary>
    /// <param name="camera">Active perspective camera.</param>
    /// <returns>Extracted <see cref="Frustum"/>.</returns>
    public static Frustum FromCamera(FirstPersonCamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var m = camera.ComputeRelativeViewProjectionMatrix();

        return new Frustum(
            NormalizePlane(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41),
            NormalizePlane(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41),
            NormalizePlane(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42),
            NormalizePlane(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42),
            NormalizePlane(m.M13, m.M23, m.M33, m.M43),
            NormalizePlane(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43),
            camera.Position);
    }

    /// <summary>
    /// Determines whether <paramref name="worldBox"/> is at least partially inside all 6 frustum half-spaces.
    /// </summary>
    /// <param name="worldBox">World-space axis-aligned bounding box.</param>
    /// <returns><see langword="true"/> if potentially visible; <see langword="false"/> if completely outside.</returns>
    public bool Intersects(BoundingBox worldBox)
    {
        // Rebase worldBox into camera-relative float coordinates for precision at arbitrary world offsets.
        var min = new Vector3(
            (float)(worldBox.MinX - _cameraOrigin.X),
            (float)(worldBox.MinY - _cameraOrigin.Y),
            (float)(worldBox.MinZ - _cameraOrigin.Z));

        var max = new Vector3(
            (float)(worldBox.MaxX - _cameraOrigin.X),
            (float)(worldBox.MaxY - _cameraOrigin.Y),
            (float)(worldBox.MaxZ - _cameraOrigin.Z));

        return IsInsideOrIntersectingPlane(_left, min, max) &&
            IsInsideOrIntersectingPlane(_right, min, max) &&
            IsInsideOrIntersectingPlane(_bottom, min, max) &&
            IsInsideOrIntersectingPlane(_top, min, max) &&
            IsInsideOrIntersectingPlane(_near, min, max) &&
            IsInsideOrIntersectingPlane(_far, min, max);
    }

    private static bool IsInsideOrIntersectingPlane(Plane plane, Vector3 min, Vector3 max)
    {
        // Positive vertex (p-vertex) along plane normal.
        var px = plane.Normal.X >= 0.0f ? max.X : min.X;
        var py = plane.Normal.Y >= 0.0f ? max.Y : min.Y;
        var pz = plane.Normal.Z >= 0.0f ? max.Z : min.Z;

        return (plane.Normal.X * px) + (plane.Normal.Y * py) + (plane.Normal.Z * pz) + plane.D >= 0.0f;
    }

    private static Plane NormalizePlane(float a, float b, float c, float d) =>
        Plane.Normalize(new Plane(a, b, c, d));
}
