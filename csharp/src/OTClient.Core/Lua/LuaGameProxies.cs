using MoonSharp.Interpreter;
using System.Numerics;

namespace OTClient.Framework.Lua;

// ─── g_graphics ───────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the graphics context, exposed as <c>g_graphics</c>.
/// Surfaces resolution, frame-rate, and drawing-state queries.
/// Task 6.6.
/// </summary>
[LuaBinding("g_graphics")]
[MoonSharpUserData]
public sealed class LuaGraphicsProxy
{
    [LuaMethod] public int  getWidth()        => Raylib_cs.Raylib.GetScreenWidth();
    [LuaMethod] public int  getHeight()       => Raylib_cs.Raylib.GetScreenHeight();
    [LuaMethod] public int  getFPS()          => Raylib_cs.Raylib.GetFPS();
    [LuaMethod] public bool isWindowFocused() => Raylib_cs.Raylib.IsWindowFocused();
    [LuaMethod] public bool isFullscreen()    => Raylib_cs.Raylib.IsWindowFullscreen();
    [LuaMethod] public void setFullscreen(bool v)
    {
        bool current = Raylib_cs.Raylib.IsWindowFullscreen();
        if (current != v) Raylib_cs.Raylib.ToggleFullscreen();
    }
    [LuaMethod] public string getVendor()   => "Raylib";
    [LuaMethod] public string getRenderer() => "Raylib";
    [LuaMethod] public string getVersion()  => "5.x";
}

// ─── g_textures ───────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for texture management, exposed as <c>g_textures</c>.
/// Task 6.6.
/// </summary>
[LuaBinding("g_textures")]
[MoonSharpUserData]
public sealed class LuaTexturesProxy
{
    private readonly Dictionary<string, Graphics.Texture> _cache
        = new(StringComparer.OrdinalIgnoreCase);

    [LuaMethod]
    public Graphics.Texture? getTexture(string path)
    {
        if (_cache.TryGetValue(path, out var tex)) return tex;
        return null;
    }

    [LuaMethod] public int  getCacheSize() => _cache.Count;
    [LuaMethod] public void clearCache()   { foreach (var t in _cache.Values) t.Dispose(); _cache.Clear(); }
}

// ─── g_fonts ──────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for font management, exposed as <c>g_fonts</c>.
/// Task 6.6.
/// </summary>
[LuaBinding("g_fonts")]
[MoonSharpUserData]
public sealed class LuaFontsProxy
{
    private readonly Graphics.FontManager _fontManager;

    public LuaFontsProxy(Graphics.FontManager fontManager)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        _fontManager = fontManager;
    }

    [LuaMethod]
    public bool hasFont(string name) => _fontManager.TryGet(name) is not null;

    [LuaMethod]
    public void setDefaultFont(string name) { /* stub — font selection */ }

    [LuaMethod]
    public string getDefaultFont() => "default";
}

// ─── g_drawpool ───────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the draw pool manager, exposed as <c>g_drawpool</c>.
/// Task 6.6.
/// </summary>
[LuaBinding("g_drawpool")]
[MoonSharpUserData]
public sealed class LuaDrawPoolProxy
{
    [LuaMethod] public void beginDraw(int layer) { /* stub */ }
    [LuaMethod] public void endDraw()            { /* stub */ }
    [LuaMethod] public void flush()              { /* stub */ }
}

// ─── g_keyboard ───────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for keyboard input, exposed as <c>g_keyboard</c>.
/// Task 6.7.
/// </summary>
[LuaBinding("g_keyboard")]
[MoonSharpUserData]
public sealed class LuaKeyboardProxy
{
    [LuaMethod]
    public bool isKeyDown(string keyName)
    {
        if (Enum.TryParse<Input.Key>(keyName, ignoreCase: true, out var k))
            return Raylib_cs.Raylib.IsKeyDown((Raylib_cs.KeyboardKey)(int)k);
        return false;
    }

    [LuaMethod]
    public bool isKeyPressed(string keyName)
    {
        if (Enum.TryParse<Input.Key>(keyName, ignoreCase: true, out var k))
            return Raylib_cs.Raylib.IsKeyPressed((Raylib_cs.KeyboardKey)(int)k);
        return false;
    }

    [LuaMethod]
    public bool isKeyReleased(string keyName)
    {
        if (Enum.TryParse<Input.Key>(keyName, ignoreCase: true, out var k))
            return Raylib_cs.Raylib.IsKeyReleased((Raylib_cs.KeyboardKey)(int)k);
        return false;
    }

