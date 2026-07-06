using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Telemetry;

namespace WinIsoOptimizer.Core.Jobs;

public sealed record IsoOptimizationRequest
{
    public required string SourceIsoPath { get; init; }
    public required string OutputIsoPath { get; init; }
    public required string WorkingDirectory { get; init; }
    public string VolumeLabel { get; init; } = "CUSTOM_WIN";

    /// <summary>Set when the caller (typically the GUI, after letting the user inspect editions/apps)
    /// already extracted <see cref="SourceIsoPath"/> into "WorkingDirectory/extracted" itself — skips
    /// re-extracting it here.</summary>
    public bool SourceAlreadyExtracted { get; init; }

    /// <summary>Which install.wim indices (editions) to service. Empty/null means "all indices found".</summary>
    public IReadOnlyList<int>? EditionIndices { get; init; }

    /// <summary>Provisioned apps to remove, applied identically to every serviced edition. Populate by
    /// cross-referencing <see cref="TelemetryDebloatProfile.RemovableAppCatalog"/> against what
    /// <see cref="DismService.GetProvisionedAppxPackagesAsync"/> actually found for that edition.</summary>
    public IReadOnlyList<ProvisionedAppxPackage> AppsToRemove { get; init; } = Array.Empty<ProvisionedAppxPackage>();

    public OptimizationOptions Optimizations { get; init; } = new();

    /// <summary>Folder of driver .inf files (e.g. from <see cref="Drivers.DriverService.ExportFromRunningSystemAsync"/>) to inject, or null to skip.</summary>
    public string? DriverFolderToInject { get; init; }

    /// <summary>Also inject the same drivers into boot.wim (setup environment) — needed when the
    /// target machine's storage/NIC controller isn't recognized by Setup itself, not just by the
    /// installed OS.</summary>
    public bool AlsoInjectDriversIntoBootWim { get; init; }

    public UnattendOptions? Unattend { get; init; }

    /// <summary>Apply the Windows 7/Vista-SP1+ x64 UEFI fallback-bootloader fix if the source media
    /// qualifies (see <see cref="LegacyBoot.LegacyUefiBootInjector"/>). Silently does nothing (rather
    /// than failing) when the media doesn't have a fixable case, since Win10/11 media never does.</summary>
    public bool ApplyLegacyUefiBootFixIfApplicable { get; init; } = true;

    public bool ForceUnsignedDrivers { get; init; }
}
