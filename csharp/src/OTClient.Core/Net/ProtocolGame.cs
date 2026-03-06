using System.Security.Cryptography;

namespace OTClient.Framework.Net;

// ─── Opcode enumerations ─────────────────────────────────────────────────────

/// <summary>Packets sent by the client to the game server (Tibia 12.x).</summary>
public enum GameClientPacket : byte
{
    LoginRequest  = 0x0A,   // first packet to login server
    EnterGame     = 0x0F,   // sent after login server approves
    QuitGame      = 0x14,
    PingBack      = 0x1E,
    AutoWalk      = 0x64,
    MoveNorth     = 0x65,
    MoveEast      = 0x66,
    MoveSouth     = 0x67,
    MoveWest      = 0x68,
    Stop          = 0x69,
    MoveNorthEast = 0x6A,
    MoveSouthEast = 0x6B,
    MoveSouthWest = 0x6C,
    MoveNorthWest = 0x6D,
    TurnNorth     = 0x6F,
    TurnEast      = 0x70,
    TurnSouth     = 0x71,
    TurnWest      = 0x72,
    Say           = 0x96,
}

/// <summary>Packets sent by the game server to the client (Tibia 12.x).</summary>
public enum GameServerPacket : byte
{
    Ping             = 0x1C,
    PingBack         = 0x1D,
    LoginError       = 0x14,
    LoginAdvice      = 0x15,
    LoginWait        = 0x16,
    SessionEnd       = 0x17,
    Death            = 0x28,
    InitGame         = 0x64,
    MoveNorth        = 0x65,
    MoveEast         = 0x66,
    MoveSouth        = 0x67,
    MoveWest         = 0x68,
    TileAddThing     = 0x6A,   // GameServerCreateOnMap   — parseTileAddThing (T02)
    TileTransformThing = 0x6B, // GameServerChangeOnMap   — parseTileTransformThing (T02)
    TileRemoveThing  = 0x6C,   // GameServerDeleteOnMap   — parseTileRemoveThing (T02)
    MoveCreature     = 0x6D,   // GameServerMoveCreature  — parseCreatureMove (T03)
    CreatureData     = 0x8B,   // GameServerCreatureData  — parseCreatureData (T03)
    CreatureHealth   = 0x8C,   // GameServerCreatureHealth — parseCreatureHealth (T03)
    CreatureOutfit   = 0x8E,   // GameServerCreatureOutfit — parseCreatureOutfit (T03)
    CreatureSpeed    = 0x8F,   // GameServerCreatureSpeed  — parseCreatureSpeed (T03)
    PlayerData       = 0xA0,   // parsePlayerStats (T05)
    PlayerSkills     = 0xA1,   // parsePlayerSkills (T05)
    PlayerState      = 0xA2,   // parsePlayerState (T05)
    PlayerModes      = 0xA7,   // parsePlayerModes (T05)
    TextMessage      = 0xB4,
    PlayerSpeech     = 0x96,
}

/// <summary>Walk / look directions (Tibia wire encoding).</summary>
public enum Direction : byte
{
    North     = 0,
    East      = 1,
    South     = 2,
    West      = 3,
    NorthEast = 4,
    SouthEast = 5,
    SouthWest = 6,
    NorthWest = 7,
}

/// <summary>In-game speech modes.</summary>
public enum ChatMode : byte
{
    Say     = 0x01,
    Whisper = 0x02,
    Yell    = 0x03,
    Private = 0x04,
    Channel = 0x05,
}

// ─── ProtocolGame (main state + lifecycle) ────────────────────────────────────

/// <summary>
/// Tibia 12.x game protocol implementation.
/// <para>
/// The class is split across three partial files for organisation:
/// <list type="bullet">
///   <item><description><c>ProtocolGame.cs</c> — state, lifecycle, constructor</description></item>
///   <item><description><c>ProtocolGameSend.cs</c> — outgoing packet methods (task 5.7)</description></item>
///   <item><description><c>ProtocolGameParse.cs</c> — incoming packet parsers (task 5.8)</description></item>
/// </list>
/// Maps to <c>src/client/protocolgame.*</c>.
/// Task 5.6.
/// </para>
/// </summary>
public sealed partial class ProtocolGame : Protocol
{
    // ─── XTEA session key ─────────────────────────────────────────────────────

    private uint[] _xteaKey = new uint[4];
    private bool   _encryptEnabled;

    // ─── Player / session state ───────────────────────────────────────────────

    private string _characterName = string.Empty;
    private string _accountName   = string.Empty;
    private string _password      = string.Empty;

    /// <summary>Character name chosen for this session.</summary>
    public string CharacterName => _characterName;

    /// <summary><c>true</c> once the server has confirmed game entry.</summary>
    public bool IsInGame { get; private set; }

    // ─── Events ───────────────────────────────────────────────────────────────

    /// <summary>Server rejected the login (human-readable <paramref name="reason"/>).</summary>
    public event Action<string>? LoginError;

    /// <summary>Server provided a non-fatal login advisory message.</summary>
    public event Action<string>? LoginAdvice;

    /// <summary>Server requests the client to wait before retrying.</summary>
    public event Action<string, int>? LoginWait;

    /// <summary>Player entered the game world successfully.</summary>
    public event Action? GameEntered;

    /// <summary>Player character died.</summary>
    public event Action? PlayerDied;

    /// <summary>A text message was received from the server.</summary>
    public event Action<byte, string>? TextMessageReceived;

    /// <summary>A creature spoke in-game.</summary>
    public event Action<string, ChatMode, string>? SpeechReceived;

    /// <summary>
    /// Raised when the server sends updated player stat values.
    /// Parameters: (health, maxHealth, mana, maxMana, freeCapacity, experience, level, levelPercent, stamina, soul)
    /// </summary>
    public event Action<int, int, int, int, int, ulong, int, int, int, int>? PlayerStatsUpdated;

