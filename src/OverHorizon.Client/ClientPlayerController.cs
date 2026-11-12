using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Physics;
using OverHorizon.Rendering.Camera;

namespace OverHorizon.Client;

/// <summary>
/// Client-side first-person player kinematics, camera synchronization, 6-block DDA raycast targeting,
/// 9-slot block hotbar, F3 toggle state, and footstep audio trigger.
/// </summary>
public sealed class ClientPlayerController
{
    /// <summary>Standard player horizontal bounding-box width in blocks.</summary>
    public const double PlayerWidth = 0.6;

    /// <summary>Standard player vertical bounding-box height in blocks.</summary>
    public const double PlayerHeight = 1.8;

    /// <summary>Eye offset above feet in blocks.</summary>
    public const double EyeHeight = 1.62;

    /// <summary>Maximum block interaction reach in blocks.</summary>
    public const double BlockReachDistance = 6.0;

    private const double WalkSpeedBlocksPerSec = 4.317;
    private const double SprintMultiplier = 1.38;
    private const double JumpImpulse = 8.4;
    private const double FootstepStrideBlocks = 1.85;

    private double _stepDistanceAccumulator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientPlayerController"/> class.
    /// </summary>
    /// <param name="initialFeetPosition">Initial world-space feet position.</param>
    /// <param name="blocks">Frozen block registry for the 9-slot building hotbar.</param>
    public ClientPlayerController(Vec3d initialFeetPosition, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        FeetPosition = initialFeetPosition;
        Hotbar = new ClientHotbar(blocks);
        Camera = new FirstPersonCamera
        {
            Position = initialFeetPosition + new Vec3d(0.0, EyeHeight, 0.0),
        };
    }

    /// <summary>Gets the player's current world-space feet position.</summary>
    public Vec3d FeetPosition { get; private set; }

    /// <summary>Gets the player's current linear velocity (blocks/s).</summary>
    public Vec3d Velocity { get; private set; }

    /// <summary>Gets a value indicating whether the player is resting on solid ground.</summary>
    public bool OnGround { get; private set; }

    /// <summary>Gets or sets a value indicating whether the F3 debug telemetry overlay is enabled.</summary>
    public bool IsDebugOverlayVisible { get; set; }

    /// <summary>Gets the synchronized first-person 3D camera.</summary>
    public FirstPersonCamera Camera { get; }

    /// <summary>Gets the 9-slot quick-build block hotbar.</summary>
    public ClientHotbar Hotbar { get; }

    /// <summary>Gets the block currently targeted by the player's crosshair within 6 blocks, if any.</summary>
    public VoxelRaycastHit? CurrentTarget { get; private set; }

    /// <summary>
    /// Applies an authoritative server teleport or spawn synchronization.
    /// </summary>
    /// <param name="feetPosition">Authoritative feet position.</param>
    /// <param name="yawDegrees">Horizontal yaw in degrees.</param>
    /// <param name="pitchDegrees">Vertical pitch in degrees.</param>
    public void ApplyAuthoritativePose(Vec3d feetPosition, float yawDegrees, float pitchDegrees)
    {
        FeetPosition = feetPosition;
        Velocity = Vec3d.Zero;
        Camera.SetRotation(yawDegrees, pitchDegrees);
        Camera.Position = feetPosition + new Vec3d(0.0, EyeHeight, 0.0);
    }

    /// <summary>
    /// Recomputes <see cref="CurrentTarget"/> against <paramref name="world"/> along the current camera look ray.
    /// </summary>
    /// <param name="world">Client world collision cache.</param>
    public void RefreshRaycastTarget(ClientWorldCache world)
    {
        ArgumentNullException.ThrowIfNull(world);
        CurrentTarget = VoxelRaycaster.TryRaycast(world, Camera.Position, Camera.Forward, BlockReachDistance, out var hit)
            ? hit
            : null;
    }

    /// <summary>
    /// Advances client-side player movement and swept-AABB voxel physics by <paramref name="deltaSeconds"/>.
    /// </summary>
    public void StepMovement(
        ClientWorldCache world,
        IBlockRegistry blocks,
        IAudioSubsystem audio,
        (float Forward, float Strafe, bool Jump, bool Sprint) input,
        double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(audio);

        var clampedDt = Math.Clamp(deltaSeconds, 0.0005, 0.1);
        var forwardFlat = Camera.HorizontalForward;
        var rightFlat = Camera.Right;

        var wishX = (forwardFlat.X * input.Forward) + (rightFlat.X * input.Strafe);
        var wishZ = (forwardFlat.Z * input.Forward) + (rightFlat.Z * input.Strafe);
        var wishLen = MathF.Sqrt((wishX * wishX) + (wishZ * wishZ));
        if (wishLen > 1e-4f)
        {
            wishX /= wishLen;
            wishZ /= wishLen;
        }

        var speed = WalkSpeedBlocksPerSec * (input.Sprint ? SprintMultiplier : 1.0);
        var vy = Velocity.Y;
        if (input.Jump && OnGround)
        {
            vy = JumpImpulse;
        }

        var inputVelocity = new Vec3d(wishX * speed, vy, wishZ * speed);
        var previousFeet = FeetPosition;

        var step = KinematicsIntegrator.StepEntity(
            world,
            FeetPosition,
            inputVelocity,
            PlayerWidth,
            PlayerHeight,
            clampedDt);

        FeetPosition = step.Position;
        Velocity = step.Velocity;
        OnGround = step.OnGround;
        Camera.Position = FeetPosition + new Vec3d(0.0, EyeHeight, 0.0);

        RefreshRaycastTarget(world);
        audio.UpdateListener(Camera.Position, Camera.Forward, Camera.Up);
        UpdateFootstepAudio(world, blocks, audio, previousFeet);
    }

    private void UpdateFootstepAudio(
        ClientWorldCache world,
        IBlockRegistry blocks,
        IAudioSubsystem audio,
        Vec3d previousFeet)
    {
        if (!OnGround)
        {
            return;
        }

        var dx = FeetPosition.X - previousFeet.X;
        var dz = FeetPosition.Z - previousFeet.Z;
        var horizontalDist = Math.Sqrt((dx * dx) + (dz * dz));
        if (horizontalDist <= 1e-4)
        {
            return;
        }

        _stepDistanceAccumulator += horizontalDist;
        if (_stepDistanceAccumulator < FootstepStrideBlocks)
        {
            return;
        }

        _stepDistanceAccumulator -= FootstepStrideBlocks;
        var belowPos = new BlockPos(
            (int)Math.Floor(FeetPosition.X),
            (int)Math.Floor(FeetPosition.Y - 0.2),
            (int)Math.Floor(FeetPosition.Z));

        var groundState = world.GetBlockState(belowPos);
        var groundKey = blocks.GetStateMetadata(groundState).BlockKey.Path;
        var cue = groundKey switch
        {
            "grass_block" or "dirt" or "sand" or "oak_leaves" => SoundCue.FootstepGrass,
            "oak_log" or "oak_planks" or "crafting_table" => SoundCue.FootstepWood,
            _ => SoundCue.FootstepStone,
        };

        audio.PlayPositional(cue, FeetPosition, gain: 0.45f, pitch: 1.0f);
    }
}
