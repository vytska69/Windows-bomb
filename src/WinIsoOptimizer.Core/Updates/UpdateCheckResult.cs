namespace WinIsoOptimizer.Core.Updates;

/// <summary>Result of comparing the running app's build number against the latest published GitHub
/// release. <see cref="InstallerDownloadUrl"/>/<see cref="InstallerSha256"/> are null if that release
/// has no asset named <see cref="GitHubReleaseUpdateChecker.InstallerAssetName"/> (e.g. an older
/// release from before the installer existed) — callers should fall back to just linking
/// <see cref="ReleaseUrl"/> in that case rather than offering a one-click update.</summary>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    int? LatestBuildNumber,
    string? LatestVersionTag,
    string? ReleaseUrl,
    string? InstallerDownloadUrl,
    string? InstallerSha256);
