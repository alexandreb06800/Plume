using System.Net;
using System.Net.Http;

namespace Plume.Tests;

/// <summary>HttpMessageHandler factice : renvoie une réponse prédéfinie et capture la requête.</summary>
public class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly int _delayMs;
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public FakeHttpHandler(HttpStatusCode status, string body, int delayMs = 0)
    {
        _status = status;
        _body = body;
        _delayMs = delayMs;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        // Simule une latence réseau : honore l'annulation (délai par appel / Échap).
        if (_delayMs > 0)
            await Task.Delay(_delayMs, cancellationToken);
        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body)
        };
    }
}
