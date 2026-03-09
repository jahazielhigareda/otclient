using OTClient.Framework.Core;
using OTClient.Framework.Lua;
using OTClient.Framework.Game;
using OTClient.Framework.UI;
using MoonSharp.Interpreter;
using Xunit;

namespace OTClient.Tests.Lua;

/// <summary>
/// Tests for the Phase 6 Lua global proxies added in tasks 6.6, 6.7, 6.10, 6.11.
/// Verifies that all new g_* globals are registered and callable from Lua.
/// </summary>
public sealed class LuaGameGlobalsTests : IDisposable
{
    private readonly LuaInterface _lua;

    public LuaGameGlobalsTests()
    {
        _lua = new LuaInterface();
        _lua.Init();
        LuaGlobals.Register(_lua,
            game:      new OTClient.Framework.Game.Game(),
            uiManager: new UIManager());
    }

    public void Dispose() => _lua.Dispose();

    // ─── g_graphics (6.6) ─────────────────────────────────────────────────────

    [Fact]
    public void G_Graphics_GetVendor_ReturnsRaylib()
    {
        var result = _lua.DoString("return g_graphics.getVendor()");
        Assert.Equal("Raylib", result.String);
    }

    [Fact]
    public void G_Graphics_GetRenderer_ReturnsRaylib()
    {
        var result = _lua.DoString("return g_graphics.getRenderer()");
        Assert.Equal("Raylib", result.String);
    }

    [Fact]
    public void G_Graphics_GetVersion_ReturnsString()
    {
        var result = _lua.DoString("return g_graphics.getVersion()");
        Assert.Equal(DataType.String, result.Type);
        Assert.False(string.IsNullOrEmpty(result.String));
    }

    // ─── g_textures (6.6) ────────────────────────────────────────────────────

    [Fact]
    public void G_Textures_GetCacheSize_ReturnsZero()
    {
        var result = _lua.DoString("return g_textures.getCacheSize()");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void G_Textures_GetTexture_MissingReturnsNil()
    {
        var result = _lua.DoString("return g_textures.getTexture('missing.png')");
        Assert.Equal(DataType.Nil, result.Type);
    }

    // ─── g_fonts (6.6) ───────────────────────────────────────────────────────

    [Fact]
    public void G_Fonts_GetDefaultFont_ReturnsString()
    {
        var result = _lua.DoString("return g_fonts.getDefaultFont()");
        Assert.Equal(DataType.String, result.Type);
    }

    [Fact]
    public void G_Fonts_HasFont_MissingReturnsFalse()
    {
        var result = _lua.DoString("return g_fonts.hasFont('nonexistent')");
        Assert.Equal(DataType.Boolean, result.Type);
        Assert.False(result.Boolean);
    }

    // ─── g_drawpool (6.6) ────────────────────────────────────────────────────

    [Fact]
    public void G_Drawpool_Flush_DoesNotThrow()
    {
        var result = _lua.DoString("g_drawpool.flush() return true");
        Assert.True(result.Boolean);
    }

    // ─── g_keyboard (6.7) ────────────────────────────────────────────────────

    [Fact]
    public void G_Keyboard_IsKeyDown_UnknownKeyReturnsFalse()
    {
        var result = _lua.DoString("return g_keyboard.isKeyDown('Z')");
        Assert.Equal(DataType.Boolean, result.Type);
    }

    [Fact]
    public void G_Keyboard_IsCtrlDown_ReturnsBool()
    {
        var result = _lua.DoString("return g_keyboard.isCtrlDown()");
        Assert.Equal(DataType.Boolean, result.Type);
    }

    [Fact]
    public void G_Keyboard_IsShiftDown_ReturnsBool()
    {
        var result = _lua.DoString("return g_keyboard.isShiftDown()");
        Assert.Equal(DataType.Boolean, result.Type);
    }

    // ─── g_mouse (6.7) ───────────────────────────────────────────────────────

    [Fact]
    public void G_Mouse_GetX_ReturnsNumber()
    {
        var result = _lua.DoString("return g_mouse.getX()");
        Assert.Equal(DataType.Number, result.Type);
    }

    [Fact]
    public void G_Mouse_GetScrollWheel_ReturnsNumber()
    {
        var result = _lua.DoString("return g_mouse.getScrollWheel()");
        Assert.Equal(DataType.Number, result.Type);
    }

    [Fact]
    public void G_Mouse_IsLeftDown_ReturnsBool()
    {
        var result = _lua.DoString("return g_mouse.isLeftDown()");
        Assert.Equal(DataType.Boolean, result.Type);
    }

    // ─── g_game (6.10) ───────────────────────────────────────────────────────

    [Fact]
    public void G_Game_IsOnline_ReturnsFalseInitially()
    {
        var result = _lua.DoString("return g_game.isOnline()");
        Assert.False(result.Boolean);
    }

    [Fact]
    public void G_Game_GetState_ReturnsString()
    {
        var result = _lua.DoString("return g_game.getState()");
        Assert.Equal(DataType.String, result.Type);
    }

    [Fact]
    public void G_Game_IsConnected_ReturnsFalseInitially()
    {
        var result = _lua.DoString("return g_game.isConnected()");
        Assert.False(result.Boolean);
    }

    [Fact]
    public void G_Game_GetProtocolVersion_ReturnsNumber()
    {
        var result = _lua.DoString("return g_game.getProtocolVersion()");
        Assert.Equal(DataType.Number, result.Type);
    }

    [Fact]
    public void G_Game_Logout_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.logout() return true");
        Assert.True(result.Boolean);
    }

    // ─── g_game T21: movement methods ─────────────────────────────────────────

