using System.Net;
using System.Net.Http;

namespace ParcelTrack.WebhookDispatchService.UnitTests;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that returns a configurable
/// <see cref="HttpResponseMessage"/> (or throws) and records how many requests
/// it received. Lets us exercise <see cref="WebhookDispatcher"/> deterministically
/// without any network I/O.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Exception? _exception;
    private Func<HttpRequestMessage, HttpResponseMessage>? _responder;

    public int SendCount { get; private set; }

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Return the same response for every request.</summary>
    public FakeHttpMessageHandler(HttpResponseMessage response)
    {
        _responder = _ => response;
    }

    /// <summary>Return the same status code for every request.</summary>
    public FakeHttpMessageHandler(HttpStatusCode code)
    {
        _responder = _ => new HttpResponseMessage(code);
    }

    /// <summary>Build a response per request using the supplied factory.</summary>
    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>Throw the supplied exception for every request (simulates a transport failure).</summary>
    public FakeHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    /// <summary>Swap the canned response returned for subsequent requests.</summary>
    public void ResetTo(HttpResponseMessage response) => _responder = _ => response;

    /// <summary>Swap the canned status code returned for subsequent requests.</summary>
    public void ResetTo(HttpStatusCode code) => _responder = _ => new HttpResponseMessage(code);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        SendCount++;
        Requests.Add(request);

        if (_exception is not null)
            return Task.FromException<HttpResponseMessage>(_exception);

        if (_responder is null)
            throw new InvalidOperationException("No response configured for FakeHttpMessageHandler.");

        return Task.FromResult(_responder(request));
    }

    // The dispatcher disposes the HttpClient (and thus its handler) after every
    // subscription, but we intentionally keep the fake alive so it can be reused
    // across the multiple clients the mocked IHttpClientFactory hands out.
    protected override void Dispose(bool disposing)
    {
    }
}

/// <summary>Handler that succeeds (2xx) for one subscriber URL and fails (5xx) for another,
/// so we can test per-subscription dead-lettering within a single dispatch.</summary>
internal sealed class RoutingFakeHandler : HttpMessageHandler
{
    private readonly string _okUrl;

    public int OkSendCount { get; private set; }
    public int BadSendCount { get; private set; }
    public List<string> SeenUris { get; } = [];

    public RoutingFakeHandler(string okUrl) => _okUrl = okUrl;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        SeenUris.Add(request.RequestUri?.ToString() ?? "<null>");

        if (request.RequestUri is not null &&
            request.RequestUri.ToString().Contains(_okUrl, StringComparison.OrdinalIgnoreCase))
        {
            OkSendCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        BadSendCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    protected override void Dispose(bool disposing)
    {
    }
}
