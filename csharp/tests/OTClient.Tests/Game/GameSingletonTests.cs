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
}
