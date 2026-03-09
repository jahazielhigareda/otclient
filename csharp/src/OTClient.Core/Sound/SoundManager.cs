using System.Text.Json;
using Raylib_cs;

namespace OTClient.Framework.Sound;

// ─── T29 soundbank data types ─────────────────────────────────────────────────

/// <summary>
/// Maps to <c>ENumericSoundType</c> in <c>soundmanager.h</c>.
/// Describes what game action or context a sound effect is associated with.
/// </summary>
public enum ClientSoundType
{
    Unknown                   = 0,
    SpellAttack               = 1,
    SpellHealing              = 2,
    SpellSupport              = 3,
    WeaponAttack              = 4,
    CreatureNoise             = 5,
    CreatureDeath             = 6,
    CreatureAttack            = 7,
    AmbienceStream            = 8,
    FoodAndDrink              = 9,
    ItemMovement              = 10,
    Event                     = 11,
    Ui                        = 12,
    WhisperWithoutOpenChat    = 13,
    ChatMessage               = 14,
    Party                     = 15,
    VipList                   = 16,
    RaidAnnouncement          = 17,
    ServerMessage             = 18,
    SpellGeneric              = 19,
}

/// <summary>
/// Maps to <c>EMusicType</c> in <c>soundmanager.h</c>.
/// </summary>
public enum ClientMusicType
{
    Unknown        = 0,
    Music          = 1,
    MusicImmediate = 2,
    MusicTitle     = 3,
}

/// <summary>
/// One numeric sound effect deserialized from the protobuf soundbank.
/// Maps to <c>ClientSoundEffect</c> struct in <c>soundmanager.h</c>.
/// </summary>
public sealed class ClientSoundEffect
{
    public uint              Id            { get; init; }
    public ClientSoundType   Type          { get; init; }
    public float             PitchMin      { get; init; }
    public float             PitchMax      { get; init; }
    public float             VolumeMin     { get; init; }
    public float             VolumeMax     { get; init; }
    /// <summary>Audio file id for simple (single) sound effects; 0 when unused.</summary>
    public uint              SoundId       { get; init; }
    /// <summary>Candidate audio file ids for random-selection sound effects.</summary>
    public IReadOnlyList<uint> RandomSoundIds { get; init; } = [];
}

/// <summary>
/// A delayed sound-effect pair: (effectId, delaySeconds).
/// Maps to <c>DelayedSoundEffect = std::pair&lt;uint32,uint32&gt;</c>.
/// </summary>
public readonly record struct DelayedSoundEffect(uint EffectId, uint DelaySeconds);

/// <summary>
/// Location ambient deserialized from the soundbank.
/// Maps to <c>ClientLocationAmbient</c> in <c>soundmanager.h</c>.
/// </summary>
public sealed class ClientLocationAmbient
{
    public uint                          Id                  { get; init; }
    public uint                          LoopedAudioFileId   { get; init; }
    public IReadOnlyList<DelayedSoundEffect> DelayedEffects  { get; init; } = [];
}

/// <summary>
/// One (loopedSoundId, requiredCount) pair for item-ambient sound effects.
/// </summary>
public readonly record struct ItemCountSoundEffect(uint LoopedSoundId, uint RequiredCount);

/// <summary>
/// Item-ambient effect deserialized from the soundbank.
/// Maps to <c>ClientItemAmbient</c> in <c>soundmanager.h</c>.
/// </summary>
public sealed class ClientItemAmbient
{
    public uint                             Id                   { get; init; }
    public IReadOnlyList<uint>              ClientIds            { get; init; } = [];
    public IReadOnlyList<ItemCountSoundEffect> ItemCountEffects  { get; init; } = [];
}

/// <summary>
/// Music track deserialized from the soundbank.
/// Maps to <c>ClientMusic</c> in <c>soundmanager.h</c>.
/// </summary>
public sealed class ClientMusic
{
    public uint            Id            { get; init; }
    public uint            AudioFileId   { get; init; }
    public ClientMusicType MusicType     { get; init; }
}

