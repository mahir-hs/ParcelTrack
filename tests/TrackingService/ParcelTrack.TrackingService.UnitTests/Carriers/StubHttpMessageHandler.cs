using System.Net;
using System.Text;

namespace ParcelTrack.TrackingService.UnitTests.Carriers;

/// <summary>
/// Serves canned responses in order and records every request that arrived.
///
/// Carrier adapters are tested against this rather than the live Pathao sandbox: tests must
/// be deterministic and offline, and half of what matters here — 401 retry, 5xx handling,
/// malformed JSON — cannot be provoked on demand from a real server.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public int CallCount => Requests.Count;

    public StubHttpMessageHandler RespondWith(HttpStatusCode status, string body)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        return this;
    }

    public StubHttpMessageHandler RespondWithJson(string body) => RespondWith(HttpStatusCode.OK, body);

    public StubHttpMessageHandler Throws(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (_responses.Count == 0)
            throw new InvalidOperationException("StubHttpMessageHandler received an unexpected request.");

        return Task.FromResult(_responses.Dequeue()(request));
    }
}

/// <summary>Hands every caller the same stubbed client, whatever name they ask for.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler, string baseAddress = "https://courier-api-sandbox.pathao.com/")
    : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };
}
