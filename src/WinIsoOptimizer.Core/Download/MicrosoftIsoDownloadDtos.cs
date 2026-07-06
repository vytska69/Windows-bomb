namespace WinIsoOptimizer.Core.Download;

/// <summary>A language/edition available for a given <see cref="WindowsIsoRelease"/>, with one SKU
/// reference per architecture-session it was found in (a language might be offered for x64 but not
/// ARM64, or vice versa).</summary>
public sealed record WindowsIsoLanguageOption(string DisplayName, string LanguageCode, IReadOnlyList<WindowsIsoSkuReference> Skus);

/// <summary>Ties a Microsoft SKU ID back to the session (and therefore edition/architecture) it was
/// retrieved under — needed because the final download-link call must reuse that exact session.</summary>
public sealed record WindowsIsoSkuReference(Guid SessionId, string SkuId);

public sealed record WindowsIsoDownloadLink(string Architecture, string Url);

internal sealed record SkuInformationResponse(SkuEntry[]? Skus, MicrosoftApiError[]? Errors);
internal sealed record SkuEntry(string Id, string Language, string LocalizedLanguage);
internal sealed record DownloadLinksResponse(ProductDownloadOptionEntry[]? ProductDownloadOptions, MicrosoftApiError[]? Errors);
internal sealed record ProductDownloadOptionEntry(int DownloadType, string Uri);
internal sealed record MicrosoftApiError(int Type, string Value);
