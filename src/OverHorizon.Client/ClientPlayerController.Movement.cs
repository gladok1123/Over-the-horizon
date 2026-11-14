using OverHorizon.Core;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Physics;

namespace OverHorizon.Client;

/// <summary>
/// Swept-AABB movement, Spectator noclip flight, respawn/pose sync, Jump &amp; Land audio, and vitals ticking
/// for <see cref="ClientPlayerController"/>.
/// </summary>
public sealed partial class ClientPlayerController
{
    private const double WalkSpeedBlocksPerSec = 4.317;
    private const double CreativeFlySpeedBlocksPerSec = 10.90;
    private const double SpectatorFlySpeedBlocksPerSec = 15.20;
    private const double CreativeFlyVerticalSpeed = 8.40;
    private const double SwimHorizontalSpeedBlocksPerSec = 3.25;
    private const double SprintMultiplier = 1.38;
    private const double JumpImpulse = 9.55;

    /// <summary>Seconds a Survival player must stand inside a Nether Portal before travelling (<c>4</c>, vanilla).</summary>
    public const double PortalTravelDwellSeconds = 4.0;

    /// <summary>Applies authoritative multiplayer PvP/mob/void combat damage and knockback impulse.</summary>
    public void ApplyAuthoritativeCombatHit(float newHealth, Vec3d knockbackVelocity, bool targetDied)
    {
        if (Mode == GameMode.Spectator || (Mode != GameMode.Survival && !targetDied && newHealth > 0.01f)) return;
        HurtFlashStrength = targetDied ? 0.0f : 1.0f;
        if (targetDied)
        {
            Health = 20.0f;
            HungerModel.Reset();
            StatusEffects.Clear();
            AirSupplySeconds = MaxAirSeconds;
            Velocity = Vec3d.Zero;
            _fallDistance = 0.0;
            Hotbar.ClearEntireInventory();
            return;
        }

        Health = Math.Clamp(newHealth, 0.0f, 20.0f);
        Velocity = new Vec3d(
            Velocity.X + knockbackVelocity.X,
            Math.Clamp(Velocity.Y + knockbackVelocity.Y, -12.0, 7.8),
            Velocity.Z + knockbackVelocity.Z);
        OnGround = false;
    }

    /// <summary>Respawns the player at <see cref="SpawnPosition"/> with full vitals while preserving current <see cref="Mode"/> and unified inventory.</summary>
    public void Respawn()
    {
        var bedOrSpawn = SpawnPosition;
        Health = 20.0f;
        HungerModel.Reset();
        StatusEffects.Clear();
        AirSupplySeconds = MaxAirSeconds;
        HurtFlashStrength = 0.0f;
        MiningProgress = 0.0f;
        BowChargeNormalized = 0.0f;
        IsBlockingShield = false;
        ShieldRaiseNormalized = 0.0f;
        RidingMinecartId = -1;
        DeathDropsExtracted = false;
        _fallDistance = 0.0;
        _vitals.ResetTimers();
        FeetPosition = bedOrSpawn;
        Velocity = Vec3d.Zero;
        Camera.SetRotation(0.0f, 0.0f);
        Camera.Position = bedOrSpawn + new Vec3d(0.0, EyeHeight, 0.0);
        Hotbar.RefreshSlotVisuals(Mode);
    }

    /// <summary>Applies an authoritative server teleport or spawn synchronization.</summary>
    /// <summary>Moves the player into <paramref name="dimension"/> and resets all portal travel state.</summary>
    /// <param name="dimension">Destination dimension.</param>
    public void SetDimension(DimensionId dimension)
    {
        Dimension = dimension;
        PortalDwellSeconds = 0.0;
        _portalTravelArmed = false;
        _portalTravelTarget = DimensionId.Overworld;
        _portalTravelBlock = default;
    }

    /// <summary>Blocks Nether Portal travel for <paramref name="seconds"/> so arrivals never bounce.</summary>
    /// <param name="seconds">Cooldown duration in seconds.</param>
    public void ApplyPortalCooldown(double seconds = 5.0) => PortalCooldownSeconds = System.Math.Max(0.0, seconds);

