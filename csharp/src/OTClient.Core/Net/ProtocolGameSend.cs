namespace OTClient.Framework.Net;

/// <summary>
/// Outgoing (client → server) packet methods for <see cref="ProtocolGame"/>.
/// Maps to <c>src/client/protocolgamesend.cpp</c>.
/// Task 5.7.
/// </summary>
public sealed partial class ProtocolGame
{
    // ─── Login server ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends the first login packet to the <em>login server</em>.
    /// The RSA-encrypted block contains the XTEA session key and credentials.
    /// </summary>
    /// <param name="accountName">Account name or e-mail.</param>
    /// <param name="password">Account password.</param>
    /// <param name="characterName">Character to play (sent inside RSA block).</param>
    /// <param name="os">Operating system identifier (1 = Windows, 2 = Linux, 3 = macOS).</param>
    /// <param name="version">Client version (e.g. 1212 for Tibia 12.12).</param>
    /// <param name="contentVersion">Content/dat CRC version.</param>
    public void SendLoginRequest(
        string accountName,
        string password,
        string characterName = "",
        ushort os            = 2,
        ushort version       = 1212,
        uint   contentVersion = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        _accountName   = accountName;
        _password      = password;
        _characterName = characterName;

        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.LoginRequest);
        msg.WriteU16(os);
        msg.WriteU16(version);
        msg.WriteU32(contentVersion);

        // ── RSA block (128 bytes) ─────────────────────────────────────────────
        // The RSA block is filled then encrypted with the server's public key.
        // Layout inside the block:
        //   [0]       = 0x00  (indicates valid RSA plaintext)
        //   [1..4]    = XTEA key word 0
        //   [5..8]    = XTEA key word 1
        //   [9..12]   = XTEA key word 2
        //   [13..16]  = XTEA key word 3
        //   [17..]    = account name (Pascal string)
        //               password     (Pascal string)
        //   [rest]    = zero padding
        // Total: 128 bytes (1024-bit RSA key).
        var rsaBlock = new byte[128];
        rsaBlock[0] = 0x00; // RSA plaintext marker
        for (int i = 0; i < 4; i++)
        {
            uint k = _xteaKey[i];
            int  b = 1 + i * 4;
            rsaBlock[b]     = (byte)k;
            rsaBlock[b + 1] = (byte)(k >> 8);
            rsaBlock[b + 2] = (byte)(k >> 16);
            rsaBlock[b + 3] = (byte)(k >> 24);
        }

        // Write credentials into the RSA block after the key (position 17)
        using var inner = new System.IO.MemoryStream(rsaBlock, 17, rsaBlock.Length - 17, writable: true);
        using var bw    = new System.IO.BinaryWriter(inner, System.Text.Encoding.UTF8, leaveOpen: true);
        byte[] accBytes = System.Text.Encoding.UTF8.GetBytes(accountName);
        byte[] pwdBytes = System.Text.Encoding.UTF8.GetBytes(password);
        bw.Write((ushort)accBytes.Length);
        bw.Write(accBytes);
        bw.Write((ushort)pwdBytes.Length);
        bw.Write(pwdBytes);

        if (RsaHelper.HasPublicKey)
            RsaHelper.Encrypt(rsaBlock);

