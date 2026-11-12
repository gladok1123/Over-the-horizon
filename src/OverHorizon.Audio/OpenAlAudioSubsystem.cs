using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Core.Math;
using Silk.NET.OpenAL;

namespace OverHorizon.Audio;

/// <summary>
/// Silk.NET OpenAL 3D positional audio subsystem with clean-room procedural PCM buffers,
/// voice pooling, camera-relative anti-jitter source coordinates, and graceful headless fallback.
/// </summary>
public sealed unsafe class OpenAlAudioSubsystem : IAudioSubsystem
{
    private const int VoicePoolSize = 8;
    private const double MaxAudibleDistanceSq = 48.0 * 48.0;

    private readonly ILogger<OpenAlAudioSubsystem> _logger;
    private readonly IReadOnlyDictionary<SoundCue, PcmSoundClip> _soundBank;
    private readonly Dictionary<SoundCue, uint> _alBuffers = [];
    private readonly uint[] _voiceSources = new uint[VoicePoolSize];

    private ALContext? _alc;
    private AL? _al;
    private Device* _device;
    private Context* _context;
    private int _roundRobinVoice;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAlAudioSubsystem"/> class.
    /// </summary>
    /// <param name="logger">Structured logger.</param>
    public OpenAlAudioSubsystem(ILogger<OpenAlAudioSubsystem> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _soundBank = ProceduralSoundBank.CreateDefaultBank();
    }

    /// <inheritdoc />
    public bool IsInitialized { get; private set; }

    /// <inheritdoc />
    public long TotalSoundsTriggered { get; private set; }

    /// <inheritdoc />
    public Vec3d ListenerPosition { get; private set; }

    /// <summary>Gets the synthesized PCM clips managed by this subsystem.</summary>
    public IReadOnlyDictionary<SoundCue, PcmSoundClip> SoundBank => _soundBank;

