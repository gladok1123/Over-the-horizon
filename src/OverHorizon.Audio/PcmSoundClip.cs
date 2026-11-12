namespace OverHorizon.Audio;

/// <summary>
/// Immutable 16-bit signed mono PCM waveform clip.
/// </summary>
/// <param name="Cue">Associated sound cue identifier.</param>
/// <param name="SampleRate">Sample rate in Hz.</param>
/// <param name="Samples">Signed 16-bit mono PCM samples.</param>
public sealed record PcmSoundClip(
    SoundCue Cue,
    int SampleRate,
    ReadOnlyMemory<short> Samples)
{
    /// <summary>Gets the playback duration of the clip.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}
