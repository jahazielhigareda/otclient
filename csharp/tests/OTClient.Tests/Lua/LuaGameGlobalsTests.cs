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
}
