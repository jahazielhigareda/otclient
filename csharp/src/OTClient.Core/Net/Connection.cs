using System.Net.Security;
using System.Net.Sockets;

namespace OTClient.Framework.Net;

/// <summary>Connection lifecycle state.</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
}

/// <summary>
/// Asynchronous TCP connection with optional TLS support.
/// Uses <see cref="TcpClient"/> for transport and <see cref="SslStream"/>
/// for TLS (task 5.2), replacing the ASIO-based connection in
/// <c>src/framework/net/connection.*</c>.
/// <para>
/// Wire-framing: every message is preceded by a 2-byte little-endian
/// payload-length header, matching the Tibia protocol convention used by
/// <see cref="OutputMessage.ToWireFrame"/>.
/// </para>
/// Task 5.1.
/// </summary>
public sealed class Connection : IDisposable
{
    private TcpClient?              _client;
    private Stream?                 _stream;
    private CancellationTokenSource? _cts;
    private ConnectionState         _state = ConnectionState.Disconnected;
    private bool                    _disposed;

    private const int ReadBufferSize = 65536;

    // ─── Events ───────────────────────────────────────────────────────────────

    /// <summary>Raised on the task-pool thread when the TCP handshake completes.</summary>
    public event Action? Connected;

    /// <summary>Raised on the task-pool thread when the connection closes.</summary>
    public event Action? Disconnected;

    /// <summary>Raised on the task-pool thread when a network or IO error occurs.</summary>
    public event Action<Exception>? Error;

    /// <summary>
    /// Raised on the task-pool thread with raw payload bytes for each fully
    /// received packet (the 2-byte length header is already stripped).
    /// </summary>
    public event Action<byte[]>? DataReceived;

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Current connection lifecycle state.</summary>
    public ConnectionState State => _state;

    /// <summary><c>true</c> when the connection is in the <see cref="ConnectionState.Connected"/> state.</summary>
    public bool IsConnected => _state == ConnectionState.Connected;

    // ─── Connect ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to <paramref name="host"/>:<paramref name="port"/> asynchronously.
    /// When <paramref name="useTls"/> is <c>true</c> the stream is wrapped in
    /// <see cref="SslStream"/> and the TLS client handshake is performed.
    /// </summary>
    public async Task ConnectAsync(
        string            host,
        int               port,
        bool              useTls            = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_state != ConnectionState.Disconnected)
            throw new InvalidOperationException("Connection is already active.");

        _state = ConnectionState.Connecting;
        _cts   = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(host, port, _cts.Token).ConfigureAwait(false);

        Stream baseStream = _client.GetStream();
        if (useTls)
        {
            var ssl = new SslStream(baseStream, leaveInnerStreamOpen: false);
            await ssl.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions { TargetHost = host },
                _cts.Token).ConfigureAwait(false);
            _stream = ssl;
        }
        else
        {
            _stream = baseStream;
        }

        _state = ConnectionState.Connected;
        Connected?.Invoke();

        // Launch background read loop (fire-and-forget on the thread pool)
        _ = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
    }

    // ─── Send ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a pre-built wire frame (including the 2-byte length header) to the
    /// remote endpoint.  Fire-and-forget; errors surface via the
    /// <see cref="Error"/> event.
    /// </summary>
    public void Send(byte[] frame) => _ = SendAsync(frame);

    /// <summary>Sends a pre-built wire frame asynchronously.</summary>
    public async Task SendAsync(byte[] frame, CancellationToken cancellationToken = default)
    {
        if (_stream is null || !IsConnected)
            throw new InvalidOperationException("Not connected.");
        await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    // ─── Disconnect ───────────────────────────────────────────────────────────

    /// <summary>Closes the connection gracefully, signalling the read loop to exit.</summary>
    public void Disconnect()
    {
        if (_state == ConnectionState.Disconnected) return;
        _state = ConnectionState.Disconnecting;
        _cts?.Cancel();
        try { _stream?.Close(); } catch { /* ignore */ }
        try { _client?.Close(); } catch { /* ignore */ }
    }

    // ─── Read loop ────────────────────────────────────────────────────────────

    private async Task ReadLoopAsync(CancellationToken token)
    {
        byte[] header  = new byte[2];
        byte[] payload = new byte[ReadBufferSize];

        try
        {
            while (!token.IsCancellationRequested && IsConnected)
            {
                // 1. Read the 2-byte length header
                if (!await ReadExactAsync(header, 0, 2, token).ConfigureAwait(false))
                    break;

                int len = header[0] | (header[1] << 8);
                if (len <= 0 || len > ReadBufferSize)
                    break;

                // 2. Read the payload
                if (!await ReadExactAsync(payload, 0, len, token).ConfigureAwait(false))
                    break;

                // 3. Deliver a copy (caller may keep the reference)
                var data = new byte[len];
                Array.Copy(payload, data, len);
                DataReceived?.Invoke(data);
            }
        }
        catch (OperationCanceledException) { /* clean shutdown */ }
        catch (Exception ex)
        {
            if (_state == ConnectionState.Connected)
                Error?.Invoke(ex);
        }
        finally
        {
            if (_state != ConnectionState.Disconnected)
            {
                _state = ConnectionState.Disconnected;
                Disconnected?.Invoke();
            }
        }
    }

    private async Task<bool> ReadExactAsync(
        byte[] buf, int offset, int count, CancellationToken token)
    {
        int read = 0;
        while (read < count)
        {
            if (_stream is null) return false;
            int n = await _stream.ReadAsync(buf, offset + read, count - read, token)
                                 .ConfigureAwait(false);
            if (n == 0) return false; // remote closed
            read += n;
        }
        return true;
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
        _state = ConnectionState.Disconnected;
    }
}
