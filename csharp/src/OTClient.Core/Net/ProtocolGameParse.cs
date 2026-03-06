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

    // ─── Game-world entry + map description (T01) ────────────────────────────

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

    private void ParsePlayerSpeech(InputMessage msg)
    {
        string   author  = msg.ReadString();
        var      mode    = (ChatMode)msg.ReadU8();
        string   content = msg.ReadString();
        SpeechReceived?.Invoke(author, mode, content);
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
        msg.ReadU16(); // regeneration (offline training time placeholder)
        msg.ReadU16(); // offline training time

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
            stamina, soul);
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

        _ = baseMagicLevel;  // available if caller wants it

        // 7 combat skills: Fist, Club, Sword, Axe, Distance, Shielding, Fishing.
        // Protocol 1281: level U16, baseLevel U16, loyalty U16, percent (U16/100).
        int[] levels   = new int[SkillCount];
        int[] percents = new int[SkillCount];
        for (int i = 0; i < SkillCount; i++)
        {
            levels[i]   = msg.ReadU16();
            msg.ReadU16(); // baseLevel
            msg.ReadU16(); // loyalty bonus
            percents[i] = msg.ReadU16() / 100;
        }

        PlayerSkillsUpdated?.Invoke(magicLevel, magicLevelPercent, levels, percents);
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
    private static Game.Outfit ReadOutfit(InputMessage msg)
    {
        int lookType = msg.ReadU16();
        if (lookType != 0)
        {
            byte head   = msg.ReadU8();
            byte body   = msg.ReadU8();
            byte legs   = msg.ReadU8();
            byte feet   = msg.ReadU8();
            byte addons = msg.ReadU8();

            int mountId = msg.ReadU16();
            if (mountId != 0)
            {
                // Protocol 1281: mount colour bytes follow mount ID
                msg.ReadU8(); // mountHead
                msg.ReadU8(); // mountBody
                msg.ReadU8(); // mountLegs
                msg.ReadU8(); // mountFeet
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
}
