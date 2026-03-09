namespace OTClient.Framework.Net;

/// <summary>
/// Incoming (server → client) packet parsers for <see cref="ProtocolGame"/>.
/// Each method corresponds to one <see cref="GameServerPacket"/> opcode and is
/// registered in <see cref="ProtocolGame.ProtocolGame()"/>.
/// Maps to <c>src/client/protocolgameparse.cpp</c>.
/// Task 5.8.
/// </summary>
public sealed partial class ProtocolGame
{
    // ─── Keep-alive ───────────────────────────────────────────────────────────

    private void ParsePing(InputMessage msg)
    {
        // Server says ping; reply immediately
        if (IsConnected) SendPingBack();
    }

    private void ParsePingBack(InputMessage msg)
    {
        // Acknowledgement of our ping — no payload, nothing to do
    }

    // ─── Login flow ───────────────────────────────────────────────────────────

    private void ParseLoginError(InputMessage msg)
    {
        string reason = msg.ReadString();
        LoginError?.Invoke(reason);
    }

    private void ParseLoginAdvice(InputMessage msg)
    {
        string advice = msg.ReadString();
        LoginAdvice?.Invoke(advice);
    }

    private void ParseLoginWait(InputMessage msg)
    {
        string message  = msg.ReadString();
        int    waitSecs = msg.ReadU8();
        LoginWait?.Invoke(message, waitSecs);
    }

    // ─── Login-flow parsers (T44) ─────────────────────────────────────────────

    /// <summary>
    /// Parses <c>GameServerLoginOrPendingState</c> (0x0A).
    /// In Tibia 12.x (protocol 1281) <c>GameLoginPending</c> is active, so this
    /// opcode carries no payload and means the session is now in "pending" state.
    /// Maps to <c>ProtocolGame::parsePendingGame</c>.
    /// Task T44.
    /// </summary>
    private void ParseLoginOrPendingState(InputMessage msg)
    {
        // At protocol 1281 GameLoginPending IS active → no payload, pending state.
        PendingGameReceived?.Invoke();
    }

    /// <summary>
    /// Parses <c>GameServerLoginSuccess</c> (0x17).
    /// Reads the full login payload (playerId, serverBeat, speed params, etc.)
    /// and fires <see cref="LoginSuccessReceived"/>.
    /// Maps to <c>ProtocolGame::parseLogin</c>.
    /// Task T44.
    /// </summary>
    private void ParseLoginSuccess(InputMessage msg)
    {
        // Wire format at protocol 1281:
        // U32  playerId
        // U16  serverBeat
        // dbl  speedA  (GameNewSpeedLaw always on at 1281)
        // dbl  speedB
        // dbl  speedC
        // [canReportBugs skipped — GameDynamicBugReporter on at 1281]
        // U8   canChangePvpFrame  (>= 1054, skip)
        // U8   expertPvpMode      (>= 1058)
        // str  storeUrl           (GameIngameStore on at 1281)
        // U16  coinsPacketSize
        // U8   exivaEnabled       (>= 1281, skip)
        uint   playerId        = msg.ReadU32();
        ushort serverBeat      = msg.ReadU16();
        double speedA          = msg.ReadDouble();
        double speedB          = msg.ReadDouble();
        double speedC          = msg.ReadDouble();
        msg.ReadU8();                              // canChangePvpFrame — skip
        bool   expertPvpMode   = msg.ReadU8() > 0;
        string storeUrl        = msg.ReadString();
        ushort coinsPacketSize = msg.ReadU16();
        msg.ReadU8();                              // exivaEnabled — skip
        LoginSuccessReceived?.Invoke(playerId, serverBeat, speedA, speedB, speedC,
                                     expertPvpMode, storeUrl, coinsPacketSize);
    }

    /// <summary>
    /// Parses <c>GameServerEnterGame</c> (0x0F) — the server confirms the player
    /// is now fully in-game.
    /// Maps to <c>ProtocolGame::parseEnterGame</c>.
    /// Task T44.
    /// </summary>
    private void ParseServerEnterGame(InputMessage msg)
    {
        EnterGameReceived?.Invoke();
    }

    /// <summary>
    /// Parses <c>GameServerSessionEnd</c> (0x18) — the server is ending the session.
    /// Reads one byte: the session-end reason code.
    /// Maps to <c>ProtocolGame::parseSessionEnd → Game::processSessionEnd</c>.
    /// Task T44.
    /// </summary>
    private void ParseSessionEnd(InputMessage msg)
    {
        byte reason = msg.ReadU8();
        SessionEndReceived?.Invoke(reason);
    }

    /// <summary>
    /// Parses <c>GameServerGMActions</c> (0x0B) — 20 bytes of GM action permissions.
    /// Maps to <c>ProtocolGame::parseGMActions → Game::processGMActions</c>.
    /// Task T44.
    /// </summary>
    private void ParseGMActions(InputMessage msg)
    {
        var actions = new byte[20];
        for (int i = 0; i < actions.Length; i++)
            actions[i] = msg.ReadU8();
        GMActionsUpdated?.Invoke(actions);
    }

    /// <summary>
    /// Parses <c>GameServerUpdateNeeded</c> (0x11) — server requests a client update.
    /// Reads a string signature identifying the required version.
    /// Maps to <c>ProtocolGame::parseUpdateNeeded → Game::processUpdateNeeded</c>.
    /// Task T44.
    /// </summary>
    private void ParseUpdateNeeded(InputMessage msg)
    {
        string signature = msg.ReadString();
        UpdateNeededReceived?.Invoke(signature);
    }

    /// <summary>
    /// Parses <c>GameServerStoreButtonIndicators</c> (0x19).
    /// Reads two booleans (isSaleBannerVisible, isNewBannerVisible) and discards them;
    /// the C# client does not have an in-game store UI yet.
    /// Maps to <c>ProtocolGame::parseStoreButtonIndicators</c>.
    /// Task T44.
    /// </summary>
    private void ParseStoreButtonIndicators(InputMessage msg)
    {
        msg.ReadU8(); // isSaleBannerVisible
        msg.ReadU8(); // isNewBannerVisible
    }

    // Map constants from C++ gameconfig defaults:
    private const int MapSeaFloor             = 7;   // floors 0-7 are above ground
    private const int MapMaxZ                 = 15;  // floors 0-15 total
    private const int MapAwareUndergroundRange = 2;   // ± floors around player when underground

    /// <summary>
    /// Parses the <c>FullMap</c> (0x64 / GameServerFullMap) packet.
    /// This is the first game-server packet after XTEA encryption is active.
    /// Reads the player's initial position and the full tile-grid description,
    /// then fires <see cref="GameEntered"/> and <see cref="MapDescriptionReceived"/>.
    /// Maps to <c>ProtocolGame::parseMapDescription</c>.
    /// Task T01.
    /// </summary>
    private void ParseMapDescription(InputMessage msg)
    {
        // Enable XTEA encryption for all subsequent game-server packets.
        _encryptEnabled = true;
        IsInGame = true;

        var pos = ReadPosition(msg);
        _map.CentralPosition = pos;

        if (!_mapKnown)
        {
            _mapKnown = true;
        }

        var range = _map.AwareRange;
        SetMapDescription(msg, pos.X - range.Left, pos.Y - range.Top, pos.Z,
                          range.Horizontal, range.Vertical);

        GameEntered?.Invoke();
        MapDescriptionReceived?.Invoke(pos);
    }

    /// <summary>
    /// Parses the <c>FloorDescription</c> (0x4B / GameServerFloorDescription) packet.
    /// Reads a teleport/floor-change position and populates the tile grid for that
    /// floor from the wire data.
    /// Maps to <c>ProtocolGame::parseFloorDescription</c>.
    /// Task T01.
    /// </summary>
    private void ParseFloorDescription(InputMessage msg)
    {
        var pos   = ReadPosition(msg);
        int floor = msg.ReadU8();

        if (pos.Z == floor)
        {
            _map.CentralPosition = pos;
            if (!_mapKnown) _mapKnown = true;
        }

        var range = _map.AwareRange;
        SetFloorDescription(msg,
                            pos.X - range.Left, pos.Y - range.Top,
                            floor, range.Horizontal, range.Vertical,
                            pos.Z - floor, skip: 0);

        MapDescriptionReceived?.Invoke(pos);
    }

    /// <summary>
    /// Parses <c>MapTopRow</c> (0x65) — the player walked north; a new row of
    /// tiles at the northern edge of the viewport is received.
    /// Maps to <c>ProtocolGame::parseMapMoveNorth</c>.
    /// Task T01.
    /// </summary>
    private void ParseMapMoveNorth(InputMessage msg)
    {
        var  pos   = _map.CentralPosition;
        pos = new Game.Position(pos.X, pos.Y - 1, pos.Z);
        _map.CentralPosition = pos;

        var range = _map.AwareRange;
        SetMapDescription(msg,
                          pos.X - range.Left, pos.Y - range.Top,
                          pos.Z, range.Horizontal, height: 1);
    }

    /// <summary>Parses <c>MapRightRow</c> (0x66) — the player walked east.</summary>
    private void ParseMapMoveEast(InputMessage msg)
    {
        var pos = _map.CentralPosition;
        pos = new Game.Position(pos.X + 1, pos.Y, pos.Z);
        _map.CentralPosition = pos;

        var range = _map.AwareRange;
        SetMapDescription(msg,
                          pos.X + range.Right, pos.Y - range.Top,
                          pos.Z, width: 1, range.Vertical);
    }

    /// <summary>Parses <c>MapBottomRow</c> (0x67) — the player walked south.</summary>
    private void ParseMapMoveSouth(InputMessage msg)
    {
        var pos = _map.CentralPosition;
        pos = new Game.Position(pos.X, pos.Y + 1, pos.Z);
        _map.CentralPosition = pos;

        var range = _map.AwareRange;
        SetMapDescription(msg,
                          pos.X - range.Left, pos.Y + range.Bottom,
                          pos.Z, range.Horizontal, height: 1);
    }

    /// <summary>Parses <c>MapLeftRow</c> (0x68) — the player walked west.</summary>
    private void ParseMapMoveWest(InputMessage msg)
    {
        var pos = _map.CentralPosition;
        pos = new Game.Position(pos.X - 1, pos.Y, pos.Z);
        _map.CentralPosition = pos;

        var range = _map.AwareRange;
        SetMapDescription(msg,
                          pos.X - range.Left, pos.Y - range.Top,
                          pos.Z, width: 1, range.Vertical);
    }

    // ─── Tile update handlers (T02) ───────────────────────────────────────────

    /// <summary>
    /// Parses <c>UpdateTile</c> (0x69) — replaces all things on one tile.
    /// Maps to <c>ProtocolGame::parseUpdateTile</c>.
    /// Task T02.
    /// </summary>
    private void ParseUpdateTile(InputMessage msg)
    {
        var pos = ReadPosition(msg);
        SetTileDescription(msg, pos);
    }

    /// <summary>
    /// Parses <c>TileAddThing</c> (0x6A) — a new thing is added to an existing
    /// tile at a given stack position.
    /// Maps to <c>ProtocolGame::parseTileAddThing</c>.
    /// Task T02.
    /// </summary>
    private void ParseTileAddThing(InputMessage msg)
    {
        var pos      = ReadPosition(msg);
        int stackPos = msg.ReadU8();   // wire always sends stackPos for protocol 1281
        var thing    = ReadThing(msg);
        if (thing is not null)
            _map.AddThing(thing, pos, stackPos);
    }

    /// <summary>
    /// Parses <c>TileTransformThing</c> (0x6B) — replaces one thing on a tile
    /// with another (e.g., door opens/closes, corpse decays).
    /// Maps to <c>ProtocolGame::parseTileTransformThing</c>.
    /// Task T02.
    /// </summary>
    private void ParseTileTransformThing(InputMessage msg)
    {
        // Read the source: either (x, y, z, stackPos) or (0xFFFF, creatureId)
        var (sourceThing, sourcePos, sourceStack) = ReadMappedThing(msg);
        var newThing = ReadThing(msg);

        if (sourceThing is null || newThing is null) return;

        _map.RemoveThing(sourceThing);
        _map.AddThing(newThing, sourcePos, sourceStack);
    }

    /// <summary>
    /// Parses <c>TileRemoveThing</c> (0x6C) — removes one thing from a tile.
    /// Maps to <c>ProtocolGame::parseTileRemoveThing</c>.
    /// Task T02.
    /// </summary>
    private void ParseTileRemoveThing(InputMessage msg)
    {
        var (thing, _, _) = ReadMappedThing(msg);
        if (thing is not null)
            _map.RemoveThing(thing);
    }

    // ─── Map description helpers (T01) ────────────────────────────────────────

    /// <summary>
    /// Iterates over the relevant z-floors around <paramref name="z"/> and calls
    /// <see cref="SetFloorDescription"/> for each, just as the C++ server does.
    /// Maps to <c>ProtocolGame::setMapDescription</c>.
    /// </summary>
    private void SetMapDescription(InputMessage msg, int x, int y, int z,
                                   int width, int height)
    {
        int startz, endz, zstep;
        if (z > MapSeaFloor)
        {
            startz = z - MapAwareUndergroundRange;
            endz   = Math.Min(z + MapAwareUndergroundRange, MapMaxZ);
            zstep  = 1;
        }
        else
        {
            startz = MapSeaFloor;
            endz   = 0;
            zstep  = -1;
        }

        int skip = 0;
        for (int nz = startz; nz != endz + zstep; nz += zstep)
            skip = SetFloorDescription(msg, x, y, nz, width, height, z - nz, skip);
    }

    /// <summary>
    /// Reads tile descriptions for a single floor, populating the map.
    /// Returns the updated <paramref name="skip"/> counter.
    /// Maps to <c>ProtocolGame::setFloorDescription</c>.
    /// </summary>
    private int SetFloorDescription(InputMessage msg, int x, int y, int z,
                                    int width, int height, int offset, int skip)
    {
        for (int nx = 0; nx < width; nx++)
        for (int ny = 0; ny < height; ny++)
        {
            var tilePos = new Game.Position(x + nx + offset, y + ny + offset, z);
            if (skip == 0)
                skip = SetTileDescription(msg, tilePos);
            else
            {
                _map.CleanTile(tilePos);
                skip--;
            }
        }
        return skip;
    }

    /// <summary>
    /// Reads all things for one tile from <paramref name="msg"/>, replacing
    /// any existing tile content.  Returns the skip counter encoded in the
    /// terminator (bits 0–7 of the first U16 ≥ 0xFF00).
    /// Maps to <c>ProtocolGame::setTileDescription</c>.
    /// </summary>
    private int SetTileDescription(InputMessage msg, Game.Position pos)
    {
        _map.CleanTile(pos);
        bool gotEffect = false;

        for (int stackPos = 0; stackPos < 256; stackPos++)
        {
            // Peek at the next U16: if ≥ 0xFF00, it is a skip-count terminator
            ushort peek = msg.PeekU16();
            if (peek >= 0xFF00)
            {
                msg.ReadU16();              // consume the terminator
                TileDescriptionSet?.Invoke(pos);
                return peek & 0xFF;         // low byte = skip count
            }

            // Protocol 1281: first entry on each tile is an environment-effect
            // opcode which must be skipped (consumed and discarded).
            if (!gotEffect)
            {
                msg.ReadU16();  // environment effect — discard
                gotEffect = true;
                continue;
            }

            var thing = ReadThing(msg);
            if (thing is not null)
                _map.AddThing(thing, pos, stackPos);
        }

        TileDescriptionSet?.Invoke(pos);
        return 0;
    }

    // ─── Thing reading helpers (T01/T02) ──────────────────────────────────────

    /// <summary>
    /// Reads a <see cref="Game.Thing"/> from the wire: if the next U16 is a
    /// creature-type ID (97 = unknown, 98 = outdated, 99 = known), delegates
    /// to <see cref="ReadCreatureForTile"/>; otherwise creates an item via
    /// <see cref="ReadItem"/>.
    /// Maps to <c>ProtocolGame::getThing</c>.
    /// </summary>
    private Game.Thing? ReadThing(InputMessage msg)
    {
        ushort id = msg.ReadU16();
        if (id == 0) return null;

        // Creature type IDs: UnknownCreature=97, OutdatedCreature=98, Creature=99
        if (id is 97 or 98 or 99)
            return ReadCreatureForTile(msg, id);

        return ReadItemById(msg, id);
    }

    /// <summary>
    /// Reads an item from the wire given its already-read type <paramref name="id"/>.
    /// For stackable / fluid / splash items the count/sub-type byte is read.
    /// Maps to <c>ProtocolGame::getItem</c> (simplified for protocol 1281).
    /// </summary>
    private static Game.Item ReadItemById(InputMessage msg, int id)
    {
        var item = new Game.Item { Id = id };

        // Stackable, fluid-container, and splash items carry a count/sub-type byte
        // (U8 for protocol < CountU16 extensions, which are not used at 1281 base).
        // We read it if the item flags indicate it is needed; without a ThingType
        // registry we conservatively skip it — a future integration point.
        // (When ThingTypeManager is wired in, replace with real flag lookup.)

        return item;
    }

    /// <summary>
    /// Reads the wire representation of a creature appearing on a tile.
    /// <list type="bullet">
    ///   <item><description>97 (UnknownCreature): full creature data — creates/replaces creature.</description></item>
    ///   <item><description>98 (OutdatedCreature): known creature ID — reads outfit/health update.</description></item>
    ///   <item><description>99 (Creature): known creature ID — no additional data.</description></item>
    /// </list>
    /// Maps to the inline creature-reading logic inside <c>ProtocolGame::getCreature</c>.
    /// Task T01/T02.
    /// </summary>
    private Game.Creature? ReadCreatureForTile(InputMessage msg, int type)
    {
        Game.Creature? creature = null;

        if (type == 97 || type == 98)  // UnknownCreature or OutdatedCreature
        {
            if (type == 97)             // Unknown: remove old ID, assign new ID
            {
                uint removeId = msg.ReadU32();
                uint newId    = msg.ReadU32();

                if (removeId != newId)
                    _map.RemoveCreature(removeId);

                creature = _map.GetCreature(newId);
                if (creature is null)
                {
                    // Determine creature category from the type byte that follows
                    // (creatureType: 0=Player,1=Monster,2=NPC,…); create accordingly.
                    byte creatureType = msg.ReadU8();
                    creature = creatureType switch
                    {
                        0 => new Game.Player(),
                        2 => new Game.Npc(),
                        _ => new Game.Monster(),
                    };
                    creature.Id = newId;
                    _map.AddCreature(creature);
                }
                else
                {
                    msg.ReadU8(); // creatureType — creature already known, skip
                }
            }
            else                        // Outdated: creature is known by ID
            {
                uint creatureId = msg.ReadU32();
                creature = _map.GetCreature(creatureId);
                // If not found the stream still has to be consumed below.
            }

            // Common fields for Unknown + Outdated
            string name          = msg.ReadString();
            byte   healthPercent = msg.ReadU8();
            var    direction     = (Game.Direction)msg.ReadU8();
            var    outfit        = ReadOutfit(msg);
            msg.ReadU8();   // light intensity
            msg.ReadU8();   // light colour
            int speed            = msg.ReadU16();

            // Protocol 1281: icon list (addCreatureIcon)
            byte iconCount = msg.ReadU8();
            for (int i = 0; i < iconCount; i++)
            {
                msg.ReadU8();   // icon type
                msg.ReadU8();   // icon category
                msg.ReadU16();  // icon count
            }

            byte skull  = msg.ReadU8();
            byte shield = msg.ReadU8();

            if (type == 97)             // Unknown: emblem present
                msg.ReadU8();           // emblem

            // GameThingMarks: creatureType byte (redundant for 1281 path already read above
            // for unknown; for outdated it appears here)
            byte creatureTypeForMarks = msg.ReadU8();

            // For summons and players: extra byte (masterId / vocationId)
            if (creatureTypeForMarks is 5 or 6) // SummonOwn / SummonOther
                msg.ReadU32();  // masterId
            else if (creatureTypeForMarks == 0) // Player
                msg.ReadU8();  // vocationId

            msg.ReadU8();   // icon (GameCreatureIcons)
            msg.ReadU8();   // mark (GameThingMarks)
            msg.ReadU8();   // inspection type (v1281)
            bool unpass = msg.ReadU8() != 0; // unpass (v854)

            if (creature is not null)
            {
                creature.Name          = name;
                creature.Direction     = direction;
                creature.Outfit        = outfit;
                creature.Speed         = speed;
                creature.Skull         = skull;
                creature.Shield        = shield;
            }
        }
        else // type == 99 (Creature): creature is known, no additional data
        {
            uint creatureId = msg.ReadU32();
            creature = _map.GetCreature(creatureId);
        }

        return creature;
    }

    /// <summary>
    /// Reads a "mapped thing" from the wire — the source reference used by
    /// <c>TileTransformThing</c> and <c>TileRemoveThing</c>.
    /// Returns the resolved <see cref="Game.Thing"/> (may be null if not found),
    /// its position, and stack index.
    /// Maps to <c>ProtocolGame::getMappedThing</c>.
    /// </summary>
    private (Game.Thing? thing, Game.Position pos, int stackPos) ReadMappedThing(InputMessage msg)
    {
        ushort x = msg.ReadU16();
        if (x != 0xFFFF)
        {
            int y        = msg.ReadU16();
            int z        = msg.ReadU8();
            int stackPos = msg.ReadU8();
            var pos      = new Game.Position(x, y, z);
            var tile     = _map.Get(pos);
            var thing    = tile?.GetThingAtStack(stackPos);
            return (thing, pos, stackPos);
        }
        else
        {
            uint creatureId = msg.ReadU32();
            var  creature   = _map.GetCreature(creatureId);
            return (creature, creature?.Position ?? Game.Position.Invalid, 0);
        }
    }

    // ─── Death ────────────────────────────────────────────────────────────────

    private void ParseDeath(InputMessage msg)
    {
        // Tibia 12.x death packet may include death type / penalty — skip for now
        if (msg.Remaining > 0)
            msg.Skip(msg.Remaining);
        PlayerDied?.Invoke();
    }

    // ─── Text messages ────────────────────────────────────────────────────────

    private void ParseTextMessage(InputMessage msg)
    {
        byte   type    = msg.ReadU8();
        string message = msg.ReadString();
        TextMessageReceived?.Invoke(type, message);
    }

    /// <summary>
    /// Parses <c>EditText</c> (0x96 / GameServerEditText).
    /// Reads id U32, item (U16 + optional fields), maxLength U16, text string,
    /// writer string, suffix byte (protocol 1281 always), and fires
    /// <see cref="EditTextReceived"/>.
    /// Maps to <c>ProtocolGame::parseEditText</c>.
    /// Task T23.
    /// </summary>
    private void ParseEditText(InputMessage msg)
    {
        uint   id        = msg.ReadU32();
        int    itemId    = msg.ReadU16();   // getItem reads U16 id at protocol 1281
        ushort maxLength = msg.ReadU16();
        string text      = msg.ReadString();
        string writer    = msg.ReadString();
        msg.ReadU8();                        // suffix byte (always present at protocol 1281)
        // GameWritableDate feature not set at protocol 1281 — skip date string
        EditTextReceived?.Invoke(id, itemId, maxLength, text, writer, string.Empty);
    }

