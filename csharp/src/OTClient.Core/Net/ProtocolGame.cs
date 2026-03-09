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
    AddVip             = 0xDC,   // ClientAddVip (220)            — T21
    RemoveVip          = 0xDD,   // ClientRemoveVip (221)         — T21
    EditText           = 0x89,   // ClientEditText (137)          — T23
    EditList           = 0x8A,   // ClientEditList (138)          — T23
    RequestQuestLog    = 0xF0,   // ClientRequestQuestLog (240)   — T23
    RequestQuestLine   = 0xF1,   // ClientRequestQuestLine (241)  — T23
    AnswerModalDialog  = 0xF9,   // ClientAnswerModalDialog (249) — T23
    MarketLeave        = 0xF4,   // ClientMarketLeave (244)       — T26
    MarketBrowse       = 0xF5,   // ClientMarketBrowse (245)      — T26
    MarketCreate       = 0xF6,   // ClientMarketCreate (246)      — T26
    MarketCancel       = 0xF7,   // ClientMarketCancel (247)      — T26
    MarketAccept       = 0xF8,   // ClientMarketAccept (248)      — T26
    // Item and container operations (T39)
    MoveItem           = 0x78,   // ClientMove (120)               — T39
    UseItem            = 0x82,   // ClientUseItem (130)            — T39
    UseItemWith        = 0x83,   // ClientUseItemWith (131)        — T39
    UseOnCreature      = 0x84,   // ClientUseOnCreature (132)      — T39
    RotateItem         = 0x85,   // ClientRotateItem (133)         — T39
    CloseContainer     = 0x87,   // ClientCloseContainer (135)     — T39
    UpContainer        = 0x88,   // ClientUpContainer (136)        — T39
    OnWrapItem         = 0x8B,   // ClientOnWrapItem (139)         — T39
    LookAt             = 0x8C,   // ClientLook (140)               — T39
    LookCreature       = 0x8D,   // ClientLookCreature (141)       — T39
    BrowseField        = 0xCB,   // ClientBrowseField (203)        — T39
    SeekInContainer    = 0xCC,   // ClientSeekInContainer (204)    — T39
    // Party management (T57)
    PartyAnalyzerAction  = 0x2B,  // ClientPartyAnalyzerAction (43)      — T57
    InviteToParty        = 0xA3,  // ClientInviteToParty (163)           — T57
    JoinParty            = 0xA4,  // ClientJoinParty (164)               — T57
    RevokeInvitation     = 0xA5,  // ClientRevokeInvitation (165)        — T57
    PassLeadership       = 0xA6,  // ClientPassLeadership (166)          — T57
    LeaveParty           = 0xA7,  // ClientLeaveParty (167)              — T57
    ShareExperience      = 0xA8,  // ClientShareExperience (168)         — T57
    // Own channel management (T57)
    OpenOwnChannel          = 0xAA, // ClientOpenOwnChannel (170)          — T57
    InviteToOwnChannel      = 0xAB, // ClientInviteToOwnChannel (171)      — T57
    ExcludeFromOwnChannel   = 0xAC, // ClientExcludeFromOwnChannel (172)   — T57
    // Outfit / mount / typing (T57)
    Typing         = 0x38,  // GameServerCreatureTyping (56) — bidirectional   — T57
    RequestOutfit  = 0xD2,  // ClientRequestOutfit (210)                       — T57
    ChangeOutfit   = 0xD3,  // ClientChangeOutfit (211)                        — T57
    MountToggle    = 0xD4,  // ClientMount (212)                               — T57
    // VIP edit, misc utility, bug/debug reporting (T59)
    GmTeleport               = 0x73,  // ClientGmTeleport (115)                — T59
    EquipItem                = 0x77,  // ClientEquipItem (119)                 — T59
    RefreshContainer         = 0xCA,  // ClientRefreshContainer (202)          — T59
    RequestBless             = 0xCF,  // ClientRequestBless (207)              — T59
    RequestTrackerQuestLog   = 0xD0,  // ClientRequestTrackerQuestLog (208)    — T59
    EditVip                  = 0xDE,  // ClientEditVip (222)                   — T59
    EditVipGroups            = 0xDF,  // ClientEditVipGroups (223)             — T59
    BugReport                = 0xE6,  // ClientBugReport (230)                 — T59
    DebugReport              = 0xE8,  // ClientDebugReport (232)               — T59
    // Channel closure, rule violations, item inspection, bestiary, bosstiary (T60)
    BestiaryTrackerStatus        = 0x2A,  // ClientBestiaryTrackerStatus (42)          — T60
    CloseNpcChannel              = 0x9E,  // ClientCloseNpcChannel (158)               — T60
    OpenRuleViolation            = 0x9B,  // ClientOpenRuleViolation (155)             — T60
    CloseRuleViolation           = 0x9C,  // ClientCloseRuleViolation (156)            — T60
    CancelRuleViolation          = 0x9D,  // ClientCancelRuleViolation (157)           — T60
    CyclopediaHouseAuction       = 0xAD,  // ClientCyclopediaHouseAuction (173)        — T60
    BosstiaryRequestInfo         = 0xAE,  // ClientBosstiaryRequestInfo (174)          — T60
    BosstiaryRequestSlotInfo     = 0xAF,  // ClientBosstiaryRequestSlotInfo (175)      — T60
    BosstiaryRequestSlotAction   = 0xB0,  // ClientBosstiaryRequestSlotAction (176)    — T60
    InspectionObject             = 0xCD,  // ClientInspectionObject (205)              — T60
    BestiaryRequest              = 0xE1,  // ClientBestiaryRequest (225)               — T60
    BestiaryRequestOverview      = 0xE2,  // ClientBestiaryRequestOverview (226)       — T60
    BestiaryRequestSearch        = 0xE3,  // ClientBestiaryRequestSearch (227)         — T60
    BuyCharmRune                 = 0xE4,  // ClientCyclopediaSendBuyCharmRune (228)    — T60
    CyclopediaRequestCharacterInfo = 0xE5, // ClientCyclopediaRequestCharacterInfo (229) — T60
    NewRuleViolation             = 0xF2,  // ClientNewRuleViolation (242)              — T60
    RequestItemInfo              = 0xF3,  // ClientRequestItemInfo (243)               — T60
}

/// <summary>Packets sent by the game server to the client (Tibia 12.x).</summary>
public enum GameServerPacket : byte
{
    Ping             = 0x1C,
    PingBack         = 0x1D,
    LoginOrPendingState    = 0x0A,   // GameServerLoginOrPendingState (10)   — parseLogin/parsePendingGame (T44)
    GMActions              = 0x0B,   // GameServerGMActions (11)             — parseGMActions (T44)
    ServerEnterGame        = 0x0F,   // GameServerEnterGame (15)             — parseEnterGame (T44)
    UpdateNeeded           = 0x11,   // GameServerUpdateNeeded (17)          — parseUpdateNeeded (T44)
    LoginError       = 0x14,
    LoginAdvice      = 0x15,
    LoginWait        = 0x16,
    LoginSuccess           = 0x17,   // GameServerLoginSuccess (23)          — parseLogin (T44)
    SessionEnd             = 0x18,   // GameServerSessionEnd (24)            — parseSessionEnd (T44)
    StoreButtonIndicators  = 0x19,   // GameServerStoreButtonIndicators (25) — parseStoreButtonIndicators (T44)
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
    EditText         = 0x96,   // GameServerEditText (150)        — parseEditText (T23)
    EditList         = 0x97,   // GameServerEditList (151)        — parseEditList (T23)
    VipAdd           = 0xD2,   // GameServerVipAdd (210)          — parseVipAdd (T11)
    VipState         = 0xD3,   // GameServerVipState (211)        — parseVipState (T11)
    VipLogout        = 0xD4,   // GameServerVipLogout (212)       — parseVipLogout (T11)
    CancelWalk       = 0xB5,   // GameServerCancelWalk (181)      — parseCancelWalk (T13)
    QuestLog         = 0xF0,   // GameServerQuestLog (240)        — parseQuestLog (T23)
    QuestLine        = 0xF1,   // GameServerQuestLine (241)       — parseQuestLine (T23)
    MarketEnter      = 0xF6,   // GameServerMarketEnter (246)     — parseMarketEnter (T26)
    MarketLeave      = 0xF7,   // GameServerMarketLeave (247)     — parseMarketLeave (T26)
    MarketDetail     = 0xF8,   // GameServerMarketDetail (248)    — parseMarketDetail (T26)
    MarketBrowse     = 0xF9,   // GameServerMarketBrowse (249)    — parseMarketBrowse (T26)
    ModalDialog      = 0xFA,   // GameServerModalDialog (250)     — parseModalDialog (T23)

    // T27 opcodes
    ImbuementDurations = 0x5D,  // GameServerImbuementDurations (93)   — parseImbuementDurations (T27)
    OpenWheelWindow    = 0x5F,  // GameServerOpenWheelWindow (95)       — parseOpenWheelWindow (T27)
    ForgeResult        = 0x8A,  // GameServerForgeResult (138)          — parseForgeResult (T27)
    BestiaryRaces      = 0xD5,  // GameServerBestiaryRaces (213)        — parseBestiaryRaces (T27)
    BestiaryOverview   = 0xD6,  // GameServerBestiaryOverview (214)     — parseBestiaryOverview (T27)
    BestiaryMonsterData= 0xD7,  // GameServerBestiaryMonsterData (215)  — parseBestiaryMonsterData (T27)
    PreyFreeRerolls    = 0xE6,  // GameServerSendPreyFreeRerolls (230)  — parsePreyFreeRerolls (T27)
    PreyTimeLeft       = 0xE7,  // GameServerSendPreyTimeLeft (231)     — parsePreyTimeLeft (T27)
    PreyData           = 0xE8,  // GameServerSendPreyData (232)         — parsePreyData (T27)
    PreyRerollPrice    = 0xE9,  // GameServerSendPreyRerollPrice (233)  — parsePreyRerollPrice (T27)
    ImbuementWindow    = 0xEB,  // GameServerSendImbuementWindow (235)  — parseImbuementWindow (T27)

