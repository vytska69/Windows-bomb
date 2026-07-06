using System.IO.Compression;
using WinIsoOptimizer.Core.Setup;

namespace WinIsoOptimizer.Core.LegacyBoot;

/// <summary>
/// Downloads a <see cref="UefiSevenReleaseAsset"/> to disk and, if it's a .zip archive, extracts it and
/// locates the compiled .efi bootloader inside. See <see cref="UefiSevenReleaseFetcher"/> for why this
/// downloads straight from the upstream GitHub release rather than a copy this project would host.
/// </summary>
public sealed class UefiSevenDownloadService
{
    private readonly IHttpDownloader _downloader;

    public UefiSevenDownloadService(IHttpDownloader? downloader = null)
    {
        _downloader = downloader ?? new HttpDownloader();
    }

    /// <summary>
    /// Downloads <paramref name="asset"/> into <paramref name="destinationDirectory"/> and returns the
    /// path to the usable .efi file — either the asset itself, or (if it was a .zip) the .efi file
    /// found inside it. Throws if a .zip asset contains no .efi file at all.
    /// </summary>
    public async Task<string> DownloadBootloaderAsync(UefiSevenReleaseAsset asset, string destinationDirectory, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var downloadPath = Path.Combine(destinationDirectory, asset.Name);
        progress?.Report($"Downloading {asset.Name} from the UefiSeven GitHub release...");
        await _downloader.DownloadFileAsync(asset.DownloadUrl, downloadPath, progress, ct).ConfigureAwait(false);

        if (!asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return downloadPath;
        }

        var extractDir = Path.Combine(destinationDirectory, "extracted");
        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, recursive: true);
        }

        progress?.Report($"Extracting {asset.Name}...");
        ZipFile.ExtractToDirectory(downloadPath, extractDir);

        return FindEfiFileInExtractedFolder(extractDir)
            ?? throw new InvalidOperationException($"Downloaded {asset.Name} but couldn't find a .efi file inside it.");
    }

    /// <summary>Prefers a file with "bootx64" in its name (what UefiSeven's own README names it), then
    /// falls back to whatever the only .efi file present is.</summary>
    internal static string? FindEfiFileInExtractedFolder(string extractDir)
    {
        var efiFiles = Directory.EnumerateFiles(extractDir, "*.efi", SearchOption.AllDirectories).ToList();
        return efiFiles.FirstOrDefault(p => Path.GetFileName(p).Contains("bootx64", StringComparison.OrdinalIgnoreCase))
            ?? efiFiles.FirstOrDefault();
    }
}
