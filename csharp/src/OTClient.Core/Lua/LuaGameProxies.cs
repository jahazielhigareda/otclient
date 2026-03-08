using MoonSharp.Interpreter;
using System.Numerics;
using System.IO;

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

    /// <summary>
    /// Returns the creature currently being attacked, or nil.
    /// Maps to <c>Game::getAttackingCreature()</c>. Task T38.
    /// </summary>
    [LuaMethod]
    public object? getAttackingCreature()
    {
        var id = _game.AttackingCreatureId;
        if (id == 0) return null;
        return _game.Map.GetCreature(id);
    }

    /// <summary>
    /// Returns the creature currently being followed, or nil.
    /// Maps to <c>Game::getFollowingCreature()</c>. Task T38.
    /// </summary>
    [LuaMethod]
    public object? getFollowingCreature()
    {
        var id = _game.FollowingCreatureId;
        if (id == 0) return null;
        return _game.Map.GetCreature(id);
    }

    [LuaMethod] public void enterGame()       { /* stub — protocol sends login */ }
    [LuaMethod] public void logout()          => _game.Logout();
    [LuaMethod] public void forceLogout()     => _game.Logout();

    [LuaMethod] public int  getProtocolVersion() => 1281;
    [LuaMethod] public int  getClientVersion()   => 1281;

    [LuaMethod] public bool isGM()               => false;
    [LuaMethod] public bool hasFeature(string f) => false;

    // ─── Server config accessors (T44) ────────────────────────────────────────

    /// <summary>Server heartbeat interval in ms. Maps to <c>g_game:getServerBeat()</c>. Task T44.</summary>
    [LuaMethod] public int  getServerBeat()    => _game.ServerBeat;

    /// <summary>Whether the player can submit bug reports. Maps to <c>g_game:canReportBugs()</c>. Task T44.</summary>
    [LuaMethod] public bool canReportBugs()    => _game.CanReportBugs;

    /// <summary>Whether expert PvP mode is active. Maps to <c>g_game:getExpertPvpMode()</c>. Task T44.</summary>
    [LuaMethod] public bool isExpertPvpMode()  => _game.ExpertPvpMode;

    /// <summary>
    /// Returns the 20-element GM action-permission table (Lua array of integers).
    /// Maps to <c>g_game:getGMActions()</c>. Task T44.
    /// </summary>
    [LuaMethod]
    public MoonSharp.Interpreter.DynValue getGmActions()
    {
        var table = new MoonSharp.Interpreter.Table(null!);
        var actions = _game.GmActions;
        for (int i = 0; i < actions.Count; i++)
            table.Set(i + 1, MoonSharp.Interpreter.DynValue.NewNumber(actions[i]));
        return MoonSharp.Interpreter.DynValue.NewTable(table);
    }

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

    // ─── VIP management (T21 / T43) ──────────────────────────────────────────

    /// <summary>Adds a player to the VIP (friends) list.</summary>
    [LuaMethod] public void addVip(string name)
        => _game.AddVip(name);

    /// <summary>Removes a player from the VIP (friends) list by creature ID.</summary>
    [LuaMethod] public void removeVip(uint id)
        => _game.RemoveVip(id);

    /// <summary>
    /// Returns the VIP entry for the given creature ID, or nil if unknown.
    /// Maps to <c>Game::GetVip()</c>.
    /// Task T43.
    /// </summary>
    [LuaMethod] public Game.VipEntry? getVip(uint id)
        => _game.GetVip(id);

    /// <summary>
    /// Returns a list of all known VIP entries.
    /// Maps to <c>Game::getVips()</c> in the C++ client.
    /// Task T43.
    /// </summary>
    [LuaMethod] public IReadOnlyList<Game.VipEntry> getVips()
        => _game.GetVips();

    // ─── Container / Inventory accessors (T39) ────────────────────────────────

    /// <summary>
    /// Returns the open container at wire slot <paramref name="id"/>, or nil.
    /// Maps to <c>Game::getContainer(id)</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public object? getContainer(int id)
    {
        var c = _game.GetContainer(id);
        return c is not null ? (object)c : null;
    }

    /// <summary>
    /// Returns a Lua table mapping container slot ID → Container for all open containers.
    /// Maps to <c>Game::getContainers()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public MoonSharp.Interpreter.Table getContainers()
    {
        // Construct a Lua table; callers iterate by integer keys.
        var all = _game.GetContainers();
        var tbl = new MoonSharp.Interpreter.Table(null!);
        foreach (var kv in all)
            tbl[kv.Key] = kv.Value;
        return tbl;
    }

    /// <summary>
    /// Returns the item in inventory slot <paramref name="slot"/> (1-based wire value), or nil.
    /// Maps to <c>Game::getInventoryItem(slot)</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public object? getInventoryItem(int slot)
    {
        var item = _game.GetInventoryItem((Game.InventorySlot)slot);
        return item is not null ? (object)item : null;
    }

    /// <summary>
    /// Returns a Lua table mapping slot number → Item for all non-empty inventory slots.
    /// Maps to <c>Game::getInventory()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public MoonSharp.Interpreter.Table getInventory()
    {
        var inv = _game.GetInventory();
        var tbl = new MoonSharp.Interpreter.Table(null!);
        for (int i = 1; i < inv.Count; i++)
            if (inv[i] is { } item)
                tbl[i] = item;
        return tbl;
    }

    // ─── Item action methods (T39) ────────────────────────────────────────────

    /// <summary>
    /// Uses item at world position (x,y,z) with given ID, stack position and container index.
    /// Lua: <c>g_game.use(x, y, z, itemId, stackPos, index)</c>.
    /// Maps to <c>Game::use()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void use(int x, int y, int z, int itemId, int stackPos, int index = 0)
        => _game.UseItem(new Game.Position((ushort)x, (ushort)y, (byte)z), itemId, stackPos, index);

    /// <summary>
    /// Uses item at fromPos with toPos (cross-use).
    /// Lua: <c>g_game.useWith(fx,fy,fz, itemId, fsp, tx,ty,tz, toItemId, tsp)</c>.
    /// Maps to <c>Game::useWith()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void useWith(int fx, int fy, int fz, int itemId, int fromStackPos,
                        int tx, int ty, int tz, int toItemId, int toStackPos)
        => _game.UseItemWith(
            new Game.Position((ushort)fx, (ushort)fy, (byte)fz), itemId, fromStackPos,
            new Game.Position((ushort)tx, (ushort)ty, (byte)tz), toItemId, toStackPos);

    /// <summary>
    /// Uses item at pos on a creature.
    /// Lua: <c>g_game.useOnCreature(x,y,z, itemId, stackPos, creatureId)</c>.
    /// Maps to <c>Game::useOnCreature()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void useOnCreature(int x, int y, int z, int itemId, int stackPos, uint creatureId)
        => _game.UseOnCreature(new Game.Position((ushort)x, (ushort)y, (byte)z), itemId, stackPos, creatureId);

    /// <summary>
    /// Moves item from one world position to another.
    /// Lua: <c>g_game.move(fx,fy,fz, itemId, stackPos, tx,ty,tz, count)</c>.
    /// Maps to <c>Game::move()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void move(int fx, int fy, int fz, int itemId, int stackPos,
                     int tx, int ty, int tz, int count = 1)
        => _game.MoveItem(
            new Game.Position((ushort)fx, (ushort)fy, (byte)fz), itemId, stackPos,
            new Game.Position((ushort)tx, (ushort)ty, (byte)tz), count);

    /// <summary>
    /// Looks at item at world position.
    /// Lua: <c>g_game.look(x, y, z, itemId, stackPos)</c>.
    /// Maps to <c>Game::look()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void look(int x, int y, int z, int itemId, int stackPos)
        => _game.LookAt(new Game.Position((ushort)x, (ushort)y, (byte)z), itemId, stackPos);

    /// <summary>
    /// Looks at a creature by ID.
    /// Lua: <c>g_game.lookCreature(creatureId)</c>.
    /// Maps to <c>Game::lookCreature()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void lookCreature(uint creatureId)
        => _game.LookCreature(creatureId);

    /// <summary>
    /// Rotates item at world position.
    /// Lua: <c>g_game.rotate(x, y, z, itemId, stackPos)</c>.
    /// Maps to <c>Game::rotate()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void rotate(int x, int y, int z, int itemId, int stackPos)
        => _game.RotateItem(new Game.Position((ushort)x, (ushort)y, (byte)z), itemId, stackPos);

    /// <summary>
    /// Closes an open container by its wire slot ID.
    /// Lua: <c>g_game.close(containerId)</c>.
    /// Maps to <c>Game::close()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void close(int containerId)
        => _game.CloseContainer(containerId);

    /// <summary>
    /// Navigates up to the parent container.
    /// Lua: <c>g_game.openParent(containerId)</c>.
    /// Maps to <c>Game::openParent()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void openParent(int containerId)
        => _game.UpContainer(containerId);

    /// <summary>
    /// Browses the tile field at world position.
    /// Lua: <c>g_game.browseField(x, y, z)</c>.
    /// Maps to <c>Game::browseField()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void browseField(int x, int y, int z)
        => _game.BrowseField(new Game.Position((ushort)x, (ushort)y, (byte)z));

    /// <summary>
    /// Seeks to the given page in a paginated container.
    /// Lua: <c>g_game.seekInContainer(containerId, index)</c>.
    /// Maps to <c>Game::seekInContainer()</c>. Task T39.
    /// </summary>
    [LuaMethod]
    public void seekInContainer(int containerId, int index)
        => _game.SeekInContainer(containerId, index);
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

    /// <summary>
    /// Returns the creature with the given id, or nil if not found.
    /// Maps to <c>Map::getCreatureById</c>. Task T38.
    /// </summary>
    [LuaMethod]
    public object? getCreatureById(uint id)
        => _map.GetCreature(id);

    /// <summary>
    /// Returns whether a projectile can travel through the tile at (x, y, z).
    /// Maps to <c>Map::isSightClear(from, to)</c>. Task T25.
    /// </summary>
    [LuaMethod] public bool isLookPossible(int x, int y, int z)
    {
        var tile = _map.Get(new Game.Position((ushort)x, (ushort)y, (byte)z));
        return tile?.IsLookPossible ?? true;
    }

    /// <summary>
    /// Returns all creatures in the full aware range around (x, y, z).
    /// If <paramref name="multiFloor"/> is true, spans all aware floors.
    /// Maps to <c>Map::getSpectators</c>. Task T24.
    /// </summary>
    [LuaMethod]
    public MoonSharp.Interpreter.Table getSpectators(int x, int y, int z, bool multiFloor = false)
    {
        var creatures = _map.GetSpectators(
            new Game.Position((ushort)x, (ushort)y, (byte)z), multiFloor);
        return ToTable(creatures);
    }

    /// <summary>
    /// Returns all creatures in the sight-spectator range (aware − 1 tile margin).
    /// Maps to <c>Map::getSightSpectators</c>. Task T24.
    /// </summary>
    [LuaMethod]
    public MoonSharp.Interpreter.Table getSightSpectators(int x, int y, int z, bool multiFloor = false)
    {
        var creatures = _map.GetSightSpectators(
            new Game.Position((ushort)x, (ushort)y, (byte)z), multiFloor);
        return ToTable(creatures);
    }

    /// <summary>
    /// Returns all creatures within the symmetric (xRange, yRange) box.
    /// Maps to <c>Map::getSpectatorsInRange</c>. Task T24.
    /// </summary>
    [LuaMethod]
    public MoonSharp.Interpreter.Table getSpectatorsInRange(
        int x, int y, int z, bool multiFloor, int xRange, int yRange)
    {
        var creatures = _map.GetSpectatorsInRange(
            new Game.Position((ushort)x, (ushort)y, (byte)z),
            multiFloor, xRange, yRange);
        return ToTable(creatures);
    }

    /// <summary>
    /// Returns whether the tile at (x, y, z) is visually covered by a higher floor.
    /// Maps to <c>Map::isCovered</c>. Task T25.
    /// </summary>
    [LuaMethod] public bool isCovered(int x, int y, int z, int firstFloor = 0)
        => _map.IsCovered(new Game.Position((ushort)x, (ushort)y, (byte)z), firstFloor);

    /// <summary>
    /// Returns whether there is an unobstructed line of sight between two positions.
    /// Maps to <c>Map::isSightClear</c>. Task T25.
    /// </summary>
    [LuaMethod]
    public bool isSightClear(int fromX, int fromY, int fromZ, int toX, int toY, int toZ)
        => _map.IsSightClear(
            new Game.Position((ushort)fromX, (ushort)fromY, (byte)fromZ),
            new Game.Position((ushort)toX,   (ushort)toY,   (byte)toZ));

    private MoonSharp.Interpreter.Table ToTable(IReadOnlyList<Game.Creature> creatures)
    {
        // Build a Lua array table from the creature list.
        // Each entry is exposed as the Creature object itself.
        var table = new MoonSharp.Interpreter.Table(null);
        for (int i = 0; i < creatures.Count; i++)
            table[i + 1] = creatures[i];
        return table;
    }
}

