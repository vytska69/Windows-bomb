using System.Net;

namespace WinIsoOptimizer.Core.Tests;

/// <summary>Routes requests to a canned response based on a predicate over the request URL, recording
/// every request seen — lets HttpClient-based code be tested without a real network call.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<string, bool> Matches, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public void When(Func<string, bool> urlMatches, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        _routes.Add((urlMatches, respond));

    public void WhenContains(string urlSubstring, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        When(url => url.Contains(urlSubstring, StringComparison.OrdinalIgnoreCase), respond);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var url = request.RequestUri!.ToString();
        var route = _routes.FirstOrDefault(r => r.Matches(url));
        var response = route.Respond?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }

    public static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    };

    public static HttpResponseMessage Text(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "text/plain"),
    };
}
