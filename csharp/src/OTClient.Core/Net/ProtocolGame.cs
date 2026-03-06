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
    Say           = 0x96,   // ClientTalk (150)
    RequestChannels    = 0x97,   // ClientRequestChannels (151) — T10
    JoinChannel        = 0x98,   // ClientJoinChannel (152)    — T10
    LeaveChannel       = 0x99,   // ClientLeaveChannel (153)   — T10
    OpenPrivateChannel = 0x9A,   // ClientOpenPrivateChannel (154) — T10
    ChangeFightModes   = 0xA0,   // ClientChangeFightModes (160)  — T12
    Attack             = 0xA1,   // ClientAttack (161)            — T12
    Follow             = 0xA2,   // ClientFollow (162)            — T12
    CancelAttackAndFollow = 0xBE, // ClientCancelAttackAndFollow (190) — T12
    InspectNpcTrade    = 0x79,   // ClientInspectNpcTrade (121)   — T15
    BuyItem            = 0x7A,   // ClientBuyItem (122)           — T15
    SellItem           = 0x7B,   // ClientSellItem (123)          — T15
    CloseNpcTrade      = 0x7C,   // ClientCloseNpcTrade (124)     — T15
    RequestTrade       = 0x7D,   // ClientRequestTrade (125)      — T16
    InspectTrade       = 0x7E,   // ClientInspectTrade (126)      — T16
    AcceptTrade        = 0x7F,   // ClientAcceptTrade (127)       — T16
    RejectTrade        = 0x80,   // ClientRejectTrade (128)       — T16
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
    FloorDescription = 0x4B,   // GameServerFloorDescription (75) — parseFloorDescription (T01)
    FullMap          = 0x64,   // GameServerFullMap (100)         — parseMapDescription (T01)
    MapTopRow        = 0x65,   // GameServerMapTopRow (101)       — map scroll north (T01)
    MapRightRow      = 0x66,   // GameServerMapRightRow (102)     — map scroll east (T01)
    MapBottomRow     = 0x67,   // GameServerMapBottomRow (103)    — map scroll south (T01)
    MapLeftRow       = 0x68,   // GameServerMapLeftRow (104)      — map scroll west (T01)
    UpdateTile       = 0x69,   // GameServerUpdateTile (105)      — parseUpdateTile (T02)
    TileAddThing     = 0x6A,   // GameServerCreateOnMap (106)     — parseTileAddThing (T02)
    TileTransformThing = 0x6B, // GameServerChangeOnMap (107)     — parseTileTransformThing (T02)
    TileRemoveThing  = 0x6C,   // GameServerDeleteOnMap (108)     — parseTileRemoveThing (T02)
    MoveCreature     = 0x6D,   // GameServerMoveCreature (109)    — parseCreatureMove (T03)
    OpenContainer    = 0x6E,   // GameServerOpenContainer (110)   — parseOpenContainer (T04)
    CloseContainer   = 0x6F,   // GameServerCloseContainer (111)  — parseCloseContainer (T04)
    ContainerAddItem    = 0x70, // GameServerCreateContainer (112) — parseContainerAddItem (T04)
    ContainerUpdateItem = 0x71, // GameServerChangeInContainer (113)— parseContainerUpdateItem (T04)
    ContainerRemoveItem = 0x72, // GameServerDeleteInContainer (114)— parseContainerRemoveItem (T04)
    SetInventory     = 0x78,   // GameServerSetInventory (120)    — parseAddInventoryItem (T04)
    DeleteInventory  = 0x79,   // GameServerDeleteInventory (121) — parseRemoveInventoryItem (T04)
    OpenNpcTrade     = 0x7A,   // GameServerOpenNpcTrade (122)    — parseOpenNpcTrade (T15)
    PlayerGoods      = 0x7B,   // GameServerPlayerGoods (123)     — parsePlayerGoods (T15)
    CloseNpcTrade    = 0x7C,   // GameServerCloseNpcTrade (124)   — parseCloseNpcTrade (T15)
    OwnTrade         = 0x7D,   // GameServerOwnTrade (125)        — parseOwnTrade (T16)
    CounterTrade     = 0x7E,   // GameServerCounterTrade (126)    — parseCounterTrade (T16)
    CloseTrade       = 0x7F,   // GameServerCloseTrade (127)      — parseCloseTrade (T16)
    CreatureData     = 0x8B,   // GameServerCreatureData  (139)   — parseCreatureData (T03)
    CreatureHealth   = 0x8C,   // GameServerCreatureHealth (140)  — parseCreatureHealth (T03)
    CreatureOutfit   = 0x8E,   // GameServerCreatureOutfit (142)  — parseCreatureOutfit (T03)
    CreatureSpeed    = 0x8F,   // GameServerCreatureSpeed  (143)  — parseCreatureSpeed (T03)
    CreatureSkull    = 0x90,   // GameServerCreatureSkull  (144)  — parseCreatureSkulls (T13)
    CreatureParty    = 0x91,   // GameServerCreatureParty  (145)  — parseCreatureShields (T13)
    CreatureMarks    = 0x93,   // GameServerCreatureMarks  (147)  — parseCreaturesMark (T13)
    PlayerData       = 0xA0,   // parsePlayerStats (T05)
    PlayerSkills     = 0xA1,   // parsePlayerSkills (T05)
    PlayerState      = 0xA2,   // parsePlayerState (T05)
    PlayerModes      = 0xA7,   // parsePlayerModes (T05)
    Talk             = 0xAA,   // GameServerTalk (170)            — parseTalk (T09)
    ChannelList      = 0xAB,   // GameServerChannels (171)        — parseChannelList (T09)
    OpenChannel      = 0xAC,   // GameServerOpenChannel (172)     — parseOpenChannel (T09)
    OpenPrivateChannel = 0xAD, // GameServerOpenPrivateChannel (173) — parseOpenPrivateChannel (T09)
    CloseChannel     = 0xB3,   // GameServerCloseChannel (179)    — parseCloseChannel (T09)
    TextMessage      = 0xB4,
    PlayerSpeech     = 0x96,
    VipAdd           = 0xD2,   // GameServerVipAdd (210)          — parseVipAdd (T11)
    VipState         = 0xD3,   // GameServerVipState (211)        — parseVipState (T11)
    VipLogout        = 0xD4,   // GameServerVipLogout (212)       — parseVipLogout (T11)
    CancelWalk       = 0xB5,   // GameServerCancelWalk (181)      — parseCancelWalk (T13)
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

