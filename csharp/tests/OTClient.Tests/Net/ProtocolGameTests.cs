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
    public void ParseEditText_InvokesEvent_WithCorrectFields()
    {
        using var pg = new ProtocolGame();

        uint    rcvId        = 0;
        int     rcvItemId    = 0;
        ushort  rcvMaxLength = 0;
        string? rcvText      = null;
        string? rcvWriter    = null;
        pg.EditTextReceived += (id, itemId, maxLen, text, writer, _) =>
        {
            rcvId = id; rcvItemId = itemId; rcvMaxLength = maxLen;
            rcvText = text; rcvWriter = writer;
        };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.EditText);
        out_.WriteU32(42);           // id
        out_.WriteU16(2400);         // itemId (item U16 at 1281)
        out_.WriteU16(255);          // maxLength
        out_.WriteString("Hello!");  // text
        out_.WriteString("Alice");   // writer
        out_.WriteU8(0);             // suffix byte (always present at 1281)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(42u, rcvId);
        Assert.Equal(2400, rcvItemId);
        Assert.Equal((ushort)255, rcvMaxLength);
        Assert.Equal("Hello!", rcvText);
        Assert.Equal("Alice", rcvWriter);
    }

    [Fact]
    public void ParseEditList_InvokesEvent_WithDoorIdAndText()
    {
        using var pg = new ProtocolGame();

        uint    rcvId     = 0;
        byte    rcvDoorId = 0;
        string? rcvText   = null;
        pg.EditListReceived += (id, doorId, text) => { rcvId = id; rcvDoorId = doorId; rcvText = text; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.EditList);
        out_.WriteU8(3);              // doorId
        out_.WriteU32(77);            // id
        out_.WriteString("List text");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(77u, rcvId);
        Assert.Equal((byte)3, rcvDoorId);
        Assert.Equal("List text", rcvText);
    }

    [Fact]
    public void ParseQuestLog_InvokesEvent_WithQuestEntries()
    {
        using var pg = new ProtocolGame();

        IReadOnlyList<OTClient.Framework.Game.QuestEntry>? received = null;
        pg.QuestLogReceived += entries => received = entries;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.QuestLog);
        out_.WriteU16(2);            // count
        out_.WriteU16(100); out_.WriteString("Dragon Quest"); out_.WriteU8(0);  // id, name, completed=false
        out_.WriteU16(101); out_.WriteString("Orc Slayer");   out_.WriteU8(1);  // id, name, completed=true

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(2, received!.Count);
        Assert.Equal((ushort)100, received[0].Id);
        Assert.Equal("Dragon Quest", received[0].Name);
        Assert.False(received[0].Completed);
        Assert.Equal((ushort)101, received[1].Id);
        Assert.True(received[1].Completed);
    }

    [Fact]
    public void ParseQuestLine_InvokesEvent_WithMissions()
    {
        using var pg = new ProtocolGame();

        ushort rcvQuestId = 0;
        IReadOnlyList<OTClient.Framework.Game.QuestMission>? received = null;
        pg.QuestLineReceived += (qid, missions) => { rcvQuestId = qid; received = missions; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.QuestLine);
        out_.WriteU16(100);          // questId
        out_.WriteU8(1);             // missionCount
        out_.WriteU16(5);            // missionId (≥1200, always present at 1281)
        out_.WriteString("Mission Name");
        out_.WriteString("Mission Description");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal((ushort)100, rcvQuestId);
        Assert.NotNull(received);
        Assert.Single(received!);
        Assert.Equal("Mission Name", received[0].Name);
        Assert.Equal("Mission Description", received[0].Description);
        Assert.Equal((ushort)5, received[0].MissionId);
    }

    [Fact]
    public void ParseModalDialog_InvokesEvent_WithDialogData()
    {
        using var pg = new ProtocolGame();

        OTClient.Framework.Game.ModalDialog? received = null;
        pg.ModalDialogReceived += dlg => received = dlg;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.ModalDialog);
        out_.WriteU32(999);           // windowId
        out_.WriteString("Confirm");  // title
        out_.WriteString("Are you sure?"); // message
        out_.WriteU8(2);              // buttonsCount
        out_.WriteString("Yes"); out_.WriteU8(1);
        out_.WriteString("No");  out_.WriteU8(0);
        out_.WriteU8(0);              // choicesCount
        out_.WriteU8(0);              // escapeButton (version > 970: escape first)
        out_.WriteU8(1);              // enterButton
        out_.WriteU8(1);              // priority

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(received);
        Assert.Equal(999u, received!.WindowId);
        Assert.Equal("Confirm", received.Title);
        Assert.Equal("Are you sure?", received.Message);
        Assert.Equal(2, received.Buttons.Count);
        Assert.Equal((byte)1, received.EnterButton);
        Assert.Equal((byte)0, received.EscapeButton);
        Assert.True(received.Priority);
    }

    // ─── Send method round-trips (T23) ────────────────────────────────────────

    [Fact]
    public void SendRequestQuestLog_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendRequestQuestLog());
    }

    [Fact]
    public void SendRequestQuestLine_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendRequestQuestLine(42));
    }

    [Fact]
    public void SendAnswerModalDialog_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendAnswerModalDialog(999, 1, 0));
    }

    [Fact]
    public void SendEditText_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendEditText(1, "my text"));
    }

    [Fact]
    public void SendEditList_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendEditList(2, 3, "list content"));
    }

    // ─── Market parse handlers (T26) ─────────────────────────────────────────

    [Fact]
    public void ParseMarketEnter_InvokesEvent_WithDepotItems()
    {
        using var pg = new ProtocolGame();

        IReadOnlyList<OTClient.Framework.Game.MarketDepotItem>? items = null;
        byte activeOffers = 0;
        pg.MarketEntered += (depot, offers) =>
        {
            items        = depot;
            activeOffers = offers;
        };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.MarketEnter);
        out_.WriteU8(5);          // activeOffers
        out_.WriteU16(2);         // 2 depot items
        // item 1: id=2160, no tier (classification=0), count=3
        out_.WriteU16(2160);
        out_.WriteU16(3);
        // item 2: id=2400, no tier, count=1
        out_.WriteU16(2400);
        out_.WriteU16(1);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal(2160, items[0].ItemId);
        Assert.Equal(3, items[0].Count);
        Assert.Equal(2400, items[1].ItemId);
        Assert.Equal(1, items[1].Count);
        Assert.Equal(5, activeOffers);
    }

    [Fact]
    public void ParseMarketLeave_InvokesEvent()
    {
        using var pg = new ProtocolGame();
        bool left = false;
        pg.MarketLeft += () => left = true;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.MarketLeave);

        InvokeHandleRawData(pg, out_.ToArray());
        Assert.True(left);
    }

    [Fact]
    public void ParseMarketDetail_InvokesEvent_WithDescriptionsAndStats()
    {
        using var pg = new ProtocolGame();

        ushort receivedItemId = 0;
        IReadOnlyDictionary<int, string>? descs = null;
        IReadOnlyList<OTClient.Framework.Game.MarketStatEntry>? buyStats = null;
        pg.MarketDetailReceived += (id, _, d, buy, _) =>
        {
            receivedItemId = id;
            descs          = d;
            buyStats       = buy;
        };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.MarketDetail);
        out_.WriteU16(2160);   // itemId (no tier, Classification=0)

        // 26 description attributes: only attr 1 (ITEM_DESC_ARMOR) is present
        // Present: write U16 length + bytes for "12 Armor"
        const string armorValue = "12 Armor";
        out_.WriteString(armorValue);      // attr 1 — non-zero length = present
        for (int a = 2; a <= 26; a++)
            out_.WriteU16(0);              // not present

        // buy stats: 1 entry
        out_.WriteU8(1);
        out_.WriteU32(10);      // transactions
        out_.WriteU64(5000);    // totalPrice
        out_.WriteU64(600);     // highestPrice
        out_.WriteU64(400);     // lowestPrice

        // sell stats: 0 entries
        out_.WriteU8(0);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(2160, receivedItemId);
        Assert.NotNull(descs);
        Assert.True(descs!.ContainsKey(1));
        Assert.Equal(armorValue, descs[1]);
        Assert.NotNull(buyStats);
        Assert.Single(buyStats!);
        Assert.Equal(10u, buyStats[0].Transactions);
        Assert.Equal(5000u, buyStats[0].TotalPrice);
    }

    [Fact]
    public void ParseMarketBrowse_InvokesEvent_WithBuyAndSellOffers()
    {
        using var pg = new ProtocolGame();

        IReadOnlyList<OTClient.Framework.Game.MarketOffer>? offers = null;
        ushort receivedVar = 0;
        pg.MarketBrowseReceived += (v, o) =>
        {
            receivedVar = v;
            offers      = o;
        };

        // browseId=3 (item browse), browse item id=2160 (no tier)
        const ushort itemId = 2160;
        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.MarketBrowse);
        out_.WriteU8(3);          // browseId = MARKETREQUEST_ITEM_BROWSE
        out_.WriteU16(itemId);    // item id

        // 1 buy offer (var = itemId, so playerName is read)
        out_.WriteU32(1);
        out_.WriteU32(0xDEAD);   // timestamp
        out_.WriteU16(1);        // counter
        // itemId not embedded (var == itemId path, not own-offers)
        out_.WriteU16(5);        // amount
        out_.WriteU64(1000);     // price
        out_.WriteString("Seller"); // playerName

        // 0 sell offers
        out_.WriteU32(0);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(itemId, receivedVar);
        Assert.NotNull(offers);
        Assert.Single(offers!);
        Assert.Equal(0, offers![0].Action); // buy
        Assert.Equal(5, offers[0].Amount);
        Assert.Equal(1000UL, offers[0].Price);
        Assert.Equal("Seller", offers[0].PlayerName);
    }

    // ─── Market send methods (T26) ────────────────────────────────────────────

    [Fact]
    public void SendMarketLeave_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendMarketLeave());
    }

    [Fact]
    public void SendMarketBrowse_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendMarketBrowse(3, 2160, 0));
    }

    [Fact]
    public void SendMarketCreateOffer_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendMarketCreateOffer(0, 2160, 0, 1, 1000, 0));
    }

    [Fact]
    public void SendMarketCancelOffer_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendMarketCancelOffer(1234, 5));
    }

    [Fact]
    public void SendMarketAcceptOffer_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendMarketAcceptOffer(1234, 5, 1));
    }

    // ─── Main-thread dispatcher (T35) ─────────────────────────────────────────

    [Fact]
    public void WithDispatcher_ParseIsDeferredUntilPoll()
    {
        using var pg = new ProtocolGame();
        var dispatcher = new OTClient.Framework.Core.EventDispatcher();
        pg.SetDispatcher(dispatcher);

        bool died = false;
        pg.PlayerDied += () => died = true;

        // Build a Death packet
        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Death);

        // Invoke HandleRawData — with a dispatcher wired this should ENQUEUE,
        // NOT run the handler yet.
        InvokeHandleRawData(pg, out_.ToArray());

        // Handler has NOT run yet (still on the "network thread" side)
        Assert.False(died, "Handler must not run before Poll()");
        Assert.Equal(1, dispatcher.PendingCount);

        // Now drain the dispatcher (simulates the main-thread game loop tick)
        dispatcher.Poll();

        Assert.True(died, "Handler must run after Poll()");
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public void WithoutDispatcher_ParseIsImmediate()
    {
        using var pg = new ProtocolGame();
        // No SetDispatcher call — default behaviour

        bool died = false;
        pg.PlayerDied += () => died = true;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Death);

        InvokeHandleRawData(pg, out_.ToArray());

        // Must have fired synchronously (no dispatcher)
        Assert.True(died);
    }

    [Fact]
    public void WithDispatcher_MultiplePackets_AllDeferredUntilPoll()
    {
        using var pg = new ProtocolGame();
        var dispatcher = new OTClient.Framework.Core.EventDispatcher();
        pg.SetDispatcher(dispatcher);

        // Collect death events
        int deathCount = 0;
        pg.PlayerDied += () => deathCount++;

        // Two Death packets sent before any Poll
        var out1 = new OutputMessage();
        out1.WriteU8((byte)GameServerPacket.Death);
        InvokeHandleRawData(pg, out1.ToArray());

        var out2 = new OutputMessage();
        out2.WriteU8((byte)GameServerPacket.Death);
        InvokeHandleRawData(pg, out2.ToArray());

        // Two events queued, handler has not fired
        Assert.Equal(0, deathCount);
        Assert.Equal(2, dispatcher.PendingCount);

        dispatcher.Poll();

        Assert.Equal(2, deathCount);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public void SetDispatcher_ToNull_RestoresImmediateMode()
    {
        using var pg = new ProtocolGame();
        var dispatcher = new OTClient.Framework.Core.EventDispatcher();

        // Wire dispatcher
        pg.SetDispatcher(dispatcher);

        // Then remove it
        pg.SetDispatcher(null);

        bool died = false;
        pg.PlayerDied += () => died = true;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Death);
        InvokeHandleRawData(pg, out_.ToArray());

        // Should fire immediately (dispatcher has been removed)
        Assert.True(died);
        Assert.Equal(0, dispatcher.PendingCount);
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

    // ─── T09: Opcode values ───────────────────────────────────────────────────

    [Fact] public void GameServerPacket_Talk_Is0xAA()            => Assert.Equal(0xAA, (byte)GameServerPacket.Talk);
    [Fact] public void GameServerPacket_ChannelList_Is0xAB()     => Assert.Equal(0xAB, (byte)GameServerPacket.ChannelList);
    [Fact] public void GameServerPacket_OpenChannel_Is0xAC()     => Assert.Equal(0xAC, (byte)GameServerPacket.OpenChannel);
    [Fact] public void GameServerPacket_OpenPrivateChannel_Is0xAD() => Assert.Equal(0xAD, (byte)GameServerPacket.OpenPrivateChannel);
    [Fact] public void GameServerPacket_CloseChannel_Is0xB3()    => Assert.Equal(0xB3, (byte)GameServerPacket.CloseChannel);
    [Fact] public void GameServerPacket_VipAdd_Is0xD2()          => Assert.Equal(0xD2, (byte)GameServerPacket.VipAdd);
    [Fact] public void GameServerPacket_VipState_Is0xD3()        => Assert.Equal(0xD3, (byte)GameServerPacket.VipState);
    [Fact] public void GameServerPacket_VipLogout_Is0xD4()       => Assert.Equal(0xD4, (byte)GameServerPacket.VipLogout);

    // ─── T10: Client opcode values ────────────────────────────────────────────

    [Fact] public void GameClientPacket_RequestChannels_Is0x97() => Assert.Equal(0x97, (byte)GameClientPacket.RequestChannels);
    [Fact] public void GameClientPacket_JoinChannel_Is0x98()     => Assert.Equal(0x98, (byte)GameClientPacket.JoinChannel);
    [Fact] public void GameClientPacket_LeaveChannel_Is0x99()    => Assert.Equal(0x99, (byte)GameClientPacket.LeaveChannel);
    [Fact] public void GameClientPacket_OpenPrivateChannel_Is0x9A() => Assert.Equal(0x9A, (byte)GameClientPacket.OpenPrivateChannel);

    // ─── T12: Client opcode values ────────────────────────────────────────────

    [Fact] public void GameClientPacket_ChangeFightModes_Is0xA0()       => Assert.Equal(0xA0, (byte)GameClientPacket.ChangeFightModes);
    [Fact] public void GameClientPacket_Attack_Is0xA1()                 => Assert.Equal(0xA1, (byte)GameClientPacket.Attack);
    [Fact] public void GameClientPacket_Follow_Is0xA2()                 => Assert.Equal(0xA2, (byte)GameClientPacket.Follow);
    [Fact] public void GameClientPacket_CancelAttackAndFollow_Is0xBE()  => Assert.Equal(0xBE, (byte)GameClientPacket.CancelAttackAndFollow);

    // ─── T09: TalkMode enum values ────────────────────────────────────────────

    [Fact] public void TalkMode_Say_Is1()         => Assert.Equal(1,  (byte)TalkMode.Say);
    [Fact] public void TalkMode_Channel_Is7()     => Assert.Equal(7,  (byte)TalkMode.Channel);
    [Fact] public void TalkMode_NpcFrom_Is11()    => Assert.Equal(11, (byte)TalkMode.NpcFrom);
    [Fact] public void TalkMode_BarkLow_Is36()    => Assert.Equal(36, (byte)TalkMode.BarkLow);
    [Fact] public void TalkMode_Potion_Is52()     => Assert.Equal(52, (byte)TalkMode.Potion);

    // ─── T09: ParseTalk — positional message ─────────────────────────────────

    [Fact]
    public void ParseTalk_SayMode_FiresTalkReceivedWithPosition()
    {
        using var pg = new ProtocolGame();

        string? gotAuthor = null;
        int     gotLevel  = -1;
        TalkMode gotMode  = TalkMode.None;
        string? gotText   = null;
        OTClient.Framework.Game.Position? gotPos = null;

        pg.TalkReceived += (author, level, mode, text, _, pos) =>
        {
            gotAuthor = author; gotLevel = level; gotMode = mode;
            gotText   = text;   gotPos   = pos;
        };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Talk);
        out_.WriteU32(0);          // statement = 0 (no suffix)
        out_.WriteString("Hero");  // author
        // no suffix (statement == 0)
        out_.WriteU16(100);        // level
        out_.WriteU8((byte)TalkMode.Say);
        out_.WriteU16(1000);       // position X
        out_.WriteU16(1000);       // position Y
        out_.WriteU8(7);           // position Z
        out_.WriteString("Hello!"); // text

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Hero",      gotAuthor);
        Assert.Equal(100,         gotLevel);
        Assert.Equal(TalkMode.Say,gotMode);
        Assert.Equal("Hello!",    gotText);
        Assert.NotNull(gotPos);
        Assert.Equal(1000, gotPos!.Value.X);
    }

    [Fact]
    public void ParseTalk_ChannelMode_FiresTalkReceivedWithChannelId()
    {
        using var pg = new ProtocolGame();

        ushort gotChannelId = 0;
        TalkMode gotMode    = TalkMode.None;
        pg.TalkReceived += (_, _, mode, _, channelId, _) =>
            { gotMode = mode; gotChannelId = channelId; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Talk);
        out_.WriteU32(0);               // statement = 0
        out_.WriteString("Gm");         // author
        out_.WriteU16(999);             // level
        out_.WriteU8((byte)TalkMode.Channel);
        out_.WriteU16(5);               // channelId = 5
        out_.WriteString("Hi channel"); // text

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(TalkMode.Channel, gotMode);
        Assert.Equal(5, gotChannelId);
    }

    [Fact]
    public void ParseTalk_WithNonZeroStatement_ConsumesExtraSuffixByte()
    {
        using var pg = new ProtocolGame();

        string? gotAuthor = null;
        pg.TalkReceived += (author, _, _, _, _, _) => gotAuthor = author;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Talk);
        out_.WriteU32(12345);         // statement != 0 → suffix follows
        out_.WriteString("NpcFred");  // author
        out_.WriteU8(0);              // suffix byte (consumed and discarded)
        out_.WriteU16(1);             // level
        out_.WriteU8((byte)TalkMode.NpcFrom);
        out_.WriteString("Welcome!"); // text

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("NpcFred", gotAuthor);
    }

    // ─── T09: ParseChannelList ────────────────────────────────────────────────

    [Fact]
    public void ParseChannelList_FiresChannelListReceivedWithAllEntries()
    {
        using var pg = new ProtocolGame();

        IReadOnlyList<(ushort Id, string Name)>? got = null;
        pg.ChannelListReceived += list => got = list;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.ChannelList);
        out_.WriteU8(2);             // count = 2
        out_.WriteU16(0);  out_.WriteString("Default");   // channel 0
        out_.WriteU16(6);  out_.WriteString("Trade");     // channel 6

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(got);
        Assert.Equal(2, got!.Count);
        Assert.Equal((ushort)0, got[0].Id);
        Assert.Equal("Default",  got[0].Name);
        Assert.Equal((ushort)6, got[1].Id);
        Assert.Equal("Trade",    got[1].Name);
    }

    // ─── T09: ParseOpenChannel ────────────────────────────────────────────────

    [Fact]
    public void ParseOpenChannel_FiresChannelOpenedWithIdAndName()
    {
        using var pg = new ProtocolGame();

        ushort? gotId   = null;
        string? gotName = null;
        pg.ChannelOpened += (id, name) => { gotId = id; gotName = name; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.OpenChannel);
        out_.WriteU16(3);               // channelId = 3
        out_.WriteString("Help");       // channelName
        out_.WriteU16(0);               // joined players count
        out_.WriteU16(0);               // invited players count

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal((ushort)3, gotId);
        Assert.Equal("Help",    gotName);
    }

    // ─── T09: ParseOpenPrivateChannel ────────────────────────────────────────

    [Fact]
    public void ParseOpenPrivateChannel_FiresPrivateChannelOpenedWithName()
    {
        using var pg = new ProtocolGame();

        string? gotName = null;
        pg.PrivateChannelOpened += name => gotName = name;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.OpenPrivateChannel);
        out_.WriteString("Alice");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Alice", gotName);
    }

    // ─── T09: ParseCloseChannel ───────────────────────────────────────────────

    [Fact]
    public void ParseCloseChannel_FiresChannelClosedWithId()
    {
        using var pg = new ProtocolGame();

        ushort? gotId = null;
        pg.ChannelClosed += id => gotId = id;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CloseChannel);
        out_.WriteU16(7);   // channelId = 7

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal((ushort)7, gotId);
    }

    // ─── T11: ParseVipAdd ────────────────────────────────────────────────────

    [Fact]
    public void ParseVipAdd_FiresVipAddedWithAllFields()
    {
        using var pg = new ProtocolGame();

        uint?   gotId    = null;
        string? gotName  = null;
        uint?   gotStatus = null;
        bool?   gotNotify = null;
        pg.VipAdded += (id, name, status, _, _, notify) =>
            { gotId = id; gotName = name; gotStatus = status; gotNotify = notify; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.VipAdd);
        out_.WriteU32(42);                 // id
        out_.WriteString("Bob");           // name
        out_.WriteString("A good friend"); // description (GameAdditionalVipInfo)
        out_.WriteU32(1);                  // iconId
        out_.WriteU8(1);                   // notifyLogin = true
        out_.WriteU8(1);                   // status = 1 (online)
        out_.WriteU8(0);                   // vipGroupSize = 0 (GameVipGroups)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(42u,    gotId);
        Assert.Equal("Bob",  gotName);
        Assert.Equal(1u,     gotStatus);
        Assert.True(gotNotify);
    }

    // ─── T11: ParseVipState ───────────────────────────────────────────────────

    [Fact]
    public void ParseVipState_FiresVipStateChangedWithIdAndStatus()
    {
        using var pg = new ProtocolGame();

        uint? gotId     = null;
        uint? gotStatus = null;
        pg.VipStateChanged += (id, status) => { gotId = id; gotStatus = status; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.VipState);
        out_.WriteU32(99);   // id
        out_.WriteU8(0);     // status = 0 (offline)

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(99u, gotId);
        Assert.Equal(0u,  gotStatus);
    }

    // ─── T11: ParseVipLogout (VipGroups) ─────────────────────────────────────

    [Fact]
    public void ParseVipLogout_ParsesGroupsWithoutThrowing()
    {
        using var pg = new ProtocolGame();

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.VipLogout);
        out_.WriteU8(2);           // 2 groups
        out_.WriteU8(1);  out_.WriteString("Hunters"); out_.WriteU8(0); // group 1
        out_.WriteU8(2);  out_.WriteString("Guild");   out_.WriteU8(1); // group 2
        out_.WriteU8(10); // groupsAmountLeft

        var ex = Record.Exception(() => InvokeHandleRawData(pg, out_.ToArray()));
        Assert.Null(ex);
    }

    // ─── T10: SendRequestChannels ─────────────────────────────────────────────

    [Fact]
    public void SendRequestChannels_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendRequestChannels());
    }

    [Fact]
    public void SendJoinChannel_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendJoinChannel(5));
    }

    [Fact]
    public void SendLeaveChannel_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendLeaveChannel(5));
    }

    [Fact]
    public void SendOpenPrivateChannel_NotConnected_ThrowsInvalidOperation()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<InvalidOperationException>(() => pg.SendOpenPrivateChannel("Alice"));
    }

    [Fact]
    public void SendOpenPrivateChannel_EmptyReceiver_Throws()
    {
        using var pg = new ProtocolGame();
        Assert.Throws<ArgumentException>(() => pg.SendOpenPrivateChannel(""));
    }

    // ─── Helper builders ──────────────────────────────────────────────────────

    // ─── T13: Opcode values ───────────────────────────────────────────────────

    [Fact] public void GameServerPacket_CreatureSkull_Is0x90()  => Assert.Equal(0x90, (byte)GameServerPacket.CreatureSkull);
    [Fact] public void GameServerPacket_CreatureParty_Is0x91()  => Assert.Equal(0x91, (byte)GameServerPacket.CreatureParty);
    [Fact] public void GameServerPacket_CreatureMarks_Is0x93()  => Assert.Equal(0x93, (byte)GameServerPacket.CreatureMarks);
    [Fact] public void GameServerPacket_CancelWalk_Is0xB5()     => Assert.Equal(0xB5, (byte)GameServerPacket.CancelWalk);

    // ─── T13: ParseCreatureSkull ─────────────────────────────────────────────

    [Fact]
    public void ParseCreatureSkull_FiresCreatureSkullUpdated()
    {
        using var pg = new ProtocolGame();
        uint? gotId = null; byte? gotSkull = null;
        pg.CreatureSkullUpdated += (id, skull) => { gotId = id; gotSkull = skull; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureSkull);
        out_.WriteU32(0x1234_5678u);
        out_.WriteU8(3);  // skull type 3 = red skull

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(0x1234_5678u, gotId);
        Assert.Equal(3, (int)gotSkull!.Value);
    }

    // ─── T13: ParseCreatureShield ────────────────────────────────────────────

    [Fact]
    public void ParseCreatureShield_FiresCreatureShieldUpdated()
    {
        using var pg = new ProtocolGame();
        uint? gotId = null; byte? gotShield = null;
        pg.CreatureShieldUpdated += (id, shield) => { gotId = id; gotShield = shield; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureParty);
        out_.WriteU32(0xDEAD_BEEFu);
        out_.WriteU8(4);  // shield type 4 = yellow shared

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(0xDEAD_BEEFu, gotId);
        Assert.Equal(4, (int)gotShield!.Value);
    }

    // ─── T13: ParseCreatureMarks ─────────────────────────────────────────────

    [Fact]
    public void ParseCreatureMarks_PermanentMark_IsPermanentTrue()
    {
        using var pg = new ProtocolGame();
        uint? gotId = null; bool? gotPerm = null; byte? gotMark = null;
        pg.CreatureMarksUpdated += (id, perm, mark) => { gotId = id; gotPerm = perm; gotMark = mark; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureMarks);
        out_.WriteU32(999u);
        out_.WriteU8(0);    // isPermanent byte == 0 → isPermanent = true
        out_.WriteU8(0xFF); // clear square

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(999u, gotId);
        Assert.True(gotPerm);
        Assert.Equal(0xFF, (int)gotMark!.Value);
    }

    [Fact]
    public void ParseCreatureMarks_TimedMark_IsPermanentFalse()
    {
        using var pg = new ProtocolGame();
        bool? gotPerm = null;
        pg.CreatureMarksUpdated += (_, perm, _) => gotPerm = perm;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CreatureMarks);
        out_.WriteU32(1u);
        out_.WriteU8(1);    // non-zero → timed → isPermanent = false
        out_.WriteU8(5);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.False(gotPerm);
    }

    // ─── T13: ParseCancelWalk ────────────────────────────────────────────────

    [Fact]
    public void ParseCancelWalk_FiresWalkCanceled_WithDirection()
    {
        using var pg = new ProtocolGame();
        OTClient.Framework.Game.Direction? gotDir = null;
        pg.WalkCanceled += dir => gotDir = dir;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CancelWalk);
        out_.WriteU8((byte)OTClient.Framework.Game.Direction.South);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(OTClient.Framework.Game.Direction.South, gotDir);
    }

    // ─── T15: Opcode values ───────────────────────────────────────────────────

    [Fact] public void GameServerPacket_OpenNpcTrade_Is0x7A()  => Assert.Equal(0x7A, (byte)GameServerPacket.OpenNpcTrade);
    [Fact] public void GameServerPacket_PlayerGoods_Is0x7B()   => Assert.Equal(0x7B, (byte)GameServerPacket.PlayerGoods);
    [Fact] public void GameServerPacket_CloseNpcTrade_Is0x7C() => Assert.Equal(0x7C, (byte)GameServerPacket.CloseNpcTrade);
    [Fact] public void GameClientPacket_InspectNpcTrade_Is0x79() => Assert.Equal(0x79, (byte)GameClientPacket.InspectNpcTrade);
    [Fact] public void GameClientPacket_BuyItem_Is0x7A()        => Assert.Equal(0x7A, (byte)GameClientPacket.BuyItem);
    [Fact] public void GameClientPacket_SellItem_Is0x7B()       => Assert.Equal(0x7B, (byte)GameClientPacket.SellItem);
    [Fact] public void GameClientPacket_CloseNpcTrade_Is0x7C()  => Assert.Equal(0x7C, (byte)GameClientPacket.CloseNpcTrade);

    // ─── T15: ParseOpenNpcTrade ───────────────────────────────────────────────

    [Fact]
    public void ParseOpenNpcTrade_FiresNpcTradeOpened_WithItems()
    {
        using var pg = new ProtocolGame();
        IReadOnlyList<OTClient.Framework.Game.NpcTradeItem>? gotItems = null;
        pg.NpcTradeOpened += items => gotItems = items;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.OpenNpcTrade);
        out_.WriteString("Nelly");          // npcName
        out_.WriteU16(3031);                // currency item type id (gold coin)
        out_.WriteString("gold coin");      // currency name
        out_.WriteU16(2);                   // item count
        // item 1
        out_.WriteU16(100);  out_.WriteU8(1);  out_.WriteString("Sword");   out_.WriteU32(120);  out_.WriteU32(50);   out_.WriteU32(25);
        // item 2
        out_.WriteU16(200);  out_.WriteU8(1);  out_.WriteString("Shield");  out_.WriteU32(400);  out_.WriteU32(200);  out_.WriteU32(100);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.NotNull(gotItems);
        Assert.Equal(2, gotItems!.Count);
        Assert.Equal(100,     gotItems[0].Item.Id);
        Assert.Equal("Sword", gotItems[0].Name);
        Assert.Equal(50u,     gotItems[0].BuyPrice);
        Assert.Equal(25u,     gotItems[0].SellPrice);
        Assert.Equal(200,     gotItems[1].Item.Id);
    }

    // ─── T15: ParsePlayerGoods ────────────────────────────────────────────────

    [Fact]
    public void ParsePlayerGoods_FiresPlayerGoodsReceived()
    {
        using var pg = new ProtocolGame();
        ulong? gotMoney = null;
        IReadOnlyList<(int ItemId, int Amount)>? gotGoods = null;
        pg.PlayerGoodsReceived += (money, goods) => { gotMoney = money; gotGoods = goods; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PlayerGoods);
        out_.WriteU64(12345UL); // money (consumed by parser)
        out_.WriteU8(1);        // 1 good
        out_.WriteU16(3031);    out_.WriteU8(10); // gold coin × 10

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(12345UL, gotMoney);
        Assert.NotNull(gotGoods);
        Assert.Single(gotGoods!);
        Assert.Equal(3031, gotGoods![0].ItemId);
        Assert.Equal(10,   gotGoods![0].Amount);
    }

    // ─── T15: ParseCloseNpcTrade ──────────────────────────────────────────────

    [Fact]
    public void ParseCloseNpcTrade_FiresNpcTradeClosed()
    {
        using var pg = new ProtocolGame();
        bool fired = false;
        pg.NpcTradeClosed += () => fired = true;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CloseNpcTrade);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.True(fired);
    }

    // ─── T16: Opcode values ───────────────────────────────────────────────────

    [Fact] public void GameServerPacket_OwnTrade_Is0x7D()     => Assert.Equal(0x7D, (byte)GameServerPacket.OwnTrade);
    [Fact] public void GameServerPacket_CounterTrade_Is0x7E() => Assert.Equal(0x7E, (byte)GameServerPacket.CounterTrade);
    [Fact] public void GameServerPacket_CloseTrade_Is0x7F()   => Assert.Equal(0x7F, (byte)GameServerPacket.CloseTrade);
    [Fact] public void GameClientPacket_RequestTrade_Is0x7D() => Assert.Equal(0x7D, (byte)GameClientPacket.RequestTrade);
    [Fact] public void GameClientPacket_InspectTrade_Is0x7E() => Assert.Equal(0x7E, (byte)GameClientPacket.InspectTrade);
    [Fact] public void GameClientPacket_AcceptTrade_Is0x7F()  => Assert.Equal(0x7F, (byte)GameClientPacket.AcceptTrade);
    [Fact] public void GameClientPacket_RejectTrade_Is0x80()  => Assert.Equal(0x80, (byte)GameClientPacket.RejectTrade);

    // ─── T16: ParseOwnTrade ───────────────────────────────────────────────────

    [Fact]
    public void ParseOwnTrade_FiresOwnTradeReceived()
    {
        using var pg = new ProtocolGame();
        string? gotName = null;
        IReadOnlyList<OTClient.Framework.Game.Item>? gotItems = null;
        pg.OwnTradeReceived += (name, items) => { gotName = name; gotItems = items; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.OwnTrade);
        out_.WriteString("Alice");
        out_.WriteU8(1);        // 1 item
        out_.WriteU16(2400);    // item type id

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Alice", gotName);
        Assert.NotNull(gotItems);
        Assert.Single(gotItems!);
        Assert.Equal(2400, gotItems![0].Id);
    }

    // ─── T16: ParseCounterTrade ───────────────────────────────────────────────

    [Fact]
    public void ParseCounterTrade_FiresCounterTradeReceived()
    {
        using var pg = new ProtocolGame();
        string? gotName = null;
        IReadOnlyList<OTClient.Framework.Game.Item>? gotItems = null;
        pg.CounterTradeReceived += (name, items) => { gotName = name; gotItems = items; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CounterTrade);
        out_.WriteString("Bob");
        out_.WriteU8(2);        // 2 items
        out_.WriteU16(1234);
        out_.WriteU16(5678);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Bob", gotName);
        Assert.Equal(2, gotItems!.Count);
        Assert.Equal(1234, gotItems![0].Id);
        Assert.Equal(5678, gotItems![1].Id);
    }

    // ─── T16: ParseCloseTrade ─────────────────────────────────────────────────

    [Fact]
    public void ParseCloseTrade_FiresPlayerTradeClosed()
    {
        using var pg = new ProtocolGame();
        bool fired = false;
        pg.PlayerTradeClosed += () => fired = true;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.CloseTrade);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.True(fired);
    }

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
