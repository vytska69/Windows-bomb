namespace WinIsoOptimizer.Core.Telemetry;

/// <summary>Toggles for the optional, non-appx optimizations offered alongside app removal.</summary>
public sealed class OptimizationOptions
{
    public bool ApplyPrivacyRegistryTweaks { get; init; } = true;
    public bool DisableTelemetryServices { get; init; } = true;
    public bool DisableTelemetryScheduledTasks { get; init; } = true;
    public bool RemoveOneDriveSetup { get; init; }
    public bool GenerateUnattendForLocalAccountAndSkipPrivacyScreens { get; init; }

    /// <summary>Runs `/Cleanup-Image /StartComponentCleanup` (safe, always reversible via Windows Update re-download).</summary>
    public bool ComponentStoreCleanup { get; init; } = true;

    /// <summary>Also passes `/ResetBase`: permanently discards superseded component versions to shrink
    /// the image further. Irreversible for this WIM (can't uninstall/rollback later Windows updates
    /// whose superseded predecessor was purged) — off by default, opt-in only.</summary>
    public bool ComponentStoreResetBase { get; init; }
}
