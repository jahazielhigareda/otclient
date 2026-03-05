using System.Net;
using System.Net.Sockets;
using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Integration tests for <see cref="Connection"/>.
/// Each test spins up a real <see cref="TcpListener"/> on the loopback
/// interface so no mocking is required.
/// Task 5.1 / task 5.12.
/// </summary>
public sealed class ConnectionTests
{
    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void State_IsDisconnected_Initially()
    {
        using var conn = new Connection();
        Assert.Equal(ConnectionState.Disconnected, conn.State);
    }

    [Fact]
    public void IsConnected_IsFalse_Initially()
    {
        using var conn = new Connection();
        Assert.False(conn.IsConnected);
    }

    // ─── Dispose guard ────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var conn = new Connection();
        var ex   = Record.Exception(() => conn.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var conn = new Connection();
        conn.Dispose();
        var ex = Record.Exception(() => conn.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task ConnectAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var conn = new Connection();
        conn.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => conn.ConnectAsync("127.0.0.1", 1, useTls: false));
    }

    // ─── Connect → data → disconnect (loopback TCP) ───────────────────────────

    [Fact]
    public async Task ConnectAsync_Loopback_SetsConnectedState()
    {
        using var listener = StartListener(out int port);
        var conn = new Connection();

        bool connectedEventFired = false;
        conn.Connected += () => connectedEventFired = true;

        // Accept the server side in background
        var acceptTask = listener.AcceptTcpClientAsync();
        await conn.ConnectAsync("127.0.0.1", port);
        var serverClient = await acceptTask;
        serverClient.Dispose();

        Assert.True(conn.IsConnected);
        Assert.True(connectedEventFired);
        conn.Dispose();
    }

    [Fact]
    public async Task SendAsync_ThenReceiveOnServer_DeliversWireFrame()
    {
        using var listener = StartListener(out int port);
        using var conn     = new Connection();

        // Accept server side
        var acceptTask = listener.AcceptTcpClientAsync();
        await conn.ConnectAsync("127.0.0.1", port);
        using var server = await acceptTask;

        // Build and send a wire frame (2-byte length + payload)
        var msg   = new OutputMessage();
        msg.WriteU8(0xAB);
        msg.WriteU8(0xCD);
        byte[] wire = msg.ToWireFrame();

        await conn.SendAsync(wire);

        // Read from the server-side stream
        var ns     = server.GetStream();
        byte[] buf = new byte[wire.Length];
        int    n   = await ns.ReadAsync(buf, 0, buf.Length);

        Assert.Equal(wire.Length, n);
        Assert.Equal(wire, buf);
    }

    [Fact]
    public async Task DataReceived_Event_FiredWithPayload()
    {
        using var listener = StartListener(out int port);
        using var conn     = new Connection();

        byte[]? received = null;
        var tcs = new TaskCompletionSource<byte[]>();
        conn.DataReceived += data =>
        {
            received = data;
            tcs.TrySetResult(data);
        };

        var acceptTask = listener.AcceptTcpClientAsync();
        await conn.ConnectAsync("127.0.0.1", port);
        using var server = await acceptTask;

        // Write a framed message from the server to the client
        byte[] payload = [0xDE, 0xAD, 0xBE, 0xEF];
        byte[] frame   = [0x04, 0x00, .. payload]; // 2-byte LE length + payload
        var ns = server.GetStream();
        await ns.WriteAsync(frame);

        // Wait for the DataReceived event (max 3 s in CI)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        cts.Token.Register(() => tcs.TrySetCanceled());
        byte[] result = await tcs.Task;

        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task Disconnected_Event_FiredWhenServerClosesConnection()
    {
        using var listener = StartListener(out int port);
        using var conn     = new Connection();

        var tcs = new TaskCompletionSource<bool>();
        conn.Disconnected += () => tcs.TrySetResult(true);

        var acceptTask = listener.AcceptTcpClientAsync();
        await conn.ConnectAsync("127.0.0.1", port);
        using var server = await acceptTask;

        // Close the server side to trigger a disconnect on the client
        server.Close();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        cts.Token.Register(() => tcs.TrySetCanceled());
        bool fired = await tcs.Task;
        Assert.True(fired);
    }

    [Fact]
    public async Task Connect_AlreadyConnected_Throws()
    {
        using var listener = StartListener(out int port);
        using var conn     = new Connection();

        var acceptTask = listener.AcceptTcpClientAsync();
        await conn.ConnectAsync("127.0.0.1", port);
        using var server = await acceptTask;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => conn.ConnectAsync("127.0.0.1", port));

        conn.Dispose();
        server.Dispose();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }
}
