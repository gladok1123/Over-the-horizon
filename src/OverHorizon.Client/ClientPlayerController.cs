using OverHorizon.Core;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Effects;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;
using OverHorizon.Rendering.Camera;

namespace OverHorizon.Client;

/// <summary>
/// Client-side first-person player state, unified <see cref="ClientHotbar"/>, Spectator noclip flight,
/// Shield blocking, Offhand left-arm swing, <see cref="StatusEffectTracker"/>, Survival vitals, and sub-voxel raycasting.
/// </summary>
public sealed partial class ClientPlayerController
{
    /// <summary>Standard player horizontal bounding-box width in blocks.</summary>
    public const double PlayerWidth = 0.6;

    /// <summary>Standard player vertical bounding-box height in blocks.</summary>
    public const double PlayerHeight = 1.8;

    /// <summary>Eye offset above feet in blocks.</summary>
    public const double EyeHeight = 1.62;

    /// <summary>Maximum block interaction reach in blocks.</summary>
    public const double BlockReachDistance = 6.0;

    private const double MaxAirSeconds = 15.0;
    private const float HandSwingDurationSeconds = 0.24f;

    private readonly ClientPlayerVitalsAndAudio _vitals = new();
    private double _fallDistance;
    private float _handSwingTimer;
    private float _offhandSwingTimer;
    private float _sleepAnimTimer;
    private bool _inFluid;
    private bool _portalTravelArmed;
    private DimensionId _portalTravelTarget;
    private BlockPos _portalTravelBlock;

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

    /// <summary>Gets a value indicating whether Creative or Spectator Flight is currently engaged.</summary>
    public bool IsFlying { get; private set; }

    /// <summary>Gets a value indicating whether the player is sprinting.</summary>
    public bool IsSprinting { get; private set; }

    /// <summary>Gets a value indicating whether the player is sneaking/descending.</summary>
    public bool IsSneaking { get; private set; }

    /// <summary>Gets a value indicating whether the player's bounding box currently intersects a fluid.</summary>
    public bool InFluid => _inFluid;

    /// <summary>Gets a value indicating whether the camera eye point is submerged inside a fluid block.</summary>
    public bool IsHeadUnderwater { get; private set; }

    /// <summary>Gets the active gameplay mode (<c>Survival</c>, <c>Creative</c>, or <c>Spectator</c>).</summary>
    public GameMode Mode { get; private set; } = GameMode.Creative;

    /// <summary>Gets current health points (<c>0..20</c>).</summary>
    public float Health { get; private set; } = 20.0f;

    /// <summary>Gets the active timed status effects (<c>Swiftness, Regeneration, Strength, NightVision, Absorption, FireResistance</c>).</summary>
    public StatusEffectTracker StatusEffects { get; } = new();

    /// <summary>Gets the underlying Hunger + Saturation + Exhaustion model.</summary>
    public HungerAndVitalsModel HungerModel { get; } = new();

    /// <summary>Gets current hunger points (<c>0..20</c>).</summary>
    public int Hunger => HungerModel.Hunger;

    /// <summary>Gets current food saturation reserve (<c>0..20</c>).</summary>
    public float Saturation => HungerModel.Saturation;

    /// <summary>Gets remaining underwater breath in seconds (<c>0..15</c>).</summary>
    public double AirSupplySeconds { get; private set; } = MaxAirSeconds;

    /// <summary>Gets normalized underwater oxygen supply (<c>0.0..1.0</c>).</summary>
    public float AirSupplyNormalized => (float)Math.Clamp(AirSupplySeconds / MaxAirSeconds, 0.0, 1.0);

    /// <summary>Gets screen red hurt-flash intensity (<c>0.0..1.0</c>).</summary>
    public float HurtFlashStrength { get; private set; }

    /// <summary>Gets or sets progressive Survival block-breaking ratio (<c>0.0..1.0</c>).</summary>
    public float MiningProgress { get; set; }

    /// <summary>Gets or sets normalized Bow draw progress (<c>0.0..1.0</c>) while holding Right-Click.</summary>
    public float BowChargeNormalized { get; set; }

    /// <summary>Gets or sets whether the player is actively raising a Shield with Right-Click.</summary>
    public bool IsBlockingShield { get; set; }

    /// <summary>Gets or sets normalized Shield raise progress (<c>0.0..1.0</c>).</summary>
    public float ShieldRaiseNormalized { get; set; }