    [LuaMethod] public bool isCtrlDown()  => Raylib_cs.Raylib.IsKeyDown(Raylib_cs.KeyboardKey.LeftControl)
                                          || Raylib_cs.Raylib.IsKeyDown(Raylib_cs.KeyboardKey.RightControl);
    [LuaMethod] public bool isShiftDown() => Raylib_cs.Raylib.IsKeyDown(Raylib_cs.KeyboardKey.LeftShift)
                                          || Raylib_cs.Raylib.IsKeyDown(Raylib_cs.KeyboardKey.RightShift);
    [LuaMethod] public bool isAltDown()   => Raylib_cs.Raylib.IsKeyDown(Raylib_cs.KeyboardKey.LeftAlt)
                                          || Raylib_cs.Raylib.IsKeyDown(Raylib_cs.KeyboardKey.RightAlt);
}

// ─── g_mouse ──────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for mouse input, exposed as <c>g_mouse</c>.
/// Task 6.7.
/// </summary>
[LuaBinding("g_mouse")]
[MoonSharpUserData]
public sealed class LuaMouseProxy
{
    [LuaMethod] public float getX()            => Raylib_cs.Raylib.GetMousePosition().X;
    [LuaMethod] public float getY()            => Raylib_cs.Raylib.GetMousePosition().Y;
    [LuaMethod] public float getDeltaX()       => Raylib_cs.Raylib.GetMouseDelta().X;
    [LuaMethod] public float getDeltaY()       => Raylib_cs.Raylib.GetMouseDelta().Y;
    [LuaMethod] public float getScrollWheel()  => Raylib_cs.Raylib.GetMouseWheelMove();

    [LuaMethod] public bool isLeftDown()   => Raylib_cs.Raylib.IsMouseButtonDown(Raylib_cs.MouseButton.Left);
    [LuaMethod] public bool isRightDown()  => Raylib_cs.Raylib.IsMouseButtonDown(Raylib_cs.MouseButton.Right);
    [LuaMethod] public bool isMiddleDown() => Raylib_cs.Raylib.IsMouseButtonDown(Raylib_cs.MouseButton.Middle);

    [LuaMethod] public bool isLeftPressed()   => Raylib_cs.Raylib.IsMouseButtonPressed(Raylib_cs.MouseButton.Left);
    [LuaMethod] public bool isRightPressed()  => Raylib_cs.Raylib.IsMouseButtonPressed(Raylib_cs.MouseButton.Right);
    [LuaMethod] public bool isMiddlePressed() => Raylib_cs.Raylib.IsMouseButtonPressed(Raylib_cs.MouseButton.Middle);
}

// ─── g_game ───────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for game state, exposed as <c>g_game</c>.
/// Task 6.10.
/// </summary>
[LuaBinding("g_game")]
[MoonSharpUserData]
public sealed class LuaGameProxy
{
    private readonly Game.Game _game;

    public LuaGameProxy(Game.Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        _game = game;
    }

    [LuaMethod] public bool   isOnline()          => _game.State == Game.GameState.InGame;
    [LuaMethod] public bool   isLogging()         => _game.State is Game.GameState.Connecting or Game.GameState.LoginServer;
    [LuaMethod] public string getState()          => _game.State.ToString();
    [LuaMethod] public bool   isConnected()       => _game.State != Game.GameState.Disconnected;

    [LuaMethod]
    public object? getLocalPlayer()
    {
        var lp = _game.LocalPlayer;
        return lp is null ? null : (object)lp;
    }

    [LuaMethod] public void enterGame()       { /* stub — protocol sends login */ }
    [LuaMethod] public void logout()          => _game.Logout();
    [LuaMethod] public void forceLogout()     => _game.Logout();

    [LuaMethod] public int  getProtocolVersion() => 1281;
    [LuaMethod] public int  getClientVersion()   => 1281;

    [LuaMethod] public bool isGM()               => false;
    [LuaMethod] public bool hasFeature(string f) => false;

    // ─── Movement (T06/T07) ───────────────────────────────────────────────────

    /// <summary>Walk in a direction. <paramref name="dir"/> is the Tibia wire byte (0=N,1=E,2=S,3=W…).</summary>
    [LuaMethod] public void walk(int dir)     => _game.Walk((Game.Direction)dir);

    /// <summary>Turn to face a direction without moving.</summary>
    [LuaMethod] public void turn(int dir)     => _game.Turn((Game.Direction)dir);

    /// <summary>Stops all movement.</summary>
    [LuaMethod] public void stop()            => _game.Stop();

    // ─── Combat (T12) ─────────────────────────────────────────────────────────

    /// <summary>Attacks the creature with the given creature ID.</summary>
    [LuaMethod] public void attack(uint creatureId)  => _game.Attack(creatureId);

    /// <summary>Follows the creature with the given creature ID.</summary>
    [LuaMethod] public void follow(uint creatureId)  => _game.Follow(creatureId);

    /// <summary>Cancels the current attack.</summary>
    [LuaMethod] public void cancelAttack()           => _game.CancelAttack();

