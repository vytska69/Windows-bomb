namespace WinIsoOptimizer.Core.Imaging;

/// <summary>
/// Read-only lookups a GUI needs before a user commits to running the full optimization job: what
/// editions does this install.wim contain, and what provisioned apps does a given edition have. Each
/// call mounts read-only, inspects, and always discards the mount afterwards — nothing here modifies
/// the image.
/// </summary>
public sealed class ImageInspectionService
{
    private readonly DismService _dism;

    public ImageInspectionService(DismService dism)
    {
        _dism = dism;
    }

    public Task<IReadOnlyList<WimImageInfo>> ListEditionsAsync(string wimPath, IProgress<string>? progress = null, CancellationToken ct = default) =>
        _dism.GetWimInfoAsync(wimPath, progress, ct);

    public async Task<IReadOnlyList<ProvisionedAppxPackage>> ListProvisionedAppsAsync(string wimPath, int index, string scratchMountDirectory, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(scratchMountDirectory);
        await _dism.MountImageAsync(wimPath, index, scratchMountDirectory, readOnly: true, progress, ct).ConfigureAwait(false);
        try
        {
            return await _dism.GetProvisionedAppxPackagesAsync(scratchMountDirectory, progress, ct).ConfigureAwait(false);
        }
        finally
        {
            // Read-only inspection mount: always discard, there is nothing to commit and a read-only
            // mount left attached blocks the directory from being reused/deleted afterwards.
            await _dism.UnmountImageAsync(scratchMountDirectory, commit: false, progress, ct).ConfigureAwait(false);
        }
    }
}