    /// <summary>Gets the entity ID of the currently ridden <c>Minecart</c>, or <c>-1</c> when on foot.</summary>
    public int RidingMinecartId { get; private set; } = -1;

    /// <summary>Gets screen sleep-vignette opacity (<c>0.0..1.0</c>) during a Bed night-skip transition.</summary>
    public float SleepFadeAlpha => _sleepAnimTimer <= 0.0f ? 0.0f : MathF.Sin(Math.Clamp(1.0f - (_sleepAnimTimer / 1.10f), 0.0f, 1.0f) * MathF.PI);

    /// <summary>Gets whether the player has died (Survival damage or Creative deep-Void hazard).</summary>
    public bool IsDead => Mode != GameMode.Spectator && Health <= 0.0f;

    /// <summary>Gets the dimension the player currently inhabits.</summary>
    public DimensionId Dimension { get; private set; } = DimensionId.Overworld;

    /// <summary>Gets elapsed seconds the player has continuously stood inside a Nether Portal.</summary>
    public double PortalDwellSeconds { get; private set; }

    /// <summary>Gets remaining seconds before the player may travel through another Nether Portal.</summary>
    public double PortalCooldownSeconds { get; private set; }

    /// <summary>Gets the portal dwell progress in <c>[0, 1]</c> for the travelling overlay vignette.</summary>
    public float PortalDwellNormalized =>
        (float)System.Math.Clamp(PortalDwellSeconds / (Mode == GameMode.Creative ? 0.35 : PortalTravelDwellSeconds), 0.0, 1.0);

    /// <summary>Gets the active camera perspective (<c>0 = FirstPerson</c>, <c>1 = ThirdPersonBack</c>, <c>2 = ThirdPersonFront</c>).</summary>
    public int CameraPerspectiveMode { get; private set; }

    /// <summary>Gets or sets whether a death inventory drop is pending extraction on the current death.</summary>
    public bool DeathDropsExtracted { get; set; }

    /// <summary>Gets or sets a value indicating whether the F3 debug telemetry overlay is enabled.</summary>
    public bool IsDebugOverlayVisible { get; set; }

    /// <summary>Gets or sets mouse look sensitivity in degrees per pixel.</summary>
    public float MouseSensitivity { get; set; } = 0.12f;

    /// <summary>Gets or sets whether vertical mouse look is inverted.</summary>
    public bool InvertMouseY { get; set; }

    /// <summary>Gets or sets master audio gain in <c>[0.0, 1.0]</c>.</summary>
    public float MasterVolume { get; set; } = 1.0f;

    /// <summary>Gets the accumulated walk-bob phase in radians for the 3D held-item viewmodel.</summary>
    public float WalkBobPhase { get; private set; }

    /// <summary>Gets the normalized main-hand (right arm) swing animation progress in <c>[0.0, 1.0]</c>.</summary>
    public float HandSwingProgress => _handSwingTimer > 0.0f ? 1.0f - (_handSwingTimer / HandSwingDurationSeconds) : 0.0f;

    /// <summary>Gets the normalized offhand (left arm) swing animation progress in <c>[0.0, 1.0]</c>.</summary>
    public float OffhandSwingProgress => _offhandSwingTimer > 0.0f ? 1.0f - (_offhandSwingTimer / HandSwingDurationSeconds) : 0.0f;

    /// <summary>Gets a monotonic byte counter incremented on every hand swing for multiplayer replication.</summary>
    public byte SwingCounter { get; private set; }

    /// <summary>Gets whether the most recent hand swing used the left/offhand arm.</summary>
    public bool LastSwingWasOffhand { get; private set; }

    /// <summary>Gets the synchronized first-person 3D camera.</summary>
    public FirstPersonCamera Camera { get; }

    /// <summary>Gets the unified 41-slot inventory, hotbar, Offhand state, and Creative Catalog.</summary>
    public ClientHotbar Hotbar { get; }

    /// <summary>Gets the block currently targeted by the player's crosshair within 6 blocks, if any.</summary>
    public VoxelRaycastHit? CurrentTarget { get; private set; }