    /// <summary>Consumes a pending Nether Portal travel request raised by the dwell timer.</summary>
    /// <param name="target">Destination dimension.</param>
    /// <param name="scaledFeetBlock">Scaled destination feet block coordinate.</param>
    /// <returns><see langword="true"/> when a travel request was pending.</returns>
    public bool TryConsumePortalTravel(out DimensionId target, out BlockPos scaledFeetBlock)
    {
        if (!_portalTravelArmed)
        {
            target = DimensionId.Overworld;
            scaledFeetBlock = default;
            return false;
        }

        _portalTravelArmed = false;
        PortalDwellSeconds = 0.0;
        target = _portalTravelTarget;
        scaledFeetBlock = _portalTravelBlock;
        return true;
    }

    public void ApplyAuthoritativePose(Vec3d feetPosition, float yawDegrees, float pitchDegrees, bool updateSpawn = true)
    {
        FeetPosition = feetPosition;
        if (updateSpawn) SpawnPosition = feetPosition;
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

    /// <summary>Directly restores saved world vitals when loading a persisted world.</summary>
    public void RestoreSavedVitals(float health, int hunger, float saturation, Vec3d? respawnPoint)
    {
        Health = Math.Clamp(health, 1.0f, 20.0f);
        HungerModel.Hunger = Math.Clamp(hunger, 0, 20);
        HungerModel.Saturation = Math.Clamp(saturation, 0.0f, HungerModel.Hunger);
        if (respawnPoint is { } rp) SpawnPosition = rp;
    }

    /// <summary>Recomputes <see cref="CurrentTarget"/> against <paramref name="world"/> along the current camera look ray.</summary>
    public void RefreshRaycastTarget(ClientWorldCache world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (Mode == GameMode.Spectator)
        {
            CurrentTarget = null;
            return;
        }

        CurrentTarget = VoxelRaycaster.TryRaycast(world, Camera.Position, Camera.Forward, BlockReachDistance, out var hit) ? hit : null;
    }

    /// <summary>Advances player kinematics, Spectator noclip, Creative flight, swimming, Void hazard, Jump/Land audio, and vitals.</summary>
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
        if (_offhandSwingTimer > 0.0f) _offhandSwingTimer = MathF.Max(0.0f, _offhandSwingTimer - (float)clampedDt);
        if (_sleepAnimTimer > 0.0f) _sleepAnimTimer = MathF.Max(0.0f, _sleepAnimTimer - (float)clampedDt);

        var regenHeal = StatusEffects.Step((float)clampedDt);
        if (regenHeal > 0.0f && !IsDead)
        {
            Health = MathF.Min(20.0f, Health + regenHeal);
        }

        if (IsDead)
        {
            RidingMinecartId = -1;
            IsBlockingShield = false;
            ShieldRaiseNormalized = 0.0f;
            IsHeadUnderwater = _vitals.UpdateUnderwaterState(world, blocks, audio, Camera.Position, FeetPosition, IsHeadUnderwater, MasterVolume, clampedDt, false);
            return;
        }

        // Spectator Mode: pure 3D noclip flight through all blocks, caves, and Bedrock!
        if (Mode == GameMode.Spectator)
        {
            StepSpectatorNoclip(audio, input, descend, clampedDt);
            return;
        }

        StepPortalDwell(world, blocks, clampedDt);

        if (RidingMinecartId >= 0)
        {
            if (descend)
            {
                DismountMinecart();
            }
            else
            {
                IsHeadUnderwater = _vitals.UpdateUnderwaterState(world, blocks, audio, Camera.Position, FeetPosition, IsHeadUnderwater, MasterVolume, clampedDt, false);
                _vitals.StepSurvivalVitals(this, BuiltInBlocks.Air, MaxAirSeconds, clampedDt, 0.0, false, false, audio);
                RefreshRaycastTarget(world);
                audio.UpdateListener(Camera.Position, Camera.Forward, Camera.Up, MasterVolume);
                return;
            }
        }

        if (Mode != GameMode.Creative) IsFlying = false;

        var feetBlockKey = blocks.GetStateMetadata(world.GetBlockState(FeetPosition.ToBlockPos())).BlockKey;
        var inCobweb = feetBlockKey == BuiltInBlocks.Cobweb;
        var onLadder = feetBlockKey == BuiltInBlocks.Ladder;

        // Nether ground hazards: soul sand drags walkers down to ~40% speed and magma blocks scorch anyone standing on them.
        var groundBelowKey = blocks.GetStateMetadata(world.GetBlockState(FeetPosition.ToBlockPos().Offset(Direction.Down))).BlockKey;
        var onSoulSand = OnGround && !IsFlying && groundBelowKey == BuiltInBlocks.SoulSand;
        var onMagmaBlock = OnGround && !IsFlying && Mode != GameMode.Creative && groundBelowKey == BuiltInBlocks.MagmaBlock;
        if (onMagmaBlock)
        {
            _magmaBurnTickTimer += clampedDt;
            if (_magmaBurnTickTimer >= 0.5)
            {
                _magmaBurnTickTimer = 0.0;
                _ = ApplyDamage(1.0f, audio);
            }
        }
        else
        {
            _magmaBurnTickTimer = 0.0;
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

        var canSprint = input.Sprint && !IsBlockingShield && !_inFluid && (Mode == GameMode.Creative || HungerModel.CanSprint);
        IsSprinting = canSprint && wishLen > 0.1f;
        IsSneaking = descend && !IsFlying;

        var groundOrSwimSpeed = _inFluid ? SwimHorizontalSpeedBlocksPerSec : WalkSpeedBlocksPerSec;
        var baseSpeed = IsFlying ? CreativeFlySpeedBlocksPerSec : groundOrSwimSpeed;
        var sprintBoost = IsFlying ? 1.85 : SprintMultiplier;
        var speed = baseSpeed * (canSprint ? sprintBoost : 1.0) * StatusEffects.GetSpeedMultiplier();
        if (IsBlockingShield && !IsFlying) speed *= 0.42;
        if (IsSneaking && OnGround) speed *= 0.48;
        if (inCobweb && !IsFlying) speed *= 0.28;
        if (onSoulSand && !IsFlying) speed *= 0.42;

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
            AddExhaustion(canSprint ? 0.20f : 0.05f);
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

        if (!wasInFluid && _inFluid)
        {
            var heavy = preStepVelY < -8.0;
            audio.PlayPositional(SoundCue.WaterSplash, FeetPosition, gain: (heavy ? 0.72f : 0.48f) * MasterVolume, pitch: heavy ? 0.88f : 1.04f);
        }

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
            if (!wasOnGround && _fallDistance > 0.65)
            {
                if (Mode == GameMode.Survival && _fallDistance > 3.35 && preStepVelY < -6.5)
                {
                    audio.PlayPositional(_fallDistance > 6.5 ? SoundCue.FallBig : SoundCue.FallSmall, FeetPosition, gain: 0.68f * MasterVolume);
                    _ = ApplyDamage((float)Math.Floor(_fallDistance - 3.0), audio, bypassArmor: true);
                }
                else
                {
                    audio.PlayPositional(SoundCue.LandSoft, FeetPosition, gain: 0.46f * MasterVolume, pitch: 0.94f);
                }
            }

            _fallDistance = 0.0;
        }

        var dx = FeetPosition.X - previousFeet.X;
        var dy = FeetPosition.Y - previousFeet.Y;
        var dz = FeetPosition.Z - previousFeet.Z;
        var horizMoved = Math.Sqrt((dx * dx) + (dz * dz));
        if ((OnGround || _inFluid) && !IsFlying && horizMoved > 1e-4) WalkBobPhase += (float)(horizMoved * 3.1);

        var sneakEyeDip = IsSneaking ? -0.14 : 0.0;
        var bobOffsetY = (OnGround && !IsFlying) ? Math.Sin(WalkBobPhase) * 0.035 : 0.0;
        Camera.Position = FeetPosition + new Vec3d(0.0, EyeHeight + sneakEyeDip + bobOffsetY, 0.0);

        IsHeadUnderwater = _vitals.UpdateUnderwaterState(
            world, blocks, audio, Camera.Position, FeetPosition, IsHeadUnderwater, MasterVolume, clampedDt, _inFluid && (horizMoved > 0.008 || input.Jump));
        _vitals.StepSurvivalVitals(this, feetBlockKey, MaxAirSeconds, clampedDt, horizMoved, canSprint, _inFluid, audio);
        RefreshRaycastTarget(world);
        audio.UpdateListener(Camera.Position, Camera.Forward, Camera.Up, MasterVolume);
        _vitals.UpdateFootstepAudio(this, world, blocks, audio, horizMoved, dy, onLadder);
    }