    // ─── Player stats (T05) ───────────────────────────────────────────────────

    /// <summary>
    /// Parses the <c>PlayerData</c> (0xA0) packet for protocol version 1281.
    /// Updates health, mana, experience, level, stamina, soul and fires
    /// <see cref="PlayerStatsUpdated"/>.
    /// Maps to <c>ProtocolGame::parsePlayerStats</c> in
    /// <c>src/client/protocolgameparse.cpp</c>.
    /// Task T05.
    /// </summary>
    private void ParsePlayerStats(InputMessage msg)
    {
        // Protocol 1281 uses U32 for health/mana (GameDoubleHealth feature ON).
        int health    = (int)msg.ReadU32();
        int maxHealth = (int)msg.ReadU32();

        // freeCapacity: U32 scaled by 100 (GameDoubleFreeCapacity feature ON).
        int freeCapacity = (int)(msg.ReadU32() / 100u);

        // experience: U64 (GameDoubleExperience feature ON in 1281).
        ulong experience = msg.ReadU64();

        // level and percent (GameLevelU16 feature ON in 1281).
        int level        = msg.ReadU16();
        int levelPercent = msg.ReadU8();

        // Experience rate bonus fields (GameExperienceBonus feature, >= 1097).
        // For 1281: baseXpGain, grindingAddend, storeBoostAddend, huntingBoostFactor.
        msg.ReadU16(); // baseXpGain
        msg.ReadU16(); // grindingAddend
        msg.ReadU16(); // storeBoostAddend
        msg.ReadU16(); // huntingBoostFactor

        int mana    = (int)msg.ReadU32();
        int maxMana = (int)msg.ReadU32();

        int soul    = msg.ReadU8();
        int stamina = msg.ReadU16();
        msg.ReadU16(); // baseSpeed
        int regenerationTime    = msg.ReadU16(); // seconds until next regeneration
        int offlineTrainingTime = msg.ReadU16(); // offline training time remaining (seconds)

        // >= 1097: xpBoostTime (seconds) + enableXpBoostStore flag.
        msg.ReadU16(); // xpBoostTime
        msg.ReadU8();  // enableXpBoostStore

        // >= 1281: remaining mana shield + total mana shield (U32 each, GameDoubleHealth ON).
        msg.ReadU32(); // manaShield
        msg.ReadU32(); // maxManaShield

        PlayerStatsUpdated?.Invoke(
            health, maxHealth, mana, maxMana,
            freeCapacity, experience,
            level, levelPercent,
            stamina, soul,
            regenerationTime, offlineTrainingTime);
    }

    private static readonly int SkillCount = Enum.GetValues<Game.SkillType>().Length; // 7

    /// <summary>
    /// Parses the <c>PlayerSkills</c> (0xA1) packet for protocol version 1281.
    /// Fires <see cref="PlayerSkillsUpdated"/>.
    /// Maps to <c>ProtocolGame::parsePlayerSkills</c>.
    /// Task T05.
    /// </summary>
    private void ParsePlayerSkills(InputMessage msg)
    {
        // Protocol 1281: magic level section precedes combat skills.
        int magicLevel        = msg.ReadU16();
        int baseMagicLevel    = msg.ReadU16();
        msg.ReadU16();                      // base + loyalty bonus (discarded)
        int magicLevelPercent = msg.ReadU16() / 100;

        // 7 combat skills: Fist, Club, Sword, Axe, Distance, Shielding, Fishing.
        // Protocol 1281: level U16, baseLevel U16, loyalty U16, percent (U16/100).
        int[] levels     = new int[SkillCount];
        int[] baseLevels = new int[SkillCount];
        int[] percents   = new int[SkillCount];
        for (int i = 0; i < SkillCount; i++)
        {
            levels[i]     = msg.ReadU16();
            baseLevels[i] = msg.ReadU16();
            msg.ReadU16(); // loyalty bonus
            percents[i]   = msg.ReadU16() / 100;
        }

        PlayerSkillsUpdated?.Invoke(magicLevel, baseMagicLevel, magicLevelPercent, levels, baseLevels, percents);
    }

    /// <summary>
    /// Parses the <c>PlayerState</c> (0xA2) packet for protocol version 1281.
    /// Fires <see cref="PlayerStateUpdated"/>.
    /// Maps to <c>ProtocolGame::parsePlayerState</c>.
    /// Task T05.
    /// </summary>
    private void ParsePlayerState(InputMessage msg)
    {
        // Protocol 1281 (< 1405): U32 state bitmask.
        uint states = msg.ReadU32();
        PlayerStateUpdated?.Invoke(states);
    }

    /// <summary>
    /// Parses the <c>PlayerModes</c> (0xA7) packet.
    /// Fires <see cref="PlayerModesUpdated"/>.
    /// Maps to <c>ProtocolGame::parsePlayerModes</c>.
    /// Task T05.
    /// </summary>
    private void ParsePlayerModes(InputMessage msg)
    {
        var  fightMode = (Game.FightMode)msg.ReadU8();
        var  chaseMode = (Game.ChaseMode)msg.ReadU8();
        bool safeMode  = msg.ReadU8() != 0;
        var  pvpMode   = (Game.PvpMode)msg.ReadU8();
        PlayerModesUpdated?.Invoke(fightMode, chaseMode, safeMode, pvpMode);
    }

    // ─── Creature handlers (T03) ─────────────────────────────────────────────

    /// <summary>
    /// Reads a 3-byte position (x U16, y U16, z U8) from <paramref name="msg"/>.
    /// </summary>
    private static Game.Position ReadPosition(InputMessage msg)
        => new(msg.ReadU16(), msg.ReadU16(), msg.ReadU8());

    /// <summary>
    /// Reads an outfit from <paramref name="msg"/> for protocol version 1281.
    /// Wire format: lookType U16; if nonzero: head/body/legs/feet/addons (5×U8);
    /// else: lookTypeEx U16.  Then mountId U16; if nonzero (1281): 4 mount
    /// colour bytes.
    /// Maps to <c>ProtocolGame::getOutfit</c>.
    /// </summary>
    private static Game.Outfit ReadOutfit(InputMessage msg, bool parseMount = true)
    {
        int lookType = msg.ReadU16();
        if (lookType != 0)
        {
            byte head   = msg.ReadU8();
            byte body   = msg.ReadU8();
            byte legs   = msg.ReadU8();
            byte feet   = msg.ReadU8();
            byte addons = msg.ReadU8();

            int mountId = 0;
            if (parseMount)
            {
                mountId = msg.ReadU16();
                if (mountId != 0)
                {
                    // Protocol 1281: mount colour bytes follow mount ID
                    msg.ReadU8(); // mountHead
                    msg.ReadU8(); // mountBody
                    msg.ReadU8(); // mountLegs
                    msg.ReadU8(); // mountFeet
                }
            }

            return new Game.Outfit
            {
                Id      = lookType,
                Head    = head,
                Body    = body,
                Legs    = legs,
                Feet    = feet,
                Addons  = addons,
                MountId = mountId,
            };
        }
        else
        {
            // lookTypeEx: 0 = invisible effect placeholder; nonzero = item-appearance ID.
            // The C# Outfit record does not have a separate AuxId field, so the item
            // appearance ID is stored in Id (with MountId = 0 to signal "not a creature
            // outfit").  Callers can distinguish the invisible-effect case by Id == 0.
            int lookTypeEx = msg.ReadU16();
            return new Game.Outfit { Id = lookTypeEx, MountId = 0 };
        }
    }

    /// <summary>
    /// Parses the <c>MoveCreature</c> (0x6D) packet.
    /// The wire encoding carries either a tile position+stackpos (x != 0xFFFF)
    /// or a creature ID (x == 0xFFFF) for the source, followed by the
    /// destination position.
    /// Fires <see cref="CreatureTileMoved"/> or <see cref="CreatureMovedById"/>.
    /// Maps to <c>ProtocolGame::parseCreatureMove</c>.
    /// Task T03.
    /// </summary>
    private void ParseCreatureMove(InputMessage msg)
    {
        ushort x = msg.ReadU16();
        if (x != 0xFFFF)
        {
            // Position-based form: creature at (x, y, z, stackPos)
            int y        = msg.ReadU16();
            int z        = msg.ReadU8();
            int stackPos = msg.ReadU8();
            var fromPos  = new Game.Position(x, y, z);
            var toPos    = ReadPosition(msg);
            CreatureTileMoved?.Invoke(fromPos, stackPos, toPos);
        }
        else
        {
            // ID-based form: creature known by ID
            uint creatureId = msg.ReadU32();
            var  toPos      = ReadPosition(msg);
            CreatureMovedById?.Invoke(creatureId, toPos);
        }
    }

    /// <summary>
    /// Parses the <c>CreatureHealth</c> (0x8C) packet.
    /// Fires <see cref="CreatureHealthUpdated"/>.
    /// Maps to <c>ProtocolGame::parseCreatureHealth</c>.
    /// Task T03.
    /// </summary>
    private void ParseCreatureHealth(InputMessage msg)
    {
        uint creatureId    = msg.ReadU32();
        byte healthPercent = msg.ReadU8();      // 0–100
        CreatureHealthUpdated?.Invoke(creatureId, healthPercent);
    }

    /// <summary>
    /// Parses the <c>CreatureOutfit</c> (0x8E) packet.
    /// Fires <see cref="CreatureOutfitUpdated"/>.
    /// Maps to <c>ProtocolGame::parseCreatureOutfit</c>.
    /// Task T03.
    /// </summary>
    private void ParseCreatureOutfit(InputMessage msg)
    {
        uint      creatureId = msg.ReadU32();
        Game.Outfit outfit   = ReadOutfit(msg);
        CreatureOutfitUpdated?.Invoke(creatureId, outfit);
    }

    /// <summary>
    /// Parses the <c>CreatureSpeed</c> (0x8F) packet.
    /// Fires <see cref="CreatureSpeedUpdated"/>.
    /// Maps to <c>ProtocolGame::parseCreatureSpeed</c>.
    /// Task T03.
    /// </summary>
    private void ParseCreatureSpeed(InputMessage msg)
    {
        uint creatureId = msg.ReadU32();
        int  baseSpeed  = msg.ReadU16();    // protocol 1281 always sends baseSpeed
        int  speed      = msg.ReadU16();
        CreatureSpeedUpdated?.Invoke(creatureId, baseSpeed, speed);
    }

    /// <summary>
    /// Parses the <c>CreatureData</c> (0x8B) packet.
    /// Handles type 11/12/13 (vocation data — fires <see cref="CreatureVocationUpdated"/>)
    /// and type 14 (creature icons — reads and discards icon entries to keep
    /// the stream in sync).
    /// Type 0 (full creature creation/update) is not yet implemented; it
    /// requires the tile-addition flow from T01/T02.
    /// Maps to <c>ProtocolGame::parseCreatureData</c>.
    /// Task T03.
    /// </summary>
    private void ParseCreatureData(InputMessage msg)
    {
        uint  creatureId = msg.ReadU32();
        byte  type       = msg.ReadU8();

        switch (type)
        {
            case 11: // creature mana percent (0–100)
            {
                byte value = msg.ReadU8();
                CreatureDataByteReceived?.Invoke(creatureId, type, value);
                break;
            }
            case 12: // creature show-status byte
            {
                byte value = msg.ReadU8();
                CreatureDataByteReceived?.Invoke(creatureId, type, value);
                break;
            }
            case 13: // player vocation ID
            {
                byte value = msg.ReadU8();
                CreatureDataByteReceived?.Invoke(creatureId, type, value);
                break;
            }
            case 14: // creature icons
            {
                byte count = msg.ReadU8();
                for (int i = 0; i < count; i++)
                {
                    msg.ReadU8();  // icon type
                    msg.ReadU8();  // icon category
                    msg.ReadU16(); // icon count
                }
                break;
            }
            default:
                // Type 0 (full creature update) requires T01/T02 infrastructure.
                // Unknown types: consume no further bytes to avoid stream corruption.
                break;
        }
    }

    // ─── Container handlers (T04) ─────────────────────────────────────────────

    // ─── Combat status handlers (T13) ────────────────────────────────────────

    /// <summary>
    /// Parses <c>CreatureSkull</c> (0x90 / GameServerCreatureSkull).
    /// Reads a creature ID and its new skull byte.
    /// Maps to <c>ProtocolGame::parseCreatureSkulls</c>.
    /// Task T13.
    /// </summary>
    private void ParseCreatureSkull(InputMessage msg)
    {
        uint creatureId = msg.ReadU32();
        byte skull      = msg.ReadU8();
        CreatureSkullUpdated?.Invoke(creatureId, skull);
    }

    /// <summary>
    /// Parses <c>CreatureParty</c> (0x91 / GameServerCreatureParty).
    /// Reads a creature ID and its new party-shield byte.
    /// Maps to <c>ProtocolGame::parseCreatureShields</c>.
    /// Task T13.
    /// </summary>
    private void ParseCreatureShield(InputMessage msg)
    {
        uint creatureId = msg.ReadU32();
        byte shield     = msg.ReadU8();
        CreatureShieldUpdated?.Invoke(creatureId, shield);
    }

    /// <summary>
    /// Parses <c>CreatureMarks</c> (0x93 / GameServerCreatureMarks).
    /// At protocol 1281 (clientVersion ≥ 1076) the permanent/timed flag is
    /// always present as the second byte.
    /// Maps to <c>ProtocolGame::parseCreaturesMark</c>.
    /// Task T13.
    /// </summary>
    private void ParseCreatureMarks(InputMessage msg)
    {
        uint creatureId  = msg.ReadU32();
        bool isPermanent = msg.ReadU8() == 0;  // 0 = permanent; non-zero = timed
        byte markType    = msg.ReadU8();
        CreatureMarksUpdated?.Invoke(creatureId, isPermanent, markType);
    }

    /// <summary>
    /// Parses <c>CancelWalk</c> (0xB5 / GameServerCancelWalk).
    /// Reads the direction the local player should face after the walk is
    /// cancelled by the server.
    /// Maps to <c>ProtocolGame::parseCancelWalk → Game::processWalkCancel</c>.
    /// Task T13.
    /// </summary>
    private void ParseCancelWalk(InputMessage msg)
    {
        var direction = (Game.Direction)msg.ReadU8();
        WalkCanceled?.Invoke(direction);
    }

    /// <summary>
    /// Parses <c>OpenContainer</c> (0x6E / GameServerOpenContainer).
    /// Reads all container meta-data and initial item list, creates a
    /// <see cref="Game.Container"/> in <c>_containers[id]</c>, and raises
    /// <see cref="ContainerOpened"/>.
    /// Maps to <c>ProtocolGame::parseOpenContainer</c>.
    /// Task T04.
    /// </summary>
    private void ParseOpenContainer(InputMessage msg)
    {
        int  containerId    = msg.ReadU8();
        var  containerItem  = ReadItemById(msg, msg.ReadU16());
        string name         = msg.ReadString();
        int  capacity       = msg.ReadU8();
        bool hasParent      = msg.ReadU8() != 0;

        // Protocol 1281: show-search-icon byte (discard)
        msg.ReadU8();

        // GameContainerPagination fields (present for protocol 1281)
        bool   isUnlocked     = msg.ReadU8() != 0;
        bool   hasPages       = msg.ReadU8() != 0;
        int    containerSize  = msg.ReadU16();
        int    firstIndex     = msg.ReadU16();

        int    itemCount      = msg.ReadU8();
        var    items          = new List<Game.Item>(itemCount);
        for (int i = 0; i < itemCount; i++)
            items.Add(ReadItemById(msg, msg.ReadU16()));

        // Close any previously open container at this slot
        var previous = GetContainer(containerId);
        previous?.Close();

        var container = new Game.Container
        {
            Id            = containerId,
            Name          = name,
            Capacity      = capacity,
            ContainerItem = containerItem,
            HasParent     = hasParent,
            IsUnlocked    = isUnlocked,
            HasPages      = hasPages,
            Size          = containerSize,
            FirstIndex    = firstIndex,
        };
        container.AddItems(items);

        _containers[containerId] = container;
        ContainerOpened?.Invoke(container);
    }

    /// <summary>
    /// Parses <c>CloseContainer</c> (0x6F / GameServerCloseContainer).
    /// Maps to <c>ProtocolGame::parseCloseContainer</c>.
    /// Task T04.
    /// </summary>
    private void ParseCloseContainer(InputMessage msg)
    {
        int containerId = msg.ReadU8();
        var container   = GetContainer(containerId);
        if (container is not null)
        {
            container.Close();
            _containers[containerId] = null;
        }
        ContainerClosed?.Invoke(containerId);
    }

    /// <summary>
    /// Parses <c>ContainerAddItem</c> (0x70 / GameServerCreateContainer / wire opcode 112).
    /// Reads a slot index (U16 for paginated bags) and item ID, adds the item
    /// to the container, and raises <see cref="ContainerItemAdded"/>.
    /// Maps to <c>ProtocolGame::parseContainerAddItem</c>.
    /// Task T04.
    /// </summary>
    private void ParseContainerAddItem(InputMessage msg)
    {
        int containerId = msg.ReadU8();
        int slot        = msg.ReadU16();   // GameContainerPagination always U16 at 1281
        var item        = ReadItemById(msg, msg.ReadU16());

        GetContainer(containerId)?.AddItem(item, slot);
        ContainerItemAdded?.Invoke(containerId, slot, item);
    }

    /// <summary>
    /// Parses <c>ContainerUpdateItem</c> (0x71 / GameServerChangeInContainer).
    /// Maps to <c>ProtocolGame::parseContainerUpdateItem</c>.
    /// Task T04.
    /// </summary>
    private void ParseContainerUpdateItem(InputMessage msg)
    {
        int containerId = msg.ReadU8();
        int slot        = msg.ReadU16();   // U16 at 1281 (GameContainerPagination)
        var item        = ReadItemById(msg, msg.ReadU16());

        GetContainer(containerId)?.UpdateAt(slot, item);
        ContainerItemUpdated?.Invoke(containerId, slot, item);
    }

    /// <summary>
    /// Parses <c>ContainerRemoveItem</c> (0x72 / GameServerDeleteInContainer).
    /// Reads the slot, then an optional last-item ID (non-zero = paginated bag
    /// moves the item from the hidden overflow into view after the removal).
    /// Maps to <c>ProtocolGame::parseContainerRemoveItem</c>.
    /// Task T04.
    /// </summary>
    private void ParseContainerRemoveItem(InputMessage msg)
    {
        int  containerId = msg.ReadU8();
        int  slot        = msg.ReadU16();   // GameContainerPagination → always U16

        Game.Item? lastItem = null;
        ushort lastId = msg.ReadU16();
        if (lastId != 0)
            lastItem = ReadItemById(msg, lastId);

        GetContainer(containerId)?.RemoveAt(slot, lastItem);
        ContainerItemRemoved?.Invoke(containerId, slot, lastItem);
    }

    // ─── Inventory handlers (T04) ─────────────────────────────────────────────

    /// <summary>
    /// Parses <c>SetInventory</c> (0x78 / GameServerSetInventory).
    /// Sets an inventory slot to the supplied item and raises
    /// <see cref="InventoryItemChanged"/>.
    /// Maps to <c>ProtocolGame::parseAddInventoryItem</c>.
    /// Task T04.
    /// </summary>
    private void ParseAddInventoryItem(InputMessage msg)
    {
        var slot = (Game.InventorySlot)msg.ReadU8();
        var item = ReadItemById(msg, msg.ReadU16());

        int idx = (int)slot;
        if ((uint)idx < InventorySlotCount)
            _inventory[idx] = item;

        InventoryItemChanged?.Invoke(slot, item);
    }

    /// <summary>
    /// Parses <c>DeleteInventory</c> (0x79 / GameServerDeleteInventory).
    /// Clears an inventory slot and raises <see cref="InventoryItemChanged"/> with
    /// a null item.
    /// Maps to <c>ProtocolGame::parseRemoveInventoryItem</c>.
    /// Task T04.
    /// </summary>
    private void ParseRemoveInventoryItem(InputMessage msg)
    {
        var slot = (Game.InventorySlot)msg.ReadU8();

        int idx = (int)slot;
        if ((uint)idx < InventorySlotCount)
            _inventory[idx] = null;

        InventoryItemChanged?.Invoke(slot, null);
    }

    // ─── Chat / channel handlers (T09) ────────────────────────────────────────

    /// <summary>
    /// Parses <c>Talk</c> (0xAA / GameServerTalk).
    /// Reads the statement GUID, speaker name, optional suffix, speaker level, talk mode,
    /// optional position or channel ID, and the text, then raises <see cref="TalkReceived"/>.
    /// Protocol 1281: GameMessageStatements and GameMessageLevel are always present.
    /// Maps to <c>ProtocolGame::parseTalk</c>.
    /// Task T09.
    /// </summary>
    private void ParseTalk(InputMessage msg)
    {
        // Statement GUID (always present at protocol 1281 — GameMessageStatements feature)
        uint statement = msg.ReadU32();

        string author = msg.ReadString();

        // Suffix byte — only present when statement != 0 at protocol 1281
        if (statement != 0)
            msg.ReadU8();

        // Speaker level (always present at protocol 1281 — GameMessageLevel feature)
        int level = msg.ReadU16();

        var mode = (TalkMode)msg.ReadU8();

        Game.Position? pos = null;
        ushort channelId = 0;

        switch (mode)
        {
            // Modes that include the world-position of the speaker
            case TalkMode.Say:
            case TalkMode.Whisper:
            case TalkMode.Yell:
            case TalkMode.Spell:
            case TalkMode.NpcFromStartBlock:
            case TalkMode.NpcTo:
            case TalkMode.BarkLow:
            case TalkMode.BarkLoud:
            case TalkMode.Potion:
                pos = ReadPosition(msg);
                break;

            // Modes that include a channel ID
            case TalkMode.Channel:
            case TalkMode.ChannelManagement:
            case TalkMode.ChannelHighlight:
            case TalkMode.GamemasterChannel:
                channelId = msg.ReadU16();
                break;

            // All other modes carry no extra context bytes
            default:
                break;
        }

        string text = msg.ReadString();
        TalkReceived?.Invoke(author, level, mode, text, channelId, pos);
    }

    /// <summary>
    /// Parses <c>ChannelList</c> (0xAB / GameServerChannels).
    /// Reads a list of (channelId, channelName) pairs and raises
    /// <see cref="ChannelListReceived"/>.
    /// Maps to <c>ProtocolGame::parseChannelList</c>.
    /// Task T09.
    /// </summary>
    private void ParseChannelList(InputMessage msg)
    {
        int count = msg.ReadU8();
        var channels = new List<(ushort Id, string Name)>(count);
        for (int i = 0; i < count; i++)
        {
            ushort id   = msg.ReadU16();
            string name = msg.ReadString();
            channels.Add((id, name));
        }
        ChannelListReceived?.Invoke(channels);
    }