    // T28 opcodes
    CoinBalance              = 0xDF,  // GameServerCoinBalance (223)             — parseCoinBalance (T28)
    StoreError               = 0xE0,  // GameServerStoreError (224)              — parseStoreError (T28)
    CoinBalanceUpdating      = 0xF2,  // GameServerCoinBalanceUpdating (242)     — parseCoinBalanceUpdating (T28)
    Store                    = 0xFB,  // GameServerStore (251)                   — parseStore (T28)
    StoreOffers              = 0xFC,  // GameServerStoreOffers (252)             — parseStoreOffers (T28)
    StoreTransactionHistory  = 0xFD,  // GameServerStoreTransactionHistory (253) — parseStoreTransactionHistory (T28)
    StoreCompletePurchase    = 0xFE,  // GameServerStoreCompletePurchase (254)   — parseCompleteStorePurchase (T28)

    // T45 opcodes
    AttchedEffect   = 0x34,  // GameServerAttchedEffect (52)    — parseAttachedEffect (T45)
    DetachEffect    = 0x35,  // GameServerDetachEffect (53)     — parseDetachEffect (T45)
    CreatureShader  = 0x36,  // GameServerCreatureShader (54)   — parseCreatureShader (T45)
    MapShader       = 0x37,  // GameServerMapShader (55)        — parseMapShader (T45)
    CreatureTyping  = 0x38,  // GameServerCreatureTyping (56)   — parseCreatureTyping (T45)
    CreatureUnpass  = 0x92,  // GameServerCreatureUnpass (146)  — parseCreatureUnpass (T45)
    PlayerHelpers   = 0x94,  // GameServerPlayerHelpers (148)   — parsePlayerHelpers (T45)
    CreatureType    = 0x95,  // GameServerCreatureType (149)    — parseCreatureType (T45)
    FloorChangeUp   = 0xBE,  // GameServerFloorChangeUp (190)   — parseFloorChangeUp (T45)
    FloorChangeDown = 0xBF,  // GameServerFloorChangeDown (191) — parseFloorChangeDown (T45)

    // T46 opcodes
    Blessings          = 0x9C,  // GameServerBlessings (156)        — parseBlessings (T46)
    SpellCooldown      = 0xA4,  // GameServerSpellDelay (164)       — parseSpellCooldown (T46)
    SpellGroupCooldown = 0xA5,  // GameServerSpellGroupDelay (165)  — parseSpellGroupCooldown (T46)
    MultiUseCooldown   = 0xA6,  // GameServerMultiUseDelay (166)    — parseMultiUseCooldown (T46)
    OpenOwnChannel     = 0xB2,  // GameServerOpenOwnChannel (178)   — parseOpenOwnPrivateChannel (T46)
    PvpSituations      = 0xB8,  // GameServerPvpSituations (184)    — parsePvpSituations (T46)
    ResourceBalance    = 0xEE,  // GameServerResourceBalance (238)  — parseResourceBalance (T46)
    WorldTime          = 0xEF,  // GameServerWorldTime (239)        — parseWorldTime (T46)

    // T47 opcodes
    WorldLight        = 0x82,  // GameServerAmbient (130)          — parseWorldLight (T47)
    GraphicalEffect   = 0x83,  // GameServerGraphicalEffect (131)  — parseMagicEffect (T47)
    AnimatedText      = 0x84,  // GameServerTextEffect (132)       — parseAnimatedText (T47)
    DistanceMissile   = 0x85,  // GameServerMissileEffect (133)    — parseDistanceMissile (T47)
    CreatureLight     = 0x8D,  // GameServerCreatureLight (141)    — parseCreatureLight (T47)
    PlayerInfo        = 0x9F,  // GameServerPlayerDataBasic (159)  — parsePlayerInfo (T47)
    ClearTarget       = 0xA3,  // GameServerClearTarget (163)      — parsePlayerCancelAttack (T47)
    WalkWait          = 0xB6,  // GameServerWalkWait (182)         — parseWalkWait (T47)

    // T48 opcodes
    BugReport         = 0x1A,  // GameServerBugReport (26)         — parseBugReport (T48)
    Trappers          = 0x87,  // GameServerTrappers (135)         — parseTrappers (T48)
    CloseForgeWindow  = 0x89,  // GameServerCloseForgeWindow (137) — parseCloseForgeWindow (T48)
    RestingAreaState  = 0xA9,  // GameServerSendRestingAreaState (169) — parseRestingAreaState (T48)
    UnjustifiedStats  = 0xB7,  // GameServerUnjustifiedStats (183) — parseUnjustifiedStats (T48)
    TutorialHint      = 0xDC,  // GameServerTutorialHint (220)     — parseTutorialHint (T48)
    AutomapFlag       = 0xDD,  // GameServerAutomapFlag (221)      — parseAutomapFlag (T48)
    ChannelEvent      = 0xF3,  // GameServerChannelEvent (243)     — parseChannelEvent (T48)

    // T49 opcodes
    ChangeMapAwareRange          = 0x33,  // GameServerChangeMapAwareRange (51)              — parseChangeMapAwareRange (T49)
    LootContainers               = 0xC0,  // GameServerLootContainers (192)                  — parseLootContainers (T49)
    SetStoreDeepLink             = 0xA8,  // GameServerSetStoreDeepLink (168)                — parseSetStoreDeepLink (T49)
    DailyRewardCollectionState   = 0xDE,  // GameServerSendDailyRewardCollectionState (222)  — parseDailyRewardCollectionState (T49)
    OpenRewardWall               = 0xE2,  // GameServerSendOpenRewardWall (226)              — parseOpenRewardWall (T49)
    DailyReward                  = 0xE4,  // GameServerSendDailyReward (228)                 — parseDailyReward (T49)
    RewardHistory                = 0xE5,  // GameServerSendRewardHistory (229)               — parseRewardHistory (T49)

    // T50 opcodes
    ExtendedOpcode          = 0x32,  // GameServerExtendedOpcode (50)            — parseExtendedOpcode (T50)
    TakeScreenshot          = 0x75,  // GameServerTakeScreenshot (117)           — parseTakeScreenshot (T50)
    SendGameNews            = 0x98,  // GameServerSendGameNews (152)             — parseGameNews (T50)
    Preset                  = 0x9D,  // GameServerPreset (157)                   — parsePreset (T50)
    PremiumTrigger          = 0x9E,  // GameServerPremiumTrigger (158)           — parsePremiumTrigger (T50)
    RuleViolationChannel    = 0xAE,  // GameServerRuleViolationChannel (174)     — parseRuleViolationChannel (T50)
    RuleViolationRemove     = 0xAF,  // GameServerRuleViolationRemove (175) / ExperienceTracker at proto≥1200 (T50)
    RuleViolationCancel     = 0xB0,  // GameServerRuleViolationCancel (176)      — parseRuleViolationCancel (T50)
    RuleViolationLock       = 0xB1,  // GameServerRuleViolationLock (177) at proto<1310 (T50)

    // T51 opcodes
    SupplyStash             = 0x29,  // GameServerSupplyStash (41)               — parseSupplyStash (T51)
    SpecialContainer        = 0x2A,  // GameServerSpecialContainer (42)          — parseSpecialContainer (T51)
    PartyAnalyzer           = 0x2B,  // GameServerPartyAnalyzer (43)             — parsePartyAnalyzer (T51)
    AttachedPaperdoll       = 0x3C,  // GameServerAttachedPaperdoll (60)         — parseAttachedPaperdoll (T51)
    DetachPaperdoll         = 0x3D,  // GameServerDetachPaperdoll (61)           — parseDetachPaperdoll (T51)
    Features                = 0x43,  // GameServerFeatures (67)                  — parseFeatures (T51)
    WeaponProficiencyExp    = 0x5C,  // GameServerWeaponProficiencyExperience (92)— parseWeaponProficiencyExperience (T51)
    PassiveCooldown         = 0x5E,  // GameServerPassiveCooldown (94)           — parsePassiveCooldown (T51)
    BosstiaryData           = 0x61,  // GameServerBosstiaryData (97)             — parseBosstiaryData (T51)
    ClientCheck             = 0x63,  // GameServerSendClientCheck (99)           — parseClientCheck (T51)

    // T52 opcodes
    BosstiarySlots          = 0x62,  // GameServerBosstiarySlots (98)            — parseBosstiarySlots (T52)
    BosstiaryInfo           = 0x73,  // GameServerBosstiaryInfo (115)            — parseBosstiaryInfo (T52)
    BosstiaryCooldownTimer  = 0xBD,  // GameServerBosstiaryCooldownTimer (189)   — parseBosstiaryCooldownTimer (T52)
    UpdateImpactTracker     = 0xCC,  // GameServerSendUpdateImpactTracker (204)  — parseUpdateImpactTracker (T52)
    ItemsPrice              = 0xCD,  // GameServerSendItemsPrice (205)           — parseItemsPrice (T52)
    UpdateSupplyTracker     = 0xCE,  // GameServerSendUpdateSupplyTracker (206)  — parseUpdateSupplyTracker (T52)
    UpdateLootTracker       = 0xCF,  // GameServerSendUpdateLootTracker (207)    — parseUpdateLootTracker (T52)
    QuestTracker            = 0xD0,  // GameServerQuestTracker (208)             — parseQuestTracker (T52)
    KillTracker             = 0xD1,  // GameServerKillTracker (209)              — parseKillTracker (T52)
    BestiaryEntryChanged    = 0xD9,  // GameServerBestiaryEntryChanged (217)     — parseBestiaryEntryChanged (T52)
    ItemInfo                = 0xF4,  // GameServerItemInfo (244)                 — parseItemInfo (T52)
    PlayerInventory         = 0xF5,  // GameServerPlayerInventory (245)          — parsePlayerInventory (T52)

