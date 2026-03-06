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
        mm.Record(pos, Raylib_cs.Color.Red);
        Assert.Equal(Raylib_cs.Color.Red, mm.GetColor(pos));
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
}
