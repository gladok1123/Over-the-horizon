using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Modding.Commands;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.Server;

internal sealed class ConnectedPlayerState(
    long entityId,
    string username,
    ChunkPos initialChunk,
    Vec3d initialPosition,
    PermissionLevel initialPermission)
{
    public const float MaxPlayerHealth = 20.0f;

    public long EntityId { get; } = entityId;

    /// <summary>Gets or sets the network session identifier owning this player.</summary>
    public long SessionId { get; set; }

    public string Username { get; } = username;

    public Vec3d SpawnPosition { get; set; } = initialPosition;

    public ChunkPos CenterChunk { get; set; } = initialChunk;

    public Vec3d LastPosition { get; set; } = initialPosition;

    public float YawDegrees { get; set; }

    public float PitchDegrees { get; set; }

    public float Health { get; set; } = MaxPlayerHealth;

    public long LastHurtTick { get; set; } = -100L;

    public long LastVoidDamageTick { get; set; } = -100L;

    public PermissionLevel Permission { get; set; } = initialPermission;

    public int NextTeleportId { get; set; } = 1;

    public GameMode GameMode { get; set; } = GameMode.Survival;

    public ushort MainHandItemId { get; set; }

    public ushort OffhandItemId { get; set; }

    public byte PackedArmorLower { get; set; }

    public byte PackedArmorUpper { get; set; }

    public byte ActionFlags { get; set; }

    public byte BowChargeByte { get; set; }

    public byte SwingCounter { get; set; }

    public int RidingMinecartId { get; set; }

    public float RiderForwardSteer { get; set; }

    public HashSet<long> SentChunks { get; } = [];

    /// <summary>Gets or sets the dimension this player currently inhabits.</summary>
    public DimensionId Dimension { get; set; } = DimensionId.Overworld;

    /// <summary>Gets or sets elapsed seconds this player has continuously stood inside a Nether Portal.</summary>
    public double PortalDwellSeconds { get; set; }

    /// <summary>Gets or sets remaining seconds before this player may travel through another Nether Portal.</summary>
    public double PortalCooldownSeconds { get; set; }

    /// <summary>Gets or sets chunks queued for streaming to this player but not yet sent (smooths dimension switches).</summary>
    public Queue<ChunkPos> PendingChunkQueue { get; } = new();

    /// <summary>Gets a value indicating whether the player is immune to combat and environmental damage.</summary>
    public bool IsInvulnerable => GameMode is GameMode.Creative or GameMode.Spectator;

    /// <summary>Gets a value indicating whether the player is actively raising a Shield.</summary>
    public bool IsBlockingShield => (ActionFlags & 0x01) != 0;

    /// <summary>Gets a value indicating whether the player is sprinting.</summary>
    public bool IsSprinting => (ActionFlags & 0x04) != 0;

    /// <summary>Gets horizontal look forward vector derived from <see cref="YawDegrees"/>.</summary>
    public Vector3 LookForward
    {
        get
        {
            var r = YawDegrees * (MathF.PI / 180.0f);
            return new Vector3(MathF.Sin(r), 0.0f, -MathF.Cos(r));
        }
    }

    public RemotePlayerEquipmentPacket ToEquipmentPacket() =>
        new(
            EntityId,
            (byte)GameMode,
            MainHandItemId,
            OffhandItemId,
            PackedArmorLower,
            PackedArmorUpper,
            ActionFlags,
            BowChargeByte,
            SwingCounter,
            RidingMinecartId);

    /// <summary>Computes total armor defense points (<c>0..20</c>) from equipped 3D armor tiers.</summary>
    public int GetArmorDefensePoints()
    {
        var head = PackedArmorLower & 0x0F;
        var chest = (PackedArmorLower >> 4) & 0x0F;
        var legs = PackedArmorUpper & 0x0F;
        var boots = (PackedArmorUpper >> 4) & 0x0F;
        return GetSlotDefense(head, 0) + GetSlotDefense(chest, 1) + GetSlotDefense(legs, 2) + GetSlotDefense(boots, 3);
    }

    /// <summary>Computes damage multiplier (<c>0.20..1.0</c>) from equipped 3D armor tiers.</summary>
    public float GetArmorDamageMultiplier() =>
        Math.Clamp(1.0f - (GetArmorDefensePoints() * 0.04f), 0.20f, 1.0f);

    private static int GetSlotDefense(int tier, int slot) =>
        (tier, slot) switch
        {
            (1, 0) => 1, (1, 1) => 3, (1, 2) => 2, (1, 3) => 1, // Leather
            (2, 0) => 2, (2, 1) => 6, (2, 2) => 5, (2, 3) => 2, // Iron
            (3, 0) => 2, (3, 1) => 5, (3, 2) => 3, (3, 3) => 1, // Golden
            (4, 0) => 3, (4, 1) => 8, (4, 2) => 6, (4, 3) => 3, // Diamond
            _ => 0,
        };
}