/// <summary>
/// Talk message modes received from the server (protocol 1281 wire-byte values).
/// Maps to <c>Otc::MessageMode</c> enum values that equal the wire byte for
/// client versions ≥ 1055 (see <c>protocolcodes.cpp buildMessageModesMap</c>).
/// Position-reading modes: Say/Whisper/Yell/Spell/NpcFromStartBlock/NpcTo/BarkLow/BarkLoud/Potion.
/// Channel-ID modes: Channel/ChannelManagement/ChannelHighlight/GamemasterChannel.
/// Task T09.
/// </summary>
public enum TalkMode : byte
{
    None                  = 0,
    Say                   = 1,
    Whisper               = 2,
    Yell                  = 3,
    PrivateFrom           = 4,
    PrivateTo             = 5,
    ChannelManagement     = 6,
    Channel               = 7,
    ChannelHighlight      = 8,
    Spell                 = 9,
    NpcFromStartBlock     = 10,
    NpcFrom               = 11,
    NpcTo                 = 12,
    GamemasterBroadcast   = 13,
    GamemasterChannel     = 14,
    GamemasterPrivateFrom = 15,
    GamemasterPrivateTo   = 16,
    Login                 = 17,
    Game                  = 19,
    BarkLow               = 36,
    BarkLoud              = 37,
    Potion                = 52,
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

    // ─── Map state (T01/T02) ─────────────────────────────────────────────────

    private readonly Game.Map _map = new();
    private bool _mapKnown;

    /// <summary>The game map populated by incoming map-description packets.</summary>
    public Game.Map Map => _map;

    // ─── Container / inventory state (T04/T08) ────────────────────────────────

    private const int MaxContainers = 64;
    private readonly Game.Container?[] _containers = new Game.Container[MaxContainers];

    /// <summary>
    /// Returns the open container at wire slot <paramref name="id"/>, or <c>null</c>.
    /// </summary>
    public Game.Container? GetContainer(int id)
        => (uint)id < MaxContainers ? _containers[id] : null;