// ─── g_things ─────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the thing-type manager, exposed as <c>g_things</c>.
/// Task 6.10 / T37.
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

    /// <summary>
    /// Returns the <see cref="Game.ThingType"/> for the given <paramref name="id"/>
    /// and integer <paramref name="category"/> (0=Item, 1=Creature, 2=Effect, 3=Missile).
    /// Returns <c>nil</c> when the type is not registered.
    /// Lua: <c>g_things.getThingType(id, category)</c>
    /// </summary>
    [LuaMethod]
    public object? getThingType(int id, int category)
        => _things.Get((Game.ThingCategory)category, id);

    /// <summary>Total number of registered types across all categories.</summary>
    [LuaMethod] public int  getCount()         => _things.Count;

    /// <summary>Number of registered Item types. Lua: <c>g_things.countItems()</c></summary>
    [LuaMethod] public int  countItems()        => _things.GetAll(Game.ThingCategory.Item).Count();

    /// <summary>Number of registered Creature types. Lua: <c>g_things.countCreatures()</c></summary>
    [LuaMethod] public int  countCreatures()    => _things.GetAll(Game.ThingCategory.Creature).Count();

    /// <summary>Number of registered Effect types. Lua: <c>g_things.countEffects()</c></summary>
    [LuaMethod] public int  countEffects()      => _things.GetAll(Game.ThingCategory.Effect).Count();

    /// <summary>Number of registered Missile types. Lua: <c>g_things.countMissiles()</c></summary>
    [LuaMethod] public int  countMissiles()     => _things.GetAll(Game.ThingCategory.Missile).Count();

    [LuaMethod] public bool isLoaded()          => true;
    [LuaMethod] public void loadFromFile(string path) { /* stub */ }
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

