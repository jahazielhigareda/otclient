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

    // ─── Character list ───────────────────────────────────────────────────────

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

    // ─── VIP management (T21) ─────────────────────────────────────────────────

    /// <summary>Raised to request adding a player to the VIP list.</summary>
    public event Action<string>? AddVipRequested;   // name

    /// <summary>Raised to request removing a player from the VIP list.</summary>
    public event Action<uint>? RemoveVipRequested;  // id

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
}
