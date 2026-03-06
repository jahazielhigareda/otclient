using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for the <see cref="OTClient.Framework.Game.Game"/> singleton — state machine, world ownership,
/// data managers.  Task 8.21.
/// </summary>
public sealed class GameSingletonTests
{
    private static OTClient.Framework.Game.Game MakeGame() => new OTClient.Framework.Game.Game();
    // ─── State machine ────────────────────────────────────────────────────────

    [Fact]
    public void Game_InitialState_IsDisconnected()
    {
        var g = MakeGame();
        Assert.Equal(GameState.Disconnected, g.State);
        Assert.True(g.IsDisconnected);
    }

    [Fact]
    public void StartConnect_SetsConnecting()
    {
        var g = MakeGame();
        g.StartConnect();
        Assert.Equal(GameState.Connecting, g.State);
    }

    [Fact]
    public void SetLoginServer_SetsLoginServer()
    {
        var g = MakeGame();
        g.StartConnect();
        g.SetLoginServer();
        Assert.Equal(GameState.LoginServer, g.State);
    }

    [Fact]
    public void EnterGame_SetsInGame()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        Assert.Equal(GameState.InGame, g.State);
        Assert.True(g.IsOnline);
    }

    [Fact]
    public void EnterGame_NullPlayer_Throws()
    {
        var g = MakeGame();
        Assert.Throws<ArgumentNullException>(() => g.EnterGame(null!));
    }

    [Fact]
    public void Logout_SetsDisconnected()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        g.Logout();
        Assert.Equal(GameState.Disconnected, g.State);
    }

    [Fact]
    public void Logout_ClearsLocalPlayerKnown()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        g.Logout();
        Assert.False(g.LocalPlayer.IsKnown);
    }

    [Fact]
    public void OnStateChanged_Raised_OnTransition()
    {
        var g = MakeGame();
        GameState? from = null, to = null;
        g.OnStateChanged += (f, t) => { from = f; to = t; };
        g.StartConnect();
        Assert.Equal(GameState.Disconnected, from);
        Assert.Equal(GameState.Connecting,   to);
    }

    [Fact]
    public void OnStateChanged_NotRaised_WhenSameState()
    {
        var g     = MakeGame();
        int count = 0;
        g.OnStateChanged += (_, _) => count++;
        g.StartConnect();
        count = 0;
        g.StartConnect();  // same state → no event
        Assert.Equal(0, count);
    }

    // ─── Character list ───────────────────────────────────────────────────────

    [Fact]
    public void SetCharacterList_UpdatesCharacters()
    {
        var g = MakeGame();
        g.SetCharacterList([
            new CharacterInfo("Hero", "Antica", "127.0.0.1", 7171),
            new CharacterInfo("Mage", "Antica", "127.0.0.1", 7171),
        ]);
        Assert.Equal(2, g.Characters.Count);
    }

    [Fact]
    public void SetCharacterList_ReplacesExisting()
    {
        var g = MakeGame();
        g.SetCharacterList([new CharacterInfo("A", "W", "1.1.1.1", 7171)]);
        g.SetCharacterList([new CharacterInfo("B", "W", "1.1.1.1", 7171),
                            new CharacterInfo("C", "W", "1.1.1.1", 7171)]);
        Assert.Equal(2, g.Characters.Count);
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_DoesNotThrow_WhenDisconnected()
    {
        var g  = MakeGame();
        var ex = Record.Exception(delegate { g.Update(16f); });
        Assert.Null(ex);
    }

    [Fact]
    public void Update_AdvancesCreatureWalks()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 99, Name = "Test" });

        var c = new Creature { Id = 1, Position = new Position(10, 10, 7) };
        g.Map.AddCreature(c);
        c.Walk(new Position(11, 10, 7), 500);

        g.Update(600f);  // 600 ms > 500 ms → walk complete
        Assert.False(c.IsWalking);
    }

    // ─── Data managers ────────────────────────────────────────────────────────

    [Fact]
    public void TownManager_AddAndGet()
    {
        var mgr = new TownManager();
        var town = new Town { Id = 1, Name = "Thais", TemplePos = new Position(128, 131, 7) };
        mgr.Add(town);
        Assert.Same(town, mgr.Get(1));
    }

    [Fact]
    public void HouseManager_AddAndGet()
    {
        var mgr   = new HouseManager();
        var house = new House { Id = 10, Name = "Thais House", TownId = 1 };
        mgr.Add(house);
        Assert.Same(house, mgr.Get(10));
    }

    [Fact]
    public void CreatureDataManager_CaseInsensitiveLookup()
    {
        var mgr = new CreatureDataManager();
        mgr.Add(new CreatureData { Name = "Dragon", TypeId = 100 });
        Assert.NotNull(mgr.Get("DRAGON"));
        Assert.NotNull(mgr.Get("dragon"));
    }

    [Fact]
    public void GameConfig_DefaultsCorrect()
    {
        var cfg = new GameConfig();
        Assert.Equal(1281, cfg.ClientVersion);
        Assert.True(cfg.NewProtocol);
    }

    // ─── LightView ────────────────────────────────────────────────────────────

    [Fact]
    public void LightView_AmbientOnly_CorrectLevel()
    {
        var lv = new LightView { AmbientLight = 128 };
        float level = lv.GetLightAt(new Position(0, 0, 7));
        Assert.Equal(128f / 255f, level, precision: 4);
    }

    [Fact]
    public void LightView_DynamicSource_IncreasesLight()
    {
        var lv = new LightView { AmbientLight = 0 };
        lv.AddSource(new LightSource
        {
            Position = new Position(5, 5, 7),
            Level    = 255,
            Radius   = 4,
        });
        float level = lv.GetLightAt(new Position(5, 5, 7));
        Assert.True(level > 0f);
    }

    [Fact]
    public void LightView_Clear_RemovesSources()
    {
        var lv = new LightView { AmbientLight = 0 };
        lv.AddSource(new LightSource { Position = new Position(0, 0, 7), Level = 255, Radius = 4 });
        lv.Clear();
        float level = lv.GetLightAt(new Position(0, 0, 7));
        Assert.Equal(0f, level);
    }

    // ─── Minimap ──────────────────────────────────────────────────────────────

    [Fact]
    public void Minimap_Record_And_Get()
    {
        var mm  = new Minimap();
        var pos = new Position(10, 10, 7);
        // Colors go through 8-bit palette quantisation (same as C++ Color::to8bit/from8bit)
        // so exact round-trip is not guaranteed; we verify the tile is seen and non-black.
        mm.Record(pos, Raylib_cs.Color.Red);
        var got = mm.GetColor(pos);
        Assert.NotEqual(Raylib_cs.Color.Black, got);
        Assert.True(got.R > 100); // clearly reddish
    }

    [Fact]
    public void Minimap_UnknownPos_ReturnsBlack()
    {
        var mm = new Minimap();
        Assert.Equal(Raylib_cs.Color.Black, mm.GetColor(new Position(0, 0, 0)));
    }

    [Fact]
    public void Minimap_IsKnown_TrueAfterRecord()
    {
        var mm  = new Minimap();
        var pos = new Position(5, 5, 7);
        mm.Record(pos, Raylib_cs.Color.White);
        Assert.True(mm.IsKnown(pos));
    }

    // ─── T18 Minimap extended ─────────────────────────────────────────────────

    [Fact]
    public void MinimapBlock_UpdateTile_RetrievesCorrectly()
    {
        var block = new MinimapBlock();
        var td    = new MinimapTileData { Color = 42, Flags = MinimapTileFlags.WasSeen, Speed = 5 };
        block.UpdateTile(3, 7, td);
        Assert.Equal(td, block.GetTile(3, 7));
    }

    [Fact]
    public void MinimapBlock_ResetTile_ReturnsDefault()
    {
        var block = new MinimapBlock();
        block.UpdateTile(0, 0, new MinimapTileData { Color = 10 });
        block.ResetTile(0, 0);
        Assert.Equal(new MinimapTileData(), block.GetTile(0, 0));
    }

    [Fact]
    public void MinimapBlock_IsDirty_AfterUpdateTileWithDifferentColor()
    {
        var block = new MinimapBlock();
        block.ClearDirty();
        block.UpdateTile(1, 1, new MinimapTileData { Color = 5 });
        Assert.True(block.IsDirty);
    }

    [Fact]
    public void MinimapBlock_WasSeen_AfterMarkSeen()
    {
        var block = new MinimapBlock();
        Assert.False(block.WasSeen);
        block.MarkSeen();
        Assert.True(block.WasSeen);
    }

    [Fact]
    public void MinimapTileData_Equality()
    {
        var a = new MinimapTileData { Color = 10, Flags = MinimapTileFlags.WasSeen, Speed = 2 };
        var b = new MinimapTileData { Color = 10, Flags = MinimapTileFlags.WasSeen, Speed = 2 };
        var c = new MinimapTileData { Color = 99 };
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a == b);
        Assert.True(a != c);
    }

    [Fact]
    public void MinimapTileData_HasFlag_Works()
    {
        var td = new MinimapTileData { Flags = MinimapTileFlags.WasSeen | MinimapTileFlags.NotWalkable };
        Assert.True(td.HasFlag(MinimapTileFlags.WasSeen));
        Assert.True(td.HasFlag(MinimapTileFlags.NotWalkable));
        Assert.False(td.HasFlag(MinimapTileFlags.NotPathable));
    }

    [Fact]
    public void Minimap_Record_MakesIsKnownTrue()
    {
        var mm  = new Minimap();
        var pos = new Position(100, 100, 7);
        mm.Record(pos, Raylib_cs.Color.Green);
        Assert.True(mm.IsKnown(pos));
    }

    [Fact]
    public void Minimap_GetTile_UnseenReturnsDefault()
    {
        var mm  = new Minimap();
        var td  = mm.GetTile(new Position(0, 0, 0));
        Assert.False(td.HasFlag(MinimapTileFlags.WasSeen));
    }

    [Fact]
    public void Minimap_GetTile_SeenAfterRecord()
    {
        var mm  = new Minimap();
        var pos = new Position(5, 5, 7);
        mm.Record(pos, Raylib_cs.Color.White);
        var td  = mm.GetTile(pos);
        Assert.True(td.HasFlag(MinimapTileFlags.WasSeen));
    }

    [Fact]
    public void Minimap_Clear_ResetsKnownTiles()
    {
        var mm  = new Minimap();
        var pos = new Position(1, 1, 7);
        mm.Record(pos, Raylib_cs.Color.Red);
        mm.Clear();
        Assert.False(mm.IsKnown(pos));
    }

    [Fact]
    public void Minimap_SaveAndLoad_RoundTrip()
    {
        var mm1 = new Minimap();
        var pos = new Position(64, 128, 7);  // second block column, third block row
        mm1.Record(pos, Raylib_cs.Color.Green);

        using var ms = new System.IO.MemoryStream();
        mm1.SaveOtmm(ms);
        ms.Seek(0, System.IO.SeekOrigin.Begin);

        var mm2 = new Minimap();
        Assert.True(mm2.LoadOtmm(ms));
        Assert.True(mm2.IsKnown(pos));
    }

    [Fact]
    public void Minimap_LoadOtmm_InvalidSignature_ReturnsFalse()
    {
        using var ms = new System.IO.MemoryStream([0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00]);
        var mm = new Minimap();
        Assert.False(mm.LoadOtmm(ms));
    }

    [Fact]
    public void Minimap_GetTilePoint_SameFloor_ReturnsInsideRect()
    {
        var screenRect = new System.Drawing.Rectangle(0, 0, 200, 200);
        var center     = new Position(100, 100, 7);
        var pos        = new Position(100, 100, 7); // same as center → screen center
        var pt = Minimap.GetTilePoint(pos, screenRect, center, scale: 1f);
        Assert.Equal(100f, pt.X, 1f);
        Assert.Equal(100f, pt.Y, 1f);
    }

    [Fact]
    public void Minimap_GetTilePoint_DifferentFloor_ReturnsMinusOne()
    {
        var screenRect = new System.Drawing.Rectangle(0, 0, 200, 200);
        var center     = new Position(100, 100, 7);
        var pos        = new Position(100, 100, 6); // different floor
        var pt = Minimap.GetTilePoint(pos, screenRect, center, scale: 1f);
        Assert.Equal(-1, (int)pt.X);
    }

    [Fact]
    public void Minimap_GetTilePosition_RoundTrip()
    {
        var screenRect = new System.Drawing.Rectangle(0, 0, 200, 200);
        var center     = new Position(100, 100, 7);
        var orig       = new Position(105, 97, 7);
        var pt         = Minimap.GetTilePoint(orig, screenRect, center, scale: 1f);
        var back       = Minimap.GetTilePosition(pt, screenRect, center, scale: 1f);
        Assert.Equal(orig.X, back.X);
        Assert.Equal(orig.Y, back.Y);
        Assert.Equal(orig.Z, back.Z);
    }

    [Fact]
    public void Minimap_GetTileRect_DifferentFloor_ReturnsEmpty()
    {
        var screenRect = new System.Drawing.Rectangle(0, 0, 200, 200);
        var center     = new Position(100, 100, 7);
        var pos        = new Position(100, 100, 6);
        var r = Minimap.GetTileRect(pos, screenRect, center, scale: 2f);
        Assert.Equal(System.Drawing.Rectangle.Empty, r);
    }

    [Fact]
    public void MinimapBlock_ToBytes_CopyFromBytes_RoundTrip()
    {
        var b1 = new MinimapBlock();
        b1.UpdateTile(0, 0, new MinimapTileData { Color = 7, Flags = MinimapTileFlags.WasSeen, Speed = 3 });
        b1.UpdateTile(3, 5, new MinimapTileData { Color = 55, Flags = MinimapTileFlags.NotWalkable, Speed = 9 });

        var bytes = b1.ToBytes();

        var b2 = new MinimapBlock();
        b2.CopyFromBytes(bytes);

        Assert.Equal(b1.GetTile(0, 0), b2.GetTile(0, 0));
        Assert.Equal(b1.GetTile(3, 5), b2.GetTile(3, 5));
    }

    // ─── PaperDoll ────────────────────────────────────────────────────────────

    [Fact]
    public void PaperDoll_SlotEmpty_Initially()
    {
        var pd = new PaperDoll();
        Assert.True(pd.IsSlotEmpty(PaperDoll.Slot.Head));
    }

    [Fact]
    public void PaperDoll_Equip_And_Get()
    {
        var pd   = new PaperDoll();
        var item = Item.Create(500);
        pd.Equip(PaperDoll.Slot.Body, item);
        Assert.Same(item, pd.GetSlot(PaperDoll.Slot.Body));
    }

    [Fact]
    public void PaperDoll_Clear_EmptiesAll()
    {
        var pd = new PaperDoll();
        pd.Equip(PaperDoll.Slot.Head, Item.Create(1));
        pd.Clear();
        Assert.True(pd.IsSlotEmpty(PaperDoll.Slot.Head));
    }

    // ─── AttachedEffectManager ────────────────────────────────────────────────

    [Fact]
    public void AttachedEffectManager_RegisterAndGet()
    {
        var mgr = new AttachedEffectManager();
        var eff = new AttachedEffect { Id = 7, Name = "Wings" };
        mgr.Register(eff);
        Assert.Same(eff, mgr.Get(7));
    }

    [Fact]
    public void AttachedEffectManager_GetNull_WhenNotFound()
    {
        var mgr = new AttachedEffectManager();
        Assert.Null(mgr.Get(999));
    }

    // ─── Walk / Turn / Stop / AutoWalk ────────────────────────────────────────

    [Fact]
    public void Walk_WhenOnline_FiresWalkRequestedEvent()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        Direction? received = null;
        g.WalkRequested += dir => received = dir;

        g.Walk(Direction.North);
        Assert.Equal(Direction.North, received);
    }

    [Fact]
    public void Walk_WhenOffline_DoesNotFireEvent()
    {
        var g     = MakeGame();
        bool fired = false;
        g.WalkRequested += _ => fired = true;

        g.Walk(Direction.South);
        Assert.False(fired);
    }

    [Fact]
    public void Turn_WhenOnline_FiresTurnRequestedEvent()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        Direction? received = null;
        g.TurnRequested += dir => received = dir;

        g.Turn(Direction.East);
        Assert.Equal(Direction.East, received);
    }

    [Fact]
    public void Turn_WhenOffline_DoesNotFireEvent()
    {
        var g      = MakeGame();
        bool fired = false;
        g.TurnRequested += _ => fired = true;

        g.Turn(Direction.West);
        Assert.False(fired);
    }

    [Fact]
    public void Stop_WhenOnline_FiresStopRequestedEvent()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        bool fired = false;
        g.StopRequested += () => fired = true;

        g.Stop();
        Assert.True(fired);
    }

    [Fact]
    public void Stop_WhenOffline_DoesNotFireEvent()
    {
        var g      = MakeGame();
        bool fired = false;
        g.StopRequested += () => fired = true;

        g.Stop();
        Assert.False(fired);
    }

    [Fact]
    public void AutoWalk_WhenOnline_FiresAutoWalkRequestedEvent()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        IReadOnlyList<Direction>? received = null;
        g.AutoWalkRequested += path => received = path;

        var steps = new[] { Direction.North, Direction.East };
        g.AutoWalk(steps);

        Assert.NotNull(received);
        Assert.Equal(2, received!.Count);
        Assert.Equal(Direction.North, received[0]);
        Assert.Equal(Direction.East,  received[1]);
    }

    [Fact]
    public void AutoWalk_EmptyPath_DoesNotFireEvent()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        bool fired = false;
        g.AutoWalkRequested += _ => fired = true;

        g.AutoWalk(Array.Empty<Direction>());
        Assert.False(fired);
    }

    [Fact]
    public void AutoWalk_NullPath_Throws()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        Assert.Throws<ArgumentNullException>(() => g.AutoWalk(null!));
    }

    // ─── T10: Chat methods ────────────────────────────────────────────────────

    [Fact]
    public void TalkSay_WhenOnline_FiresTalkSayRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        string? fired = null;
        g.TalkSayRequested += msg => fired = msg;
        g.TalkSay("Hello world");
        Assert.Equal("Hello world", fired);
    }

    [Fact]
    public void TalkSay_WhenOffline_DoesNotFire()
    {
        var g = MakeGame();
        bool fired = false;
        g.TalkSayRequested += _ => fired = true;
        g.TalkSay("Hello");
        Assert.False(fired);
    }

    [Fact]
    public void TalkChannel_WhenOnline_FiresTalkChannelRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        ushort? gotId = null; string? gotMsg = null;
        g.TalkChannelRequested += (id, msg) => { gotId = id; gotMsg = msg; };
        g.TalkChannel(5, "Trade chat");
        Assert.Equal((ushort)5, gotId);
        Assert.Equal("Trade chat", gotMsg);
    }

    [Fact]
    public void TalkPrivate_WhenOnline_FiresTalkPrivateRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        string? gotReceiver = null; string? gotMsg = null;
        g.TalkPrivateRequested += (recv, msg) => { gotReceiver = recv; gotMsg = msg; };
        g.TalkPrivate("Alice", "Hey");
        Assert.Equal("Alice", gotReceiver);
        Assert.Equal("Hey",   gotMsg);
    }

    [Fact]
    public void RequestChannels_WhenOnline_FiresChannelsRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        bool fired = false;
        g.ChannelsRequested += () => fired = true;
        g.RequestChannels();
        Assert.True(fired);
    }

    [Fact]
    public void JoinChannel_WhenOnline_FiresJoinChannelRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        ushort? gotId = null;
        g.JoinChannelRequested += id => gotId = id;
        g.JoinChannel(3);
        Assert.Equal((ushort)3, gotId);
    }

    [Fact]
    public void LeaveChannel_WhenOnline_FiresLeaveChannelRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        ushort? gotId = null;
        g.LeaveChannelRequested += id => gotId = id;
        g.LeaveChannel(3);
        Assert.Equal((ushort)3, gotId);
    }

    [Fact]
    public void OpenPrivateChannel_WhenOnline_FiresOpenPrivateChannelRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        string? gotName = null;
        g.OpenPrivateChannelRequested += name => gotName = name;
        g.OpenPrivateChannel("Bob");
        Assert.Equal("Bob", gotName);
    }

    [Fact]
    public void TalkPrivate_EmptyReceiver_Throws()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        Assert.Throws<ArgumentException>(() => g.TalkPrivate("", "msg"));
    }

    [Fact]
    public void TalkPrivate_EmptyMessage_Throws()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        Assert.Throws<ArgumentException>(() => g.TalkPrivate("Alice", ""));
    }

    // ─── T12: Combat methods ──────────────────────────────────────────────────

    [Fact]
    public void Attack_WhenOnline_FiresAttackRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        uint? gotId = null;
        g.AttackRequested += id => gotId = id;
        g.Attack(42);
        Assert.Equal(42u, gotId);
    }

    [Fact]
    public void Attack_WhenOffline_DoesNotFire()
    {
        var g = MakeGame();
        bool fired = false;
        g.AttackRequested += _ => fired = true;
        g.Attack(42);
        Assert.False(fired);
    }

    [Fact]
    public void Follow_WhenOnline_FiresFollowRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        uint? gotId = null;
        g.FollowRequested += id => gotId = id;
        g.Follow(99);
        Assert.Equal(99u, gotId);
    }

    [Fact]
    public void CancelAttack_WhenOnline_FiresEvent()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        bool fired = false;
        g.CancelAttackRequested += () => fired = true;
        g.CancelAttack();
        Assert.True(fired);
    }

    [Fact]
    public void CancelFollow_WhenOnline_FiresEvent()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        bool fired = false;
        g.CancelFollowRequested += () => fired = true;
        g.CancelFollow();
        Assert.True(fired);
    }

    [Fact]
    public void CancelAttackAndFollow_WhenOnline_FiresEvent()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        bool fired = false;
        g.CancelAttackAndFollowRequested += () => fired = true;
        g.CancelAttackAndFollow();
        Assert.True(fired);
    }

    [Fact]
    public void SetFightModes_WhenOnline_FiresFightModesChanged()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        FightMode? gotFight = null; ChaseMode? gotChase = null;
        bool? gotSafe = null; PvpMode? gotPvp = null;
        g.FightModesChanged += (f, c, s, p) =>
            { gotFight = f; gotChase = c; gotSafe = s; gotPvp = p; };
        g.SetFightModes(FightMode.Offensive, ChaseMode.ChaseOpponent, true, PvpMode.WhiteHand);
        Assert.Equal(FightMode.Offensive,      gotFight);
        Assert.Equal(ChaseMode.ChaseOpponent,  gotChase);
        Assert.True(gotSafe);
        Assert.Equal(PvpMode.WhiteHand,        gotPvp);
    }

    [Fact]
    public void SetFightModes_WhenOffline_DoesNotFire()
    {
        var g = MakeGame();
        bool fired = false;
        g.FightModesChanged += (_, _, _, _) => fired = true;
        g.SetFightModes(FightMode.Offensive, ChaseMode.DontChase, false);
        Assert.False(fired);
    }

    // ─── T15: NPC trade methods ───────────────────────────────────────────────

    [Fact]
    public void InspectNpcTrade_WhenOnline_FiresInspectNpcTradeRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        int? gotId = null; int? gotCount = null;
        g.InspectNpcTradeRequested += (id, c) => { gotId = id; gotCount = c; };
        g.InspectNpcTrade(100, 3);
        Assert.Equal(100, gotId);
        Assert.Equal(3,   gotCount);
    }

    [Fact]
    public void InspectNpcTrade_WhenOffline_DoesNotFire()
    {
        var g = MakeGame();
        bool fired = false;
        g.InspectNpcTradeRequested += (_, _) => fired = true;
        g.InspectNpcTrade(100, 1);
        Assert.False(fired);
    }

    [Fact]
    public void BuyItem_WhenOnline_FiresBuyItemRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        int? gotId = null;
        g.BuyItemRequested += (id, _, _, _, _) => gotId = id;
        g.BuyItem(200, 1, 5, ignoreCapacity: false, buyWithBackpack: true);
        Assert.Equal(200, gotId);
    }

    [Fact]
    public void SellItem_WhenOnline_FiresSellItemRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        int? gotId = null; bool? gotIgnore = null;
        g.SellItemRequested += (id, _, _, ignore) => { gotId = id; gotIgnore = ignore; };
        g.SellItem(300, 0, 2, ignoreEquipped: true);
        Assert.Equal(300, gotId);
        Assert.True(gotIgnore);
    }

    [Fact]
    public void CloseNpcTrade_WhenOnline_FiresCloseNpcTradeRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        bool fired = false;
        g.CloseNpcTradeRequested += () => fired = true;
        g.CloseNpcTrade();
        Assert.True(fired);
    }

    [Fact]
    public void CloseNpcTrade_WhenOffline_DoesNotFire()
    {
        var g = MakeGame();
        bool fired = false;
        g.CloseNpcTradeRequested += () => fired = true;
        g.CloseNpcTrade();
        Assert.False(fired);
    }

    // ─── T16: Player-to-player trade methods ──────────────────────────────────

    [Fact]
    public void RequestTrade_WhenOnline_FiresRequestTradeRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        uint? gotCreatureId = null;
        g.RequestTradeRequested += (_, _, _, cid) => gotCreatureId = cid;
        g.RequestTrade(new Position(100, 200, 7), 500, 0, 42u);
        Assert.Equal(42u, gotCreatureId);
    }

    [Fact]
    public void RequestTrade_WhenOffline_DoesNotFire()
    {
        var g = MakeGame();
        bool fired = false;
        g.RequestTradeRequested += (_, _, _, _) => fired = true;
        g.RequestTrade(new Position(1, 1, 7), 1, 0, 1u);
        Assert.False(fired);
    }

    [Fact]
    public void InspectTrade_WhenOnline_FiresInspectTradeRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        bool? gotCounter = null; int? gotIndex = null;
        g.InspectTradeRequested += (c, i) => { gotCounter = c; gotIndex = i; };
        g.InspectTrade(counterOffer: true, index: 2);
        Assert.True(gotCounter);
        Assert.Equal(2, gotIndex);
    }

    [Fact]
    public void AcceptTrade_WhenOnline_FiresAcceptTradeRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        bool fired = false;
        g.AcceptTradeRequested += () => fired = true;
        g.AcceptTrade();
        Assert.True(fired);
    }

    [Fact]
    public void RejectTrade_WhenOnline_FiresRejectTradeRequested()
    {
        var g = MakeGame();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });
        bool fired = false;
        g.RejectTradeRequested += () => fired = true;
        g.RejectTrade();
        Assert.True(fired);
    }
}