    /// <summary>Cancels the current follow target.</summary>
    [LuaMethod] public void cancelFollow()           => _game.CancelFollow();

    /// <summary>Cancels both attack and follow at once.</summary>
    [LuaMethod] public void cancelAttackAndFollow()  => _game.CancelAttackAndFollow();

    /// <summary>
    /// Updates fight/chase/safe/PvP mode settings.
    /// <paramref name="fightMode"/>: 1=Offensive, 2=Balanced, 3=Defensive.
    /// <paramref name="chaseMode"/>: 0=DontChase, 1=ChaseOpponent.
    /// <paramref name="pvpMode"/>: 0=WhiteDove (default), 1=WhiteHand, 2=YellowHand, 3=RedFist.
    /// Defaults to <c>pvpMode = 0</c> (WhiteDove) matching the C++ default.
    /// </summary>
    [LuaMethod]
    public void setFightModes(int fightMode, int chaseMode, bool safeFight, int pvpMode = 0)
        => _game.SetFightModes(
            (Game.FightMode)fightMode,
            (Game.ChaseMode)chaseMode,
            safeFight,
            (Game.PvpMode)pvpMode);

    // ─── Chat (T09/T10) ───────────────────────────────────────────────────────

    /// <summary>Says a message in the default channel.</summary>
    [LuaMethod] public void talk(string message)
        => _game.TalkSay(message);

    /// <summary>Sends a message to a channel by ID.</summary>
    [LuaMethod] public void talkChannel(int channelId, string message)
        => _game.TalkChannel((ushort)channelId, message);

    /// <summary>Sends a private message to a player.</summary>
    [LuaMethod] public void talkPrivate(string receiver, string message)
        => _game.TalkPrivate(receiver, message);

    /// <summary>Requests the list of available channels.</summary>
    [LuaMethod] public void requestChannels()
        => _game.RequestChannels();

    /// <summary>Joins a channel by ID.</summary>
    [LuaMethod] public void joinChannel(int channelId)
        => _game.JoinChannel((ushort)channelId);

    /// <summary>Leaves a channel by ID.</summary>
    [LuaMethod] public void leaveChannel(int channelId)
        => _game.LeaveChannel((ushort)channelId);

    /// <summary>Opens a private chat channel with a player.</summary>
    [LuaMethod] public void openPrivateChannel(string player)
        => _game.OpenPrivateChannel(player);

    // ─── NPC trade (T15) ──────────────────────────────────────────────────────

    /// <summary>Requests detailed info for an NPC trade item.</summary>
    [LuaMethod] public void inspectNpcTrade(int itemId, int count = 1)
        => _game.InspectNpcTrade(itemId, count);

    /// <summary>Buys an item from the active NPC.</summary>
    [LuaMethod] public void buyItem(int itemId, int subType, int amount,
        bool ignoreCapacity = false, bool buyWithBackpack = false)
        => _game.BuyItem(itemId, subType, amount, ignoreCapacity, buyWithBackpack);

    /// <summary>Sells an item to the active NPC.</summary>
    [LuaMethod] public void sellItem(int itemId, int subType, int amount,
        bool ignoreEquipped = false)
        => _game.SellItem(itemId, subType, amount, ignoreEquipped);

    /// <summary>Closes the active NPC trade window.</summary>
    [LuaMethod] public void closeNpcTrade()
        => _game.CloseNpcTrade();

    // ─── Player-to-player trade (T16) ─────────────────────────────────────────

    /// <summary>
    /// Initiates a player trade for the item at the given world position.
    /// Parameters: <paramref name="x"/>/<paramref name="y"/>/<paramref name="z"/> — world position of the item;
    /// <paramref name="itemId"/> — item type ID; <paramref name="stackPos"/> — stack position on tile;
    /// <paramref name="creatureId"/> — target player creature ID.
    /// </summary>
    [LuaMethod] public void requestTrade(int x, int y, int z, int itemId, int stackPos, uint creatureId)
        => _game.RequestTrade(new Game.Position((ushort)x, (ushort)y, (byte)z), itemId, stackPos, creatureId);

    /// <summary>Inspects a slot in the current trade (own or partner).</summary>
    [LuaMethod] public void inspectTrade(bool counterOffer, int index)
        => _game.InspectTrade(counterOffer, index);

    /// <summary>Accepts the current player trade.</summary>
    [LuaMethod] public void acceptTrade()
        => _game.AcceptTrade();

    /// <summary>Rejects the current player trade.</summary>
    [LuaMethod] public void rejectTrade()
        => _game.RejectTrade();

    // ─── VIP management (T21) ─────────────────────────────────────────────────

    /// <summary>Adds a player to the VIP (friends) list.</summary>
    [LuaMethod] public void addVip(string name)
        => _game.AddVip(name);

    /// <summary>Removes a player from the VIP (friends) list by creature ID.</summary>
    [LuaMethod] public void removeVip(uint id)
        => _game.RemoveVip(id);
}

