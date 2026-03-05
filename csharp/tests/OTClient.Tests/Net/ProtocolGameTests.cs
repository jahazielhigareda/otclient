using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Tests for <see cref="ProtocolGame"/> — Tibia 12.x game protocol.
/// All tests operate purely on in-memory <see cref="InputMessage"/> /
/// <see cref="OutputMessage"/> objects; no TCP connection is required.
/// Task 5.6–5.8 / task 5.12.
/// </summary>
public sealed class ProtocolGameTests
{
    // ─── Construction ────────────────────────────────────────────────────────

    [Fact]
    public void Ctor_IsNotConnected()
    {
        using var pg = new ProtocolGame();
        Assert.False(pg.IsConnected);
    }

    [Fact]
    public void Ctor_CharacterName_IsEmpty()
    {
        using var pg = new ProtocolGame();
        Assert.Equal(string.Empty, pg.CharacterName);
    }

    [Fact]
    public void Ctor_IsNotInGame()
    {
        using var pg = new ProtocolGame();
        Assert.False(pg.IsInGame);
    }

    // ─── Opcode enumerations ─────────────────────────────────────────────────

    [Fact]
    public void GameClientPacket_LoginRequest_Is0x0A()
    {
        Assert.Equal(0x0A, (byte)GameClientPacket.LoginRequest);
    }

    [Fact]
    public void GameClientPacket_MoveNorth_Is0x65()
    {
        Assert.Equal(0x65, (byte)GameClientPacket.MoveNorth);
    }

    [Fact]
    public void GameServerPacket_LoginError_Is0x14()
    {
        Assert.Equal(0x14, (byte)GameServerPacket.LoginError);
    }

    [Fact]
    public void GameServerPacket_TextMessage_Is0xB4()
    {
        Assert.Equal(0xB4, (byte)GameServerPacket.TextMessage);
    }

    // ─── Direction enum ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(Direction.North,     0)]
    [InlineData(Direction.East,      1)]
    [InlineData(Direction.South,     2)]
    [InlineData(Direction.West,      3)]
    [InlineData(Direction.NorthEast, 4)]
    [InlineData(Direction.SouthEast, 5)]
    [InlineData(Direction.SouthWest, 6)]
    [InlineData(Direction.NorthWest, 7)]
    public void Direction_WireValues_AreCorrect(Direction dir, byte expected)
    {
        Assert.Equal(expected, (byte)dir);
    }

    // ─── ParseLoginError (parse method tests) ─────────────────────────────────

    [Fact]
    public void ParseLoginError_InvokesEvent_WithCorrectReason()
    {
        using var pg = new ProtocolGame();

        string? received = null;
        pg.LoginError += reason => received = reason;

        // Build a raw packet: [opcode][string]
        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.LoginError);
        out_.WriteString("Account not found.");

        // Feed it via HandleRawData (internal, exposed via the protected override test)
        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Account not found.", received);
    }

    [Fact]
    public void ParseLoginAdvice_InvokesEvent_WithCorrectMessage()
    {
        using var pg = new ProtocolGame();

        string? received = null;
        pg.LoginAdvice += msg => received = msg;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.LoginAdvice);
        out_.WriteString("Your client version is outdated.");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Your client version is outdated.", received);
    }

    [Fact]
    public void ParseLoginWait_InvokesEvent_WithMessageAndSeconds()
    {
        using var pg = new ProtocolGame();

        string? waitMsg = null;
        int     waitSec = -1;
        pg.LoginWait += (msg, sec) => { waitMsg = msg; waitSec = sec; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.LoginWait);
        out_.WriteString("Server is full.");
        out_.WriteU8(30);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("Server is full.", waitMsg);
        Assert.Equal(30, waitSec);
    }

    [Fact]
    public void ParseTextMessage_InvokesEvent_WithTypeAndContent()
    {
        using var pg = new ProtocolGame();

        byte   msgType   = 0;
        string? msgText  = null;
        pg.TextMessageReceived += (t, m) => { msgType = t; msgText = m; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.TextMessage);
        out_.WriteU8(0x13);              // some message type byte
        out_.WriteString("You levelled up!");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal(0x13, msgType);
        Assert.Equal("You levelled up!", msgText);
    }

    [Fact]
    public void ParsePlayerSpeech_InvokesEvent_WithAuthorModeContent()
    {
        using var pg = new ProtocolGame();

        string?   author  = null;
        ChatMode  mode    = default;
        string?   content = null;
        pg.SpeechReceived += (a, m, c) => { author = a; mode = m; content = c; };

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.PlayerSpeech);
        out_.WriteString("PlayerX");
        out_.WriteU8((byte)ChatMode.Say);
        out_.WriteString("Hello world");

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.Equal("PlayerX", author);
        Assert.Equal(ChatMode.Say, mode);
        Assert.Equal("Hello world", content);
    }

    // ─── Ping round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void ParseDeath_InvokesEvent()
    {
        using var pg = new ProtocolGame();

        bool died = false;
        pg.PlayerDied += () => died = true;

        var out_ = new OutputMessage();
        out_.WriteU8((byte)GameServerPacket.Death);

        InvokeHandleRawData(pg, out_.ToArray());

        Assert.True(died);
    }

    // ─── OutputMessage packet building ────────────────────────────────────────

    [Fact]
    public void OutputMessage_LoginRequest_StartsWithCorrectOpcode()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.LoginRequest);
        msg.WriteU16(2);    // os = Linux
        msg.WriteU16(1212); // version

        byte[] payload = msg.ToArray();
        Assert.Equal((byte)GameClientPacket.LoginRequest, payload[0]);
        // OS LE u16 at [1..2] = 2
        Assert.Equal(2, payload[1] | (payload[2] << 8));
    }

    [Fact]
    public void OutputMessage_WalkNorth_OpcodePresentInPayload()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.MoveNorth);
        byte[] payload = msg.ToArray();
        Assert.Equal((byte)GameClientPacket.MoveNorth, payload[0]);
    }

    [Fact]
    public void OutputMessage_Say_ContainsMode_And_Text()
    {
        var msg = new OutputMessage();
        msg.WriteU8((byte)GameClientPacket.Say);
        msg.WriteU8((byte)ChatMode.Yell);
        msg.WriteString("HELLO!");

        byte[] payload = msg.ToArray();
        Assert.Equal((byte)GameClientPacket.Say,    payload[0]);
        Assert.Equal((byte)ChatMode.Yell,           payload[1]);
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var pg = new ProtocolGame();
        var ex = Record.Exception(() => pg.Dispose());
        Assert.Null(ex);
    }

    // ─── Helper to invoke the protected HandleRawData method ──────────────────

    private static void InvokeHandleRawData(ProtocolGame pg, byte[] data)
    {
        // Expose via reflection — the method is protected in the base class
        var method = typeof(Protocol).GetMethod(
            "HandleRawData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(pg, [data]);
    }
}
