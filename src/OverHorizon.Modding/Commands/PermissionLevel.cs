namespace OverHorizon.Modding.Commands;

/// <summary>
/// Hierarchical permission tier required to execute a server command.
/// </summary>
public enum PermissionLevel : byte
{
    /// <summary>Accessible to every connected player.</summary>
    Everyone = 0,

    /// <summary>Moderation commands (kick/mute).</summary>
    Moderator = 1,

    /// <summary>World manipulation and teleportation commands (<c>/tp</c>, <c>/setblock</c>).</summary>
    GameMaster = 2,

    /// <summary>Server administration commands.</summary>
    Admin = 3,

    /// <summary>Server console / root owner level.</summary>
    Owner = 4,
}