// ─── g_map ────────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for map access, exposed as <c>g_map</c>.
/// Task 6.10.
/// </summary>
[LuaBinding("g_map")]
[MoonSharpUserData]
public sealed class LuaMapProxy
{
    private readonly Game.Map _map;

    public LuaMapProxy(Game.Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
    }

    [LuaMethod]
    public object? getTile(int x, int y, int z)
    {
        var tile = _map.Get(new Game.Position((ushort)x, (ushort)y, (byte)z));
        return tile;
    }

    [LuaMethod] public bool isTileWalkable(int x, int y, int z)
    {
        var tile = _map.Get(new Game.Position((ushort)x, (ushort)y, (byte)z));
        return tile?.IsWalkable ?? false;
    }

    [LuaMethod] public int  getTileCount()  => _map.TileCount;
    [LuaMethod] public void clean()         => _map.Clear();
    [LuaMethod] public bool isLookPossible(int x, int y, int z) => true;
}

// ─── g_things ─────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the thing-type manager, exposed as <c>g_things</c>.
/// Task 6.10.
/// </summary>
[LuaBinding("g_things")]
[MoonSharpUserData]
public sealed class LuaThingsProxy
{
    private readonly Game.ThingTypeManager _things;

    public LuaThingsProxy(Game.ThingTypeManager things)
    {
        ArgumentNullException.ThrowIfNull(things);
        _things = things;
    }

    [LuaMethod]
    public object? getThingType(int id, int category)
        => _things.Get((Game.ThingCategory)category, id);

    [LuaMethod] public bool isLoaded()                          => true;
    [LuaMethod] public void loadFromFile(string path)           { /* stub */ }
}

// ─── g_sprites ────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the sprite manager, exposed as <c>g_sprites</c>.
/// Task 6.10.
/// </summary>
[LuaBinding("g_sprites")]
[MoonSharpUserData]
public sealed class LuaSpritesProxy
{
    [LuaMethod] public bool isLoaded()                => true;
    [LuaMethod] public int  getSpritesCount()         => 0;
    [LuaMethod] public void loadFromFile(string path) { /* stub */ }
}

// ─── g_creatures ──────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the creature data manager, exposed as <c>g_creatures</c>.
/// Task 6.10.
/// </summary>
[LuaBinding("g_creatures")]
[MoonSharpUserData]
public sealed class LuaCreaturesProxy
{
    private readonly Game.CreatureDataManager _creatures;

    public LuaCreaturesProxy(Game.CreatureDataManager creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        _creatures = creatures;
    }

    [LuaMethod]
    public object? getCreatureData(string name)
        => _creatures.Get(name);

    [LuaMethod] public bool exists(string name)  => _creatures.Get(name) is not null;
    [LuaMethod] public int  getCount()           => _creatures.Count;
}

// ─── g_client ─────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for miscellaneous client state, exposed as <c>g_client</c>.
/// Task 6.10.
/// </summary>
[LuaBinding("g_client")]
[MoonSharpUserData]
public sealed class LuaClientProxy
{
    [LuaMethod] public string getOs()          => Platform.OsName.ToLowerInvariant()
                                                        .Replace("macos", "mac");
    [LuaMethod] public bool   isRunning()      => true;
    [LuaMethod] public string getVersion()     => "0.0.1";
}

// ─── g_ui ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the UI manager, exposed as <c>g_ui</c>.
/// Task 6.11.
/// </summary>
[LuaBinding("g_ui")]
[MoonSharpUserData]
public sealed class LuaUiProxy
{
    private readonly UI.UIManager _uiManager;

    public LuaUiProxy(UI.UIManager uiManager)
    {
        ArgumentNullException.ThrowIfNull(uiManager);
        _uiManager = uiManager;
    }

    [LuaMethod]
    public object? getRootWidget() => _uiManager.Root;

    [LuaMethod]
    public object? createWidget(string typeName, object? parent)
    {
        var parentWidget = parent as UI.UIWidget;
        return UI.UIWidgetFactory.Create(typeName, parentWidget);
    }

    [LuaMethod]
    public object? getWidget(string id)
    {
        // Walk the root tree looking for first match on Id
        return FindById(_uiManager.Root, id);
    }

    [LuaMethod] public void clearWidgets() => _uiManager.Root.ClearChildren();

    [LuaMethod] public bool isVisible() => true;
    [LuaMethod] public void show()      { /* stub */ }
    [LuaMethod] public void hide()      { /* stub */ }

    private static UI.UIWidget? FindById(UI.UIWidget widget, string id)
    {
        if (widget.Id == id) return widget;
        foreach (var child in widget.Children)
        {
            var found = FindById(child, id);
            if (found is not null) return found;
        }
        return null;
    }
}
