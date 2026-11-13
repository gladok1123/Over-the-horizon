using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Render-thread snapshot of a connected remote player's 3D pose, lighting, limb animation, armor, and held equipment.
/// </summary>
public readonly record struct RemotePlayerAvatar(
    long EntityId,
    string Username,
    Vec3d FeetPosition,
    float YawDegrees,
    float PitchDegrees,
    float WalkPhase,
    float HurtFlash = 0.0f,
    float Health = 20.0f,
    PlayerArmorVisual Armor = default,
    float LightBrightness = 1.0f,
    bool IsSpectator = false,
    bool IsBlockingShield = false,
    bool IsDrawingBow = false,
    float BowCharge = 0.0f,
    float HandSwingProgress = 0.0f,
    bool LeftHandSwing = false,
    int MainHandSpriteTile = -1,
    bool MainHandIsShield = false,
    bool MainHandIsBow = false,
    bool MainHandIsBlock3D = false,
    int MainHandBlockTile = -1,
    bool OffhandIsShield = false,
    int OffhandSpriteTile = -1,
    bool IsSneaking = false,
    bool IsRidingMinecart = false);
