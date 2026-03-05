namespace OTClient.Framework.Net;

/// <summary>
/// HTTP client wrapper built on <see cref="System.Net.Http.HttpClient"/>.
/// Provides simple GET, POST (JSON and form-encoded), and raw-request helpers.
/// Replaces <c>cpp-httplib</c> and the associated OpenSSL dependency.
/// <para>
/// TLS is handled transparently by <see cref="HttpClient"/>'s default handler
/// (<see cref="System.Net.Http.HttpClientHandler"/> which uses the OS TLS stack).
/// </para>
/// Maps to <c>ProtocolHttp</c> in <c>src/framework/net/</c>.
/// Task 5.9.
/// </summary>
public sealed class ProtocolHttp : IDisposable
{
    private readonly HttpClient _client;
    private bool _disposed;

    // ─── Construction ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="ProtocolHttp"/> using an optionally supplied
    /// <see cref="HttpClient"/> (useful for injecting a mocked handler in tests).
    /// When <paramref name="client"/> is <c>null</c> a new instance is created.
    /// </summary>
    public ProtocolHttp(HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
    }

    // ─── Configuration ────────────────────────────────────────────────────────

    /// <summary>Optional base address for relative-URI requests.</summary>
    public Uri? BaseAddress
    {
        get => _client.BaseAddress;
        set => _client.BaseAddress = value;
    }

    /// <summary>Request timeout (default 100 s).</summary>
    public TimeSpan Timeout
    {
        get => _client.Timeout;
        set => _client.Timeout = value;
    }

    // ─── HTTP operations ──────────────────────────────────────────────────────

    /// <summary>
    /// Sends a GET request and returns the response body as a UTF-8 string.
    /// Throws <see cref="HttpRequestException"/> for non-2xx responses.
    /// </summary>
    public async Task<string> GetAsync(
        string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a GET request and returns the response body as raw bytes.
    /// Throws <see cref="HttpRequestException"/> for non-2xx responses.
    /// </summary>
    public async Task<byte[]> GetBytesAsync(
        string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request with a JSON body and returns the response body.
    /// Content-Type is set to <c>application/json; charset=utf-8</c>.
    /// </summary>
    public async Task<string> PostJsonAsync(
        string url, string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var content  = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request with a URL-encoded form body and returns the response body.
    /// </summary>
    public async Task<string> PostFormAsync(
        string url,
        IEnumerable<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var content  = new FormUrlEncodedContent(fields);
        var response = await _client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a fully custom <see cref="HttpRequestMessage"/> and returns the response.
    /// </summary>
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken = default)
        => _client.SendAsync(request, cancellationToken);

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
    }
}
