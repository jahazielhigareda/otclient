using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Tests for <see cref="ProtocolGame"/> — Tibia 12.x game protocol.
/// All tests operate purely on in-memory <see cref="InputMessage"/> /
/// <see cref="OutputMessage"/> objects; no TCP connection is required.
/// Task 5.6–5.8 / task 5.12.
/// </summary>
public sealed class ProtocolGameTests
{
    // ─── Construction ────────────────────────────────────────────────────────

    [Fact]
    public void Ctor_IsNotConnected()
    {
        using var pg = new ProtocolGame();
        Assert.False(pg.IsConnected);
    }

    [Fact]
    public void Ctor_CharacterName_IsEmpty()
    {
        using var pg = new ProtocolGame();
        Assert.Equal(string.Empty, pg.CharacterName);
    }

    [Fact]
    public void Ctor_IsNotInGame()
    {
        using var pg = new ProtocolGame();
        Assert.False(pg.IsInGame);
    }

    // ─── Opcode enumerations ─────────────────────────────────────────────────

    [Fact]
    public void GameClientPacket_LoginRequest_Is0x0A()
    {
        Assert.Equal(0x0A, (byte)GameClientPacket.LoginRequest);
    }

    [Fact]
    public void GameClientPacket_MoveNorth_Is0x65()
    {
        Assert.Equal(0x65, (byte)GameClientPacket.MoveNorth);
    }

    [Fact]
    public void GameClientPacket_Stop_Is0x69()
    {
        Assert.Equal(0x69, (byte)GameClientPacket.Stop);
    }

    [Fact]
    public void GameClientPacket_MoveNorthWest_Is0x6D()
    {
        // Regression: previously MoveNorthWest was wrongly set to 0x69 (Stop).
        Assert.Equal(0x6D, (byte)GameClientPacket.MoveNorthWest);
    }

    [Fact]
    public void GameServerPacket_LoginError_Is0x14()
    {
        Assert.Equal(0x14, (byte)GameServerPacket.LoginError);
    }

    [Fact]
    public void GameServerPacket_TextMessage_Is0xB4()
    {
        Assert.Equal(0xB4, (byte)GameServerPacket.TextMessage);
    }

    // ─── Direction enum ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(Direction.North,     0)]
    [InlineData(Direction.East,      1)]
    [InlineData(Direction.South,     2)]
    [InlineData(Direction.West,      3)]
    [InlineData(Direction.NorthEast, 4)]
    [InlineData(Direction.SouthEast, 5)]
    [InlineData(Direction.SouthWest, 6)]
    [InlineData(Direction.NorthWest, 7)]
    public void Direction_WireValues_AreCorrect(Direction dir, byte expected)
    {
        Assert.Equal(expected, (byte)dir);
    }

    // ─── ParseLoginError (parse method tests) ─────────────────────────────────

    [Fact]
    public void ParseLoginError_InvokesEvent_WithCorrectReason()
    {
        using var pg = new ProtocolGame();

        string? received = null;
        pg.LoginError += reason => received = reason;

        // Build a raw packet: [opcode][string]
        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.LoginError);
        out_.WriteString("Account not found.");

