using WinIsoOptimizer.Core.Imaging;

namespace WinIsoOptimizer.Core.Telemetry;

/// <summary>Applies <see cref="TelemetryDebloatProfile"/> entries to a mounted Windows image, gated by <see cref="OptimizationOptions"/>.</summary>
public sealed class TelemetryDebloatService
{
    private readonly OfflineRegistryService _registry;
    private readonly DismService _dism;

    public TelemetryDebloatService(OfflineRegistryService registry, DismService dism)
    {
        _registry = registry;
        _dism = dism;
    }

    public async Task ApplyAsync(string mountDirectory, OptimizationOptions options, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (options.ApplyPrivacyRegistryTweaks)
        {
            progress?.Report("Applying privacy/telemetry registry policies...");
            await _registry.ApplySoftwareHiveTweaksAsync(mountDirectory, TelemetryDebloatProfile.PrivacyRegistryTweaksSoftwareHive, progress, ct).ConfigureAwait(false);
        }

        if (options.DisableTelemetryServices)
        {
            progress?.Report("Disabling DiagTrack / dmwappushservice...");
            await _registry.ApplySystemHiveTweaksAsync(mountDirectory, TelemetryDebloatProfile.TelemetryServiceDisableTweaksSystemHive, progress, ct).ConfigureAwait(false);
        }

        if (options.DisableTelemetryScheduledTasks)
        {
            RemoveTelemetryScheduledTasks(mountDirectory, progress);
        }

        if (options.RemoveOneDriveSetup)
        {
            RemoveOneDriveSetup(mountDirectory, progress);
        }

        if (options.ComponentStoreCleanup || options.ComponentStoreResetBase)
        {
            progress?.Report(options.ComponentStoreResetBase
                ? "Cleaning up component store (ResetBase — permanent)..."
                : "Cleaning up component store...");
            await _dism.CleanupImageAsync(mountDirectory, options.ComponentStoreResetBase, progress, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Removes the selected provisioned apps. Caller supplies the exact packages to remove
    /// (e.g. cross-referencing <see cref="TelemetryDebloatProfile.RemovableAppCatalog"/> against what
    /// <see cref="DismService.GetProvisionedAppxPackagesAsync"/> found in this specific image/edition).</summary>
    public async Task RemoveSelectedAppsAsync(string mountDirectory, IEnumerable<ProvisionedAppxPackage> packagesToRemove, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        foreach (var package in packagesToRemove)
        {
            progress?.Report($"Removing app: {package.DisplayName}...");
            await _dism.RemoveProvisionedAppxPackageAsync(mountDirectory, package.PackageName, progress, ct).ConfigureAwait(false);
        }
    }

    private static void RemoveTelemetryScheduledTasks(string mountDirectory, IProgress<string>? progress)
    {
        var tasksRoot = Path.Combine(mountDirectory, "Windows", "System32", "Tasks");
        foreach (var relativePath in TelemetryDebloatProfile.TelemetryScheduledTaskRelativePaths)
        {
            // Catalogue paths are written with literal '\' regardless of host OS (they mirror how
            // Windows itself lays out Windows\System32\Tasks\...), so split explicitly rather than
            // relying on Path.Combine/GetDirectoryName to recognize '\' as a separator on non-Windows.
            var fullPath = Path.Combine(new[] { tasksRoot }.Concat(relativePath.Split('\\')).ToArray());
            if (File.Exists(fullPath))
            {
                progress?.Report($"Removing scheduled task: {relativePath}");
                File.Delete(fullPath);
            }
        }
    }

    private static void RemoveOneDriveSetup(string mountDirectory, IProgress<string>? progress)
    {
        string[] candidates =
        {
            Path.Combine(mountDirectory, "Windows", "System32", "OneDriveSetup.exe"),
            Path.Combine(mountDirectory, "Windows", "SysWOW64", "OneDriveSetup.exe"),
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                progress?.Report($"Removing OneDrive setup: {path}");
                File.Delete(path);
            }
        }
    }
}
