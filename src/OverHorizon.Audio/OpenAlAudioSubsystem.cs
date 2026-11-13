using System.Numerics;
using OverHorizon.Core.Math;
using Silk.NET.Core.Loader;
using Silk.NET.OpenAL;

namespace OverHorizon.Audio;

/// <summary>
/// Silk.NET OpenAL 3D positional audio engine with a 32-voice pooled channel ring, O(1) buffer lookup,
/// micro-cooldown spam deduplication, and hot-reloadable <c>23w13a__or__b</c> <c>.ogg</c> sound packs.
/// </summary>
public sealed unsafe class OpenAlAudioSubsystem : IAudioSubsystem
{
    private const int VoicePoolSize = 32;
    private const int MaxCueSlots = 128;
    private const long DuplicateCueCooldownMs = 18;
    private static readonly object ResolverSyncRoot = new();

    private readonly Lock _sync = new();
    private readonly uint[] _alBuffers = new uint[MaxCueSlots];
    private readonly int[] _clipDurationsMs = new int[MaxCueSlots];
    private readonly long[] _lastCuePlayTick = new long[MaxCueSlots];
    private readonly uint[] _voiceSources = new uint[VoicePoolSize];
    private readonly long[] _voiceBusyUntilTick = new long[VoicePoolSize];
    private IReadOnlyDictionary<SoundCue, PcmSoundClip> _soundBank;

    private ALContext? _alc;
    private AL? _al;
    private Device* _device;
    private Context* _context;
    private int _roundRobinVoice;
    private long _totalSoundsTriggered;
    private float _masterGain = 1.0f;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAlAudioSubsystem"/> class.
    /// </summary>
    /// <param name="externalAssetsPath">Optional path to external <c>.ogg</c>/<c>.wav</c> assets folder.</param>
    public OpenAlAudioSubsystem(string? externalAssetsPath = null)
    {
        _soundBank = ProceduralSoundBank.LoadOrCreateAll(externalAssetsPath);
        CacheClipDurations(_soundBank);
    }

    /// <inheritdoc />
    public bool IsInitialized { get; private set; }

    /// <inheritdoc />
    public long TotalSoundsTriggered => Interlocked.Read(ref _totalSoundsTriggered);

    /// <inheritdoc />
    public Vec3d ListenerPosition { get; private set; }

    /// <inheritdoc />
    public bool TryInitializeDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            if (IsInitialized)
            {
                return true;
            }