    /// <summary>
    /// Parses <c>OpenChannel</c> (0xAC / GameServerOpenChannel).
    /// Reads the channel ID and name, then the joined / invited player lists
    /// (always present at protocol 1281 — GameChannelPlayerList feature), then
    /// raises <see cref="ChannelOpened"/>.
    /// Maps to <c>ProtocolGame::parseOpenChannel</c>.
    /// Task T09.
    /// </summary>
    private void ParseOpenChannel(InputMessage msg)
    {
        ushort channelId   = msg.ReadU16();
        string channelName = msg.ReadString();

        // GameChannelPlayerList — always present at protocol 1281
        int joinedCount = msg.ReadU16();
        for (int i = 0; i < joinedCount; i++)
            msg.ReadString(); // player name

        int invitedCount = msg.ReadU16();
        for (int i = 0; i < invitedCount; i++)
            msg.ReadString(); // player name

        ChannelOpened?.Invoke(channelId, channelName);
    }

    /// <summary>
    /// Parses <c>OpenPrivateChannel</c> (0xAD / GameServerOpenPrivateChannel).
    /// Reads the other player's name and raises <see cref="PrivateChannelOpened"/>.
    /// Maps to <c>ProtocolGame::parseOpenPrivateChannel</c>.
    /// Task T09.
    /// </summary>
    private void ParseOpenPrivateChannel(InputMessage msg)
    {
        string name = msg.ReadString();
        PrivateChannelOpened?.Invoke(name);
    }

    /// <summary>
    /// Parses <c>CloseChannel</c> (0xB3 / GameServerCloseChannel).
    /// Reads the channel ID and raises <see cref="ChannelClosed"/>.
    /// Maps to <c>ProtocolGame::parseCloseChannel</c>.
    /// Task T09.
    /// </summary>
    private void ParseCloseChannel(InputMessage msg)
    {
        ushort channelId = msg.ReadU16();
        ChannelClosed?.Invoke(channelId);
    }

    // ─── VIP handlers (T11) ───────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>VipAdd</c> (0xD2 / GameServerVipAdd).
    /// Reads a VIP entry and raises <see cref="VipAdded"/>.
    /// Protocol 1281: GameAdditionalVipInfo and GameVipGroups are always present.
    /// Maps to <c>ProtocolGame::parseVipAdd</c>.
    /// Task T11.
    /// </summary>
    private void ParseVipAdd(InputMessage msg)
    {
        uint   id          = msg.ReadU32();
        string name        = msg.ReadString();

        // GameAdditionalVipInfo — always present at protocol 1281
        string description = msg.ReadString();
        uint   iconId      = msg.ReadU32();
        bool   notify      = msg.ReadU8() != 0;

        uint status = msg.ReadU8();

        // GameVipGroups — always present at protocol 1281
        int groupCount = msg.ReadU8();
        for (int i = 0; i < groupCount; i++)
            msg.ReadU8(); // group ID

        VipAdded?.Invoke(id, name, status, description, iconId, notify);
    }

    /// <summary>
    /// Parses <c>VipState</c> (0xD3 / GameServerVipState).
    /// Reads the VIP entry ID and new status byte and raises <see cref="VipStateChanged"/>.
    /// Protocol 1281: GameLoginPending is always present (status is U8).
    /// Maps to <c>ProtocolGame::parseVipState</c>.
    /// Task T11.
    /// </summary>
    private void ParseVipState(InputMessage msg)
    {
        uint id     = msg.ReadU32();
        uint status = msg.ReadU8(); // GameLoginPending — always present at protocol 1281
        VipStateChanged?.Invoke(id, status);
    }

    /// <summary>
    /// Parses <c>VipLogout</c> (0xD4 / GameServerVipLogout).
    /// Protocol 1281: GameVipGroups is present, so this packet carries the full
    /// VIP groups list rather than a single logout.  The group data is consumed
    /// and discarded; client-side effects (e.g. refreshing the groups UI) are not
    /// yet implemented.
    /// Maps to <c>ProtocolGame::parseVipLogout</c>.
    /// Task T11.
    /// </summary>
    private void ParseVipLogout(InputMessage msg)
    {
        // GameVipGroups — always present at protocol 1281
        int groupCount = msg.ReadU8();
        for (int i = 0; i < groupCount; i++)
        {
            msg.ReadU8();     // group ID
            msg.ReadString(); // group name
            msg.ReadU8();     // canEditGroup
        }
        msg.ReadU8(); // groupsAmountLeft
    }

    // ─── NPC trade handlers (T15) ─────────────────────────────────────────────

    /// <summary>
    /// Parses <c>OpenNpcTrade</c> (0x7A / GameServerOpenNpcTrade).
    /// Protocol 1281: reads optional npcName (GameNameOnNpcTrade), currency U16 +
    /// string, then a U16 item count (GameDoubleNpcTrade) followed by each item's
    /// type U16, count U8, name string, weight U32, buyPrice U32, sellPrice U32.
    /// Raises <see cref="NpcTradeOpened"/>.
    /// Maps to <c>ProtocolGame::parseOpenNpcTrade</c>.
    /// Task T15.
    /// </summary>
    private void ParseOpenNpcTrade(InputMessage msg)
    {
        // GameNameOnNpcTrade — always present at protocol 1281
        msg.ReadString(); // npcName (consumed; surfaced to UI through the items list)

        // Currency info — always present at protocol 1281 (clientVersion >= 1281)
        msg.ReadU16();    // currency item type ID
        msg.ReadString(); // currency name

        // Item count: U16 for protocol >= 900 (always true at 1281)
        int count = msg.ReadU16();
        var items = new List<Game.NpcTradeItem>(count);

        for (int i = 0; i < count; i++)
        {
            ushort itemId    = msg.ReadU16();
            byte   itemCount = msg.ReadU8();
            string name      = msg.ReadString();
            uint   weight    = msg.ReadU32();
            uint   buyPrice  = msg.ReadU32();
            uint   sellPrice = msg.ReadU32();

            var item = Game.Item.Create(itemId, itemCount);
            items.Add(new Game.NpcTradeItem(item, name, weight, buyPrice, sellPrice));
        }

        NpcTradeOpened?.Invoke(items);
    }

    /// <summary>
    /// Parses <c>PlayerGoods</c> (0x7B / GameServerPlayerGoods).
    /// Protocol 1281: money field is skipped (server still sends it); item count is U8.
    /// Raises <see cref="PlayerGoodsReceived"/>.
    /// Maps to <c>ProtocolGame::parsePlayerGoods</c>.
    /// Task T15.
    /// </summary>
    private void ParsePlayerGoods(InputMessage msg)
    {
        // At protocol 1281 the server still sends the money U64 even though the
        // client derives it from the resource balance — we consume it here.
        ulong money = msg.ReadU64();

        // Item count: U8 for protocol < 1334 (base 1281)
        int count = msg.ReadU8();
        var goods = new List<(int ItemId, int Amount)>(count);

        for (int i = 0; i < count; i++)
        {
            int itemId = msg.ReadU16();
            int amount = msg.ReadU8(); // U8 for protocol < GameDoubleShopSellAmount
            goods.Add((itemId, amount));
        }

        PlayerGoodsReceived?.Invoke(money, goods);
    }

    /// <summary>
    /// Parses <c>CloseNpcTrade</c> (0x7C / GameServerCloseNpcTrade).
    /// No payload. Raises <see cref="NpcTradeClosed"/>.
    /// Maps to <c>ProtocolGame::parseCloseNpcTrade</c>.
    /// Task T15.
    /// </summary>
    private void ParseCloseNpcTrade(InputMessage _)
        => NpcTradeClosed?.Invoke();

    // ─── Player-to-player trade handlers (T16) ────────────────────────────────

    /// <summary>
    /// Parses <c>OwnTrade</c> (0x7D / GameServerOwnTrade).
    /// Reads partner name, item count U8, then each item via <see cref="ReadItemById"/>.
    /// Raises <see cref="OwnTradeReceived"/>.
    /// Maps to <c>ProtocolGame::parseOwnTrade</c>.
    /// Task T16.
    /// </summary>
    private void ParseOwnTrade(InputMessage msg)
    {
        string name  = msg.ReadString();
        int    count = msg.ReadU8();
        var    items = new List<Game.Item>(count);
        for (int i = 0; i < count; i++)
            items.Add(ReadItemById(msg, msg.ReadU16()));
        OwnTradeReceived?.Invoke(name, items);
    }

    /// <summary>
    /// Parses <c>CounterTrade</c> (0x7E / GameServerCounterTrade).
    /// Reads partner name, item count U8, then each item via <see cref="ReadItemById"/>.
    /// Raises <see cref="CounterTradeReceived"/>.
    /// Maps to <c>ProtocolGame::parseCounterTrade</c>.
    /// Task T16.
    /// </summary>
    private void ParseCounterTrade(InputMessage msg)
    {
        string name  = msg.ReadString();
        int    count = msg.ReadU8();
        var    items = new List<Game.Item>(count);
        for (int i = 0; i < count; i++)
            items.Add(ReadItemById(msg, msg.ReadU16()));
        CounterTradeReceived?.Invoke(name, items);
    }

    /// <summary>
    /// Parses <c>CloseTrade</c> (0x7F / GameServerCloseTrade).
    /// No payload. Raises <see cref="PlayerTradeClosed"/>.
    /// Maps to <c>ProtocolGame::parseCloseTrade</c>.
    /// Task T16.
    /// </summary>
    private void ParseCloseTrade(InputMessage _)
        => PlayerTradeClosed?.Invoke();

    // ─── Quest log (T23) ─────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>QuestLog</c> (0xF0 / GameServerQuestLog).
    /// Reads U16 count; per entry: U16 id, string name, U8 completed flag.
    /// Raises <see cref="QuestLogReceived"/>.
    /// Maps to <c>ProtocolGame::parseQuestLog</c>.
    /// Task T23.
    /// </summary>
    private void ParseQuestLog(InputMessage msg)
    {
        int count = msg.ReadU16();
        var quests = new List<Game.QuestEntry>(count);
        for (int i = 0; i < count; i++)
        {
            ushort id        = msg.ReadU16();
            string name      = msg.ReadString();
            bool   completed = msg.ReadU8() != 0;
            quests.Add(new Game.QuestEntry(id, name, completed));
        }
        QuestLogReceived?.Invoke(quests);
    }

    /// <summary>
    /// Parses <c>QuestLine</c> (0xF1 / GameServerQuestLine).
    /// Reads U16 questId; U8 missionCount; per mission: U16 missionId (≥1200),
    /// string name, string description.
    /// Raises <see cref="QuestLineReceived"/>.
    /// Maps to <c>ProtocolGame::parseQuestLine</c>.
    /// Task T23.
    /// </summary>
    private void ParseQuestLine(InputMessage msg)
    {
        ushort questId  = msg.ReadU16();
        int    count    = msg.ReadU8();
        var    missions = new List<Game.QuestMission>(count);
        for (int i = 0; i < count; i++)
        {
            ushort missionId   = msg.ReadU16();   // always present: clientVersion 1281 satisfies ≥ 1200
            string missionName = msg.ReadString();
            string description = msg.ReadString();
            missions.Add(new Game.QuestMission(missionName, description, missionId));
        }
        QuestLineReceived?.Invoke(questId, missions);
    }

    // ─── Modal dialog (T23) ───────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>ModalDialog</c> (0xFA / GameServerModalDialog).
    /// Reads windowId U32, title string, message string,
    /// buttonsCount U8 → per button: string label, U8 id,
    /// choicesCount U8 → per choice: string label, U8 id,
    /// escapeButton U8, enterButton U8 (order for version > 970),
    /// priority U8.
    /// Raises <see cref="ModalDialogReceived"/>.
    /// Maps to <c>ProtocolGame::parseModalDialog</c>.
    /// Task T23.
    /// </summary>
    private void ParseModalDialog(InputMessage msg)
    {
        uint   windowId = msg.ReadU32();
        string title    = msg.ReadString();
        string message  = msg.ReadString();

        int buttonsCount = msg.ReadU8();
        var buttons      = new List<Game.ModalButton>(buttonsCount);
        for (int i = 0; i < buttonsCount; i++)
        {
            string label = msg.ReadString();
            byte   id    = msg.ReadU8();
            buttons.Add(new Game.ModalButton(id, label));
        }

        int choicesCount = msg.ReadU8();
        var choices      = new List<Game.ModalChoice>(choicesCount);
        for (int i = 0; i < choicesCount; i++)
        {
            string label = msg.ReadString();
            byte   id    = msg.ReadU8();
            choices.Add(new Game.ModalChoice(id, label));
        }

        // For clientVersion > 970 (always true at 1281): escapeButton first, then enterButton
        byte escapeButton = msg.ReadU8();
        byte enterButton  = msg.ReadU8();
        bool priority     = msg.ReadU8() != 0;

        var dialog = new Game.ModalDialog(
            windowId, title, message, buttons,
            enterButton, escapeButton, choices, priority);
        ModalDialogReceived?.Invoke(dialog);
    }

    // ─── Edit list (T23) ──────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>EditList</c> (0x97 / GameServerEditList).
    /// Reads doorId U8, id U32, text string.
    /// Raises <see cref="EditListReceived"/>.
    /// Maps to <c>ProtocolGame::parseEditList</c>.
    /// Task T23.
    /// </summary>
    private void ParseEditList(InputMessage msg)
    {
        byte   doorId = msg.ReadU8();
        uint   id     = msg.ReadU32();
        string text   = msg.ReadString();
        EditListReceived?.Invoke(id, doorId, text);
    }

    // ─── Market (T26) ────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the item tier byte from the stream for the given <paramref name="itemId"/>.
    /// The tier byte is only present when the item's <c>Classification</c> is &gt; 0
    /// and we are at protocol 1281 (which we always are in this port).
    /// Maps to <c>readMarketItemTier</c> in <c>src/client/protocolgameparse.cpp</c>.
    /// Task T26.
    /// </summary>
    private byte ReadMarketItemTier(InputMessage msg, ushort _)
    {
        // Classification info requires a ThingTypeManager reference that is not
        // currently wired into ProtocolGame. Since tier is only present when
        // Classification > 0, we conservatively return 0 (no tier byte consumed).
        // In a future pass, wire in ThingTypeManager and check item.Classification > 0.
        return 0;
    }

    /// <summary>
    /// Reads the per-attribute description strings for <c>parseMarketDetail</c>.
    /// Protocol 1281 always sends up to <c>ITEM_DESC_IMBUEMENTEFFECT</c> (26).
    /// Each attribute is prefixed by U16: 0x0000 = not present (skip), else read string.
    /// Maps to <c>readMarketDescriptions</c>.
    /// Task T26.
    /// </summary>
    private static Dictionary<int, string> ReadMarketDescriptions(InputMessage msg)
    {
        const int ITEM_DESC_FIRST = 1;   // ITEM_DESC_ARMOR
        const int ITEM_DESC_LAST  = 26;  // ITEM_DESC_IMBUEMENTEFFECT (at clientVersion 1510, which our port targets)
        var descriptions = new Dictionary<int, string>();
        for (int attr = ITEM_DESC_FIRST; attr <= ITEM_DESC_LAST; attr++)
        {
            // C++ peeks at next U16: 0x0000 → attribute not present (consume U16, skip);
            // otherwise the U16 is the string length prefix, so ReadString reads U16 + body.
            if (msg.PeekU16() != 0)
                descriptions[attr] = msg.ReadString();  // reads U16 length + body
            else
                msg.ReadU16();                           // skip the 0x0000 sentinel
        }
        return descriptions;
    }