    // Inventory (slots 0-15; slot 0 unused, 1=Head … 15=Ext4)
    private const int InventorySlotCount = 16;
    private readonly Game.Item?[] _inventory = new Game.Item[InventorySlotCount];

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

    /// <summary>
    /// Raised when the server sends a creature skull update (packet 0x90).
    /// Parameters: (creatureId, skullByte)
    /// Maps to <c>ProtocolGame::parseCreatureSkulls</c>.
    /// Task T13.
    /// </summary>
    public event Action<uint, byte>? CreatureSkullUpdated;

    /// <summary>
    /// Raised when the server sends a creature party/shield update (packet 0x91).
    /// Parameters: (creatureId, shieldByte)
    /// Maps to <c>ProtocolGame::parseCreatureShields</c>.
    /// Task T13.
    /// </summary>
    public event Action<uint, byte>? CreatureShieldUpdated;

    /// <summary>
    /// Raised when the server sends a creature marks/square update (packet 0x93).
    /// Parameters: (creatureId, isPermanent, markType)
    /// Maps to <c>ProtocolGame::parseCreaturesMark</c>.
    /// Task T13.
    /// </summary>
    public event Action<uint, bool, byte>? CreatureMarksUpdated;

    /// <summary>
    /// Raised when the server cancels the local player's walk (packet 0xB5).
    /// Parameter: direction the player is facing after the cancel.
    /// Maps to <c>ProtocolGame::parseCancelWalk → Game::processWalkCancel</c>.
    /// Task T13.
    /// </summary>
    public event Action<Game.Direction>? WalkCanceled;

    // ─── Map events (T01/T02) ─────────────────────────────────────────────────

    /// <summary>
    /// Raised after the server sends a full or partial map description that has
    /// been applied to <see cref="Map"/>.
    /// Parameters: (centralPosition)
    /// </summary>
    public event Action<Game.Position>? MapDescriptionReceived;

    /// <summary>
    /// Raised after a single tile has been fully populated from the wire
    /// description (UpdateTile, TileAddThing, or map-scroll row).
    /// Parameters: (tilePosition)
    /// </summary>
    public event Action<Game.Position>? TileDescriptionSet;

    // ─── Container and inventory events (T04) ────────────────────────────────

    /// <summary>
    /// Raised when the server opens a container.
    /// Parameters: (container)
    /// </summary>
    public event Action<Game.Container>? ContainerOpened;

    /// <summary>
    /// Raised when the server closes a container.
    /// Parameters: (containerId)
    /// </summary>
    public event Action<int>? ContainerClosed;

    /// <summary>
    /// Raised when an item is added to a container slot.
    /// Parameters: (containerId, slot, item)
    /// </summary>
    public event Action<int, int, Game.Item>? ContainerItemAdded;

    /// <summary>
    /// Raised when an item in a container slot is replaced.
    /// Parameters: (containerId, slot, newItem)
    /// </summary>
    public event Action<int, int, Game.Item>? ContainerItemUpdated;

    /// <summary>
    /// Raised when an item is removed from a container slot.
    /// Parameters: (containerId, slot, lastItem — may be null for non-paginated)
    /// </summary>
    public event Action<int, int, Game.Item?>? ContainerItemRemoved;

    /// <summary>
    /// Raised when an inventory slot is set or cleared.
    /// Parameters: (slot, item — null when the slot is cleared)
    /// </summary>
    public event Action<Game.InventorySlot, Game.Item?>? InventoryItemChanged;

    // ─── Chat / channel events (T09) ─────────────────────────────────────────

    /// <summary>
    /// Raised when any in-game talk message is received from the server.
    /// Parameters: (author, level, mode, text, channelId — 0 when not a channel message,
    ///   position — null when not a positional message)
    /// Maps to <c>ProtocolGame::parseTalk</c>.
    /// </summary>
    public event Action<string, int, TalkMode, string, ushort, Game.Position?>? TalkReceived;

    /// <summary>
    /// Raised when the server sends the list of available channels.
    /// Parameters: IReadOnlyList of (channelId, channelName) tuples.
    /// Maps to <c>ProtocolGame::parseChannelList</c>.
    /// </summary>
    public event Action<IReadOnlyList<(ushort Id, string Name)>>? ChannelListReceived;