// ─── g_minimap ────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the minimap, exposed as <c>g_minimap</c>.
/// Wraps <see cref="Game.Minimap"/> and mirrors the C++ bindings registered in
/// <c>luafunctions.cpp</c>: clean, loadOtmm, saveOtmm, isKnown, getTileColor,
/// getTileFlags, getTileSpeed, getTilePoint, getTilePosition, getTileRect, update.
/// Task T36.
/// </summary>
[LuaBinding("g_minimap")]
[MoonSharpUserData]
public sealed class LuaMinimapProxy
{
    private readonly Game.Minimap _minimap;

    public LuaMinimapProxy(Game.Minimap minimap)
    {
        ArgumentNullException.ThrowIfNull(minimap);
        _minimap = minimap;
    }

    /// <summary>Clears all recorded minimap tiles. Mirrors <c>Minimap::clean</c>.</summary>
    [LuaMethod] public void clean() => _minimap.Clear();

    /// <summary>
    /// Returns whether the minimap tile at (x, y, z) has been visited.
    /// Mirrors <c>Minimap::isKnown</c>.
    /// </summary>
    [LuaMethod]
    public bool isKnown(int x, int y, int z)
        => _minimap.IsKnown(new Game.Position((ushort)x, (ushort)y, (byte)z));

    /// <summary>
    /// Returns the 8-bit minimap color (0–255) for tile (x, y, z).
    /// Returns 255 (transparent/unknown) for unseen tiles.
    /// </summary>
    [LuaMethod]
    public int getTileColor(int x, int y, int z)
    {
        var td = _minimap.GetTile(new Game.Position((ushort)x, (ushort)y, (byte)z));
        return td.Color;
    }