    /// <inheritdoc />
    public bool TryInitializeDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsInitialized)
        {
            return true;
        }

        try
        {
            var (alc, al) = AcquireOpenAlApis();

            var device = alc.OpenDevice(string.Empty);
            if (device is null)
            {
                al.Dispose();
                alc.Dispose();
                AudioLog.OpenAlFallback(_logger, "OpenDevice returned null");
                return false;
            }

            var context = alc.CreateContext(device, null);
            if (context is null || !alc.MakeContextCurrent(context))
            {
                if (context is not null)
                {
                    alc.DestroyContext(context);
                }

                alc.CloseDevice(device);
                al.Dispose();
                alc.Dispose();
                AudioLog.OpenAlFallback(_logger, "CreateContext/MakeContextCurrent failed");
                return false;
            }

            _alc = alc;
            _al = al;
            _device = device;
            _context = context;

            al.GetError();
            al.DistanceModel(DistanceModel.InverseDistanceClamped);
            al.SetListenerProperty(ListenerVector3.Position, 0.0f, 0.0f, 0.0f);

            UploadSoundBank(al);
            AllocateVoicePool(al);

            IsInitialized = true;
            AudioLog.OpenAlInitialized(_logger, _alBuffers.Count, _voiceSources.Length);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReleaseOpenAlHandles();
            AudioLog.OpenAlFallback(_logger, ex.GetType().Name);
            return false;
        }
    }

    /// <inheritdoc />
    public void UpdateListener(Vec3d position, Vector3 forward, Vector3 up, float masterGain = 1.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ListenerPosition = position;

        if (!IsInitialized || _al is null)
        {
            return;
        }

        var clampedGain = Math.Clamp(masterGain, 0.0f, 1.0f);
        _al.SetListenerProperty(ListenerFloat.Gain, clampedGain);

        Span<float> orientation =
        [
            forward.X, forward.Y, forward.Z,
            up.X, up.Y, up.Z,
        ];

        fixed (float* ptr = orientation)
        {
            _al.SetListenerProperty(ListenerFloatArray.Orientation, ptr);
        }
    }

    /// <inheritdoc />
    public void PlayPositional(SoundCue cue, Vec3d worldPosition, float gain = 1.0f, float pitch = 1.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var dx = worldPosition.X - ListenerPosition.X;
        var dy = worldPosition.Y - ListenerPosition.Y;
        var dz = worldPosition.Z - ListenerPosition.Z;
        var distSq = (dx * dx) + (dy * dy) + (dz * dz);
        if (distSq > MaxAudibleDistanceSq)
        {
            return;
        }

        TotalSoundsTriggered++;

        if (!IsInitialized || _al is null || !_alBuffers.TryGetValue(cue, out var bufferId))
        {
            return;
        }

        var source = SelectAvailableVoice(_al);
        _al.SourceStop(source);
        _al.SetSourceProperty(source, SourceInteger.Buffer, bufferId);
        _al.SetSourceProperty(source, SourceVector3.Position, (float)dx, (float)dy, (float)dz);
        _al.SetSourceProperty(source, SourceFloat.Gain, Math.Clamp(gain, 0.0f, 2.0f));
        _al.SetSourceProperty(source, SourceFloat.Pitch, Math.Clamp(pitch, 0.5f, 2.0f));
        _al.SourcePlay(source);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseOpenAlHandles();
        _disposed = true;
    }

    private static (ALContext Alc, AL Al) AcquireOpenAlApis()
    {
        try
        {
            return (ALContext.GetApi(soft: true), AL.GetApi(soft: true));
        }
        catch (FileNotFoundException)
        {
            return (ALContext.GetApi(soft: false), AL.GetApi(soft: false));
        }
    }

    private void UploadSoundBank(AL al)
    {
        foreach (var (cue, clip) in _soundBank)
        {
            var buffer = al.GenBuffer();
            var span = clip.Samples.Span;
            fixed (short* samplePtr = span)
            {
                al.BufferData(buffer, BufferFormat.Mono16, samplePtr, span.Length * sizeof(short), clip.SampleRate);
            }

            _alBuffers[cue] = buffer;
        }
    }

    private void AllocateVoicePool(AL al)
    {
        for (var i = 0; i < _voiceSources.Length; i++)
        {
            var src = al.GenSource();
            al.SetSourceProperty(src, SourceFloat.ReferenceDistance, 2.0f);
            al.SetSourceProperty(src, SourceFloat.MaxDistance, 48.0f);
            al.SetSourceProperty(src, SourceFloat.RolloffFactor, 1.0f);
            al.SetSourceProperty(src, SourceBoolean.Looping, false);
            _voiceSources[i] = src;
        }
    }

    private uint SelectAvailableVoice(AL al)
    {
        for (var i = 0; i < _voiceSources.Length; i++)
        {
            var candidate = _voiceSources[i];
            al.GetSourceProperty(candidate, GetSourceInteger.SourceState, out var state);
            if ((SourceState)state != SourceState.Playing)
            {
                return candidate;
            }
        }

        var stolen = _voiceSources[_roundRobinVoice];
        _roundRobinVoice = (_roundRobinVoice + 1) % _voiceSources.Length;
        return stolen;
    }

    private void ReleaseOpenAlHandles()
    {
        IsInitialized = false;

        if (_al is not null)
        {
            for (var i = 0; i < _voiceSources.Length; i++)
            {
                if (_voiceSources[i] != 0)
                {
                    _al.SourceStop(_voiceSources[i]);
                    _al.DeleteSource(_voiceSources[i]);
                    _voiceSources[i] = 0;
                }
            }

            foreach (var buf in _alBuffers.Values)
            {
                if (buf != 0)
                {
                    _al.DeleteBuffer(buf);
                }
            }

            _alBuffers.Clear();
        }

        if (_alc is not null)
        {
            if (_context is not null)
            {
                _alc.MakeContextCurrent(null);
                _alc.DestroyContext(_context);
                _context = null;
            }

            if (_device is not null)
            {
                _alc.CloseDevice(_device);
                _device = null;
            }
        }

        _al?.Dispose();
        _al = null;
        _alc?.Dispose();
        _alc = null;
    }
}
