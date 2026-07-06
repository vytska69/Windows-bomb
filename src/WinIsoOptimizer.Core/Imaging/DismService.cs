using System.Globalization;
using WinIsoOptimizer.Core.Processes;

namespace WinIsoOptimizer.Core.Imaging;

/// <summary>
/// Thin, testable wrapper around dism.exe for the operations this tool needs: reading and mounting
/// install.wim/boot.wim, listing/removing provisioned apps, injecting drivers, and offline cleanup.
/// Every method builds a dism.exe argument list and hands it to <see cref="IProcessRunner"/>, so the
/// argument-construction and text-parsing logic can be unit tested without a real Windows/dism host.
/// </summary>
public sealed class DismService
{
    private readonly IProcessRunner _runner;
    private readonly ExternalToolPaths _tools;

    public DismService(IProcessRunner runner, ExternalToolPaths? tools = null)
    {
        _runner = runner;
        _tools = tools ?? new ExternalToolPaths();
    }

    public async Task<IReadOnlyList<WimImageInfo>> GetWimInfoAsync(string wimPath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var result = await RunDismAsync(new[] { $"/Get-WimInfo", $"/WimFile:{wimPath}" }, progress, ct).ConfigureAwait(false);
        return ParseWimInfo(result.StandardOutput);
    }

    /// <summary>Queries one index directly (no mount needed — `/Get-WimInfo` reads WIM metadata, it
    /// doesn't need `/Image:`) for the extra detail the plain listing above doesn't include, notably
    /// the NT kernel version needed to reliably tell Windows 10 from 11 (see <see cref="WindowsVersionClassifier"/>).</summary>
    public async Task<WimImageDetails?> GetWimImageDetailsAsync(string wimPath, int index, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var result = await RunDismAsync(new[] { "/Get-WimInfo", $"/WimFile:{wimPath}", $"/Index:{index}" }, progress, ct).ConfigureAwait(false);
        return ParseWimImageDetails(result.StandardOutput);
    }

    public async Task MountImageAsync(string wimPath, int index, string mountDirectory, bool readOnly = false, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(mountDirectory);
        var args = new List<string>
        {
            "/Mount-Image",
            $"/ImageFile:{wimPath}",
            $"/Index:{index}",
            $"/MountDir:{mountDirectory}",
        };
        if (readOnly) args.Add("/ReadOnly");
        await RunDismAsync(args, progress, ct).ConfigureAwait(false);
    }

    public async Task UnmountImageAsync(string mountDirectory, bool commit, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "/Unmount-Image",
            $"/MountDir:{mountDirectory}",
            commit ? "/Commit" : "/Discard",
        };
        await RunDismAsync(args, progress, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProvisionedAppxPackage>> GetProvisionedAppxPackagesAsync(string mountDirectory, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var result = await RunDismAsync(new[] { "/Image:" + mountDirectory, "/Get-ProvisionedAppxPackages" }, progress, ct).ConfigureAwait(false);
        return ParseProvisionedAppxPackages(result.StandardOutput);
    }

    public async Task RemoveProvisionedAppxPackageAsync(string mountDirectory, string packageName, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        await RunDismAsync(new[]
        {
            "/Image:" + mountDirectory,
            "/Remove-ProvisionedAppxPackage",
            $"/PackageName:{packageName}",
        }, progress, ct).ConfigureAwait(false);
    }

    /// <summary>Injects every driver (.inf) under <paramref name="driverFolder"/>, recursively, into the mounted image.</summary>
    public async Task AddDriverAsync(string mountDirectory, string driverFolder, bool recurse = true, bool forceUnsigned = false, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "/Image:" + mountDirectory,
            "/Add-Driver",
            $"/Driver:{driverFolder}",
        };
        if (recurse) args.Add("/Recurse");
        if (forceUnsigned) args.Add("/ForceUnsigned");
        await RunDismAsync(args, progress, ct).ConfigureAwait(false);
    }

