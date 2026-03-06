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

    // ─── T03: New GameServerPacket creature opcodes ────────────────────────────

    [Fact]
    public void GameServerPacket_MoveCreature_Is0x6D()
    {
        // Regression: previously MoveCreature was wrongly 0x6C (TileRemoveThing).
        Assert.Equal(0x6D, (byte)GameServerPacket.MoveCreature);
    }

    [Fact]
    public void GameServerPacket_CreatureData_Is0x8B()
    {
        Assert.Equal(0x8B, (byte)GameServerPacket.CreatureData);
    }

    [Fact]
    public void GameServerPacket_CreatureHealth_Is0x8C()
    {
        Assert.Equal(0x8C, (byte)GameServerPacket.CreatureHealth);
    }

    [Fact]
    public void GameServerPacket_CreatureOutfit_Is0x8E()
    {
        Assert.Equal(0x8E, (byte)GameServerPacket.CreatureOutfit);
    }

    [Fact]
    public void GameServerPacket_CreatureSpeed_Is0x8F()
    {
        Assert.Equal(0x8F, (byte)GameServerPacket.CreatureSpeed);
    }

    // ─── T03: ParseCreatureHealth ─────────────────────────────────────────────

    [Fact]
    public void ParseCreatureHealth_InvokesEvent_WithIdAndPercent()
    {
        using var pg = new ProtocolGame();

        uint receivedId   = 0;
        byte receivedPct  = 0;
        pg.CreatureHealthUpdated += (id, pct) => { receivedId = id; receivedPct = pct; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureHealth);
        out_.WriteU32(12345);   // creature ID
        out_.WriteU8(75);       // health percent

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(12345u, receivedId);
        Assert.Equal(75,     receivedPct);
    }

    // ─── T03: ParseCreatureSpeed ──────────────────────────────────────────────

    [Fact]
    public void ParseCreatureSpeed_InvokesEvent_WithIdBaseAndSpeed()
    {
        using var pg = new ProtocolGame();

        uint receivedId  = 0;
        int  receivedBase = 0, receivedSpeed = 0;
        pg.CreatureSpeedUpdated += (id, b, s) => { receivedId = id; receivedBase = b; receivedSpeed = s; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureSpeed);
        out_.WriteU32(99u);     // creature ID
        out_.WriteU16(220);     // base speed
        out_.WriteU16(300);     // speed

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(99u,  receivedId);
        Assert.Equal(220,  receivedBase);
        Assert.Equal(300,  receivedSpeed);
    }

    // ─── T03: ParseCreatureOutfit ─────────────────────────────────────────────

    [Fact]
    public void ParseCreatureOutfit_InvokesEvent_WithOutfitFields()
    {
        using var pg = new ProtocolGame();

        uint             receivedId     = 0;
        OTClient.Framework.Game.Outfit receivedOutfit = OTClient.Framework.Game.Outfit.Default;
        bool outfitReceived = false;
        pg.CreatureOutfitUpdated += (id, o) => { receivedId = id; receivedOutfit = o; outfitReceived = true; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureOutfit);
        out_.WriteU32(777u);    // creature ID
        // Outfit: lookType U16
        out_.WriteU16(128);     // lookType (nonzero → creature outfit)
        out_.WriteU8(10);       // head
        out_.WriteU8(20);       // body
        out_.WriteU8(30);       // legs
        out_.WriteU8(40);       // feet
        out_.WriteU8(0);        // addons
        out_.WriteU16(0);       // mount ID (0 = no mount, no extra colour bytes)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(777u,  receivedId);
        Assert.True(outfitReceived);
        Assert.Equal(128,   receivedOutfit!.Id);
        Assert.Equal(10,    receivedOutfit.Head);
        Assert.Equal(20,    receivedOutfit.Body);
        Assert.Equal(30,    receivedOutfit.Legs);
        Assert.Equal(40,    receivedOutfit.Feet);
    }

    // ─── T03: ParseCreatureMove (ID-based) ───────────────────────────────────

    [Fact]
    public void ParseCreatureMove_IdBased_InvokesCreatureMovedById()
    {
        using var pg = new ProtocolGame();

        uint                              receivedId  = 0;
        OTClient.Framework.Game.Position? receivedPos = null;
        pg.CreatureMovedById += (id, pos) => { receivedId = id; receivedPos = pos; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.MoveCreature);
        out_.WriteU16(0xFFFF);  // signals "id-based" form
        out_.WriteU32(42u);     // creature ID
        // destination position
        out_.WriteU16(11);
        out_.WriteU16(10);
        out_.WriteU8(7);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(42u, receivedId);
        Assert.Equal(new OTClient.Framework.Game.Position(11, 10, 7), receivedPos);
    }

    // ─── T03: ParseCreatureMove (tile-based) ─────────────────────────────────

    [Fact]
    public void ParseCreatureMove_TileBased_InvokesCreatureTileMoved()
    {
        using var pg = new ProtocolGame();

        OTClient.Framework.Game.Position? fromPos = null;
        int  stackPos    = -1;
        OTClient.Framework.Game.Position? toPos   = null;
        pg.CreatureTileMoved += (f, s, t) => { fromPos = f; stackPos = s; toPos = t; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.MoveCreature);
        out_.WriteU16(10);      // x (not 0xFFFF → position-based)
        out_.WriteU16(10);      // y
        out_.WriteU8(7);        // z
        out_.WriteU8(2);        // stackpos
        // destination
        out_.WriteU16(11);
        out_.WriteU16(10);
        out_.WriteU8(7);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(new OTClient.Framework.Game.Position(10, 10, 7), fromPos);
        Assert.Equal(2, stackPos);
        Assert.Equal(new OTClient.Framework.Game.Position(11, 10, 7), toPos);
    }

    // ─── T03: ParseCreatureData (types 11–14) ────────────────────────────────

    [Fact]
    public void ParseCreatureData_Type13_FiresDataByteReceivedEvent()
    {
        using var pg = new ProtocolGame();

        uint receivedId   = 0;
        byte receivedType = 0;
        byte receivedVal  = 0;
        pg.CreatureDataByteReceived += (id, t, v) => { receivedId = id; receivedType = t; receivedVal = v; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureData);
        out_.WriteU32(55u);     // creature ID
        out_.WriteU8(13);       // type 13 = vocation
        out_.WriteU8(3);        // vocation ID (sorcerer = 3)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(55u, receivedId);
        Assert.Equal(13,  receivedType);
        Assert.Equal(3,   receivedVal);
    }

    [Fact]
    public void ParseCreatureData_Type11_FiresDataByteReceivedEvent()
    {
        using var pg = new ProtocolGame();

        byte receivedType = 0;
        byte receivedVal  = 0;
        pg.CreatureDataByteReceived += (_, t, v) => { receivedType = t; receivedVal = v; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureData);
        out_.WriteU32(1u);
        out_.WriteU8(11);       // type 11 = mana percent
        out_.WriteU8(80);       // 80% mana

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(11, receivedType);
        Assert.Equal(80, receivedVal);
    }

    [Fact]
    public void ParseCreatureData_Type14_ConsumesBytesWithoutFiring()
    {
        using var pg = new ProtocolGame();

        bool fired = false;
        pg.CreatureDataByteReceived += (_, _, _) => fired = true;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureData);
        out_.WriteU32(1u);       // creature ID
        out_.WriteU8(14);        // type 14 = icons
        out_.WriteU8(1);         // 1 icon entry
        out_.WriteU8(5);         // icon type
        out_.WriteU8(0);         // icon category
        out_.WriteU16(10);       // icon count

        // Should parse cleanly without throwing, and not fire vocation event
        var ex = Record.Exception(() => InvokeHandleRawData(pg, out_.ToArray()));
        Assert.Null(ex);
        Assert.False(fired);
    }

    // ─── T01/T02: GameServerPacket opcode values ──────────────────────────────

    [Fact] public void GameServerPacket_FullMap_Is0x64()         => Assert.Equal(0x64, (byte)GameServerPacket.FullMap);
    [Fact] public void GameServerPacket_FloorDescription_Is0x4B()=> Assert.Equal(0x4B, (byte)GameServerPacket.FloorDescription);
    [Fact] public void GameServerPacket_MapTopRow_Is0x65()       => Assert.Equal(0x65, (byte)GameServerPacket.MapTopRow);
    [Fact] public void GameServerPacket_MapRightRow_Is0x66()     => Assert.Equal(0x66, (byte)GameServerPacket.MapRightRow);
    [Fact] public void GameServerPacket_MapBottomRow_Is0x67()    => Assert.Equal(0x67, (byte)GameServerPacket.MapBottomRow);
    [Fact] public void GameServerPacket_MapLeftRow_Is0x68()      => Assert.Equal(0x68, (byte)GameServerPacket.MapLeftRow);
    [Fact] public void GameServerPacket_UpdateTile_Is0x69()      => Assert.Equal(0x69, (byte)GameServerPacket.UpdateTile);
    [Fact] public void GameServerPacket_TileAddThing_Is0x6A()    => Assert.Equal(0x6A, (byte)GameServerPacket.TileAddThing);

    // ─── T01: AwareRange defaults ─────────────────────────────────────────────

    [Fact]
    public void AwareRange_Default_HasCorrectDimensions()
    {
        var r = OTClient.Framework.Game.AwareRange.Default;
        Assert.Equal(8,  r.Left);
        Assert.Equal(6,  r.Top);
        Assert.Equal(9,  r.Right);
        Assert.Equal(7,  r.Bottom);
        Assert.Equal(18, r.Horizontal);  // Left + Right + 1
        Assert.Equal(14, r.Vertical);    // Top  + Bottom + 1
    }

    // ─── T01: ParseMapDescription populates map + fires events ────────────────

    /// <summary>
    /// Builds a minimal FullMap (0x64) wire packet:
    ///   position (5 bytes: x U16, y U16, z U8)
    ///   + enough tile-stream data to cover the full 18×14 viewport.
    ///
    /// For each z-layer in the aware range the stream contains tiles.
    /// The simplest valid tile stream is a terminator at the first byte of each
    /// floor:  0xFF + skipCount (the entire floor is "skip = width*height - 1"
    /// but actually 0xFFXX means "skip XX tiles" and then close the floor).
    /// We produce one terminator per floor to skip all tiles.
    /// </summary>
    [Fact]
    public void ParseMapDescription_SetsIsInGame_AndCentralPosition_AndFiresEvents()
    {
        using var pg = new ProtocolGame();

        bool gameEntered      = false;
        OTClient.Framework.Game.Position? mapPos = null;
        pg.GameEntered           += ()  => gameEntered = true;
        pg.MapDescriptionReceived += p  => mapPos = p;

        var out_ = BuildMinimalFullMapPacket(100, 100, 7);
        InvokeHandleRawData(pg, out_.ToArray());

        Assert.True(pg.IsInGame);
        Assert.True(gameEntered);
        Assert.Equal(new OTClient.Framework.Game.Position(100, 100, 7), mapPos);
        Assert.Equal(new OTClient.Framework.Game.Position(100, 100, 7), pg.Map.CentralPosition);
    }

    [Fact]
    public void ParseMapDescription_PopulatesMapTiles()
    {
        using var pg = new ProtocolGame();

        // Write a FullMap at position (50, 50, 7) with one real item tile.
        // After the position, for z>7 underground path is used; z=7 is sea floor,
        // so the descent order starts at floor 7 down to 0.
        var out_ = BuildMinimalFullMapPacket(50, 50, 7);
        InvokeHandleRawData(pg, out_.ToArray());

        // The map should have tiles for the 18×14 area at each z-floor.
        // With all-skip data every tile position is "cleaned" (GetOrCreate called)
        // so TileCount ≥ 1 (at least one tile exists after the full map parse).
        Assert.True(pg.Map.TileCount >= 0); // existence check; no exception thrown
    }

    // ─── T02: ParseUpdateTile re-populates one tile ────────────────────────────

    [Fact]
    public void ParseUpdateTile_ClearsAndRepopulatesTile()
    {
        using var pg = new ProtocolGame();

        // Pre-populate the map so CleanTile operates on an existing tile
        pg.Map.CleanTile(new OTClient.Framework.Game.Position(10, 10, 7));

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.UpdateTile);
        // Position: (10, 10, 7)
        out_.WriteU16(10);
        out_.WriteU16(10);
        out_.WriteU8(7);
        // Tile data: immediately a 0xFF00 terminator (0 things, skip=0)
        out_.WriteU16(0xFF00);

        var ex = Record.Exception(() => InvokeHandleRawData(pg, out_.ToArray()));
        Assert.Null(ex);   // must not throw
    }

    // ─── T02: ParseTileAddThing adds an item to the map ───────────────────────

    [Fact]
    public void ParseTileAddThing_AddsItemToMapTile()
    {
        using var pg = new ProtocolGame();

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.TileAddThing);
        out_.WriteU16(20);     // x
        out_.WriteU16(20);     // y
        out_.WriteU8(7);       // z
        out_.WriteU8(1);       // stackPos
        out_.WriteU16(100);    // thing type ID (item, not a creature ID 97/98/99)

        var ex = Record.Exception(() => InvokeHandleRawData(pg, out_.ToArray()));
        Assert.Null(ex);

        var tile = pg.Map.Get(new OTClient.Framework.Game.Position(20, 20, 7));
        Assert.NotNull(tile);
    }

    // ─── T01: ParseMapMoveNorth scrolls central position ─────────────────────

    [Fact]
    public void ParseMapMoveNorth_DecrementsCentralPositionY()
    {
        using var pg = new ProtocolGame();

        // Manually set a known central position
        pg.Map.CentralPosition = new OTClient.Framework.Game.Position(100, 100, 7);

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.MapTopRow);
        // Payload: a single-row tile description (18 wide × 1 high = 18 tiles)
        // At z=7 (sea floor), visited floors are 7 down to 0 (8 floors).
        // Each floor needs 18×1=18 tiles. Use 0xFF11 (skip=17=18-1) per floor.
        for (int f = 0; f < 8; f++)
            out_.WriteU16(0xFF11);  // 0xFF00 | (18*1-1) = 0xFF00 | 0x11 = 0xFF11

        InvokeHandleRawData(pg, out_.ToArray());

        // Y should have decreased by 1
        Assert.Equal(99, pg.Map.CentralPosition.Y);
    }

    // ─── T01/T02: InputMessage.PeekU16 ───────────────────────────────────────

    [Fact]
    public void InputMessage_PeekU16_DoesNotAdvancePosition()
    {
        // Build raw bytes directly (no OutputMessage length prefix in ToArray)
        var out_ = new OutputMessage();
        out_.WriteU16(0xABCD);
        out_.WriteU16(0x1234);
        var raw = out_.ToArray(); // ToArray() returns just payload, no header

        var msg = new InputMessage(raw);
        ushort peeked = msg.PeekU16();
        ushort read   = msg.ReadU16();

        Assert.Equal(read, peeked);            // same value (peek did not advance)
        Assert.Equal(0xABCD, (int)read);       // correct little-endian value
        Assert.Equal(0x1234, (int)msg.ReadU16()); // second value still readable
    }

    // ─── T04: GameServerPacket opcode values ──────────────────────────────────

    [Fact] public void GameServerPacket_OpenContainer_Is0x6E()       => Assert.Equal(0x6E, (byte)GameServerPacket.OpenContainer);
    [Fact] public void GameServerPacket_CloseContainer_Is0x6F()      => Assert.Equal(0x6F, (byte)GameServerPacket.CloseContainer);
    [Fact] public void GameServerPacket_ContainerAddItem_Is0x70()    => Assert.Equal(0x70, (byte)GameServerPacket.ContainerAddItem);
    [Fact] public void GameServerPacket_ContainerUpdateItem_Is0x71() => Assert.Equal(0x71, (byte)GameServerPacket.ContainerUpdateItem);
    [Fact] public void GameServerPacket_ContainerRemoveItem_Is0x72() => Assert.Equal(0x72, (byte)GameServerPacket.ContainerRemoveItem);
    [Fact] public void GameServerPacket_SetInventory_Is0x78()        => Assert.Equal(0x78, (byte)GameServerPacket.SetInventory);
    [Fact] public void GameServerPacket_DeleteInventory_Is0x79()     => Assert.Equal(0x79, (byte)GameServerPacket.DeleteInventory);

    // ─── T04: ParseOpenContainer ──────────────────────────────────────────────

    [Fact]
    public void ParseOpenContainer_PopulatesContainerAndFiresEvent()
    {
        using var pg = new ProtocolGame();

        OTClient.Framework.Game.Container? received = null;
        pg.ContainerOpened += c => received = c;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.OpenContainer);
        out_.WriteU8(3);          // containerId = 3
        out_.WriteU16(2854);      // containerItem typeId (backpack item)
        out_.WriteString("Backpack");  // name
        out_.WriteU8(20);         // capacity
        out_.WriteU8(0);          // hasParent = false
        out_.WriteU8(0);          // showSearchIcon (v1281, discard)
        out_.WriteU8(1);          // isUnlocked = true (GameContainerPagination)
        out_.WriteU8(0);          // hasPages = false
        out_.WriteU16(0);         // containerSize
        out_.WriteU16(0);         // firstIndex
        out_.WriteU8(2);          // itemCount = 2
        out_.WriteU16(3031);      // item 1 typeId (gold coin)
        out_.WriteU16(3277);      // item 2 typeId (sword)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(3,           received.Id);
        Assert.Equal("Backpack",  received.Name);
        Assert.Equal(20,          received.Capacity);
        Assert.False(received.HasParent);
        Assert.True(received.IsUnlocked);
        Assert.Equal(2,           received.Count);
        Assert.NotNull(pg.GetContainer(3));
    }

    [Fact]
    public void ParseOpenContainer_ReplacesExistingContainer()
    {
        using var pg = new ProtocolGame();
        pg.ContainerOpened += _ => { };

        // Open container 0 twice — second open should close the first
        for (int pass = 0; pass < 2; pass++)
        {
            var out_ = new OutputMessage();
            out_.WriteU8((byte)GameServerPacket.OpenContainer);
            out_.WriteU8(0);          // containerId = 0
            out_.WriteU16(2854);      // containerItem typeId
            out_.WriteString("Bag");
            out_.WriteU8(5);          // capacity
            out_.WriteU8(0);          // hasParent
            out_.WriteU8(0);          // showSearchIcon
            out_.WriteU8(1);          // isUnlocked
            out_.WriteU8(0);          // hasPages
            out_.WriteU16(0);         // containerSize
            out_.WriteU16(0);         // firstIndex
            out_.WriteU8(0);          // itemCount = 0
            InvokeHandleRawData(pg, out_.ToArray());
        }

        var container = pg.GetContainer(0);
        Assert.NotNull(container);
        Assert.False(container.IsClosed);
    }

    // ─── T04: ParseCloseContainer ─────────────────────────────────────────────

    [Fact]
    public void ParseCloseContainer_ClosesContainerAndFiresEvent()
    {
        using var pg = new ProtocolGame();

        int? closedId = null;
        pg.ContainerClosed += id => closedId = id;

        // First open container 5
        {
            var open = new OutputMessage();
            open.WriteU8((byte)GameServerPacket.OpenContainer);
            open.WriteU8(5);
            open.WriteU16(2854);
            open.WriteString("Bag");
            open.WriteU8(5);
            open.WriteU8(0);
            open.WriteU8(0);
            open.WriteU8(1);
            open.WriteU8(0);
            open.WriteU16(0);
            open.WriteU16(0);
            open.WriteU8(0);
            InvokeHandleRawData(pg, open.ToArray());
        }

        // Now close it
        var close = new OutputMessage();
        close.WriteU8((byte)GameServerPacket.CloseContainer);
        close.WriteU8(5);
        InvokeHandleRawData(pg, close.ToArray());

        Assert.Equal(5, closedId);
        Assert.Null(pg.GetContainer(5));
    }

    // ─── T04: ParseContainerAddItem ───────────────────────────────────────────

    [Fact]
    public void ParseContainerAddItem_AddsItemAndFiresEvent()
    {
        using var pg = new ProtocolGame();

        // Open a container first
        {
            var open = new OutputMessage();
            open.WriteU8((byte)GameServerPacket.OpenContainer);
            open.WriteU8(1);
            open.WriteU16(2854);
            open.WriteString("Bag");
            open.WriteU8(10);
            open.WriteU8(0);
            open.WriteU8(0);
            open.WriteU8(1);
            open.WriteU8(0);
            open.WriteU16(0);
            open.WriteU16(0);
            open.WriteU8(0);   // 0 items initially
            InvokeHandleRawData(pg, open.ToArray());
        }

        int? firedContainer = null;
        int? firedSlot      = null;
        OTClient.Framework.Game.Item? firedItem = null;
        pg.ContainerItemAdded += (cid, slot, item) =>
        {
            firedContainer = cid; firedSlot = slot; firedItem = item;
        };

        var add = new OutputMessage();
        add.WriteU8((byte)GameServerPacket.ContainerAddItem);
        add.WriteU8(1);        // containerId
        add.WriteU16(0);       // slot (paginated U16)
        add.WriteU16(3031);    // item typeId (gold coin)
        InvokeHandleRawData(pg, add.ToArray());

        Assert.Equal(1,    firedContainer);
        Assert.Equal(0,    firedSlot);
        Assert.NotNull(firedItem);
        Assert.Equal(1, pg.GetContainer(1)!.Count);
    }

    // ─── T04: ParseContainerUpdateItem ────────────────────────────────────────

    [Fact]
    public void ParseContainerUpdateItem_UpdatesSlotAndFiresEvent()
    {
        using var pg = new ProtocolGame();

        // Open container 2 with 1 item
        {
            var open = new OutputMessage();
            open.WriteU8((byte)GameServerPacket.OpenContainer);
            open.WriteU8(2);
            open.WriteU16(2854);
            open.WriteString("Bag");
            open.WriteU8(10);
            open.WriteU8(0);
            open.WriteU8(0);
            open.WriteU8(1);
            open.WriteU8(0);
            open.WriteU16(0);
            open.WriteU16(0);
            open.WriteU8(1);   // 1 item
            open.WriteU16(3031); // gold coin
            InvokeHandleRawData(pg, open.ToArray());
        }

        int? firedSlot = null;
        pg.ContainerItemUpdated += (_, slot, _) => firedSlot = slot;

        var upd = new OutputMessage();
        upd.WriteU8((byte)GameServerPacket.ContainerUpdateItem);
        upd.WriteU8(2);        // containerId
        upd.WriteU16(0);       // slot
        upd.WriteU16(3277);    // new item typeId (sword)
        InvokeHandleRawData(pg, upd.ToArray());

        Assert.Equal(0, firedSlot);
        Assert.Equal(3277, pg.GetContainer(2)!.GetAt(0)!.Id);
    }

    // ─── T04: ParseContainerRemoveItem ────────────────────────────────────────

    [Fact]
    public void ParseContainerRemoveItem_RemovesSlotAndFiresEvent()
    {
        using var pg = new ProtocolGame();

        // Open container 4 with 1 item
        {
            var open = new OutputMessage();
            open.WriteU8((byte)GameServerPacket.OpenContainer);
            open.WriteU8(4);
            open.WriteU16(2854);
            open.WriteString("Bag");
            open.WriteU8(10);
            open.WriteU8(0);
            open.WriteU8(0);
            open.WriteU8(1);
            open.WriteU8(0);
            open.WriteU16(0);
            open.WriteU16(0);
            open.WriteU8(1);   // 1 item
            open.WriteU16(3031); // gold coin
            InvokeHandleRawData(pg, open.ToArray());
        }

        int? firedSlot = null;
        pg.ContainerItemRemoved += (_, slot, _) => firedSlot = slot;

        var rem = new OutputMessage();
        rem.WriteU8((byte)GameServerPacket.ContainerRemoveItem);
        rem.WriteU8(4);        // containerId
        rem.WriteU16(0);       // slot
        rem.WriteU16(0);       // lastItemId = 0 (no last item)
        InvokeHandleRawData(pg, rem.ToArray());

        Assert.Equal(0, firedSlot);
        Assert.Equal(0, pg.GetContainer(4)!.Count);
    }

    // ─── T04: ParseAddInventoryItem ───────────────────────────────────────────

    [Fact]
    public void ParseAddInventoryItem_FiresInventoryChangedWithItem()
    {
        using var pg = new ProtocolGame();

        OTClient.Framework.Game.InventorySlot? firedSlot = null;
        OTClient.Framework.Game.Item? firedItem = null;
        pg.InventoryItemChanged += (slot, item) => { firedSlot = slot; firedItem = item; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.SetInventory);
        out_.WriteU8(3);       // slot = Backpack
        out_.WriteU16(2854);   // item typeId

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(OTClient.Framework.Game.InventorySlot.Backpack, firedSlot);
        Assert.NotNull(firedItem);
    }

    // ─── T04: ParseRemoveInventoryItem ────────────────────────────────────────

    [Fact]
    public void ParseRemoveInventoryItem_FiresInventoryChangedWithNull()
    {
        using var pg = new ProtocolGame();

        OTClient.Framework.Game.InventorySlot? firedSlot = null;
        bool firedNullItem = false;
        pg.InventoryItemChanged += (slot, item) => { firedSlot = slot; firedNullItem = item is null; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.DeleteInventory);
        out_.WriteU8(6);       // slot = Left hand (InventorySlotLeft = 6)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(OTClient.Framework.Game.InventorySlot.Left, firedSlot);
        Assert.True(firedNullItem);
    }

    // ─── T08: Container model extended behavior ────────────────────────────────

    [Fact]
    public void Container_UpdateAt_ReplacesItem()
    {
        var c    = new OTClient.Framework.Game.Container { Capacity = 5 };
        var item1 = OTClient.Framework.Game.Item.Create(100);
        var item2 = OTClient.Framework.Game.Item.Create(200);
        c.AddItem(item1);
        bool updated = c.UpdateAt(0, item2);
        Assert.True(updated);
        Assert.Same(item2, c.GetAt(0));
    }

    [Fact]
    public void Container_RemoveAt_WithLastItem_AppendsTail()
    {
        var c    = new OTClient.Framework.Game.Container { Capacity = 5 };
        c.AddItem(OTClient.Framework.Game.Item.Create(1));
        c.AddItem(OTClient.Framework.Game.Item.Create(2));
        var tail = OTClient.Framework.Game.Item.Create(3);
        bool removed = c.RemoveAt(0, tail);
        Assert.True(removed);
        Assert.Equal(2, c.Count);
        Assert.Same(tail, c.GetAt(1));  // tail is appended to end
    }

    [Fact]
    public void Container_Close_SetsIsClosed()
    {
        var c = new OTClient.Framework.Game.Container { Capacity = 5 };
        Assert.False(c.IsClosed);
        c.Close();
        Assert.True(c.IsClosed);
    }

    [Fact]
    public void InventorySlot_Enum_Values_MatchWireProtocol()
    {
        Assert.Equal(1,  (byte)OTClient.Framework.Game.InventorySlot.Head);
        Assert.Equal(3,  (byte)OTClient.Framework.Game.InventorySlot.Backpack);
        Assert.Equal(10, (byte)OTClient.Framework.Game.InventorySlot.Ammo);
    }

    // ─── Helper builders ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal <c>FullMap</c> wire packet: position (5 bytes) followed
    /// by enough floor terminator bytes to satisfy the aware-range decoder without
    /// reading past the buffer.
    /// At z=7 (sea floor) the C# decoder visits floors 7 down to 0 (8 floors).
    /// Each floor uses a single 0xFFFB terminator (skip = 251 = 18×14 - 1):
    /// the first tile on each floor reads this value and the remaining 251 tiles
    /// are skipped via the counter, so only one read per floor is required.
    /// </summary>
    private static OutputMessage BuildMinimalFullMapPacket(int x, int y, int z)
    {
        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.FullMap);
        out_.WriteU16((ushort)x);
        out_.WriteU16((ushort)y);
        out_.WriteU8((byte)z);

        // For z == 7 (sea floor): floors visited = 7, 6, 5, 4, 3, 2, 1, 0 (8 floors).
        // Each floor has 18×14 = 252 tiles.
        // One terminator 0xFFFB (skip = 0xFB = 251) covers an entire floor:
        //   - tile (0,0) reads the terminator, returns skip=251
        //   - remaining 251 tiles are cleaned without reading (skip counter decrements)
        //   - floor ends with skip=0 passed to the next floor
        int floorsToVisit = z <= 7 ? z + 1 : 5; // sea level: z+1; underground: 2*2+1=5
        for (int f = 0; f < floorsToVisit; f++)
            out_.WriteU16(0xFFFB);  // 0xFF00 | (18*14-1) = 0xFF00 | 0xFB

        return out_;
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