    /// <summary>
    /// Raised when the server opens a public channel.
    /// Parameters: (channelId, channelName)
    /// Maps to <c>ProtocolGame::parseOpenChannel</c>.
    /// </summary>
    public event Action<ushort, string>? ChannelOpened;

    /// <summary>
    /// Raised when the server opens a private channel.
    /// Parameters: (playerName)
    /// Maps to <c>ProtocolGame::parseOpenPrivateChannel</c>.
    /// </summary>
    public event Action<string>? PrivateChannelOpened;

    /// <summary>
    /// Raised when the server closes a channel.
    /// Parameters: (channelId)
    /// Maps to <c>ProtocolGame::parseCloseChannel</c>.
    /// </summary>
    public event Action<ushort>? ChannelClosed;

    // ─── VIP events (T11) ────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server adds an entry to the VIP (friends) list.
    /// Parameters: (id, name, status, description, iconId, notifyLogin)
    /// Maps to <c>ProtocolGame::parseVipAdd</c>.
    /// </summary>
    public event Action<uint, string, uint, string, uint, bool>? VipAdded;

    /// <summary>
    /// Raised when the server updates the online/offline status of a VIP entry.
    /// Parameters: (id, status — 0=offline, 1=online)
    /// Maps to <c>ProtocolGame::parseVipState</c>.
    /// </summary>
    public event Action<uint, uint>? VipStateChanged;

    // ─── NPC trade events (T15) ───────────────────────────────────────────────

    /// <summary>
    /// Raised when the server opens the NPC trade window.
    /// Parameters: list of <see cref="Game.NpcTradeItem"/> entries.
    /// Maps to <c>ProtocolGame::parseOpenNpcTrade</c>.
    /// </summary>
    public event Action<IReadOnlyList<Game.NpcTradeItem>>? NpcTradeOpened;

    /// <summary>
    /// Raised when the server sends the player's current goods for trade.
    /// Parameters: (totalMoney, list of (itemId, amount) pairs).
    /// Maps to <c>ProtocolGame::parsePlayerGoods</c>.
    /// </summary>
    public event Action<ulong, IReadOnlyList<(int ItemId, int Amount)>>? PlayerGoodsReceived;

    /// <summary>
    /// Raised when the server closes the NPC trade window.
    /// Maps to <c>ProtocolGame::parseCloseNpcTrade</c>.
    /// </summary>
    public event Action? NpcTradeClosed;

    // ─── Player-to-player trade events (T16) ──────────────────────────────────

    /// <summary>
    /// Raised when the server sends the player's own trade offer.
    /// Parameters: (partnerName, list of items).
    /// Maps to <c>ProtocolGame::parseOwnTrade</c>.
    /// </summary>
    public event Action<string, IReadOnlyList<Game.Item>>? OwnTradeReceived;

    /// <summary>
    /// Raised when the server sends the partner's trade counter-offer.
    /// Parameters: (partnerName, list of items).
    /// Maps to <c>ProtocolGame::parseCounterTrade</c>.
    /// </summary>
    public event Action<string, IReadOnlyList<Game.Item>>? CounterTradeReceived;

    /// <summary>
    /// Raised when the player-to-player trade is closed by the server.
    /// Maps to <c>ProtocolGame::parseCloseTrade</c>.
    /// </summary>
    public event Action? PlayerTradeClosed;