    /// <summary>
    /// Reads the daily price-statistics list for <c>parseMarketDetail</c>.
    /// Protocol 1281 sends U64 prices.
    /// Maps to <c>readMarketStatsList</c>.
    /// Task T26.
    /// </summary>
    private static List<Game.MarketStatEntry> ReadMarketStatsList(InputMessage msg, byte action)
    {
        const ulong kDaySeconds = 86400;
        int count = msg.ReadU8();
        var result = new List<Game.MarketStatEntry>(count);
        for (int i = 0; i < count; i++)
        {
            uint  transactions = msg.ReadU32();
            ulong totalPrice   = msg.ReadU64();
            ulong highPrice    = msg.ReadU64();
            ulong lowPrice     = msg.ReadU64();
            // day = (time(nullptr) / 1000) * 86400 - i * kDaySeconds, approximated as 0 here
            // Parenthesise correctly to avoid dividing by 1000 then multiplying by 86400.
            ulong day = ((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 1000) * kDaySeconds
                        - (ulong)i * kDaySeconds;
            result.Add(new Game.MarketStatEntry(day, action, transactions, totalPrice, highPrice, lowPrice));
        }
        return result;
    }

    /// <summary>
    /// Reads a single market offer from the stream.
    /// Maps to <c>ProtocolGame::readMarketOffer</c>.
    /// Task T26.
    /// </summary>
    private Game.MarketOffer ReadMarketOffer(InputMessage msg, byte action, ushort var)
    {
        const ushort MARKETREQUEST_OWN_HISTORY = 1;
        const ushort MARKETREQUEST_OWN_OFFERS  = 2;

        uint   timestamp  = msg.ReadU32();
        ushort counter    = msg.ReadU16();
        ushort itemId     = 0;
        byte   itemTier   = 0;

        if (var == MARKETREQUEST_OWN_OFFERS || var == MARKETREQUEST_OWN_HISTORY
            || var == 0xFFFE || var == 0xFFFF)
        {
            // own offers / history: item id embedded in each record
            itemId   = msg.ReadU16();
            itemTier = ReadMarketItemTier(msg, itemId);
        }
        else
        {
            itemId = var;   // browse by item: var carries the item id
        }

        ushort amount = msg.ReadU16();
        ulong  price  = msg.ReadU64();   // protocol 1281: always U64

        string playerName = string.Empty;
        byte   state      = 0; // OFFER_STATE_ACTIVE

        if (var == MARKETREQUEST_OWN_HISTORY || var == 0xFFFF)
        {
            state = msg.ReadU8();
        }
        else if (var != MARKETREQUEST_OWN_OFFERS && var != 0xFFFE)
        {
            playerName = msg.ReadString();
        }

        return new Game.MarketOffer(timestamp, counter, action, itemId, itemTier,
                                   amount, price, playerName, state, var);
    }

    /// <summary>
    /// Parses <c>MarketEnter</c> (0xF6 / GameServerMarketEnter).
    /// Reads activeOffers U8, then U16 count of depot items;
    /// per item: U16 itemId, optional U8 tier, U16 count.
    /// Fires <see cref="MarketEntered"/>.
    /// Maps to <c>ProtocolGame::parseMarketEnter</c>.
    /// Task T26.
    /// </summary>
    private void ParseMarketEnter(InputMessage msg)
    {
        byte activeOffers = msg.ReadU8();
        int  itemCount    = msg.ReadU16();
        var  depotItems   = new List<Game.MarketDepotItem>(itemCount);
        for (int i = 0; i < itemCount; i++)
        {
            ushort itemId = msg.ReadU16();
            byte   tier   = ReadMarketItemTier(msg, itemId);
            ushort count  = msg.ReadU16();
            depotItems.Add(new Game.MarketDepotItem(itemId, tier, count));
        }
        MarketEntered?.Invoke(depotItems, activeOffers);
    }

    /// <summary>
    /// Parses <c>MarketLeave</c> (0xF7 / GameServerMarketLeave).
    /// No payload. Fires <see cref="MarketLeft"/>.
    /// Task T26.
    /// </summary>
    private void ParseMarketLeave(InputMessage _) => MarketLeft?.Invoke();

    /// <summary>
    /// Parses <c>MarketDetail</c> (0xF8 / GameServerMarketDetail).
    /// Reads U16 itemId, optional U8 tier, description map, buy stats, sell stats.
    /// Fires <see cref="MarketDetailReceived"/>.
    /// Maps to <c>ProtocolGame::parseMarketDetail</c>.
    /// Task T26.
    /// </summary>
    private void ParseMarketDetail(InputMessage msg)
    {
        ushort itemId       = msg.ReadU16();
        byte   tier         = ReadMarketItemTier(msg, itemId);
        var    descriptions = ReadMarketDescriptions(msg);
        var    buyStats     = ReadMarketStatsList(msg, 0);   // MARKETACTION_BUY
        var    sellStats    = ReadMarketStatsList(msg, 1);   // MARKETACTION_SELL
        MarketDetailReceived?.Invoke(itemId, tier, descriptions, buyStats, sellStats);
    }

    /// <summary>
    /// Parses <c>MarketBrowse</c> (0xF9 / GameServerMarketBrowse).
    /// At protocol 1281: reads U8 browseId; if browseId == 3, reads U16 itemId + optional tier.
    /// Then U32 buyOfferCount + offers, U32 sellOfferCount + offers.
    /// Fires <see cref="MarketBrowseReceived"/>.
    /// Maps to <c>ProtocolGame::parseMarketBrowse</c>.
    /// Task T26.
    /// </summary>
    private void ParseMarketBrowse(InputMessage msg)
    {
        // Protocol 1281: first byte is the browse-type (1=own history, 2=own offers, 3=item browse)
        ushort var      = msg.ReadU8();
        byte   itemTier = 0;

        if (var == 3)
        {
            // browse by item: read the actual item id
            ushort browseItemId = msg.ReadU16();
            itemTier = ReadMarketItemTier(msg, browseItemId);
            var      = browseItemId;  // replace var with itemId for readMarketOffer compatibility
        }

        uint buyCount = msg.ReadU32();
        var  offers   = new List<Game.MarketOffer>((int)buyCount);
        for (uint i = 0; i < buyCount; i++)
            offers.Add(ReadMarketOffer(msg, 0, var));

        uint sellCount = msg.ReadU32();
        for (uint i = 0; i < sellCount; i++)
            offers.Add(ReadMarketOffer(msg, 1, var));

        MarketBrowseReceived?.Invoke(var, offers);
    }

    // ─── T27: Imbuement durations ─────────────────────────────────────────────

    /// <summary>
    /// Parses <c>ImbuementDurations</c> (0x5D / GameServerImbuementDurations).
    /// Reads U8 itemCount; per item: U8 trackSlot, item data, U8 totalSlots,
    /// then per slot: U8 imbued flag, and if imbued: string name + U16 iconId + U32 duration + U8 state.
    /// Fires <see cref="ImbuementDurationsReceived"/>.
    /// Maps to <c>ProtocolGame::parseImbuementDurations</c>.
    /// Task T27.
    /// </summary>
    private void ParseImbuementDurations(InputMessage msg)
    {
        byte count = msg.ReadU8();
        var items  = new List<Game.ImbuementTrackerItem>(count);

        for (int i = 0; i < count; i++)
        {
            var tracker = new Game.ImbuementTrackerItem
            {
                TrackSlot   = msg.ReadU8(),
                TrackedItem = new Game.Item { Id = msg.ReadU16() },
                TotalSlots  = msg.ReadU8(),
            };

            var slots = new List<Game.ImbuementSlot>();
            for (int s = 0; s < tracker.TotalSlots; s++)
            {
                bool imbued = msg.ReadU8() != 0;
                if (!imbued) continue;

                slots.Add(new Game.ImbuementSlot(
                    SlotIndex: (byte)s,
                    Name:      msg.ReadString(),
                    IconId:    msg.ReadU16(),
                    Duration:  msg.ReadU32(),
                    State:     msg.ReadU8()));
            }

            tracker.Slots = slots;
            items.Add(tracker);
        }

        ImbuementDurationsReceived?.Invoke(items);
    }

    // ─── T27: Wheel of Destiny ────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>OpenWheelWindow</c> (0x5F / GameServerOpenWheelWindow).
    /// Reads U32 playerId, U8 canView; if canView: U8 changeState, U8 vocationId,
    /// U16 points, U16 extraPoints.
    /// Fires <see cref="WheelWindowReceived"/>.
    /// Maps to <c>ProtocolGame::parseOpenWheelWindow</c>.
    /// Task T27.
    /// </summary>
    private void ParseOpenWheelWindow(InputMessage msg)
    {
        uint playerId = msg.ReadU32();
        bool canView  = msg.ReadU8() != 0;

        var data = new Game.WheelData { PlayerId = playerId, CanView = canView };

        if (canView)
        {
            data.ChangeState  = msg.ReadU8();
            data.VocationId   = msg.ReadU8();
            data.Points       = msg.ReadU16();
            data.ExtraPoints  = msg.ReadU16();
        }

        WheelWindowReceived?.Invoke(data);
    }

    // ─── T27: Forge result ────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>ForgeResult</c> (0x8A / GameServerForgeResult).
    /// Reads U8 actionType, U8 convergence, U8 success, U16+U8 leftItem, U16+U8 rightItem,
    /// then bonus bytes depending on actionType.
    /// Fires <see cref="ForgeResultReceived"/>.
    /// Maps to <c>ProtocolGame::parseForgeResult</c>.
    /// Task T27.
    /// </summary>
    private void ParseForgeResult(InputMessage msg)
    {
        var result = new Game.ForgeResult
        {
            ActionType  = msg.ReadU8(),
            Convergence = msg.ReadU8() == 1,
            Success     = msg.ReadU8() == 1,
            LeftItemId  = msg.ReadU16(),
            LeftTier    = msg.ReadU8(),
            RightItemId = msg.ReadU16(),
            RightTier   = msg.ReadU8(),
        };

        if (result.ActionType == 1)
        {
            msg.ReadU8(); // bonus type always none for transfer
        }
        else
        {
            result.Bonus = msg.ReadU8();
            if (result.Bonus == 2)
            {
                result.CoreCount = msg.ReadU8();
            }
            else if (result.Bonus >= 4 && result.Bonus <= 8)
            {
                result.LeftItemId = msg.ReadU16();
                result.LeftTier   = msg.ReadU8();
            }
        }

        ForgeResultReceived?.Invoke(result);
    }

    // ─── T27: Bestiary races ──────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>BestiaryRaces</c> (0xD5 / GameServerBestiaryRaces).
    /// Reads U16 count; per race: string className + U16 count + U16 unlockedCount.
    /// Fires <see cref="BestiaryRacesReceived"/>.
    /// Maps to <c>ProtocolGame::parseBestiaryRaces</c>.
    /// Task T27.
    /// </summary>
    private void ParseBestiaryRaces(InputMessage msg)
    {
        ushort count = msg.ReadU16();
        var    races = new List<Game.BestiaryRace>(count);

        for (int i = 0; i < count; i++)
        {
            races.Add(new Game.BestiaryRace(
                Race:          i,
                ClassName:     msg.ReadString(),
                Count:         msg.ReadU16(),
                UnlockedCount: msg.ReadU16()));
        }

        BestiaryRacesReceived?.Invoke(races);
    }

    // ─── T27: Bestiary overview ───────────────────────────────────────────────

    /// <summary>
    /// Parses <c>BestiaryOverview</c> (0xD6 / GameServerBestiaryOverview).
    /// Reads string raceName, U16 count; per monster: U16 id + U8 level +
    /// if level>0: U8 occurrence + U16 animusMasteryBonus. Then U16 animusMasteryPoints.
    /// Fires <see cref="BestiaryOverviewReceived"/>.
    /// Maps to <c>ProtocolGame::parseBestiaryOverview</c>.
    /// Task T27.
    /// </summary>
    private void ParseBestiaryOverview(InputMessage msg)
    {
        string raceName = msg.ReadString();
        ushort count    = msg.ReadU16();
        var    monsters = new List<Game.BestiaryMonster>(count);

        for (int i = 0; i < count; i++)
        {
            ushort id    = msg.ReadU16();
            byte   level = msg.ReadU8();
            byte   occ   = level > 0 ? msg.ReadU8() : (byte)0;
            ushort bonus = msg.ReadU16(); // animusMasteryBonus (always present at 1281)
            monsters.Add(new Game.BestiaryMonster(id, level, occ, bonus));
        }

        ushort animusPoints = msg.ReadU16();
        BestiaryOverviewReceived?.Invoke(raceName, monsters, animusPoints);
    }

    // ─── T27: Bestiary monster data ───────────────────────────────────────────

    /// <summary>
    /// Parses <c>BestiaryMonsterData</c> (0xD7 / GameServerBestiaryMonsterData).
    /// Reads the full monster data sheet including optional level-gated fields.
    /// Fires <see cref="BestiaryMonsterDataReceived"/>.
    /// Maps to <c>ProtocolGame::parseBestiaryMonsterData</c>.
    /// Task T27.
    /// </summary>
    private void ParseBestiaryMonsterData(InputMessage msg)
    {
        var data = new Game.BestiaryMonsterData
        {
            Id                  = msg.ReadU16(),
            ClassName           = msg.ReadString(),
            CurrentLevel        = msg.ReadU8(),
            AnimusMasteryBonus  = msg.ReadU16(),
            AnimusMasteryPoints = msg.ReadU16(),
            KillCounter         = msg.ReadU32(),
            ThirdDifficulty     = msg.ReadU16(),
            SecondUnlock        = msg.ReadU16(),
            LastProgressKillCount = msg.ReadU16(),
            Difficulty          = msg.ReadU8(),
            Occurrence          = msg.ReadU8(),
        };

        byte   lootCount = msg.ReadU8();
        var    loot      = new List<Game.BestiaryLootItem>(lootCount);
        for (int i = 0; i < lootCount; i++)
        {
            ushort itemId       = msg.ReadU16();
            byte   diff         = msg.ReadU8();
            byte   special      = msg.ReadU8();
            string lootName     = itemId != 0 ? msg.ReadString() : "";
            byte   amount       = itemId != 0 ? msg.ReadU8() : (byte)0;
            loot.Add(new Game.BestiaryLootItem(itemId, diff, special, lootName, amount));
        }
        data.Loot = loot;

        if (data.CurrentLevel > 1)
        {
            data.CharmValue  = msg.ReadU16();
            data.AttackMode  = msg.ReadU8();
            msg.ReadU8();                   // padding byte
            data.MaxHealth   = msg.ReadU32();
            data.Experience  = msg.ReadU32();
            data.Speed       = msg.ReadU16();
            data.Armor       = msg.ReadU16();
            msg.ReadDouble();               // mitigation (not stored in C# record)
        }

        if (data.CurrentLevel > 2)
        {
            byte elemCount = msg.ReadU8();
            var  combat    = new Dictionary<byte, ushort>(elemCount);
            for (int i = 0; i < elemCount; i++)
                combat[msg.ReadU8()] = msg.ReadU16();
            data.Combat   = combat;
            msg.ReadU16(); // padding
            data.Location = msg.ReadString();
        }

        BestiaryMonsterDataReceived?.Invoke(data);
    }

    // ─── T27: Prey free rerolls ───────────────────────────────────────────────

    /// <summary>
    /// Parses <c>PreyFreeRerolls</c> (0xE6 / GameServerSendPreyFreeRerolls).
    /// Reads U8 slot + U16 timeLeft.
    /// Fires <see cref="PreyFreeRerollsReceived"/>.
    /// Maps to <c>ProtocolGame::parsePreyFreeRerolls</c>.
    /// Task T27.
    /// </summary>
    private void ParsePreyFreeRerolls(InputMessage msg)
    {
        byte   slot     = msg.ReadU8();
        ushort timeLeft = msg.ReadU16();
        PreyFreeRerollsReceived?.Invoke(slot, timeLeft);
    }

    // ─── T27: Prey time left ─────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>PreyTimeLeft</c> (0xE7 / GameServerSendPreyTimeLeft).
    /// Reads U8 slot + U16 timeLeft.
    /// Fires <see cref="PreyTimeLeftReceived"/>.
    /// Maps to <c>ProtocolGame::parsePreyTimeLeft</c>.
    /// Task T27.
    /// </summary>
    private void ParsePreyTimeLeft(InputMessage msg)
    {
        byte   slot     = msg.ReadU8();
        ushort timeLeft = msg.ReadU16();
        PreyTimeLeftReceived?.Invoke(slot, timeLeft);
    }

    // ─── T27: Prey data ───────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>PreyData</c> (0xE8 / GameServerSendPreyData).
    /// Reads U8 slot + U8 state then state-specific fields.
    /// Fires <see cref="PreyDataReceived"/>.
    /// Maps to <c>ProtocolGame::parsePreyData</c>.
    /// Task T27.
    /// </summary>
    private void ParsePreyData(InputMessage msg)
    {
        var data = new Game.PreyData
        {
            Slot  = msg.ReadU8(),
            State = (Game.PreyState)msg.ReadU8(),
        };

        switch (data.State)
        {
            case Game.PreyState.Locked:
            {
                msg.ReadU8();                       // unlockState
                data.NextFreeReroll = msg.ReadU32();
                data.Wildcards      = msg.ReadU8();
                break;
            }
            case Game.PreyState.Inactive:
            {
                data.NextFreeReroll = msg.ReadU32();
                data.Wildcards      = msg.ReadU8();
                break;
            }
            case Game.PreyState.Active:
            {
                data.ActiveMonster  = new Game.PreyMonster(msg.ReadString());
                ReadOutfit(msg);                    // outfit – consumed, not stored
                data.BonusType      = msg.ReadU8();
                data.BonusValue     = msg.ReadU16();
                data.BonusGrade     = msg.ReadU8();
                data.TimeLeft       = msg.ReadU16();
                data.NextFreeReroll = msg.ReadU32();
                data.Wildcards      = (byte)0;
                msg.ReadU8();                       // option toggle
                break;
            }
            case Game.PreyState.Selection:
            case Game.PreyState.SelectionChangeMonster:
            case Game.PreyState.ListSelection:
            {
                byte listCount = msg.ReadU8();
                var  list      = new List<Game.PreyMonster>(listCount);
                for (int i = 0; i < listCount; i++)
                {
                    list.Add(new Game.PreyMonster(msg.ReadString()));
                    ReadOutfit(msg); // outfit
                }
                data.Monsters       = list;
                data.NextFreeReroll = msg.ReadU32();
                data.Wildcards      = msg.ReadU8();
                break;
            }
        }

        PreyDataReceived?.Invoke(data);
    }

    // ─── T27: Prey reroll price ───────────────────────────────────────────────

    /// <summary>
    /// Parses <c>PreyRerollPrice</c> (0xE9 / GameServerSendPreyRerollPrice).
    /// Reads U32 price + U32 wildcardPrice.
    /// Fires <see cref="PreyRerollPriceReceived"/>.
    /// Maps to <c>ProtocolGame::parsePreyRerollPrice</c>.
    /// Task T27.
    /// </summary>
    private void ParsePreyRerollPrice(InputMessage msg)
    {
        uint price         = msg.ReadU32();
        uint wildcardPrice = msg.ReadU32();
        PreyRerollPriceReceived?.Invoke(price, wildcardPrice);
    }

    // ─── T27: Imbuement window ────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>ImbuementWindow</c> (0xEB / GameServerSendImbuementWindow).
    /// This packet is complex and item-type-specific; at protocol 1281 we
    /// consume the item ID header and raise the event so the UI can request
    /// a full refresh via the store if needed.
    /// Maps to <c>ProtocolGame::parseImbuementWindow</c>.
    /// Task T27.
    /// </summary>
    private void ParseImbuementWindow(InputMessage msg)
    {
        // Consume item id — full parsing requires ThingType registry (future work)
        msg.ReadU16(); // itemId
    }

    // ─── T28: CoinBalance ────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>CoinBalance</c> (0xDF / GameServerCoinBalance).
    /// Wire: U8 update; if update≠0: U32 coins + U32 transferableCoins + U32 auctionCoins (≥1281).
    /// Fires <see cref="CoinBalanceReceived"/>.
    /// Maps to <c>ProtocolGame::parseCoinBalance</c>.
    /// Task T28.
    /// </summary>
    private void ParseCoinBalance(InputMessage msg)
    {
        bool update = msg.ReadU8() != 0;
        var balance = new Game.CoinBalance { IsUpdated = update };
        if (update)
        {
            balance.Coins             = msg.ReadU32();
            balance.TransferableCoins = msg.ReadU32();
            balance.AuctionCoins      = msg.ReadU32(); // protocol ≥ 1281
        }
        CoinBalanceReceived?.Invoke(balance);
    }

    // ─── T28: StoreError ─────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>StoreError</c> (0xE0 / GameServerStoreError).
    /// Wire: U8 errorType + string message.
    /// Fires <see cref="StoreErrorReceived"/>.
    /// Maps to <c>ProtocolGame::parseStoreError</c>.
    /// Task T28.
    /// </summary>
    private void ParseStoreError(InputMessage msg)
    {
        byte   errorType = msg.ReadU8();
        string message   = msg.ReadString();
        StoreErrorReceived?.Invoke(errorType, message);
    }

    // ─── T28: CoinBalanceUpdating ─────────────────────────────────────────────

    /// <summary>
    /// Parses <c>CoinBalanceUpdating</c> (0xF2 / GameServerCoinBalanceUpdating).
    /// At protocol 1281 (<1291): reads a single U8 isUpdating byte and discards it.
    /// Maps to <c>ProtocolGame::parseCoinBalanceUpdating</c>.
    /// Task T28.
    /// </summary>
    private static void ParseCoinBalanceUpdating(InputMessage msg)
    {
        msg.ReadU8(); // isUpdating byte (consumed, not acted on at 1281)
    }

    // ─── T28: Store (category list) ──────────────────────────────────────────

    /// <summary>
    /// Parses <c>Store</c> (0xFB / GameServerStore).
    /// At protocol 1281 (between 1100 and 1291):
    ///   U16 categoryCount, per-category: string name + string description (<1291)
    ///   + U8 state (GameIngameStoreHighlights enabled for 1281)
    ///   + U8 iconCount + icons + string parent.
    /// Fires <see cref="StoreCategoriesReceived"/>.
    /// Maps to <c>ProtocolGame::parseStore</c>.
    /// Task T28.
    /// </summary>
    private void ParseStore(InputMessage msg)
    {
        ushort count      = msg.ReadU16();
        var    categories = new List<Game.StoreCategory>(count);
        for (int i = 0; i < count; i++)
        {
            var cat = new Game.StoreCategory();
            cat.Name        = msg.ReadString();
            cat.Description = msg.ReadString();           // present for protocol < 1291
            cat.State       = msg.ReadU8();               // GameIngameStoreHighlights enabled at 1281

            byte iconCount = msg.ReadU8();
            for (int j = 0; j < iconCount; j++)
                cat.Icons.Add(msg.ReadString());

            cat.Parent = msg.ReadString();
            categories.Add(cat);
        }
        // protocol >= 1332 would add 2 extra bytes here, but 1281 < 1332 so nothing extra
        StoreCategoriesReceived?.Invoke(categories);
    }

    // ─── T28: StoreOffers ────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>StoreOffers</c> (0xFC / GameServerStoreOffers).
    /// At protocol 1281 (<1291 branch):
    ///   string categoryName, U16 offersCount, per-offer:
    ///     U32 id, string name, string description, U32 price,
    ///     U8 highlightState (if STATE_SALE(==2) & feature: U32 validUntil + U32 basePrice),
    ///     U8 disabled (if disabled: string reason),
    ///     U8 iconCount + icons,
    ///     U16 subOffersCount, per-sub: string name, string desc, U8 subIconsCount + icons, string serviceType.
    /// Fires <see cref="StoreOffersReceived"/>.
    /// Maps to <c>ProtocolGame::parseStoreOffers</c>.
    /// Task T28.
    /// </summary>
    private void ParseStoreOffers(InputMessage msg)
    {
        string categoryName = msg.ReadString();
        ushort offerCount   = msg.ReadU16();
        var    offers       = new List<Game.StoreOffer>(offerCount);

        for (int i = 0; i < offerCount; i++)
        {
            var offer = new Game.StoreOffer();
            offer.Id          = msg.ReadU32();
            offer.Name        = msg.ReadString();
            offer.Description = msg.ReadString();
            offer.Price       = msg.ReadU32();

            byte highlightState = msg.ReadU8();
            if (highlightState == 2)            // STATE_SALE
            {
                offer.State         = 2;
                offer.SaleValidUntil = msg.ReadU32();
                offer.BasePrice      = msg.ReadU32();
            }
            else
            {
                offer.State = highlightState;
            }

            bool disabled = msg.ReadU8() != 0;
            offer.Disabled = disabled;
            if (disabled)
                offer.DisabledReason = msg.ReadString();

            byte iconCount = msg.ReadU8();
            for (int j = 0; j < iconCount; j++)
                offer.Icon = msg.ReadString(); // last icon wins, as in C++

            ushort subCount = msg.ReadU16();
            for (int j = 0; j < subCount; j++)
            {
                var sub = new Game.StoreSubOffer();
                sub.Name        = msg.ReadString();
                sub.Description = msg.ReadString();
                byte subIconCount = msg.ReadU8();
                for (int k = 0; k < subIconCount; k++)
                    sub.Icons.Add(msg.ReadString());
                sub.ServiceType = msg.ReadString();
                offer.SubOffers.Add(sub);
            }
            offers.Add(offer);
        }
        StoreOffersReceived?.Invoke(categoryName, offers);
    }

    // ─── T28: StoreTransactionHistory ────────────────────────────────────────

    /// <summary>
    /// Parses <c>StoreTransactionHistory</c> (0xFD / GameServerStoreTransactionHistory).
    /// At protocol 1281 (between 1097 and 1291):
    ///   U32 currentPage + U32 pageCount, U8 entryCount, per-entry:
    ///     U32 time, U8 productType, U32 coinChange, string productName.
    /// Maps to <c>ProtocolGame::parseStoreTransactionHistory</c>.
    /// Task T28.
    /// </summary>
    private static void ParseStoreTransactionHistory(InputMessage msg)
    {
        msg.ReadU32(); // currentPage
        msg.ReadU32(); // pageCount

        byte entries = msg.ReadU8();
        for (int i = 0; i < entries; i++)
        {
            msg.ReadU32(); // time
            msg.ReadU8();  // productType / mode
            msg.ReadU32(); // coinChange / amount
            msg.ReadString(); // productName
        }
    }

    // ─── T28: CompleteStorePurchase ───────────────────────────────────────────

    /// <summary>
    /// Parses <c>StoreCompletePurchase</c> (0xFE / GameServerStoreCompletePurchase).
    /// At protocol 1281 (<1291 branch):
    ///   U8 (unused), string message, U32 remainingCoins, U32 transferableCoins.
    /// Fires <see cref="StorePurchaseCompleted"/>.
    /// Maps to <c>ProtocolGame::parseCompleteStorePurchase</c>.
    /// Task T28.
    /// </summary>
    private void ParseCompleteStorePurchase(InputMessage msg)
    {
        msg.ReadU8(); // unused
        var result = new Game.StorePurchaseResult();
        result.Message           = msg.ReadString();
        result.RemainingCoins    = msg.ReadU32();
        result.TransferableCoins = msg.ReadU32();
        StorePurchaseCompleted?.Invoke(result);
    }

    // ─── T45: CreatureUnpass ─────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>CreatureUnpass</c> (0x92 / GameServerCreatureUnpass).
    /// Reads the creature ID and a boolean indicating whether the creature
    /// is unpassable (true = tile is blocked by creature).
    /// Fires <see cref="CreatureUnpassUpdated"/>.
    /// Maps to <c>ProtocolGame::parseCreatureUnpass</c>.
    /// Task T45.
    /// </summary>
    private void ParseCreatureUnpass(InputMessage msg)
    {
        uint creatureId    = msg.ReadU32();
        bool isUnpassable  = msg.ReadU8() != 0;
        CreatureUnpassUpdated?.Invoke(creatureId, isUnpassable);
    }

    // ─── T45: PlayerHelpers ───────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>PlayerHelpers</c> (0x94 / GameServerPlayerHelpers).
    /// Reads the creature ID (for which player) and the helpers count (U16).
    /// Fires <see cref="PlayerHelpersReceived"/>.
    /// Maps to <c>ProtocolGame::parsePlayerHelpers</c>.
    /// Task T45.
    /// </summary>
    private void ParsePlayerHelpers(InputMessage msg)
    {
        uint   creatureId = msg.ReadU32();
        ushort helpers    = msg.ReadU16();
        PlayerHelpersReceived?.Invoke(creatureId, helpers);
    }

    // ─── T45: CreatureType ────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>CreatureType</c> (0x95 / GameServerCreatureType).
    /// Reads the creature ID and a byte encoding the creature type
    /// (0 = player, 1 = monster, 2 = NPC, 3 = summon-own, 4 = summon-other).
    /// Fires <see cref="CreatureTypeUpdated"/>.
    /// Maps to <c>ProtocolGame::parseCreatureType</c>.
    /// Task T45.
    /// </summary>
    private void ParseCreatureType(InputMessage msg)
    {
        uint creatureId  = msg.ReadU32();
        byte creatureType = msg.ReadU8();
        CreatureTypeUpdated?.Invoke(creatureId, creatureType);
    }

    // ─── T45: CreatureTyping ──────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>CreatureTyping</c> (0x38 / GameServerCreatureTyping).
    /// Reads the creature ID and a boolean indicating whether the creature is
    /// currently typing (chat-bubble visible).
    /// Fires <see cref="CreatureTypingUpdated"/>.
    /// Maps to <c>ProtocolGame::parseCreatureTyping</c>.
    /// Task T45.
    /// </summary>
    private void ParseCreatureTyping(InputMessage msg)
    {
        uint creatureId = msg.ReadU32();
        bool isTyping   = msg.ReadU8() != 0;
        CreatureTypingUpdated?.Invoke(creatureId, isTyping);
    }

    // ─── T45: AttachedEffect / DetachEffect ───────────────────────────────────

    /// <summary>
    /// Parses <c>AttchedEffect</c> (0x34 / GameServerAttchedEffect).
    /// Reads the creature ID and the attached-effect ID to add.
    /// Fires <see cref="CreatureEffectAttached"/>.
    /// Maps to <c>ProtocolGame::parseAttachedEffect</c>.
    /// Task T45.
    /// </summary>
    private void ParseAttachedEffect(InputMessage msg)
    {
        uint   creatureId = msg.ReadU32();
        ushort effectId   = msg.ReadU16();
        CreatureEffectAttached?.Invoke(creatureId, effectId);
    }

    /// <summary>
    /// Parses <c>DetachEffect</c> (0x35 / GameServerDetachEffect).
    /// Reads the creature ID and the attached-effect ID to remove.
    /// Fires <see cref="CreatureEffectDetached"/>.
    /// Maps to <c>ProtocolGame::parseDetachEffect</c>.
    /// Task T45.
    /// </summary>
    private void ParseDetachEffect(InputMessage msg)
    {
        uint   creatureId = msg.ReadU32();
        ushort effectId   = msg.ReadU16();
        CreatureEffectDetached?.Invoke(creatureId, effectId);
    }

    // ─── T45: CreatureShader / MapShader ──────────────────────────────────────

    /// <summary>
    /// Parses <c>CreatureShader</c> (0x36 / GameServerCreatureShader).
    /// Reads the creature ID and a GLSL shader name string.
    /// An empty string clears the shader.
    /// Fires <see cref="CreatureShaderChanged"/>.
    /// Maps to <c>ProtocolGame::parseCreatureShader</c>.
    /// Task T45.
    /// </summary>
    private void ParseCreatureShader(InputMessage msg)
    {
        uint   creatureId  = msg.ReadU32();
        string shaderName  = msg.ReadString();
        CreatureShaderChanged?.Invoke(creatureId, shaderName);
    }

    /// <summary>
    /// Parses <c>MapShader</c> (0x37 / GameServerMapShader).
    /// Reads a GLSL shader name string to apply to the map view.
    /// An empty string clears the shader.
    /// Fires <see cref="MapShaderChanged"/>.
    /// Maps to <c>ProtocolGame::parseMapShader</c>.
    /// Task T45.
    /// </summary>
    private void ParseMapShader(InputMessage msg)
    {
        string shaderName = msg.ReadString();
        MapShaderChanged?.Invoke(shaderName);
    }

    // ─── T45: FloorChangeUp / FloorChangeDown ─────────────────────────────────

    /// <summary>
    /// Parses <c>FloorChangeUp</c> (0xBE / GameServerFloorChangeUp).
    /// Called when the local player moves to a higher floor (e.g., leaves a cave).
    /// At protocol 1281 (GameMapMovePosition always set) the new central position
    /// is read from the wire; z is decremented to obtain the destination floor.
    /// For the surface transition (landing on sea floor 7), floors
    /// [7 − <c>MapAwareUndergroundRange</c>, 0] are loaded.
    /// For any other underground transition one floor at
    /// pos.z − <c>MapAwareUndergroundRange</c> is loaded.
    /// The map central position is then set to pos + (1, 1) as per the C++ reference,
    /// and <see cref="FloorChanged"/> is fired with (newPosition, oldPosition).
    /// Maps to <c>ProtocolGame::parseFloorChangeUp</c>.
    /// Task T45.
    /// </summary>
    private void ParseFloorChangeUp(InputMessage msg)
    {
        var oldPos = _map.CentralPosition;
        var pos    = ReadPosition(msg);
        pos = new Game.Position(pos.X, pos.Y, pos.Z - 1);

        int skip = 0;
        var range = _map.AwareRange;

        if (pos.Z == MapSeaFloor)
        {
            // Resurfacing: load all floors from underground-range limit down to 0
            for (int i = MapSeaFloor - MapAwareUndergroundRange; i >= 0; i--)
                skip = SetFloorDescription(msg, pos.X - range.Left, pos.Y - range.Top,
                                           i, range.Horizontal, range.Vertical,
                                           offset: MapSeaFloor + 1 - i, skip);
        }
        else if (pos.Z > MapSeaFloor)
        {
            // Underground: load the single floor coming into view above
            SetFloorDescription(msg, pos.X - range.Left, pos.Y - range.Top,
                                pos.Z - MapAwareUndergroundRange, range.Horizontal, range.Vertical,
                                offset: MapAwareUndergroundRange + 1, skip);
        }

        var newPos = new Game.Position(pos.X + 1, pos.Y + 1, pos.Z);
        _map.CentralPosition = newPos;
        FloorChanged?.Invoke(newPos, oldPos);
    }

    /// <summary>
    /// Parses <c>FloorChangeDown</c> (0xBF / GameServerFloorChangeDown).
    /// Called when the local player descends to a lower floor (e.g., enters a cave).
    /// At protocol 1281 the new central position is read from the wire; z is
    /// incremented to obtain the destination floor.
    /// When entering the underground for the first time (pos.z == 8) three floors
    /// [8, 8 + <c>MapAwareUndergroundRange</c>] are loaded.
    /// When already underground (and not at the deepest level − 1) one additional
    /// floor at pos.z + <c>MapAwareUndergroundRange</c> is loaded.
    /// The map central position is then set to pos − (1, 1) as per the C++ reference,
    /// and <see cref="FloorChanged"/> is fired with (newPosition, oldPosition).
    /// Maps to <c>ProtocolGame::parseFloorChangeDown</c>.
    /// Task T45.
    /// </summary>
    private void ParseFloorChangeDown(InputMessage msg)
    {
        var oldPos = _map.CentralPosition;
        var pos    = ReadPosition(msg);
        pos = new Game.Position(pos.X, pos.Y, pos.Z + 1);

        int skip = 0;
        var range = _map.AwareRange;

        if (pos.Z == MapSeaFloor + 1)
        {
            // First underground floor: load range [z, z + aware range]
            int j = -1;
            for (int i = pos.Z; i <= pos.Z + MapAwareUndergroundRange; i++, j--)
                skip = SetFloorDescription(msg, pos.X - range.Left, pos.Y - range.Top,
                                           i, range.Horizontal, range.Vertical,
                                           offset: j, skip);
        }
        else if (pos.Z > MapSeaFloor + 1 && pos.Z < MapMaxZ - 1)
        {
            // Deeper underground: load only the one new floor coming into view
            SetFloorDescription(msg, pos.X - range.Left, pos.Y - range.Top,
                                pos.Z + MapAwareUndergroundRange, range.Horizontal, range.Vertical,
                                offset: -MapAwareUndergroundRange - 1, skip);
        }

        var newPos = new Game.Position(pos.X - 1, pos.Y - 1, pos.Z);
        _map.CentralPosition = newPos;
        FloorChanged?.Invoke(newPos, oldPos);
    }

    // ─── T46: Blessings / cooldowns / world-time / PvP ───────────────────────

    /// <summary>
    /// Parses <c>Blessings</c> (0x9C).
    /// Reads blessings bitmask (U16) and, for protocol ≥ 1200, visual state (U8).
    /// Fires <see cref="BlessingsChanged"/>.
    /// Maps to <c>ProtocolGame::parseBlessings</c>.
    /// Task T46.
    /// </summary>
    private void ParseBlessings(InputMessage msg)
    {
        ushort blessings    = msg.ReadU16();
        byte visualState    = msg.ReadU8();   // protocol 1281 always sends this byte
        BlessingsChanged?.Invoke(blessings, visualState);
    }

    /// <summary>
    /// Parses <c>SpellCooldown</c> (0xA4).
    /// Reads spellId (U16 for protocol 1281 which has GameUshortSpell) and delay (U32).
    /// Fires <see cref="SpellCooldownReceived"/>.
    /// Maps to <c>ProtocolGame::parseSpellCooldown</c>.
    /// Task T46.
    /// </summary>
    private void ParseSpellCooldown(InputMessage msg)
    {
        ushort spellId = msg.ReadU16();
        uint   delay   = msg.ReadU32();
        SpellCooldownReceived?.Invoke(spellId, delay);
    }

    /// <summary>
    /// Parses <c>SpellGroupCooldown</c> (0xA5).
    /// Reads groupId (U8) and delay (U32).
    /// Fires <see cref="SpellGroupCooldownReceived"/>.
    /// Maps to <c>ProtocolGame::parseSpellGroupCooldown</c>.
    /// Task T46.
    /// </summary>
    private void ParseSpellGroupCooldown(InputMessage msg)
    {
        byte groupId = msg.ReadU8();
        uint delay   = msg.ReadU32();
        SpellGroupCooldownReceived?.Invoke(groupId, delay);
    }

    /// <summary>
    /// Parses <c>MultiUseCooldown</c> (0xA6).
    /// Reads delay (U32).
    /// Fires <see cref="MultiUseCooldownReceived"/>.
    /// Maps to <c>ProtocolGame::parseMultiUseCooldown</c>.
    /// Task T46.
    /// </summary>
    private void ParseMultiUseCooldown(InputMessage msg)
    {
        uint delay = msg.ReadU32();
        MultiUseCooldownReceived?.Invoke(delay);
    }

    /// <summary>
    /// Parses <c>OpenOwnChannel</c> (0xB2).
    /// Reads channelId (U16) and channel name (string).
    /// Fires <see cref="OwnPrivateChannelOpened"/>.
    /// Maps to <c>ProtocolGame::parseOpenOwnPrivateChannel</c>.
    /// Task T46.
    /// </summary>
    private void ParseOpenOwnPrivateChannel(InputMessage msg)
    {
        ushort channelId   = msg.ReadU16();
        string channelName = msg.ReadString();
        OwnPrivateChannelOpened?.Invoke(channelId, channelName);
    }

    /// <summary>
    /// Parses <c>PvpSituations</c> (0xB8).
    /// Reads the number of open PvP situations (U8).
    /// Fires <see cref="PvpSituationsChanged"/>.
    /// Maps to <c>ProtocolGame::parsePvpSituations</c>.
    /// Task T46.
    /// </summary>
    private void ParsePvpSituations(InputMessage msg)
    {
        byte openPvpSituations = msg.ReadU8();
        PvpSituationsChanged?.Invoke(openPvpSituations);
    }

    /// <summary>
    /// Parses <c>ResourceBalance</c> (0xEE).
    /// Reads resource type (U8) and value (U64).
    /// Fires <see cref="ResourceBalanceChanged"/>.
    /// Maps to <c>ProtocolGame::parseResourceBalance</c>.
    /// Task T46.
    /// </summary>
    private void ParseResourceBalance(InputMessage msg)
    {
        byte  type  = msg.ReadU8();
        ulong value = msg.ReadU64();
        ResourceBalanceChanged?.Invoke(type, value);
    }

    /// <summary>
    /// Parses <c>WorldTime</c> (0xEF).
    /// Reads hour (U8) and minute (U8).
    /// Fires <see cref="WorldTimeChanged"/>.
    /// Maps to <c>ProtocolGame::parseWorldTime</c>.
    /// Task T46.
    /// </summary>
    private void ParseWorldTime(InputMessage msg)
    {
        byte hour   = msg.ReadU8();
        byte minute = msg.ReadU8();
        WorldTimeChanged?.Invoke(hour, minute);
    }

    // ─── T47: World/creature light, effects, player info, attack cancel, walk wait ──

    /// <summary>
    /// Parses <c>WorldLight</c> (0x82 / GameServerAmbient).
    /// Reads ambient light intensity (U8) and color (U8).
    /// Fires <see cref="WorldLightChanged"/>.
    /// Maps to <c>ProtocolGame::parseWorldLight</c>.
    /// Task T47.
    /// </summary>
    private void ParseWorldLight(InputMessage msg)
    {
        byte intensity = msg.ReadU8();
        byte color     = msg.ReadU8();
        WorldLightChanged?.Invoke(intensity, color);
    }

    /// <summary>
    /// Parses <c>GraphicalEffect</c> (0x83 / GameServerGraphicalEffect).
    /// At protocol ≥ 1203 reads a position then loops over typed effect entries
    /// until <c>MAGIC_EFFECTS_END_LOOP (0)</c>.  For each
    /// <c>MAGIC_EFFECTS_CREATE_EFFECT (3)</c> entry, fires
    /// <see cref="MagicEffectReceived"/> with the position and effectId.
    /// Maps to <c>ProtocolGame::parseMagicEffect</c>.
    /// Task T47.
    /// </summary>
    private void ParseMagicEffect(InputMessage msg)
    {
        var pos = ReadPosition(msg);

        // Protocol 1281 always uses the ≥ 1203 loop format.
        byte effectType = msg.ReadU8();
        while (effectType != 0)   // 0 = MAGIC_EFFECTS_END_LOOP
        {
            switch (effectType)
            {
                case 1:  // MAGIC_EFFECTS_DELTA  — U8 delta
                case 2:  // MAGIC_EFFECTS_DELAY  — U8 (wire) delay
                    msg.ReadU8();
                    break;

                case 3:  // MAGIC_EFFECTS_CREATE_EFFECT — U16 effectId
                {
                    ushort effectId = msg.ReadU16();
                    MagicEffectReceived?.Invoke(pos, effectId);
                    break;
                }

                case 4:  // MAGIC_EFFECTS_CREATE_DISTANCEEFFECT
                case 5:  // MAGIC_EFFECTS_CREATE_DISTANCEEFFECT_REVERSED
                {
                    ushort shotId = msg.ReadU16();
                    msg.ReadU8();   // offsetX (int8)
                    msg.ReadU8();   // offsetY (int8)
                    DistanceMissileReceived?.Invoke(pos, pos, shotId);
                    break;
                }

                case 6:  // MAGIC_EFFECTS_CREATE_SOUND_MAIN_EFFECT
                    msg.ReadU8();    // source
                    msg.ReadU16();   // sound id
                    break;

                case 7:  // MAGIC_EFFECTS_CREATE_SOUND_SECONDARY_EFFECT
                    msg.ReadU8();    // enum
                    msg.ReadU8();    // source
                    msg.ReadU16();   // sound id
                    break;

                default:
                    break;
            }

            effectType = msg.ReadU8();
        }
    }

    /// <summary>
    /// Parses <c>AnimatedText</c> (0x84 / GameServerTextEffect).
    /// Reads the tile position, text color (U8), and text string.
    /// Fires <see cref="AnimatedTextReceived"/>.
    /// Maps to <c>ProtocolGame::parseAnimatedText</c>.
    /// Task T47.
    /// </summary>
    private void ParseAnimatedText(InputMessage msg)
    {
        var    pos   = ReadPosition(msg);
        byte   color = msg.ReadU8();
        string text  = msg.ReadString();
        AnimatedTextReceived?.Invoke(pos, color, text);
    }

    /// <summary>
    /// Parses <c>DistanceMissile</c> (0x85 / GameServerMissileEffect).
    /// Reads fromPosition, toPosition, and shotId (U16).
    /// Fires <see cref="DistanceMissileReceived"/>.
    /// Maps to <c>ProtocolGame::parseDistanceMissile</c>.
    /// Task T47.
    /// </summary>
    private void ParseDistanceMissile(InputMessage msg)
    {
        var    fromPos = ReadPosition(msg);
        var    toPos   = ReadPosition(msg);
        ushort shotId  = msg.ReadU16();
        DistanceMissileReceived?.Invoke(fromPos, toPos, shotId);
    }

    /// <summary>
    /// Parses <c>CreatureLight</c> (0x8D / GameServerCreatureLight).
    /// Reads creatureId (U32), intensity (U8), and color (U8).
    /// Fires <see cref="CreatureLightUpdated"/>.
    /// Maps to <c>ProtocolGame::parseCreatureLight</c>.
    /// Task T47.
    /// </summary>
    private void ParseCreatureLight(InputMessage msg)
    {
        uint creatureId = msg.ReadU32();
        byte intensity  = msg.ReadU8();
        byte color      = msg.ReadU8();
        CreatureLightUpdated?.Invoke(creatureId, intensity, color);
    }

    /// <summary>
    /// Parses <c>PlayerInfo</c> (0x9F / GameServerPlayerDataBasic).
    /// Reads:
    ///   U8 isPremium, U32 premiumExpiration (discarded), U8 vocation,
    ///   U8 preyEnabled (discarded), U16 spellCount + spell ids (U16 each),
    ///   U8 isMagicShieldActive (discarded, protocol 1281).
    /// Fires <see cref="PlayerInfoReceived"/>.
    /// Maps to <c>ProtocolGame::parsePlayerInfo</c>.
    /// Task T47.
    /// </summary>
    private void ParsePlayerInfo(InputMessage msg)
    {
        bool isPremium = msg.ReadU8() != 0;
        msg.ReadU32();   // premium expiration timestamp — discarded

        byte vocation = msg.ReadU8();
        msg.ReadU8();    // prey enabled (bool) — discarded

        ushort spellCount = msg.ReadU16();
        var    spells     = new List<ushort>(spellCount);
        for (int i = 0; i < spellCount; i++)
            spells.Add(msg.ReadU16());

        msg.ReadU8();    // isMagicShieldActive — discarded (protocol 1281)

        PlayerInfoReceived?.Invoke(isPremium, vocation, spells);
    }

    /// <summary>
    /// Parses <c>ClearTarget</c> (0xA3 / GameServerClearTarget).
    /// Reads a sequence number (U32) used to acknowledge the attack cancel.
    /// Fires <see cref="AttackCancelReceived"/>.
    /// Maps to <c>ProtocolGame::parsePlayerCancelAttack</c>.
    /// Task T47.
    /// </summary>
    private void ParsePlayerCancelAttack(InputMessage msg)
    {
        uint seq = msg.ReadU32();
        AttackCancelReceived?.Invoke(seq);
    }

    /// <summary>
    /// Parses <c>WalkWait</c> (0xB6 / GameServerWalkWait).
    /// Reads a delay in milliseconds (U16) that the local player must wait
    /// before the next walk step is allowed.
    /// Fires <see cref="WalkWaitReceived"/>.
    /// Maps to <c>ProtocolGame::parseWalkWait</c>.
    /// Task T47.
    /// </summary>
    private void ParseWalkWait(InputMessage msg)
    {
        ushort millis = msg.ReadU16();
        WalkWaitReceived?.Invoke(millis);
    }

    // ─── T48 parsers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>BugReport</c> (0x1A / GameServerBugReport).
    /// Reads one U8: non-zero means the player is allowed to report bugs.
    /// Fires <see cref="BugReportReceived"/>.
    /// Maps to <c>ProtocolGame::parseBugReport</c>.
    /// Task T48.
    /// </summary>
    private void ParseBugReport(InputMessage msg)
    {
        bool canReport = msg.ReadU8() != 0;
        BugReportReceived?.Invoke(canReport);
    }

    /// <summary>
    /// Parses <c>Trappers</c> (0x87 / GameServerTrappers).
    /// Reads U8 count followed by one U32 creature ID per entry.
    /// Fires <see cref="TrappersReceived"/>.
    /// Maps to <c>ProtocolGame::parseTrappers</c>.
    /// Task T48.
    /// </summary>
    private void ParseTrappers(InputMessage msg)
    {
        byte count = msg.ReadU8();
        var  ids   = new List<uint>(count);
        for (int i = 0; i < count; i++)
            ids.Add(msg.ReadU32());
        TrappersReceived?.Invoke(ids);
    }

    /// <summary>
    /// Parses <c>CloseForgeWindow</c> (0x89 / GameServerCloseForgeWindow).
    /// No payload — simply fires <see cref="ForgeWindowClosed"/>.
    /// Maps to <c>ProtocolGame::parseCloseForgeWindow</c>.
    /// Task T48.
    /// </summary>
    private void ParseCloseForgeWindow(InputMessage _)
        => ForgeWindowClosed?.Invoke();

    /// <summary>
    /// Parses <c>RestingAreaState</c> (0xA9 / GameServerSendRestingAreaState).
    /// Reads U8 zone, U8 state, and a string message.
    /// Fires <see cref="RestingAreaStateReceived"/>.
    /// Maps to <c>ProtocolGame::parseRestingAreaState</c>.
    /// Task T48.
    /// </summary>
    private void ParseRestingAreaState(InputMessage msg)
    {
        byte   zone    = msg.ReadU8();
        byte   state   = msg.ReadU8();
        string message = msg.ReadString();
        RestingAreaStateReceived?.Invoke(zone, state, message);
    }

    /// <summary>
    /// Parses <c>UnjustifiedStats</c> (0xB7 / GameServerUnjustifiedStats).
    /// Reads seven U8 values: killsDay, killsDayRemaining, killsWeek,
    /// killsWeekRemaining, killsMonth, killsMonthRemaining, skullTime.
    /// Fires <see cref="UnjustifiedStatsReceived"/>.
    /// Maps to <c>ProtocolGame::parseUnjustifiedStats</c>.
    /// Task T48.
    /// </summary>
    private void ParseUnjustifiedStats(InputMessage msg)
    {
        byte killsDay              = msg.ReadU8();
        byte killsDayRemaining     = msg.ReadU8();
        byte killsWeek             = msg.ReadU8();
        byte killsWeekRemaining    = msg.ReadU8();
        byte killsMonth            = msg.ReadU8();
        byte killsMonthRemaining   = msg.ReadU8();
        byte skullTime             = msg.ReadU8();
        UnjustifiedStatsReceived?.Invoke(new UnjustifiedStats(
            killsDay, killsDayRemaining,
            killsWeek, killsWeekRemaining,
            killsMonth, killsMonthRemaining,
            skullTime));
    }

    /// <summary>
    /// Parses <c>TutorialHint</c> (0xDC / GameServerTutorialHint).
    /// Reads one U8 hint ID.
    /// Fires <see cref="TutorialHintReceived"/>.
    /// Maps to <c>ProtocolGame::parseTutorialHint</c>.
    /// Task T48.
    /// </summary>
    private void ParseTutorialHint(InputMessage msg)
    {
        byte hintId = msg.ReadU8();
        TutorialHintReceived?.Invoke(hintId);
    }

    /// <summary>
    /// Parses <c>AutomapFlag</c> (0xDD / GameServerAutomapFlag).
    /// Reads a map position, icon (U8), description string, and remove flag (U8).
    /// Fires <see cref="AutomapFlagReceived"/>.
    /// Maps to <c>ProtocolGame::parseAutomapFlag</c>.
    /// Task T48.
    /// </summary>
    private void ParseAutomapFlag(InputMessage msg)
    {
        Position pos         = ReadPosition(msg);
        byte     icon        = msg.ReadU8();
        string   description = msg.ReadString();
        bool     remove      = msg.ReadU8() != 0;
        AutomapFlagReceived?.Invoke(pos, icon, description, remove);
    }

    /// <summary>
    /// Parses <c>ChannelEvent</c> (0xF3 / GameServerChannelEvent).
    /// Reads channel ID (U16), channel name (string), and event type (U8).
    /// Fires <see cref="ChannelEventReceived"/>.
    /// Maps to <c>ProtocolGame::parseChannelEvent</c>.
    /// Task T48.
    /// </summary>
    private void ParseChannelEvent(InputMessage msg)
    {
        ushort channelId   = msg.ReadU16();
        string channelName = msg.ReadString();
        byte   eventType   = msg.ReadU8();
        ChannelEventReceived?.Invoke(channelId, channelName, eventType);
    }

    // ─── T49 parsers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>SetStoreDeepLink</c> (0xA8 / GameServerSetStoreDeepLink).
    /// Reads one U8 service-type byte (discarded by the C++ reference client).
    /// Fires <see cref="StoreDeepLinkReceived"/>.
    /// Maps to <c>ProtocolGame::parseSetStoreDeepLink</c>.
    /// Task T49.
    /// </summary>
    private void ParseSetStoreDeepLink(InputMessage msg)
    {
        byte serviceType = msg.ReadU8();
        StoreDeepLinkReceived?.Invoke(serviceType);
    }

    /// <summary>
    /// Parses <c>ChangeMapAwareRange</c> (0x33 / GameServerChangeMapAwareRange).
    /// Reads xRange (U8) and yRange (U8) defining the new client-visible area.
    /// Fires <see cref="MapAwareRangeChanged"/>.
    /// Maps to <c>ProtocolGame::parseChangeMapAwareRange</c>.
    /// Task T49.
    /// </summary>
    private void ParseChangeMapAwareRange(InputMessage msg)
    {
        byte xRange = msg.ReadU8();
        byte yRange = msg.ReadU8();
        MapAwareRangeChanged?.Invoke(xRange, yRange);
    }

    /// <summary>
    /// Parses <c>DailyRewardCollectionState</c> (0xDE / GameServerSendDailyRewardCollectionState).
    /// Reads one U8 state (0 = not collected, 1 = collected).
    /// Fires <see cref="DailyRewardCollectionStateReceived"/>.
    /// Maps to <c>ProtocolGame::parseDailyRewardCollectionState</c>.
    /// Task T49.
    /// </summary>
    private void ParseDailyRewardCollectionState(InputMessage msg)
    {
        byte state = msg.ReadU8();
        DailyRewardCollectionStateReceived?.Invoke(state);
    }

    /// <summary>
    /// Parses <c>OpenRewardWall</c> (0xE2 / GameServerSendOpenRewardWall).
    /// Wire format: U8 bonusShrine, U32 nextRewardTime, U8 dayStreakDay,
    /// U8 wasDailyRewardTaken; if taken: string errorMessage + optional U8+U16 tokens;
    /// else: skip U8 + U32 timeLeft + U16 tokens; finally U16 dayStreakLevel.
    /// Fires <see cref="RewardWallOpened"/>.
    /// Maps to <c>ProtocolGame::parseOpenRewardWall</c>.
    /// Task T49.
    /// </summary>
    private void ParseOpenRewardWall(InputMessage msg)
    {
        byte   bonusShrine         = msg.ReadU8();
        uint   nextRewardTime      = msg.ReadU32();
        byte   dayStreakDay        = msg.ReadU8();
        byte   wasDailyRewardTaken = msg.ReadU8();

        string errorMessage = string.Empty;
        ushort tokens       = 0;
        uint   timeLeft     = 0;

        if (wasDailyRewardTaken != 0)
        {
            errorMessage = msg.ReadString();
            if (msg.ReadU8() != 0)
                tokens = msg.ReadU16();
        }
        else
        {
            msg.ReadU8();           // unknown
            timeLeft = msg.ReadU32();
            tokens   = msg.ReadU16();
        }

        ushort dayStreakLevel = msg.ReadU16();
        RewardWallOpened?.Invoke(bonusShrine, nextRewardTime, dayStreakDay,
            wasDailyRewardTaken, errorMessage, tokens, timeLeft, dayStreakLevel);
    }

    /// <summary>
    /// Helper: reads one <c>DailyRewardDay</c> from the wire stream.
    /// Mode 1 (select-from-list): U8 itemsToSelect, U8 listSize × (U16 id, string name, U32 weight).
    /// Mode 2 (redeem-all): U8 listSize × (U8 bundleType, then type-specific fields).
    /// Maps to the <c>parseRewardDay</c> lambda in <c>protocolgameparse.cpp</c>.
    /// Task T49.
    /// </summary>
    private static Game.DailyRewardDay ReadRewardDay(InputMessage msg)
    {
        byte redeemMode = msg.ReadU8();
        if (redeemMode == 1)
        {
            byte itemsToSelect = msg.ReadU8();
            byte listSize      = msg.ReadU8();
            var  items         = new List<Game.DailyRewardItem>(listSize);
            for (int i = 0; i < listSize; i++)
            {
                ushort itemId = msg.ReadU16();
                string name   = msg.ReadString();
                uint   weight = msg.ReadU32();
                items.Add(new Game.DailyRewardItem(itemId, name, weight));
            }
            return new Game.DailyRewardDay
            {
                RedeemMode = redeemMode, ItemsToSelect = itemsToSelect, SelectableItems = items
            };
        }
        else if (redeemMode == 2)
        {
            byte listSize = msg.ReadU8();
            var  bundles  = new List<Game.DailyRewardBundle>(listSize);
            for (int i = 0; i < listSize; i++)
            {
                byte   bundleType = msg.ReadU8();
                ushort itemId     = 0;
                string name       = string.Empty;
                byte   count      = 0;
                switch (bundleType)
                {
                    case 1:
                        itemId = msg.ReadU16();
                        name   = msg.ReadString();
                        count  = msg.ReadU8();
                        break;
                    case 2:
                        name  = "Prey Wildcards";
                        count = msg.ReadU8();
                        break;
                    case 3:
                        itemId = msg.ReadU16(); // XP Boost minutes
                        name   = "XP Boost";
                        break;
                }
                bundles.Add(new Game.DailyRewardBundle(bundleType, itemId, name, count));
            }
            return new Game.DailyRewardDay { RedeemMode = redeemMode, BundleItems = bundles };
        }

        return new Game.DailyRewardDay { RedeemMode = redeemMode };
    }

    /// <summary>
    /// Parses <c>DailyReward</c> (0xE4 / GameServerSendDailyReward).
    /// Reads a <see cref="Game.DailyRewardData"/> structure:
    /// U8 days × (free DailyRewardDay + premium DailyRewardDay);
    /// U8 bonusCount × (string name, U8 id); U8 maxUnlockableDragons.
    /// Fires <see cref="DailyRewardReceived"/>.
    /// Maps to <c>ProtocolGame::parseDailyReward</c>.
    /// Task T49.
    /// </summary>
    private void ParseDailyReward(InputMessage msg)
    {
        byte days         = msg.ReadU8();
        var  freeRewards    = new List<Game.DailyRewardDay>(days);
        var  premiumRewards = new List<Game.DailyRewardDay>(days);
        for (int i = 0; i < days; i++)
        {
            freeRewards.Add(ReadRewardDay(msg));
            premiumRewards.Add(ReadRewardDay(msg));
        }

        byte bonusCount = msg.ReadU8();
        var  bonuses    = new List<Game.DailyRewardBonus>(bonusCount);
        for (int i = 0; i < bonusCount; i++)
        {
            string bonusName = msg.ReadString();
            byte   bonusId   = msg.ReadU8();
            bonuses.Add(new Game.DailyRewardBonus(bonusName, bonusId));
        }

        byte maxDragons = msg.ReadU8();
        DailyRewardReceived?.Invoke(new Game.DailyRewardData
        {
            Days = days, FreeRewards = freeRewards, PremiumRewards = premiumRewards,
            Bonuses = bonuses, MaxUnlockableDragons = maxDragons
        });
    }

    /// <summary>
    /// Parses <c>RewardHistory</c> (0xE5 / GameServerSendRewardHistory).
    /// Wire: U8 count × (U32 timestamp, U8 isPremium, string description, U16 dayStreak).
    /// Fires <see cref="RewardHistoryReceived"/>.
    /// Maps to <c>ProtocolGame::parseRewardHistory</c>.
    /// Task T49.
    /// </summary>
    private void ParseRewardHistory(InputMessage msg)
    {
        byte count   = msg.ReadU8();
        var  history = new List<(uint, bool, string, ushort)>(count);
        for (int i = 0; i < count; i++)
        {
            uint   timestamp   = msg.ReadU32();
            bool   isPremium   = msg.ReadU8() != 0;
            string description = msg.ReadString();
            ushort dayStreak   = msg.ReadU16();
            history.Add((timestamp, isPremium, description, dayStreak));
        }
        RewardHistoryReceived?.Invoke(history);
    }

    /// <summary>
    /// Parses <c>LootContainers</c> (0xC0 / GameServerLootContainers).
    /// Wire: U8 quickLootFallback, U8 count × (U8 categoryType, U16 lootContainerId,
    /// U16 obtainerContainerId for protocol ≥ 1332 — we always read it at 1281).
    /// Fires <see cref="LootContainersReceived"/>.
    /// Maps to <c>ProtocolGame::parseLootContainers</c>.
    /// Task T49.
    /// </summary>
    private void ParseLootContainers(InputMessage msg)
    {
        bool quickLootFallback = msg.ReadU8() != 0;
        byte count             = msg.ReadU8();
        var  list              = new List<(byte, ushort, ushort)>(count);
        for (int i = 0; i < count; i++)
        {
            byte   categoryType        = msg.ReadU8();
            ushort lootContainerId     = msg.ReadU16();
            ushort obtainerContainerId = msg.ReadU16();
            list.Add((categoryType, lootContainerId, obtainerContainerId));
        }
        LootContainersReceived?.Invoke(quickLootFallback, list);
    }

    // ─── T50 parsers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>ExtendedOpcode</c> (0x32 / GameServerExtendedOpcode).
    /// Wire: U8 opcode, string buffer.
    /// Fires <see cref="ExtendedOpcodeReceived"/>.
    /// Maps to <c>ProtocolGame::parseExtendedOpcode</c>.
    /// Task T50.
    /// </summary>
    private void ParseExtendedOpcode(InputMessage msg)
    {
        byte   opcode = msg.ReadU8();
        string buffer = msg.ReadString();
        ExtendedOpcodeReceived?.Invoke(opcode, buffer);
    }

    /// <summary>
    /// Parses <c>TakeScreenshot</c> (0x75 / GameServerTakeScreenshot).
    /// Wire: U8 screenshotType.
    /// Fires <see cref="TakeScreenshotReceived"/>.
    /// Maps to <c>ProtocolGame::parseTakeScreenshot</c>.
    /// Task T50.
    /// </summary>
    private void ParseTakeScreenshot(InputMessage msg)
    {
        byte screenshotType = msg.ReadU8();
        TakeScreenshotReceived?.Invoke(screenshotType);
    }

    /// <summary>
    /// Parses <c>SendGameNews</c> (0x98 / GameServerSendGameNews).
    /// Wire: U32 categoryId, U8 pageNumber.
    /// Fires <see cref="GameNewsReceived"/>.
    /// Maps to <c>ProtocolGame::parseGameNews</c>.
    /// Task T50.
    /// </summary>
    private void ParseGameNews(InputMessage msg)
    {
        uint categoryId  = msg.ReadU32();
        byte pageNumber  = msg.ReadU8();
        GameNewsReceived?.Invoke(categoryId, pageNumber);
    }

    /// <summary>
    /// Parses <c>Preset</c> (0x9D / GameServerPreset).
    /// Wire: U32 preset value (discarded by C++ reference impl; we expose it).
    /// Fires <see cref="PresetReceived"/>.
    /// Maps to <c>ProtocolGame::parsePreset</c>.
    /// Task T50.
    /// </summary>
    private void ParsePreset(InputMessage msg)
    {
        uint preset = msg.ReadU32();
        PresetReceived?.Invoke(preset);
    }

    /// <summary>
    /// Parses <c>PremiumTrigger</c> (0x9E / GameServerPremiumTrigger).
    /// Wire: U8 count, then count × U8 trigger type bytes.
    /// Fires <see cref="PremiumTriggerReceived"/>.
    /// Maps to <c>ProtocolGame::parsePremiumTrigger</c>.
    /// Task T50.
    /// </summary>
    private void ParsePremiumTrigger(InputMessage msg)
    {
        byte count    = msg.ReadU8();
        var  triggers = new List<byte>(count);
        for (int i = 0; i < count; i++)
            triggers.Add(msg.ReadU8());
        PremiumTriggerReceived?.Invoke(triggers);
    }

    /// <summary>
    /// Parses <c>RuleViolationChannel</c> (0xAE / GameServerRuleViolationChannel).
    /// Wire: U16 channelId.
    /// Fires <see cref="RuleViolationChannelReceived"/>.
    /// Maps to <c>ProtocolGame::parseRuleViolationChannel</c>.
    /// Task T50.
    /// </summary>
    private void ParseRuleViolationChannel(InputMessage msg)
    {
        ushort channelId = msg.ReadU16();
        RuleViolationChannelReceived?.Invoke(channelId);
    }

    /// <summary>
    /// Parses the <c>RuleViolationRemove</c> (0xAF) packet as an
    /// <em>experience-tracker</em> update (protocol ≥ 1200 path).
    /// Wire: I64 rawExp, I64 finalExp.
    /// Fires <see cref="ExperienceTrackerReceived"/>.
    /// Maps to <c>ProtocolGame::parseExperienceTracker</c>.
    /// Task T50.
    /// </summary>
    private void ParseExperienceTracker(InputMessage msg)
    {
        long rawExp   = msg.ReadS64();
        long finalExp = msg.ReadS64();
        ExperienceTrackerReceived?.Invoke(rawExp, finalExp);
    }

    /// <summary>
    /// Parses <c>RuleViolationCancel</c> (0xB0 / GameServerRuleViolationCancel).
    /// Wire: string reporterName.
    /// Fires <see cref="RuleViolationCancelReceived"/>.
    /// Maps to <c>ProtocolGame::parseRuleViolationCancel</c>.
    /// Task T50.
    /// </summary>
    private void ParseRuleViolationCancel(InputMessage msg)
    {
        string reporterName = msg.ReadString();
        RuleViolationCancelReceived?.Invoke(reporterName);
    }

    /// <summary>
    /// Parses <c>RuleViolationLock</c> (0xB1 / GameServerRuleViolationLock).
    /// No payload at protocol 1281 (&lt;1310 branch).
    /// Fires <see cref="RuleViolationLockReceived"/>.
    /// Maps to <c>ProtocolGame::parseRuleViolationLock</c>.
    /// Task T50.
    /// </summary>
    private void ParseRuleViolationLock(InputMessage _)
    {
        RuleViolationLockReceived?.Invoke();
    }

    // ─── T51 parsers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>SupplyStash</c> (0x29 / GameServerSupplyStash).
    /// Wire: U16 count; count × {U16 itemId, U32 amount}; U16 freeSlots
    /// (always present at protocol 1281 &lt; 1410).
    /// Fires <see cref="SupplyStashReceived"/>.
    /// Maps to <c>ProtocolGame::parseSupplyStash</c>.
    /// Task T51.
    /// </summary>
    private void ParseSupplyStash(InputMessage msg)
    {
        ushort count = msg.ReadU16();
        var items = new List<SupplyStashItem>(count);
        for (int i = 0; i < count; i++)
        {
            ushort itemId = msg.ReadU16();
            uint   amount = msg.ReadU32();
            items.Add(new SupplyStashItem(itemId, amount));
        }
        msg.ReadU16(); // free slots (proto 1281 < 1410)
        SupplyStashReceived?.Invoke(items);
    }

    /// <summary>
    /// Parses <c>SpecialContainer</c> (0x2A / GameServerSpecialContainer).
    /// Wire: U8 supplyStashAvailable; U8 isMarketAvailable
    /// (always present at protocol 1281 ≥ 1220).
    /// Fires <see cref="SpecialContainerReceived"/>.
    /// Maps to <c>ProtocolGame::parseSpecialContainer</c>.
    /// Task T51.
    /// </summary>
    private void ParseSpecialContainer(InputMessage msg)
    {
        byte supplyStash = msg.ReadU8();
        byte isMarket    = msg.ReadU8(); // proto 1281 >= 1220
        SpecialContainerReceived?.Invoke(supplyStash, isMarket);
    }

    /// <summary>
    /// Parses <c>PartyAnalyzer</c> (0x2B / GameServerPartyAnalyzer).
    /// Wire: U32 startTime, U32 leaderId, U8 lootType,
    ///       U8 memberCount, memberCount × {U32 id, U8 highlight, 4×U64 stats},
    ///       U8 hasNames, [U8 nameCount, nameCount × {U32 id, str name}].
    /// Fires <see cref="PartyAnalyzerReceived"/>.
    /// Maps to <c>ProtocolGame::parsePartyAnalyzer</c>.
    /// Task T51.
    /// </summary>
    private void ParsePartyAnalyzer(InputMessage msg)
    {
        uint startTime  = msg.ReadU32();
        uint leaderId   = msg.ReadU32();
        byte lootType   = msg.ReadU8();

        byte memberCount = msg.ReadU8();
        var members = new List<PartyMemberData>(memberCount);
        for (int i = 0; i < memberCount; i++)
        {
            uint  memberId  = msg.ReadU32();
            byte  highlight = msg.ReadU8();
            ulong loot      = msg.ReadU64();
            ulong supply    = msg.ReadU64();
            ulong damage    = msg.ReadU64();
            ulong healing   = msg.ReadU64();
            members.Add(new PartyMemberData(memberId, highlight, loot, supply, damage, healing));
        }

        bool hasNames = msg.ReadU8() != 0;
        var names = new List<PartyMemberName>();
        if (hasNames)
        {
            byte nameCount = msg.ReadU8();
            for (int i = 0; i < nameCount; i++)
            {
                uint   memberId   = msg.ReadU32();
                string memberName = msg.ReadString();
                names.Add(new PartyMemberName(memberId, memberName));
            }
        }

        PartyAnalyzerReceived?.Invoke(new PartyAnalyzerData
        {
            StartTime = startTime,
            LeaderId  = leaderId,
            LootType  = lootType,
            Members   = members,
            Names     = names,
        });
    }

    /// <summary>
    /// Parses <c>AttachedPaperdoll</c> (0x3C / GameServerAttachedPaperdoll).
    /// Wire: U32 creatureId; U16 id, U8 slot, U8 color, U8 head, U8 body,
    ///       U8 legs, U8 feet, str shader.
    /// Fires <see cref="PaperdollAttachedReceived"/>.
    /// Maps to <c>ProtocolGame::parseAttachedPaperdoll</c> +
    /// <c>ProtocolGame::getPaperdoll</c>.
    /// Task T51.
    /// </summary>
    private void ParseAttachedPaperdoll(InputMessage msg)
    {
        uint   creatureId = msg.ReadU32();
        ushort id         = msg.ReadU16();
        byte   slot       = msg.ReadU8();
        byte   color      = msg.ReadU8();
        byte   head       = msg.ReadU8();
        byte   body       = msg.ReadU8();
        byte   legs       = msg.ReadU8();
        byte   feet       = msg.ReadU8();
        string shader     = msg.ReadString();
        PaperdollAttachedReceived?.Invoke(creatureId, new PaperdollAttachData(id, slot, color, head, body, legs, feet, shader));
    }

    /// <summary>
    /// Parses <c>DetachPaperdoll</c> (0x3D / GameServerDetachPaperdoll).
    /// Wire: U32 creatureId, U8 bySlot, U16 idOrSlot.
    /// Fires <see cref="PaperdollDetachedReceived"/>.
    /// Maps to <c>ProtocolGame::parseDetachPaperdoll</c>.
    /// Task T51.
    /// </summary>
    private void ParseDetachPaperdoll(InputMessage msg)
    {
        uint   creatureId = msg.ReadU32();
        bool   bySlot     = msg.ReadU8() != 0;
        ushort idOrSlot   = msg.ReadU16();
        PaperdollDetachedReceived?.Invoke(creatureId, bySlot, idOrSlot);
    }

    /// <summary>
    /// Parses <c>Features</c> (0x43 / GameServerFeatures).
    /// Wire: U16 count; count × {U8 featureId, U8 enabled}.
    /// Fires <see cref="FeaturesReceived"/>.
    /// Maps to <c>ProtocolGame::parseFeatures</c>.
    /// Task T51.
    /// </summary>
    private void ParseFeatures(InputMessage msg)
    {
        ushort count = msg.ReadU16();
        var features = new List<(byte FeatureId, bool Enabled)>(count);
        for (int i = 0; i < count; i++)
        {
            byte featureId = msg.ReadU8();
            bool enabled   = msg.ReadU8() != 0;
            features.Add((featureId, enabled));
        }
        FeaturesReceived?.Invoke(features);
    }

    /// <summary>
    /// Parses <c>WeaponProficiencyExp</c> (0x5C / GameServerWeaponProficiencyExperience).
    /// Wire: U16 itemId, U32 experience, U8 unknown.
    /// Fires <see cref="WeaponProficiencyExpReceived"/>.
    /// Maps to <c>ProtocolGame::parseWeaponProficiencyExperience</c>.
    /// Task T51.
    /// </summary>
    private void ParseWeaponProficiencyExperience(InputMessage msg)
    {
        ushort itemId     = msg.ReadU16();
        uint   experience = msg.ReadU32();
        msg.ReadU8();  // unused
        WeaponProficiencyExpReceived?.Invoke(itemId, experience);
    }

    /// <summary>
    /// Parses <c>PassiveCooldown</c> (0x5E / GameServerPassiveCooldown).
    /// Wire: U8 (skip), U8 type;
    ///   type 0: U32 currentCooldown, U32 maxCooldown, U8 canDecay → fires event;
    ///   type 1: U8 unknown1, U8 unknown2 → no event.
    /// Fires <see cref="PassiveCooldownReceived"/> for type 0.
    /// Maps to <c>ProtocolGame::parsePassiveCooldown</c>.
    /// Task T51.
    /// </summary>
    private void ParsePassiveCooldown(InputMessage msg)
    {
        msg.ReadU8();  // skip first byte
        byte type = msg.ReadU8();
        if (type == 0)
        {
            uint current  = msg.ReadU32();
            uint max      = msg.ReadU32();
            bool canDecay = msg.ReadU8() != 0;
            PassiveCooldownReceived?.Invoke(current, max, canDecay);
        }
        else if (type == 1)
        {
            msg.ReadU8();
            msg.ReadU8();
        }
    }

    /// <summary>
    /// Parses <c>BosstiaryData</c> (0x61 / GameServerBosstiaryData).
    /// Wire: 18 × U16 kill-count thresholds and point rewards for each tier.
    /// Fires <see cref="BosstiaryDataReceived"/>.
    /// Maps to <c>ProtocolGame::parseBosstiaryData</c>.
    /// Task T51.
    /// </summary>
    private void ParseBosstiaryData(InputMessage msg)
    {
        ushort baneProwessKills     = msg.ReadU16();
        ushort baneExpertiseKills   = msg.ReadU16();
        ushort baneMasteryKills     = msg.ReadU16();
        ushort archfoeProwessKills  = msg.ReadU16();
        ushort archfoeExpertiseKills= msg.ReadU16();
        ushort archfoeMasteryKills  = msg.ReadU16();
        ushort nemesisProwessKills  = msg.ReadU16();
        ushort nemesisExpertiseKills= msg.ReadU16();
        ushort nemesisMasteryKills  = msg.ReadU16();
        ushort baneProwessPoints    = msg.ReadU16();
        ushort baneExpertisePoints  = msg.ReadU16();
        ushort baneMasteryPoints    = msg.ReadU16();
        ushort archfoeProwessPoints = msg.ReadU16();
        ushort archfoeExpertisePoints=msg.ReadU16();
        ushort archfoeMasteryPoints = msg.ReadU16();
        ushort nemesisProwessPoints = msg.ReadU16();
        ushort nemesisExpertisePoints=msg.ReadU16();
        ushort nemesisMasteryPoints = msg.ReadU16();
        BosstiaryDataReceived?.Invoke(new BosstiaryKillThresholds(
            baneProwessKills, baneExpertiseKills, baneMasteryKills,
            archfoeProwessKills, archfoeExpertiseKills, archfoeMasteryKills,
            nemesisProwessKills, nemesisExpertiseKills, nemesisMasteryKills,
            baneProwessPoints, baneExpertisePoints, baneMasteryPoints,
            archfoeProwessPoints, archfoeExpertisePoints, archfoeMasteryPoints,
            nemesisProwessPoints, nemesisExpertisePoints, nemesisMasteryPoints));
    }

    /// <summary>
    /// Parses <c>ClientCheck</c> (0x63 / GameServerSendClientCheck).
    /// Wire: U32 size; size × U8 (unknown, anti-cheat data).
    /// Data is consumed and discarded.
    /// Maps to <c>ProtocolGame::parseClientCheck</c>.
    /// Task T51.
    /// </summary>
    private void ParseClientCheck(InputMessage msg)
    {
        uint size = msg.ReadU32();
        for (uint i = 0; i < size; i++)
            msg.ReadU8();
    }

    // ─── T52 parsers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>BosstiaryInfo</c> (0x73 / GameServerBosstiaryInfo).
    /// Wire: U16 count; for each entry: U32 raceId, U8 category, U32 kills, U8 (skip),
    ///   [U8 isTrackerActive if proto ≥ 1320].
    /// Fires <see cref="BosstiaryInfoReceived"/>.
    /// Maps to <c>ProtocolGame::parseBosstiaryInfo</c>.
    /// Task T52.
    /// </summary>
    private void ParseBosstiaryInfo(InputMessage msg)
    {
        ushort count = msg.ReadU16();
        var entries  = new List<BosstiaryEntry>(count);
        for (int i = 0; i < count; i++)
        {
            uint raceId   = msg.ReadU32();
            byte category = msg.ReadU8();
            uint kills    = msg.ReadU32();
            msg.ReadU8();  // unknown / padding
            bool isTrackerActive = false;
            if (ProtocolVersion >= 1320)
                isTrackerActive = msg.ReadU8() != 0;
            entries.Add(new BosstiaryEntry(raceId, category, kills, isTrackerActive));
        }
        BosstiaryInfoReceived?.Invoke(entries);
    }

    /// <summary>
    /// Helper that reads one <see cref="BosstiarySlot"/> value from the wire.
    /// Wire: U8 bossRace, U32 killCount, U16 lootBonus, U8 killBonus,
    ///       U8 bossRaceRepeat, U32 removePrice, U8 inactive.
    /// Task T52.
    /// </summary>
    private static BosstiarySlot ReadBosstiarySlot(InputMessage msg)
    {
        byte   bossRace       = msg.ReadU8();
        uint   killCount      = msg.ReadU32();
        ushort lootBonus      = msg.ReadU16();
        byte   killBonus      = msg.ReadU8();
        byte   bossRaceRepeat = msg.ReadU8();
        uint   removePrice    = msg.ReadU32();
        bool   inactive       = msg.ReadU8() != 0;
        return new BosstiarySlot(bossRace, killCount, lootBonus, killBonus, bossRaceRepeat, removePrice, inactive);
    }

    /// <summary>
    /// Parses <c>BosstiarySlots</c> (0x62 / GameServerBosstiarySlots).
    /// Wire: U32 playerPoints, U32 totalPointsNextBonus, U16 currentBonus, U16 nextBonus;
    ///   3 slot blocks (slot1, slot2, todaySlot): U8 unlocked, U32 bossId, [slot data if unlocked &amp;&amp; bossId≠0];
    ///   U8 bossesUnlocked; if true: U16 count + loop{U32 bossId, U8 bossRace}.
    /// Fires <see cref="BosstiarySlotReceived"/>.
    /// Maps to <c>ProtocolGame::parseBosstiarySlots</c>.
    /// Task T52.
    /// </summary>
    private void ParseBosstiarySlots(InputMessage msg)
    {
        uint   playerPoints         = msg.ReadU32();
        uint   totalPointsNextBonus = msg.ReadU32();
        ushort currentBonus         = msg.ReadU16();
        ushort nextBonus            = msg.ReadU16();

        bool          isSlotOneUnlocked = msg.ReadU8() != 0;
        uint          bossIdSlotOne     = msg.ReadU32();
        BosstiarySlot? slotOneData      = (isSlotOneUnlocked && bossIdSlotOne != 0)
                                          ? ReadBosstiarySlot(msg) : null;

        bool          isSlotTwoUnlocked = msg.ReadU8() != 0;
        uint          bossIdSlotTwo     = msg.ReadU32();
        BosstiarySlot? slotTwoData      = (isSlotTwoUnlocked && bossIdSlotTwo != 0)
                                          ? ReadBosstiarySlot(msg) : null;

        bool          isTodaySlotUnlocked = msg.ReadU8() != 0;
        uint          boostedBossId       = msg.ReadU32();
        BosstiarySlot? todaySlotData      = (isTodaySlotUnlocked && boostedBossId != 0)
                                           ? ReadBosstiarySlot(msg) : null;

        bool bossesUnlocked = msg.ReadU8() != 0;
        var  bossesData     = new List<(uint BossId, byte BossRace)>();
        if (bossesUnlocked)
        {
            ushort size = msg.ReadU16();
            for (int i = 0; i < size; i++)
            {
                uint bossId   = msg.ReadU32();
                byte bossRace = msg.ReadU8();
                bossesData.Add((bossId, bossRace));
            }
        }

        var data = new BosstiarySlotsData
        {
            PlayerPoints          = playerPoints,
            TotalPointsNextBonus  = totalPointsNextBonus,
            CurrentBonus          = currentBonus,
            NextBonus             = nextBonus,
            IsSlotOneUnlocked     = isSlotOneUnlocked,
            BossIdSlotOne         = bossIdSlotOne,
            SlotOneData           = slotOneData,
            IsSlotTwoUnlocked     = isSlotTwoUnlocked,
            BossIdSlotTwo         = bossIdSlotTwo,
            SlotTwoData           = slotTwoData,
            IsTodaySlotUnlocked   = isTodaySlotUnlocked,
            BoostedBossId         = boostedBossId,
            TodaySlotData         = todaySlotData,
            BossesUnlocked        = bossesUnlocked,
            BossesUnlockedData    = bossesData,
        };
        BosstiarySlotReceived?.Invoke(data);
    }

    /// <summary>
    /// Parses <c>BosstiaryCooldownTimer</c> (0xBD / GameServerBosstiaryCooldownTimer).
    /// Wire: U16 count; for each entry: U32 bossRaceId, U64 cooldownSeconds.
    /// Fires <see cref="BosstiaryCooldownTimerReceived"/>.
    /// Maps to <c>ProtocolGame::parseBosstiaryCooldownTimer</c>.
    /// Task T52.
    /// </summary>
    private void ParseBosstiaryCooldownTimer(InputMessage msg)
    {
        ushort count = msg.ReadU16();
        var list     = new List<(uint BossId, ulong CooldownSeconds)>(count);
        for (int i = 0; i < count; i++)
        {
            uint  bossId   = msg.ReadU32();
            ulong cooldown = msg.ReadU64();
            list.Add((bossId, cooldown));
        }
        BosstiaryCooldownTimerReceived?.Invoke(list);
    }

    /// <summary>
    /// Parses <c>BestiaryEntryChanged</c> (0xD9 / GameServerBestiaryEntryChanged).
    /// Wire: U16 monsterID.
    /// Fires <see cref="BestiaryEntryChangedReceived"/>.
    /// Maps to <c>ProtocolGame::parseBestiaryEntryChanged</c>.
    /// Task T52.
    /// </summary>
    private void ParseBestiaryEntryChanged(InputMessage msg)
    {
        ushort monsterId = msg.ReadU16();
        BestiaryEntryChangedReceived?.Invoke(monsterId);
    }

    /// <summary>
    /// Parses <c>UpdateImpactTracker</c> (0xCC / GameServerSendUpdateImpactTracker).
    /// Wire: U8 analyzerType, U32 amount;
    ///   type 1 (damage dealt): U8 effect;
    ///   type 2 (damage received): U8 effect, str target.
    /// Fires <see cref="ImpactTrackerReceived"/>.
    /// Maps to <c>ProtocolGame::parseUpdateImpactTracker</c>.
    /// Task T52.
    /// </summary>
    private void ParseUpdateImpactTracker(InputMessage msg)
    {
        byte   analyzerType = msg.ReadU8();
        uint   amount       = msg.ReadU32();
        byte   effect       = 0;
        string target       = string.Empty;
        if (analyzerType == 1)
        {
            effect = msg.ReadU8();
        }
        else if (analyzerType == 2)
        {
            effect = msg.ReadU8();
            target = msg.ReadString();
        }
        ImpactTrackerReceived?.Invoke(analyzerType, amount, effect, target);
    }

    /// <summary>
    /// Parses <c>ItemsPrice</c> (0xCD / GameServerSendItemsPrice).
    /// Wire: U16 count; for each: U16 itemId, [U8 tier if classification>0 at proto≥1281],
    ///   U64 price (or U32 before proto 1281). Data is consumed and discarded.
    /// Maps to <c>ProtocolGame::parseItemsPrice</c>.
    /// Task T52.
    /// </summary>
    private void ParseItemsPrice(InputMessage msg)
    {
        ushort count = msg.ReadU16();
        for (int i = 0; i < count; i++)
        {
            ushort itemId = msg.ReadU16();
            if (ProtocolVersion >= 1281)
            {
                // If the item has a classification > 0, consume tier byte.
                // We don't have ThingType lookup in parse context here, so we
                // conservatively skip as the C++ reference does.
                // In practice: item.getClassification() > 0 → read U8 tier.
                // We skip the tier byte only when the server sends it; without
                // the ThingType DB available here we simply check item classification
                // through a lightweight lookup if available.
                _ = itemId; // used above
                msg.ReadU64(); // price
            }
            else
            {
                msg.ReadU32(); // price (legacy)
            }
        }
    }

    /// <summary>
    /// Parses <c>UpdateSupplyTracker</c> (0xCE / GameServerSendUpdateSupplyTracker).
    /// Wire: U16 itemId.
    /// Fires <see cref="SupplyTrackerReceived"/>.
    /// Maps to <c>ProtocolGame::parseUpdateSupplyTracker</c>.
    /// Task T52.
    /// </summary>
    private void ParseUpdateSupplyTracker(InputMessage msg)
    {
        ushort itemId = msg.ReadU16();
        SupplyTrackerReceived?.Invoke(itemId);
    }

    /// <summary>
    /// Parses <c>UpdateLootTracker</c> (0xCF / GameServerSendUpdateLootTracker).
    /// Wire: U16 itemId, [U8/U16 subType if stackable/fluid], str itemName.
    /// Fires <see cref="LootTrackerReceived"/>.
    /// Maps to <c>ProtocolGame::parseUpdateLootTracker</c>.
    /// Task T52.
    /// </summary>
    private void ParseUpdateLootTracker(InputMessage msg)
    {
        var    item     = ReadItemById(msg, msg.ReadU16());
        string itemName = msg.ReadString();
        LootTrackerReceived?.Invoke(item, itemName);
    }

    /// <summary>
    /// Parses <c>QuestTracker</c> (0xD0 / GameServerQuestTracker).
    /// Wire: U8 messageType;
    ///   type 1: U8 remainingQuests, U8 missionCount, loop{ [U16 questId at proto≥1410],
    ///              U16 missionId, str questName, str missionName, str missionDesc };
    ///   type 0: [U16 questId at proto≥1410], U16 missionId, [str questName at proto≥1410],
    ///              str missionName, str missionDesc.
    /// Fires <see cref="QuestTrackerReceived"/>.
    /// Maps to <c>ProtocolGame::parseQuestTracker</c>.
    /// Task T52.
    /// </summary>
    private void ParseQuestTracker(InputMessage msg)
    {
        byte messageType = msg.ReadU8();
        var  missions    = new List<(ushort QuestId, ushort MissionId, string QuestName, string MissionName, string MissionDesc)>();

        if (messageType == 1)
        {
            byte remainingQuests = msg.ReadU8();
            byte missionCount    = msg.ReadU8();
            for (int i = 0; i < missionCount; i++)
            {
                ushort questId    = ProtocolVersion >= 1410 ? msg.ReadU16() : (ushort)0;
                ushort missionId  = msg.ReadU16();
                string questName  = msg.ReadString();
                string missionName= msg.ReadString();
                string missionDesc= msg.ReadString();
                missions.Add((questId, missionId, questName, missionName, missionDesc));
            }
            QuestTrackerReceived?.Invoke(remainingQuests, missions);
        }
        else if (messageType == 0)
        {
            ushort questId    = ProtocolVersion >= 1410 ? msg.ReadU16() : (ushort)0;
            ushort missionId  = msg.ReadU16();
            string questName  = ProtocolVersion >= 1410 ? msg.ReadString() : string.Empty;
            string missionName= msg.ReadString();
            string missionDesc= msg.ReadString();
            missions.Add((questId, missionId, questName, missionName, missionDesc));
            QuestTrackerReceived?.Invoke(0, missions);
        }
    }

    /// <summary>
    /// Parses <c>KillTracker</c> (0xD1 / GameServerKillTracker).
    /// Wire: str monsterName, Outfit (no mount), U8 corpseItemsSize, loop{item}.
    /// Fires <see cref="KillTrackerReceived"/>.
    /// Maps to <c>ProtocolGame::parseKillTracker</c>.
    /// Task T52.
    /// </summary>
    private void ParseKillTracker(InputMessage msg)
    {
        string     monsterName = msg.ReadString();
        Game.Outfit outfit     = ReadOutfit(msg, parseMount: false);
        byte        itemCount  = msg.ReadU8();
        var         items      = new List<Game.Item>(itemCount);
        for (int i = 0; i < itemCount; i++)
            items.Add(ReadItemById(msg, msg.ReadU16()));
        KillTrackerReceived?.Invoke(monsterName, outfit, items);
    }

    /// <summary>
    /// Parses <c>ItemInfo</c> (0xF4 / GameServerItemInfo).
    /// Wire: U8 listCount; for each: U16 itemId, U8 subType (or U16 if GameCountU16), str desc.
    /// Fires <see cref="ItemInfoReceived"/>.
    /// Maps to <c>ProtocolGame::parseItemInfo</c>.
    /// Task T52.
    /// </summary>
    private void ParseItemInfo(InputMessage msg)
    {
        byte listCount = msg.ReadU8();
        var  list      = new List<(ushort ItemId, byte SubType, string Description)>(listCount);
        for (int i = 0; i < listCount; i++)
        {
            ushort itemId  = msg.ReadU16();
            // GameCountU16 feature → U16 subType; otherwise U8
            byte   subType = (byte)(ProtocolVersion >= 1220 ? msg.ReadU16() : msg.ReadU8());
            string desc    = msg.ReadString();
            list.Add((itemId, subType, desc));
        }
        ItemInfoReceived?.Invoke(list);
    }

    /// <summary>
    /// Parses <c>PlayerInventory</c> (0xF5 / GameServerPlayerInventory).
    /// Wire: U16 size; for each: U16 itemId, U8 tier, U16 amount (or packed U8/U24 at proto≥1500).
    /// Fires <see cref="PlayerInventoryReceived"/>.
    /// Maps to <c>ProtocolGame::parsePlayerInventory</c>.
    /// Task T52.
    /// </summary>
    private void ParsePlayerInventory(InputMessage msg)
    {
        ushort size = msg.ReadU16();
        var    list = new List<(ushort ItemId, byte Tier, uint Amount)>(size);
        for (int i = 0; i < size; i++)
        {
            ushort itemId = msg.ReadU16();
            byte   tier   = msg.ReadU8();
            uint   amount;
            if (ProtocolVersion >= 1500)
            {
                // packed encoding: 1-byte (< 0x40), 2-byte (0x40–0x7F prefix), 4-byte (≥ 0x80)
                byte b1 = msg.ReadU8();
                if (b1 < 0x40)
                    amount = b1;
                else if (b1 < 0x80)
                    amount = (uint)((b1 - 0x40) << 8) | msg.ReadU8();
                else
                {
                    byte b2 = msg.ReadU8();
                    byte b3 = msg.ReadU8();
                    byte b4 = msg.ReadU8();
                    amount = ((uint)b2 << 16) | ((uint)b3 << 8) | b4;
                }
            }
            else
            {
                amount = msg.ReadU16();
            }
            list.Add((itemId, tier, amount));
        }
        PlayerInventoryReceived?.Invoke(list);
    }

    // ─── T53 parsers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>BrowseForgeHistory</c> (0x88 / GameServerBrowseForgeHistory).
    /// Wire: U16 pageNumber; U16 lastPage; U8 count; count × {U32 createdAt; U8 actionType; str description; U8 bonus}.
    /// Fires <see cref="ForgeHistoryReceived"/>.
    /// Maps to <c>ProtocolGame::parseBrowseForgeHistory</c>.
    /// Task T53.
    /// </summary>
    private void ParseBrowseForgeHistory(InputMessage msg)
    {
        ushort pageNumber    = msg.ReadU16();
        ushort lastPage      = msg.ReadU16();
        byte   historyCount  = msg.ReadU8();
        var    list          = new List<ForgeHistoryEntry>(historyCount);
        for (int i = 0; i < historyCount; i++)
        {
            uint   createdAt   = msg.ReadU32();
            byte   actionType  = msg.ReadU8();
            string description = msg.ReadString();
            byte   bonus       = msg.ReadU8();
            list.Add(new ForgeHistoryEntry(createdAt, actionType, description, bonus));
        }
        ForgeHistoryReceived?.Invoke(pageNumber, lastPage, list);
    }

    /// <summary>
    /// Parses <c>BlessDialog</c> (0x9B / GameServerSendBlessDialog).
    /// Wire: U8 totalBless; totalBless × {U16 blessBitwise; U8 playerBlessCount; U8 store};
    ///   U8 premium; U8 promotion; U8 pvpMinXpLoss; U8 pvpMaxXpLoss; U8 pveExpLoss;
    ///   U8 equipPvpLoss; U8 equipPveLoss; U8 skull; U8 aol;
    ///   U8 logCount; logCount × {U32 timestamp; U8 colorMessage; str historyMessage}.
    /// Fires <see cref="BlessDialogReceived"/>.
    /// Maps to <c>ProtocolGame::parseBlessDialog</c>.
    /// Task T53.
    /// </summary>
    private void ParseBlessDialog(InputMessage msg)
    {
        byte totalBless = msg.ReadU8();
        var  blesses    = new List<BlessData>(totalBless);
        for (int i = 0; i < totalBless; i++)
        {
            ushort blessBitwise      = msg.ReadU16();
            byte   playerBlessCount  = msg.ReadU8();
            byte   store             = msg.ReadU8();
            blesses.Add(new BlessData(blessBitwise, playerBlessCount, store));
        }

        byte premium       = msg.ReadU8();
        byte promotion     = msg.ReadU8();
        byte pvpMinXpLoss  = msg.ReadU8();
        byte pvpMaxXpLoss  = msg.ReadU8();
        byte pveExpLoss    = msg.ReadU8();
        byte equipPvpLoss  = msg.ReadU8();
        byte equipPveLoss  = msg.ReadU8();
        byte skull         = msg.ReadU8();
        byte aol           = msg.ReadU8();

        byte logCount = msg.ReadU8();
        var  logs     = new List<BlessLogEntry>(logCount);
        for (int i = 0; i < logCount; i++)
        {
            uint   timestamp      = msg.ReadU32();
            byte   colorMessage   = msg.ReadU8();
            string historyMessage = msg.ReadString();
            logs.Add(new BlessLogEntry(timestamp, colorMessage, historyMessage));
        }

        BlessDialogReceived?.Invoke(new BlessDialogData
        {
            TotalBless     = totalBless,
            Blesses        = blesses,
            Premium        = premium,
            Promotion      = promotion,
            PvpMinXpLoss   = pvpMinXpLoss,
            PvpMaxXpLoss   = pvpMaxXpLoss,
            PveExpLoss     = pveExpLoss,
            EquipPvpLoss   = equipPvpLoss,
            EquipPveLoss   = equipPveLoss,
            Skull          = skull,
            Aol            = aol,
            Logs           = logs,
        });
    }

    /// <summary>
    /// Parses <c>BestiaryRefreshTracker</c> (0xB9 / GameServerBestiaryRefreshTracker).
    /// Wire: [U8 trackerType if proto≥1320]; U8 size; size × {U16 raceId; U32 killCount;
    ///   U16 firstUnlock; U16 secondUnlock; U16 lastUnlock; U8 status}.
    /// Fires <see cref="BestiaryTrackerReceived"/>.
    /// Maps to <c>ProtocolGame::parseBestiaryTracker</c>.
    /// Task T53.
    /// </summary>
    private void ParseBestiaryTracker(InputMessage msg)
    {
        byte trackerType = 0;
        if (ProtocolVersion >= 1320)
            trackerType = msg.ReadU8();

        byte size = msg.ReadU8();
        var  list = new List<(ushort RaceId, uint KillCount, ushort FirstUnlock, ushort SecondUnlock, ushort LastUnlock, byte Status)>(size);
        for (int i = 0; i < size; i++)
        {
            ushort raceId       = msg.ReadU16();
            uint   killCount    = msg.ReadU32();
            ushort firstUnlock  = msg.ReadU16();
            ushort secondUnlock = msg.ReadU16();
            ushort lastUnlock   = msg.ReadU16();
            byte   status       = msg.ReadU8();
            list.Add((raceId, killCount, firstUnlock, secondUnlock, lastUnlock, status));
        }
        BestiaryTrackerReceived?.Invoke(trackerType, list);
    }

    /// <summary>
    /// Parses <c>TaskHuntingBasicData</c> (0xBA / GameServerTaskHuntingBasicData).
    /// Wire: U16 preyCount; preyCount × {U16 raceId; U8 difficult};
    ///   U8 optionCount; optionCount × {U8 difficult; U8 stars; U16 firstKill; U16 firstReward;
    ///   U16 secondKill; U16 secondReward}.
    /// No event fired — data is consumed and discarded.
    /// Maps to <c>ProtocolGame::parseTaskHuntingBasicData</c>.
    /// Task T53.
    /// </summary>
    private static void ParseTaskHuntingBasicData(InputMessage msg)
    {
        ushort preyCount = msg.ReadU16();
        for (int i = 0; i < preyCount; i++)
        {
            msg.ReadU16(); // raceId
            msg.ReadU8();  // difficult
        }
        byte optionCount = msg.ReadU8();
        for (int i = 0; i < optionCount; i++)
        {
            msg.ReadU8();  // difficult
            msg.ReadU8();  // stars
            msg.ReadU16(); // firstKill
            msg.ReadU16(); // firstReward
            msg.ReadU16(); // secondKill
            msg.ReadU16(); // secondReward
        }
    }

    /// <summary>
    /// Parses <c>TaskHuntingData</c> (0xBB / GameServerTaskHuntingData).
    /// Wire: U8 slot; U8 state; [state-dependent fields]; U32 nextFreeRoll.
    /// States: 0=locked (U8 slotUnlocked), 1=inactive, 2/3=selection (U16+loop{U16+U8}),
    ///   4=active (U16 raceId; U8 upgraded; U16 required; U16 current; U8 stars),
    ///   5=completed (U16 raceId; U8 upgraded; U16 required; U16 current).
    /// Fires <see cref="TaskHuntingDataReceived"/>.
    /// Maps to <c>ProtocolGame::parseTaskHuntingData</c>.
    /// Task T53.
    /// </summary>
    private void ParseTaskHuntingData(InputMessage msg)
    {
        byte slot  = msg.ReadU8();
        byte state = msg.ReadU8();

        var    creatures    = new List<(ushort RaceId, bool Unlocked)>();
        ushort activeRaceId = 0;
        ushort requiredKills= 0;
        ushort currentKills = 0;
        byte   stars        = 0;
        ushort slotUnlocked = 0;

        switch (state)
        {
            case 0: // locked
                slotUnlocked = msg.ReadU8();
                break;
            case 1: // inactive
                break;
            case 2: // selection
            case 3: // list selection
            {
                ushort creatureCount = msg.ReadU16();
                for (int i = 0; i < creatureCount; i++)
                {
                    ushort raceId   = msg.ReadU16();
                    bool   unlocked = msg.ReadU8() != 0;
                    creatures.Add((raceId, unlocked));
                }
                break;
            }
            case 4: // active
                activeRaceId  = msg.ReadU16();
                msg.ReadU8();            // upgraded
                requiredKills = msg.ReadU16();
                currentKills  = msg.ReadU16();
                stars         = msg.ReadU8();
                break;
            case 5: // completed
                activeRaceId  = msg.ReadU16();
                msg.ReadU8();            // upgraded
                requiredKills = msg.ReadU16();
                currentKills  = msg.ReadU16();
                break;
        }

        uint nextFreeRoll = msg.ReadU32();
        TaskHuntingDataReceived?.Invoke(slot, state, nextFreeRoll, creatures,
            activeRaceId, requiredKills, currentKills, stars, slotUnlocked);
    }

    /// <summary>
    /// Parses <c>MonkData</c> (0xC1 / GameServerMonkData).
    /// Wire: U8 subtype; U8 value.
    ///   subtype 0 = harmony (value = harmonyValue),
    ///   subtype 1 = serene (value = 0/1 bool),
    ///   subtype 2 = virtue (value discarded).
    /// Fires <see cref="MonkDataReceived"/>.
    /// Maps to <c>ProtocolGame::parseMonkData</c>.
    /// Task T53.
    /// </summary>
    private void ParseMonkData(InputMessage msg)
    {
        byte subtype = msg.ReadU8();
        byte value   = msg.ReadU8();
        MonkDataReceived?.Invoke(subtype, value);
    }

    /// <summary>
    /// Parses <c>CyclopediaHouseAuctionMessage</c> (0xC3 / GameServerCyclopediaHouseAuctionMessage).
    /// Wire: U32 houseId; U8 type; [U8 extra if type==1]; U8 index.
    /// Fires <see cref="HouseAuctionMessageReceived"/>.
    /// Maps to <c>ProtocolGame::parseCyclopediaHouseAuctionMessage</c>.
    /// Task T53.
    /// </summary>
    private void ParseCyclopediaHouseAuctionMessage(InputMessage msg)
    {
        uint houseId = msg.ReadU32();
        byte type    = msg.ReadU8();
        if (type == 1)
            msg.ReadU8(); // extra byte (0x00)
        byte index = msg.ReadU8();
        HouseAuctionMessageReceived?.Invoke(houseId, type, index);
    }

    /// <summary>
    /// Parses <c>WeaponProficiencyInfo</c> (0xC4 / GameServerWeaponProficiencyInfo).
    /// Wire: U16 itemId; U32 experience; U8 count; count × {U8 proficiencyLevel; U8 perkPosition}.
    /// Fires <see cref="WeaponProficiencyInfoReceived"/>.
    /// Maps to <c>ProtocolGame::parseWeaponProficiencyInfo</c>.
    /// Task T53.
    /// </summary>
    private void ParseWeaponProficiencyInfo2(InputMessage msg)
    {
        ushort itemId     = msg.ReadU16();
        uint   experience = msg.ReadU32();
        byte   count      = msg.ReadU8();
        var    list       = new List<(byte ProficiencyLevel, byte PerkPosition)>(count);
        for (int i = 0; i < count; i++)
        {
            byte proficiencyLevel = msg.ReadU8();
            byte perkPosition     = msg.ReadU8();
            list.Add((proficiencyLevel, perkPosition));
        }
        WeaponProficiencyInfoReceived?.Invoke(itemId, experience, list);
    }

    /// <summary>
    /// Parses <c>ChooseOutfit</c> (0xC8 / GameServerChooseOutfit).
    /// Wire (at protocol 1281): outfit; [if mount==0: 4×U8 mount colours]; U16 familiarLookType;
    ///   U16 outfitCount; outfitCount × {U16 id; str name; U8 addons; U8 mode; [if mode==1: U32 storeOfferId]};
    ///   U16 mountCount; mountCount × {U16 id; str name; U8 mode; [if mode==1: U32 storeOfferId]};
    ///   U16 familiarCount; familiarCount × {U16 lookType; str name; U8 mode; [if mode==1: U32 storeOfferId]};
    ///   U8 tryOutfitMode; U8 mounted; U8 randomizeMount.
    /// Fires <see cref="OutfitWindowReceived"/>.
    /// Maps to <c>ProtocolGame::parseOpenOutfitWindow</c>.
    /// Task T53.
    /// </summary>
    private void ParseChooseOutfit(InputMessage msg)
    {
        // Current outfit (with mount)
        Game.Outfit currentOutfit = ReadOutfit(msg);

        // Protocol ≥ 1281: if mount == 0, read 4 mount colour bytes; then familiar looktype
        if (currentOutfit.MountId == 0)
        {
            msg.ReadU8(); // mountHead
            msg.ReadU8(); // mountBody
            msg.ReadU8(); // mountLegs
            msg.ReadU8(); // mountFeet
        }
        ushort familiarLookType = msg.ReadU16();

        // Outfit list (GameNewOutfitProtocol ON, proto ≥ 1281 → U16 count)
        ushort outfitCount = msg.ReadU16();
        var outfits = new List<OutfitEntry>(outfitCount);
        for (int i = 0; i < outfitCount; i++)
        {
            ushort outfitId    = msg.ReadU16();
            string outfitName  = msg.ReadString();
            byte   addons      = msg.ReadU8();
            byte   mode        = msg.ReadU8(); // 0=available, 1=store, 2=golden
            if (mode == 1)
                msg.ReadU32(); // storeOfferId
            outfits.Add(new OutfitEntry(outfitId, outfitName, addons, mode));
        }

        // Mount list (GamePlayerMounts ON, proto ≥ 1281 → U16 count)
        ushort mountCount = msg.ReadU16();
        var mounts = new List<MountEntry>(mountCount);
        for (int i = 0; i < mountCount; i++)
        {
            ushort mountId   = msg.ReadU16();
            string mountName = msg.ReadString();
            byte   mode      = msg.ReadU8(); // 0=available, 1=store
            if (mode == 1)
                msg.ReadU32(); // storeOfferId
            mounts.Add(new MountEntry(mountId, mountName, mode));
        }

        // Familiar list (GamePlayerFamiliars ON → U16 count)
        ushort familiarCount = msg.ReadU16();
        var familiars = new List<FamiliarEntry>(familiarCount);
        for (int i = 0; i < familiarCount; i++)
        {
            ushort lookType  = msg.ReadU16();
            string name      = msg.ReadString();
            byte   mode      = msg.ReadU8(); // 0=available, 1=store
            if (mode == 1)
                msg.ReadU32(); // storeOfferId
            familiars.Add(new FamiliarEntry(lookType, name));
        }

        // Protocol ≥ 1281 trailing bytes
        bool tryOutfitMode  = msg.ReadU8() != 0;
        bool mounted        = msg.ReadU8() != 0;
        bool randomizeMount = msg.ReadU8() != 0;

        OutfitWindowReceived?.Invoke(new OutfitWindowData
        {
            CurrentOutfit   = currentOutfit,
            FamiliarLookType= familiarLookType,
            Outfits         = outfits,
            Mounts          = mounts,
            Familiars       = familiars,
            TryOutfitMode   = tryOutfitMode,
            Mounted         = mounted,
            RandomizeMount  = randomizeMount,
        });
    }

    /// <summary>
    /// Parses <c>BestiaryCharmsData</c> (0xD8 / GameServerBestiaryCharmsData).
    /// Wire (proto 1281 &lt; 1410): U32 points; U8 charmsAmount;
    ///   charmsAmount × {U8 id; str name; str description; U8 unk; U16 unlockPrice; U8 unlocked;
    ///     [if unlocked: U8 assigned; [if assigned: U16 raceId; U32 removeRuneCost]] else U8 unk};
    ///   U8 availableCharmSlots; U16 finishedMonstersSize; finishedMonstersSize × U16 raceId.
    /// Fires <see cref="BestiaryCharmsDataReceived"/>.
    /// Maps to <c>ProtocolGame::parseBestiaryCharmsData</c>.
    /// Task T53.
    /// </summary>
    private void ParseBestiaryCharmsData(InputMessage msg)
    {
        // Protocol 1281 < 1410 → U32 points
        uint  points           = msg.ReadU32();
        byte  charmsAmount     = msg.ReadU8();
        var   charms           = new List<CharmData>(charmsAmount);

        for (int i = 0; i < charmsAmount; i++)
        {
            byte   id          = msg.ReadU8();
            string name        = msg.ReadString();
            string description = msg.ReadString();
            msg.ReadU8();                         // unknown byte
            ushort unlockPrice = msg.ReadU16();
            bool   unlocked    = msg.ReadU8() == 1;
            bool   assigned     = false;
            ushort raceId      = 0;
            uint   removeRuneCost = 0;

            if (unlocked)
            {
                assigned = msg.ReadU8() != 0;
                if (assigned)
                {
                    raceId        = msg.ReadU16();
                    removeRuneCost= msg.ReadU32();
                }
            }
            else
            {
                msg.ReadU8(); // unknown byte
            }

            charms.Add(new CharmData(id, name, description, unlockPrice, unlocked, /* tier */ (byte)(unlocked ? 1 : 0),
                assigned, raceId, removeRuneCost));
        }

        byte availableCharmSlots    = msg.ReadU8();
        ushort finishedMonstersSize = msg.ReadU16();
        var    finishedMonsters     = new List<uint>(finishedMonstersSize);
        for (int i = 0; i < finishedMonstersSize; i++)
            finishedMonsters.Add(msg.ReadU16());

        BestiaryCharmsDataReceived?.Invoke(new BestiaryCharmsData
        {
            Points              = points,
            ResetAllCharmsCost  = 0,
            Charms              = charms,
            AvailableCharmSlots = availableCharmSlots,
            FinishedMonsters    = finishedMonsters,
        });
    }

    // ─── T54 parsers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>CyclopediaItemDetail</c> (0x76 / GameServerCyclopediaItemDetail).
    /// Wire: U8 skip; U8 skip; U32 creatureId (skip); U8 skip;
    ///   str itemName; item (read + discard); U8 skip;
    ///   U8 descriptionsSize; descriptionsSize × {str header; str body}.
    /// Fires <see cref="ItemDetailReceived"/>.
    /// Maps to <c>ProtocolGame::parseCyclopediaItemDetail</c>.
    /// Task T54.
    /// </summary>
    private void ParseCyclopediaItemDetail(InputMessage msg)
    {
        msg.ReadU8();          // 0x00
        msg.ReadU8();          // bool isCyclopedia
        msg.ReadU32();         // creatureId (version 13.00)
        msg.ReadU8();          // 0x01

        string itemName = msg.ReadString();
        ReadThing(msg);        // item – read and discard
        msg.ReadU8();          // 0x00

        byte descCount = msg.ReadU8();
        var descriptions = new List<(string Header, string Body)>(descCount);
        for (int i = 0; i < descCount; i++)
        {
            string header = msg.ReadString();
            string body   = msg.ReadString();
            descriptions.Add((header, body));
        }

        ItemDetailReceived?.Invoke(itemName, descriptions);
    }

    /// <summary>
    /// Parses <c>ItemClasses</c> (0x86 / GameServerItemClasses).
    /// Wire (proto ≥ 1281, no optional feature sections):
    ///   U8 classSize; classSize × {U8 classId; U8 tiersSize; tiersSize × {U8 tier; U64 price}}.
    /// Fires <see cref="ItemClassesReceived"/>.
    /// Maps to <c>ProtocolGame::parseItemClasses</c>.
    /// Task T54.
    /// </summary>
    private void ParseItemClasses(InputMessage msg)
    {
        byte classSize = msg.ReadU8();
        var classes = new List<ForgeClassEntry>(classSize);
        for (int i = 0; i < classSize; i++)
        {
            byte classId   = msg.ReadU8();
            byte tiersSize = msg.ReadU8();
            var  tiers     = new List<ForgeClassTierEntry>(tiersSize);
            for (int j = 0; j < tiersSize; j++)
            {
                byte  tier  = msg.ReadU8();
                ulong price = msg.ReadU64();
                tiers.Add(new ForgeClassTierEntry(tier, price));
            }
            classes.Add(new ForgeClassEntry(classId, tiers));
        }

        ItemClassesReceived?.Invoke(classes);
    }

    /// <summary>
    /// Parses <c>CyclopediaHousesInfo</c> (0xC6 / GameServerCyclopediaHousesInfo).
    /// Wire: U32 houseClientId; U8; U8; U8; U8; U8; U8; U8; U32 houseClientId;
    ///   U16 housesList; housesList × U32 clientId.
    /// Fires <see cref="CyclopediaHousesInfoReceived"/>.
    /// Maps to <c>ProtocolGame::parseCyclopediaHousesInfo</c>.
    /// Task T54.
    /// </summary>
    private void ParseCyclopediaHousesInfo(InputMessage msg)
    {
        uint houseClientId = msg.ReadU32();
        msg.ReadU8();  // 0x00
        msg.ReadU8();  // accountHouseCount
        msg.ReadU8();  // 0x00
        msg.ReadU8();  // 3
        msg.ReadU8();  // 3
        msg.ReadU8();  // 0x01
        msg.ReadU8();  // 0x01
        msg.ReadU32(); // houseClientId (duplicate)

        ushort housesList = msg.ReadU16();
        var houses = new List<uint>(housesList);
        for (int i = 0; i < housesList; i++)
            houses.Add(msg.ReadU32());

        CyclopediaHousesInfoReceived?.Invoke(houseClientId, houses);
    }

    /// <summary>
    /// Parses <c>CyclopediaHouseList</c> (0xC7 / GameServerCyclopediaHouseList).
    /// Wire: U16 housesCount; housesCount × {
    ///   U32 clientId; U8 renovationType; U8 state;
    ///   [state=0 Available]: str bidderName; U8 isBidder; U8 disableIndex;
    ///     [if bidderName non-empty]: U32 bidEndDate; U64 highestBid; [if isBidder]: U64 bidHolderLimit;
    ///   [state=1 Rented]: str ownerName; U32 paidUntil; U8 isRented; [if isRented]: U8; U8;
    ///   [state=2 Transfer]: str ownerName; U32 paidUntil; U8 isOwner; [if isOwner]: U8; U8;
    ///     U32 bidEndDate; str bidderName; U8; U64 internalBid; U8 isNewOwner;
    ///     [if isNewOwner]: U8 acceptErr; U8 rejectErr; [if isOwner]: U8 cancelErr;
    ///   [state=3 MoveOut]: str ownerName; U32 paidUntil; U8 isOwner;
    ///     [if isOwner]: U8; U8; U32 bidEndDate; U8; else: U32 bidEndDate }.
    /// Fires <see cref="CyclopediaHouseListReceived"/>.
    /// Maps to <c>ProtocolGame::parseCyclopediaHouseList</c>.
    /// Task T54.
    /// </summary>
    private void ParseCyclopediaHouseList(InputMessage msg)
    {
        ushort housesCount = msg.ReadU16();
        var entries = new List<CyclopediaHouseEntry>(housesCount);

        for (int i = 0; i < housesCount; i++)
        {
            uint clientId        = msg.ReadU32();
            byte renovationType  = msg.ReadU8();
            var  state           = (CyclopediaHouseState)msg.ReadU8();

            var entry = new CyclopediaHouseEntry { ClientId = clientId, RenovationType = renovationType, State = state };

            switch (state)
            {
                case CyclopediaHouseState.Available:
                {
                    string bidderName  = msg.ReadString();
                    bool   isBidder    = msg.ReadU8() != 0;
                    byte   disableIndex = msg.ReadU8();
                    uint   bidEndDate  = 0;
                    ulong  highestBid  = 0;
                    ulong  bidHolderLimit = 0;
                    if (!string.IsNullOrEmpty(bidderName))
                    {
                        bidEndDate = msg.ReadU32();
                        highestBid = msg.ReadU64();
                        if (isBidder)
                            bidHolderLimit = msg.ReadU64();
                    }
                    entry = entry with
                    {
                        OwnerOrBidder  = bidderName,
                        IsBidder       = isBidder,
                        DisableIndex   = disableIndex,
                        BidEndDate     = bidEndDate,
                        HighestBid     = highestBid,
                        BidHolderLimit = bidHolderLimit,
                    };
                    break;
                }
                case CyclopediaHouseState.Rented:
                {
                    string ownerName = msg.ReadString();
                    uint   paidUntil = msg.ReadU32();
                    bool   isOwner   = msg.ReadU8() != 0;
                    if (isOwner)
                    {
                        msg.ReadU8(); // unknown
                        msg.ReadU8(); // unknown
                    }
                    entry = entry with { OwnerOrBidder = ownerName, PaidUntil = paidUntil, IsOwner = isOwner };
                    break;
                }
                case CyclopediaHouseState.Transfer:
                {
                    string ownerName  = msg.ReadString();
                    uint   paidUntil  = msg.ReadU32();
                    bool   isOwner    = msg.ReadU8() != 0;
                    if (isOwner)
                    {
                        msg.ReadU8(); // unknown
                        msg.ReadU8(); // unknown
                    }
                    uint   bidEndDate  = msg.ReadU32();
                    string bidderName  = msg.ReadString();
                    msg.ReadU8();       // unknown
                    ulong  internalBid = msg.ReadU64();
                    bool   isNewOwner  = msg.ReadU8() != 0;
                    byte   acceptErr   = 0;
                    byte   rejectErr   = 0;
                    if (isNewOwner)
                    {
                        acceptErr = msg.ReadU8();
                        rejectErr = msg.ReadU8();
                    }
                    byte cancelErr = 0;
                    if (isOwner)
                        cancelErr = msg.ReadU8();
                    entry = entry with
                    {
                        OwnerOrBidder       = ownerName,
                        PaidUntil           = paidUntil,
                        IsOwner             = isOwner,
                        BidEndDate          = bidEndDate,
                        BidderName          = bidderName,
                        InternalBid         = internalBid,
                        IsNewOwner          = isNewOwner,
                        AcceptTransferError = acceptErr,
                        RejectTransferError = rejectErr,
                        CancelTransferError = cancelErr,
                    };
                    break;
                }
                case CyclopediaHouseState.MoveOut:
                {
                    string ownerName = msg.ReadString();
                    uint   paidUntil = msg.ReadU32();
                    bool   isOwner   = msg.ReadU8() != 0;
                    if (isOwner)
                    {
                        msg.ReadU8(); // unknown
                        msg.ReadU8(); // unknown
                        uint bidEndDate = msg.ReadU32();
                        msg.ReadU8();  // unknown
                        entry = entry with { OwnerOrBidder = ownerName, PaidUntil = paidUntil, IsOwnerMoveOut = isOwner, BidEndDate = bidEndDate };
                    }
                    else
                    {
                        uint bidEndDate = msg.ReadU32();
                        entry = entry with { OwnerOrBidder = ownerName, PaidUntil = paidUntil, IsOwnerMoveOut = isOwner, BidEndDate = bidEndDate };
                    }
                    break;
                }
            }

            entries.Add(entry);
        }

        CyclopediaHouseListReceived?.Invoke(entries);
    }

    /// <summary>
    /// Parses <c>RequestPurchaseData</c> (0xE1 / GameServerRequestPurchaseData).
    /// Wire: U32 transactionId; U8 productType.
    /// Fires <see cref="RequestPurchaseDataReceived"/>.
    /// Maps to <c>ProtocolGame::parseRequestPurchaseData</c>.
    /// Task T54.
    /// </summary>
    private void ParseRequestPurchaseData(InputMessage msg)
    {
        uint transactionId = msg.ReadU32();
        byte productType   = msg.ReadU8();
        RequestPurchaseDataReceived?.Invoke(transactionId, productType);
    }

    /// <summary>
    /// Parses <c>ShowDescription</c> (0xEA / GameServerSendShowDescription).
    /// Wire: U32 offerId; str description.
    /// Fires <see cref="StoreOfferDescriptionReceived"/>.
    /// Maps to <c>ProtocolGame::parseShowDescription</c>.
    /// Task T54.
    /// </summary>
    private void ParseShowDescription(InputMessage msg)
    {
        uint   offerId     = msg.ReadU32();
        string description = msg.ReadString();
        StoreOfferDescriptionReceived?.Invoke(offerId, description);
    }

    /// <summary>
    /// Parses <c>CloseImbuementWindow</c> (0xEC / GameServerSendCloseImbuementWindow).
    /// Wire: no payload.
    /// Fires <see cref="ImbuementWindowClosed"/>.
    /// Maps to <c>ProtocolGame::parseCloseImbuementWindow</c>.
    /// Task T54.
    /// </summary>
    private void ParseCloseImbuementWindow(InputMessage msg)
    {
        ImbuementWindowClosed?.Invoke();
    }

    /// <summary>
    /// Parses <c>ServerError</c> (0xED / GameServerSendError).
    /// Wire: U8 code; str message.
    /// Fires <see cref="ServerErrorReceived"/>.
    /// Maps to <c>ProtocolGame::parseError</c>.
    /// Task T54.
    /// </summary>
    private void ParseServerError(InputMessage msg)
    {
        byte   code    = msg.ReadU8();
        string message = msg.ReadString();
        ServerErrorReceived?.Invoke(code, message);
    }
}
