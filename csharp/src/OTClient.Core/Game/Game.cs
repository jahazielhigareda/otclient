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

    public Map          Map         { get; } = new();
    public MapView      MapView     { get; } = new();
    public LightView    LightView   { get; } = new();
    public Minimap      Minimap     { get; } = new();
    public LocalPlayer  LocalPlayer { get; } = new();
    public GameConfig   Config      { get; } = new();

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
}
