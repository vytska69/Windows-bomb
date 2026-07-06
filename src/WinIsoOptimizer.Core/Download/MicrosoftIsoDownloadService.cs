using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WinIsoOptimizer.Core.Download;

/// <summary>
/// Fetches official Windows 10/11 ISO download links directly from Microsoft's own servers, by
/// automating the same public multi-step handshake the microsoft.com/software-download page's
/// JavaScript performs in a browser. This is the same approach pbatard/Fido and Rufus use — no
/// Microsoft account, no paid licensing portal, no third-party mirror; every byte comes from
/// Microsoft's CDN under a short-lived signed URL Microsoft itself issues.
///
/// This only reaches the consumer flow (Home/Pro/Education). Enterprise ISOs are never offered here —
/// Microsoft gates those behind Volume Licensing/MVS, which this deliberately does not attempt to
/// reach; see <see cref="WindowsIsoCatalog"/>.
/// </summary>
public sealed class MicrosoftIsoDownloadService
{
    // Case-insensitive: PowerShell's dot-access (used by the reference implementation this is based
    // on) is case-insensitive too, so it's never actually been verified whether the live API returns
    // PascalCase or camelCase JSON — don't assume a specific casing where it hasn't been confirmed.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;

    public MicrosoftIsoDownloadService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
        var client = new HttpClient(handler);
        // Some of these endpoints appear to be pickier about requests that don't look like they came
        // from a real browser; a normal desktop UA costs nothing and matches what Fido/Rufus send.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        return client;
    }

    /// <summary>
    /// Runs one architecture-session's worth of the handshake (vlscppe whitelist, ov-df challenge/
    /// response, then the SKU-information call) and returns the raw SKUs found for that one edition ID.
    /// Exposed separately from <see cref="GetLanguagesAsync"/> so the per-session flow can be unit
    /// tested in isolation.
    /// </summary>
    internal async Task<IReadOnlyList<SkuEntry>> RunSessionAndGetSkusAsync(int editionId, string locale, Guid sessionId, IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report($"Requesting a download session for edition {editionId}...");
        await _httpClient.GetAsync(MicrosoftIsoDownloadProtocol.BuildVlscppeTagsUrl(sessionId), ct).ConfigureAwait(false);

        var mdtJsBody = await _httpClient.GetStringAsync(MicrosoftIsoDownloadProtocol.BuildOvDfChallengeUrl(sessionId), ct).ConfigureAwait(false);
        var w = MicrosoftIsoDownloadProtocol.ExtractWParameter(mdtJsBody);
        var rticks = MicrosoftIsoDownloadProtocol.ExtractRTicksParameter(mdtJsBody);
        var unixMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _httpClient.GetAsync(MicrosoftIsoDownloadProtocol.BuildOvDfReplyUrl(sessionId, w, rticks, unixMillis), ct).ConfigureAwait(false);

        progress?.Report("Fetching available languages...");
        var skuInfoUrl = MicrosoftIsoDownloadProtocol.BuildSkuInformationUrl(editionId, locale, sessionId);
        var skuInfo = await _httpClient.GetFromJsonAsync<SkuInformationResponse>(skuInfoUrl, JsonOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty response from Microsoft while listing languages.");

        if (skuInfo.Errors is { Length: > 0 })
        {
            throw new InvalidOperationException($"Microsoft's server refused the request: {skuInfo.Errors[0].Value}");
        }

        return skuInfo.Skus ?? Array.Empty<SkuEntry>();
    }

    /// <summary>
    /// Runs the handshake for every architecture-session in <paramref name="release"/> and returns the
    /// distinct languages available, each carrying a SKU reference per session it was found in.
    /// </summary>
    public async Task<IReadOnlyList<WindowsIsoLanguageOption>> GetLanguagesAsync(WindowsIsoRelease release, string locale = "en-US", IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var languages = new Dictionary<string, (string DisplayName, List<WindowsIsoSkuReference> Skus)>();

        foreach (var editionId in release.EditionIds)
        {
            var sessionId = Guid.NewGuid();
            var skus = await RunSessionAndGetSkusAsync(editionId, locale, sessionId, progress, ct).ConfigureAwait(false);
            foreach (var sku in skus)
            {
                if (!languages.TryGetValue(sku.Language, out var entry))
                {
                    entry = (sku.LocalizedLanguage, new List<WindowsIsoSkuReference>());
                    languages[sku.Language] = entry;
                }
                entry.Skus.Add(new WindowsIsoSkuReference(sessionId, sku.Id));
            }
        }

        return languages
            .Select(kvp => new WindowsIsoLanguageOption(kvp.Value.DisplayName, kvp.Key, kvp.Value.Skus))
            .OrderBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Gets the final CDN download link(s) — one per architecture the language was offered
    /// under — for a language previously returned by <see cref="GetLanguagesAsync"/>. These URLs are
    /// signed and short-lived (Microsoft's servers, not this tool, decide how long).</summary>
    public async Task<IReadOnlyList<WindowsIsoDownloadLink>> GetDownloadLinksAsync(WindowsIsoLanguageOption language, string locale = "en-US", IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var links = new List<WindowsIsoDownloadLink>();

        foreach (var sku in language.Skus)
        {
            progress?.Report($"Fetching download link for {language.DisplayName}...");
            var url = MicrosoftIsoDownloadProtocol.BuildDownloadLinksUrl(sku.SkuId, locale, sku.SessionId);
            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { Referrer = new Uri(MicrosoftIsoDownloadProtocol.RefererUrl) },
            };
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<DownloadLinksResponse>(JsonOptions, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Empty response from Microsoft while fetching download links.");

            if (result.Errors is { Length: > 0 })
            {
                throw new InvalidOperationException($"Microsoft's server refused the request: {result.Errors[0].Value}");
            }

            links.AddRange((result.ProductDownloadOptions ?? Array.Empty<ProductDownloadOptionEntry>())
                .Select(o => new WindowsIsoDownloadLink(MicrosoftIsoDownloadProtocol.MapDownloadTypeToArchitecture(o.DownloadType), o.Uri)));
        }

        return links;
    }
}