    // T53 opcodes
    BrowseForgeHistory           = 0x88,  // GameServerBrowseForgeHistory (136)             — parseBrowseForgeHistory (T53)
    BlessDialog                  = 0x9B,  // GameServerSendBlessDialog (155)                — parseBlessDialog (T53)
    BestiaryRefreshTracker       = 0xB9,  // GameServerBestiaryRefreshTracker (185)         — parseBestiaryTracker (T53)
    TaskHuntingBasicData         = 0xBA,  // GameServerTaskHuntingBasicData (186)           — parseTaskHuntingBasicData (T53)
    TaskHuntingData              = 0xBB,  // GameServerTaskHuntingData (187)                — parseTaskHuntingData (T53)
    MonkData                     = 0xC1,  // GameServerMonkData (193)                       — parseMonkData (T53)
    CyclopediaHouseAuctionMessage= 0xC3,  // GameServerCyclopediaHouseAuctionMessage (195)  — parseCyclopediaHouseAuctionMessage (T53)
    WeaponProficiencyInfo        = 0xC4,  // GameServerWeaponProficiencyInfo (196)          — parseWeaponProficiencyInfo2 (T53)
    ChooseOutfit                 = 0xC8,  // GameServerChooseOutfit (200)                   — parseChooseOutfit (T53)
    BestiaryCharmsData           = 0xD8,  // GameServerBestiaryCharmsData (216)             — parseBestiaryCharmsData (T53)

    // T54 opcodes
    CyclopediaItemDetail  = 0x76,  // GameServerCyclopediaItemDetail (118)            — parseCyclopediaItemDetail (T54)
    ItemClasses           = 0x86,  // GameServerItemClasses (134)                     — parseItemClasses (T54)
    CyclopediaHousesInfo  = 0xC6,  // GameServerCyclopediaHousesInfo (198)            — parseCyclopediaHousesInfo (T54)
    CyclopediaHouseList   = 0xC7,  // GameServerCyclopediaHouseList (199)             — parseCyclopediaHouseList (T54)
    RequestPurchaseData   = 0xE1,  // GameServerRequestPurchaseData (225)             — parseRequestPurchaseData (T54)
    ShowDescription       = 0xEA,  // GameServerSendShowDescription (234)             — parseShowDescription (T54)
    CloseImbuementWindow  = 0xEC,  // GameServerSendCloseImbuementWindow (236)        — parseCloseImbuementWindow (T54)
    ServerError           = 0xED,  // GameServerSendError (237)                       — parseServerError (T54)

    // T55 opcodes
    Challenge               = 0x1F,  // GameServerChallenge (31)                         — parseLoginChallenge (T55)
    CyclopediaCharacterInfo = 0xDA,  // GameServerCyclopediaCharacterInfoData (218)       — parseCyclopediaCharacterInfo (T55)
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

    /// <summary>
    /// The negotiated protocol version for this session.
    /// Defaults to 1281 (Tibia 12.x baseline).
    /// </summary>
    public int ProtocolVersion { get; set; } = 1281;

    // ─── Events ───────────────────────────────────────────────────────────────

    /// <summary>Server rejected the login (human-readable <paramref name="reason"/>).</summary>
    public event Action<string>? LoginError;

    /// <summary>Server provided a non-fatal login advisory message.</summary>
    public event Action<string>? LoginAdvice;

    /// <summary>Server requests the client to wait before retrying.</summary>
    public event Action<string, int>? LoginWait;

    // ─── Login-flow events (T44) ──────────────────────────────────────────────

    /// <summary>
    /// Raised when the server accepts the login (GameServerLoginSuccess = 0x17 or
    /// GameServerLoginOrPendingState = 0x0A without GameLoginPending).
    /// Parameters: (playerId, serverBeat, speedA, speedB, speedC, expertPvpMode,
    ///              storeUrl, coinsPacketSize)
    /// Maps to <c>ProtocolGame::parseLogin</c>.
    /// Task T44.
    /// </summary>
    public event Action<uint, ushort, double, double, double, bool, string, ushort>? LoginSuccessReceived;

    /// <summary>
    /// Raised when the server sends <c>GameServerLoginOrPendingState</c> (0x0A)
    /// in "pending" mode (GameLoginPending feature active).
    /// No parameters.
    /// Maps to <c>ProtocolGame::parsePendingGame</c>.
    /// Task T44.
    /// </summary>
    public event Action? PendingGameReceived;

    /// <summary>
    /// Raised when the server confirms the player has fully entered the game world
    /// (<c>GameServerEnterGame</c> = 0x0F).
    /// No parameters.
    /// Maps to <c>ProtocolGame::parseEnterGame</c>.
    /// Task T44.
    /// </summary>
    public event Action? EnterGameReceived;

    /// <summary>
    /// Raised when the server ends the game session (<c>GameServerSessionEnd</c> = 0x18).
    /// Parameter: reason byte.
    /// Maps to <c>ProtocolGame::parseSessionEnd</c>.
    /// Task T44.
    /// </summary>
    public event Action<byte>? SessionEndReceived;

    /// <summary>
    /// Raised when the server sends GM action permissions (<c>GameServerGMActions</c> = 0x0B).
    /// Parameter: array of 20 action-permission bytes.
    /// Maps to <c>ProtocolGame::parseGMActions → Game::processGMActions</c>.
    /// Task T44.
    /// </summary>
    public event Action<byte[]>? GMActionsUpdated;

    /// <summary>
    /// Raised when the server requests a client update (<c>GameServerUpdateNeeded</c> = 0x11).
    /// Parameter: signature string.
    /// Maps to <c>ProtocolGame::parseUpdateNeeded → Game::processUpdateNeeded</c>.
    /// Task T44.
    /// </summary>
    public event Action<string>? UpdateNeededReceived;


    public event Action? GameEntered;

    /// <summary>Player character died.</summary>
    public event Action? PlayerDied;

    /// <summary>A text message was received from the server.</summary>
    public event Action<byte, string>? TextMessageReceived;

    /// <summary>
    /// Raised when the server opens an editable text window (opcode 0x96, T23).
    /// Parameters: (id, itemId, maxLength, text, writer, date)
    /// </summary>
    public event Action<uint, int, ushort, string, string, string>? EditTextReceived;

    /// <summary>
    /// Raised when the server opens an editable list window (opcode 0x97, T23).
    /// Parameters: (id, doorId, text)
    /// </summary>
    public event Action<uint, byte, string>? EditListReceived;

    /// <summary>Raised when the server sends the quest log (opcode 0xF0, T23).</summary>
    public event Action<IReadOnlyList<Game.QuestEntry>>? QuestLogReceived;

    /// <summary>Raised when the server sends quest mission details (opcode 0xF1, T23).</summary>
    public event Action<ushort, IReadOnlyList<Game.QuestMission>>? QuestLineReceived;

    /// <summary>Raised when the server opens a modal dialog (opcode 0xFA, T23).</summary>
    public event Action<Game.ModalDialog>? ModalDialogReceived;

    // ─── Market events (T26) ──────────────────────────────────────────────────

    /// <summary>
    /// Raised when <c>parseMarketEnter</c> is received.
    /// Parameters: (depotItems, activeOffers)
    /// Maps to <c>onMarketEnter</c> Lua callback.
    /// </summary>
    public event Action<IReadOnlyList<Game.MarketDepotItem>, byte>? MarketEntered;

    /// <summary>Raised when the server sends <c>MarketLeave</c>.</summary>
    public event Action? MarketLeft;

    /// <summary>
    /// Raised when <c>parseMarketDetail</c> is received.
    /// Parameters: (itemId, itemTier, descriptions{attr→value}, purchaseStats, saleStats)
    /// </summary>
    public event Action<ushort, byte, IReadOnlyDictionary<int, string>, IReadOnlyList<Game.MarketStatEntry>, IReadOnlyList<Game.MarketStatEntry>>? MarketDetailReceived;

    /// <summary>
    /// Raised when <c>parseMarketBrowse</c> is received.
    /// Parameters: (var, offers)
    /// </summary>
    public event Action<ushort, IReadOnlyList<Game.MarketOffer>>? MarketBrowseReceived;

    // ─── T27 events (Prey / Forge / Bestiary / Wheel / Imbuement) ────────────

    /// <summary>Raised when <c>parsePreyData</c> is received. Parameter: populated prey slot data.</summary>
    public event Action<Game.PreyData>? PreyDataReceived;

    /// <summary>Raised when <c>parsePreyFreeRerolls</c> is received. Parameters: (slot, timeLeft).</summary>
    public event Action<byte, ushort>? PreyFreeRerollsReceived;

    /// <summary>Raised when <c>parsePreyTimeLeft</c> is received. Parameters: (slot, timeLeft).</summary>
    public event Action<byte, ushort>? PreyTimeLeftReceived;

    /// <summary>Raised when <c>parsePreyRerollPrice</c> is received. Parameters: (price, wildCardPrice).</summary>
    public event Action<uint, uint>? PreyRerollPriceReceived;

    /// <summary>Raised when <c>parseForgeResult</c> is received.</summary>
    public event Action<Game.ForgeResult>? ForgeResultReceived;

    /// <summary>Raised when <c>parseBestiaryRaces</c> is received.</summary>
    public event Action<IReadOnlyList<Game.BestiaryRace>>? BestiaryRacesReceived;

    /// <summary>Raised when <c>parseBestiaryOverview</c> is received. Parameters: (raceName, monsters, animusMasteryPoints).</summary>
    public event Action<string, IReadOnlyList<Game.BestiaryMonster>, ushort>? BestiaryOverviewReceived;

    /// <summary>Raised when <c>parseBestiaryMonsterData</c> is received.</summary>
    public event Action<Game.BestiaryMonsterData>? BestiaryMonsterDataReceived;

    /// <summary>Raised when <c>parseImbuementDurations</c> is received.</summary>
    public event Action<IReadOnlyList<Game.ImbuementTrackerItem>>? ImbuementDurationsReceived;

    /// <summary>Raised when <c>parseOpenWheelWindow</c> is received.</summary>
    public event Action<Game.WheelData>? WheelWindowReceived;

    // ─── T28 events ───────────────────────────────────────────────────────────

    /// <summary>Raised when <c>parseCoinBalance</c> is received.</summary>
    public event Action<Game.CoinBalance>? CoinBalanceReceived;

    /// <summary>Raised when <c>parseStore</c> category list is received.</summary>
    public event Action<IReadOnlyList<Game.StoreCategory>>? StoreCategoriesReceived;

