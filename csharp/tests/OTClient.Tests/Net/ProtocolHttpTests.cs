using System.Net;
using System.Net.Http;
using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Tests for <see cref="ProtocolHttp"/>.
/// Uses a fake <see cref="HttpMessageHandler"/> so no real network is needed.
/// Task 5.9 / task 5.12.
/// </summary>
public sealed class ProtocolHttpTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static ProtocolHttp MakeClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var fake   = new FakeHandler(handler);
        var client = new HttpClient(fake);
        return new ProtocolHttp(client);
    }

    private static HttpResponseMessage OkJson(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage OkBytes(byte[] data) =>
        new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(data)
        };

    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultCtor_DoesNotThrow()
    {
        var ex = Record.Exception(() => { using var h = new ProtocolHttp(); });
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var h  = new ProtocolHttp();
        var ex = Record.Exception(() => h.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var h = new ProtocolHttp();
        h.Dispose();
        var ex = Record.Exception(() => h.Dispose());
        Assert.Null(ex);
    }

    // ─── BaseAddress / Timeout properties ─────────────────────────────────────

    [Fact]
    public void BaseAddress_CanBeSetAndRead()
    {
        using var h = new ProtocolHttp();
        var uri = new Uri("https://example.com/");
        h.BaseAddress = uri;
        Assert.Equal(uri, h.BaseAddress);
    }

    [Fact]
    public void Timeout_CanBeSetAndRead()
    {
        using var h = new ProtocolHttp();
        h.Timeout = TimeSpan.FromSeconds(42);
        Assert.Equal(TimeSpan.FromSeconds(42), h.Timeout);
    }

    // ─── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsResponseBody()
    {
        using var h = MakeClient(_ => OkJson("""{"ok":true}"""));
        string result = await h.GetAsync("http://fake/api");
        Assert.Equal("""{"ok":true}""", result);
    }

    [Fact]
    public async Task GetAsync_EmptyUrl_Throws()
    {
        using var h = new ProtocolHttp();
        await Assert.ThrowsAsync<ArgumentException>(
            () => h.GetAsync(string.Empty));
    }

    [Fact]
    public async Task GetAsync_Non2xx_Throws()
    {
        using var h = MakeClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        await Assert.ThrowsAsync<HttpRequestException>(
            () => h.GetAsync("http://fake/missing"));
    }

    // ─── GetBytesAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBytesAsync_ReturnsRawBytes()
    {
        byte[] expected = [0xDE, 0xAD, 0xBE, 0xEF];
        using var h = MakeClient(_ => OkBytes(expected));
        byte[] result = await h.GetBytesAsync("http://fake/bin");
        Assert.Equal(expected, result);
    }

    // ─── PostJsonAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task PostJsonAsync_SendsCorrectContentType()
    {
        string? contentType = null;
        using var h = MakeClient(req =>
        {
            contentType = req.Content?.Headers.ContentType?.MediaType;
            return OkJson("{}");
        });

        await h.PostJsonAsync("http://fake/api", """{"x":1}""");
        Assert.Equal("application/json", contentType);
    }

    [Fact]
    public async Task PostJsonAsync_ReturnsResponseBody()
    {
        using var h = MakeClient(_ => OkJson("""{"result":"ok"}"""));
        string result = await h.PostJsonAsync("http://fake/api", "{}");
        Assert.Equal("""{"result":"ok"}""", result);
    }

    // ─── PostFormAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task PostFormAsync_SendsFormEncodedData()
    {
        string? requestBody = null;
        // Use the AsyncFakeHandler overload for async body-reading
        var asyncHandler = new AsyncFakeHandler(async req =>
        {
            requestBody = await req.Content!.ReadAsStringAsync();
            return OkJson("{}");
        });
        using var h = new ProtocolHttp(new HttpClient(asyncHandler));

        var fields = new Dictionary<string, string> { ["key"] = "val" };
        await h.PostFormAsync("http://fake/form", fields);
        Assert.Contains("key=val", requestBody);
    }

    // ─── SendAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_ForwardsCustomRequest()
    {
        string? method = null;
        using var h = MakeClient(req =>
        {
            method = req.Method.Method;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var request  = new HttpRequestMessage(HttpMethod.Delete, "http://fake/res");
        var response = await h.SendAsync(request);
        Assert.Equal("DELETE", method);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ─── Fake handler ─────────────────────────────────────────────────────────

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _fn;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken _)
            => Task.FromResult(_fn(request));
    }

    // Async-capable fake handler variant
    private sealed class AsyncFakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _fn;
        public AsyncFakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken _)
            => _fn(request);
    }
}
