using System.Text.RegularExpressions;
using WinIsoOptimizer.Core.Processes;

namespace WinIsoOptimizer.Core.Imaging;

/// <summary>
/// Extracts an ISO's contents to a working folder and rebuilds a dual BIOS+UEFI bootable ISO from a
/// (possibly modified) folder, using tools that ship with Windows (PowerShell's Mount-DiskImage,
/// robocopy) plus oscdimg.exe from the Windows ADK "Deployment Tools" component for the final
/// bootable-ISO authoring step, since Windows has no in-box tool for that part.
/// </summary>
public sealed class IsoService
{
    private static readonly Regex DriveLetterPattern = new(@"^[A-Za-z]:?$", RegexOptions.Compiled);

    private readonly IProcessRunner _runner;
    private readonly ExternalToolPaths _tools;

    public IsoService(IProcessRunner runner, ExternalToolPaths? tools = null)
    {
        _runner = runner;
        _tools = tools ?? new ExternalToolPaths();
    }

    public async Task ExtractAsync(string isoPath, string destinationFolder, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationFolder);

        progress?.Report($"Mounting {isoPath}...");
        var driveLetter = await MountAndGetDriveLetterAsync(isoPath, progress, ct).ConfigureAwait(false);
        try
        {
            progress?.Report($"Copying ISO contents from {driveLetter}:\\ to {destinationFolder}...");
            await RobocopyMirrorAsync($"{driveLetter}:\\", destinationFolder, progress, ct).ConfigureAwait(false);
        }
        finally
        {
            progress?.Report("Dismounting ISO...");
            await RunPowerShellAsync($"Dismount-DiskImage -ImagePath '{EscapeForPowerShellSingleQuoted(isoPath)}'", progress, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds a bootable ISO from <paramref name="sourceFolder"/> using the BIOS (etfsboot.com) and
    /// UEFI (efisys.bin) boot sector files already present inside that same folder — these are part
    /// of every modern Windows install media and are what makes the rebuilt ISO boot identically on
    /// legacy BIOS and UEFI machines. Throws with a clear message if either is missing (this is the
    /// expected case for Windows XP/Vista media, which never shipped a UEFI boot sector at all —
    /// see docs/LEGACY-UEFI-BOOT.md).
    /// </summary>
    public async Task BuildBootableIsoAsync(string sourceFolder, string outputIsoPath, string volumeLabel, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var biosBootFile = Path.Combine(sourceFolder, "boot", "etfsboot.com");
        var uefiBootFile = Path.Combine(sourceFolder, "efi", "microsoft", "boot", "efisys.bin");

        var hasBios = File.Exists(biosBootFile);
        var hasUefi = File.Exists(uefiBootFile);
        if (!hasBios && !hasUefi)
        {
            throw new InvalidOperationException(
                $"Neither a BIOS boot file ({biosBootFile}) nor a UEFI boot file ({uefiBootFile}) was " +
                "found in the source folder — this doesn't look like extracted Windows install media.");
        }

        if (!File.Exists(_tools.Oscdimg))
        {
            throw new FileNotFoundException(
                "oscdimg.exe was not found. It ships with the Windows ADK \"Deployment Tools\" component, " +
                "not with Windows itself — install the ADK or point Settings at your own oscdimg.exe.",
                _tools.Oscdimg);
        }

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputIsoPath));
        if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

        var args = new List<string> { "-m", "-o", "-u2", "-udfver102", $"-l{volumeLabel}" };

        if (hasBios && hasUefi)
        {
            args.Add($"-bootdata:2#p0,e,b{biosBootFile}#pEF,e,b{uefiBootFile}");
        }
        else if (hasBios)
        {
            // BIOS-only media (e.g. original Windows 7/Vista/XP source with no efisys.bin at all).
            args.Add($"-bootdata:1#p0,e,b{biosBootFile}");
        }
        else
        {
            args.Add($"-bootdata:1#pEF,e,b{uefiBootFile}");
        }

        args.Add(sourceFolder);
        args.Add(outputIsoPath);

        progress?.Report($"Building ISO: {outputIsoPath}...");
        var request = new ProcessRequest(_tools.Oscdimg, args, OutputLineProgress: progress);
        var result = await _runner.RunAsync(request, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ExternalToolException(request, result);
        }
    }

    private async Task<string> MountAndGetDriveLetterAsync(string isoPath, IProgress<string>? progress, CancellationToken ct)
    {
        var script =
            $"$img = Mount-DiskImage -ImagePath '{EscapeForPowerShellSingleQuoted(isoPath)}' -PassThru; " +
            "($img | Get-Volume).DriveLetter";
        var result = await RunPowerShellAsync(script, progress, ct).ConfigureAwait(false);

        var driveLetter = result.StandardOutput.Trim();
        if (!DriveLetterPattern.IsMatch(driveLetter))
        {
            throw new InvalidOperationException($"Could not determine the drive letter Windows assigned to the mounted ISO (got: '{driveLetter}').");
        }
        return driveLetter.TrimEnd(':');
    }

    private async Task RobocopyMirrorAsync(string sourceRoot, string destinationFolder, IProgress<string>? progress, CancellationToken ct)
    {
        // /E copies subfolders including empty ones; /R:1 /W:1 keep a locked/in-use file from
        // stalling the whole copy for robocopy's (very long) default retry window.
        var request = new ProcessRequest("robocopy.exe", new[] { sourceRoot, destinationFolder, "/E", "/R:1", "/W:1" }, OutputLineProgress: progress);
        var result = await _runner.RunAsync(request, ct).ConfigureAwait(false);
        // robocopy's exit code is a bitmask where 0-7 all mean success (8+ means at least one failure).
        if (result.ExitCode >= 8)
        {
            throw new ExternalToolException(request, result);
        }
    }

    private async Task<ProcessResult> RunPowerShellAsync(string script, IProgress<string>? progress, CancellationToken ct)
    {
        var request = new ProcessRequest("powershell.exe", new[] { "-NoProfile", "-NonInteractive", "-Command", script }, OutputLineProgress: progress);
        var result = await _runner.RunAsync(request, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ExternalToolException(request, result);
        }
        return result;
    }

    private static string EscapeForPowerShellSingleQuoted(string value) => value.Replace("'", "''");
}