    /// <summary>Advances the Nether Portal dwell timer and arms a travel request once the dwell completes.</summary>
    /// <param name="world">Client voxel cache.</param>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="dt">Frame delta in seconds.</param>
    private void StepPortalDwell(ClientWorldCache world, IBlockRegistry blocks, double dt)
    {
        if (PortalCooldownSeconds > 0.0)
        {
            PortalCooldownSeconds = Math.Max(0.0, PortalCooldownSeconds - dt);
            PortalDwellSeconds = 0.0;
            return;
        }

        var feet = FeetPosition.ToBlockPos();
        var head = new BlockPos(feet.X, feet.Y + 1, feet.Z);
        var insidePortal = NetherPortalRules.IsPortal(blocks, world.GetBlockState(feet)) ||
                           NetherPortalRules.IsPortal(blocks, world.GetBlockState(head));
        if (!insidePortal)
        {
            PortalDwellSeconds = 0.0;
            return;
        }

        PortalDwellSeconds += dt;
        var requiredSeconds = Mode == GameMode.Creative ? 0.35 : PortalTravelDwellSeconds;
        if (PortalDwellSeconds < requiredSeconds || _portalTravelArmed)
        {
            return;
        }

        _portalTravelArmed = true;
        _portalTravelTarget = DimensionRules.Opposite(Dimension);
        _portalTravelBlock = NetherPortalRules.ScaleTravel(feet, Dimension, _portalTravelTarget);
    }

