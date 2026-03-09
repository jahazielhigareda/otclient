namespace OTClient.Framework.Game;

// ─── GameState ────────────────────────────────────────────────────────────────

/// <summary>High-level game states matching the OTClient state machine.</summary>
public enum GameState
{
    Disconnected,
    Connecting,
    LoginServer,
    PendingGame,
    InGame,
}

// ─── CharacterInfo ────────────────────────────────────────────────────────────

/// <summary>
/// Entry in the character list received from the login server.
/// </summary>
public sealed record CharacterInfo(
    string Name,
    string World,
    string WorldIp,
    int    WorldPort)
{
    public bool IsPremium { get; init; }
    public bool IsHidden  { get; init; }
    public int  Level     { get; init; }
}

// ─── Game ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Central game singleton — owns the world state, the local player,
/// the map, and drives the main game state machine.
/// Maps to <c>src/client/game.h</c>.
/// Task 8.21.
/// </summary>
public sealed class Game
{
    // ─── State machine ────────────────────────────────────────────────────────

    public GameState State { get; private set; } = GameState.Disconnected;

    public bool IsOnline       => State == GameState.InGame;
    public bool IsConnecting   => State == GameState.Connecting;
    public bool IsDisconnected => State == GameState.Disconnected;

    public event Action<GameState, GameState>? OnStateChanged;

    private void SetState(GameState next)
    {
        var prev = State;
        State = next;
        if (prev != next) OnStateChanged?.Invoke(prev, next);
    }

    // ─── Game world ───────────────────────────────────────────────────────────

    public Map                Map         { get; } = new();
    public MapView            MapView     { get; } = new();
    public LightView          LightView   { get; } = new();
    public Minimap            Minimap     { get; } = new();
    public LocalPlayer        LocalPlayer { get; } = new();
    public GameConfig         Config      { get; } = new();
    public ThingTypeManager   Things      { get; } = new();
    public CreatureDataManager Creatures  { get; } = new();

    // ─── Server configuration (T44) ──────────────────────────────────────────

    /// <summary>
    /// Server heartbeat interval in ms (default 50, set from <c>ParseLoginSuccess</c>).
    /// Maps to <c>g_game.m_serverBeat</c>.
    /// Task T44.
    /// </summary>
    public int  ServerBeat    { get; private set; } = 50;

    /// <summary>
    /// Whether the player can report bugs via the in-game bug-report button.
    /// Maps to <c>g_game.m_canReportBugs</c>.
    /// Task T44.
    /// </summary>
    public bool CanReportBugs { get; private set; }

    /// <summary>
    /// Whether expert PvP mode is active.
    /// Maps to <c>g_game.m_expertPvpMode</c>.
    /// Task T44.
    /// </summary>
    public bool ExpertPvpMode { get; private set; }

    /// <summary>
    /// Current GM action-permission bytes (20 bytes, all zero until received).
    /// Maps to <c>g_game.m_gmActions</c>.
    /// Task T44.
    /// </summary>
    public IReadOnlyList<byte> GmActions { get; private set; } = Array.Empty<byte>();

    // ─── PvP state (T46) ─────────────────────────────────────────────────────

    /// <summary>
    /// Number of currently open PvP situations, as last received from the server.
    /// Maps to <c>Game::setOpenPvpSituations</c>.
    /// Task T46.
    /// </summary>
    public byte OpenPvpSituations { get; private set; }

    // ─── Unjustified kills / skull (T48) ─────────────────────────────────────

    /// <summary>
    /// Current unjustified kill statistics as last received from the server.
    /// Null until the first <c>UnjustifiedStats</c> packet arrives.
    /// Maps to <c>Game::setUnjustifiedPoints</c>.
    /// Task T48.
    /// </summary>
    public UnjustifiedStats? UnjustifiedStats { get; private set; }

    // ─── Login-flow events (T44) ──────────────────────────────────────────────

    /// <summary>Raised when <c>ParseLoginSuccess</c> has been processed.</summary>
    public event Action? OnLogin;

    /// <summary>Raised when the server transitions the session to "pending game" state.</summary>
    public event Action? OnPendingGame;

    /// <summary>Raised when the server transitions the session to "entered game" state.</summary>
    public event Action? OnEnterGame;

    /// <summary>Raised when the server ends the session. Parameter: reason byte.</summary>
    public event Action<byte>? OnSessionEnd;

    /// <summary>Raised when GM action permissions are updated. Parameter: 20-byte array.</summary>
    public event Action<IReadOnlyList<byte>>? OnGMActionsChanged;

    /// <summary>Raised when the server requests a client update. Parameter: signature string.</summary>
    public event Action<string>? OnUpdateNeeded;



    private readonly List<CharacterInfo> _characters = [];
    public IReadOnlyList<CharacterInfo>  Characters  => _characters;

    public void SetCharacterList(IEnumerable<CharacterInfo> chars)
    {
        _characters.Clear();
        _characters.AddRange(chars);
    }

    // ─── Session control ──────────────────────────────────────────────────────

    /// <summary>Called when the client begins connecting to the login/game server.</summary>
    public void StartConnect()
    {
        SetState(GameState.Connecting);
    }

    /// <summary>Called when the login server has responded and the player can pick a character.</summary>
    public void SetLoginServer()
    {
        SetState(GameState.LoginServer);
    }

    /// <summary>Called when the character is selected and the game server accepts us.</summary>
    public void EnterGame(LocalPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        LocalPlayer.Id   = player.Id;
        LocalPlayer.Name = player.Name;
        LocalPlayer.IsKnown = true;
        SetState(GameState.InGame);
    }

