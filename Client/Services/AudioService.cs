using System.Buffers.Binary;
using Client.Audio;
using Raylib_cs;

namespace Client.Services;

/// <summary>
/// TASK-031 – Audio System (Task 9.1).
///
/// Manages:
///   • Audio device lifecycle (InitAudioDevice / CloseAudioDevice)
///   • Procedural placeholder sounds (generated PCM WAV – no external files needed)
///   • Background music with zone-based track switching
///   • 3-D spatial audio (quadratic volume falloff + stereo pan) as per roadmap
///
/// Call <see cref="Initialize"/> once after <c>Raylib.InitWindow()</c>.
/// Call <see cref="Update"/> once per frame (pumps the active music stream).
/// Call <see cref="Dispose"/> at shutdown.
/// </summary>
public sealed class AudioService : IDisposable
{
    // ── Spatial audio constants ────────────────────────────────────────────
    private const int   MaxDistanceTiles = 15;   // tiles beyond which sounds are inaudible

    // ── Volume state ──────────────────────────────────────────────────────
    public float MasterVolume { get; private set; } = 0.8f;
    public float SfxVolume   { get; private set; } = 1.0f;
    public float MusicVolume { get; private set; } = 0.4f;

    // ── Internal state ────────────────────────────────────────────────────
    private readonly Dictionary<SoundId, Sound> _sounds = new();
    private          Music[]                    _musicTracks = [];
    private          int                        _currentZone  = -1;
    private          bool                       _initialized;
    private          bool                       _disposed;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes the audio device and generates procedural placeholder sounds.
    /// Must be called after <c>Raylib.InitWindow()</c>.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Raylib.InitAudioDevice();

        // ── Sound effects (frequency Hz, duration s, amplitude) ───────────
        AddSound(SoundId.HitMelee,        frequency: 300f,  duration: 0.12f, amplitude: 0.60f);
        AddSound(SoundId.HitMiss,         frequency: 180f,  duration: 0.06f, amplitude: 0.40f);
        AddSound(SoundId.HitSpell,        frequency: 880f,  duration: 0.25f, amplitude: 0.50f);
        AddSound(SoundId.UiClick,         frequency: 1200f, duration: 0.04f, amplitude: 0.30f);
        AddSound(SoundId.UiOpenContainer, frequency: 480f,  duration: 0.10f, amplitude: 0.40f);
        AddSound(SoundId.UiEquip,         frequency: 620f,  duration: 0.15f, amplitude: 0.50f);
        AddSound(SoundId.AmbientWater,    frequency: 200f,  duration: 0.50f, amplitude: 0.25f);
        AddSound(SoundId.AmbientWind,     frequency: 350f,  duration: 0.40f, amplitude: 0.20f);
        AddSound(SoundId.AmbientFire,     frequency: 230f,  duration: 0.45f, amplitude: 0.30f);
        AddSound(SoundId.CreatureGrowl,   frequency: 120f,  duration: 0.35f, amplitude: 0.55f);

        // ── Background music tracks (one per zone) ────────────────────────
        _musicTracks = new Music[2];
        _musicTracks[0] = CreateMusicTrack(baseFreq: 220f, durationSec: 12f);
        _musicTracks[1] = CreateMusicTrack(baseFreq: 165f, durationSec: 15f);