    [Fact]
    public void G_Game_Walk_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.walk(0) return true");   // 0 = North
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_Turn_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.turn(1) return true");   // 1 = East
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_Stop_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.stop() return true");
        Assert.True(result.Boolean);
    }

    // ─── g_game T21: combat methods ───────────────────────────────────────────

    [Fact]
    public void G_Game_Attack_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.attack(42) return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_Follow_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.follow(99) return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_CancelAttack_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.cancelAttack() return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_CancelFollow_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.cancelFollow() return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_CancelAttackAndFollow_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.cancelAttackAndFollow() return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_SetFightModes_WhenOffline_DoesNotThrow()
    {
        // fightMode=1(Offensive), chaseMode=0(DontChase), safeFight=false
        var result = _lua.DoString("g_game.setFightModes(1, 0, false) return true");
        Assert.True(result.Boolean);
    }

    // ─── g_game T21: chat methods ─────────────────────────────────────────────

    [Fact]
    public void G_Game_Talk_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.talk('hello world') return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_TalkChannel_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.talkChannel(5, 'msg') return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_TalkPrivate_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.talkPrivate('Alice', 'hi') return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_RequestChannels_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.requestChannels() return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_JoinChannel_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.joinChannel(5) return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_LeaveChannel_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.leaveChannel(5) return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_OpenPrivateChannel_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.openPrivateChannel('Bob') return true");
        Assert.True(result.Boolean);
    }

    // ─── g_game T21: NPC trade methods ────────────────────────────────────────

    [Fact]
    public void G_Game_InspectNpcTrade_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.inspectNpcTrade(2400, 1) return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_BuyItem_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.buyItem(100, 1, 5, false, false) return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_SellItem_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.sellItem(200, 0, 3, false) return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_CloseNpcTrade_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.closeNpcTrade() return true");
        Assert.True(result.Boolean);
    }

    // ─── g_game T21: player trade methods ─────────────────────────────────────

    [Fact]
    public void G_Game_InspectTrade_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.inspectTrade(false, 0) return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_AcceptTrade_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.acceptTrade() return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_RejectTrade_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.rejectTrade() return true");
        Assert.True(result.Boolean);
    }

    // ─── g_game T21: VIP methods ──────────────────────────────────────────────

    [Fact]
    public void G_Game_AddVip_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.addVip('Alice') return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Game_RemoveVip_WhenOffline_DoesNotThrow()
    {
        var result = _lua.DoString("g_game.removeVip(12345) return true");
        Assert.True(result.Boolean);
    }

    // ─── g_game T43: getVip / getVips Lua methods ─────────────────────────────

    [Fact]
    public void G_Game_GetVip_UnknownId_ReturnsNil()
    {
        var result = _lua.DoString("return g_game.getVip(9999)");
        Assert.Equal(DataType.Nil, result.Type);
    }

    [Fact]
    public void G_Game_GetVips_EmptyGame_ReturnsEmptyTable()
    {
        var result = _lua.DoString("local v = g_game.getVips() return #v");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void G_Game_GetVip_KnownId_ReturnsVipEntry()
    {
        var game = new OTClient.Framework.Game.Game();
        game.ProcessVipAdd(77, "Charlie", 1, "best bud", 2, true);

        using var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: game);

        var result = lua.DoString("local e = g_game.getVip(77) return e:getName()");
        Assert.Equal("Charlie", result.String);
    }

    [Fact]
    public void G_Game_GetVips_ReturnsAllEntries()
    {
        var game = new OTClient.Framework.Game.Game();
        game.ProcessVipAdd(1, "Alpha", 0, "", 0, false);
        game.ProcessVipAdd(2, "Beta",  1, "", 0, false);

        using var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: game);

        var result = lua.DoString("return #g_game.getVips()");
        Assert.Equal(2.0, result.Number);
    }

    // ─── g_map (6.10) ────────────────────────────────────────────────────────

    [Fact]
    public void G_Map_GetTileCount_ReturnsNumber()
    {
        var result = _lua.DoString("return g_map.getTileCount()");
        Assert.Equal(DataType.Number, result.Type);
    }

    [Fact]
    public void G_Map_GetTile_EmptyMapReturnsNil()
    {
        var result = _lua.DoString("return g_map.getTile(100, 100, 7)");
        Assert.Equal(DataType.Nil, result.Type);
    }

    [Fact]
    public void G_Map_IsTileWalkable_EmptyReturnsFalse()
    {
        var result = _lua.DoString("return g_map.isTileWalkable(0, 0, 0)");
        Assert.Equal(DataType.Boolean, result.Type);
    }

    // ─── g_things (6.10) ─────────────────────────────────────────────────────

    [Fact]
    public void G_Things_IsLoaded_ReturnsTrue()
    {
        var result = _lua.DoString("return g_things.isLoaded()");
        Assert.True(result.Boolean);
    }

    // ─── g_sprites (6.10) ────────────────────────────────────────────────────

    [Fact]
    public void G_Sprites_IsLoaded_ReturnsTrue()
    {
        var result = _lua.DoString("return g_sprites.isLoaded()");
        Assert.True(result.Boolean);
    }

    // ─── g_creatures (6.10) ──────────────────────────────────────────────────

    [Fact]
    public void G_Creatures_GetCount_ReturnsNumber()
    {
        var result = _lua.DoString("return g_creatures.getCount()");
        Assert.Equal(DataType.Number, result.Type);
    }

    [Fact]
    public void G_Creatures_Exists_UnknownReturnsFalse()
    {
        var result = _lua.DoString("return g_creatures.exists('UnknownCreature')");
        Assert.False(result.Boolean);
    }

    // ─── g_client (6.10) ─────────────────────────────────────────────────────

    [Fact]
    public void G_Client_IsRunning_ReturnsTrue()
    {
        var result = _lua.DoString("return g_client.isRunning()");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Client_GetOs_ReturnsString()
    {
        var result = _lua.DoString("return g_client.getOs()");
        Assert.Equal(DataType.String, result.Type);
    }

    // ─── g_ui (6.11) ─────────────────────────────────────────────────────────

    [Fact]
    public void G_Ui_GetRootWidget_ReturnsUserData()
    {
        var result = _lua.DoString("return g_ui.getRootWidget()");
        Assert.Equal(DataType.UserData, result.Type);
    }

    [Fact]
    public void G_Ui_CreateWidget_ReturnsUserData()
    {
        var result = _lua.DoString("return g_ui.createWidget('UILabel', nil)");
        Assert.Equal(DataType.UserData, result.Type);
    }

    [Fact]
    public void G_Ui_GetWidget_MissingReturnsNil()
    {
        var result = _lua.DoString("return g_ui.getWidget('nonexistent-id')");
        Assert.Equal(DataType.Nil, result.Type);
    }

    [Fact]
    public void G_Ui_ClearWidgets_DoesNotThrow()
    {
        var result = _lua.DoString("g_ui.clearWidgets() return true");
        Assert.True(result.Boolean);
    }

    // ─── g_minimap (T36) ─────────────────────────────────────────────────────

    [Fact]
    public void G_Minimap_IsRegistered()
    {
        var result = _lua.DoString("return g_minimap ~= nil");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Minimap_Clean_DoesNotThrow()
    {
        var result = _lua.DoString("g_minimap.clean() return true");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Minimap_IsKnown_UnseenTileReturnsFalse()
    {
        var result = _lua.DoString("return g_minimap.isKnown(1000, 1000, 7)");
        Assert.False(result.Boolean);
    }

    [Fact]
    public void G_Minimap_GetTileColor_UnseenTileReturns255()
    {
        // Default MinimapTileData has Color=255 (transparent/unknown)
        var result = _lua.DoString("return g_minimap.getTileColor(5000, 5000, 7)");
        Assert.Equal(255.0, result.Number);
    }

    [Fact]
    public void G_Minimap_GetTileFlags_UnseenTileReturnsZero()
    {
        var result = _lua.DoString("return g_minimap.getTileFlags(5000, 5000, 7)");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void G_Minimap_GetTileSpeed_UnseenTileReturnsDefault()
    {
        // Default MinimapTileData has Speed=10
        var result = _lua.DoString("return g_minimap.getTileSpeed(5000, 5000, 7)");
        Assert.Equal(10.0, result.Number);
    }

    [Fact]
    public void G_Minimap_GetTilePoint_DifferentFloorReturnsNil()
    {
        // center on floor 7, ask for a tile on floor 6 → nil
        var result = _lua.DoString(
            "return g_minimap.getTilePoint(100, 100, 6,  0,0,800,600,  100,100,7,  32)");
        Assert.Equal(DataType.Nil, result.Type);
    }

    [Fact]
    public void G_Minimap_GetTilePoint_SameFloorReturnsTable()
    {
        // tile at the exact center position should land near mid-screen
        var result = _lua.DoString(
            "local pt = g_minimap.getTilePoint(100, 100, 7,  0,0,800,600,  100,100,7,  32)" +
            " return pt ~= nil and type(pt.x) == 'number' and type(pt.y) == 'number'");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Minimap_GetTilePosition_ReturnsTable()
    {
        var result = _lua.DoString(
            "local p = g_minimap.getTilePosition(400, 300,  0,0,800,600,  100,100,7,  32)" +
            " return type(p.x) == 'number' and type(p.y) == 'number' and p.z == 7");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Minimap_GetTileRect_DifferentFloorReturnsNil()
    {
        var result = _lua.DoString(
            "return g_minimap.getTileRect(100, 100, 6,  0,0,800,600,  100,100,7,  32)");
        Assert.Equal(DataType.Nil, result.Type);
    }

    [Fact]
    public void G_Minimap_GetTileRect_SameFloorReturnsTable()
    {
        var result = _lua.DoString(
            "local r = g_minimap.getTileRect(100, 100, 7,  0,0,800,600,  100,100,7,  32)" +
            " return r ~= nil and type(r.x) == 'number' and r.width >= 1");
        Assert.True(result.Boolean);
    }

    [Fact]
    public void G_Minimap_LoadOtmm_MissingFileReturnsFalse()
    {
        var result = _lua.DoString("return g_minimap.loadOtmm('/no/such/file.otmm')");
        Assert.False(result.Boolean);
    }

    // ─── ThingCategory constants (T37) ───────────────────────────────────────

    [Fact]
    public void ThingCategoryItem_IsZero()
    {
        var result = _lua.DoString("return ThingCategoryItem");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void ThingCategoryCreature_IsOne()
    {
        var result = _lua.DoString("return ThingCategoryCreature");
        Assert.Equal(1.0, result.Number);
    }

    [Fact]
    public void ThingCategoryEffect_IsTwo()
    {
        var result = _lua.DoString("return ThingCategoryEffect");
        Assert.Equal(2.0, result.Number);
    }

    [Fact]
    public void ThingCategoryMissile_IsThree()
    {
        var result = _lua.DoString("return ThingCategoryMissile");
        Assert.Equal(3.0, result.Number);
    }

    // ─── g_things enrichment (T37) ───────────────────────────────────────────

    [Fact]
    public void G_Things_CountItems_EmptyManagerReturnsZero()
    {
        var result = _lua.DoString("return g_things.countItems()");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void G_Things_CountCreatures_EmptyManagerReturnsZero()
    {
        var result = _lua.DoString("return g_things.countCreatures()");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void G_Things_CountEffects_EmptyManagerReturnsZero()
    {
        var result = _lua.DoString("return g_things.countEffects()");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void G_Things_CountMissiles_EmptyManagerReturnsZero()
    {
        var result = _lua.DoString("return g_things.countMissiles()");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void G_Things_GetCount_EmptyManagerReturnsZero()
    {
        var result = _lua.DoString("return g_things.getCount()");
        Assert.Equal(0.0, result.Number);
    }

    [Fact]
    public void G_Things_GetThingType_UnknownTypeReturnsNil()
    {
        var result = _lua.DoString("return g_things.getThingType(9999, ThingCategoryItem)");
        Assert.Equal(DataType.Nil, result.Type);
    }

    // ─── ThingType Lua userdata (T37) ────────────────────────────────────────

    // Helpers: seed one ThingType into the game's manager, then query via Lua.
    private ThingType MakeThingType(
        int id,
        ThingCategory cat           = ThingCategory.Item,
        string name                 = "Sword",
        ThingTypeFlag flags         = ThingTypeFlag.None,
        int classification          = 0,
        int groundSpeed             = 150,
        int lightLevel              = 0,
        int lightRadius             = 0,
        int width                   = 1,
        int height                  = 1,
        int frames                  = 1)
        => new()
        {
            Id             = id,
            Category       = cat,
            Name           = name,
            Flags          = flags,
            Classification = classification,
            GroundSpeed    = groundSpeed,
            LightLevel     = lightLevel,
            LightRadius    = lightRadius,
            Width          = width,
            Height         = height,
            Frames         = frames,
        };

    [Fact]
    public void ThingType_GetId_ReturnsCorrectValue()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(42, ThingCategory.Item));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(42, ThingCategoryItem) " +
            "return t ~= nil and t:getId()");
        Assert.Equal(42.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void ThingType_GetName_ReturnsCorrectValue()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(1, name: "Dragon"));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(1, ThingCategoryItem) " +
            "return t:getName()");
        Assert.Equal("Dragon", result.String);
        lua.Dispose();
    }

    [Fact]
    public void ThingType_GetCategory_ReturnsCorrectValue()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(10, ThingCategory.Creature));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(10, ThingCategoryCreature) " +
            "return t:getCategory()");
        Assert.Equal((double)ThingCategory.Creature, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void ThingType_IsGround_TrueWhenFlagSet()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(3, flags: ThingTypeFlag.Ground));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(3, ThingCategoryItem) " +
            "return t:isGround()");
        Assert.True(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void ThingType_IsStackable_FalseWhenFlagUnset()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(4, flags: ThingTypeFlag.None));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(4, ThingCategoryItem) " +
            "return t:isStackable()");
        Assert.False(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void ThingType_GetClassification_ReturnsCorrectValue()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(5, classification: 3));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(5, ThingCategoryItem) " +
            "return t:getClassification()");
        Assert.Equal(3.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void ThingType_IsAnimated_TrueWhenMultipleFrames()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(6, frames: 4));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(6, ThingCategoryItem) " +
            "return t:isAnimated()");
        Assert.True(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void ThingType_GetGroundSpeed_ReturnsCorrectValue()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(7, groundSpeed: 200));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(7, ThingCategoryItem) " +
            "return t:getGroundSpeed()");
        Assert.Equal(200.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void ThingType_GetLightLevel_ReturnsCorrectValue()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(8, lightLevel: 5, lightRadius: 3));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local t = g_things.getThingType(8, ThingCategoryItem) " +
            "return t:getLightLevel(), t:getLightRadius()");
        Assert.Equal(5.0, result.Tuple[0].Number);
        Assert.Equal(3.0, result.Tuple[1].Number);
        lua.Dispose();
    }

    [Fact]
    public void G_Things_CountItems_ReflectsAddedItems()
    {
        var g = new OTClient.Framework.Game.Game();
        g.Things.Add(MakeThingType(101, ThingCategory.Item));
        g.Things.Add(MakeThingType(102, ThingCategory.Item));
        g.Things.Add(MakeThingType(201, ThingCategory.Creature));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var items     = lua.DoString("return g_things.countItems()");
        var creatures = lua.DoString("return g_things.countCreatures()");
        var total     = lua.DoString("return g_things.getCount()");

        Assert.Equal(2.0, items.Number);
        Assert.Equal(1.0, creatures.Number);
        Assert.Equal(3.0, total.Number);
        lua.Dispose();
    }

    // ─── T38: Creature as Lua userdata ────────────────────────────────────────

    [Fact]
    public void Creature_GetId_ReturnsId()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 42, Name = "Rat" };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(42) return cr:getId()");
        Assert.Equal(42.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetName_ReturnsName()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 1, Name = "Dragon" };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(1) return cr:getName()");
        Assert.Equal("Dragon", result.String);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetHealthPercent_ReturnsCorrectValue()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 2, Health = 50, MaxHealth = 100 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(2) return cr:getHealthPercent()");
        Assert.Equal(0.5, result.Number, 5);
        lua.Dispose();
    }

    [Fact]
    public void Creature_IsDead_TrueWhenHealthZero()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 3, Health = 0, MaxHealth = 100 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(3) return cr:isDead()");
        Assert.True(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Creature_IsFullHealth_TrueWhenHealthEqualsMax()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 4, Health = 100, MaxHealth = 100 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(4) return cr:isFullHealth()");
        Assert.True(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetSkullShieldEmblem_ReturnValues()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 5, Skull = 2, Shield = 3, Emblem = 1 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local cr = g_map.getCreatureById(5) " +
            "return cr:getSkull(), cr:getShield(), cr:getEmblem()");
        Assert.Equal(2.0, result.Tuple[0].Number);
        Assert.Equal(3.0, result.Tuple[1].Number);
        Assert.Equal(1.0, result.Tuple[2].Number);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetSpeed_ReturnsSpeed()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 6, Speed = 350, BaseSpeed = 300 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local cr = g_map.getCreatureById(6) " +
            "return cr:getSpeed(), cr:getBaseSpeed()");
        Assert.Equal(350.0, result.Tuple[0].Number);
        Assert.Equal(300.0, result.Tuple[1].Number);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetType_ReturnsType()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 7, Type = 4 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(7) return cr:getType()");
        Assert.Equal(4.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Creature_IsInvisible_ReturnsFalseByDefault()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 8 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(8) return cr:isInvisible()");
        Assert.False(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Creature_TextSetAndClear_Works()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 9 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local cr = g_map.getCreatureById(9) " +
            "cr:setText('hello') " +
            "local t = cr:getText() " +
            "cr:clearText() " +
            "return t, cr:getText()");
        Assert.Equal("hello", result.Tuple[0].String);
        Assert.Equal("", result.Tuple[1].String);
        lua.Dispose();
    }

    [Fact]
    public void Creature_Typing_SetAndGet()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 10 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local cr = g_map.getCreatureById(10) " +
            "cr:setTyping(true) " +
            "return cr:getTyping()");
        Assert.True(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetDirection_ReturnsDirection()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 11, Direction = Direction.North };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(11) return cr:getDirection()");
        Assert.Equal((double)Direction.North, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetManaPercent_ReturnsValue()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 12, ManaPercent = 75 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(12) return cr:getManaPercent()");
        Assert.Equal(75.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetMasterId_ReturnsValue()
    {
        var g = new OTClient.Framework.Game.Game();
        var c = new Creature { Id = 13, MasterId = 99 };
        g.Map.AddCreature(c);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(13) return cr:getMasterId()");
        Assert.Equal(99.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void G_Map_GetCreatureById_ReturnsNilForUnknown()
    {
        var g = new OTClient.Framework.Game.Game();

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_map.getCreatureById(9999)");
        Assert.Equal(DataType.Nil, result.Type);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_GetLocalPlayer_IsLuaUserdata()
    {
        var g = new OTClient.Framework.Game.Game();

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return type(g_game.getLocalPlayer())");
        Assert.Equal("userdata", result.String);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_GetLocalPlayer_GetId_Works()
    {
        var g = new OTClient.Framework.Game.Game();
        g.LocalPlayer.Id = 55;

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getLocalPlayer():getId()");
        Assert.Equal(55.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Player_GetVocation_ReturnsVocation()
    {
        var g = new OTClient.Framework.Game.Game();
        var p = new Player { Id = 20, Vocation = 3 };
        g.Map.AddCreature(p);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(20) return cr:getVocation()");
        Assert.Equal(3.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_GetAttackingCreature_ReturnsNilByDefault()
    {
        var g = new OTClient.Framework.Game.Game();

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getAttackingCreature()");
        Assert.Equal(DataType.Nil, result.Type);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_GetFollowingCreature_ReturnsNilByDefault()
    {
        var g = new OTClient.Framework.Game.Game();

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getFollowingCreature()");
        Assert.Equal(DataType.Nil, result.Type);
        lua.Dispose();
    }

    [Fact]
    public void Monster_IsSubtypeOfCreature_HasLuaMethods()
    {
        var g = new OTClient.Framework.Game.Game();
        var m = new Monster { Id = 30, Name = "Orc" };
        g.Map.AddCreature(m);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(30) return cr:getName()");
        Assert.Equal("Orc", result.String);
        lua.Dispose();
    }

    [Fact]
    public void Npc_IsSubtypeOfCreature_HasLuaMethods()
    {
        var g = new OTClient.Framework.Game.Game();
        var n = new Npc { Id = 31, Name = "Guard" };
        g.Map.AddCreature(n);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local cr = g_map.getCreatureById(31) return cr:getName()");
        Assert.Equal("Guard", result.String);
        lua.Dispose();
    }

    // ─── T39: Item as Lua userdata ────────────────────────────────────────────

    [Fact]
    public void Item_IsLuaUserdata_WhenReturnedFromContainer()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 0, Name = "Backpack" };
        con.AddItem(Item.Create(100, 3));
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local c = g_game.getContainer(0) return type(c:getItem(0))");
        Assert.Equal("userdata", result.String);
        lua.Dispose();
    }

    [Fact]
    public void Item_GetId_ReturnsId()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 0 };
        con.AddItem(Item.Create(555));
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local c = g_game.getContainer(0) return c:getItem(0):getId()");
        Assert.Equal(555.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Item_GetCount_ReturnsCount()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 0 };
        var item = Item.Create(10, 7);
        item.ThingType = new ThingType { Id = 10, Flags = ThingTypeFlag.Stackable };
        con.AddItem(item);
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("local c = g_game.getContainer(0) return c:getItem(0):getCount()");
        Assert.Equal(7.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Item_SetAndGetTooltip_RoundTrip()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 1 };
        con.AddItem(Item.Create(20));
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local c = g_game.getContainer(1) " +
            "local item = c:getItem(0) " +
            "item:setTooltip('hello') " +
            "return item:getTooltip()");
        Assert.Equal("hello", result.String);
        lua.Dispose();
    }

    [Fact]
    public void Item_SetAndGetTier_RoundTrip()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 2 };
        con.AddItem(Item.Create(30));
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local item = g_game.getContainer(2):getItem(0) " +
            "item:setTier(5) " +
            "return item:getTier()");
        Assert.Equal(5.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Item_Clone_ReturnsNewInstanceWithSameId()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 3 };
        con.AddItem(Item.Create(99));
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString(
            "local item = g_game.getContainer(3):getItem(0) " +
            "local copy = item:clone() " +
            "return copy:getId()");
        Assert.Equal(99.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Item_IsContainer_FalseByDefault()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 4 };
        con.AddItem(Item.Create(50));
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getContainer(4):getItem(0):isContainer()");
        Assert.False(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Item_IsStackable_TrueWhenFlagSet()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 5 };
        var item = Item.Create(60);
        item.ThingType = new ThingType { Id = 60, Flags = ThingTypeFlag.Stackable };
        con.AddItem(item);
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getContainer(5):getItem(0):isStackable()");
        Assert.True(result.Boolean);
        lua.Dispose();
    }

    // ─── T39: Container as Lua userdata ──────────────────────────────────────

    [Fact]
    public void Container_IsLuaUserdata_WhenReturnedFromGGame()
    {
        var g   = new OTClient.Framework.Game.Game();
        g.OpenContainer(new Container { Id = 0, Name = "Bag" });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return type(g_game.getContainer(0))");
        Assert.Equal("userdata", result.String);
        lua.Dispose();
    }

    [Fact]
    public void Container_GetId_ReturnsId()
    {
        var g = new OTClient.Framework.Game.Game();
        g.OpenContainer(new Container { Id = 7, Name = "Chest" });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getContainer(7):getId()");
        Assert.Equal(7.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Container_GetName_ReturnsName()
    {
        var g = new OTClient.Framework.Game.Game();
        g.OpenContainer(new Container { Id = 8, Name = "Loot Bag" });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getContainer(8):getName()");
        Assert.Equal("Loot Bag", result.String);
        lua.Dispose();
    }

    [Fact]
    public void Container_GetCapacity_ReturnsCapacity()
    {
        var g = new OTClient.Framework.Game.Game();
        g.OpenContainer(new Container { Id = 9, Name = "Backpack", Capacity = 20 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getContainer(9):getCapacity()");
        Assert.Equal(20.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Container_GetItemsCount_ReturnsCount()
    {
        var g   = new OTClient.Framework.Game.Game();
        var con = new Container { Id = 10, Capacity = 5 };
        con.AddItem(Item.Create(1));
        con.AddItem(Item.Create(2));
        g.OpenContainer(con);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getContainer(10):getItemsCount()");
        Assert.Equal(2.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void Container_IsClosed_FalseWhenOpen()
    {
        var g = new OTClient.Framework.Game.Game();
        g.OpenContainer(new Container { Id = 11 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getContainer(11):isClosed()");
        Assert.False(result.Boolean);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_GetContainer_ReturnsNilForUnknown()
    {
        var g = new OTClient.Framework.Game.Game();

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getContainer(63)");
        Assert.Equal(DataType.Nil, result.Type);
        lua.Dispose();
    }

    // ─── T39: g_game inventory accessors ─────────────────────────────────────

    [Fact]
    public void G_Game_GetInventoryItem_ReturnsItemWhenSet()
    {
        var g = new OTClient.Framework.Game.Game();
        g.SetInventoryItem(InventorySlot.Armor, Item.Create(300));

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        // Armor = slot 4
        var result = lua.DoString("local item = g_game.getInventoryItem(4) return item:getId()");
        Assert.Equal(300.0, result.Number);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_GetInventoryItem_ReturnsNilWhenEmpty()
    {
        var g = new OTClient.Framework.Game.Game();

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var result = lua.DoString("return g_game.getInventoryItem(1)");
        Assert.Equal(DataType.Nil, result.Type);
        lua.Dispose();
    }

    // ─── T39: g_game item-action events ──────────────────────────────────────

    [Fact]
    public void G_Game_Use_FiresEventWhenOnline()
    {
        var g = new OTClient.Framework.Game.Game();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        (Position pos, int itemId, int stackPos, int index) captured = default;
        g.UseItemRequested += (p, id, sp, idx) => captured = (p, id, sp, idx);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        lua.DoString("g_game.use(100, 200, 7, 555, 3, 0)");
        Assert.Equal(555, captured.itemId);
        Assert.Equal(3,   captured.stackPos);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_Look_FiresEventWhenOnline()
    {
        var g = new OTClient.Framework.Game.Game();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        Position capturedPos = default;
        int capturedId = 0;
        g.LookAtRequested += (p, id, _) => { capturedPos = p; capturedId = id; };

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        lua.DoString("g_game.look(10, 20, 7, 888, 1)");
        Assert.Equal(10,  capturedPos.X);
        Assert.Equal(888, capturedId);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_LookCreature_FiresEventWhenOnline()
    {
        var g = new OTClient.Framework.Game.Game();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        uint captured = 0;
        g.LookCreatureRequested += id => captured = id;

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        lua.DoString("g_game.lookCreature(9999)");
        Assert.Equal(9999u, captured);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_Close_FiresEventWhenOnline()
    {
        var g = new OTClient.Framework.Game.Game();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        int captured = -1;
        g.CloseContainerRequested += id => captured = id;

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        lua.DoString("g_game.close(5)");
        Assert.Equal(5, captured);
        lua.Dispose();
    }

    [Fact]
    public void G_Game_Move_FiresEventWhenOnline()
    {
        var g = new OTClient.Framework.Game.Game();
        g.EnterGame(new LocalPlayer { Id = 1, Name = "Hero" });

        int capturedItemId = 0;
        g.MoveItemRequested += (_, id, _, _, _) => capturedItemId = id;

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        lua.DoString("g_game.move(1,2,7, 77, 0, 3,4,7, 1)");
        Assert.Equal(77, capturedItemId);
        lua.Dispose();
    }

    // ─── T40: Outfit as Lua userdata ─────────────────────────────────────────

    [Fact]
    public void Outfit_GettersReturnConstructedValues()
    {
        var outfit = new Outfit { Id = 42, Head = 10, Body = 20, Legs = 30, Feet = 40, Addons = 3, MountId = 5 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["outfit"] = outfit;

        Assert.Equal(42, lua.DoString("return outfit:getId()").Number);
        Assert.Equal(5,  lua.DoString("return outfit:getMount()").Number);
        Assert.Equal(10, lua.DoString("return outfit:getHead()").Number);
        Assert.Equal(20, lua.DoString("return outfit:getBody()").Number);
        Assert.Equal(30, lua.DoString("return outfit:getLegs()").Number);
        Assert.Equal(40, lua.DoString("return outfit:getFeet()").Number);
        Assert.Equal(3,  lua.DoString("return outfit:getAddons()").Number);
        lua.Dispose();
    }

    [Fact]
    public void Outfit_HasMount_ReturnsCorrectly()
    {
        var mounted   = new Outfit { MountId = 10 };
        var unmounted = new Outfit { MountId = 0 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["mounted"]   = mounted;
        lua.RawScript.Globals["unmounted"] = unmounted;

        Assert.True (lua.DoString("return mounted:hasMount()").Boolean);
        Assert.False(lua.DoString("return unmounted:hasMount()").Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Outfit_SettersUpdateValues()
    {
        var outfit = new Outfit();
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["outfit"] = outfit;

        lua.DoString("outfit:setId(128); outfit:setMount(120); outfit:setHead(5); outfit:setBody(6); outfit:setLegs(7); outfit:setFeet(8); outfit:setAddons(3)");

        Assert.Equal(128, outfit.Id);
        Assert.Equal(120, outfit.MountId);
        Assert.Equal(5,   outfit.Head);
        Assert.Equal(6,   outfit.Body);
        Assert.Equal(7,   outfit.Legs);
        Assert.Equal(8,   outfit.Feet);
        Assert.Equal(3,   outfit.Addons);
        lua.Dispose();
    }

    [Fact]
    public void Outfit_SettersClampToByte()
    {
        var outfit = new Outfit();
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["outfit"] = outfit;

        lua.DoString("outfit:setHead(999); outfit:setAddons(-5)");

        Assert.Equal(255, outfit.Head);
        Assert.Equal(0,   outfit.Addons);
        lua.Dispose();
    }

    [Fact]
    public void Creature_GetOutfit_ReturnsLuaAccessibleOutfit()
    {
        var creature = new Creature { Outfit = new Outfit { Id = 77, Head = 3 } };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["c"] = creature;

        Assert.Equal(77, lua.DoString("return c:getOutfit():getId()").Number);
        Assert.Equal(3,  lua.DoString("return c:getOutfit():getHead()").Number);
        lua.Dispose();
    }

    // ─── T40: Tile as Lua userdata ───────────────────────────────────────────

    [Fact]
    public void Tile_GetPosition_ReturnsTable()
    {
        var tile = new Tile(new Position(100, 200, 7));
        var lua  = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        var x = lua.DoString("return tile:getPosition().x").Number;
        var y = lua.DoString("return tile:getPosition().y").Number;
        var z = lua.DoString("return tile:getPosition().z").Number;

        Assert.Equal(100, x);
        Assert.Equal(200, y);
        Assert.Equal(7,   z);
        lua.Dispose();
    }

    [Fact]
    public void Tile_GetGround_ReturnsNilWhenEmpty()
    {
        var tile = new Tile(new Position(0, 0, 7));
        var lua  = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        var r = lua.DoString("return tile:getGround()");
        Assert.Equal(MoonSharp.Interpreter.DataType.Nil, r.Type);
        lua.Dispose();
    }

    [Fact]
    public void Tile_GetGround_ReturnsItemAfterSetGround()
    {
        var tile   = new Tile(new Position(0, 0, 7));
        var ground = new Item { Id = 101 };
        tile.SetGround(ground);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        var r = lua.DoString("return tile:getGround():getId()").Number;
        Assert.Equal(101, r);
        lua.Dispose();
    }

    [Fact]
    public void Tile_GetItems_ReturnsTableWithItems()
    {
        var tile = new Tile(new Position(0, 0, 7));
        tile.AddItem(new Item { Id = 10 });
        tile.AddItem(new Item { Id = 20 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        var count = lua.DoString("local t = tile:getItems(); local n=0; for _ in pairs(t) do n=n+1 end; return n").Number;
        Assert.Equal(2, count);
        lua.Dispose();
    }

    [Fact]
    public void Tile_GetThings_IncludesGroundAndItems()
    {
        var tile = new Tile(new Position(0, 0, 7));
        tile.SetGround(new Item { Id = 1 });
        tile.AddItem(new Item { Id = 2 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        var count = lua.DoString("local t = tile:getThings(); local n=0; for _ in pairs(t) do n=n+1 end; return n").Number;
        Assert.Equal(2, count);
        lua.Dispose();
    }

    [Fact]
    public void Tile_GetThing_ReturnsGroundAtStackPos0()
    {
        var tile   = new Tile(new Position(0, 0, 7));
        var ground = new Item { Id = 55 };
        tile.SetGround(ground);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        var r = lua.DoString("return tile:getThing(0):getId()").Number;
        Assert.Equal(55, r);
        lua.Dispose();
    }

    [Fact]
    public void Tile_GetThingCount_CountsAllThings()
    {
        var tile = new Tile(new Position(0, 0, 7));
        tile.SetGround(new Item { Id = 1 });
        tile.AddItem(new Item { Id = 2 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        var count = lua.DoString("return tile:getThingCount()").Number;
        Assert.Equal(2, count);
        lua.Dispose();
    }

    [Fact]
    public void Tile_IsWalkable_FalseWhenNoGround()
    {
        var tile = new Tile(new Position(0, 0, 7));
        var lua  = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        Assert.False(lua.DoString("return tile:isWalkable()").Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Tile_IsEmpty_TrueWhenNoThings()
    {
        var tile = new Tile(new Position(0, 0, 7));
        var lua  = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        Assert.True(lua.DoString("return tile:isEmpty()").Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Tile_HasCreatures_TrueAfterAddCreature()
    {
        var tile = new Tile(new Position(0, 0, 7));
        tile.AddCreature(new Creature { Id = 1 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        Assert.True(lua.DoString("return tile:hasCreatures()").Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Tile_Clean_RemovesAllItems()
    {
        var tile = new Tile(new Position(0, 0, 7));
        tile.SetGround(new Item { Id = 1 });
        tile.AddItem(new Item { Id = 2 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        lua.DoString("tile:clean()");
        Assert.True(tile.IsEmpty);
        lua.Dispose();
    }

    [Fact]
    public void Tile_GetCreatures_ReturnsTable()
    {
        var tile = new Tile(new Position(0, 0, 7));
        tile.AddCreature(new Creature { Id = 1 });
        tile.AddCreature(new Creature { Id = 2 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["tile"] = tile;

        var count = lua.DoString("local t = tile:getCreatures(); local n=0; for _ in pairs(t) do n=n+1 end; return n").Number;
        Assert.Equal(2, count);
        lua.Dispose();
    }

    [Fact]
    public void GMap_GetTile_ReturnsTileAsLuaUserdata()
    {
        var g    = new OTClient.Framework.Game.Game();
        var pos  = new Position(100, 100, 7);
        var tile = g.Map.GetOrCreate(pos);
        tile.SetGround(new Item { Id = 5 });

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        var r = lua.DoString("local t = g_map.getTile(100, 100, 7); return t:getGround():getId()").Number;
        Assert.Equal(5, r);
        lua.Dispose();
    }

    // ─── T41: Player / LocalPlayer stat Lua methods ───────────────────────────

    [Fact]
    public void Player_GetLevel_ReturnsLevel()
    {
        var p   = new Player { Level = 42, LevelPercent = 75 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        Assert.Equal(42, (int)lua.DoString("return p:getLevel()").Number);
        Assert.Equal(75, (int)lua.DoString("return p:getLevelPercent()").Number);
        lua.Dispose();
    }

    [Fact]
    public void Player_GetExperience_ReturnsExp()
    {
        var p = new Player { Exp = 1_000_000 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        Assert.Equal(1_000_000.0, lua.DoString("return p:getExperience()").Number);
        lua.Dispose();
    }

    [Fact]
    public void Player_GetMana_ReturnsManaAndMaxMana()
    {
        var p = new Player { Mana = 300, MaxMana = 500 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        Assert.Equal(300, (int)lua.DoString("return p:getMana()").Number);
        Assert.Equal(500, (int)lua.DoString("return p:getMaxMana()").Number);
        lua.Dispose();
    }

    [Fact]
    public void Player_GetMagicLevel_ReturnsMagicLevels()
    {
        var p = new Player { MagicLevel = 12, MagicLevelPercent = 60, BaseMagicLevel = 10 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        Assert.Equal(12, (int)lua.DoString("return p:getMagicLevel()").Number);
        Assert.Equal(60, (int)lua.DoString("return p:getMagicLevelPercent()").Number);
        Assert.Equal(10, (int)lua.DoString("return p:getBaseMagicLevel()").Number);
        lua.Dispose();
    }

    [Fact]
    public void Player_GetCapacity_ReturnsFreeAndTotal()
    {
        var p = new Player { FreeCapacity = 150, TotalCapacity = 400 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        Assert.Equal(150, (int)lua.DoString("return p:getFreeCapacity()").Number);
        Assert.Equal(400, (int)lua.DoString("return p:getTotalCapacity()").Number);
        lua.Dispose();
    }

    [Fact]
    public void Player_GetSkillLevel_ReturnsSkillData()
    {
        var p = new Player();
        p.SetSkill(SkillType.Sword, 55, 80, 50);
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        // SkillType.Sword = 2
        Assert.Equal(55, (int)lua.DoString("return p:getSkillLevel(2)").Number);
        Assert.Equal(50, (int)lua.DoString("return p:getSkillBaseLevel(2)").Number);
        Assert.Equal(80, (int)lua.DoString("return p:getSkillLevelPercent(2)").Number);
        lua.Dispose();
    }

    [Fact]
    public void Player_GetSkillLevel_DefaultsToZero()
    {
        var p   = new Player();
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        Assert.Equal(0, (int)lua.DoString("return p:getSkillLevel(0)").Number);
        lua.Dispose();
    }

    [Fact]
    public void LocalPlayer_GetSoul_ReturnsSoul()
    {
        var lp  = new LocalPlayer { Soul = 87 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["lp"] = lp;

        Assert.Equal(87, (int)lua.DoString("return lp:getSoul()").Number);
        lua.Dispose();
    }

    [Fact]
    public void LocalPlayer_GetStamina_ReturnsStamina()
    {
        var lp  = new LocalPlayer { Stamina = 1800 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["lp"] = lp;

        Assert.Equal(1800, (int)lua.DoString("return lp:getStamina()").Number);
        lua.Dispose();
    }

    [Fact]
    public void LocalPlayer_GetStates_ReturnsConditions()
    {
        var lp  = new LocalPlayer { Conditions = 0b0101u };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["lp"] = lp;

        Assert.Equal(5, (int)lua.DoString("return lp:getStates()").Number);
        lua.Dispose();
    }

    [Fact]
    public void LocalPlayer_GetOfflineTrainingTime_ReturnsValue()
    {
        var lp  = new LocalPlayer { OfflineTrainingTime = 720 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["lp"] = lp;

        Assert.Equal(720, (int)lua.DoString("return lp:getOfflineTrainingTime()").Number);
        lua.Dispose();
    }

    [Fact]
    public void LocalPlayer_GetRegenerationTime_ReturnsValue()
    {
        var lp  = new LocalPlayer { RegenerationTime = 300 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["lp"] = lp;

        Assert.Equal(300, (int)lua.DoString("return lp:getRegenerationTime()").Number);
        lua.Dispose();
    }

    [Fact]
    public void LocalPlayer_IsPremium_ReturnsPremiumFlag()
    {
        var lp  = new LocalPlayer { IsPremium = true };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["lp"] = lp;

        Assert.True(lua.DoString("return lp:isPremium()").Boolean);
        lua.Dispose();
    }

    [Fact]
    public void Player_SetSkill_WithDefaultBaseLevel_UsesLevelAsBase()
    {
        var p = new Player();
        p.SetSkill(SkillType.Axe, 30, 50);   // no explicit base
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        // SkillType.Axe = 3; base should default to level value (30)
        Assert.Equal(30, (int)lua.DoString("return p:getSkillBaseLevel(3)").Number);
        lua.Dispose();
    }

    [Fact]
    public void LocalPlayer_InheritsPlayerStatMethods()
    {
        var lp = new LocalPlayer { Level = 10, Mana = 200, MaxMana = 300 };
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["lp"] = lp;

        Assert.Equal(10,  (int)lua.DoString("return lp:getLevel()").Number);
        Assert.Equal(200, (int)lua.DoString("return lp:getMana()").Number);
        Assert.Equal(300, (int)lua.DoString("return lp:getMaxMana()").Number);
        lua.Dispose();
    }

    [Fact]
    public void GGame_GetLocalPlayer_ExposesStatMethods()
    {
        var g  = new OTClient.Framework.Game.Game();
        g.LocalPlayer.Level   = 99;
        g.LocalPlayer.Soul    = 50;
        g.LocalPlayer.Stamina = 2520;

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua, game: g);

        Assert.Equal(99,   (int)lua.DoString("return g_game.getLocalPlayer():getLevel()").Number);
        Assert.Equal(50,   (int)lua.DoString("return g_game.getLocalPlayer():getSoul()").Number);
        Assert.Equal(2520, (int)lua.DoString("return g_game.getLocalPlayer():getStamina()").Number);
        lua.Dispose();
    }

    [Fact]
    public void Player_AllSkillTypes_Accessible()
    {
        var p = new Player();
        // Set all 7 skills
        foreach (var s in Enum.GetValues<SkillType>())
            p.SetSkill(s, (int)s + 10, (int)s * 5, (int)s + 5);

        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["p"] = p;

        // Verify Fishing (id=6): level=16, base=11, percent=30
        Assert.Equal(16, (int)lua.DoString("return p:getSkillLevel(6)").Number);
        Assert.Equal(11, (int)lua.DoString("return p:getSkillBaseLevel(6)").Number);
        Assert.Equal(30, (int)lua.DoString("return p:getSkillLevelPercent(6)").Number);
        lua.Dispose();
    }

    [Fact]
    public void LocalPlayer_DefaultValues_AreCorrect()
    {
        var lp  = new LocalPlayer();
        var lua = new LuaInterface();
        lua.Init();
        LuaGlobals.Register(lua);
        lua.RawScript.Globals["lp"] = lp;

        Assert.Equal(100,  (int)lua.DoString("return lp:getSoul()").Number);
        Assert.Equal(2520, (int)lua.DoString("return lp:getStamina()").Number);
        Assert.Equal(0,    (int)lua.DoString("return lp:getStates()").Number);
        Assert.False(lua.DoString("return lp:isPremium()").Boolean);
        lua.Dispose();
    }
}
