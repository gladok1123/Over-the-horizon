using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Camera;

/// <summary>
/// Perspective 3D first-person camera supporting camera-relative origin rebasing for jitter-free large-world rendering.
/// </summary>
public sealed class FirstPersonCamera
{
    private float _pitchDegrees;
    private float _fovDegrees = 70.0f;
    private float _aspectRatio = 16.0f / 9.0f;

    /// <summary>Gets or sets the world-space eye position in double precision.</summary>
    public Vec3d Position { get; set; } = new(0.5, 80.0, 0.5);

    /// <summary>Gets or sets horizontal yaw in degrees (<c>0</c> = looking along <c>-Z</c> / North).</summary>
    public float YawDegrees { get; set; }

    /// <summary>Gets or sets vertical pitch in degrees (clamped to <c>[-89.9, 89.9]</c>).</summary>
    public float PitchDegrees
    {
        get => _pitchDegrees;
        set => _pitchDegrees = Math.Clamp(value, -89.9f, 89.9f);
    }

    /// <summary>Gets or sets vertical field of view in degrees (<c>[30, 120]</c>).</summary>
    public float FieldOfViewDegrees
    {
        get => _fovDegrees;
        set => _fovDegrees = Math.Clamp(value, 30.0f, 120.0f);
    }

    /// <summary>Gets or sets viewport width-to-height aspect ratio.</summary>
    public float AspectRatio
    {
        get => _aspectRatio;
        set => _aspectRatio = Math.Max(0.1f, value);
    }

    /// <summary>Gets or sets the perspective near clip plane distance.</summary>
    public float NearPlane { get; set; } = 0.08f;

    /// <summary>Gets or sets the perspective far clip plane distance.</summary>
    public float FarPlane { get; set; } = 512.0f;

    /// <summary>Gets the normalized forward look direction vector.</summary>
    public Vector3 Forward
    {
        get
        {
            var yawRad = YawDegrees * (MathF.PI / 180.0f);
            var pitchRad = _pitchDegrees * (MathF.PI / 180.0f);
            var cosPitch = MathF.Cos(pitchRad);

            var dir = new Vector3(
                MathF.Sin(yawRad) * cosPitch,
                MathF.Sin(pitchRad),
                -MathF.Cos(yawRad) * cosPitch);
            return Vector3.Normalize(dir);
        }
    }

    /// <summary>Gets the normalized right strafe vector.</summary>
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));

    /// <summary>Gets the normalized camera up vector.</summary>
    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Forward));

    /// <summary>Gets the normalized horizontal XZ forward movement vector.</summary>
    public Vector3 HorizontalForward
    {
        get
        {
            var yawRad = YawDegrees * (MathF.PI / 180.0f);
            return Vector3.Normalize(new Vector3(MathF.Sin(yawRad), 0.0f, -MathF.Cos(yawRad)));
        }
    }

    /// <summary>Gets the cardinal horizontal <see cref="Direction"/> the camera is currently facing.</summary>
    public Direction CardinalFacing => GetCardinalFacing();

    /// <summary>Sets horizontal yaw and vertical pitch in degrees.</summary>
    /// <param name="yawDegrees">Horizontal yaw in degrees.</param>
    /// <param name="pitchDegrees">Vertical pitch in degrees.</param>
    public void SetRotation(float yawDegrees, float pitchDegrees)
    {
        YawDegrees = yawDegrees % 360.0f;
        PitchDegrees = pitchDegrees;
    }

    /// <summary>Updates <see cref="AspectRatio"/> from pixel viewport dimensions.</summary>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    public void SetAspectRatio(int width, int height)
    {
        if (width > 0 && height > 0)
        {
            AspectRatio = (float)width / height;
        }
    }

    /// <summary>Applies mouse look deltas in degrees.</summary>
    /// <param name="deltaYaw">Horizontal yaw delta.</param>
    /// <param name="deltaPitch">Vertical pitch delta.</param>
    public void Rotate(float deltaYaw, float deltaPitch)
    {
        YawDegrees = (YawDegrees + deltaYaw) % 360.0f;
        PitchDegrees += deltaPitch;
    }

    /// <summary>
    /// Computes the camera-relative view matrix (camera eye anchored at <see cref="Vector3.Zero"/>).
    /// </summary>
    /// <returns>Camera-relative <see cref="Matrix4x4"/> view matrix.</returns>
    public Matrix4x4 ComputeRelativeViewMatrix() =>
        Matrix4x4.CreateLookAt(Vector3.Zero, Forward, Vector3.UnitY);

    /// <summary>
    /// Computes the right-handed perspective projection matrix.
    /// </summary>
    /// <returns>Perspective <see cref="Matrix4x4"/>.</returns>
    public Matrix4x4 ComputeProjectionMatrix() =>
        Matrix4x4.CreatePerspectiveFieldOfView(
            _fovDegrees * (MathF.PI / 180.0f),
            _aspectRatio,
            NearPlane,
            FarPlane);

    /// <summary>
    /// Computes the combined camera-relative <c>View * Projection</c> matrix.
    /// </summary>
    /// <returns>Combined <see cref="Matrix4x4"/>.</returns>
    public Matrix4x4 ComputeRelativeViewProjectionMatrix() =>
        ComputeRelativeViewMatrix() * ComputeProjectionMatrix();

    /// <summary>
    /// Alias for <see cref="ComputeRelativeViewProjectionMatrix"/>.
    /// </summary>
    /// <returns>Combined <see cref="Matrix4x4"/>.</returns>
    public Matrix4x4 BuildCameraRelativeViewProjectionMatrix() =>
        ComputeRelativeViewProjectionMatrix();

    /// <summary>
    /// Computes the cardinal horizontal <see cref="Direction"/> the camera is currently facing.
    /// </summary>
    /// <returns>Horizontal cardinal <see cref="Direction"/>.</returns>
    public Direction GetCardinalFacing()
    {
        var fwd = Forward;
        if (MathF.Abs(fwd.X) > MathF.Abs(fwd.Z))
        {
            return fwd.X >= 0.0f ? Direction.East : Direction.West;
        }

        return fwd.Z >= 0.0f ? Direction.South : Direction.North;
    }
}
