using WinIsoOptimizer.Core.Drivers;
using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.LegacyBoot;
using WinIsoOptimizer.Core.Processes;
using WinIsoOptimizer.Core.Telemetry;

namespace WinIsoOptimizer.Core.Jobs;

/// <summary>
/// Orchestrates the end-to-end pipeline: extract ISO -> convert install.esd to install.wim if needed
/// -> service each requested edition (debloat, telemetry tweaks, app removal, driver injection) ->
/// optionally patch legacy UEFI boot support -> optionally drop in an autounattend.xml -> rebuild a
/// bootable ISO. Every stage reports through the same <see cref="IProgress{OptimizationProgress}"/> so
/// a GUI can drive both a progress bar and a screen-reader-friendly live region from one callback.
/// </summary>
public sealed class IsoOptimizationJob
{
    private readonly IsoService _iso;
    private readonly DismService _dism;
    private readonly TelemetryDebloatService _debloat;
    private readonly DriverService _drivers;

    /// <summary>Read-only edition/app inspection, exposed so a GUI can populate its app checklist
    /// after <see cref="PrepareSourceAsync"/> without constructing its own duplicate set of services.</summary>
    public ImageInspectionService Inspection { get; }

    /// <summary>Driver export/injection, exposed so a GUI can drive the "export drivers from this PC"
    /// step independently of running the full optimization job.</summary>
    public DriverService Drivers => _drivers;

    public IsoOptimizationJob(IProcessRunner runner, ExternalToolPaths? toolPaths = null)
    {
        var tools = toolPaths ?? new ExternalToolPaths();
        _iso = new IsoService(runner, tools);
        _dism = new DismService(runner, tools);
        _debloat = new TelemetryDebloatService(new OfflineRegistryService(runner, tools), _dism);
        _drivers = new DriverService(_dism);
        Inspection = new ImageInspectionService(_dism);
    }

    /// <summary>Extracts the source ISO (unless <paramref name="alreadyExtracted"/>) into
    /// "workingDirectory/extracted" and makes sure it has an install.wim (converting install.esd if
    /// that's what the media shipped instead), returning the resulting install.wim path. Exposed
    /// publicly so a GUI can call this once to let the user inspect editions/apps, then run the rest
    /// of the pipeline via <see cref="RunAsync"/> with <see cref="IsoOptimizationRequest.SourceAlreadyExtracted"/> set.</summary>
    public async Task<string> PrepareSourceAsync(string sourceIsoPath, string workingDirectory, bool alreadyExtracted, IProgress<OptimizationProgress>? progress = null, CancellationToken ct = default)
    {
        var textProgress = new Progress<string>(msg => progress?.Report(new OptimizationProgress(msg)));
        var extractedFolder = Path.Combine(workingDirectory, "extracted");

        if (alreadyExtracted)
        {
            Report(progress, "Using previously-extracted source ISO contents...", 5);
        }
        else
        {
            Report(progress, "Extracting source ISO...", 5);
            await _iso.ExtractAsync(sourceIsoPath, extractedFolder, textProgress, ct).ConfigureAwait(false);
        }

        var sourcesFolder = Path.Combine(extractedFolder, "sources");
        var wimPath = Path.Combine(sourcesFolder, "install.wim");
        var esdPath = Path.Combine(sourcesFolder, "install.esd");

        if (!File.Exists(wimPath) && File.Exists(esdPath))
        {
            Report(progress, "Converting install.esd to install.wim (this can take a while)...", 15);
            await ConvertEsdToWimAsync(esdPath, wimPath, textProgress, ct).ConfigureAwait(false);
        }

        if (!File.Exists(wimPath))
        {
            throw new FileNotFoundException("No install.wim (or install.esd to convert) was found in \\sources — this doesn't look like Windows 10/11 install media.", wimPath);
        }

        return wimPath;
    }