    /// <summary>Called when the server sends a logout / disconnect packet.</summary>
    public void Logout()
    {
        LocalPlayer.IsKnown = false;
        Map.Clear();
        LightView.Clear();
        SetState(GameState.Disconnected);
    }

    // ─── Per-frame update ─────────────────────────────────────────────────────

    /// <summary>
    /// Advances game logic by <paramref name="deltaMs"/> milliseconds.
    /// Updates creature walk animations, effect lifetimes, etc.
    /// </summary>
    public void Update(float deltaMs)
    {
        float deltaSeconds = deltaMs / 1000f;
        MapView.Update(deltaSeconds);

        if (!IsOnline) return;

        foreach (var (_, creature) in Map.KnownCreatures)
            creature.Update(deltaMs);
    }

    // ─── Movement ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the local player requests a single-step walk.
    /// The handler (typically <see cref="Net.ProtocolGame"/>) should send the
    /// appropriate network packet.
    /// </summary>
    public event Action<Direction>? WalkRequested;

    /// <summary>
    /// Raised when the local player requests a turn (cardinal directions only).
    /// </summary>
    public event Action<Direction>? TurnRequested;

    /// <summary>Raised when the local player requests to stop movement.</summary>
    public event Action? StopRequested;

    /// <summary>
    /// Raised when the local player requests an auto-walk along a path.
    /// </summary>
    public event Action<IReadOnlyList<Direction>>? AutoWalkRequested;

    /// <summary>
    /// Requests a single-step walk in <paramref name="dir"/>.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="WalkRequested"/>.
    /// Maps to <c>Game::walk()</c> in <c>src/client/game.cpp</c>.
    /// </summary>
    public void Walk(Direction dir)
    {
        if (!IsOnline) return;
        WalkRequested?.Invoke(dir);
    }

    /// <summary>
    /// Requests a turn in <paramref name="dir"/> (cardinal directions only).
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="TurnRequested"/>.
    /// </summary>
    public void Turn(Direction dir)
    {
        if (!IsOnline) return;
        TurnRequested?.Invoke(dir);
    }

    /// <summary>
    /// Requests that the character stop moving.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="StopRequested"/>.
    /// </summary>
    public void Stop()
    {
        if (!IsOnline) return;
        StopRequested?.Invoke();
    }