    /// <summary>Raised when <c>parseStoreOffers</c> is received. Parameters: (categoryName, offers).</summary>
    public event Action<string, IReadOnlyList<Game.StoreOffer>>? StoreOffersReceived;

    /// <summary>Raised when <c>parseCompleteStorePurchase</c> is received.</summary>
    public event Action<Game.StorePurchaseResult>? StorePurchaseCompleted;

    /// <summary>Raised when a store error packet is received. Parameters: (errorType, message).</summary>
    public event Action<byte, string>? StoreErrorReceived;

    /// <summary>
    /// Raised when the server sends updated player stat values.
    /// Parameters: (health, maxHealth, mana, maxMana, freeCapacity, experience,
    ///              level, levelPercent, stamina, soul, regenerationTime, offlineTrainingTime)
    /// Task T42.
    /// </summary>
    public event Action<int, int, int, int, int, ulong, int, int, int, int, int, int>? PlayerStatsUpdated;

    /// <summary>
    /// Raised when the server sends updated player skill values.
    /// Parameters: (magicLevel, baseMagicLevel, magicLevelPercent, levels[], baseLevels[], percents[])
    /// Task T42.
    /// </summary>
    public event Action<int, int, int, int[], int[], int[]>? PlayerSkillsUpdated;

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

    // ─── Creature-state events (T45) ──────────────────────────────────────────

    /// <summary>
    /// Raised when the server updates the passable/unpassable state of a creature.
    /// Parameters: (creatureId, isUnpassable — true means the tile is blocked).
    /// Maps to <c>ProtocolGame::parseCreatureUnpass</c>.
    /// Task T45.
    /// </summary>
    public event Action<uint, bool>? CreatureUnpassUpdated;

    /// <summary>
    /// Raised when the server sends the number of party/helper members for a player.
    /// Parameters: (creatureId, helpersCount).
    /// Maps to <c>ProtocolGame::parsePlayerHelpers</c>.
    /// Task T45.
    /// </summary>
    public event Action<uint, ushort>? PlayerHelpersReceived;

    /// <summary>
    /// Raised when the server sends a creature's type (player/monster/NPC/summon).
    /// Parameters: (creatureId, creatureType — byte matching <c>Otc::CreatureType</c>).
    /// Maps to <c>ProtocolGame::parseCreatureType</c>.
    /// Task T45.
    /// </summary>
    public event Action<uint, byte>? CreatureTypeUpdated;

    /// <summary>
    /// Raised when the server updates a creature's chat-bubble (typing) state.
    /// Parameters: (creatureId, isTyping).
    /// Maps to <c>ProtocolGame::parseCreatureTyping</c>.
    /// Task T45.
    /// </summary>
    public event Action<uint, bool>? CreatureTypingUpdated;

    /// <summary>
    /// Raised when the server attaches a visual effect to a creature.
    /// Parameters: (creatureId, attachedEffectId).
    /// Maps to <c>ProtocolGame::parseAttachedEffect</c>.
    /// Task T45.
    /// </summary>
    public event Action<uint, ushort>? CreatureEffectAttached;

    /// <summary>
    /// Raised when the server removes a visual effect from a creature.
    /// Parameters: (creatureId, attachedEffectId).
    /// Maps to <c>ProtocolGame::parseDetachEffect</c>.
    /// Task T45.
    /// </summary>
    public event Action<uint, ushort>? CreatureEffectDetached;

    /// <summary>
    /// Raised when the server sets a GLSL shader on a creature.
    /// Parameters: (creatureId, shaderName — empty string to clear).
    /// Maps to <c>ProtocolGame::parseCreatureShader</c>.
    /// Task T45.
    /// </summary>
    public event Action<uint, string>? CreatureShaderChanged;

    /// <summary>
    /// Raised when the server sets a GLSL shader on the map view.
    /// Parameters: shaderName — empty string to clear.
    /// Maps to <c>ProtocolGame::parseMapShader</c>.
    /// Task T45.
    /// </summary>
    public event Action<string>? MapShaderChanged;

    /// <summary>
    /// Raised after the map central position changes due to a floor change.
    /// Parameters: (newPosition, oldPosition).
    /// Maps to <c>ProtocolGame::parseFloorChangeUp</c> and
    /// <c>ProtocolGame::parseFloorChangeDown</c>.
    /// Task T45.
    /// </summary>
    public event Action<Game.Position, Game.Position>? FloorChanged;

    // ─── Player-state events (T46) ────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends a <c>Blessings</c> (0x9C) packet.
    /// Parameters: blessings bitmask, visual state (0=hidden, 1=disabled, 2=normal, 3=green).
    /// Maps to <c>ProtocolGame::parseBlessings</c>.
    /// Task T46.
    /// </summary>
    public event Action<uint, byte>? BlessingsChanged;

    /// <summary>
    /// Raised when the server sends a <c>SpellCooldown</c> (0xA4) packet.
    /// Parameters: spellId, delayMs.
    /// Maps to <c>ProtocolGame::parseSpellCooldown</c>.
    /// Task T46.
    /// </summary>
    public event Action<ushort, uint>? SpellCooldownReceived;

    /// <summary>
    /// Raised when the server sends a <c>SpellGroupCooldown</c> (0xA5) packet.
    /// Parameters: groupId, delayMs.
    /// Maps to <c>ProtocolGame::parseSpellGroupCooldown</c>.
    /// Task T46.
    /// </summary>
    public event Action<byte, uint>? SpellGroupCooldownReceived;

    /// <summary>
    /// Raised when the server sends a <c>MultiUseCooldown</c> (0xA6) packet.
    /// Parameter: delayMs.
    /// Maps to <c>ProtocolGame::parseMultiUseCooldown</c>.
    /// Task T46.
    /// </summary>
    public event Action<uint>? MultiUseCooldownReceived;

    /// <summary>
    /// Raised when the server sends an <c>OpenOwnChannel</c> (0xB2) packet.
    /// Parameters: channelId, channelName.
    /// Maps to <c>ProtocolGame::parseOpenOwnPrivateChannel</c>.
    /// Task T46.
    /// </summary>
    public event Action<ushort, string>? OwnPrivateChannelOpened;

    /// <summary>
    /// Raised when the server sends a <c>PvpSituations</c> (0xB8) packet.
    /// Parameter: open PvP situation count.
    /// Maps to <c>ProtocolGame::parsePvpSituations</c>.
    /// Task T46.
    /// </summary>
    public event Action<byte>? PvpSituationsChanged;

    /// <summary>
    /// Raised when the server sends a <c>ResourceBalance</c> (0xEE) packet.
    /// Parameters: resource type byte, value.
    /// Maps to <c>ProtocolGame::parseResourceBalance</c>.
    /// Task T46.
    /// </summary>
    public event Action<byte, ulong>? ResourceBalanceChanged;

    /// <summary>
    /// Raised when the server sends a <c>WorldTime</c> (0xEF) packet.
    /// Parameters: hour, minute.
    /// Maps to <c>ProtocolGame::parseWorldTime</c>.
    /// Task T46.
    /// </summary>
    public event Action<byte, byte>? WorldTimeChanged;

    // ─── T47 events (world/creature light, effects, player info, walk wait) ───

    /// <summary>
    /// Raised when the server sends a <c>WorldLight</c> (0x82) packet.
    /// Parameters: intensity, color.
    /// Maps to <c>ProtocolGame::parseWorldLight</c>.
    /// Task T47.
    /// </summary>
    public event Action<byte, byte>? WorldLightChanged;

    /// <summary>
    /// Raised for each <c>MAGIC_EFFECTS_CREATE_EFFECT</c> entry inside a
    /// <c>GraphicalEffect</c> (0x83) packet.
    /// Parameters: position, effectId.
    /// Maps to <c>ProtocolGame::parseMagicEffect</c>.
    /// Task T47.
    /// </summary>
    public event Action<Game.Position, ushort>? MagicEffectReceived;

    /// <summary>
    /// Raised when the server sends an <c>AnimatedText</c> (0x84) packet.
    /// Parameters: position, color, text.
    /// Maps to <c>ProtocolGame::parseAnimatedText</c>.
    /// Task T47.
    /// </summary>
    public event Action<Game.Position, byte, string>? AnimatedTextReceived;

    /// <summary>
    /// Raised when the server sends a <c>DistanceMissile</c> (0x85) packet.
    /// Parameters: fromPosition, toPosition, shotId.
    /// Maps to <c>ProtocolGame::parseDistanceMissile</c>.
    /// Task T47.
    /// </summary>
    public event Action<Game.Position, Game.Position, ushort>? DistanceMissileReceived;

    /// <summary>
    /// Raised when the server sends a <c>CreatureLight</c> (0x8D) packet.
    /// Parameters: creatureId, intensity, color.
    /// Maps to <c>ProtocolGame::parseCreatureLight</c>.
    /// Task T47.
    /// </summary>
    public event Action<uint, byte, byte>? CreatureLightUpdated;

    /// <summary>
    /// Raised when the server sends a <c>PlayerInfo</c> (0x9F) packet
    /// with the basic player information (premium, vocation, spells).
    /// Parameters: isPremium, vocation, spells.
    /// Maps to <c>ProtocolGame::parsePlayerInfo</c>.
    /// Task T47.
    /// </summary>
    public event Action<bool, byte, IReadOnlyList<ushort>>? PlayerInfoReceived;

    /// <summary>
    /// Raised when the server sends a <c>ClearTarget</c> (0xA3) packet
    /// cancelling the current attack.
    /// Parameters: sequence number.
    /// Maps to <c>ProtocolGame::parsePlayerCancelAttack</c>.
    /// Task T47.
    /// </summary>
    public event Action<uint>? AttackCancelReceived;

    /// <summary>
    /// Raised when the server sends a <c>WalkWait</c> (0xB6) packet
    /// indicating the client should delay walking.
    /// Parameters: delay in milliseconds.
    /// Maps to <c>ProtocolGame::parseWalkWait</c>.
    /// Task T47.
    /// </summary>
    public event Action<ushort>? WalkWaitReceived;

