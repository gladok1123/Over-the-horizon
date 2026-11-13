using OverHorizon.Gameplay;

namespace OverHorizon.Client;

/// <summary>
/// Persisted metadata for a singleplayer world folder inside <c>./saves/&lt;slug&gt;/</c>.
/// </summary>
public sealed record SavedWorldMetadata(
    string Slug,
    string WorldName,
    long Seed,
    GameMode DefaultGameMode,
    long WorldTimeTicks,
    DateTimeOffset LastPlayedUtc,
    double PlayerX,
    double PlayerY,
    double PlayerZ,
    bool HasSavedPose,
    double? RespawnX = null,
    double? RespawnY = null,
    double? RespawnZ = null,
    string FolderPath = "");