    public async Task RunAsync(IsoOptimizationRequest request, IProgress<OptimizationProgress>? progress = null, CancellationToken ct = default)
    {
        var textProgress = new Progress<string>(msg => progress?.Report(new OptimizationProgress(msg)));

        var extractedFolder = Path.Combine(request.WorkingDirectory, "extracted");
        var mountFolder = Path.Combine(request.WorkingDirectory, "mount");

        var wimPath = await PrepareSourceAsync(request.SourceIsoPath, request.WorkingDirectory, request.SourceAlreadyExtracted, progress, ct).ConfigureAwait(false);

        var allEditions = await _dism.GetWimInfoAsync(wimPath, textProgress, ct).ConfigureAwait(false);
        var editionsToService = request.EditionIndices is { Count: > 0 }
            ? allEditions.Where(e => request.EditionIndices.Contains(e.Index)).ToList()
            : allEditions.ToList();

        var editionCount = Math.Max(editionsToService.Count, 1);
        for (var i = 0; i < editionsToService.Count; i++)
        {
            var edition = editionsToService[i];
            var basePercent = 20 + i * 50 / editionCount;
            Report(progress, $"Servicing edition {edition.Index} ({edition.Name})...", basePercent);
            await ServiceEditionAsync(wimPath, edition.Index, mountFolder, request, textProgress, progress, basePercent, ct).ConfigureAwait(false);
        }

        if (request.AlsoInjectDriversIntoBootWim && request.DriverFolderToInject is { Length: > 0 })
        {
            Report(progress, "Injecting drivers into boot.wim (Setup environment)...", 72);
            await InjectDriversIntoBootWimAsync(extractedFolder, mountFolder, request.DriverFolderToInject, request.ForceUnsignedDrivers, textProgress, ct).ConfigureAwait(false);
        }

        if (request.Unattend is not null)
        {
            Report(progress, "Writing autounattend.xml...", 78);
            var xml = UnattendGenerator.Generate(request.Unattend);
            await File.WriteAllTextAsync(Path.Combine(extractedFolder, "autounattend.xml"), xml, ct).ConfigureAwait(false);
        }

        if (request.ApplyLegacyUefiBootFixIfApplicable)
        {
            var assessment = LegacyUefiBootInjector.Assess(extractedFolder);
            if (assessment.Support == LegacyUefiBootSupport.FixableFallbackBootloaderMissing)
            {
                Report(progress, "Applying legacy UEFI boot fix (adding \\EFI\\Boot\\bootx64.efi fallback loader)...", 82);
                LegacyUefiBootInjector.ApplyFallbackBootloaderFix(extractedFolder, textProgress);
            }
            else
            {
                Report(progress, $"Legacy UEFI boot check: {assessment.Explanation}", 82);
            }
        }

        Report(progress, "Building the optimized ISO...", 90);
        await _iso.BuildBootableIsoAsync(extractedFolder, request.OutputIsoPath, request.VolumeLabel, textProgress, ct).ConfigureAwait(false);

        Report(progress, $"Done. Optimized ISO written to {request.OutputIsoPath}.", 100);
    }

    private async Task ServiceEditionAsync(
        string wimPath,
        int index,
        string mountFolderRoot,
        IsoOptimizationRequest request,
        IProgress<string> textProgress,
        IProgress<OptimizationProgress>? progress,
        int basePercent,
        CancellationToken ct)
    {
        var mountDir = Path.Combine(mountFolderRoot, $"index-{index}");
        Directory.CreateDirectory(mountDir);

        await _dism.MountImageAsync(wimPath, index, mountDir, readOnly: false, textProgress, ct).ConfigureAwait(false);
        var committed = false;
        try
        {
            await _debloat.ApplyAsync(mountDir, request.Optimizations, textProgress, ct).ConfigureAwait(false);

            if (request.AppsToRemove.Count > 0)
            {
                var installedApps = await _dism.GetProvisionedAppxPackagesAsync(mountDir, textProgress, ct).ConfigureAwait(false);
                var toRemove = installedApps
                    .Where(installed => request.AppsToRemove.Any(requested =>
                        string.Equals(requested.PackageName, installed.PackageName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                await _debloat.RemoveSelectedAppsAsync(mountDir, toRemove, textProgress, ct).ConfigureAwait(false);
            }

            if (request.DriverFolderToInject is { Length: > 0 })
            {
                Report(progress, $"Injecting drivers into edition {index}...", basePercent + 10);
                await _drivers.InjectIntoMountedImageAsync(mountDir, request.DriverFolderToInject, request.ForceUnsignedDrivers, textProgress, ct).ConfigureAwait(false);
            }

            committed = true;
        }
        finally
        {
            // Always unmount, even on failure — otherwise the mount directory stays locked and every
            // later step (rebuilding the ISO, even re-running this tool) fails confusingly.
            await _dism.UnmountImageAsync(mountDir, commit: committed, textProgress, ct).ConfigureAwait(false);
        }
    }

    private async Task ConvertEsdToWimAsync(string esdPath, string wimPath, IProgress<string> textProgress, CancellationToken ct)
    {
        var editions = await _dism.GetWimInfoAsync(esdPath, textProgress, ct).ConfigureAwait(false);
        foreach (var edition in editions)
        {
            await _dism.ExportImageIndexAsync(esdPath, edition.Index, wimPath, compressMax: true, textProgress, ct).ConfigureAwait(false);
        }
    }

    private async Task InjectDriversIntoBootWimAsync(string extractedFolder, string mountFolderRoot, string driverFolder, bool forceUnsigned, IProgress<string> textProgress, CancellationToken ct)
    {
        var bootWimPath = Path.Combine(extractedFolder, "sources", "boot.wim");
        if (!File.Exists(bootWimPath)) return;

        var bootEditions = await _dism.GetWimInfoAsync(bootWimPath, textProgress, ct).ConfigureAwait(false);
        foreach (var edition in bootEditions)
        {
            var mountDir = Path.Combine(mountFolderRoot, $"boot-index-{edition.Index}");
            Directory.CreateDirectory(mountDir);
            await _dism.MountImageAsync(bootWimPath, edition.Index, mountDir, readOnly: false, textProgress, ct).ConfigureAwait(false);
            var committed = false;
            try
            {
                await _drivers.InjectIntoMountedImageAsync(mountDir, driverFolder, forceUnsigned, textProgress, ct).ConfigureAwait(false);
                committed = true;
            }
            finally
            {
                await _dism.UnmountImageAsync(mountDir, commit: committed, textProgress, ct).ConfigureAwait(false);
            }
        }
    }

    private static void Report(IProgress<OptimizationProgress>? progress, string message, int percent) =>
        progress?.Report(new OptimizationProgress(message, percent));
}