    // ─── T48 events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends a <c>BugReport</c> (0x1A) packet.
    /// Parameters: canReportBugs.
    /// Maps to <c>ProtocolGame::parseBugReport</c>.
    /// Task T48.
    /// </summary>
    public event Action<bool>? BugReportReceived;

    /// <summary>
    /// Raised when the server sends a <c>Trappers</c> (0x87) packet
    /// listing creature IDs that are currently trappers.
    /// Parameters: list of creature IDs.
    /// Maps to <c>ProtocolGame::parseTrappers</c>.
    /// Task T48.
    /// </summary>
    public event Action<IReadOnlyList<uint>>? TrappersReceived;

    /// <summary>
    /// Raised when the server sends a <c>CloseForgeWindow</c> (0x89) packet.
    /// Maps to <c>ProtocolGame::parseCloseForgeWindow</c>.
    /// Task T48.
    /// </summary>
    public event Action? ForgeWindowClosed;

    /// <summary>
    /// Raised when the server sends a <c>RestingAreaState</c> (0xA9) packet.
    /// Parameters: (zone, state, message).
    /// Maps to <c>ProtocolGame::parseRestingAreaState</c>.
    /// Task T48.
    /// </summary>
    public event Action<byte, byte, string>? RestingAreaStateReceived;

    /// <summary>
    /// Raised when the server sends an <c>UnjustifiedStats</c> (0xB7) packet.
    /// Parameters: <see cref="OTClient.Framework.Game.UnjustifiedStats"/>.
    /// Maps to <c>ProtocolGame::parseUnjustifiedStats</c>.
    /// Task T48.
    /// </summary>
    public event Action<UnjustifiedStats>? UnjustifiedStatsReceived;

    /// <summary>
    /// Raised when the server sends a <c>TutorialHint</c> (0xDC) packet.
    /// Parameters: hint ID.
    /// Maps to <c>ProtocolGame::parseTutorialHint</c>.
    /// Task T48.
    /// </summary>
    public event Action<byte>? TutorialHintReceived;

    /// <summary>
    /// Raised when the server sends an <c>AutomapFlag</c> (0xDD) packet.
    /// Parameters: (position, icon, description, remove).
    /// Maps to <c>ProtocolGame::parseAutomapFlag</c>.
    /// Task T48.
    /// </summary>
    public event Action<Position, byte, string, bool>? AutomapFlagReceived;

    /// <summary>
    /// Raised when the server sends a <c>ChannelEvent</c> (0xF3) packet.
    /// Parameters: (channelId, channelName, eventType).
    /// Maps to <c>ProtocolGame::parseChannelEvent</c>.
    /// Task T48.
    /// </summary>
    public event Action<ushort, string, byte>? ChannelEventReceived;

    // ─── T49 events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends a <c>SetStoreDeepLink</c> (0xA8) packet.
    /// Parameters: serviceType (discarded by client but forwarded here).
    /// Maps to <c>ProtocolGame::parseSetStoreDeepLink</c>.
    /// Task T49.
    /// </summary>
    public event Action<byte>? StoreDeepLinkReceived;

    /// <summary>
    /// Raised when the server sends a <c>ChangeMapAwareRange</c> (0x33) packet
    /// updating the client's visible map area.
    /// Parameters: (xRange, yRange).
    /// Maps to <c>ProtocolGame::parseChangeMapAwareRange</c>.
    /// Task T49.
    /// </summary>
    public event Action<byte, byte>? MapAwareRangeChanged;

    /// <summary>
    /// Raised when the server sends a <c>DailyRewardCollectionState</c> (0xDE) packet.
    /// Parameters: state (0 = not collected, 1 = collected).
    /// Maps to <c>ProtocolGame::parseDailyRewardCollectionState</c>.
    /// Task T49.
    /// </summary>
    public event Action<byte>? DailyRewardCollectionStateReceived;

    /// <summary>
    /// Raised when the server sends an <c>OpenRewardWall</c> (0xE2) packet.
    /// Parameters: (bonusShrine, nextRewardTime, dayStreakDay, wasDailyRewardTaken,
    ///   errorMessage, tokens, timeLeft, dayStreakLevel).
    /// Maps to <c>ProtocolGame::parseOpenRewardWall</c>.
    /// Task T49.
    /// </summary>
    public event Action<byte, uint, byte, byte, string, ushort, uint, ushort>? RewardWallOpened;

    /// <summary>
    /// Raised when the server sends a <c>DailyReward</c> (0xE4) packet.
    /// Parameters: <see cref="OTClient.Framework.Game.DailyRewardData"/>.
    /// Maps to <c>ProtocolGame::parseDailyReward</c>.
    /// Task T49.
    /// </summary>
    public event Action<Game.DailyRewardData>? DailyRewardReceived;

    /// <summary>
    /// Raised when the server sends a <c>RewardHistory</c> (0xE5) packet.
    /// Parameters: list of (timestamp, isPremium, description, dayStreak) tuples.
    /// Maps to <c>ProtocolGame::parseRewardHistory</c>.
    /// Task T49.
    /// </summary>
    public event Action<IReadOnlyList<(uint Timestamp, bool IsPremium, string Description, ushort DayStreak)>>? RewardHistoryReceived;

    /// <summary>
    /// Raised when the server sends a <c>LootContainers</c> (0xC0) packet
    /// describing the quick-loot container configuration.
    /// Parameters: (quickLootFallback, list of (categoryType, lootContainerId, obtainerContainerId)).
    /// Maps to <c>ProtocolGame::parseLootContainers</c>.
    /// Task T49.
    /// </summary>
    public event Action<bool, IReadOnlyList<(byte CategoryType, ushort LootContainerId, ushort ObtainerContainerId)>>? LootContainersReceived;

    // ─── T50 events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends an <c>ExtendedOpcode</c> (0x32) packet.
    /// Parameters: (opcode, buffer). OTClient extension — opcode 0 enables
    /// extended-opcode send, opcode 2 is a ping-back.
    /// Maps to <c>ProtocolGame::parseExtendedOpcode</c>.
    /// Task T50.
    /// </summary>
    public event Action<byte, string>? ExtendedOpcodeReceived;

    /// <summary>
    /// Raised when the server sends a <c>TakeScreenshot</c> (0x75) packet.
    /// Parameter: screenshotType byte.
    /// Maps to <c>ProtocolGame::parseTakeScreenshot</c>.
    /// Task T50.
    /// </summary>
    public event Action<byte>? TakeScreenshotReceived;

    /// <summary>
    /// Raised when the server sends a <c>SendGameNews</c> (0x98) packet.
    /// Parameters: (categoryId, pageNumber).
    /// Maps to <c>ProtocolGame::parseGameNews</c>.
    /// Task T50.
    /// </summary>
    public event Action<uint, byte>? GameNewsReceived;

    /// <summary>
    /// Raised when the server sends a <c>Preset</c> (0x9D) packet.
    /// Parameter: preset value (U32).
    /// Maps to <c>ProtocolGame::parsePreset</c>.
    /// Task T50.
    /// </summary>
    public event Action<uint>? PresetReceived;

    /// <summary>
    /// Raised when the server sends a <c>PremiumTrigger</c> (0x9E) packet.
    /// Parameter: array of trigger-type bytes.
    /// Maps to <c>ProtocolGame::parsePremiumTrigger</c>.
    /// Task T50.
    /// </summary>
    public event Action<IReadOnlyList<byte>>? PremiumTriggerReceived;

    /// <summary>
    /// Raised when the server sends a <c>RuleViolationChannel</c> (0xAE) packet.
    /// Parameter: channelId.
    /// Maps to <c>ProtocolGame::parseRuleViolationChannel</c>.
    /// Task T50.
    /// </summary>
    public event Action<ushort>? RuleViolationChannelReceived;

    /// <summary>
    /// Raised when the server sends a <c>RuleViolationRemove</c> (0xAF) packet.
    /// At protocol 1281 (≥1200) this carries experience-tracker data:
    /// (rawExp, finalExp).
    /// Maps to <c>ProtocolGame::parseExperienceTracker</c>.
    /// Task T50.
    /// </summary>
    public event Action<long, long>? ExperienceTrackerReceived;

    /// <summary>
    /// Raised when the server sends a <c>RuleViolationCancel</c> (0xB0) packet.
    /// Parameter: reporter name.
    /// Maps to <c>ProtocolGame::parseRuleViolationCancel</c>.
    /// Task T50.
    /// </summary>
    public event Action<string>? RuleViolationCancelReceived;

    /// <summary>
    /// Raised when the server sends a <c>RuleViolationLock</c> (0xB1) packet.
    /// No payload at protocol 1281 (&lt;1310).
    /// Maps to <c>ProtocolGame::parseRuleViolationLock</c>.
    /// Task T50.
    /// </summary>
    public event Action? RuleViolationLockReceived;

    // ─── T51 events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends a <c>SupplyStash</c> (0x29) packet.
    /// Parameters: list of stash items; free slots (only at protocol &lt;1410).
    /// Maps to <c>ProtocolGame::parseSupplyStash</c>.
    /// Task T51.
    /// </summary>
    public event Action<IReadOnlyList<SupplyStashItem>>? SupplyStashReceived;

    /// <summary>
    /// Raised when the server sends a <c>SpecialContainer</c> (0x2A) packet.
    /// Parameters: (supplyStashAvailable, isMarketAvailable).
    /// Maps to <c>ProtocolGame::parseSpecialContainer</c>.
    /// Task T51.
    /// </summary>
    public event Action<byte, byte>? SpecialContainerReceived;

    /// <summary>
    /// Raised when the server sends a <c>PartyAnalyzer</c> (0x2B) packet.
    /// Maps to <c>ProtocolGame::parsePartyAnalyzer</c>.
    /// Task T51.
    /// </summary>
    public event Action<PartyAnalyzerData>? PartyAnalyzerReceived;

    /// <summary>
    /// Raised when the server attaches a paperdoll to a creature (0x3C).
    /// Parameters: (creatureId, paperdoll data).
    /// Maps to <c>ProtocolGame::parseAttachedPaperdoll</c>.
    /// Task T51.
    /// </summary>
    public event Action<uint, PaperdollAttachData>? PaperdollAttachedReceived;