            try
            {
                EnsureSingleFileNativeResolver();
                (_alc, _al) = AcquireOpenAlApis();

                _device = _alc.OpenDevice(string.Empty);
                if (_device is null)
                {
                    ReleaseOpenAlHandles();
                    return false;
                }

                _context = _alc.CreateContext(_device, null);
                if (_context is null || !_alc.MakeContextCurrent(_context))
                {
                    ReleaseOpenAlHandles();
                    return false;
                }

                _al.DistanceModel(DistanceModel.InverseDistanceClamped);
                UploadSoundBank(_al);
                AllocateVoicePool(_al);

                IsInitialized = true;
                UpdateListenerCore(ListenerPosition, -Vector3.UnitZ, Vector3.UnitY, _masterGain);
                return true;
            }
            catch (Exception ex) when (ex is DllNotFoundException or FileNotFoundException or TypeInitializationException or InvalidOperationException)
            {
                ReleaseOpenAlHandles();
                return false;
            }
        }
    }

    /// <inheritdoc />
    public void ReloadSoundPack(string? externalAssetsPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var reloaded = ProceduralSoundBank.LoadOrCreateAll(externalAssetsPath);
        lock (_sync)
        {
            _soundBank = reloaded;
            CacheClipDurations(reloaded);
            if (!IsInitialized || _al is null)
            {
                return;
            }

            for (var i = 0; i < _voiceSources.Length; i++)
            {
                if (_voiceSources[i] != 0)
                {
                    _al.SourceStop(_voiceSources[i]);
                    _al.SetSourceProperty(_voiceSources[i], SourceInteger.Buffer, 0);
                    _voiceBusyUntilTick[i] = 0;
                }
            }

            DeleteAllBuffers(_al);
            UploadSoundBank(_al);
        }
    }

    /// <inheritdoc />
    public void UpdateListener(Vec3d position, Vector3 forward, Vector3 up, float masterGain = 1.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            ListenerPosition = position;
            _masterGain = Math.Clamp(masterGain, 0f, 1f);

            if (!IsInitialized || _al is null)
            {
                return;
            }

            UpdateListenerCore(position, forward, up, _masterGain);
        }
    }

    /// <inheritdoc />
    public void PlayPositional(SoundCue cue, Vec3d worldPosition, float gain = 1.0f, float pitch = 1.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _ = Interlocked.Increment(ref _totalSoundsTriggered);
        var cueIndex = (int)cue;
        if ((uint)cueIndex >= MaxCueSlots)
        {
            return;
        }

        lock (_sync)
        {
            if (!IsInitialized || _al is null)
            {
                return;
            }

            var buffer = _alBuffers[cueIndex];
            if (buffer == 0)
            {
                return;
            }

            var nowTick = Environment.TickCount64;
            if (nowTick - _lastCuePlayTick[cueIndex] < DuplicateCueCooldownMs)
            {
                return;
            }

            // Camera-relative floating-point origin prevents 32-bit float precision jitter far from world origin.
            var rel = worldPosition - ListenerPosition;
            if (rel.LengthSquared > 64.0 * 64.0)
            {
                return;
            }

            _lastCuePlayTick[cueIndex] = nowTick;
            var relFloat = new Vector3((float)rel.X, (float)rel.Y, (float)rel.Z);
            var clampedPitch = Math.Clamp(pitch, 0.5f, 2.0f);
            var durationMs = (int)MathF.Ceiling(_clipDurationsMs[cueIndex] / clampedPitch);

            var voiceIdx = SelectAvailableVoiceIndex(_al, nowTick);
            var source = _voiceSources[voiceIdx];
            _voiceBusyUntilTick[voiceIdx] = nowTick + durationMs;

            _al.SourceStop(source);
            _al.SetSourceProperty(source, SourceInteger.Buffer, (int)buffer);
            _al.SetSourceProperty(source, SourceVector3.Position, in relFloat);
            _al.SetSourceProperty(source, SourceFloat.Gain, Math.Clamp(gain, 0f, 2f));
            _al.SetSourceProperty(source, SourceFloat.Pitch, clampedPitch);
            _al.SourcePlay(source);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            ReleaseOpenAlHandles();
            _disposed = true;
        }
    }

    private static void EnsureSingleFileNativeResolver()
    {
        const string markerKey = "OverHorizon.SilkSingleFileResolverRegistered";
        lock (ResolverSyncRoot)
        {
            if (AppContext.GetData(markerKey) is true || PathResolver.Default is not DefaultPathResolver resolver)
            {
                return;
            }

            var idx = resolver.Resolvers.IndexOf(DefaultPathResolver.MacVersioningResolver);
            resolver.Resolvers.Insert(idx >= 0 ? idx + 1 : 0, ResolveNativeFromAppContext);
            AppContext.SetData(markerKey, data: true);
        }
    }

    private static List<string> ResolveNativeFromAppContext(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
        {
            return [];
        }

        var fileName = Path.GetFileName(candidate);
        if (string.IsNullOrWhiteSpace(fileName)
            || AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is not string rawDirs
            || string.IsNullOrWhiteSpace(rawDirs))
        {
            return [];
        }

        var matches = new List<string>();
        foreach (var dir in rawDirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Directory.Exists))
        {
            var exact = Path.Combine(dir, fileName);
            if (File.Exists(exact))
            {
                matches.Add(exact);
            }

            if (fileName.Contains(".so", StringComparison.Ordinal))
            {
                matches.AddRange(Directory.EnumerateFiles(dir, fileName + ".*").Order(StringComparer.Ordinal));
            }
            else if (fileName.EndsWith(".dylib", StringComparison.Ordinal))
            {
                matches.AddRange(Directory.EnumerateFiles(dir, fileName[..^".dylib".Length] + ".*.dylib").Order(StringComparer.Ordinal));
            }
        }

        return matches;
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

    private void CacheClipDurations(IReadOnlyDictionary<SoundCue, PcmSoundClip> bank)
    {
        Array.Clear(_clipDurationsMs, 0, _clipDurationsMs.Length);
        foreach (var (cue, clip) in bank)
        {
            var idx = (int)cue;
            if ((uint)idx < MaxCueSlots && clip.SampleRate > 0)
            {
                _clipDurationsMs[idx] = Math.Max(20, (int)(clip.Samples.Length * 1000L / clip.SampleRate));
            }
        }
    }

    private void UpdateListenerCore(Vec3d position, Vector3 forward, Vector3 up, float masterGain)
    {
        if (_al is null)
        {
            return;
        }

        ListenerPosition = position;
        var origin = Vector3.Zero;
        _al.SetListenerProperty(ListenerVector3.Position, in origin);
        _al.SetListenerProperty(ListenerFloat.Gain, masterGain);

        Span<float> orientation =
        [
            forward.X, forward.Y, forward.Z,
            up.X, up.Y, up.Z,
        ];

        fixed (float* oriPtr = orientation)
        {
            _al.SetListenerProperty(ListenerFloatArray.Orientation, oriPtr);
        }
    }

    private void UploadSoundBank(AL al)
    {
        foreach (var (cue, clip) in _soundBank)
        {
            var idx = (int)cue;
            if ((uint)idx >= MaxCueSlots)
            {
                continue;
            }

            var buffer = al.GenBuffer();
            var span = clip.Samples.Span;
            fixed (short* samplePtr = span)
            {
                al.BufferData(buffer, BufferFormat.Mono16, samplePtr, span.Length * sizeof(short), clip.SampleRate);
            }

            _alBuffers[idx] = buffer;
        }
    }

    private void DeleteAllBuffers(AL al)
    {
        for (var i = 0; i < _alBuffers.Length; i++)
        {
            if (_alBuffers[i] != 0)
            {
                al.DeleteBuffer(_alBuffers[i]);
                _alBuffers[i] = 0;
            }
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
            _voiceBusyUntilTick[i] = 0;
        }
    }

    private int SelectAvailableVoiceIndex(AL al, long nowTick)
    {
        // Fast O(1)-amortized pure C# timestamp scan avoids up to 32 native OpenAL P/Invoke queries per sound!
        for (var i = 0; i < _voiceSources.Length; i++)
        {
            var idx = (_roundRobinVoice + i) % _voiceSources.Length;
            if (nowTick >= _voiceBusyUntilTick[idx])
            {
                _roundRobinVoice = (idx + 1) % _voiceSources.Length;
                return idx;
            }
        }

        for (var i = 0; i < _voiceSources.Length; i++)
        {
            al.GetSourceProperty(_voiceSources[i], GetSourceInteger.SourceState, out var state);
            if ((SourceState)state != SourceState.Playing)
            {
                _roundRobinVoice = (i + 1) % _voiceSources.Length;
                return i;
            }
        }

        var stolen = _roundRobinVoice;
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

            DeleteAllBuffers(_al);
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