    /// <summary>
    /// Requests an auto-walk along the provided sequence of directions.
    /// Only valid while <see cref="IsOnline"/> and the path is non-empty.
    /// Fires <see cref="AutoWalkRequested"/>.
    /// Maps to <c>Game::autoWalk()</c> in <c>src/client/game.cpp</c>.
    /// </summary>
    public void AutoWalk(IReadOnlyList<Direction> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!IsOnline || path.Count == 0) return;
        AutoWalkRequested?.Invoke(path);
    }

    // ─── Chat (T10) ──────────────────────────────────────────────────────────

    /// <summary>Raised when the player wants to say something in the default chat mode.</summary>
    public event Action<string>? TalkSayRequested;

    /// <summary>Raised when the player wants to send a message to a channel.</summary>
    public event Action<ushort, string>? TalkChannelRequested;

    /// <summary>Raised when the player wants to send a private message.</summary>
    public event Action<string, string>? TalkPrivateRequested;

    /// <summary>Raised when the player requests the list of available channels.</summary>
    public event Action? ChannelsRequested;

    /// <summary>Raised when the player joins a channel.</summary>
    public event Action<ushort>? JoinChannelRequested;

    /// <summary>Raised when the player leaves a channel.</summary>
    public event Action<ushort>? LeaveChannelRequested;

    /// <summary>Raised when the player opens a private channel with another player.</summary>
    public event Action<string>? OpenPrivateChannelRequested;

    /// <summary>
    /// Sends a public say message in the default chat mode.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="TalkSayRequested"/>.
    /// Maps to <c>Game::talk()</c> (mode = MessageSay) in <c>src/client/game.cpp</c>.
    /// Task T10.
    /// </summary>
    public void TalkSay(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!IsOnline) return;
        TalkSayRequested?.Invoke(message);
    }

    /// <summary>
    /// Sends a message to a channel.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="TalkChannelRequested"/>.
    /// Maps to <c>Game::talkChannel()</c> in <c>src/client/game.cpp</c>.
    /// Task T10.
    /// </summary>
    public void TalkChannel(ushort channelId, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!IsOnline) return;
        TalkChannelRequested?.Invoke(channelId, message);
    }

    /// <summary>
    /// Sends a private message to <paramref name="receiver"/>.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="TalkPrivateRequested"/>.
    /// Maps to <c>Game::talkPrivate()</c> in <c>src/client/game.cpp</c>.
    /// Task T10.
    /// </summary>
    public void TalkPrivate(string receiver, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiver);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!IsOnline) return;
        TalkPrivateRequested?.Invoke(receiver, message);
    }

    /// <summary>
    /// Requests the list of available public channels.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="ChannelsRequested"/>.
    /// Maps to <c>Game::requestChannels()</c> in <c>src/client/game.cpp</c>.
    /// Task T10.
    /// </summary>
    public void RequestChannels()
    {
        if (!IsOnline) return;
        ChannelsRequested?.Invoke();
    }

    /// <summary>
    /// Joins a public channel identified by <paramref name="channelId"/>.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="JoinChannelRequested"/>.
    /// Maps to <c>Game::joinChannel()</c> in <c>src/client/game.cpp</c>.
    /// Task T10.
    /// </summary>
    public void JoinChannel(ushort channelId)
    {
        if (!IsOnline) return;
        JoinChannelRequested?.Invoke(channelId);
    }

    /// <summary>
    /// Leaves a public channel identified by <paramref name="channelId"/>.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="LeaveChannelRequested"/>.
    /// Maps to <c>Game::leaveChannel()</c> in <c>src/client/game.cpp</c>.
    /// Task T10.
    /// </summary>
    public void LeaveChannel(ushort channelId)
    {
        if (!IsOnline) return;
        LeaveChannelRequested?.Invoke(channelId);
    }

    /// <summary>
    /// Opens a private channel with the player named <paramref name="playerName"/>.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="OpenPrivateChannelRequested"/>.
    /// Maps to <c>Game::openPrivateChannel()</c> in <c>src/client/game.cpp</c>.
    /// Task T10.
    /// </summary>
    public void OpenPrivateChannel(string playerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
        if (!IsOnline) return;
        OpenPrivateChannelRequested?.Invoke(playerName);
    }

    // ─── Combat (T12) ────────────────────────────────────────────────────────

    /// <summary>Raised when the player attacks a creature.</summary>
    public event Action<uint>? AttackRequested;

    /// <summary>Raised when the player follows a creature.</summary>
    public event Action<uint>? FollowRequested;

    /// <summary>Raised when the player cancels attack.</summary>
    public event Action? CancelAttackRequested;

    /// <summary>Raised when the player cancels follow.</summary>
    public event Action? CancelFollowRequested;

    /// <summary>Raised when both attack and follow are cancelled at once.</summary>
    public event Action? CancelAttackAndFollowRequested;

    /// <summary>Raised when fight modes change.</summary>
    public event Action<FightMode, ChaseMode, bool, PvpMode>? FightModesChanged;

    /// <summary>Id of the creature currently being attacked (0 = none). Task T38.</summary>
    public uint AttackingCreatureId { get; set; }

    /// <summary>Id of the creature currently being followed (0 = none). Task T38.</summary>
    public uint FollowingCreatureId { get; set; }

    /// <summary>
    /// Attacks the creature with id <paramref name="creatureId"/>.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="AttackRequested"/>.
    /// Maps to <c>Game::attack()</c>.
    /// Task T12.
    /// </summary>
    public void Attack(uint creatureId)
    {
        if (!IsOnline) return;
        AttackingCreatureId = creatureId;
        AttackRequested?.Invoke(creatureId);
    }

    /// <summary>
    /// Follows the creature with id <paramref name="creatureId"/>.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="FollowRequested"/>.
    /// Maps to <c>Game::follow()</c>.
    /// Task T12.
    /// </summary>
    public void Follow(uint creatureId)
    {
        if (!IsOnline) return;
        FollowingCreatureId = creatureId;
        FollowRequested?.Invoke(creatureId);
    }

    /// <summary>
    /// Cancels the current attack.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="CancelAttackRequested"/>.
    /// Maps to <c>Game::cancelAttack()</c>.
    /// Task T12.
    /// </summary>
    public void CancelAttack()
    {
        if (!IsOnline) return;
        AttackingCreatureId = 0;
        CancelAttackRequested?.Invoke();
    }

    /// <summary>
    /// Cancels the current follow target.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="CancelFollowRequested"/>.
    /// Maps to <c>Game::cancelFollow()</c>.
    /// Task T12.
    /// </summary>
    public void CancelFollow()
    {
        if (!IsOnline) return;
        FollowingCreatureId = 0;
        CancelFollowRequested?.Invoke();
    }

    /// <summary>
    /// Cancels both attack and follow in a single operation.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="CancelAttackAndFollowRequested"/>.
    /// Maps to <c>Game::cancelAttackAndFollow()</c>.
    /// Task T12.
    /// </summary>
    public void CancelAttackAndFollow()
    {
        if (!IsOnline) return;
        AttackingCreatureId  = 0;
        FollowingCreatureId  = 0;
        CancelAttackAndFollowRequested?.Invoke();
    }

    /// <summary>
    /// Updates the player's fight/chase/safe/PvP mode settings.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="FightModesChanged"/>.
    /// Maps to <c>Game::setFightMode()</c> / <c>Game::setChaseMode()</c> etc.
    /// Task T12.
    /// </summary>
    public void SetFightModes(FightMode fightMode, ChaseMode chaseMode,
        bool safeFight, PvpMode pvpMode = PvpMode.WhiteDove)
    {
        if (!IsOnline) return;
        FightModesChanged?.Invoke(fightMode, chaseMode, safeFight, pvpMode);
    }

    // ─── NPC trade (T15) ──────────────────────────────────────────────────────

    /// <summary>Raised to request the server sends InspectNpcTrade for an item.</summary>
    public event Action<int, int>? InspectNpcTradeRequested;   // itemId, count

    /// <summary>Raised to request a buy from an NPC.</summary>
    public event Action<int, int, int, bool, bool>? BuyItemRequested;  // itemId, subType, amount, ignoreCapacity, buyWithBackpack

    /// <summary>Raised to request a sell to an NPC.</summary>
    public event Action<int, int, int, bool>? SellItemRequested;   // itemId, subType, amount, ignoreEquipped

    /// <summary>Raised to request closing the NPC trade window.</summary>
    public event Action? CloseNpcTradeRequested;

    /// <summary>
    /// Requests the server to inspect a specific NPC trade item.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="InspectNpcTradeRequested"/>.
    /// Maps to <c>Game::inspectNpcTrade()</c>.
    /// Task T15.
    /// </summary>
    public void InspectNpcTrade(int itemId, int count = 1)
    {
        if (!IsOnline) return;
        InspectNpcTradeRequested?.Invoke(itemId, count);
    }

    /// <summary>
    /// Requests the server to buy an item from an NPC.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="BuyItemRequested"/>.
    /// Maps to <c>Game::buyItem()</c>.
    /// Task T15.
    /// </summary>
    public void BuyItem(int itemId, int subType, int amount, bool ignoreCapacity = false, bool buyWithBackpack = false)
    {
        if (!IsOnline) return;
        BuyItemRequested?.Invoke(itemId, subType, amount, ignoreCapacity, buyWithBackpack);
    }

    /// <summary>
    /// Requests the server to sell an item to an NPC.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="SellItemRequested"/>.
    /// Maps to <c>Game::sellItem()</c>.
    /// Task T15.
    /// </summary>
    public void SellItem(int itemId, int subType, int amount, bool ignoreEquipped = false)
    {
        if (!IsOnline) return;
        SellItemRequested?.Invoke(itemId, subType, amount, ignoreEquipped);
    }

    /// <summary>
    /// Requests the server to close the active NPC trade window.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="CloseNpcTradeRequested"/>.
    /// Maps to <c>Game::closeNpcTrade()</c>.
    /// Task T15.
    /// </summary>
    public void CloseNpcTrade()
    {
        if (!IsOnline) return;
        CloseNpcTradeRequested?.Invoke();
    }

    // ─── Player-to-player trade (T16) ─────────────────────────────────────────

    /// <summary>Raised to request the server opens a player trade with an item.</summary>
    public event Action<Position, int, int, uint>? RequestTradeRequested;   // position, itemId, stackPos, creatureId

    /// <summary>Raised to request the server to inspect a trade slot.</summary>
    public event Action<bool, int>? InspectTradeRequested;   // counterOffer, index

    /// <summary>Raised to request the server to accept the trade.</summary>
    public event Action? AcceptTradeRequested;

    /// <summary>Raised to request the server to reject the trade.</summary>
    public event Action? RejectTradeRequested;

    /// <summary>
    /// Requests the server to initiate a player-to-player trade.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="RequestTradeRequested"/>.
    /// Maps to <c>Game::requestTrade()</c>.
    /// Task T16.
    /// </summary>
    public void RequestTrade(Position position, int itemId, int stackPos, uint creatureId)
    {
        if (!IsOnline) return;
        RequestTradeRequested?.Invoke(position, itemId, stackPos, creatureId);
    }

    /// <summary>
    /// Requests the server to inspect a slot in the current trade.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="InspectTradeRequested"/>.
    /// Maps to <c>Game::inspectTrade()</c>.
    /// Task T16.
    /// </summary>
    public void InspectTrade(bool counterOffer, int index)
    {
        if (!IsOnline) return;
        InspectTradeRequested?.Invoke(counterOffer, index);
    }

    /// <summary>
    /// Accepts the current player-to-player trade.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="AcceptTradeRequested"/>.
    /// Maps to <c>Game::acceptTrade()</c>.
    /// Task T16.
    /// </summary>
    public void AcceptTrade()
    {
        if (!IsOnline) return;
        AcceptTradeRequested?.Invoke();
    }

    /// <summary>
    /// Rejects the current player-to-player trade.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="RejectTradeRequested"/>.
    /// Maps to <c>Game::rejectTrade()</c>.
    /// Task T16.
    /// </summary>
    public void RejectTrade()
    {
        if (!IsOnline) return;
        RejectTradeRequested?.Invoke();
    }

    // ─── VIP management (T21 / T43) ──────────────────────────────────────────────

    private readonly Dictionary<uint, VipEntry> _vips = [];

    // ─── Login-flow process methods (T44) ─────────────────────────────────────

    /// <summary>
    /// Processes a <c>LoginSuccess</c> packet from the protocol layer.
    /// Stores server-beat, expert-PvP flag, and fires <see cref="OnLogin"/>.
    /// Maps to <c>Game::processLogin</c>.
    /// Task T44.
    /// </summary>
    internal void ProcessLogin(uint playerId, ushort serverBeat,
        double speedA, double speedB, double speedC,
        bool expertPvpMode, string storeUrl, ushort coinsPacketSize)
    {
        LocalPlayer.Id = playerId;
        LocalPlayer.IsKnown = true;
        ServerBeat    = serverBeat;
        ExpertPvpMode = expertPvpMode;
        OnLogin?.Invoke();
    }

    /// <summary>
    /// Processes a <c>PendingGame</c> packet from the protocol layer.
    /// Maps to <c>Game::processPendingGame</c>.
    /// Task T44.
    /// </summary>
    internal void ProcessPendingGame()
    {
        SetState(GameState.PendingGame);
        OnPendingGame?.Invoke();
    }

    /// <summary>
    /// Processes an <c>EnterGame</c> packet from the protocol layer.
    /// Maps to <c>Game::processEnterGame + processGameStart</c>.
    /// Task T44.
    /// </summary>
    internal void ProcessEnterGame()
    {
        SetState(GameState.InGame);
        OnEnterGame?.Invoke();
    }

    /// <summary>
    /// Processes a <c>SessionEnd</c> packet from the protocol layer.
    /// Maps to <c>Game::processSessionEnd</c>.
    /// Task T44.
    /// </summary>
    internal void ProcessSessionEnd(byte reason)
    {
        OnSessionEnd?.Invoke(reason);
    }

    /// <summary>
    /// Processes GM action-permission bytes received from the server.
    /// Maps to <c>Game::processGMActions</c>.
    /// Task T44.
    /// </summary>
    internal void ProcessGMActions(byte[] actions)
    {
        GmActions = actions;
        OnGMActionsChanged?.Invoke(GmActions);
    }

    /// <summary>
    /// Processes an <c>UpdateNeeded</c> notification from the protocol layer.
    /// Maps to <c>Game::processUpdateNeeded</c>.
    /// Task T44.
    /// </summary>
    internal void ProcessUpdateNeeded(string signature)
    {
        OnUpdateNeeded?.Invoke(signature);
    }

    /// <summary>Raised to request adding a player to the VIP list.</summary>
    public event Action<string>? AddVipRequested;   // name

    /// <summary>Raised to request removing a player from the VIP list.</summary>
    public event Action<uint>? RemoveVipRequested;  // id

    /// <summary>
    /// Raised when the server adds or refreshes a VIP entry.
    /// Parameter is the updated <see cref="VipEntry"/>.
    /// Maps to <c>Game::processVipAdd</c> / <c>g_lua.callGlobalField("g_game","onAddVip")</c>.
    /// Task T43.
    /// </summary>
    public event Action<VipEntry>? OnVipAdded;

    /// <summary>
    /// Raised when the online/offline status of a VIP entry changes.
    /// Parameters: (id, newStatus — 0=offline, 1=online).
    /// Maps to <c>Game::processVipStateChange</c> / <c>g_lua.callGlobalField("g_game","onVipStateChange")</c>.
    /// Task T43.
    /// </summary>
    public event Action<uint, uint>? OnVipStateChanged;

    /// <summary>
    /// Returns a snapshot of all known VIP entries.
    /// Maps to <c>Game::getVips()</c>.
    /// Task T43.
    /// </summary>
    public IReadOnlyList<VipEntry> GetVips()
        => [.. _vips.Values];

    /// <summary>
    /// Returns the VIP entry with the given creature ID, or <c>null</c> if unknown.
    /// Task T43.
    /// </summary>
    public VipEntry? GetVip(uint id)
        => _vips.TryGetValue(id, out var e) ? e : null;

    /// <summary>
    /// Processes a VIP entry received from the server.
    /// Stores the entry in <see cref="_vips"/> and raises <see cref="OnVipAdded"/>.
    /// Maps to <c>Game::processVipAdd</c>.
    /// Task T43.
    /// </summary>
    internal void ProcessVipAdd(uint id, string name, uint status,
        string description, uint iconId, bool notifyLogin)
    {
        var entry = _vips.TryGetValue(id, out var existing) ? existing : new VipEntry { Id = id };
        entry.Name        = name;
        entry.Status      = status;
        entry.Description = description;
        entry.IconId      = iconId;
        entry.NotifyLogin = notifyLogin;
        _vips[id] = entry;
        OnVipAdded?.Invoke(entry);
    }

    /// <summary>
    /// Updates the status of an existing VIP entry.
    /// Maps to <c>Game::processVipStateChange</c>.
    /// Task T43.
    /// </summary>
    internal void ProcessVipStateChange(uint id, uint status)
    {
        if (!_vips.TryGetValue(id, out var entry)) return;
        entry.Status = status;
        OnVipStateChanged?.Invoke(id, status);
    }

    /// <summary>
    /// Adds a player to the VIP (friends) list.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="AddVipRequested"/>.
    /// Maps to <c>Game::addVip()</c>.
    /// Task T21.
    /// </summary>
    public void AddVip(string name)
    {
        if (!IsOnline) return;
        AddVipRequested?.Invoke(name);
    }

    /// <summary>
    /// Removes a player from the VIP (friends) list by their creature ID.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="RemoveVipRequested"/>.
    /// Maps to <c>Game::removeVip()</c>.
    /// Task T21.
    /// </summary>
    public void RemoveVip(uint id)
    {
        if (!IsOnline) return;
        RemoveVipRequested?.Invoke(id);
    }

    // ─── Quest log (T23) ──────────────────────────────────────────────────────

    /// <summary>Raised when the server sends a quest log list.</summary>
    public event Action<IReadOnlyList<QuestEntry>>? QuestLogReceived;

    /// <summary>Raised when the server sends quest mission details.</summary>
    public event Action<ushort, IReadOnlyList<QuestMission>>? QuestLineReceived;

    /// <summary>Raised to request the quest log from the server.</summary>
    public event Action? RequestQuestLogRequested;

    /// <summary>Raised to request a specific quest line from the server.</summary>
    public event Action<ushort>? RequestQuestLineRequested;

    /// <summary>
    /// Fires <see cref="QuestLogReceived"/> when a <c>parseQuestLog</c> packet
    /// is decoded. Maps to <c>Game::processQuestLog()</c>. Task T23.
    /// </summary>
    public void ProcessQuestLog(IReadOnlyList<QuestEntry> entries)
        => QuestLogReceived?.Invoke(entries);

    /// <summary>
    /// Fires <see cref="QuestLineReceived"/> when a <c>parseQuestLine</c> packet
    /// is decoded. Maps to <c>Game::processQuestLine()</c>. Task T23.
    /// </summary>
    public void ProcessQuestLine(ushort questId, IReadOnlyList<QuestMission> missions)
        => QuestLineReceived?.Invoke(questId, missions);

    /// <summary>
    /// Requests the quest log from the server.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="RequestQuestLogRequested"/>. Task T23.
    /// </summary>
    public void RequestQuestLog()
    {
        if (!IsOnline) return;
        RequestQuestLogRequested?.Invoke();
    }

    /// <summary>
    /// Requests the mission details for a specific quest.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="RequestQuestLineRequested"/>. Task T23.
    /// </summary>
    public void RequestQuestLine(ushort questId)
    {
        if (!IsOnline) return;
        RequestQuestLineRequested?.Invoke(questId);
    }

    // ─── Modal dialog (T23) ───────────────────────────────────────────────────

    /// <summary>Raised when the server opens a modal dialog.</summary>
    public event Action<ModalDialog>? ModalDialogReceived;

    /// <summary>Raised to answer a modal dialog.</summary>
    public event Action<uint, byte, byte>? AnswerModalDialogRequested;   // windowId, buttonId, choiceId

    /// <summary>
    /// Fires <see cref="ModalDialogReceived"/> when a <c>parseModalDialog</c>
    /// packet is decoded. Maps to <c>Game::processModalDialog()</c>. Task T23.
    /// </summary>
    public void ProcessModalDialog(ModalDialog dialog)
        => ModalDialogReceived?.Invoke(dialog);

    /// <summary>
    /// Sends the player's answer to a modal dialog.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="AnswerModalDialogRequested"/>. Task T23.
    /// </summary>
    public void AnswerModalDialog(uint windowId, byte buttonId, byte choiceId)
    {
        if (!IsOnline) return;
        AnswerModalDialogRequested?.Invoke(windowId, buttonId, choiceId);
    }

    // ─── Edit text / edit list (T23) ──────────────────────────────────────────

    /// <summary>Raised when the server opens an editable text window.</summary>
    public event Action<uint, int, ushort, string, string, string>? EditTextReceived;
    // (id, itemId, maxLength, text, writer, date)

    /// <summary>Raised when the server opens an editable list window.</summary>
    public event Action<uint, byte, string>? EditListReceived;
    // (id, doorId, text)

    /// <summary>Raised to submit changes to an editable text window.</summary>
    public event Action<uint, string>? EditTextSentRequested;   // id, text

    /// <summary>Raised to submit changes to an editable list window.</summary>
    public event Action<uint, byte, string>? EditListSentRequested;   // id, doorId, text

    /// <summary>
    /// Fires <see cref="EditTextReceived"/> when a <c>parseEditText</c> packet
    /// is decoded. Maps to <c>Game::processEditText()</c>. Task T23.
    /// </summary>
    public void ProcessEditText(uint id, int itemId, ushort maxLength,
        string text, string writer, string date)
        => EditTextReceived?.Invoke(id, itemId, maxLength, text, writer, date);

    /// <summary>
    /// Fires <see cref="EditListReceived"/> when a <c>parseEditList</c> packet
    /// is decoded. Maps to <c>Game::processEditList()</c>. Task T23.
    /// </summary>
    public void ProcessEditList(uint id, byte doorId, string text)
        => EditListReceived?.Invoke(id, doorId, text);

    /// <summary>
    /// Sends the player's edit of a text window to the server.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="EditTextSentRequested"/>. Task T23.
    /// </summary>
    public void EditTextSend(uint id, string text)
    {
        if (!IsOnline) return;
        EditTextSentRequested?.Invoke(id, text);
    }

    /// <summary>
    /// Sends the player's edit of a list window to the server.
    /// Only valid while <see cref="IsOnline"/>.
    /// Fires <see cref="EditListSentRequested"/>. Task T23.
    /// </summary>
    public void EditListSend(uint id, byte doorId, string text)
    {
        if (!IsOnline) return;
        EditListSentRequested?.Invoke(id, doorId, text);
    }

    // ─── Container / Inventory state (T39) ────────────────────────────────────

    private const int MaxContainers    = 64;
    private const int InventorySlots   = 16;

    private readonly Dictionary<int, Container> _containers = new();
    private readonly Item?[]                    _inventory  = new Item[InventorySlots];

    /// <summary>Returns the open container at wire slot <paramref name="id"/>, or <c>null</c>.</summary>
    public Container? GetContainer(int id)
        => _containers.TryGetValue(id, out var c) ? c : null;

    /// <summary>Returns all currently open containers.</summary>
    public IReadOnlyDictionary<int, Container> GetContainers() => _containers;

    /// <summary>Returns the item in inventory slot <paramref name="slot"/>, or <c>null</c>.</summary>
    public Item? GetInventoryItem(InventorySlot slot)
    {
        int idx = (int)slot;
        return (uint)idx < InventorySlots ? _inventory[idx] : null;
    }

    /// <summary>Returns the full inventory array (slots 0–15; slot 0 unused).</summary>
    public IReadOnlyList<Item?> GetInventory() => _inventory;

    /// <summary>
    /// Called by the protocol layer when the server opens a container.
    /// Closes any existing container at the same slot.
    /// </summary>
    public void OpenContainer(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (_containers.TryGetValue(container.Id, out var prev))
            prev.Close();
        _containers[container.Id] = container;
    }

    /// <summary>Called by the protocol layer when the server closes a container.</summary>
    public void CloseContainerSlot(int id)
    {
        if (_containers.TryGetValue(id, out var c))
        {
            c.Close();
            _containers.Remove(id);
        }
    }

    /// <summary>Sets an inventory slot item (called by the protocol layer).</summary>
    public void SetInventoryItem(InventorySlot slot, Item? item)
    {
        int idx = (int)slot;
        if ((uint)idx < InventorySlots)
            _inventory[idx] = item;
    }

    // ─── Item action events (T39) ─────────────────────────────────────────────

    /// <summary>Fired when Lua calls <c>g_game.use()</c>.</summary>
    public event Action<Position, int, int, int>? UseItemRequested;

    /// <summary>Fired when Lua calls <c>g_game.useWith()</c>.</summary>
    public event Action<Position, int, int, Position, int, int>? UseItemWithRequested;

    /// <summary>Fired when Lua calls <c>g_game.useOnCreature()</c>.</summary>
    public event Action<Position, int, int, uint>? UseOnCreatureRequested;

    /// <summary>Fired when Lua calls <c>g_game.move()</c>.</summary>
    public event Action<Position, int, int, Position, int>? MoveItemRequested;

    /// <summary>Fired when Lua calls <c>g_game.look()</c>.</summary>
    public event Action<Position, int, int>? LookAtRequested;

    /// <summary>Fired when Lua calls <c>g_game.lookCreature()</c>.</summary>
    public event Action<uint>? LookCreatureRequested;

    /// <summary>Fired when Lua calls <c>g_game.rotate()</c>.</summary>
    public event Action<Position, int, int>? RotateItemRequested;

    /// <summary>Fired when Lua calls <c>g_game.wrapItem()</c>.</summary>
    public event Action<Position, int, int>? WrapItemRequested;

    /// <summary>Fired when Lua calls <c>g_game.close()</c> with a container ID.</summary>
    public event Action<int>? CloseContainerRequested;

    /// <summary>Fired when Lua calls <c>g_game.openParent()</c> with a container ID.</summary>
    public event Action<int>? UpContainerRequested;

    /// <summary>Fired when Lua calls <c>g_game.browseField()</c>.</summary>
    public event Action<Position>? BrowseFieldRequested;

    /// <summary>Fired when Lua calls <c>g_game.seekInContainer()</c>.</summary>
    public event Action<int, int>? SeekInContainerRequested;

    // ─── Item action methods (T39) ────────────────────────────────────────────

    /// <summary>
    /// Uses item at <paramref name="pos"/> with type <paramref name="itemId"/>,
    /// stack position <paramref name="stackPos"/>, and container index <paramref name="index"/>.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="UseItemRequested"/>.
    /// Maps to <c>Game::use()</c>.
    /// </summary>
    public void UseItem(Position pos, int itemId, int stackPos, int index)
    {
        if (!IsOnline) return;
        UseItemRequested?.Invoke(pos, itemId, stackPos, index);
    }

    /// <summary>
    /// Uses item at <paramref name="fromPos"/> on item at <paramref name="toPos"/>.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="UseItemWithRequested"/>.
    /// Maps to <c>Game::useWith()</c>.
    /// </summary>
    public void UseItemWith(Position fromPos, int itemId, int fromStackPos,
                             Position toPos, int toItemId, int toStackPos)
    {
        if (!IsOnline) return;
        UseItemWithRequested?.Invoke(fromPos, itemId, fromStackPos, toPos, toItemId, toStackPos);
    }

    /// <summary>
    /// Uses item at <paramref name="pos"/> on creature <paramref name="creatureId"/>.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="UseOnCreatureRequested"/>.
    /// Maps to <c>Game::useOnCreature()</c>.
    /// </summary>
    public void UseOnCreature(Position pos, int itemId, int stackPos, uint creatureId)
    {
        if (!IsOnline) return;
        UseOnCreatureRequested?.Invoke(pos, itemId, stackPos, creatureId);
    }

    /// <summary>
    /// Moves an item from <paramref name="fromPos"/> to <paramref name="toPos"/>.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="MoveItemRequested"/>.
    /// Maps to <c>Game::move()</c>.
    /// </summary>
    public void MoveItem(Position fromPos, int itemId, int stackPos, Position toPos, int count)
    {
        if (!IsOnline) return;
        MoveItemRequested?.Invoke(fromPos, itemId, stackPos, toPos, count);
    }

    /// <summary>
    /// Looks at the item at <paramref name="pos"/>.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="LookAtRequested"/>.
    /// Maps to <c>Game::look()</c>.
    /// </summary>
    public void LookAt(Position pos, int itemId, int stackPos)
    {
        if (!IsOnline) return;
        LookAtRequested?.Invoke(pos, itemId, stackPos);
    }

    /// <summary>
    /// Looks at a creature.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="LookCreatureRequested"/>.
    /// Maps to <c>Game::lookCreature()</c>.
    /// </summary>
    public void LookCreature(uint creatureId)
    {
        if (!IsOnline) return;
        LookCreatureRequested?.Invoke(creatureId);
    }

    /// <summary>
    /// Rotates the item at <paramref name="pos"/>.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="RotateItemRequested"/>.
    /// Maps to <c>Game::rotate()</c>.
    /// </summary>
    public void RotateItem(Position pos, int itemId, int stackPos)
    {
        if (!IsOnline) return;
        RotateItemRequested?.Invoke(pos, itemId, stackPos);
    }

    /// <summary>
    /// Wraps (or unwraps) the item at <paramref name="pos"/>.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="WrapItemRequested"/>.
    /// Maps to <c>Game::wrapItem()</c>.
    /// </summary>
    public void WrapItem(Position pos, int itemId, int stackPos)
    {
        if (!IsOnline) return;
        WrapItemRequested?.Invoke(pos, itemId, stackPos);
    }

    /// <summary>
    /// Closes an open container.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="CloseContainerRequested"/>.
    /// Maps to <c>Game::close()</c>.
    /// </summary>
    public void CloseContainer(int containerId)
    {
        if (!IsOnline) return;
        CloseContainerRequested?.Invoke(containerId);
    }

    /// <summary>
    /// Navigates up to the parent container.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="UpContainerRequested"/>.
    /// Maps to <c>Game::openParent()</c>.
    /// </summary>
    public void UpContainer(int containerId)
    {
        if (!IsOnline) return;
        UpContainerRequested?.Invoke(containerId);
    }

    /// <summary>
    /// Browses the field at <paramref name="pos"/> (opens a stack-view).
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="BrowseFieldRequested"/>.
    /// Maps to <c>Game::browseField()</c>.
    /// </summary>
    public void BrowseField(Position pos)
    {
        if (!IsOnline) return;
        BrowseFieldRequested?.Invoke(pos);
    }

    /// <summary>
    /// Seeks to page <paramref name="index"/> within paginated container <paramref name="containerId"/>.
    /// Only valid while <see cref="IsOnline"/>. Fires <see cref="SeekInContainerRequested"/>.
    /// Maps to <c>Game::seekInContainer()</c>.
    /// </summary>
    public void SeekInContainer(int containerId, int index)
    {
        if (!IsOnline) return;
        SeekInContainerRequested?.Invoke(containerId, index);
    }

    // ─── Protocol wiring (T42) ────────────────────────────────────────────────

    /// <summary>
    /// Wires a <see cref="Net.ProtocolGame"/> instance to update this game's
    /// <see cref="LocalPlayer"/> automatically whenever the server sends stat,
    /// skill, state or mode packets.
    /// Call this once after creating the protocol object; the subscriptions are
    /// kept alive as long as both objects exist.
    /// Maps to the direct field assignments inside <c>ProtocolGame.ParsePlayerStats</c>
    /// etc. in the original C++ code (<c>src/client/protocolgameparse.cpp</c>).
    /// Task T42.  VIP wiring added in T43.
    /// </summary>
    public void ConnectProtocol(Net.ProtocolGame protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);

        protocol.PlayerStatsUpdated += (health, maxHealth, mana, maxMana,
            freeCapacity, experience, level, levelPercent,
            stamina, soul, regenerationTime, offlineTrainingTime) =>
        {
            LocalPlayer.Health              = health;
            LocalPlayer.MaxHealth           = maxHealth;
            LocalPlayer.Mana                = mana;
            LocalPlayer.MaxMana             = maxMana;
            LocalPlayer.FreeCapacity        = freeCapacity;
            LocalPlayer.Exp                 = experience;
            LocalPlayer.Level               = level;
            LocalPlayer.LevelPercent        = levelPercent;
            LocalPlayer.Stamina             = stamina;
            LocalPlayer.Soul                = soul;
            LocalPlayer.RegenerationTime    = regenerationTime;
            LocalPlayer.OfflineTrainingTime = offlineTrainingTime;
        };

        protocol.PlayerSkillsUpdated += (magicLevel, baseMagicLevel, magicLevelPercent,
            levels, baseLevels, percents) =>
        {
            LocalPlayer.MagicLevel        = magicLevel;
            LocalPlayer.BaseMagicLevel    = baseMagicLevel;
            LocalPlayer.MagicLevelPercent = magicLevelPercent;
            for (int i = 0; i < levels.Length && i < Enum.GetValues<SkillType>().Length; i++)
                LocalPlayer.SetSkill((SkillType)i, levels[i], percents[i], baseLevels[i]);
        };

        protocol.PlayerStateUpdated += states =>
        {
            LocalPlayer.Conditions = states;
        };

        protocol.PlayerModesUpdated += (fightMode, chaseMode, safeMode, pvpMode) =>
        {
            LocalPlayer.FightMode = fightMode;
            LocalPlayer.ChaseMode = chaseMode;
            LocalPlayer.SafeMode  = safeMode;
            LocalPlayer.PvpMode   = pvpMode;
        };

        // T43: wire VIP list events
        protocol.VipAdded += (id, name, status, description, iconId, notifyLogin) =>
            ProcessVipAdd(id, name, status, description, iconId, notifyLogin);

        protocol.VipStateChanged += (id, status) =>
            ProcessVipStateChange(id, status);

        // T44: wire login-flow events
        protocol.LoginSuccessReceived += (playerId, serverBeat, speedA, speedB, speedC,
            expertPvpMode, storeUrl, coinsPacketSize) =>
            ProcessLogin(playerId, serverBeat, speedA, speedB, speedC,
                         expertPvpMode, storeUrl, coinsPacketSize);

        protocol.PendingGameReceived += ProcessPendingGame;

        protocol.EnterGameReceived += ProcessEnterGame;

        protocol.SessionEndReceived += ProcessSessionEnd;

        protocol.GMActionsUpdated += ProcessGMActions;

        protocol.UpdateNeededReceived += ProcessUpdateNeeded;

        // T46: wire spell-cooldown, blessings, world-time, PvP, resource-balance, own-channel events
        protocol.BlessingsChanged += (blessings, _) =>
        {
            LocalPlayer.Blessings = blessings;
        };

        protocol.PvpSituationsChanged += count =>
        {
            OpenPvpSituations = count;
        };

        protocol.PlayerInfoReceived += (isPremium, vocation, spells) =>
        {
            LocalPlayer.IsPremium = isPremium;
            LocalPlayer.Vocation  = vocation;
            LocalPlayer.SetSpells(spells);
        };

        // T48: wire bug-report and unjustified-stats events
        protocol.BugReportReceived += canReport =>
        {
            CanReportBugs = canReport;
        };

        protocol.UnjustifiedStatsReceived += stats =>
        {
            UnjustifiedStats = stats;
        };
    }
}