    /// <summary>
    /// Raised when the server detaches a paperdoll from a creature (0x3D).
    /// Parameters: (creatureId, bySlot, idOrSlot).
    /// Maps to <c>ProtocolGame::parseDetachPaperdoll</c>.
    /// Task T51.
    /// </summary>
    public event Action<uint, bool, ushort>? PaperdollDetachedReceived;

    /// <summary>
    /// Raised when the server sends feature flags (0x43).
    /// Parameter: list of (featureId, enabled) pairs.
    /// Maps to <c>ProtocolGame::parseFeatures</c>.
    /// Task T51.
    /// </summary>
    public event Action<IReadOnlyList<(byte FeatureId, bool Enabled)>>? FeaturesReceived;

    /// <summary>
    /// Raised when the server sends weapon proficiency experience (0x5C).
    /// Parameters: (itemId, experience).
    /// Maps to <c>ProtocolGame::parseWeaponProficiencyExperience</c>.
    /// Task T51.
    /// </summary>
    public event Action<ushort, uint>? WeaponProficiencyExpReceived;

    /// <summary>
    /// Raised when the server sends a passive cooldown update (0x5E) with type 0
    /// (running timer).
    /// Parameters: (currentCooldown, maxCooldown, canDecay).
    /// Maps to <c>ProtocolGame::parsePassiveCooldown</c>.
    /// Task T51.
    /// </summary>
    public event Action<uint, uint, bool>? PassiveCooldownReceived;

    /// <summary>
    /// Raised when the server sends bosstriary kill-threshold data (0x61).
    /// Maps to <c>ProtocolGame::parseBosstiaryData</c>.
    /// Task T51.
    /// </summary>
    public event Action<BosstiaryKillThresholds>? BosstiaryDataReceived;

    // ─── T52 events ──────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends bosstriary info entries (0x73).
    /// Parameter: list of <see cref="BosstiaryEntry"/> records.
    /// Maps to <c>ProtocolGame::parseBosstiaryInfo</c>.
    /// Task T52.
    /// </summary>
    public event Action<IReadOnlyList<BosstiaryEntry>>? BosstiaryInfoReceived;

    /// <summary>
    /// Raised when the server sends bosstriary slot data (0x62).
    /// Parameter: fully parsed <see cref="BosstiarySlotsData"/> object.
    /// Maps to <c>ProtocolGame::parseBosstiarySlots</c>.
    /// Task T52.
    /// </summary>
    public event Action<BosstiarySlotsData>? BosstiarySlotReceived;

    /// <summary>
    /// Raised when the server sends bosstriary cooldown timers (0xBD).
    /// Parameter: list of (BossId, CooldownSeconds) tuples.
    /// Maps to <c>ProtocolGame::parseBosstiaryCooldownTimer</c>.
    /// Task T52.
    /// </summary>
    public event Action<IReadOnlyList<(uint BossId, ulong CooldownSeconds)>>? BosstiaryCooldownTimerReceived;

    /// <summary>
    /// Raised when the server notifies that a bestiary entry changed (0xD9).
    /// Parameter: monster ID.
    /// Maps to <c>ProtocolGame::parseBestiaryEntryChanged</c>.
    /// Task T52.
    /// </summary>
    public event Action<ushort>? BestiaryEntryChangedReceived;

    /// <summary>
    /// Raised when the server sends impact tracker data (0xCC).
    /// Parameters: analyzerType (0=heal,1=deal,2=recv), amount, effect, target.
    /// Maps to <c>ProtocolGame::parseUpdateImpactTracker</c>.
    /// Task T52.
    /// </summary>
    public event Action<byte, uint, byte, string>? ImpactTrackerReceived;

    /// <summary>
    /// Raised when the server sends a supply tracker update (0xCE).
    /// Parameter: item client ID.
    /// Maps to <c>ProtocolGame::parseUpdateSupplyTracker</c>.
    /// Task T52.
    /// </summary>
    public event Action<ushort>? SupplyTrackerReceived;

    /// <summary>
    /// Raised when the server sends a loot tracker update (0xCF).
    /// Parameters: the looted item and creature/monster name.
    /// Maps to <c>ProtocolGame::parseUpdateLootTracker</c>.
    /// Task T52.
    /// </summary>
    public event Action<Game.Item, string>? LootTrackerReceived;

    /// <summary>
    /// Raised when the server sends a quest tracker update (0xD0).
    /// Maps to <c>ProtocolGame::parseQuestTracker</c>.
    /// Task T52.
    /// </summary>
    public event Action<byte, IReadOnlyList<(ushort QuestId, ushort MissionId, string QuestName, string MissionName, string MissionDesc)>>? QuestTrackerReceived;

    /// <summary>
    /// Raised when the server sends kill tracker data (0xD1).
    /// Parameters: monsterName, outfit, list of dropped items.
    /// Maps to <c>ProtocolGame::parseKillTracker</c>.
    /// Task T52.
    /// </summary>
    public event Action<string, Game.Outfit, IReadOnlyList<Game.Item>>? KillTrackerReceived;

    /// <summary>
    /// Raised when the server sends item information (0xF4).
    /// Parameter: list of (ItemId, SubType, Description) tuples.
    /// Maps to <c>ProtocolGame::parseItemInfo</c>.
    /// Task T52.
    /// </summary>
    public event Action<IReadOnlyList<(ushort ItemId, byte SubType, string Description)>>? ItemInfoReceived;

    /// <summary>
    /// Raised when the server sends the player's full inventory counts (0xF5).
    /// Parameter: list of (ItemId, Tier, Amount) tuples.
    /// Maps to <c>ProtocolGame::parsePlayerInventory</c>.
    /// Task T52.
    /// </summary>
    public event Action<IReadOnlyList<(ushort ItemId, byte Tier, uint Amount)>>? PlayerInventoryReceived;

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

    // ─── T53 events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends forge history (0x88).
    /// Parameters: (pageNumber, lastPage, list of history entries).
    /// Maps to <c>ProtocolGame::parseBrowseForgeHistory</c>.
    /// Task T53.
    /// </summary>
    public event Action<ushort, ushort, IReadOnlyList<ForgeHistoryEntry>>? ForgeHistoryReceived;

    /// <summary>
    /// Raised when the server sends bless dialog data (0x9B).
    /// Maps to <c>ProtocolGame::parseBlessDialog</c>.
    /// Task T53.
    /// </summary>
    public event Action<BlessDialogData>? BlessDialogReceived;

    /// <summary>
    /// Raised when the server sends bestiary tracker data (0xB9).
    /// Parameters: (trackerType, list of tracker entries).
    /// trackerType: 0 = bestiary, 1 = boss (only at protocol ≥ 1320).
    /// Maps to <c>ProtocolGame::parseBestiaryTracker</c>.
    /// Task T53.
    /// </summary>
    public event Action<byte, IReadOnlyList<(ushort RaceId, uint KillCount, ushort FirstUnlock, ushort SecondUnlock, ushort LastUnlock, byte Status)>>? BestiaryTrackerReceived;

    /// <summary>
    /// Raised when the server sends task hunting slot data (0xBB).
    /// Parameters: (slot, state, nextFreeRoll, optional creature list for state 2/3,
    ///   optional active-task fields for state 4/5).
    /// Maps to <c>ProtocolGame::parseTaskHuntingData</c>.
    /// Task T53.
    /// </summary>
    public event Action<byte, byte, uint, IReadOnlyList<(ushort RaceId, bool Unlocked)>, ushort, ushort, ushort, byte, ushort>? TaskHuntingDataReceived;

    /// <summary>
    /// Raised when the server sends monk vocation data (0xC1).
    /// Parameters: (subtype, value).  subtype 0=harmony, 1=serene, 2=virtue.
    /// Maps to <c>ProtocolGame::parseMonkData</c>.
    /// Task T53.
    /// </summary>
    public event Action<byte, byte>? MonkDataReceived;

    /// <summary>
    /// Raised when the server sends a house auction message (0xC3).
    /// Parameters: (houseId, type, index).
    /// Maps to <c>ProtocolGame::parseCyclopediaHouseAuctionMessage</c>.
    /// Task T53.
    /// </summary>
    public event Action<uint, byte, byte>? HouseAuctionMessageReceived;

    /// <summary>
    /// Raised when the server sends weapon proficiency info (0xC4).
    /// Parameters: (itemId, experience, list of (proficiencyLevel, perkPosition) pairs).
    /// Maps to <c>ProtocolGame::parseWeaponProficiencyInfo</c>.
    /// Task T53.
    /// </summary>
    public event Action<ushort, uint, IReadOnlyList<(byte ProficiencyLevel, byte PerkPosition)>>? WeaponProficiencyInfoReceived;

    /// <summary>
    /// Raised when the server sends the outfit window data (0xC8).
    /// Maps to <c>ProtocolGame::parseOpenOutfitWindow</c>.
    /// Task T53.
    /// </summary>
    public event Action<OutfitWindowData>? OutfitWindowReceived;

    /// <summary>
    /// Raised when the server sends bestiary charms data (0xD8).
    /// Maps to <c>ProtocolGame::parseBestiaryCharmsData</c>.
    /// Task T53.
    /// </summary>
    public event Action<BestiaryCharmsData>? BestiaryCharmsDataReceived;

    // ─── T54 events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends cyclopedia item detail (0x76).
    /// Carries the item name and a list of (header, body) description pairs.
    /// Maps to <c>ProtocolGame::parseCyclopediaItemDetail</c>.
    /// Task T54.
    /// </summary>
    public event Action<string, IReadOnlyList<(string Header, string Body)>>? ItemDetailReceived;

    /// <summary>
    /// Raised when the server sends item class / forge tier price data (0x86).
    /// Carries a list of (ClassId, tiers) where each tier is (Tier, Price).
    /// Maps to <c>ProtocolGame::parseItemClasses</c>.
    /// Task T54.
    /// </summary>
    public event Action<IReadOnlyList<ForgeClassEntry>>? ItemClassesReceived;

