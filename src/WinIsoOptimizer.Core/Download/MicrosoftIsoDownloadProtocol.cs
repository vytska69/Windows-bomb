using System.Text.RegularExpressions;

namespace WinIsoOptimizer.Core.Download;

/// <summary>
/// Pure, network-free building blocks for the multi-step handshake Microsoft's public
/// software-download pages use before they'll hand out a real ISO download link — the same flow
/// pbatard/Fido (https://github.com/pbatard/Fido) automates. Kept separate from the actual HTTP
/// orchestration in <see cref="MicrosoftIsoDownloadService"/> so the URL/parameter construction and
/// response parsing can be unit tested without a live network call.
/// </summary>
public static class MicrosoftIsoDownloadProtocol
{
    public const string OrgId = "y6jn8c31";
    public const string ProfileId = "606624d44113";
    public const string OvDfInstanceId = "560dc9f3-1aa5-4a2f-b63c-9e18f8d0e175";

    /// <summary>Referer Microsoft's software-download-connector API expects — this is a fixed value in
    /// the upstream flow too (the API doesn't actually vary its behavior by which product page you
    /// name here, only by the productEditionId/SKU query parameters).</summary>
    public const string RefererUrl = "https://www.microsoft.com/software-download/windows11";

    private static readonly Regex WParameterPattern = new(@"[?&]w=([A-F0-9]+)", RegexOptions.Compiled);
    private static readonly Regex RTicksParameterPattern = new(@"rticks=""?\+?(\d+)", RegexOptions.Compiled);

    public static string BuildLocaleCheckUrl(string locale) =>
        $"https://www.microsoft.com/{locale}/software-download/windows11";

    public static string BuildVlscppeTagsUrl(Guid sessionId) =>
        $"https://vlscppe.microsoft.com/tags?org_id={OrgId}&session_id={sessionId}";

    public static string BuildOvDfChallengeUrl(Guid sessionId) =>
        $"https://ov-df.microsoft.com/mdt.js?instanceId={OvDfInstanceId}&PageId=si&session_id={sessionId}";

    public static string BuildOvDfReplyUrl(Guid sessionId, string w, string rticks, long unixMilliseconds) =>
        $"https://ov-df.microsoft.com/?session_id={sessionId}&CustomerId={OvDfInstanceId}&PageId=si&w={w}&mdt={unixMilliseconds}&rticks={rticks}";

    public static string BuildSkuInformationUrl(int editionId, string locale, Guid sessionId) =>
        "https://www.microsoft.com/software-download-connector/api/getskuinformationbyproductedition" +
        $"?profile={ProfileId}&productEditionId={editionId}&SKU=undefined&friendlyFileName=undefined&Locale={locale}&sessionID={sessionId}";

    public static string BuildDownloadLinksUrl(string skuId, string locale, Guid sessionId) =>
        "https://www.microsoft.com/software-download-connector/api/GetProductDownloadLinksBySku" +
        $"?profile={ProfileId}&productEditionId=undefined&SKU={skuId}&friendlyFileName=undefined&Locale={locale}&sessionID={sessionId}";

    /// <summary>Extracts the "w" challenge parameter from the ov-df.microsoft.com/mdt.js response body.
    /// Throws with a clear message if the response shape has changed (this whole flow is an
    /// unofficial, reverse-engineered handshake that can break whenever Microsoft changes it).</summary>
    public static string ExtractWParameter(string mdtJsResponseBody)
    {
        var match = WParameterPattern.Match(mdtJsResponseBody);
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not extract the 'w' challenge parameter from Microsoft's ov-df.microsoft.com response — this download flow may have changed upstream.");
        }
        return match.Groups[1].Value;
    }

    /// <summary>Extracts the "rticks" challenge parameter from the same response.</summary>
    public static string ExtractRTicksParameter(string mdtJsResponseBody)
    {
        var match = RTicksParameterPattern.Match(mdtJsResponseBody);
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not extract the 'rticks' challenge parameter from Microsoft's ov-df.microsoft.com response — this download flow may have changed upstream.");
        }
        return match.Groups[1].Value;
    }

    /// <summary>Maps the numeric DownloadType Microsoft's API returns to an architecture name.</summary>
    public static string MapDownloadTypeToArchitecture(int downloadType) => downloadType switch
    {
        0 => "x86",
        1 => "x64",
        2 => "ARM64",
        _ => "Unknown",
    };
}
