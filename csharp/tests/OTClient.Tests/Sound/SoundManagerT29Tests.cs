using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using OTClient.Framework.Sound;
using Xunit;

namespace OTClient.Tests.Sound;

/// <summary>
/// Tests for T29: <see cref="SoundManager.LoadClientFiles"/> and
/// <see cref="SoundManager.LoadFromProtobuf"/> soundbank loading.
/// No audio device is opened; only CPU-side deserialization is exercised.
/// </summary>
public sealed class SoundManagerT29Tests
{
    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void IsSoundbankLoaded_FalseInitially()
    {
        var mgr = new SoundManager();
        Assert.False(mgr.IsSoundbankLoaded);
    }

    [Fact]
    public void AudioFileCount_ZeroInitially()
    {
        var mgr = new SoundManager();
        Assert.Equal(0, mgr.AudioFileCount);
    }

    // ─── LoadFromProtobuf — edge cases ────────────────────────────────────────

    [Fact]
    public void LoadFromProtobuf_NullData_ReturnsFalse()
    {
        var mgr = new SoundManager();
        Assert.False(mgr.LoadFromProtobuf(null!));
        Assert.False(mgr.IsSoundbankLoaded);
    }

    [Fact]
    public void LoadFromProtobuf_EmptyData_ReturnsFalse()
    {
        var mgr = new SoundManager();
        Assert.False(mgr.LoadFromProtobuf([]));
        Assert.False(mgr.IsSoundbankLoaded);
    }

    [Fact]
    public void LoadFromProtobuf_RandomBytes_DoesNotThrow()
    {
        var mgr = new SoundManager();
        var ex = Record.Exception(() => mgr.LoadFromProtobuf([0xFF, 0xFE, 0xAB, 0xCD]));
        Assert.Null(ex);
    }

    // ─── LoadFromProtobuf — sound file entries (field 1) ─────────────────────

    [Fact]
    public void LoadFromProtobuf_SoundEntry_PopulatesAudioFileDictionary()
    {
        // Build a minimal protobuf Sounds message with one Sound entry:
        //   field 1 (sound), wire type 2 (length-delimited)
        //     field 1 (id)       = 42
        //     field 2 (filename) = "sound-abc.ogg"
        var mgr  = new SoundManager();
        byte[] pb = BuildSoundsWithOneAudioFile(42u, "sound-abc.ogg");
        bool ok   = mgr.LoadFromProtobuf(pb);

        Assert.True(ok);
        Assert.True(mgr.IsSoundbankLoaded);
        Assert.Equal(1, mgr.AudioFileCount);
        Assert.Equal("sound-abc.ogg", mgr.GetAudioFileNameById(42u));
    }

    [Fact]
    public void GetAudioFileNameById_UnknownId_ReturnsEmpty()
    {
        var mgr = new SoundManager();
        Assert.Equal(string.Empty, mgr.GetAudioFileNameById(99u));
    }

    // ─── LoadFromProtobuf — sound effect entries (field 2) ───────────────────

    [Fact]
    public void LoadFromProtobuf_SoundEffect_TryGetSoundEffectReturnsIt()
    {
        var mgr  = new SoundManager();
        byte[] pb = BuildSoundsWithOneSoundEffect(
            id: 7u, type: ClientSoundType.WeaponAttack,
            pitchMin: 0.8f, pitchMax: 1.2f,
            volMin:   0.9f, volMax:   1.0f,
            soundId:  42u);
        mgr.LoadFromProtobuf(pb);

        var effect = mgr.TryGetSoundEffect(7u);
        Assert.NotNull(effect);
        Assert.Equal(7u,                       effect.Id);
        Assert.Equal(ClientSoundType.WeaponAttack, effect.Type);
        Assert.Equal(42u,                      effect.SoundId);
        Assert.Equal(1, mgr.SoundEffectCount);
    }

    [Fact]
    public void TryGetSoundEffect_MissingId_ReturnsNull()
    {
        var mgr = new SoundManager();
        Assert.Null(mgr.TryGetSoundEffect(999u));
    }

    // ─── LoadFromProtobuf — location ambient entries (field 3) ───────────────

