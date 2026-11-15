using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Audio;

/// <summary>
/// Client-side 3D positional audio subsystem contract.
/// </summary>
public interface IAudioSubsystem : IDisposable
{
    /// <summary>Gets a value indicating whether an OpenAL hardware/software playback context is active.</summary>
    bool IsInitialized { get; }

    /// <summary>Gets the total number of positional sound playback requests processed (including silent fallback mode).</summary>
    long TotalSoundsTriggered { get; }

    /// <summary>Gets the most recent 64-bit world-space listener position.</summary>
    Vec3d ListenerPosition { get; }

    /// <summary>
    /// Attempts to open the default OpenAL audio device and upload synthesized sound buffers.
    /// Returns <see langword="false"/> gracefully on headless environments without audio libraries or output devices.
    /// </summary>
    /// <returns><see langword="true"/> when OpenAL playback is active; otherwise <see langword="false"/>.</returns>
    bool TryInitializeDevice();

    /// <summary>
    /// Updates the 3D listener pose and master gain.
    /// </summary>
    /// <param name="position">World-space 64-bit listener position.</param>
    /// <param name="forward">Unit forward vector.</param>
    /// <param name="up">Unit up vector.</param>
    /// <param name="masterGain">Master listener gain in <c>[0, 1]</c>.</param>
    void UpdateListener(Vec3d position, Vector3 forward, Vector3 up, float masterGain = 1.0f);

    /// <summary>
    /// Plays <paramref name="cue"/> attenuated from <paramref name="worldPosition"/> relative to <see cref="ListenerPosition"/>.
    /// </summary>
    /// <param name="cue">Sound effect identifier.</param>
    /// <param name="worldPosition">World-space 64-bit sound origin.</param>
    /// <param name="gain">Source gain multiplier.</param>
    /// <param name="pitch">Source pitch multiplier.</param>
    void PlayPositional(SoundCue cue, Vec3d worldPosition, float gain = 1.0f, float pitch = 1.0f);

    /// <summary>
    /// Hot-reloads the sound bank from <paramref name="externalAssetsPath"/> when new <c>.ogg</c> files become available.
    /// </summary>
    /// <param name="externalAssetsPath">Optional path to external <c>23w13a__or__b</c> resource directory.</param>
    void ReloadSoundPack(string? externalAssetsPath)
    {
    }
}
