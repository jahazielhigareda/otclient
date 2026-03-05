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
}
