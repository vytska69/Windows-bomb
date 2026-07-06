using System.Diagnostics;
using System.Security.Cryptography;
using WinIsoOptimizer.Core.Setup;

namespace WinIsoOptimizer.Core.Updates;

/// <summary>
/// Downloads and verifies the installer for a newer release, and launches it. Verification is
/// mandatory, not optional: <see cref="DownloadAndVerifyAsync"/> deletes the downloaded file and
/// throws rather than ever handing back a path to something whose hash didn't match what GitHub's own
/// release API reported — a corrupted download or a tampered release should never silently get run
/// with the admin rights this app already carries.
/// </summary>
public sealed class SelfUpdateService
{
    private readonly IHttpDownloader _downloader;

    public SelfUpdateService(IHttpDownloader? downloader = null)
    {
        _downloader = downloader ?? new HttpDownloader();
    }

    /// <summary>True if <paramref name="appDirectory"/> (normally the running exe's own folder)
    /// contains an Inno Setup uninstaller — i.e. this copy of the app was placed there by the
    /// installer, not just extracted from the portable zip. Only installed copies can be
    /// silently updated in place; a portable copy has no installer to hand back control to.</summary>
    public static bool IsRunningFromInstalledLocation(string appDirectory) =>
        Directory.Exists(appDirectory) && Directory.EnumerateFiles(appDirectory, "unins*.exe").Any();

    public async Task DownloadAndVerifyAsync(string downloadUrl, string destinationPath, string expectedSha256, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        await _downloader.DownloadFileAsync(downloadUrl, destinationPath, progress, ct).ConfigureAwait(false);

        progress?.Report("Verifying the downloaded installer...");
        var actualSha256 = await ComputeSha256Async(destinationPath, ct).ConfigureAwait(false);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException(
                "The downloaded installer's checksum doesn't match what GitHub reported for this release — " +
                "refusing to run it. This can happen with a corrupted download; try again, or download and " +
                "verify it manually from the release page.");
        }
    }

    /// <summary>Launches a verified installer silently (no wizard UI, no reboot). Does not exit the
    /// current process itself — the caller (which knows whether it's a GUI that needs to shut down
    /// cleanly first) is responsible for that.</summary>
    public static void LaunchInstallerSilently(string installerPath)
    {
        var startInfo = new ProcessStartInfo(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
        {
            UseShellExecute = true,
        };
        Process.Start(startInfo);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await Task.Run(() => sha256.ComputeHash(stream), ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