    /// <summary>
    /// Raised when the server sends cyclopedia houses info (0xC6).
    /// Carries the main house id and the list of all known house ids.
    /// Maps to <c>ProtocolGame::parseCyclopediaHousesInfo</c>.
    /// Task T54.
    /// </summary>
    public event Action<uint, IReadOnlyList<uint>>? CyclopediaHousesInfoReceived;

    /// <summary>
    /// Raised when the server sends the cyclopedia house list (0xC7).
    /// Carries the list of house entries.
    /// Maps to <c>ProtocolGame::parseCyclopediaHouseList</c>.
    /// Task T54.
    /// </summary>
    public event Action<IReadOnlyList<CyclopediaHouseEntry>>? CyclopediaHouseListReceived;

    /// <summary>
    /// Raised when the server requests purchase data (0xE1).
    /// Carries the transaction id and product type.
    /// Maps to <c>ProtocolGame::parseRequestPurchaseData</c>.
    /// Task T54.
    /// </summary>
    public event Action<uint, byte>? RequestPurchaseDataReceived;

    /// <summary>
    /// Raised when the server sends a store offer description (0xEA).
    /// Carries the offer id and the description text.
    /// Maps to <c>ProtocolGame::parseShowDescription</c>.
    /// Task T54.
    /// </summary>
    public event Action<uint, string>? StoreOfferDescriptionReceived;

    /// <summary>
    /// Raised when the server closes the imbuement window (0xEC).
    /// Maps to <c>ProtocolGame::parseCloseImbuementWindow</c>.
    /// Task T54.
    /// </summary>
    public event Action? ImbuementWindowClosed;

    /// <summary>
    /// Raised when the server sends an error message (0xED).
    /// Carries the error code and the error message string.
    /// Maps to <c>ProtocolGame::parseError</c>.
    /// Task T54.
    /// </summary>
    public event Action<byte, string>? ServerErrorReceived;

    // ─── T55 events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the server sends a login challenge (0x1F).
    /// Carries the XTEA timestamp and the random byte; the receiver should send
    /// the login packet in response.
    /// Maps to <c>ProtocolGame::parseLoginChallenge</c>.
    /// Task T55.
    /// </summary>
    public event Action<uint, byte>? LoginChallengeReceived;

    /// <summary>
    /// Raised when the server sends a CyclopediaCharacterInfo packet (0xDA) with a
    /// non-zero error code (no character data follows).
    /// Task T55.
    /// </summary>
    public event Action<byte, byte>? CharacterInfoErrorReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_BASEINFORMATION</c> (type 0).
    /// Carries: name, vocationName, level, outfit, titleName.
    /// Task T55.
    /// </summary>
    public event Action<string, string, ushort, Game.Outfit, string>? CharacterBaseInfoReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_GENERALSTATS</c> (type 1).
    /// Carries: stats object, skill list, specialized-magic-level list.
    /// Task T55.
    /// </summary>
    public event Action<Game.CharacterGeneralStats, IReadOnlyList<Game.CharacterSkill>, IReadOnlyList<(byte Element, ushort Level)>>? CharacterGeneralStatsReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_RECENTDEATHS</c> (type 3).
    /// Carries a list of (timestamp, cause) tuples.
    /// Task T55.
    /// </summary>
    public event Action<IReadOnlyList<(uint Timestamp, string Cause)>>? CharacterRecentDeathsReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_RECENTPVPKILLS</c> (type 4).
    /// Carries a list of (timestamp, description, status) tuples.
    /// Task T55.
    /// </summary>
    public event Action<IReadOnlyList<(uint Timestamp, string Description, byte Status)>>? CharacterRecentPvPKillsReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_ACHIEVEMENTS</c> (type 5).
    /// No payload.
    /// Task T55.
    /// </summary>
    public event Action? CharacterAchievementsReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_ITEMSUMMARY</c> (type 6).
    /// Task T55.
    /// </summary>
    public event Action<Game.CharacterItemSummary>? CharacterItemSummaryReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_OUTFITSMOUNTS</c> (type 7).
    /// Task T55.
    /// </summary>
    public event Action<Game.CharacterOutfitsMounts>? CharacterOutfitsMountsReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_STORESUMMARY</c> (type 8).
    /// Task T55.
    /// </summary>
    public event Action<Game.CharacterStoreSummary>? CharacterStoreSummaryReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_INSPECTION</c> (type 9).
    /// No payload.
    /// Task T55.
    /// </summary>
    public event Action? CharacterInspectionReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_BADGES</c> (type 10).
    /// Carries: showAccountInfo, isOnline, isPremium, loyaltyTitle, badges list.
    /// Task T55.
    /// </summary>
    public event Action<bool, bool, bool, string, IReadOnlyList<Game.CharacterBadge>>? CharacterBadgesReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_TITLES</c> (type 11).
    /// Carries: current title index, titles list.
    /// Task T55.
    /// </summary>
    public event Action<byte, IReadOnlyList<Game.CharacterTitle>>? CharacterTitlesReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_OFFENCESTATS</c> (type 13).
    /// Task T55.
    /// </summary>
    public event Action<Game.CharacterOffenceStats>? CharacterOffenceStatsReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_DEFENCESTATS</c> (type 14).
    /// Task T55.
    /// </summary>
    public event Action<Game.CharacterDefenceStats>? CharacterDefenceStatsReceived;

    /// <summary>
    /// Raised for <c>CYCLOPEDIA_CHARACTERINFO_MISCSTATS</c> (type 15).
    /// Task T55.
    /// </summary>
    public event Action<Game.CharacterMiscStats>? CharacterMiscStatsReceived;

    // ─── T56 events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when a magic effect is removed from the map (proto≥1320 path of opcode 0x84).
    /// Carries: tile position, effect id.
    /// Task T56.
    /// </summary>
    public event Action<Game.Position, ushort>? MagicEffectRemoved;

    /// <summary>
    /// Raised when a timed creature-mark (square) is received (proto&lt;1281 path of opcode 0x86).
    /// Carries: creatureId, color.
    /// Task T56.
    /// </summary>
    public event Action<uint, byte>? CreatureMarkReceived;

    /// <summary>
    /// Raised when the forge window is opened (proto≥1281 path of opcode 0x87).
    /// Carries the full <see cref="Game.ForgeOpenData"/> payload.
    /// Task T56.
    /// </summary>
    public event Action<Game.ForgeOpenData>? ForgeWindowOpened;

    /// <summary>
    /// Raised when highscores data is received (proto≥1310 path of opcode 0xB1).
    /// Carries <see cref="Game.HighscoresData"/> or <c>null</c> when the list is empty.
    /// Task T56.
    /// </summary>
    public event Action<Game.HighscoresData?>? HighscoresReceived;

    // ─── Feature state (T56) ──────────────────────────────────────────────────

    private readonly HashSet<byte> _enabledFeatures = [];

    /// <summary>Returns <c>true</c> if the given feature id is currently enabled.</summary>
    internal bool HasFeature(byte featureId) => _enabledFeatures.Contains(featureId);

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

        // T44 login-flow handlers
        RegisterHandler((byte)GameServerPacket.LoginOrPendingState,   ParseLoginOrPendingState);
        RegisterHandler((byte)GameServerPacket.GMActions,             ParseGMActions);
        RegisterHandler((byte)GameServerPacket.ServerEnterGame,       ParseServerEnterGame);
        RegisterHandler((byte)GameServerPacket.UpdateNeeded,          ParseUpdateNeeded);
        RegisterHandler((byte)GameServerPacket.LoginSuccess,          ParseLoginSuccess);
        RegisterHandler((byte)GameServerPacket.SessionEnd,            ParseSessionEnd);
        RegisterHandler((byte)GameServerPacket.StoreButtonIndicators, ParseStoreButtonIndicators);
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
        RegisterHandler((byte)GameServerPacket.EditText,          ParseEditText);
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
        RegisterHandler((byte)GameServerPacket.EditList,          ParseEditList);
        RegisterHandler((byte)GameServerPacket.QuestLog,          ParseQuestLog);
        RegisterHandler((byte)GameServerPacket.QuestLine,         ParseQuestLine);
        RegisterHandler((byte)GameServerPacket.MarketEnter,       ParseMarketEnter);
        RegisterHandler((byte)GameServerPacket.MarketLeave,       ParseMarketLeave);
        RegisterHandler((byte)GameServerPacket.MarketDetail,      ParseMarketDetail);
        RegisterHandler((byte)GameServerPacket.MarketBrowse,      ParseMarketBrowse);
        RegisterHandler((byte)GameServerPacket.ModalDialog,       ParseModalDialog);

        // T27 handlers
        RegisterHandler((byte)GameServerPacket.ImbuementDurations, ParseImbuementDurations);
        RegisterHandler((byte)GameServerPacket.OpenWheelWindow,    ParseOpenWheelWindow);
        RegisterHandler((byte)GameServerPacket.ForgeResult,        ParseForgeResult);
        RegisterHandler((byte)GameServerPacket.BestiaryRaces,      ParseBestiaryRaces);
        RegisterHandler((byte)GameServerPacket.BestiaryOverview,   ParseBestiaryOverview);
        RegisterHandler((byte)GameServerPacket.BestiaryMonsterData,ParseBestiaryMonsterData);
        RegisterHandler((byte)GameServerPacket.PreyFreeRerolls,    ParsePreyFreeRerolls);
        RegisterHandler((byte)GameServerPacket.PreyTimeLeft,       ParsePreyTimeLeft);
        RegisterHandler((byte)GameServerPacket.PreyData,           ParsePreyData);
        RegisterHandler((byte)GameServerPacket.PreyRerollPrice,    ParsePreyRerollPrice);
        RegisterHandler((byte)GameServerPacket.ImbuementWindow,    ParseImbuementWindow);

        // T28 handlers
        RegisterHandler((byte)GameServerPacket.CoinBalance,             ParseCoinBalance);
        RegisterHandler((byte)GameServerPacket.StoreError,              ParseStoreError);
        RegisterHandler((byte)GameServerPacket.CoinBalanceUpdating,     ParseCoinBalanceUpdating);
        RegisterHandler((byte)GameServerPacket.Store,                   ParseStore);
        RegisterHandler((byte)GameServerPacket.StoreOffers,             ParseStoreOffers);
        RegisterHandler((byte)GameServerPacket.StoreTransactionHistory, ParseStoreTransactionHistory);
        RegisterHandler((byte)GameServerPacket.StoreCompletePurchase,   ParseCompleteStorePurchase);

