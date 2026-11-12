namespace OverHorizon.World.ChunkManagement;

/// <summary>
/// Categories of chunk retention tickets controlling chunk loading and unloading.
/// </summary>
public enum ChunkTicketType : byte
{
    /// <summary>Ticket anchored to a connected player's view distance.</summary>
    Player = 0,

    /// <summary>Ticket keeping the world spawn area resident.</summary>
    Spawn = 1,

    /// <summary>Ticket pinned explicitly by gameplay or commands.</summary>
    Forced = 2,
}