    /// <summary>
    /// Raised when the server sends updated player skill values.
    /// Parameters: (magicLevel, magicLevelPercent, fist, club, sword, axe, distance, shielding, fishing)
    /// — all skill values are level only; percent for combat skills is also included in a separate event arg.
    /// </summary>
    public event Action<int, int, int[], int[]>? PlayerSkillsUpdated;

    /// <summary>
    /// Raised when the server sends updated player condition flags.
    /// Parameter: bitmask of active conditions.
    /// </summary>
    public event Action<uint>? PlayerStateUpdated;

    /// <summary>
    /// Raised when the server sends updated player combat modes.
    /// Parameters: (fightMode, chaseMode, safeMode, pvpMode)
    /// </summary>
    public event Action<Game.FightMode, Game.ChaseMode, bool, Game.PvpMode>? PlayerModesUpdated;

    // ─── Creature events (T03) ────────────────────────────────────────────────

    /// <summary>
    /// Raised when a creature moves via the tile-position form of
    /// <c>MoveCreature</c> (wire x != 0xFFFF).
    /// Parameters: (fromPos, stackPos, toPos)
    /// </summary>
    public event Action<Game.Position, int, Game.Position>? CreatureTileMoved;

    /// <summary>
    /// Raised when a creature moves via the creature-ID form of
    /// <c>MoveCreature</c> (wire x == 0xFFFF).
    /// Parameters: (creatureId, toPos)
    /// </summary>
    public event Action<uint, Game.Position>? CreatureMovedById;

    /// <summary>
    /// Raised when the server sends a creature health-percent update.
    /// Parameters: (creatureId, healthPercent 0–100)
    /// </summary>
    public event Action<uint, byte>? CreatureHealthUpdated;

    /// <summary>
    /// Raised when the server sends a creature outfit update.
    /// Parameters: (creatureId, outfit)
    /// </summary>
    public event Action<uint, Game.Outfit>? CreatureOutfitUpdated;

    /// <summary>
    /// Raised when the server sends a creature speed update.
    /// Parameters: (creatureId, baseSpeed, speed)
    /// </summary>
    public event Action<uint, int, int>? CreatureSpeedUpdated;

    /// <summary>
    /// Raised when the server sends a <c>CreatureData</c> packet whose payload is
    /// a single byte (types 11, 12, 13).
    /// <list type="bullet">
    ///   <item><description>Type 11 — mana percent (0–100)</description></item>
    ///   <item><description>Type 12 — show-status byte</description></item>
    ///   <item><description>Type 13 — player vocation ID</description></item>
    /// </list>
    /// Parameters: (creatureId, type 11/12/13, byteValue)
    /// </summary>
    public event Action<uint, byte, byte>? CreatureDataByteReceived;

    // ─── Construction ─────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the dispatch table with handlers for all supported server packets.
    /// </summary>
    public ProtocolGame()
    {
        RegisterHandler((byte)GameServerPacket.Ping,           ParsePing);
        RegisterHandler((byte)GameServerPacket.PingBack,       ParsePingBack);
        RegisterHandler((byte)GameServerPacket.LoginError,     ParseLoginError);
        RegisterHandler((byte)GameServerPacket.LoginAdvice,    ParseLoginAdvice);
        RegisterHandler((byte)GameServerPacket.LoginWait,      ParseLoginWait);
        RegisterHandler((byte)GameServerPacket.Death,          ParseDeath);
        RegisterHandler((byte)GameServerPacket.InitGame,       ParseInitGame);
        RegisterHandler((byte)GameServerPacket.TextMessage,    ParseTextMessage);
        RegisterHandler((byte)GameServerPacket.PlayerSpeech,   ParsePlayerSpeech);
        RegisterHandler((byte)GameServerPacket.PlayerData,     ParsePlayerStats);
        RegisterHandler((byte)GameServerPacket.PlayerSkills,   ParsePlayerSkills);
        RegisterHandler((byte)GameServerPacket.PlayerState,    ParsePlayerState);
        RegisterHandler((byte)GameServerPacket.PlayerModes,    ParsePlayerModes);
        RegisterHandler((byte)GameServerPacket.MoveCreature,   ParseCreatureMove);
        RegisterHandler((byte)GameServerPacket.CreatureData,   ParseCreatureData);
        RegisterHandler((byte)GameServerPacket.CreatureHealth, ParseCreatureHealth);
        RegisterHandler((byte)GameServerPacket.CreatureOutfit, ParseCreatureOutfit);
        RegisterHandler((byte)GameServerPacket.CreatureSpeed,  ParseCreatureSpeed);
    }

    // ─── Lifecycle overrides ──────────────────────────────────────────────────

    /// <summary>
    /// Called when the TCP connection to the login / game server is established.
    /// Generates a fresh random XTEA key for this session.
    /// </summary>
    protected override void OnConnected()
    {
        // Generate a cryptographically random XTEA session key
        byte[] raw = RandomNumberGenerator.GetBytes(16);
        for (int i = 0; i < 4; i++)
            _xteaKey[i] = BitConverter.ToUInt32(raw, i * 4);
    }

    protected override void OnDisconnected() => IsInGame = false;

    /// <summary>
    /// Overrides the base parser: XTEA-decrypts game-server packets before
    /// dispatching to registered handlers.
    /// </summary>
    protected override void ParseMessage(InputMessage msg)
    {
        if (_encryptEnabled)
        {
            msg.XteaDecrypt(_xteaKey);
            int innerLen = msg.ReadU16(); // advance past the 2-byte inner size
            _ = innerLen;                 // length is already captured in msg.Length
        }
        base.ParseMessage(msg);
    }
}