    [Fact]
    public void LoadFromProtobuf_LocationAmbient_TryGetLocationAmbientReturnsIt()
    {
        var mgr  = new SoundManager();
        byte[] pb = BuildSoundsWithOneLocationAmbient(id: 5u, loopId: 100u);
        mgr.LoadFromProtobuf(pb);

        var ambient = mgr.TryGetLocationAmbient(5u);
        Assert.NotNull(ambient);
        Assert.Equal(5u,   ambient.Id);
        Assert.Equal(100u, ambient.LoopedAudioFileId);
        Assert.Equal(1, mgr.LocationAmbientCount);
    }

    [Fact]
    public void TryGetLocationAmbient_MissingId_ReturnsNull()
    {
        var mgr = new SoundManager();
        Assert.Null(mgr.TryGetLocationAmbient(1u));
    }

    // ─── LoadFromProtobuf — item ambient entries (field 4) ───────────────────

    [Fact]
    public void LoadFromProtobuf_ItemAmbient_TryGetItemAmbientReturnsIt()
    {
        var mgr  = new SoundManager();
        byte[] pb = BuildSoundsWithOneItemAmbient(id: 3u, clientId: 200u, loopId: 630u, count: 1u);
        mgr.LoadFromProtobuf(pb);

        var ambient = mgr.TryGetItemAmbient(3u);
        Assert.NotNull(ambient);
        Assert.Equal(3u, ambient.Id);
        Assert.Contains(200u, ambient.ClientIds);
        Assert.Equal(1, mgr.ItemAmbientCount);
    }

    [Fact]
    public void TryGetItemAmbient_MissingId_ReturnsNull()
    {
        var mgr = new SoundManager();
        Assert.Null(mgr.TryGetItemAmbient(1u));
    }

    // ─── LoadFromProtobuf — music template entries (field 5) ─────────────────

    [Fact]
    public void LoadFromProtobuf_MusicTemplate_TryGetMusicReturnsIt()
    {
        var mgr  = new SoundManager();
        byte[] pb = BuildSoundsWithOneMusicTemplate(id: 9u, audioFileId: 777u, musicType: ClientMusicType.Music);
        mgr.LoadFromProtobuf(pb);

        var music = mgr.TryGetMusic(9u);
        Assert.NotNull(music);
        Assert.Equal(9u,                   music.Id);
        Assert.Equal(777u,                 music.AudioFileId);
        Assert.Equal(ClientMusicType.Music, music.MusicType);
        Assert.Equal(1, mgr.MusicTrackCount);
    }

    [Fact]
    public void TryGetMusic_MissingId_ReturnsNull()
    {
        var mgr = new SoundManager();
        Assert.Null(mgr.TryGetMusic(1u));
    }

    // ─── LoadClientFiles ──────────────────────────────────────────────────────

    [Fact]
    public void LoadClientFiles_ValidCatalog_ReturnsTrue()
    {
        var pb  = BuildSoundsWithOneAudioFile(1u, "s.ogg");
        var catalog = """[{"type":"sounds","file":"sounds.dat"}]""";
        var mgr = new SoundManager();

        bool ok = mgr.LoadClientFiles(
            "tibia/",
            path => catalog,
            path => pb);

        Assert.True(ok);
        Assert.True(mgr.IsSoundbankLoaded);
    }

    [Fact]
    public void LoadClientFiles_CatalogWithNoSoundsEntry_ReturnsFalse()
    {
        var catalog = """[{"type":"appearances","file":"appearances.dat"}]""";
        var mgr = new SoundManager();

        bool ok = mgr.LoadClientFiles(
            "tibia/",
            path => catalog,
            path => throw new Exception("should not be called"));

        Assert.False(ok);
    }

    [Fact]
    public void LoadClientFiles_MissingCatalog_ReturnsFalse()
    {
        var mgr = new SoundManager();
        bool ok = mgr.LoadClientFiles(
            "tibia/",
            path => throw new System.IO.FileNotFoundException(),
            path => []);
        Assert.False(ok);
    }

    // ─── Protobuf builder helpers ─────────────────────────────────────────────

