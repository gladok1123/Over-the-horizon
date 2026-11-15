namespace OverHorizon.Networking.Packets;

/// <summary>
/// World block interaction action initiated by a connected player.
/// </summary>
public enum PlayerBlockActionKind : byte
{
    /// <summary>Destroy/break the target block to air.</summary>
    BreakBlock = 0,

    /// <summary>Place a block state at the target block position.</summary>
    PlaceBlock = 1,
}