    /// <summary>
    /// Returns the raw <see cref="Game.MinimapTileFlags"/> byte for tile (x, y, z).
    /// </summary>
    [LuaMethod]
    public int getTileFlags(int x, int y, int z)
    {
        var td = _minimap.GetTile(new Game.Position((ushort)x, (ushort)y, (byte)z));
        return (int)td.Flags;
    }

    /// <summary>
    /// Returns the movement speed byte (1–254) stored for tile (x, y, z).
    /// Returns 10 (default) for unseen tiles.
    /// </summary>
    [LuaMethod]
    public int getTileSpeed(int x, int y, int z)
    {
        var td = _minimap.GetTile(new Game.Position((ushort)x, (ushort)y, (byte)z));
        return td.Speed;
    }

    /// <summary>
    /// Converts world position (x, y, z) to a pixel point in
    /// screenRect (sx, sy, sw, sh) given mapCenter (cx, cy, cz) and scale.
    /// Returns a table {x=…, y=…} or nil when z ≠ cz.
    /// Mirrors <c>Minimap::getTilePoint</c>.
    /// </summary>
    [LuaMethod]
    public object? getTilePoint(
        int x, int y, int z,
        int sx, int sy, int sw, int sh,
        int cx, int cy, int cz,
        float scale)
    {
        var pos        = new Game.Position((ushort)x,  (ushort)y,  (byte)z);
        var screenRect = new System.Drawing.Rectangle(sx, sy, sw, sh);
        var center     = new Game.Position((ushort)cx, (ushort)cy, (byte)cz);
        var pt         = Game.Minimap.GetTilePoint(pos, screenRect, center, scale);
        if (pt.X < 0 && pt.Y < 0) return null;
        var t = new Table(null);
        t["x"] = (double)pt.X;
        t["y"] = (double)pt.Y;
        return t;
    }

