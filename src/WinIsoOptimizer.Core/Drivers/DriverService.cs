using WinIsoOptimizer.Core.Imaging;

namespace WinIsoOptimizer.Core.Drivers;

public sealed record ExportedDriverInfo(string InfPath, string FolderName);

/// <summary>
/// Pulls third-party drivers out of the currently-running Windows install and injects them into a
/// mounted image (install.wim for the installed OS, and/or boot.wim so Setup itself can see storage/
/// NIC hardware that needs a driver before install even starts — the classic "F6 driver" problem).
/// </summary>
public sealed class DriverService
{
    private readonly DismService _dism;

    public DriverService(DismService dism)
    {
        _dism = dism;
    }

    /// <summary>Exports every third-party driver currently installed on this running Windows machine to <paramref name="destinationFolder"/>.</summary>
    public async Task<IReadOnlyList<ExportedDriverInfo>> ExportFromRunningSystemAsync(string destinationFolder, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        await _dism.ExportOnlineDriversAsync(destinationFolder, progress, ct).ConfigureAwait(false);
        return EnumerateExportedDrivers(destinationFolder);
    }

    /// <summary>Lists the driver folders/.inf files found under a previously-exported driver folder, without re-exporting.</summary>
    public IReadOnlyList<ExportedDriverInfo> EnumerateExportedDrivers(string driverFolder)
    {
        if (!Directory.Exists(driverFolder)) return Array.Empty<ExportedDriverInfo>();

        return Directory.EnumerateFiles(driverFolder, "*.inf", SearchOption.AllDirectories)
            .Select(infPath => new ExportedDriverInfo(infPath, Path.GetFileName(Path.GetDirectoryName(infPath)) ?? string.Empty))
            .OrderBy(d => d.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Injects every driver under <paramref name="driverFolder"/> into the mounted install image.
    /// <paramref name="forceUnsigned"/> should stay false unless the target edition allows unsigned
    /// drivers (test-signing/Secure Boot considerations are the caller/user's to make, not silently overridden).</summary>
    public Task InjectIntoMountedImageAsync(string mountDirectory, string driverFolder, bool forceUnsigned = false, IProgress<string>? progress = null, CancellationToken ct = default) =>
        _dism.AddDriverAsync(mountDirectory, driverFolder, recurse: true, forceUnsigned: forceUnsigned, progress, ct);
}