    private static byte[] BuildSoundsWithOneAudioFile(uint id, string filename)
    {
        // Sound sub-message: field1=id(varint), field2=filename(string)
        var sub = new List<byte>();
        WriteVarintField(sub, 1, id);
        WriteStringField(sub, 2, filename);
        // Sounds top-level: field1=sound (length-delimited)
        var top = new List<byte>();
        WriteLenDelimField(top, 1, sub);
        return [.. top];
    }

    private static byte[] BuildSoundsWithOneSoundEffect(
        uint id, ClientSoundType type,
        float pitchMin, float pitchMax,
        float volMin,   float volMax,
        uint soundId)
    {
        // NumericSoundEffect sub-message
        var sub = new List<byte>();
        WriteVarintField(sub, 1, id);
        WriteVarintField(sub, 2, (ulong)type);
        // random_pitch (field 3) — MinMaxFloat sub-msg
        var pitch = new List<byte>();
        WriteFloatField(pitch, 1, pitchMin);
        WriteFloatField(pitch, 2, pitchMax);
        WriteLenDelimField(sub, 3, pitch);
        // random_volume (field 4)
        var vol = new List<byte>();
        WriteFloatField(vol, 1, volMin);
        WriteFloatField(vol, 2, volMax);
        WriteLenDelimField(sub, 4, vol);
        // simple_sound_effect (field 5): sound_id(1)
        var simple = new List<byte>();
        WriteVarintField(simple, 1, soundId);
        WriteLenDelimField(sub, 5, simple);

        var top = new List<byte>();
        WriteLenDelimField(top, 2, sub);
        return [.. top];
    }

    private static byte[] BuildSoundsWithOneLocationAmbient(uint id, uint loopId)
    {
        var sub = new List<byte>();
        WriteVarintField(sub, 1, id);
        WriteVarintField(sub, 2, loopId);
        var top = new List<byte>();
        WriteLenDelimField(top, 3, sub);
        return [.. top];
    }

    private static byte[] BuildSoundsWithOneItemAmbient(uint id, uint clientId, uint loopId, uint count)
    {
        var sub = new List<byte>();
        WriteVarintField(sub, 1, id);
        WriteVarintField(sub, 2, clientId);
        // sound_effects sub-msg: count(1), looping_sound_id(2)
        var effect = new List<byte>();
        WriteVarintField(effect, 1, count);
        WriteVarintField(effect, 2, loopId);
        WriteLenDelimField(sub, 3, effect);
        var top = new List<byte>();
        WriteLenDelimField(top, 4, sub);
        return [.. top];
    }

    private static byte[] BuildSoundsWithOneMusicTemplate(uint id, uint audioFileId, ClientMusicType musicType)
    {
        var sub = new List<byte>();
        WriteVarintField(sub, 1, id);
        WriteVarintField(sub, 2, audioFileId);
        WriteVarintField(sub, 3, (ulong)musicType);
        var top = new List<byte>();
        WriteLenDelimField(top, 5, sub);
        return [.. top];
    }

    // ─── Protobuf encoding helpers ────────────────────────────────────────────

    private static void WriteVarint(List<byte> buf, ulong value)
    {
        do { buf.Add((byte)((value & 0x7F) | (value > 0x7F ? 0x80u : 0u))); value >>= 7; } while (value > 0);
    }

    private static void WriteVarintField(List<byte> buf, int field, ulong value)
    {
        WriteVarint(buf, (ulong)(field << 3) | 0); // wire type 0
        WriteVarint(buf, value);
    }

    private static void WriteStringField(List<byte> buf, int field, string value)
    {
        byte[] raw = Encoding.UTF8.GetBytes(value);
        WriteVarint(buf, (ulong)(field << 3) | 2); // wire type 2
        WriteVarint(buf, (ulong)raw.Length);
        buf.AddRange(raw);
    }

    private static void WriteLenDelimField(List<byte> buf, int field, List<byte> content)
    {
        WriteVarint(buf, (ulong)(field << 3) | 2); // wire type 2
        WriteVarint(buf, (ulong)content.Count);
        buf.AddRange(content);
    }

    private static void WriteFloatField(List<byte> buf, int field, float value)
    {
        WriteVarint(buf, (ulong)(field << 3) | 5); // wire type 5 (32-bit)
        buf.AddRange(BitConverter.GetBytes(value));
    }
}
