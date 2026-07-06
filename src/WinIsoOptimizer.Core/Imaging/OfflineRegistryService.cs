using WinIsoOptimizer.Core.Processes;

namespace WinIsoOptimizer.Core.Imaging;

public enum RegistryValueKind
{
    Dword,
    String,
}

/// <summary>A single registry value to set, expressed the way reg.exe's /v /t /d flags want it.</summary>
public sealed record RegistryTweak(string KeyPath, string ValueName, RegistryValueKind Kind, string Data)
{
    internal string RegAddValueType => Kind switch
    {
        RegistryValueKind.Dword => "REG_DWORD",
        RegistryValueKind.String => "REG_SZ",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };
}

/// <summary>
/// dism.exe has no direct "set this registry value in the offline image" verb, so telemetry/policy
/// tweaks are applied by loading the mounted image's SOFTWARE/SYSTEM hives with reg.exe, editing them,
/// and unloading them again. This must run against a mounted (not yet unmounted) image directory.
/// </summary>
public sealed class OfflineRegistryService
{
    private const string SoftwareHiveLoadPoint = @"HKLM\WIOO_OFFLINE_SOFTWARE";
    private const string SystemHiveLoadPoint = @"HKLM\WIOO_OFFLINE_SYSTEM";

    private readonly IProcessRunner _runner;
    private readonly ExternalToolPaths _tools;

    public OfflineRegistryService(IProcessRunner runner, ExternalToolPaths? tools = null)
    {
        _runner = runner;
        _tools = tools ?? new ExternalToolPaths();
    }

    /// <summary>
    /// Loads the image's SOFTWARE hive, applies every tweak whose <see cref="RegistryTweak.KeyPath"/>
    /// starts with "SOFTWARE\", then unloads it — even if a tweak fails, so the hive is never left
    /// loaded (which would block dism from unmounting the image afterwards).
    /// </summary>
    public Task ApplySoftwareHiveTweaksAsync(string mountDirectory, IReadOnlyList<RegistryTweak> tweaks, IProgress<string>? progress = null, CancellationToken ct = default) =>
        ApplyHiveTweaksAsync(Path.Combine(mountDirectory, "Windows", "System32", "config", "SOFTWARE"), SoftwareHiveLoadPoint, tweaks, progress, ct);

    public Task ApplySystemHiveTweaksAsync(string mountDirectory, IReadOnlyList<RegistryTweak> tweaks, IProgress<string>? progress = null, CancellationToken ct = default) =>
        ApplyHiveTweaksAsync(Path.Combine(mountDirectory, "Windows", "System32", "config", "SYSTEM"), SystemHiveLoadPoint, tweaks, progress, ct);

    private async Task ApplyHiveTweaksAsync(string hiveFilePath, string loadPoint, IReadOnlyList<RegistryTweak> tweaks, IProgress<string>? progress, CancellationToken ct)
    {
        if (tweaks.Count == 0) return;

        await RunRegAsync(new[] { "load", loadPoint, hiveFilePath }, progress, ct).ConfigureAwait(false);
        try
        {
            foreach (var tweak in tweaks)
            {
                var rewrittenKeyPath = RewriteKeyPathToLoadPoint(tweak.KeyPath, loadPoint);
                await RunRegAsync(new[]
                {
                    "add", rewrittenKeyPath,
                    "/v", tweak.ValueName,
                    "/t", tweak.RegAddValueType,
                    "/d", tweak.Data,
                    "/f",
                }, progress, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            // Windows keeps a handle on a just-loaded hive briefly; a loaded hive left behind will
            // make dism's later /Unmount-Image fail with "the process cannot access the file", so
            // this unload must always run, success or failure above.
            await ForceGarbageCollectForRegistryHandlesAsync().ConfigureAwait(false);
            await RunRegAsync(new[] { "unload", loadPoint }, progress, ct).ConfigureAwait(false);
        }
    }

    private static Task ForceGarbageCollectForRegistryHandlesAsync()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return Task.CompletedTask;
    }

    private static string RewriteKeyPathToLoadPoint(string keyPath, string loadPoint)
    {
        // tweak.KeyPath is expressed relative to the hive root, e.g. "SOFTWARE\Policies\Microsoft\Windows\DataCollection".
        // The hive is loaded at HKLM\WIOO_OFFLINE_SOFTWARE, so "SOFTWARE\" (or "SYSTEM\") is replaced with the load point.
        var firstSeparator = keyPath.IndexOf('\\');
        return firstSeparator < 0 ? loadPoint : loadPoint + keyPath[firstSeparator..];
    }

    private async Task RunRegAsync(IReadOnlyList<string> args, IProgress<string>? progress, CancellationToken ct)
    {
        var request = new ProcessRequest(_tools.Reg, args, OutputLineProgress: progress);
        var result = await _runner.RunAsync(request, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ExternalToolException(request, result);
        }
    }
}
