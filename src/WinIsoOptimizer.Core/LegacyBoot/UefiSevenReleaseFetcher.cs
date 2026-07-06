using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinIsoOptimizer.Core.LegacyBoot;

public sealed record UefiSevenReleaseAsset(string Name, string DownloadUrl);

public sealed record UefiSevenRelease(string TagName, string HtmlUrl, IReadOnlyList<UefiSevenReleaseAsset> Assets);

/// <summary>
/// Fetches the latest manatails/uefiseven release directly from GitHub's public API — nothing is
/// mirrored, bundled, or redistributed by this project itself. The repository publishes no LICENSE
/// file, so this project does not claim any redistribution rights over the compiled binary; fetching
/// it is instead a user-initiated action (see <see cref="UefiSevenDownloadService"/> and the GUI
/// confirmation gate) pulling straight from the upstream release, the same way a browser download
/// would. See docs/LEGACY-UEFI-BOOT.md.
/// </summary>
public sealed class UefiSevenReleaseFetcher
{
    private const string ReleasesUrl = "https://api.github.com/repos/manatails/uefiseven/releases/latest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public UefiSevenReleaseFetcher(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<UefiSevenRelease> FetchLatestReleaseAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUrl);
        // Same reasoning as GitHubReleaseUpdateChecker: set per-request, not on a possibly-shared
        // injected HttpClient's DefaultRequestHeaders. GitHub's REST API rejects requests with no
        // User-Agent header (403).
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WinIsoOptimizer-UefiSevenFetcher", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<GitHubReleaseResponse>(JsonOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty response from GitHub.");

        var assets = (release.Assets ?? Array.Empty<GitHubReleaseAsset>())
            .Select(a => new UefiSevenReleaseAsset(a.Name, a.BrowserDownloadUrl))
            .ToArray();

        return new UefiSevenRelease(release.TagName, release.HtmlUrl, assets);
    }

    /// <summary>
    /// Picks which release asset to download: a directly-published .efi file if one exists, otherwise
    /// a .zip archive to unpack (matching the project's own README, which distributes the compiled
    /// bootx64.efi inside an archive alongside the sample .ini). Returns null if neither shape is
    /// present, so the caller can report "couldn't find a usable asset" instead of guessing.
    /// </summary>
    public static UefiSevenReleaseAsset? SelectBootloaderAsset(IReadOnlyList<UefiSevenReleaseAsset> assets) =>
        assets.FirstOrDefault(a => a.Name.EndsWith(".efi", StringComparison.OrdinalIgnoreCase))
        ?? assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    private sealed record GitHubReleaseResponse(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("assets")] GitHubReleaseAsset[]? Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