/// <summary>
/// Central audio manager: initialises the Raylib audio device, owns all
/// <see cref="SoundChannel"/>s, and drives <see cref="MusicSource"/> stream
/// updates each frame.
/// <para>
/// Maps to <c>SoundManager</c> in <c>src/framework/sound/soundmanager.*</c>.
/// Call <see cref="Init"/> once (from <see cref="OTClient.Framework.Core.Application.Init"/>),
/// then <see cref="Update"/> every frame (from the main loop), then
/// <see cref="Terminate"/> on shutdown.
/// </para>
/// Lua surface: <c>g_sounds</c>
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly Dictionary<string, SoundChannel> _channels = [];
    private readonly List<MusicSource> _streams = [];
    private readonly object _lock = new();
    private float _masterVolume = 1.0f;
    private bool _initialised;
    private bool _disposed;

    // ─── T29: Soundbank data ──────────────────────────────────────────────────
    private readonly Dictionary<uint, string>               _audioFiles     = [];
    private readonly Dictionary<uint, ClientSoundEffect>    _soundEffects   = [];
    private readonly Dictionary<uint, ClientLocationAmbient>_locationAmbients = [];
    private readonly Dictionary<uint, ClientItemAmbient>    _itemAmbients   = [];
    private readonly Dictionary<uint, ClientMusic>          _musicTracks    = [];

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the Raylib audio device and applies the current
    /// <see cref="MasterVolume"/>.  Safe to call from
    /// <see cref="OTClient.Framework.Core.Application.Init"/>.
    /// </summary>
    public void Init()
    {
        if (_initialised) return;
        Raylib.InitAudioDevice();
        if (Raylib.IsAudioDeviceReady())
            Raylib.SetMasterVolume(_masterVolume);
        _initialised = true;
    }

    /// <summary>
    /// Stops all sounds, clears channels, and closes the Raylib audio device.
    /// Called automatically by <see cref="OTClient.Framework.Core.Application.Terminate"/>.
    /// </summary>
    public void Terminate()
    {
        lock (_lock)
        {
            foreach (var ch in _channels.Values)
                ch.StopAll();
            _channels.Clear();
            _streams.Clear();
        }
        if (_initialised)
        {
            if (Raylib.IsAudioDeviceReady())
                Raylib.CloseAudioDevice();
            _initialised = false;
        }
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary><c>true</c> when the audio device is open and ready.</summary>
    public bool IsReady => Raylib.IsAudioDeviceReady();

    /// <summary>
    /// Master volume in [0.0, 1.0].  Changing this value calls
    /// <c>Raylib.SetMasterVolume</c> immediately when the device is ready.
    /// <para>Lua: <c>g_sounds:setMasterVolume(vol)</c></para>
    /// </summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            if (Raylib.IsAudioDeviceReady())
                Raylib.SetMasterVolume(_masterVolume);
        }
    }

    // ─── Channel management ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the named channel, creating it if it does not yet exist.
    /// <para>Lua: <c>g_sounds:getChannel(name)</c></para>
    /// </summary>
    public SoundChannel GetChannel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_lock)
        {
            if (!_channels.TryGetValue(name, out var ch))
            {
                ch = new SoundChannel(name);
                _channels[name] = ch;
            }
            return ch;
        }
    }

    /// <summary>
    /// Sets the volume of the named channel (creates it if needed).
    /// <para>Lua: <c>g_sounds:setVolume(channelName, vol)</c></para>
    /// </summary>
    public void SetChannelVolume(string name, float volume)
        => GetChannel(name).Volume = volume;

    /// <summary>Snapshot of all registered channels.</summary>
    public IReadOnlyList<SoundChannel> Channels
    {
        get { lock (_lock) return [.. _channels.Values]; }
    }

    // ─── Playback helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="SoundSource"/>, loads the audio file, optionally
    /// assigns it to the named channel, starts playback, and returns it.
    /// OGG Vorbis, WAV, and MP3 are all supported natively by Raylib.
    /// <para>Lua: <c>g_sounds:play(file, channel)</c></para>
    /// </summary>
    public SoundSource Play(string filePath, string? channelName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var src = new SoundSource(channelName is not null ? GetChannel(channelName) : null);
        src.Load(filePath);
        src.Play();
        return src;
    }

    /// <summary>
    /// Creates a <see cref="MusicSource"/>, loads the stream, optionally assigns
    /// it to the named channel, and registers it for automatic
    /// <see cref="MusicSource.Update"/> calls each frame.
    /// <para>Lua: <c>g_sounds:createMusic(file, channel)</c></para>
    /// </summary>
    public MusicSource CreateMusic(string filePath, string? channelName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var src = new MusicSource(channelName is not null ? GetChannel(channelName) : null, this);
        src.Load(filePath);
        return src;
    }

    // ─── Per-frame update ─────────────────────────────────────────────────────

    /// <summary>
    /// Feeds the decode buffer for all active streaming <see cref="MusicSource"/>s.
    /// Must be called once per frame from the main loop.
    /// </summary>
    public void Update()
    {
        MusicSource[] snapshot;
        lock (_lock) snapshot = [.. _streams];
        foreach (var s in snapshot)
            s.Update();
    }

    // ─── Internal stream registration (called by MusicSource) ─────────────────

    internal void RegisterStream(MusicSource source)
    {
        lock (_lock)
        {
            if (!_streams.Contains(source))
                _streams.Add(source);
        }
    }

    internal void UnregisterStream(MusicSource source)
    {
        lock (_lock) _streams.Remove(source);
    }

    // ─── T29: Soundbank loading ───────────────────────────────────────────────

    /// <summary>
    /// <c>true</c> once <see cref="LoadFromProtobuf"/> has successfully parsed
    /// a soundbank binary.
    /// </summary>
    public bool IsSoundbankLoaded { get; private set; }

    /// <summary>
    /// Reads the <c>catalog-sound.json</c> file at <paramref name="directory"/>,
    /// locates the protobuf soundbank entry of type <c>"sounds"</c>, and
    /// delegates to <see cref="LoadFromProtobuf"/>.
    /// <para>
    /// Maps to <c>SoundManager::loadClientFiles</c> in <c>soundmanager.cpp</c>.
    /// Task T29.
    /// </para>
    /// </summary>
    /// <param name="directory">Path prefix used to resolve both the catalog and binary files.</param>
    /// <param name="readText">Delegate that returns the text contents of a file path (e.g. catalog JSON).</param>
    /// <param name="readBinary">Delegate that returns the raw bytes of a file path (e.g. soundbank .dat).</param>
    /// <returns><c>true</c> when at least one soundbank was loaded.</returns>
    public bool LoadClientFiles(string directory,
                                Func<string, string>  readText,
                                Func<string, byte[]>  readBinary)
    {
        ArgumentNullException.ThrowIfNull(readText);
        ArgumentNullException.ThrowIfNull(readBinary);

        try
        {
            string catalogPath = directory.TrimEnd('/') + "/catalog-sound.json";
            string json        = readText(catalogPath);
            using var doc      = JsonDocument.Parse(json);
            bool any = false;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("type", out var typeProp)) continue;
                if (typeProp.GetString() != "sounds")               continue;
                if (!element.TryGetProperty("file", out var fileProp)) continue;

                string filePath = directory.TrimEnd('/') + "/" + fileProp.GetString();
                byte[] data = readBinary(filePath);
                if (LoadFromProtobuf(data))
                    any = true;
            }
            return any;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a protobuf soundbank binary and populates the internal dictionaries.
    /// <para>
    /// At protocol version 1281 / client ≥ 13 the binary follows the
    /// <c>sounds.Sounds</c> protobuf schema. This implementation uses a
    /// hand-rolled protobuf wire-format reader (no external library) to
    /// deserialize the five top-level repeated fields: <c>sound</c> (field 1),
    /// <c>numeric_sound_effect</c> (field 2), <c>ambience_stream</c> (field 3),
    /// <c>ambience_object_stream</c> (field 4), <c>music_template</c> (field 5).
    /// </para>
    /// <para>
    /// Maps to <c>SoundManager::loadFromProtobuf</c> in <c>soundmanager.cpp</c>.
    /// Task T29.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> on success, <c>false</c> when the data is empty or cannot be parsed.</returns>
    public bool LoadFromProtobuf(byte[] data)
    {
        if (data is null || data.Length == 0) return false;
        try
        {
            ParseSoundsProtobuf(data);
            IsSoundbankLoaded = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ─── T29: Soundbank accessors ─────────────────────────────────────────────

    /// <summary>
    /// Returns the OGG filename for <paramref name="audioFileId"/>, or
    /// <see cref="string.Empty"/> when not found.
    /// Maps to <c>SoundManager::getAudioFileNameById</c>.
    /// </summary>
    public string GetAudioFileNameById(uint audioFileId)
        => _audioFiles.TryGetValue(audioFileId, out var n) ? n : string.Empty;

    /// <summary>Returns the <see cref="ClientSoundEffect"/> for the given id, or <c>null</c>.</summary>
    public ClientSoundEffect? TryGetSoundEffect(uint id)
        => _soundEffects.TryGetValue(id, out var v) ? v : null;

    /// <summary>Returns the <see cref="ClientLocationAmbient"/> for the given id, or <c>null</c>.</summary>
    public ClientLocationAmbient? TryGetLocationAmbient(uint id)
        => _locationAmbients.TryGetValue(id, out var v) ? v : null;

    /// <summary>Returns the <see cref="ClientItemAmbient"/> for the given id, or <c>null</c>.</summary>
    public ClientItemAmbient? TryGetItemAmbient(uint id)
        => _itemAmbients.TryGetValue(id, out var v) ? v : null;

    /// <summary>Returns the <see cref="ClientMusic"/> for the given id, or <c>null</c>.</summary>
    public ClientMusic? TryGetMusic(uint id)
        => _musicTracks.TryGetValue(id, out var v) ? v : null;

    /// <summary>Count of loaded audio file name mappings.</summary>
    public int AudioFileCount     => _audioFiles.Count;
    /// <summary>Count of loaded sound effects.</summary>
    public int SoundEffectCount   => _soundEffects.Count;
    /// <summary>Count of loaded location ambient effects.</summary>
    public int LocationAmbientCount => _locationAmbients.Count;
    /// <summary>Count of loaded item ambient effects.</summary>
    public int ItemAmbientCount   => _itemAmbients.Count;
    /// <summary>Count of loaded music tracks.</summary>
    public int MusicTrackCount    => _musicTracks.Count;

    // ─── T29: Protobuf wire-format parser ─────────────────────────────────────

    /// <summary>
    /// Minimal hand-rolled protobuf wire-format reader for the <c>sounds.Sounds</c>
    /// message. Does not require any external library.
    /// <para>
    /// Wire types: 0 = varint, 1 = 64-bit, 2 = length-delimited, 5 = 32-bit.
    /// Field numbers match the proto definition documented in
    /// <c>soundmanager.cpp</c>:
    ///   1 = sound, 2 = numeric_sound_effect, 3 = ambience_stream,
    ///   4 = ambience_object_stream, 5 = music_template.
    /// </para>
    /// </summary>
    private void ParseSoundsProtobuf(byte[] data)
    {
        int pos = 0;

        while (pos < data.Length)
        {
            ulong tag  = ReadVarint(data, ref pos);
            int   field = (int)(tag >> 3);
            int   wire  = (int)(tag & 0x7);

            switch (field)
            {
                case 1: // sound: id(1/varint), filename(2/string), original_filename(3/string), is_stream(4/varint)
                {
                    byte[] msg = ReadLengthDelimited(data, ref pos);
                    ParseSoundEntry(msg);
                    break;
                }
                case 2: // numeric_sound_effect
                {
                    byte[] msg = ReadLengthDelimited(data, ref pos);
                    ParseSoundEffect(msg);
                    break;
                }
                case 3: // ambience_stream
                {
                    byte[] msg = ReadLengthDelimited(data, ref pos);
                    ParseLocationAmbient(msg);
                    break;
                }
                case 4: // ambience_object_stream
                {
                    byte[] msg = ReadLengthDelimited(data, ref pos);
                    ParseItemAmbient(msg);
                    break;
                }
                case 5: // music_template
                {
                    byte[] msg = ReadLengthDelimited(data, ref pos);
                    ParseMusicTemplate(msg);
                    break;
                }
                default:
                    SkipField(data, ref pos, wire);
                    break;
            }
        }
    }

    private void ParseSoundEntry(byte[] data)
    {
        int pos = 0;
        uint id = 0; string filename = string.Empty;
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            switch (f)
            {
                case 1: id       = (uint)ReadVarint(data, ref pos); break;
                case 2: filename = System.Text.Encoding.UTF8.GetString(ReadLengthDelimited(data, ref pos)); break;
                default: SkipField(data, ref pos, w); break;
            }
        }
        _audioFiles[id] = filename;
    }

    private void ParseSoundEffect(byte[] data)
    {
        // Fields: id(1), numeric_sound_type(2), random_pitch(3/msg), random_volume(4/msg),
        //         simple_sound_effect(5/msg), random_sound_effect(6/msg)
        int pos = 0;
        uint id = 0; int type = 0;
        float pitchMin = 0, pitchMax = 0, volMin = 0, volMax = 0;
        uint soundId = 0;
        var randomIds = new List<uint>();

        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            switch (f)
            {
                case 1: id   = (uint)ReadVarint(data, ref pos); break;
                case 2: type = (int)ReadVarint(data, ref pos); break;
                case 3: // random_pitch: min_value(1), max_value(2)
                {
                    byte[] sub = ReadLengthDelimited(data, ref pos);
                    ParseMinMax(sub, ref pitchMin, ref pitchMax);
                    break;
                }
                case 4: // random_volume
                {
                    byte[] sub = ReadLengthDelimited(data, ref pos);
                    ParseMinMax(sub, ref volMin, ref volMax);
                    break;
                }
                case 5: // simple_sound_effect: sound_id(1)
                {
                    byte[] sub = ReadLengthDelimited(data, ref pos);
                    soundId = ParseSimpleSoundEffect(sub);
                    break;
                }
                case 6: // random_sound_effect: random_sound_id(1) repeated
                {
                    byte[] sub = ReadLengthDelimited(data, ref pos);
                    ParseRandomSoundEffect(sub, randomIds);
                    break;
                }
                default: SkipField(data, ref pos, w); break;
            }
        }
        _soundEffects[id] = new ClientSoundEffect
        {
            Id           = id,
            Type         = (ClientSoundType)type,
            PitchMin     = pitchMin,
            PitchMax     = pitchMax,
            VolumeMin    = volMin,
            VolumeMax    = volMax,
            SoundId      = soundId,
            RandomSoundIds = randomIds,
        };
    }

    private static void ParseMinMax(byte[] data, ref float min, ref float max)
    {
        int pos = 0;
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            switch (f)
            {
                case 1: min = ReadFloat(data, ref pos); break;
                case 2: max = ReadFloat(data, ref pos); break;
                default: SkipField(data, ref pos, w); break;
            }
        }
    }

    private static uint ParseSimpleSoundEffect(byte[] data)
    {
        int pos = 0; uint sid = 0;
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            if (f == 1) sid = (uint)ReadVarint(data, ref pos);
            else SkipField(data, ref pos, w);
        }
        return sid;
    }

    private static void ParseRandomSoundEffect(byte[] data, List<uint> ids)
    {
        int pos = 0;
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            if (f == 1) ids.Add((uint)ReadVarint(data, ref pos));
            else SkipField(data, ref pos, w);
        }
    }

    private void ParseLocationAmbient(byte[] data)
    {
        int pos = 0; uint id = 0, loopId = 0;
        var delayed = new List<DelayedSoundEffect>();
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            switch (f)
            {
                case 1: id     = (uint)ReadVarint(data, ref pos); break;
                case 2: loopId = (uint)ReadVarint(data, ref pos); break;
                case 3: // delayed_effects: numeric_sound_effect_id(1), delay_seconds(2)
                {
                    byte[] sub = ReadLengthDelimited(data, ref pos);
                    delayed.Add(ParseDelayedEffect(sub));
                    break;
                }
                default: SkipField(data, ref pos, w); break;
            }
        }
        _locationAmbients[id] = new ClientLocationAmbient
        {
            Id                 = id,
            LoopedAudioFileId  = loopId,
            DelayedEffects     = delayed,
        };
    }

    private static DelayedSoundEffect ParseDelayedEffect(byte[] data)
    {
        int pos = 0; uint effectId = 0, delay = 0;
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            switch (f)
            {
                case 1: effectId = (uint)ReadVarint(data, ref pos); break;
                case 2: delay    = (uint)ReadVarint(data, ref pos); break;
                default: SkipField(data, ref pos, w); break;
            }
        }
        return new DelayedSoundEffect(effectId, delay);
    }

    private void ParseItemAmbient(byte[] data)
    {
        int pos = 0; uint id = 0;
        var clientIds = new List<uint>();
        var effects   = new List<ItemCountSoundEffect>();
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            switch (f)
            {
                case 1: id = (uint)ReadVarint(data, ref pos); break;
                case 2: clientIds.Add((uint)ReadVarint(data, ref pos)); break;
                case 3: // sound_effects: looping_sound_id(2), count(1)
                {
                    byte[] sub = ReadLengthDelimited(data, ref pos);
                    effects.Add(ParseItemCountSoundEffect(sub));
                    break;
                }
                default: SkipField(data, ref pos, w); break;
            }
        }
        _itemAmbients[id] = new ClientItemAmbient
        {
            Id               = id,
            ClientIds        = clientIds,
            ItemCountEffects = effects,
        };
    }

    private static ItemCountSoundEffect ParseItemCountSoundEffect(byte[] data)
    {
        int pos = 0; uint count = 0, loopId = 0;
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            switch (f)
            {
                case 1: count  = (uint)ReadVarint(data, ref pos); break;
                case 2: loopId = (uint)ReadVarint(data, ref pos); break;
                default: SkipField(data, ref pos, w); break;
            }
        }
        return new ItemCountSoundEffect(loopId, count);
    }

    private void ParseMusicTemplate(byte[] data)
    {
        int pos = 0; uint id = 0, audioFileId = 0; int musicType = 0;
        while (pos < data.Length)
        {
            ulong tag = ReadVarint(data, ref pos); int f = (int)(tag >> 3); int w = (int)(tag & 7);
            switch (f)
            {
                case 1: id          = (uint)ReadVarint(data, ref pos); break;
                case 2: audioFileId = (uint)ReadVarint(data, ref pos); break;
                case 3: musicType   = (int)ReadVarint(data, ref pos); break;
                default: SkipField(data, ref pos, w); break;
            }
        }
        _musicTracks[id] = new ClientMusic
        {
            Id          = id,
            AudioFileId = audioFileId,
            MusicType   = (ClientMusicType)musicType,
        };
    }

    // ─── T29: Protobuf wire-format primitives ──────────────────────────────────

    private static ulong ReadVarint(byte[] data, ref int pos)
    {
        ulong result = 0; int shift = 0;
        while (pos < data.Length)
        {
            byte b = data[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return result;
    }

    private static byte[] ReadLengthDelimited(byte[] data, ref int pos)
    {
        int len = (int)ReadVarint(data, ref pos);
        if (pos + len > data.Length)
            throw new InvalidDataException($"Length-delimited field overflows buffer (need {len}, have {data.Length - pos}).");
        var bytes = new byte[len];
        Array.Copy(data, pos, bytes, 0, len);
        pos += len;
        return bytes;
    }

    private static float ReadFloat(byte[] data, ref int pos)
    {
        if (pos + 4 > data.Length)
            throw new InvalidDataException("32-bit float field overflows buffer.");
        float v = BitConverter.ToSingle(data, pos);
        pos += 4;
        return v;
    }

    private static void SkipField(byte[] data, ref int pos, int wireType)
    {
        switch (wireType)
        {
            case 0: ReadVarint(data, ref pos); break;
            case 1:
                if (pos + 8 > data.Length) throw new InvalidDataException("64-bit field overflows buffer.");
                pos += 8;
                break;
            case 2:
            {
                int len = (int)ReadVarint(data, ref pos);
                if (pos + len > data.Length) throw new InvalidDataException("Length-delimited skip overflows buffer.");
                pos += len;
                break;
            }
            case 5:
                if (pos + 4 > data.Length) throw new InvalidDataException("32-bit field overflows buffer.");
                pos += 4;
                break;
            default: pos = data.Length; break; // unknown — skip rest
        }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Terminate();
    }
}