        SetZone(0);
    }

    // ── Public sound API ──────────────────────────────────────────────────

    /// <summary>Plays a sound effect at full volume in the center channel (non-spatial).</summary>
    public void PlaySfx(SoundId id)
    {
        if (!_initialized || !_sounds.TryGetValue(id, out var sound)) return;
        Raylib.SetSoundVolume(sound, SfxVolume * MasterVolume);
        Raylib.SetSoundPan(sound, 0.5f); // center pan in Raylib [0=left, 0.5=center, 1=right]
        Raylib.PlaySound(sound);
    }

    /// <summary>
    /// Plays a sound effect with 3-D spatial audio as defined in the roadmap.
    ///
    /// Volume:
    ///   distance = Euclidean tile distance(source, listener)
    ///   if distance &gt; MaxDistance: silent
    ///   volume = (1 - distance/MaxDistance)²  (quadratic falloff)
    ///
    /// Pan:
    ///   dx = source.x - listener.x
    ///   pan = clamp(dx / MaxDistance, -1, 1)  → remapped to Raylib [0..1]
    /// </summary>
    public void PlaySpatial(SoundId id,
                            int sourceTileX,   int sourceTileY,
                            int listenerTileX, int listenerTileY)
    {
        if (!_initialized || !_sounds.TryGetValue(id, out var sound)) return;

        float dx       = sourceTileX - listenerTileX;
        float dy       = sourceTileY - listenerTileY;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance > MaxDistanceTiles) return;

        float t      = distance / MaxDistanceTiles;
        float volume = (1f - t) * (1f - t);                         // quadratic falloff

        float pan       = Math.Clamp(dx / MaxDistanceTiles, -1f, 1f);
        float raylibPan = (pan + 1f) * 0.5f;                        // [-1,1] → [0,1]

        Raylib.SetSoundVolume(sound, volume * SfxVolume * MasterVolume);
        Raylib.SetSoundPan(sound, raylibPan);
        Raylib.PlaySound(sound);
    }

    // ── Music zone management ─────────────────────────────────────────────

    /// <summary>
    /// Switches to the background music track for the given zone index.
    /// No-op if the zone is already active.
    /// </summary>
    public void SetZone(int zone)
    {
        if (!_initialized || zone == _currentZone) return;

        // Stop the current track
        if (_currentZone >= 0 && _currentZone < _musicTracks.Length)
        {
            if (Raylib.IsMusicStreamPlaying(_musicTracks[_currentZone]))
                Raylib.StopMusicStream(_musicTracks[_currentZone]);
        }

        _currentZone = zone;

        if (zone >= 0 && zone < _musicTracks.Length)
        {
            Raylib.SetMusicVolume(_musicTracks[zone], MusicVolume * MasterVolume);
            Raylib.PlayMusicStream(_musicTracks[zone]);
        }
    }

    /// <summary>
    /// Must be called once per frame to stream music data.
    /// Restarts the current track automatically when it finishes.
    /// </summary>
    public void Update()
    {
        if (!_initialized || _currentZone < 0 || _currentZone >= _musicTracks.Length) return;

        ref var track = ref _musicTracks[_currentZone];
        if (Raylib.IsMusicValid(track))
        {
            Raylib.UpdateMusicStream(track);

            // Restart if playback ended (looping through re-play)
            if (!Raylib.IsMusicStreamPlaying(track))
            {
                Raylib.SetMusicVolume(track, MusicVolume * MasterVolume);
                Raylib.PlayMusicStream(track);
            }
        }
    }

    // ── Volume control ────────────────────────────────────────────────────

    public void SetMasterVolume(float v)
    {
        MasterVolume = Math.Clamp(v, 0f, 1f);
        RefreshMusicVolume();
    }

    public void SetMusicVolume(float v)
    {
        MusicVolume = Math.Clamp(v, 0f, 1f);
        RefreshMusicVolume();
    }

    public void SetSfxVolume(float v) => SfxVolume = Math.Clamp(v, 0f, 1f);

    private void RefreshMusicVolume()
    {
        if (!_initialized || _currentZone < 0 || _currentZone >= _musicTracks.Length) return;
        if (Raylib.IsMusicValid(_musicTracks[_currentZone]))
            Raylib.SetMusicVolume(_musicTracks[_currentZone], MusicVolume * MasterVolume);
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed || !_initialized) return;
        _disposed = true;

        foreach (var s in _sounds.Values)
            Raylib.UnloadSound(s);
        _sounds.Clear();

        foreach (var m in _musicTracks)
            if (Raylib.IsMusicValid(m))
                Raylib.UnloadMusicStream(m);

        Raylib.CloseAudioDevice();
    }

    // ── Procedural WAV generation ─────────────────────────────────────────

    /// <summary>
    /// Generates and registers a sound from a synthesised sine-wave tone.
    /// </summary>
    private void AddSound(SoundId id, float frequency, float duration, float amplitude,
                          int sampleRate = 22050)
    {
        byte[] wavData = BuildWav(frequency, duration, amplitude, sampleRate);
        var    wave    = Raylib.LoadWaveFromMemory(".wav", wavData);
        if (!Raylib.IsWaveValid(wave)) return;
        var sound = Raylib.LoadSoundFromWave(wave);
        Raylib.UnloadWave(wave);
        _sounds[id] = sound;
    }

    /// <summary>
    /// Generates a looping ambient music WAV from layered sine harmonics.
    /// </summary>
    private static Music CreateMusicTrack(float baseFreq, float durationSec,
                                          int sampleRate = 22050)
    {
        byte[] wavData = BuildMusicWav(baseFreq, durationSec, sampleRate);
        var    music   = Raylib.LoadMusicStreamFromMemory(".wav", wavData);
        music.Looping  = true;
        return music;
    }

    /// <summary>
    /// Builds a mono 16-bit PCM WAV byte array with a sine-wave tone.
    /// A 20 % linear fade-out is applied at the end to avoid clicks.
    /// </summary>
    private static byte[] BuildWav(float frequency, float durationSec, float amplitude,
                                   int sampleRate)
    {
        int    numSamples = (int)(sampleRate * durationSec);
        byte[] data       = new byte[44 + numSamples * 2];

        WriteWavHeader(data, numSamples, sampleRate);

        float fadeStart = durationSec * 0.80f;

        for (int i = 0; i < numSamples; i++)
        {
            float t      = (float)i / sampleRate;
            float sample = amplitude * MathF.Sin(2f * MathF.PI * frequency * t);

            if (t > fadeStart)
                sample *= (durationSec - t) / (durationSec - fadeStart);

            short s = (short)Math.Clamp((int)(sample * short.MaxValue),
                                        short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(44 + i * 2), s);
        }

        return data;
    }

    /// <summary>
    /// Builds a longer ambient music WAV from stacked sine harmonics.
    /// </summary>
    private static byte[] BuildMusicWav(float baseFreq, float durationSec, int sampleRate)
    {
        int    numSamples = (int)(sampleRate * durationSec);
        byte[] data       = new byte[44 + numSamples * 2];

        WriteWavHeader(data, numSamples, sampleRate);

        // Four harmonic partials: fundamental + 5th + octave + 2nd octave
        ReadOnlySpan<float> hMults = [1f, 1.5f, 2f, 4f];
        ReadOnlySpan<float> hAmps  = [0.20f, 0.12f, 0.08f, 0.04f];

        for (int i = 0; i < numSamples; i++)
        {
            float t      = (float)i / sampleRate;
            float sample = 0f;
            for (int h = 0; h < hMults.Length; h++)
                sample += hAmps[h] * MathF.Sin(2f * MathF.PI * baseFreq * hMults[h] * t);

            short s = (short)Math.Clamp((int)(sample * short.MaxValue),
                                        short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(44 + i * 2), s);
        }

        return data;
    }

    /// <summary>
    /// Writes a standard 44-byte PCM WAV header into <paramref name="data"/>.
    /// Format: mono, 16-bit, <paramref name="sampleRate"/> Hz.
    /// </summary>
    private static void WriteWavHeader(byte[] data, int numSamples, int sampleRate)
    {
        // RIFF chunk descriptor
        data[0] = 0x52; data[1] = 0x49; data[2] = 0x46; data[3] = 0x46; // "RIFF"
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 36 + numSamples * 2);
        data[8] = 0x57; data[9] = 0x41; data[10] = 0x56; data[11] = 0x45; // "WAVE"

        // fmt sub-chunk
        data[12] = 0x66; data[13] = 0x6D; data[14] = 0x74; data[15] = 0x20; // "fmt "
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16), 16);         // sub-chunk size
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20), 1);          // PCM
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(22), 1);          // mono
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(28), sampleRate * 2); // byte rate
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(32), 2);          // block align
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(34), 16);         // bits/sample

        // data sub-chunk
        data[36] = 0x64; data[37] = 0x61; data[38] = 0x74; data[39] = 0x61; // "data"
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(40), numSamples * 2);
    }
}
