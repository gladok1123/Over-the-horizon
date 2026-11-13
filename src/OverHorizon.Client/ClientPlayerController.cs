using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Physics;
using OverHorizon.Rendering.Camera;

namespace OverHorizon.Client;

/// <summary>
/// Client-side first-person player kinematics with crisp ground acceleration/braking, Creative Flight (<c>F</c> or double-tap <c>Space</c>),
/// ladder climbing, cobweb slowness, water swimming, Survival vitals (Hearts, Armor reduction, Hunger, Oxygen, Fall Damage), and DDA raycasting.
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
    private const double CreativeFlySpeedBlocksPerSec = 10.90;
    private const double CreativeFlyVerticalSpeed = 8.20;
    private const double SwimHorizontalSpeedBlocksPerSec = 3.25;
    private const double SprintMultiplier = 1.38;
    private const double JumpImpulse = 9.55;
    private const double MaxAirSeconds = 15.0;
    private const float HandSwingDurationSeconds = 0.24f;

    private readonly ClientPlayerVitalsAndAudio _vitals = new();
    private double _fallDistance;
    private float _handSwingTimer;
    private bool _inFluid;

    /// <summary>Initializes a new instance of the <see cref="ClientPlayerController"/> class.</summary>
    public ClientPlayerController(
        Vec3d initialFeetPosition,
        IBlockRegistry blocks,
        IRegistry<ItemDefinition>? items = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        FeetPosition = initialFeetPosition;
        SpawnPosition = initialFeetPosition;
        Hotbar = new ClientHotbar(blocks, items);
        Camera = new FirstPersonCamera
        {
            Position = initialFeetPosition + new Vec3d(0.0, EyeHeight, 0.0),
        };
    }

    /// <summary>Gets the player's current world-space feet position.</summary>
    public Vec3d FeetPosition { get; private set; }

    /// <summary>Gets or sets the player's respawn feet position.</summary>
    public Vec3d SpawnPosition { get; set; }

    /// <summary>Gets the player's current linear velocity (blocks/s).</summary>
    public Vec3d Velocity { get; private set; }

    /// <summary>Gets a value indicating whether the player is resting on solid ground.</summary>
    public bool OnGround { get; private set; }

    /// <summary>Gets a value indicating whether Creative Flight is currently engaged.</summary>
    public bool IsFlying { get; private set; }

    /// <summary>Gets a value indicating whether the player's bounding box currently intersects a fluid.</summary>
    public bool InFluid => _inFluid;

    /// <summary>Gets a value indicating whether the camera eye point is submerged inside a fluid block.</summary>
    public bool IsHeadUnderwater { get; private set; }

    /// <summary>Gets the active gameplay mode.</summary>
    public GameMode Mode { get; private set; } = GameMode.Creative;

    /// <summary>Gets current health points (<c>0..20</c>).</summary>
    public float Health { get; private set; } = 20.0f;

    /// <summary>Gets current hunger points (<c>0..20</c>).</summary>
    public int Hunger { get; private set; } = 20;

    /// <summary>Gets remaining underwater breath in seconds (<c>0..15</c>).</summary>
    public double AirSupplySeconds { get; private set; } = MaxAirSeconds;

    /// <summary>Gets normalized underwater oxygen supply (<c>0.0..1.0</c>).</summary>
    public float AirSupplyNormalized => (float)Math.Clamp(AirSupplySeconds / MaxAirSeconds, 0.0, 1.0);

    /// <summary>Gets screen red hurt-flash intensity (<c>0.0..1.0</c>).</summary>
    public float HurtFlashStrength { get; private set; }

    /// <summary>Gets or sets progressive Survival block-breaking ratio (<c>0.0..1.0</c>).</summary>
    public float MiningProgress { get; set; }

    /// <summary>Gets whether the player has died in Survival mode.</summary>
    public bool IsDead => Mode == GameMode.Survival && Health <= 0.0f;

    /// <summary>Gets or sets a value indicating whether the F3 debug telemetry overlay is enabled.</summary>
    public bool IsDebugOverlayVisible { get; set; }

    /// <summary>Gets or sets mouse look sensitivity in degrees per pixel.</summary>
    public float MouseSensitivity { get; set; } = 0.12f;

    /// <summary>Gets or sets master audio gain in <c>[0.0, 1.0]</c>.</summary>
    public float MasterVolume { get; set; } = 1.0f;

    /// <summary>Gets the accumulated walk-bob phase in radians for the 3D held-item viewmodel.</summary>
    public float WalkBobPhase { get; private set; }

    /// <summary>Gets the normalized hand-swing animation progress in <c>[0.0, 1.0]</c>.</summary>
    public float HandSwingProgress => _handSwingTimer > 0.0f ? 1.0f - (_handSwingTimer / HandSwingDurationSeconds) : 0.0f;

    /// <summary>Gets the synchronized first-person 3D camera.</summary>
    public FirstPersonCamera Camera { get; }

    /// <summary>Gets the 9-slot hotbar, 41-slot Survival inventory, and Creative Catalog.</summary>
    public ClientHotbar Hotbar { get; }

    /// <summary>Gets the block currently targeted by the player's crosshair within 6 blocks, if any.</summary>
    public VoxelRaycastHit? CurrentTarget { get; private set; }

    /// <summary>Switches between <see cref="GameMode.Creative"/> and <see cref="GameMode.Survival"/>.</summary>
    public void SetGameMode(GameMode mode)
    {
        Mode = mode;
        if (mode == GameMode.Survival) IsFlying = false;
        if (mode == GameMode.Creative || Health <= 0.0f)
        {
            Health = 20.0f;
            Hunger = 20;
            AirSupplySeconds = MaxAirSeconds;
        }

        MiningProgress = 0.0f;
        _fallDistance = 0.0;
        Hotbar.RefreshSlotVisuals(mode);
    }

    /// <summary>Toggles Creative Mode flight (<c>F</c> or double-tap <c>Space</c>).</summary>
    public bool ToggleCreativeFlight()
    {
        if (Mode != GameMode.Creative)
        {
            IsFlying = false;
            return false;
        }

        IsFlying = !IsFlying;
        _fallDistance = 0.0;
        if (IsFlying)
        {
            OnGround = false;
            Velocity = new Vec3d(Velocity.X, Math.Max(0.0, Velocity.Y), Velocity.Z);
        }

        return IsFlying;
    }

    /// <summary>Triggers the first-person block/sword swing animation.</summary>
    public void TriggerHandSwing() => _handSwingTimer = HandSwingDurationSeconds;

    /// <summary>Applies Survival damage (mitigated by equipped Armor defense points) and triggers red hurt feedback.</summary>
    public bool ApplyDamage(float amount, IAudioSubsystem? audio = null, bool bypassArmor = false)
    {
        if (Mode != GameMode.Survival || amount <= 0.0f || IsDead) return false;
        var finalDmg = bypassArmor ? amount : amount * (1.0f - (Hotbar.TotalArmorDefense * 0.04f));
        Health = MathF.Max(0.0f, Health - MathF.Max(0.25f, finalDmg));
        HurtFlashStrength = 1.0f;
        audio?.PlayPositional(SoundCue.PlayerHurt, Camera.Position, gain: 0.75f * MasterVolume);
        return true;
    }

    /// <summary>Restores hunger and bonus health from eating food.</summary>
    public void ConsumeFood(int nutrition, float bonusHeal = 0.0f)
    {
        Hunger = Math.Clamp(Hunger + nutrition, 0, 20);
        if (bonusHeal > 0.0f) Health = MathF.Min(20.0f, Health + bonusHeal);
    }

    /// <summary>Applies authoritative multiplayer PvP combat damage, knockback impulse, and respawn reset.</summary>
    public void ApplyAuthoritativeCombatHit(float newHealth, Vec3d knockbackVelocity, bool targetDied)
    {
        HurtFlashStrength = targetDied ? 0.0f : 1.0f;
        if (targetDied)
        {
            Health = 20.0f;
            Hunger = 20;
            AirSupplySeconds = MaxAirSeconds;
            Velocity = Vec3d.Zero;
            _fallDistance = 0.0;
            return;
        }

        if (Mode == GameMode.Survival) Health = Math.Clamp(newHealth, 0.0f, 20.0f);
        Velocity = new Vec3d(Velocity.X + knockbackVelocity.X, Math.Clamp(Velocity.Y + knockbackVelocity.Y, -12.0, 7.5), Velocity.Z + knockbackVelocity.Z);
        OnGround = false;
    }

    /// <summary>Respawns the player at <see cref="SpawnPosition"/> with full vitals.</summary>
    public void Respawn()
    {
        Health = 20.0f;
        Hunger = 20;
        AirSupplySeconds = MaxAirSeconds;
        HurtFlashStrength = 0.0f;
        MiningProgress = 0.0f;
        _fallDistance = 0.0;
        _vitals.ResetTimers();
        ApplyAuthoritativePose(SpawnPosition, 0.0f, 0.0f);
    }

    /// <summary>Applies an authoritative server teleport or spawn synchronization.</summary>
    public void ApplyAuthoritativePose(Vec3d feetPosition, float yawDegrees, float pitchDegrees)
    {
        FeetPosition = feetPosition;
        SpawnPosition = feetPosition;
        Velocity = Vec3d.Zero;
        _fallDistance = 0.0;
        Camera.SetRotation(yawDegrees, pitchDegrees);
        Camera.Position = feetPosition + new Vec3d(0.0, EyeHeight, 0.0);
    }

    /// <summary>Applies an authoritative position correction without snapping local mouse look angles.</summary>
    public void ApplyAuthoritativePositionOnly(Vec3d feetPosition)
    {
        FeetPosition = feetPosition;
        Velocity = new Vec3d(0.0, Math.Min(0.0, Velocity.Y), 0.0);
        _fallDistance = 0.0;
        Camera.Position = feetPosition + new Vec3d(0.0, EyeHeight, 0.0);
    }

    /// <summary>Recomputes <see cref="CurrentTarget"/> against <paramref name="world"/> along the current camera look ray.</summary>
    public void RefreshRaycastTarget(ClientWorldCache world)
    {
        ArgumentNullException.ThrowIfNull(world);
        CurrentTarget = VoxelRaycaster.TryRaycast(world, Camera.Position, Camera.Forward, BlockReachDistance, out var hit) ? hit : null;
    }

    /// <summary>Advances client-side player movement, Creative Flight, ladder/cobweb traversal, water swimming, vitals, and swept-AABB physics.</summary>
    public void StepMovement(
        ClientWorldCache world,
        IBlockRegistry blocks,
        IAudioSubsystem audio,
        (float Forward, float Strafe, bool Jump, bool Sprint) input,
        double deltaSeconds,
        bool descend = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(audio);

        var clampedDt = Math.Clamp(deltaSeconds, 0.0005, 0.1);
        HurtFlashStrength = MathF.Max(0.0f, HurtFlashStrength - ((float)clampedDt * 2.4f));
        if (_handSwingTimer > 0.0f) _handSwingTimer = MathF.Max(0.0f, _handSwingTimer - (float)clampedDt);

        if (IsDead)
        {
            IsHeadUnderwater = _vitals.UpdateUnderwaterState(world, blocks, audio, Camera.Position, FeetPosition, IsHeadUnderwater, MasterVolume, clampedDt, false);
            return;
        }

        if (Mode != GameMode.Creative) IsFlying = false;

        var feetBlockKey = blocks.GetStateMetadata(world.GetBlockState(FeetPosition.ToBlockPos())).BlockKey;
        var inCobweb = feetBlockKey == BuiltInBlocks.Cobweb;
        var onLadder = feetBlockKey == BuiltInBlocks.Ladder;

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

        var canSprint = input.Sprint && !_inFluid && (Mode == GameMode.Creative || Hunger > 6);
        var groundOrSwimSpeed = _inFluid ? SwimHorizontalSpeedBlocksPerSec : WalkSpeedBlocksPerSec;
        var baseSpeed = IsFlying ? CreativeFlySpeedBlocksPerSec : groundOrSwimSpeed;
        var sprintBoost = IsFlying ? 1.85 : SprintMultiplier;
        var speed = baseSpeed * (canSprint ? sprintBoost : 1.0);
        if (inCobweb && !IsFlying) speed *= 0.28;

        var vy = Velocity.Y;
        if (IsFlying)
        {
            var flyVert = (input.Jump ? 1.0 : 0.0) - (descend ? 1.0 : 0.0);
            var blendFlyY = Math.Min(1.0, clampedDt * 14.0);
            vy = (vy * (1.0 - blendFlyY)) + (flyVert * CreativeFlyVerticalSpeed * blendFlyY);
        }
        else if (onLadder)
        {
            _fallDistance = 0.0;
            var slideSpeed = descend ? 0.0 : Math.Max(vy, -2.4);
            vy = input.Jump || MathF.Abs(input.Forward) > 0.1f ? 3.8 : slideSpeed;
        }
        else if (inCobweb)
        {
            _fallDistance = 0.0;
            vy = Math.Clamp(vy, -0.8, input.Jump ? 1.2 : 0.4);
        }
        else if (input.Jump && OnGround && !_inFluid)
        {
            vy = JumpImpulse;
            OnGround = false;
        }

        var groundOrAirAccel = OnGround ? 24.0 : 8.5;
        var accelRate = IsFlying ? 13.5 : groundOrAirAccel;
        var blendHoriz = Math.Min(1.0, clampedDt * accelRate);
        var vx = (Velocity.X * (1.0 - blendHoriz)) + (wishX * speed * blendHoriz);
        var vz = (Velocity.Z * (1.0 - blendHoriz)) + (wishZ * speed * blendHoriz);

        var previousFeet = FeetPosition;
        var wasInFluid = _inFluid;
        var wasOnGround = OnGround;
        var preStepVelY = vy;

        var step = KinematicsIntegrator.StepEntity(
            world, FeetPosition, new Vec3d(vx, vy, vz), PlayerWidth, PlayerHeight, clampedDt, swimUp: !IsFlying && input.Jump, isFlying: IsFlying);

        var nextVel = step.Velocity;
        if (!IsFlying && (wasInFluid || step.InFluid) && input.Jump && step.CollidedHorizontally && nextVel.Y < 4.5)
        {
            nextVel = new Vec3d(nextVel.X, 6.2, nextVel.Z);
        }

        if (IsFlying && step.OnGround && descend) IsFlying = false;

        FeetPosition = step.Position;
        Velocity = nextVel;
        OnGround = step.OnGround;
        _inFluid = step.InFluid;

        if (!wasInFluid && _inFluid) audio.PlayPositional(SoundCue.WaterSplash, FeetPosition, gain: 0.65f * MasterVolume, pitch: 1.0f);

        if (_inFluid || IsFlying || onLadder || inCobweb)
        {
            _fallDistance = 0.0;
        }
        else if (!OnGround && FeetPosition.Y < previousFeet.Y)
        {
            _fallDistance += previousFeet.Y - FeetPosition.Y;
        }
        else if (OnGround)
        {
            if (!wasOnGround && Mode == GameMode.Survival && _fallDistance > 3.35 && preStepVelY < -6.5)
            {
                _ = ApplyDamage((float)Math.Floor(_fallDistance - 3.0), audio, bypassArmor: true);
            }

            _fallDistance = 0.0;
        }

        var dx = FeetPosition.X - previousFeet.X;
        var dz = FeetPosition.Z - previousFeet.Z;
        var horizMoved = Math.Sqrt((dx * dx) + (dz * dz));
        if ((OnGround || _inFluid) && !IsFlying && horizMoved > 1e-4) WalkBobPhase += (float)(horizMoved * 3.1);

        var bobOffsetY = (OnGround && !IsFlying) ? Math.Sin(WalkBobPhase) * 0.035 : 0.0;
        Camera.Position = FeetPosition + new Vec3d(0.0, EyeHeight + bobOffsetY, 0.0);

        IsHeadUnderwater = _vitals.UpdateUnderwaterState(
            world, blocks, audio, Camera.Position, FeetPosition, IsHeadUnderwater, MasterVolume, clampedDt, _inFluid && (horizMoved > 0.01 || input.Jump));
        _vitals.StepSurvivalVitals(this, MaxAirSeconds, clampedDt, horizMoved > 1e-3 && OnGround && !IsFlying, canSprint, audio);
        RefreshRaycastTarget(world);
        audio.UpdateListener(Camera.Position, Camera.Forward, Camera.Up, MasterVolume);
        _vitals.UpdateFootstepAudio(this, world, blocks, audio, horizMoved);
    }

    internal void SetAirSupplyInternal(double val) => AirSupplySeconds = val;
    internal void SetHungerInternal(int val) => Hunger = val;
    internal void SetHealthInternal(float val) => Health = val;
}
