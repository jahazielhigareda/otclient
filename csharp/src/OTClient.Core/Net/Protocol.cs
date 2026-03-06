namespace OTClient.Framework.Net;

/// <summary>
/// Abstract base class for all game protocol implementations.
/// <para>
/// Owns a <see cref="Connection"/>, manages a packet-opcode dispatch table,
/// and exposes virtual lifecycle hooks (<see cref="OnConnected"/>,
/// <see cref="OnDisconnected"/>, <see cref="OnError"/>,
/// <see cref="OnUnknownPacket"/>).
/// </para>
/// Concrete protocols (e.g. <see cref="ProtocolGame"/>) register handlers
/// in their constructors via <see cref="RegisterHandler"/> and provide
/// encryption-aware overrides of <see cref="ParseMessage"/>.
/// <para>
/// Maps to <c>src/framework/net/protocol.{h,cpp}</c>.
/// Task 5.5.
/// </para>
/// </summary>
public abstract class Protocol : IDisposable
{
    /// <summary>The underlying TCP connection (may be <c>null</c> before <see cref="ConnectAsync"/>).</summary>
    protected Connection? Connection { get; private set; }

    private readonly Dictionary<byte, Action<InputMessage>> _handlers = [];
    private bool _disposed;

    /// <summary>
    /// Optional main-thread dispatcher.  When set, raw data received on the
    /// network thread is <em>not</em> parsed immediately; instead an
    /// <see cref="OTClient.Framework.Core.EventDispatcher.AddEvent"/> call is
    /// queued so that <see cref="ParseMessage"/> runs during the next
    /// <see cref="OTClient.Framework.Core.EventDispatcher.Poll"/> on the main
    /// thread.  This is the C# equivalent of the C++ pattern:
    /// <c>g_dispatcher.addEvent([=] { parseMessage(msg); });</c>
    /// Task T35.
    /// </summary>
    private OTClient.Framework.Core.EventDispatcher? _dispatcher;

    // ─── Connection management ────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="Connection"/>, wires its events to this protocol,
    /// and performs the async TCP (and optionally TLS) connect.
    /// </summary>
    public async Task ConnectAsync(
        string            host,
        int               port,
        bool              useTls            = false,
        CancellationToken cancellationToken = default)
    {
        Connection?.Dispose();
        Connection = new Connection();
        Connection.DataReceived += HandleRawData;
        Connection.Connected    += () => OnConnected();
        Connection.Disconnected += () => OnDisconnected();
        Connection.Error        += ex => OnError(ex);

        await Connection.ConnectAsync(host, port, useTls, cancellationToken)
                        .ConfigureAwait(false);
    }

    /// <summary>Closes the connection gracefully.</summary>
    public void Disconnect() => Connection?.Disconnect();

    /// <summary><c>true</c> when the underlying TCP connection is open.</summary>
    public bool IsConnected => Connection?.IsConnected ?? false;

    // ─── Main-thread dispatcher (T35) ─────────────────────────────────────────

    /// <summary>
    /// Sets the <see cref="OTClient.Framework.Core.EventDispatcher"/> that will
    /// be used to marshal <see cref="ParseMessage"/> calls back to the main thread.
    /// <para>
    /// When non-<c>null</c>, incoming packets from the network thread are
    /// captured (as an already-decoded byte array) and enqueued on the dispatcher;
    /// the actual parse runs on whichever thread calls
    /// <see cref="OTClient.Framework.Core.EventDispatcher.Poll"/>.
    /// </para>
    /// <para>
    /// When <c>null</c> (the default), parsing occurs synchronously on the
    /// thread that raised <see cref="Connection.DataReceived"/> — suitable for
    /// unit tests that do not have a game-loop.
    /// </para>
    /// Maps to the C++ pattern where <c>ProtocolGame::onRecv</c> posts via
    /// <c>g_dispatcher.addEvent</c>.
    /// Task T35.
    /// </summary>
    public void SetDispatcher(OTClient.Framework.Core.EventDispatcher? dispatcher)
        => _dispatcher = dispatcher;

    // ─── Packet dispatch ──────────────────────────────────────────────────────

    /// <summary>
    /// Registers an opcode handler.  Only one handler per opcode is kept;
    /// subsequent registrations for the same opcode overwrite the previous one.
    /// </summary>
    protected void RegisterHandler(byte opcode, Action<InputMessage> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[opcode] = handler;
    }

    /// <summary>
    /// Called by the <see cref="Connection.DataReceived"/> event with raw payload
    /// bytes (the 2-byte length header has already been stripped by
    /// <see cref="Connection"/>).  Constructs an <see cref="InputMessage"/> and
    /// either dispatches immediately (no dispatcher set) or posts the parse
    /// action to the main-thread <see cref="OTClient.Framework.Core.EventDispatcher"/>.
    /// Task T35: parse is marshalled back to the main thread when a dispatcher is wired.
    /// </summary>
    protected virtual void HandleRawData(byte[] data)
    {
        if (_dispatcher is not null)
        {
            // Marshal parse to the main thread.  The Connection already allocates
            // a fresh array per packet (see Connection.cs DataReceived), so the
            // closure can safely capture the parameter directly.
            _dispatcher.AddEvent(() =>
            {
                var msg = new InputMessage(data);
                try   { ParseMessage(msg); }
                catch (Exception ex) { OnError(ex); }
            });
        }
        else
        {
            // No dispatcher — parse synchronously (unit-test / single-thread mode).
            var msg = new InputMessage(data);
            try   { ParseMessage(msg); }
            catch (Exception ex) { OnError(ex); }
        }
    }

    /// <summary>
    /// Parses one or more logical packets from <paramref name="msg"/>.
    /// The default implementation reads opcodes from the raw buffer and
    /// dispatches to registered handlers.  Override in subclasses to perform
    /// XTEA decryption before dispatching.
    /// </summary>
    protected virtual void ParseMessage(InputMessage msg)
    {
        while (!msg.IsEof)
        {
            byte opcode = msg.ReadU8();
            if (_handlers.TryGetValue(opcode, out var handler))
                handler(msg);
            else
                OnUnknownPacket(opcode, msg);
        }
    }

    // ─── Send ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends an <see cref="OutputMessage"/> to the server as an unencrypted wire frame.
    /// </summary>
    protected void Send(OutputMessage msg)
    {
        if (Connection is null || !Connection.IsConnected)
            throw new InvalidOperationException("Protocol is not connected.");
        Connection.Send(msg.ToWireFrame());
    }

    /// <summary>
    /// Sends an <see cref="OutputMessage"/> to the server XTEA-encrypted.
    /// </summary>
    protected void SendEncrypted(OutputMessage msg, uint[] key)
    {
        if (Connection is null || !Connection.IsConnected)
            throw new InvalidOperationException("Protocol is not connected.");
        Connection.Send(msg.ToXteaWireFrame(key));
    }

    // ─── Virtual hooks ────────────────────────────────────────────────────────

    /// <summary>Invoked once the TCP connection is established.</summary>
    protected virtual void OnConnected() { }

    /// <summary>Invoked when the TCP connection closes (clean or error).</summary>
    protected virtual void OnDisconnected() { }

    /// <summary>Invoked when a network or packet-parse error occurs.</summary>
    protected virtual void OnError(Exception ex) { }

    /// <summary>
    /// Invoked when a packet opcode has no registered handler.
    /// The default implementation skips the remaining message bytes.
    /// </summary>
    protected virtual void OnUnknownPacket(byte opcode, InputMessage msg)
    {
        // Skip unrecognised packets to avoid desync
        msg.Skip(msg.Remaining);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Connection?.Dispose();
    }
}
