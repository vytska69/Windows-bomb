using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WinIsoOptimizer.Core.Updates;

/// <summary>
/// Checks GitHub's public "latest release" API for a build newer than the one currently running.
/// The CI workflow (.github/workflows/build-and-release.yml) tags every release "build-&lt;run
/// number&gt;" and stamps that same number into the published exe's file version — comparing the two
/// is just an integer comparison, no semantic-version parsing needed.
/// </summary>
public sealed class GitHubReleaseUpdateChecker
{
    /// <summary>Matches the name CI's Inno Setup step uploads the installer as (see
    /// .github/workflows/build-and-release.yml) — used to find its download URL and hash within the
    /// release's asset list.</summary>
    public const string InstallerAssetName = "WinIsoOptimizer-Setup.exe";

    private static readonly Regex BuildTagPattern = new(@"^build-(\d+)$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;

    public GitHubReleaseUpdateChecker(string owner, string repo, HttpClient? httpClient = null)
    {
        _owner = owner;
        _repo = repo;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>Extracts the numeric build number from a "build-&lt;n&gt;" release tag, or null if the
    /// tag doesn't match that shape (e.g. an old/manual tag).</summary>
    internal static int? ParseBuildNumber(string? tagName)
    {
        if (string.IsNullOrEmpty(tagName)) return null;
        var match = BuildTagPattern.Match(tagName);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    /// <summary>Strips a leading "sha256:" prefix from a GitHub asset digest, if present, leaving just
    /// the hex hash so it can be compared directly against a locally-computed one.</summary>
    internal static string? NormalizeSha256Digest(string? digest)
    {
        if (digest is null) return null;
        const string prefix = "sha256:";
        return digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? digest[prefix.Length..] : digest;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(int currentBuildNumber, CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // Set per-request rather than as DefaultRequestHeaders on _httpClient: that client may be one
        // the caller injected and reuses elsewhere, and mutating its defaults would be a surprising
        // side effect. The GitHub REST API rejects requests with no User-Agent header (403).
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WinIsoOptimizer-UpdateChecker", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<GitHubReleaseResponse>(JsonOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty response from GitHub.");

        var latestBuildNumber = ParseBuildNumber(release.TagName);
        var isNewer = latestBuildNumber is { } n && n > currentBuildNumber;

        var installerAsset = release.Assets?.FirstOrDefault(a => string.Equals(a.Name, InstallerAssetName, StringComparison.OrdinalIgnoreCase));

        return new UpdateCheckResult(
            isNewer,
            latestBuildNumber,
            release.TagName,
            release.HtmlUrl,
            installerAsset?.BrowserDownloadUrl,
            NormalizeSha256Digest(installerAsset?.Digest));
    }

    private sealed record GitHubReleaseResponse(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("assets")] GitHubReleaseAsset[]? Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest);
}