    // ─── Construction ─────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the dispatch table with handlers for all supported server packets.
    /// </summary>
    public ProtocolGame()
    {
        RegisterHandler((byte)GameServerPacket.Ping,              ParsePing);
        RegisterHandler((byte)GameServerPacket.PingBack,          ParsePingBack);
        RegisterHandler((byte)GameServerPacket.LoginError,        ParseLoginError);
        RegisterHandler((byte)GameServerPacket.LoginAdvice,       ParseLoginAdvice);
        RegisterHandler((byte)GameServerPacket.LoginWait,         ParseLoginWait);
        RegisterHandler((byte)GameServerPacket.Death,             ParseDeath);
        RegisterHandler((byte)GameServerPacket.FullMap,           ParseMapDescription);
        RegisterHandler((byte)GameServerPacket.FloorDescription,  ParseFloorDescription);
        RegisterHandler((byte)GameServerPacket.MapTopRow,         ParseMapMoveNorth);
        RegisterHandler((byte)GameServerPacket.MapRightRow,       ParseMapMoveEast);
        RegisterHandler((byte)GameServerPacket.MapBottomRow,      ParseMapMoveSouth);
        RegisterHandler((byte)GameServerPacket.MapLeftRow,        ParseMapMoveWest);
        RegisterHandler((byte)GameServerPacket.UpdateTile,        ParseUpdateTile);
        RegisterHandler((byte)GameServerPacket.TileAddThing,      ParseTileAddThing);
        RegisterHandler((byte)GameServerPacket.TileTransformThing,ParseTileTransformThing);
        RegisterHandler((byte)GameServerPacket.TileRemoveThing,   ParseTileRemoveThing);
        RegisterHandler((byte)GameServerPacket.TextMessage,       ParseTextMessage);
        RegisterHandler((byte)GameServerPacket.PlayerSpeech,      ParsePlayerSpeech);
        RegisterHandler((byte)GameServerPacket.PlayerData,        ParsePlayerStats);
        RegisterHandler((byte)GameServerPacket.PlayerSkills,      ParsePlayerSkills);
        RegisterHandler((byte)GameServerPacket.PlayerState,       ParsePlayerState);
        RegisterHandler((byte)GameServerPacket.PlayerModes,       ParsePlayerModes);
        RegisterHandler((byte)GameServerPacket.MoveCreature,      ParseCreatureMove);
        RegisterHandler((byte)GameServerPacket.OpenContainer,      ParseOpenContainer);
        RegisterHandler((byte)GameServerPacket.CloseContainer,     ParseCloseContainer);
        RegisterHandler((byte)GameServerPacket.ContainerAddItem,   ParseContainerAddItem);
        RegisterHandler((byte)GameServerPacket.ContainerUpdateItem,ParseContainerUpdateItem);
        RegisterHandler((byte)GameServerPacket.ContainerRemoveItem,ParseContainerRemoveItem);
        RegisterHandler((byte)GameServerPacket.SetInventory,       ParseAddInventoryItem);
        RegisterHandler((byte)GameServerPacket.DeleteInventory,    ParseRemoveInventoryItem);
        RegisterHandler((byte)GameServerPacket.CreatureData,       ParseCreatureData);
        RegisterHandler((byte)GameServerPacket.CreatureHealth,    ParseCreatureHealth);
        RegisterHandler((byte)GameServerPacket.CreatureOutfit,    ParseCreatureOutfit);
        RegisterHandler((byte)GameServerPacket.CreatureSpeed,     ParseCreatureSpeed);
        RegisterHandler((byte)GameServerPacket.CreatureSkull,     ParseCreatureSkull);
        RegisterHandler((byte)GameServerPacket.CreatureParty,     ParseCreatureShield);
        RegisterHandler((byte)GameServerPacket.CreatureMarks,     ParseCreatureMarks);
        RegisterHandler((byte)GameServerPacket.CancelWalk,        ParseCancelWalk);
        RegisterHandler((byte)GameServerPacket.Talk,              ParseTalk);
        RegisterHandler((byte)GameServerPacket.ChannelList,       ParseChannelList);
        RegisterHandler((byte)GameServerPacket.OpenChannel,       ParseOpenChannel);
        RegisterHandler((byte)GameServerPacket.OpenPrivateChannel,ParseOpenPrivateChannel);
        RegisterHandler((byte)GameServerPacket.CloseChannel,      ParseCloseChannel);
        RegisterHandler((byte)GameServerPacket.VipAdd,            ParseVipAdd);
        RegisterHandler((byte)GameServerPacket.VipState,          ParseVipState);
        RegisterHandler((byte)GameServerPacket.VipLogout,         ParseVipLogout);
        RegisterHandler((byte)GameServerPacket.OpenNpcTrade,      ParseOpenNpcTrade);
        RegisterHandler((byte)GameServerPacket.PlayerGoods,       ParsePlayerGoods);
        RegisterHandler((byte)GameServerPacket.CloseNpcTrade,     ParseCloseNpcTrade);
        RegisterHandler((byte)GameServerPacket.OwnTrade,          ParseOwnTrade);
        RegisterHandler((byte)GameServerPacket.CounterTrade,      ParseCounterTrade);
        RegisterHandler((byte)GameServerPacket.CloseTrade,        ParseCloseTrade);
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