        // T45 handlers
        RegisterHandler((byte)GameServerPacket.AttchedEffect,   ParseAttachedEffect);
        RegisterHandler((byte)GameServerPacket.DetachEffect,    ParseDetachEffect);
        RegisterHandler((byte)GameServerPacket.CreatureShader,  ParseCreatureShader);
        RegisterHandler((byte)GameServerPacket.MapShader,       ParseMapShader);
        RegisterHandler((byte)GameServerPacket.CreatureTyping,  ParseCreatureTyping);
        RegisterHandler((byte)GameServerPacket.CreatureUnpass,  ParseCreatureUnpass);
        RegisterHandler((byte)GameServerPacket.PlayerHelpers,   ParsePlayerHelpers);
        RegisterHandler((byte)GameServerPacket.CreatureType,    ParseCreatureType);
        RegisterHandler((byte)GameServerPacket.FloorChangeUp,   ParseFloorChangeUp);
        RegisterHandler((byte)GameServerPacket.FloorChangeDown, ParseFloorChangeDown);

        // T46 handlers
        RegisterHandler((byte)GameServerPacket.Blessings,          ParseBlessings);
        RegisterHandler((byte)GameServerPacket.SpellCooldown,      ParseSpellCooldown);
        RegisterHandler((byte)GameServerPacket.SpellGroupCooldown, ParseSpellGroupCooldown);
        RegisterHandler((byte)GameServerPacket.MultiUseCooldown,   ParseMultiUseCooldown);
        RegisterHandler((byte)GameServerPacket.OpenOwnChannel,     ParseOpenOwnPrivateChannel);
        RegisterHandler((byte)GameServerPacket.PvpSituations,      ParsePvpSituations);
        RegisterHandler((byte)GameServerPacket.ResourceBalance,    ParseResourceBalance);
        RegisterHandler((byte)GameServerPacket.WorldTime,          ParseWorldTime);

        // T47 handlers
        RegisterHandler((byte)GameServerPacket.WorldLight,        ParseWorldLight);
        RegisterHandler((byte)GameServerPacket.GraphicalEffect,   ParseMagicEffect);
        RegisterHandler((byte)GameServerPacket.AnimatedText,      ParseAnimatedText);
        RegisterHandler((byte)GameServerPacket.DistanceMissile,   ParseDistanceMissile);
        RegisterHandler((byte)GameServerPacket.CreatureLight,     ParseCreatureLight);
        RegisterHandler((byte)GameServerPacket.PlayerInfo,        ParsePlayerInfo);
        RegisterHandler((byte)GameServerPacket.ClearTarget,       ParsePlayerCancelAttack);
        RegisterHandler((byte)GameServerPacket.WalkWait,          ParseWalkWait);

        // T48
        RegisterHandler((byte)GameServerPacket.BugReport,         ParseBugReport);
        RegisterHandler((byte)GameServerPacket.Trappers,          ParseTrappers);
        RegisterHandler((byte)GameServerPacket.CloseForgeWindow,  ParseCloseForgeWindow);
        RegisterHandler((byte)GameServerPacket.RestingAreaState,  ParseRestingAreaState);
        RegisterHandler((byte)GameServerPacket.UnjustifiedStats,  ParseUnjustifiedStats);
        RegisterHandler((byte)GameServerPacket.TutorialHint,      ParseTutorialHint);
        RegisterHandler((byte)GameServerPacket.AutomapFlag,       ParseAutomapFlag);
        RegisterHandler((byte)GameServerPacket.ChannelEvent,      ParseChannelEvent);

        // T49
        RegisterHandler((byte)GameServerPacket.SetStoreDeepLink,           ParseSetStoreDeepLink);
        RegisterHandler((byte)GameServerPacket.ChangeMapAwareRange,        ParseChangeMapAwareRange);
        RegisterHandler((byte)GameServerPacket.LootContainers,             ParseLootContainers);
        RegisterHandler((byte)GameServerPacket.DailyRewardCollectionState, ParseDailyRewardCollectionState);
        RegisterHandler((byte)GameServerPacket.OpenRewardWall,             ParseOpenRewardWall);
        RegisterHandler((byte)GameServerPacket.DailyReward,               ParseDailyReward);
        RegisterHandler((byte)GameServerPacket.RewardHistory,             ParseRewardHistory);

        // T50
        RegisterHandler((byte)GameServerPacket.ExtendedOpcode,         ParseExtendedOpcode);
        RegisterHandler((byte)GameServerPacket.TakeScreenshot,         ParseTakeScreenshot);
        RegisterHandler((byte)GameServerPacket.SendGameNews,           ParseGameNews);
        RegisterHandler((byte)GameServerPacket.Preset,                 ParsePreset);
        RegisterHandler((byte)GameServerPacket.PremiumTrigger,         ParsePremiumTrigger);
        RegisterHandler((byte)GameServerPacket.RuleViolationChannel,   ParseRuleViolationChannel);
        RegisterHandler((byte)GameServerPacket.RuleViolationRemove,    ParseExperienceTracker);
        RegisterHandler((byte)GameServerPacket.RuleViolationCancel,    ParseRuleViolationCancel);
        RegisterHandler((byte)GameServerPacket.RuleViolationLock,      ParseRuleViolationLock);

        // T51
        RegisterHandler((byte)GameServerPacket.SupplyStash,            ParseSupplyStash);
        RegisterHandler((byte)GameServerPacket.SpecialContainer,       ParseSpecialContainer);
        RegisterHandler((byte)GameServerPacket.PartyAnalyzer,          ParsePartyAnalyzer);
        RegisterHandler((byte)GameServerPacket.AttachedPaperdoll,      ParseAttachedPaperdoll);
        RegisterHandler((byte)GameServerPacket.DetachPaperdoll,        ParseDetachPaperdoll);
        RegisterHandler((byte)GameServerPacket.Features,               ParseFeatures);
        RegisterHandler((byte)GameServerPacket.WeaponProficiencyExp,   ParseWeaponProficiencyExperience);
        RegisterHandler((byte)GameServerPacket.PassiveCooldown,        ParsePassiveCooldown);
        RegisterHandler((byte)GameServerPacket.BosstiaryData,          ParseBosstiaryData);
        RegisterHandler((byte)GameServerPacket.ClientCheck,            ParseClientCheck);

        // T52
        RegisterHandler((byte)GameServerPacket.BosstiarySlots,         ParseBosstiarySlots);
        RegisterHandler((byte)GameServerPacket.BosstiaryInfo,          ParseBosstiaryInfo);
        RegisterHandler((byte)GameServerPacket.BosstiaryCooldownTimer, ParseBosstiaryCooldownTimer);
        RegisterHandler((byte)GameServerPacket.BestiaryEntryChanged,   ParseBestiaryEntryChanged);
        RegisterHandler((byte)GameServerPacket.UpdateImpactTracker,    ParseUpdateImpactTracker);
        RegisterHandler((byte)GameServerPacket.ItemsPrice,             ParseItemsPrice);
        RegisterHandler((byte)GameServerPacket.UpdateSupplyTracker,    ParseUpdateSupplyTracker);
        RegisterHandler((byte)GameServerPacket.UpdateLootTracker,      ParseUpdateLootTracker);
        RegisterHandler((byte)GameServerPacket.QuestTracker,           ParseQuestTracker);
        RegisterHandler((byte)GameServerPacket.KillTracker,            ParseKillTracker);
        RegisterHandler((byte)GameServerPacket.ItemInfo,               ParseItemInfo);
        RegisterHandler((byte)GameServerPacket.PlayerInventory,        ParsePlayerInventory);

        // T53
        RegisterHandler((byte)GameServerPacket.BrowseForgeHistory,            ParseBrowseForgeHistory);
        RegisterHandler((byte)GameServerPacket.BlessDialog,                   ParseBlessDialog);
        RegisterHandler((byte)GameServerPacket.BestiaryRefreshTracker,        ParseBestiaryTracker);
        RegisterHandler((byte)GameServerPacket.TaskHuntingBasicData,          ParseTaskHuntingBasicData);
        RegisterHandler((byte)GameServerPacket.TaskHuntingData,               ParseTaskHuntingData);
        RegisterHandler((byte)GameServerPacket.MonkData,                      ParseMonkData);
        RegisterHandler((byte)GameServerPacket.CyclopediaHouseAuctionMessage, ParseCyclopediaHouseAuctionMessage);
        RegisterHandler((byte)GameServerPacket.WeaponProficiencyInfo,         ParseWeaponProficiencyInfo2);
        RegisterHandler((byte)GameServerPacket.ChooseOutfit,                  ParseChooseOutfit);
        RegisterHandler((byte)GameServerPacket.BestiaryCharmsData,            ParseBestiaryCharmsData);

        // T54
        RegisterHandler((byte)GameServerPacket.CyclopediaItemDetail, ParseCyclopediaItemDetail);
        RegisterHandler((byte)GameServerPacket.ItemClasses,          ParseItemClasses);
        RegisterHandler((byte)GameServerPacket.CyclopediaHousesInfo, ParseCyclopediaHousesInfo);
        RegisterHandler((byte)GameServerPacket.CyclopediaHouseList,  ParseCyclopediaHouseList);
        RegisterHandler((byte)GameServerPacket.RequestPurchaseData,  ParseRequestPurchaseData);
        RegisterHandler((byte)GameServerPacket.ShowDescription,      ParseShowDescription);
        RegisterHandler((byte)GameServerPacket.CloseImbuementWindow, ParseCloseImbuementWindow);
        RegisterHandler((byte)GameServerPacket.ServerError,          ParseServerError);

        // T55
        RegisterHandler((byte)GameServerPacket.Challenge,               ParseLoginChallenge);
        RegisterHandler((byte)GameServerPacket.CyclopediaCharacterInfo, ParseCyclopediaCharacterInfo);
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