        msg.WriteBytes(rsaBlock);
        Send(msg);
    }

    // ─── Game server ──────────────────────────────────────────────────────────

    /// <summary>
    /// Sends the <c>EnterGame</c> packet after the login server approves the session.
    /// Enables XTEA encryption for subsequent game-server communication.
    /// </summary>
    public void SendEnterGame()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.EnterGame);
        if (_encryptEnabled)
            SendEncrypted(msg, _xteaKey);
        else
            Send(msg);
    }

    /// <summary>Sends a <c>QuitGame</c> packet to gracefully disconnect.</summary>
    public void SendQuitGame()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.QuitGame);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>Replies to a server <c>Ping</c> with a <c>PingBack</c> packet.</summary>
    public void SendPingBack()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.PingBack);
        SendEncrypted(msg, _xteaKey);
    }

    // ─── Movement ─────────────────────────────────────────────────────────────

    private static readonly Dictionary<Direction, byte> WalkOpcode = new()
    {
        { Direction.North,     (byte)GameClientPacket.MoveNorth     },
        { Direction.East,      (byte)GameClientPacket.MoveEast      },
        { Direction.South,     (byte)GameClientPacket.MoveSouth     },
        { Direction.West,      (byte)GameClientPacket.MoveWest      },
        { Direction.NorthEast, (byte)GameClientPacket.MoveNorthEast },
        { Direction.SouthEast, (byte)GameClientPacket.MoveSouthEast },
        { Direction.SouthWest, (byte)GameClientPacket.MoveSouthWest },
        { Direction.NorthWest, (byte)GameClientPacket.MoveNorthWest },
    };

    private static readonly Dictionary<Direction, byte> TurnOpcode = new()
    {
        { Direction.North, (byte)GameClientPacket.TurnNorth },
        { Direction.East,  (byte)GameClientPacket.TurnEast  },
        { Direction.South, (byte)GameClientPacket.TurnSouth },
        { Direction.West,  (byte)GameClientPacket.TurnWest  },
    };

    /// <summary>Sends a single-step walk packet in <paramref name="dir"/>.</summary>
    public void SendWalk(Direction dir)
    {
        if (!WalkOpcode.TryGetValue(dir, out byte op))
            throw new ArgumentException($"Invalid walk direction: {dir}");
        var msg = new OutputMessage();
        msg.WriteU8(op);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>Sends a turn packet in <paramref name="dir"/>.</summary>
    public void SendTurn(Direction dir)
    {
        if (!TurnOpcode.TryGetValue(dir, out byte op))
            throw new ArgumentException($"Direction {dir} is not a cardinal turn direction.");
        var msg = new OutputMessage();
        msg.WriteU8(op);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>Sends a <c>Stop</c> packet to halt the character's movement.</summary>
    public void SendStop()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.Stop);
        SendEncrypted(msg, _xteaKey);
    }

    // Direction wire bytes used in the AutoWalk packet (Tibia protocol encoding).
    // These differ from the Direction enum values.
    private static readonly Dictionary<Direction, byte> AutoWalkByte = new()
    {
        { Direction.East,      1 },
        { Direction.NorthEast, 2 },
        { Direction.North,     3 },
        { Direction.NorthWest, 4 },
        { Direction.West,      5 },
        { Direction.SouthWest, 6 },
        { Direction.South,     7 },
        { Direction.SouthEast, 8 },
    };

    /// <summary>
    /// Sends an <c>AutoWalk</c> packet describing a multi-step path.
    /// Each step is encoded with its own direction byte per the Tibia wire format.
    /// </summary>
    public void SendAutoWalk(IReadOnlyList<Direction> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Count == 0) return;

        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.AutoWalk);
        msg.WriteU8((byte)Math.Min(path.Count, 255));
        for (int i = 0; i < Math.Min(path.Count, 255); i++)
        {
            byte wireByte = AutoWalkByte.TryGetValue(path[i], out byte b) ? b : (byte)0;
            msg.WriteU8(wireByte);
        }
        SendEncrypted(msg, _xteaKey);
    }

    // ─── Chat ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a chat message in the given <paramref name="mode"/>.
    /// For <see cref="ChatMode.Private"/>, <paramref name="receiver"/> must be set.
    /// </summary>
    public void SendSay(string message, ChatMode mode = ChatMode.Say, string? receiver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.Say);
        msg.WriteU8((byte)mode);
        if (mode == ChatMode.Private)
        {
            if (string.IsNullOrWhiteSpace(receiver))
                throw new ArgumentException("Receiver must be specified for private messages.");
            msg.WriteString(receiver);
        }
        msg.WriteString(message);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Sends a request for the list of available public channels.
    /// Maps to <c>ProtocolGame::sendRequestChannels</c>.
    /// Task T10.
    /// </summary>
    public void SendRequestChannels()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.RequestChannels);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Sends a join-channel request for the channel identified by
    /// <paramref name="channelId"/>.
    /// Maps to <c>ProtocolGame::sendJoinChannel</c>.
    /// Task T10.
    /// </summary>
    public void SendJoinChannel(ushort channelId)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.JoinChannel);
        msg.WriteU16(channelId);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Sends a leave-channel request for the channel identified by
    /// <paramref name="channelId"/>.
    /// Maps to <c>ProtocolGame::sendLeaveChannel</c>.
    /// Task T10.
    /// </summary>
    public void SendLeaveChannel(ushort channelId)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.LeaveChannel);
        msg.WriteU16(channelId);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Sends a request to open a private chat channel with
    /// <paramref name="receiver"/>.
    /// Maps to <c>ProtocolGame::sendOpenPrivateChannel</c>.
    /// Task T10.
    /// </summary>
    public void SendOpenPrivateChannel(string receiver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiver);
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.OpenPrivateChannel);
        msg.WriteString(receiver);
        SendEncrypted(msg, _xteaKey);
    }

    // ─── Combat (T12) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends updated fight, chase and safe-mode settings to the server.
    /// Protocol 1281: PvpMode byte is always included (GamePVPMode feature present).
    /// Maps to <c>ProtocolGame::sendChangeFightModes</c>.
    /// Task T12.
    /// </summary>
    public void SendChangeFightModes(Game.FightMode fightMode, Game.ChaseMode chaseMode,
        bool safeFight, Game.PvpMode pvpMode = Game.PvpMode.WhiteDove)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.ChangeFightModes);
        msg.WriteU8((byte)fightMode);
        msg.WriteU8((byte)chaseMode);
        msg.WriteU8(safeFight ? (byte)1 : (byte)0);
        msg.WriteU8((byte)pvpMode);  // GamePVPMode — always present at protocol 1281
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Sends an attack request targeting <paramref name="creatureId"/>.
    /// Pass 0 to cancel the current attack.
    /// The sequence counter is included (GameAttackSeq feature always present at 1281).
    /// Maps to <c>ProtocolGame::sendAttack</c>.
    /// Task T12.
    /// </summary>
    public void SendAttack(uint creatureId, uint seq = 0)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.Attack);
        msg.WriteU32(creatureId);
        msg.WriteU32(seq);  // GameAttackSeq — always present at protocol 1281
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Sends a follow request targeting <paramref name="creatureId"/>.
    /// Pass 0 to cancel following.
    /// Maps to <c>ProtocolGame::sendFollow</c>.
    /// Task T12.
    /// </summary>
    public void SendFollow(uint creatureId, uint seq = 0)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.Follow);
        msg.WriteU32(creatureId);
        msg.WriteU32(seq);  // GameAttackSeq — always present at protocol 1281
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Sends a request to cancel both the current attack and follow targets.
    /// Maps to <c>ProtocolGame::sendCancelAttackAndFollow</c>.
    /// Task T12.
    /// </summary>
    public void SendCancelAttackAndFollow()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.CancelAttackAndFollow);
        SendEncrypted(msg, _xteaKey);
    }

    // ─── NPC trade (T15) ──────────────────────────────────────────────────────

    /// <summary>
    /// Asks the server for detailed information on an NPC trade item.
    /// Maps to <c>ProtocolGame::sendInspectNpcTrade</c>.
    /// Task T15.
    /// </summary>
    public void SendInspectNpcTrade(int itemId, int count)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.InspectNpcTrade);
        msg.WriteU16((ushort)itemId);
        msg.WriteU16((ushort)count);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Requests the server to buy an item from the active NPC.
    /// Maps to <c>ProtocolGame::sendBuyItem</c>.
    /// Task T15.
    /// </summary>
    public void SendBuyItem(int itemId, int subType, int amount, bool ignoreCapacity, bool buyWithBackpack)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.BuyItem);
        msg.WriteU16((ushort)itemId);
        msg.WriteU8((byte)subType);
        msg.WriteU16((ushort)amount);
        msg.WriteU8(ignoreCapacity  ? (byte)1 : (byte)0);
        msg.WriteU8(buyWithBackpack ? (byte)1 : (byte)0);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Requests the server to sell an item to the active NPC.
    /// Maps to <c>ProtocolGame::sendSellItem</c>.
    /// Task T15.
    /// </summary>
    public void SendSellItem(int itemId, int subType, int amount, bool ignoreEquipped)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.SellItem);
        msg.WriteU16((ushort)itemId);
        msg.WriteU8((byte)subType);
        msg.WriteU16((ushort)amount);
        msg.WriteU8(ignoreEquipped ? (byte)1 : (byte)0);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Notifies the server that the player closed the NPC trade window.
    /// Maps to <c>ProtocolGame::sendCloseNpcTrade</c>.
    /// Task T15.
    /// </summary>
    public void SendCloseNpcTrade()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.CloseNpcTrade);
        SendEncrypted(msg, _xteaKey);
    }

    // ─── Player-to-player trade (T16) ─────────────────────────────────────────

    /// <summary>
    /// Requests the server to initiate a player trade with the specified item.
    /// Maps to <c>ProtocolGame::sendRequestTrade</c>.
    /// Task T16.
    /// </summary>
    public void SendRequestTrade(Game.Position position, int itemId, int stackPos, uint creatureId)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.RequestTrade);
        msg.WriteU16((ushort)position.X);
        msg.WriteU16((ushort)position.Y);
        msg.WriteU8((byte)position.Z);
        msg.WriteU16((ushort)itemId);
        msg.WriteU8((byte)stackPos);
        msg.WriteU32(creatureId);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Asks the server to inspect a trade slot (own or partner's).
    /// Maps to <c>ProtocolGame::sendInspectTrade</c>.
    /// Task T16.
    /// </summary>
    public void SendInspectTrade(bool counterOffer, int index)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.InspectTrade);
        msg.WriteU8(counterOffer ? (byte)1 : (byte)0);
        msg.WriteU8((byte)index);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Accepts the current player-to-player trade.
    /// Maps to <c>ProtocolGame::sendAcceptTrade</c>.
    /// Task T16.
    /// </summary>
    public void SendAcceptTrade()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.AcceptTrade);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Rejects the current player-to-player trade.
    /// Maps to <c>ProtocolGame::sendRejectTrade</c>.
    /// Task T16.
    /// </summary>
    public void SendRejectTrade()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.RejectTrade);
        SendEncrypted(msg, _xteaKey);
    }

    // ─── VIP management (T21) ─────────────────────────────────────────────────

    /// <summary>
    /// Sends a request to add a player to the VIP (friends) list.
    /// Maps to <c>ProtocolGame::sendAddVip</c>.
    /// Task T21.
    /// </summary>
    public void SendAddVip(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.AddVip);
        msg.WriteString(name);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Sends a request to remove a player from the VIP (friends) list.
    /// Maps to <c>ProtocolGame::sendRemoveVip</c>.
    /// Task T21.
    /// </summary>
    public void SendRemoveVip(uint id)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.RemoveVip);
        msg.WriteU32(id);
        SendEncrypted(msg, _xteaKey);
    }

    // ─── Quest log (T23) ─────────────────────────────────────────────────────

    /// <summary>
    /// Requests the quest log from the server.
    /// Maps to <c>ProtocolGame::sendRequestQuestLog</c>.
    /// Task T23.
    /// </summary>
    public void SendRequestQuestLog()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.RequestQuestLog);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Requests the mission details for a specific quest.
    /// Maps to <c>ProtocolGame::sendRequestQuestLine</c>.
    /// Task T23.
    /// </summary>
    public void SendRequestQuestLine(ushort questId)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.RequestQuestLine);
        msg.WriteU16(questId);
        SendEncrypted(msg, _xteaKey);
    }

    // ─── Modal dialog (T23) ───────────────────────────────────────────────────

    /// <summary>
    /// Sends the player's answer to a modal dialog.
    /// Maps to <c>ProtocolGame::sendAnswerModalDialog</c>.
    /// Task T23.
    /// </summary>
    public void SendAnswerModalDialog(uint dialogId, byte buttonId, byte choiceId)
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.AnswerModalDialog);
        msg.WriteU32(dialogId);
        msg.WriteU8(buttonId);
        msg.WriteU8(choiceId);
        SendEncrypted(msg, _xteaKey);
    }

    // ─── Edit text / edit list (T23) ──────────────────────────────────────────

    /// <summary>
    /// Submits the player's text for an editable window.
    /// Maps to <c>ProtocolGame::sendEditText</c>.
    /// Task T23.
    /// </summary>
    public void SendEditText(uint id, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.EditText);
        msg.WriteU32(id);
        msg.WriteString(text);
        SendEncrypted(msg, _xteaKey);
    }

    /// <summary>
    /// Submits the player's text for an editable list window.
    /// Maps to <c>ProtocolGame::sendEditList</c>.
    /// Task T23.
    /// </summary>
    public void SendEditList(uint id, byte doorId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.EditList);
        msg.WriteU8(doorId);
        msg.WriteU32(id);
        msg.WriteString(text);
        SendEncrypted(msg, _xteaKey);
    }
}
