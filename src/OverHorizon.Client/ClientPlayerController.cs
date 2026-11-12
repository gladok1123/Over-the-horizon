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
/// Client-side first-person player kinematics, authentic water sinking &amp; active swim-up physics,
/// submerged underwater eye detection, Survival vitals (Hearts, Hunger, Oxygen, Fall Damage), PvP knockback, and DDA raycasting.
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
    private const double SwimHorizontalSpeedBlocksPerSec = 3.25;
    private const double SprintMultiplier = 1.38;
    private const double JumpImpulse = 9.55;
    private const double FootstepStrideBlocks = 1.80;
    private const double MaxAirSeconds = 15.0;
    private const float HandSwingDurationSeconds = 0.24f;

    private double _stepDistanceAccumulator;
    private double _fallDistance;
    private double _hungerDrainTimer;
    private double _regenTimer;
    private double _drownTimer;
    private double _swimSoundTimer;
    private float _handSwingTimer;
    private int _stepPitchCounter;
    private bool _inFluid;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientPlayerController"/> class.
    /// </summary>
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

    /// <summary>Gets a value indicating whether the player's bounding box currently intersects a fluid.</summary>
    public bool InFluid => _inFluid;

    /// <summary>Gets a value indicating whether the camera eye point is submerged inside a fluid block.</summary>
    public bool IsHeadUnderwater { get; private set; }

    /// <summary>Gets the active gameplay mode (<see cref="GameMode.Creative"/> or <see cref="GameMode.Survival"/>).</summary>
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
    public float HandSwingProgress => _handSwingTimer > 0.0f
        ? 1.0f - (_handSwingTimer / HandSwingDurationSeconds)
        : 0.0f;

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

    /// <summary>Triggers the first-person block/sword swing animation.</summary>
    public void TriggerHandSwing() => _handSwingTimer = HandSwingDurationSeconds;

    /// <summary>Applies Survival damage and triggers red hurt screen feedback.</summary>
    public bool ApplyDamage(float amount, IAudioSubsystem? audio = null)
    {
        if (Mode != GameMode.Survival || amount <= 0.0f || IsDead)
        {
            return false;
        }

        Health = MathF.Max(0.0f, Health - amount);
        HurtFlashStrength = 1.0f;
        audio?.PlayPositional(SoundCue.PlayerHurt, Camera.Position, gain: 0.75f * MasterVolume);
        return true;
    }

    /// <summary>Restores hunger and bonus health from eating food.</summary>
    public void ConsumeFood(int nutrition, float bonusHeal = 0.0f)
    {
        Hunger = Math.Clamp(Hunger + nutrition, 0, 20);
        if (bonusHeal > 0.0f)
        {
            Health = MathF.Min(20.0f, Health + bonusHeal);
        }
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

        if (Mode == GameMode.Survival)
        {
            Health = Math.Clamp(newHealth, 0.0f, 20.0f);
        }

        Velocity = new Vec3d(
            Velocity.X + knockbackVelocity.X,
            Math.Clamp(Velocity.Y + knockbackVelocity.Y, -12.0, 7.5),
            Velocity.Z + knockbackVelocity.Z);
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
        CurrentTarget = VoxelRaycaster.TryRaycast(world, Camera.Position, Camera.Forward, BlockReachDistance, out var hit)
            ? hit
            : null;
    }

    /// <summary>
    /// Advances client-side player movement, original-style water sinking/swimming, underwater optics,
    /// Survival vitals, and swept-AABB voxel physics by <paramref name="deltaSeconds"/>.
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
        HurtFlashStrength = MathF.Max(0.0f, HurtFlashStrength - ((float)clampedDt * 2.4f));
        if (_handSwingTimer > 0.0f)
        {
            _handSwingTimer = MathF.Max(0.0f, _handSwingTimer - (float)clampedDt);
        }

        if (IsDead)
        {
            UpdateUnderwaterState(world, blocks, audio, clampedDt, isSwimming: false);
            return;
        }

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

        var canSprint = input.Sprint && (!_inFluid) && (Mode == GameMode.Creative || Hunger > 6);
        var baseSpeed = _inFluid ? SwimHorizontalSpeedBlocksPerSec : WalkSpeedBlocksPerSec;
        var speed = baseSpeed * (canSprint ? SprintMultiplier : 1.0);

        var vy = Velocity.Y;
        if (input.Jump && OnGround && !_inFluid)
        {
            vy = JumpImpulse;
            OnGround = false;
        }

        // Preserve knockback horizontal momentum smoothly while blending player wish velocity
        var blendHoriz = Math.Min(1.0, clampedDt * 14.0);
        var vx = (Velocity.X * (1.0 - blendHoriz)) + (wishX * speed * blendHoriz);
        var vz = (Velocity.Z * (1.0 - blendHoriz)) + (wishZ * speed * blendHoriz);

        var previousFeet = FeetPosition;
        var wasInFluid = _inFluid;
        var wasOnGround = OnGround;
        var preStepVelY = vy;

        var step = KinematicsIntegrator.StepEntity(
            world,
            FeetPosition,
            new Vec3d(vx, vy, vz),
            PlayerWidth,
            PlayerHeight,
            clampedDt,
            swimUp: input.Jump);

        // Shoreline step-up: swimming against a 1-block riverbank while holding Space hops cleanly onto land!
        var nextVel = step.Velocity;
        if ((wasInFluid || step.InFluid) && input.Jump && step.CollidedHorizontally && nextVel.Y < 4.5)
        {
            nextVel = new Vec3d(nextVel.X, 6.2, nextVel.Z);
        }

        FeetPosition = step.Position;
        Velocity = nextVel;
        OnGround = step.OnGround;
        _inFluid = step.InFluid;

        if (!wasInFluid && _inFluid)
        {
            audio.PlayPositional(SoundCue.WaterSplash, FeetPosition, gain: 0.65f * MasterVolume, pitch: 1.0f);
        }

        // Fall damage in Survival (water cushions all fall distance!)
        if (_inFluid)
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
                var dmg = (float)Math.Floor(_fallDistance - 3.0);
                _ = ApplyDamage(dmg, audio);
            }

            _fallDistance = 0.0;
        }

        var dx = FeetPosition.X - previousFeet.X;
        var dz = FeetPosition.Z - previousFeet.Z;
        var horizMoved = Math.Sqrt((dx * dx) + (dz * dz));
        if ((OnGround || _inFluid) && horizMoved > 1e-4)
        {
            WalkBobPhase += (float)(horizMoved * 3.1);
        }

        var bobOffsetY = OnGround ? Math.Sin(WalkBobPhase) * 0.035 : 0.0;
        Camera.Position = FeetPosition + new Vec3d(0.0, EyeHeight + bobOffsetY, 0.0);

        UpdateUnderwaterState(world, blocks, audio, clampedDt, isSwimming: _inFluid && (horizMoved > 0.01 || input.Jump));
        StepSurvivalHungerAndRegen(clampedDt, horizMoved > 1e-3 && OnGround, canSprint, audio);
        RefreshRaycastTarget(world);
        audio.UpdateListener(Camera.Position, Camera.Forward, Camera.Up, MasterVolume);
        UpdateFootstepAudio(world, blocks, audio, horizMoved);
    }

    private void UpdateUnderwaterState(
        ClientWorldCache world,
        IBlockRegistry blocks,
        IAudioSubsystem audio,
        double dt,
        bool isSwimming)
    {
        var wasUnderwater = IsHeadUnderwater;
        var eyeState = world.GetBlockState(Camera.Position.ToBlockPos());
        var eyeKey = blocks.GetStateMetadata(eyeState).BlockKey;
        IsHeadUnderwater = blocks.GetRequired(eyeKey).IsFluid;

        if (!wasUnderwater && IsHeadUnderwater)
        {
            audio.PlayPositional(SoundCue.UnderwaterEnter, Camera.Position, gain: 0.70f * MasterVolume, pitch: 0.95f);
        }
        else if (wasUnderwater && !IsHeadUnderwater)
        {
            audio.PlayPositional(SoundCue.WaterSplash, Camera.Position, gain: 0.50f * MasterVolume, pitch: 1.12f);
        }

        if (isSwimming)
        {
            _swimSoundTimer += dt;
            if (_swimSoundTimer >= 0.68)
            {
                _swimSoundTimer = 0.0;
                var cue = IsHeadUnderwater ? SoundCue.UnderwaterEnter : SoundCue.WaterSplash;
                audio.PlayPositional(cue, FeetPosition, gain: 0.35f * MasterVolume, pitch: 0.92f);
            }
        }
    }

    private void StepSurvivalHungerAndRegen(double dt, bool movingOnGround, bool sprinting, IAudioSubsystem audio)
    {
        if (IsHeadUnderwater && Mode == GameMode.Survival)
        {
            AirSupplySeconds = Math.Max(0.0, AirSupplySeconds - dt);
            if (AirSupplySeconds <= 0.0)
            {
                _drownTimer += dt;
                if (_drownTimer >= 1.0)
                {
                    _drownTimer -= 1.0;
                    _ = ApplyDamage(2.0f, audio);
                }
            }
        }
        else
        {
            AirSupplySeconds = Math.Min(MaxAirSeconds, AirSupplySeconds + (dt * 3.5));
            _drownTimer = 0.0;
        }

        if (Mode != GameMode.Survival)
        {
            return;
        }

        if (movingOnGround)
        {
            _hungerDrainTimer += dt * (sprinting ? 2.2 : 0.65);
            if (_hungerDrainTimer >= 22.0)
            {
                _hungerDrainTimer = 0.0;
                Hunger = Math.Max(0, Hunger - 1);
            }
        }

        if (!IsDead && Health < 20.0f && Hunger >= 18)
        {
            _regenTimer += dt;
            if (_regenTimer >= 2.5)
            {
                _regenTimer = 0.0;
                Health = MathF.Min(20.0f, Health + 1.0f);
            }
        }
        else
        {
            _regenTimer = 0.0;
        }
    }

    private void UpdateFootstepAudio(
        ClientWorldCache world,
        IBlockRegistry blocks,
        IAudioSubsystem audio,
        double horizontalDist)
    {
        if (!OnGround || _inFluid || horizontalDist <= 1e-4)
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
            "grass_block" or "dirt" or "sand" or "gravel" or "oak_leaves" or "snow_block" or "crimson_wool" or "white_wool" or "blue_wool" => SoundCue.FootstepGrass,
            "oak_log" or "oak_planks" or "crafting_table" or "bookshelf" => SoundCue.FootstepWood,
            _ => SoundCue.FootstepStone,
        };

        var pitchJitter = 0.93f + (((_stepPitchCounter++ * 37) % 15) * 0.01f);
        audio.PlayPositional(cue, FeetPosition, gain: 0.50f * MasterVolume, pitch: pitchJitter);
    }
}
