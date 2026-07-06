using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Processes;

namespace WinIsoOptimizer.Core.Setup;

/// <summary>
/// oscdimg.exe (used to author the final bootable ISO) ships only with the Windows ADK's
/// "Deployment Tools" component, not with Windows itself. This automates getting it: run the ADK's
/// own installer silently with just that one feature selected, so the user never has to click through
/// the ADK setup wizard or hunt for the right checkbox.
///
/// What this deliberately does NOT do: guess or hardcode a direct download URL for the ADK installer
/// itself. Microsoft's direct download link is versioned and changes with each ADK release, and
/// shipping a stale/wrong hardcoded URL would silently break this feature later. Instead,
/// <see cref="OfficialAdkDownloadPageUrl"/> points at Microsoft's stable documentation/download
/// landing page (unchanged for years), which the caller (the GUI) opens in the user's browser for a
/// one-click official download — everything after that (running the installer silently, verifying
/// oscdimg.exe landed where expected) is automated.
/// </summary>
public sealed class AdkDeploymentToolsInstaller
{
    public const string OfficialAdkDownloadPageUrl = "https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install";

    private readonly IProcessRunner _runner;
    private readonly IHttpDownloader _downloader;

    public AdkDeploymentToolsInstaller(IProcessRunner runner, IHttpDownloader? downloader = null)
    {
        _runner = runner;
        _downloader = downloader ?? new HttpDownloader();
    }

    public static bool IsOscdimgAvailable(ExternalToolPaths tools) => File.Exists(tools.Oscdimg);

    /// <summary>Downloads a file the caller already has a direct URL for (e.g. one the user pasted in
    /// after visiting <see cref="OfficialAdkDownloadPageUrl"/> themselves) — this class never supplies
    /// that URL on its own.</summary>
    public Task DownloadInstallerAsync(string downloadUrl, string destinationPath, IProgress<string>? progress = null, CancellationToken ct = default) =>
        _downloader.DownloadFileAsync(downloadUrl, destinationPath, progress, ct);

    /// <summary>
    /// Runs the ADK's own installer with only the Deployment Tools feature selected, no UI, no reboot.
    /// This is Microsoft's own documented silent-install syntax for adksetup.exe, not a hack.
    /// </summary>
    public async Task SilentInstallDeploymentToolsAsync(string adkSetupExePath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        progress?.Report("Installing Windows ADK Deployment Tools (silent, no reboot)...");
        var request = new ProcessRequest(
            adkSetupExePath,
            new[] { "/quiet", "/features", "OptionId.DeploymentTools", "/ceip", "off", "/norestart" },
            OutputLineProgress: progress);
        var result = await _runner.RunAsync(request, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ExternalToolException(request, result);
        }
    }

    /// <summary>
    /// End-to-end: run the already-downloaded ADK installer silently, then confirm oscdimg.exe now
    /// exists at <paramref name="tools"/>'s configured path. Returns false (rather than throwing) if
    /// the install "succeeded" per its exit code but oscdimg.exe still isn't where expected — that
    /// means the ADK was installed to a non-default location, and the caller should ask the user to
    /// browse to the real path instead of silently reporting success.
    /// </summary>
    public async Task<bool> InstallAndVerifyAsync(string adkSetupExePath, ExternalToolPaths tools, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        await SilentInstallDeploymentToolsAsync(adkSetupExePath, progress, ct).ConfigureAwait(false);

        var found = IsOscdimgAvailable(tools);
        progress?.Report(found
            ? $"oscdimg.exe found at {tools.Oscdimg}."
            : $"Install finished, but oscdimg.exe still isn't at {tools.Oscdimg} — the ADK may have installed elsewhere; browse to it manually.");
        return found;
    }
}