    private void StepSpectatorNoclip(IAudioSubsystem audio, (float Forward, float Strafe, bool Jump, bool Sprint) input, bool descend, double dt)
    {
        IsFlying = true;
        OnGround = false;
        _inFluid = false;
        IsHeadUnderwater = false;
        CurrentTarget = null;
        _fallDistance = 0.0;

        var camFwd = Camera.Forward;
        var rightFlat = Camera.Right;
        var wishX = (camFwd.X * input.Forward) + (rightFlat.X * input.Strafe);
        var wishY = (camFwd.Y * input.Forward) + (input.Jump ? 0.85f : 0.0f) - (descend ? 0.85f : 0.0f);
        var wishZ = (camFwd.Z * input.Forward) + (rightFlat.Z * input.Strafe);
        var speed = SpectatorFlySpeedBlocksPerSec * (input.Sprint ? 2.2 : 1.0);
        var blend = Math.Min(1.0, dt * 12.0);
        Velocity = new Vec3d(
            (Velocity.X * (1.0 - blend)) + (wishX * speed * blend),
            (Velocity.Y * (1.0 - blend)) + (wishY * speed * blend),
            (Velocity.Z * (1.0 - blend)) + (wishZ * speed * blend));
        FeetPosition += Velocity * dt;
        Camera.Position = FeetPosition + new Vec3d(0.0, EyeHeight, 0.0);
        audio.UpdateListener(Camera.Position, Camera.Forward, Camera.Up, MasterVolume);
    }

    internal void SetAirSupplyInternal(double val) => AirSupplySeconds = val;
    internal void SetHungerInternal(int val) => HungerModel.Hunger = Math.Clamp(val, 0, 20);
    internal void SetHealthInternal(float val) => Health = Math.Clamp(val, 0.0f, 20.0f);
}