    /// <summary>Switches between <see cref="GameMode.Survival"/>, <see cref="GameMode.Creative"/>, and <see cref="GameMode.Spectator"/> while preserving the unified inventory.</summary>
    public void SetGameMode(GameMode mode)
    {
        Mode = mode;
        if (mode == GameMode.Survival) IsFlying = false;
        else if (mode == GameMode.Spectator) IsFlying = true;

        if (mode != GameMode.Survival || Health <= 0.0f)
        {
            Health = 20.0f;
            HungerModel.Reset();
            AirSupplySeconds = MaxAirSeconds;
            DeathDropsExtracted = false;
        }

        MiningProgress = 0.0f;
        IsBlockingShield = false;
        ShieldRaiseNormalized = 0.0f;
        _fallDistance = 0.0;
        Hotbar.RefreshSlotVisuals(mode);
    }

    /// <summary>Cycles <c>Creative -&gt; Survival -&gt; Spectator -&gt; Creative</c>.</summary>
    public GameMode CycleNextGameMode()
    {
        var next = Mode switch
        {
            GameMode.Creative => GameMode.Survival,
            GameMode.Survival => GameMode.Spectator,
            _ => GameMode.Creative,
        };
        SetGameMode(next);
        return next;
    }

    /// <summary>Toggles Creative Mode flight (double-tap <c>Space</c> or <c>G</c>).</summary>
    public bool ToggleCreativeFlight()
    {
        if (Mode == GameMode.Spectator)
        {
            IsFlying = true;
            return true;
        }

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

    /// <summary>Triggers the first-person right-hand (mainhand) or left-hand (offhand) swing animation.</summary>
    public void TriggerHandSwing(bool isOffhand = false)
    {
        if (isOffhand)
        {
            _offhandSwingTimer = HandSwingDurationSeconds * 0.995f;
        }
        else
        {
            _handSwingTimer = HandSwingDurationSeconds * 0.995f;
        }

        LastSwingWasOffhand = isOffhand;
        SwingCounter = unchecked((byte)(SwingCounter + 1));
    }

    /// <summary>Starts the smooth 1.1-second Bed sleep screen vignette.</summary>
    public void StartSleepVignette() => _sleepAnimTimer = 1.10f;

    /// <summary>Mounts a <c>Minecart</c> vehicle by entity ID.</summary>
    public void MountMinecart(int cartEntityId)
    {
        RidingMinecartId = cartEntityId;
        if (Mode != GameMode.Spectator) IsFlying = false;
        _fallDistance = 0.0;
        Velocity = Vec3d.Zero;
    }

    /// <summary>Dismounts the currently ridden <c>Minecart</c> slightly above the track.</summary>
    public void DismountMinecart()
    {
        if (RidingMinecartId < 0) return;
        RidingMinecartId = -1;
        FeetPosition += new Vec3d(0.0, 0.42, 0.0);
        Velocity = Vec3d.Zero;
        _fallDistance = 0.0;
        Camera.Position = FeetPosition + new Vec3d(0.0, EyeHeight, 0.0);
    }

    /// <summary>Synchronizes the player's seated pose to a moving <c>Minecart</c>.</summary>
    public void SyncMountedMinecartPose(Vec3d seatFeetPosition)
    {
        FeetPosition = seatFeetPosition;
        Velocity = Vec3d.Zero;
        OnGround = true;
        _fallDistance = 0.0;
        Camera.Position = seatFeetPosition + new Vec3d(0.0, EyeHeight - 0.34, 0.0);
    }

    /// <summary>Swaps the currently selected main-hand slot with the offhand slot (<c>F</c> key).</summary>
    public bool SwapOffhand()
    {
        if (IsDead) return false;
        Hotbar.SwapMainHandAndOffhand();
        return true;
    }

    /// <summary>Checks whether an incoming directional attack from <paramref name="sourceWorldPos"/> is deflected by an active raised Shield.</summary>
    public bool TryDeflectWithShield(Vec3d sourceWorldPos, IAudioSubsystem? audio = null)
    {
        if (!IsBlockingShield || (!Hotbar.SelectedIsShield && !Hotbar.OffhandIsShield)) return false;
        var toSource = sourceWorldPos - FeetPosition;
        var horizLen = Math.Sqrt((toSource.X * toSource.X) + (toSource.Z * toSource.Z));
        if (horizLen > 1e-4)
        {
            var look = Camera.HorizontalForward;
            var dot = ((toSource.X / horizLen) * look.X) + ((toSource.Z / horizLen) * look.Z);
            if (dot < 0.05) return false;
        }

        audio?.PlayPositional(SoundCue.ShieldBlock, Camera.Position, gain: 0.85f * MasterVolume);
        return true;
    }

    /// <summary>Cycles camera perspective (<c>FirstPerson -&gt; ThirdPersonBack -&gt; ThirdPersonFront -&gt; FirstPerson</c>) on <c>F5</c>.</summary>
    public int CycleCameraPerspective()
    {
        CameraPerspectiveMode = (CameraPerspectiveMode + 1) % 3;
        return CameraPerspectiveMode;
    }

    /// <summary>
    /// Builds the active render camera for the current frame, pulling back up to <c>3.8m</c> behind or <c>3.6m</c> in front
    /// in <c>F5</c> third-person modes while clamping against solid voxels so the camera never clips inside walls.
    /// </summary>
    public FirstPersonCamera BuildRenderCamera(ClientWorldCache world)
    {
        if (CameraPerspectiveMode == 0 || Mode == GameMode.Spectator)
        {
            return Camera;
        }

        var isFront = CameraPerspectiveMode == 2;
        var fwd = Camera.Forward;
        var rayDir = isFront
            ? new Vec3d(fwd.X, fwd.Y, fwd.Z)
            : new Vec3d(-fwd.X, -fwd.Y, -fwd.Z);
        var maxDist = isFront ? 3.55 : 3.85;
        var safeDist = maxDist;

        if (VoxelRaycaster.TryRaycast(world, Camera.Position, new System.Numerics.Vector3((float)rayDir.X, (float)rayDir.Y, (float)rayDir.Z), maxDist + 0.30, out var wallHit))
        {
            safeDist = Math.Max(0.28, wallHit.Distance - 0.28);
        }

        var renderCam = new FirstPersonCamera
        {
            FieldOfViewDegrees = Camera.FieldOfViewDegrees,
            AspectRatio = Camera.AspectRatio,
            NearPlane = Camera.NearPlane,
            FarPlane = Camera.FarPlane,
            Position = Camera.Position + (rayDir * safeDist),
        };
        renderCam.SetRotation(
            isFront ? Camera.YawDegrees + 180.0f : Camera.YawDegrees,
            isFront ? -Camera.PitchDegrees : Camera.PitchDegrees);
        return renderCam;
    }

    /// <summary>Applies Survival damage (or Creative Void damage when <paramref name="bypassInvulnerability"/> is set) and triggers red hurt feedback.</summary>
    public bool ApplyDamage(
        float amount,
        IAudioSubsystem? audio = null,
        bool bypassArmor = false,
        SoundCue customCue = SoundCue.PlayerHurt,
        bool bypassInvulnerability = false)
    {
        if ((!bypassInvulnerability && Mode != GameMode.Survival) || Mode == GameMode.Spectator || amount <= 0.0f || IsDead) return false;
        if (customCue == SoundCue.FireHurt && StatusEffects.HasEffect(StatusEffectType.FireResistance)) return false;

        var armorPoints = Math.Clamp(Hotbar.TotalArmorDefense - (Hotbar.SelectedIsShield || Hotbar.OffhandIsShield ? 2 : 0), 0, 20);
        var finalDmg = bypassArmor ? amount : amount * (1.0f - (armorPoints * 0.04f));
        finalDmg = StatusEffects.AbsorbDamage(MathF.Max(0.25f, finalDmg));
        if (finalDmg > 0.0f)
        {
            Health = MathF.Max(0.0f, Health - finalDmg);
        }

        HungerModel.AddExhaustion(0.10f);
        HurtFlashStrength = 1.0f;
        audio?.PlayPositional(customCue, Camera.Position, gain: 0.75f * MasterVolume);
        return true;
    }

    /// <summary>Restores hunger, saturation, and bonus health from eating food.</summary>
    public void ConsumeFood(int nutrition, float bonusHeal = 0.0f, float saturation = 0.0f)
    {
        var satGain = saturation > 0.0f ? saturation : nutrition * 1.2f;
        HungerModel.Eat(nutrition, satGain);
        if (bonusHeal > 0.0f) Health = MathF.Min(20.0f, Health + bonusHeal);
    }

    /// <summary>Adds activity exhaustion (combat swing, block harvest, jump).</summary>
    public void AddExhaustion(float amount)
    {
        if (Mode == GameMode.Survival) HungerModel.AddExhaustion(amount);
    }
}