    /// <summary>
    /// Converts pixel point (px, py) in screenRect to a world Position.
    /// Returns a table {x=…, y=…, z=…}.
    /// Mirrors <c>Minimap::getTilePosition</c>.
    /// </summary>
    [LuaMethod]
    public Table getTilePosition(
        float px, float py,
        int sx, int sy, int sw, int sh,
        int cx, int cy, int cz,
        float scale)
    {
        var point      = new Vector2(px, py);
        var screenRect = new System.Drawing.Rectangle(sx, sy, sw, sh);
        var center     = new Game.Position((ushort)cx, (ushort)cy, (byte)cz);
        var pos        = Game.Minimap.GetTilePosition(point, screenRect, center, scale);
        var t = new Table(null);
        t["x"] = (double)pos.X;
        t["y"] = (double)pos.Y;
        t["z"] = (double)pos.Z;
        return t;
    }

    /// <summary>
    /// Returns the screen rectangle {x, y, width, height} that tile (x,y,z) occupies.
    /// Returns nil when the tile is on a different floor.
    /// Mirrors <c>Minimap::getTileRect</c>.
    /// </summary>
    [LuaMethod]
    public object? getTileRect(
        int x, int y, int z,
        int sx, int sy, int sw, int sh,
        int cx, int cy, int cz,
        float scale)
    {
        var pos        = new Game.Position((ushort)x,  (ushort)y,  (byte)z);
        var screenRect = new System.Drawing.Rectangle(sx, sy, sw, sh);
        var center     = new Game.Position((ushort)cx, (ushort)cy, (byte)cz);
        var rect       = Game.Minimap.GetTileRect(pos, screenRect, center, scale);
        if (rect.IsEmpty) return null;
        var t = new Table(null);
        t["x"]      = (double)rect.X;
        t["y"]      = (double)rect.Y;
        t["width"]  = (double)rect.Width;
        t["height"] = (double)rect.Height;
        return t;
    }

    /// <summary>
    /// Loads an OTMM binary minimap file from <paramref name="path"/>.
    /// Returns <c>true</c> on success.
    /// Mirrors <c>Minimap::loadOtmm</c>.
    /// </summary>
    [LuaMethod]
    public bool loadOtmm(string path)
    {
        if (!File.Exists(path)) return false;
        using var fs = File.OpenRead(path);
        return _minimap.LoadOtmm(fs);
    }

    /// <summary>
    /// Saves all visited minimap blocks to an OTMM binary file at <paramref name="path"/>.
    /// Mirrors <c>Minimap::saveOtmm</c>.
    /// </summary>
    [LuaMethod]
    public void saveOtmm(string path)
    {
        using var fs = File.Open(path, FileMode.Create, FileAccess.Write);
        _minimap.SaveOtmm(fs);
    }
}
