namespace WinIsoOptimizer.Core.Updates;

/// <summary>Result of comparing the running app's build number against the latest published GitHub release.</summary>
public sealed record UpdateCheckResult(bool IsUpdateAvailable, int? LatestBuildNumber, string? LatestVersionTag, string? ReleaseUrl);
