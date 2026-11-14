using System.Text.Json;

namespace OverHorizon.Client;

/// <summary>
/// Persists player client settings across game restarts in <c>./options.json</c>
/// (Render Distance, FOV, Sound Volume, Mouse Sensitivity, Invert Y, VSync, 3D Clouds, Particles, X-Ray, Username, and Server Endpoint).
/// </summary>
public sealed record PersistedClientSettings
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Gets the render distance in chunks (<c>2..64</c>).</summary>
    public int ViewDistanceChunks { get; init; } = 8;

    /// <summary>Gets the vertical field-of-view in degrees (<c>70..110</c>).</summary>
    public float FovDegrees { get; init; } = 78.0f;

    /// <summary>Gets the master audio volume (<c>0.0..1.0</c>).</summary>
    public float MasterVolume { get; init; } = 1.0f;

    /// <summary>Gets the mouse sensitivity in degrees per pixel (<c>0.03..0.25</c>).</summary>
    public float MouseSensitivity { get; init; } = 0.12f;

    /// <summary>Gets a value indicating whether vertical mouse look is inverted.</summary>
    public bool InvertMouseY { get; init; }

    /// <summary>Gets a value indicating whether VSync is enabled.</summary>
    public bool VSync { get; init; } = true;

    /// <summary>Gets a value indicating whether 3D volumetric clouds are rendered.</summary>
    public bool CloudsEnabled { get; init; } = true;

    /// <summary>Gets a value indicating whether 3D world particles are enabled.</summary>
    public bool ParticlesEnabled { get; init; } = true;

    /// <summary>Gets a value indicating whether X-Ray ore/cave inspection mode is active.</summary>
    public bool XRayEnabled { get; init; }

    /// <summary>Gets the last used multiplayer username.</summary>
    public string LastUsername { get; init; } = "Explorer";

    /// <summary>Gets the last used multiplayer server endpoint.</summary>
    public string LastServerEndpoint { get; init; } = "127.0.0.1:25565";

    /// <summary>Loads <c>options.json</c> from disk if present, otherwise returns defaults.</summary>
    public static PersistedClientSettings LoadOrDefault(string? filePath = null)
    {
        var path = filePath ?? GetDefaultPath();
        try
        {
            if (!File.Exists(path))
            {
                return new PersistedClientSettings();
            }

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<PersistedClientSettings>(json, JsonOpts);
            return loaded ?? new PersistedClientSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new PersistedClientSettings();
        }
    }

    /// <summary>Atomically writes settings to <c>options.json</c>.</summary>
    public void Save(string? filePath = null)
    {
        var path = filePath ?? GetDefaultPath();
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(path, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Non-fatal in read-only working directories.
        }
    }

    private static string GetDefaultPath() => Path.Combine(Directory.GetCurrentDirectory(), "options.json");
}
