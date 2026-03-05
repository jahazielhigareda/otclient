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
    MoveNorthWest = 0x69,
    MoveNorthEast = 0x6A,
    MoveSouthEast = 0x6B,
    MoveSouthWest = 0x6C,
    TurnNorth     = 0x6F,
    TurnEast      = 0x70,
    TurnSouth     = 0x71,
    TurnWest      = 0x72,
    Say           = 0x96,
}

/// <summary>Packets sent by the game server to the client (Tibia 12.x).</summary>
public enum GameServerPacket : byte
{
    Ping          = 0x1C,
    PingBack      = 0x1D,
    LoginError    = 0x14,
    LoginAdvice   = 0x15,
    LoginWait     = 0x16,
    SessionEnd    = 0x17,
    Death         = 0x28,
    InitGame      = 0x64,
    MoveNorth     = 0x65,
    MoveEast      = 0x66,
    MoveSouth     = 0x67,
    MoveWest      = 0x68,
    AddCreature   = 0x6A,
    RemoveCreature= 0x6B,
    MoveCreature  = 0x6C,
    TextMessage   = 0xB4,
    PlayerSpeech  = 0x96,
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