        // Feed it via HandleRawData (internal, exposed via the protected override test)
        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Account not found.", received);
    }

    [Fact]
    public void ParseLoginAdvice_InvokesEvent_WithCorrectMessage()
    {
        using var pg = new ProtocolGame();

        string? received = null;
        pg.LoginAdvice += msg => received = msg;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.LoginAdvice);
        out_.WriteString("Your client version is outdated.");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Your client version is outdated.", received);
    }

    [Fact]
    public void ParseLoginWait_InvokesEvent_WithMessageAndSeconds()
    {
        using var pg = new ProtocolGame();

        string? waitMsg = null;
        int     waitSec = -1;
        pg.LoginWait += (msg, sec) => { waitMsg = msg; waitSec = sec; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.LoginWait);
        out_.WriteString("Server is full.");
        out_.WriteU8(30);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Server is full.", waitMsg);
        Assert.Equal(30, waitSec);
    }

    [Fact]
    public void ParseTextMessage_InvokesEvent_WithTypeAndContent()
    {
        using var pg = new ProtocolGame();

        byte   msgType   = 0;
        string? msgText  = null;
        pg.TextMessageReceived += (t, m) => { msgType = t; msgText = m; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.TextMessage);
        out_.WriteU8(0x13);              // some message type byte
        out_.WriteString("You levelled up!");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(0x13, msgType);
        Assert.Equal("You levelled up!", msgText);
    }

    [Fact]
    public void ParsePlayerSpeech_InvokesEvent_WithAuthorModeContent()
    {
        using var pg = new ProtocolGame();

        string?   author  = null;
        ChatMode  mode    = default;
        string?   content = null;
        pg.SpeechReceived += (a, m, c) => { author = a; mode = m; content = c; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PlayerSpeech);
        out_.WriteString("PlayerX");
        out_.WriteU8((byte)ChatMode.Say);
        out_.WriteString("Hello world");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("PlayerX", author);
        Assert.Equal(ChatMode.Say, mode);
        Assert.Equal("Hello world", content);
    }

    // ─── Ping round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void ParseDeath_InvokesEvent()
    {
        using var pg = new ProtocolGame();

        bool died = false;
        pg.PlayerDied += () => died = true;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Death);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.True(died);
    }

    // ─── OutputMessage packet building ────────────────────────────────────────

    [Fact]
    public void OutputMessage_LoginRequest_StartsWithCorrectOpcode()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.LoginRequest);
        msg.WriteU16(2);    // os = Linux
        msg.WriteU16(1212); // version

        byte[] payload = msg.ToArray();
        Assert.Equal((byte)GameClientPacket.LoginRequest, payload[0]);
        // OS LE u16 at [1..2] = 2
        Assert.Equal(2, payload[1] | (payload[2] << 8));
    }

    [Fact]
    public void OutputMessage_WalkNorth_OpcodePresentInPayload()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.MoveNorth);
        byte[] payload = msg.ToArray();
        Assert.Equal((byte)GameClientPacket.MoveNorth, payload[0]);
    }

    [Fact]
    public void OutputMessage_Say_ContainsMode_And_Text()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.Say);
        msg.WriteU8((byte)ChatMode.Yell);
        msg.WriteString("HELLO!");

        byte[] payload = msg.ToArray();
        Assert.Equal((byte)GameClientPacket.Say,    payload[0]);
        Assert.Equal((byte)ChatMode.Yell,           payload[1]);
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var pg = new ProtocolGame();
        var ex = Record.Exception(() => pg.Dispose());
        Assert.Null(ex);
    }

    // ─── SendStop ─────────────────────────────────────────────────────────────

    [Fact]
    public void OutputMessage_Stop_HasCorrectOpcode()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.Stop);
        byte[] payload = msg.ToArray();
        Assert.Equal(0x69, payload[0]);
    }

    // ─── SendAutoWalk ─────────────────────────────────────────────────────────

    [Fact]
    public void OutputMessage_AutoWalk_StartsWithOpcode()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.AutoWalk);
        msg.WriteU8(1);           // one direction
        msg.WriteU8(3);           // North wire byte (3)
        byte[] payload = msg.ToArray();
        Assert.Equal(0x64, payload[0]);   // AutoWalk opcode
        Assert.Equal(1,    payload[1]);   // count
        Assert.Equal(3,    payload[2]);   // North = 3 in auto-walk wire encoding
    }

    // ─── T05: ParsePlayerStats ────────────────────────────────────────────────

    [Fact]
    public void ParsePlayerStats_InvokesEvent_WithCorrectValues()
    {
        using var pg = new ProtocolGame();

        int    health = 0, maxHealth = 0, mana = 0, maxMana = 0, freeCap = 0;
        ulong  exp    = 0;
        int    level  = 0, lvlPct = 0, stamina = 0, soul = 0;

        pg.PlayerStatsUpdated += (h, mh, mn, mmn, fc, e, lv, lp, st, so) =>
        {
            health = h; maxHealth = mh; mana = mn; maxMana = mmn;
            freeCap = fc; exp = e; level = lv; lvlPct = lp;
            stamina = st; soul = so;
        };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PlayerData);
        // health/maxHealth U32
        out_.WriteU32(450);   // health
        out_.WriteU32(500);   // maxHealth
        // freeCapacity U32 (scaled by 100)
        out_.WriteU32(40000); // 400.00 = 400
        // experience U64
        out_.WriteU32(1000); out_.WriteU32(0); // 1000 exp (lo+hi)
        // level U16, levelPercent U8
        out_.WriteU16(10);
        out_.WriteU8(75);
        // xp bonus fields: baseXpGain, grindingAddend, storeBoost, huntingFactor
        out_.WriteU16(100); out_.WriteU16(0); out_.WriteU16(0); out_.WriteU16(100);
        // mana/maxMana U32
        out_.WriteU32(200); out_.WriteU32(300);
        // soul U8, stamina U16
        out_.WriteU8(90);
        out_.WriteU16(2000);
        // baseSpeed U16, regeneration U16, offlineTraining U16
        out_.WriteU16(220); out_.WriteU16(60); out_.WriteU16(0);
        // xpBoostTime U16, enableXpBoostStore U8
        out_.WriteU16(0); out_.WriteU8(0);
        // manaShield U32, maxManaShield U32
        out_.WriteU32(0); out_.WriteU32(0);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(450,   health);
        Assert.Equal(500,   maxHealth);
        Assert.Equal(200,   mana);
        Assert.Equal(300,   maxMana);
        Assert.Equal(400,   freeCap);
        Assert.Equal(1000UL, exp);
        Assert.Equal(10,    level);
        Assert.Equal(75,    lvlPct);
        Assert.Equal(2000,  stamina);
        Assert.Equal(90,    soul);
    }

    // ─── T05: ParsePlayerSkills ───────────────────────────────────────────────

    [Fact]
    public void ParsePlayerSkills_InvokesEvent_WithMagicAndSkills()
    {
        using var pg = new ProtocolGame();

        int magicLv = 0, magicPct = 0;
        int[]? levels = null, percents = null;

        pg.PlayerSkillsUpdated += (ml, mp, lvs, pcts) =>
        {
            magicLv = ml; magicPct = mp; levels = lvs; percents = pcts;
        };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PlayerSkills);
        // magic level: level U16, base U16, loyalty U16, percent U16
        out_.WriteU16(5);    // magicLevel
        out_.WriteU16(5);    // baseMagicLevel
        out_.WriteU16(0);    // loyalty bonus
        out_.WriteU16(3400); // 34% (3400/100)
        // 7 combat skills: level U16, base U16, loyalty U16, percent U16
        for (int i = 0; i < 7; i++)
        {
            out_.WriteU16((ushort)(10 + i)); // level
            out_.WriteU16((ushort)(10 + i)); // base
            out_.WriteU16(0);                // loyalty
            out_.WriteU16((ushort)(5000));   // 50%
        }

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(5,  magicLv);
        Assert.Equal(34, magicPct);
        Assert.NotNull(levels);
        Assert.Equal(7, levels!.Length);
        Assert.Equal(10, levels[0]);   // Fist
        Assert.Equal(50, percents![0]);
    }

    // ─── T05: ParsePlayerState ────────────────────────────────────────────────

    [Fact]
    public void ParsePlayerState_InvokesEvent_WithStateBitmask()
    {
        using var pg = new ProtocolGame();

        uint receivedState = 0;
        pg.PlayerStateUpdated += s => receivedState = s;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PlayerState);
        out_.WriteU32(0b0101); // two condition flags set

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(0b0101u, receivedState);
    }

    // ─── T05: ParsePlayerModes ────────────────────────────────────────────────

    [Fact]
    public void ParsePlayerModes_InvokesEvent_WithAllModes()
    {
        using var pg = new ProtocolGame();

        OTClient.Framework.Game.FightMode fm = default;
        OTClient.Framework.Game.ChaseMode cm = default;
        bool safe = false;
        OTClient.Framework.Game.PvpMode pm = default;

        pg.PlayerModesUpdated += (f, c, s, p) => { fm = f; cm = c; safe = s; pm = p; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PlayerModes);
        out_.WriteU8((byte)OTClient.Framework.Game.FightMode.Offensive);
        out_.WriteU8((byte)OTClient.Framework.Game.ChaseMode.ChaseOpponent);
        out_.WriteU8(0);   // safeMode = false
        out_.WriteU8((byte)OTClient.Framework.Game.PvpMode.WhiteHand);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(OTClient.Framework.Game.FightMode.Offensive,    fm);
        Assert.Equal(OTClient.Framework.Game.ChaseMode.ChaseOpponent, cm);
        Assert.False(safe);
        Assert.Equal(OTClient.Framework.Game.PvpMode.WhiteHand, pm);
    }

    // ─── T05: New GameServerPacket opcodes ────────────────────────────────────

    [Fact]
    public void GameServerPacket_PlayerData_Is0xA0()
    {
        Assert.Equal(0xA0, (byte)GameServerPacket.PlayerData);
    }

    [Fact]
    public void GameServerPacket_PlayerSkills_Is0xA1()
    {
        Assert.Equal(0xA1, (byte)GameServerPacket.PlayerSkills);
    }

    [Fact]
    public void GameServerPacket_PlayerState_Is0xA2()
    {
        Assert.Equal(0xA2, (byte)GameServerPacket.PlayerState);
    }

    [Fact]
    public void GameServerPacket_PlayerModes_Is0xA7()
    {
        Assert.Equal(0xA7, (byte)GameServerPacket.PlayerModes);
    }

    // ─── Helper to invoke the protected HandleRawData method ──────────────────

    private static void InvokeHandleRawData(ProtocolGame pg, byte[] data)
    {
        // Expose via reflection — the method is protected in the base class
        var method = typeof(Protocol).GetMethod(
            "HandleRawData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(pg, [data]);
    }
}
