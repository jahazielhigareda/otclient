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

    // ─── Game-world entry ─────────────────────────────────────────────────────

    private void ParseInitGame(InputMessage msg)
    {
        // Enable XTEA encryption for all subsequent game-server packets.
        // The InitGame packet also carries the XTEA key sent from the server
        // (for game-server connections); for login-server connections the key
        // was already negotiated via the RSA block.
        _encryptEnabled = true;
        IsInGame = true;

        // Read the 4-word XTEA key echoed back by the game server (if present)
        if (msg.Remaining >= 16)
        {
            for (int i = 0; i < 4; i++)
                _xteaKey[i] = msg.ReadU32();
        }

        GameEntered?.Invoke();
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
}