    /// <summary>Exports every third-party driver from the currently running ("online") Windows install.</summary>
    public async Task ExportOnlineDriversAsync(string destinationFolder, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationFolder);
        await RunDismAsync(new[]
        {
            "/Online",
            "/Export-Driver",
            $"/Destination:{destinationFolder}",
        }, progress, ct).ConfigureAwait(false);
    }

    /// <summary>Exports one index from a source image (typically install.esd) into a destination
    /// image file (typically install.wim), appending it if the destination already has other indices.
    /// This is how a solid-compressed .esd (which dism can only mount read-only) gets turned into an
    /// editable .wim covering the same editions.</summary>
    public async Task ExportImageIndexAsync(string sourceImagePath, int sourceIndex, string destinationImagePath, bool compressMax = true, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "/Export-Image",
            $"/SourceImageFile:{sourceImagePath}",
            $"/SourceIndex:{sourceIndex}",
            $"/DestinationImageFile:{destinationImagePath}",
        };
        if (compressMax) args.Add("/Compress:max");
        await RunDismAsync(args, progress, ct).ConfigureAwait(false);
    }

    /// <summary>Runs component-store cleanup and (optionally) permanently removes superseded versions
    /// of updated components (ResetBase) to shrink the image. ResetBase is irreversible for that WIM.</summary>
    public async Task CleanupImageAsync(string mountDirectory, bool resetBase, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var args = new List<string> { "/Image:" + mountDirectory, "/Cleanup-Image", "/StartComponentCleanup" };
        if (resetBase) args.Add("/ResetBase");
        await RunDismAsync(args, progress, ct).ConfigureAwait(false);
    }

    private async Task<ProcessResult> RunDismAsync(IReadOnlyList<string> args, IProgress<string>? progress, CancellationToken ct)
    {
        var request = new ProcessRequest(_tools.Dism, args, OutputLineProgress: progress);
        var result = await _runner.RunAsync(request, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ExternalToolException(request, result);
        }
        return result;
    }

    internal static IReadOnlyList<WimImageInfo> ParseWimInfo(string dismOutput)
    {
        var images = new List<WimImageInfo>();
        int? index = null;
        string? name = null;
        string? description = null;
        long size = 0;

        void FlushIfComplete()
        {
            if (index is not null && name is not null)
            {
                images.Add(new WimImageInfo(index.Value, name, description ?? string.Empty, size));
            }
            index = null;
            name = null;
            description = null;
            size = 0;
        }

        foreach (var rawLine in dismOutput.Split('\n'))
        {
            var line = rawLine.Trim('\r', '\n', ' ');
            if (line.Length == 0) continue;

            var (key, value) = SplitKeyValue(line);
            if (key is null) continue;

            switch (key)
            {
                case "Index":
                    // A new "Index :" line starts a new entry; flush whatever we were building.
                    FlushIfComplete();
                    index = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
                    break;
                case "Name":
                    name = value;
                    break;
                case "Description":
                    description = value;
                    break;
                case "Size":
                    // Dism prints sizes like "15,406,489,038 bytes".
                    var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
                    long.TryParse(digitsOnly, out size);
                    break;
            }
        }

        FlushIfComplete();
        return images;
    }

    /// <summary>Parses the extended single-index form of `dism /Get-WimInfo /WimFile:X /Index:N`.
    /// Field names below (Version, Edition ID, Installation Type, Architecture) follow DISM's
    /// documented output for this command; tolerant of missing fields since exact wording has some
    /// history of drifting slightly across Windows/ADK versions — a field this doesn't recognize is
    /// just left null rather than treated as a parse failure.</summary>
    internal static WimImageDetails? ParseWimImageDetails(string dismOutput)
    {
        int? index = null;
        string? name = null;
        string? version = null;
        string? editionId = null;
        string? installationType = null;
        string? architecture = null;

        foreach (var rawLine in dismOutput.Split('\n'))
        {
            var line = rawLine.Trim('\r', '\n', ' ');
            if (line.Length == 0) continue;

            var (key, value) = SplitKeyValue(line);
            if (key is null) continue;

            switch (key)
            {
                case "Index":
                    index = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : index;
                    break;
                case "Name":
                    name = value;
                    break;
                case "Version":
                    version = value;
                    break;
                case "Edition ID":
                    editionId = value;
                    break;
                case "Installation Type":
                    installationType = value;
                    break;
                case "Architecture":
                    architecture = value;
                    break;
            }
        }

        return index is not null && name is not null
            ? new WimImageDetails(index.Value, name, version, editionId, installationType, architecture)
            : null;
    }

    internal static IReadOnlyList<ProvisionedAppxPackage> ParseProvisionedAppxPackages(string dismOutput)
    {
        var packages = new List<ProvisionedAppxPackage>();
        string? displayName = null;
        string? packageName = null;

        void FlushIfComplete()
        {
            if (displayName is not null && packageName is not null)
            {
                packages.Add(new ProvisionedAppxPackage(displayName, packageName));
            }
            displayName = null;
            packageName = null;
        }

        foreach (var rawLine in dismOutput.Split('\n'))
        {
            var line = rawLine.Trim('\r', '\n', ' ');
            if (line.Length == 0) continue;

            var (key, value) = SplitKeyValue(line);
            if (key is null) continue;

            switch (key)
            {
                case "DisplayName":
                    // A new "DisplayName :" line starts a new package block; flush the previous one.
                    FlushIfComplete();
                    displayName = value;
                    break;
                case "PackageName":
                    packageName = value;
                    break;
            }
        }

        FlushIfComplete();
        return packages;
    }

    private static (string? Key, string Value) SplitKeyValue(string line)
    {
        var separatorIndex = line.IndexOf(" : ", StringComparison.Ordinal);
        if (separatorIndex < 0) return (null, string.Empty);
        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 3)..].Trim();
        return (key, value);
    }
}
