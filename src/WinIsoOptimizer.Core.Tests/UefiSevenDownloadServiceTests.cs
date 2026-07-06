using System.IO.Compression;
using WinIsoOptimizer.Core.LegacyBoot;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class UefiSevenDownloadServiceTests : IDisposable
{
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("wioo-uefiseven-test-").FullName;

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    [Fact]
    public async Task DownloadBootloaderAsync_returns_the_downloaded_path_for_a_direct_efi_asset()
    {
        var downloader = new FakeHttpDownloader
        {
            OnDownload = (_, path) => File.WriteAllTextAsync(path, "fake efi bytes"),
        };
        var service = new UefiSevenDownloadService(downloader);
        var asset = new UefiSevenReleaseAsset("bootx64.efi", "https://example.invalid/bootx64.efi");

        var result = await service.DownloadBootloaderAsync(asset, _tempRoot);

        Assert.Equal(Path.Combine(_tempRoot, "bootx64.efi"), result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public async Task DownloadBootloaderAsync_extracts_a_zip_asset_and_finds_the_efi_file_inside()
    {
        var zipContentDir = Path.Combine(_tempRoot, "zip-source");
        Directory.CreateDirectory(zipContentDir);
        File.WriteAllText(Path.Combine(zipContentDir, "bootx64.efi"), "fake compiled efi");
        File.WriteAllText(Path.Combine(zipContentDir, "UefiSeven.ini"), "; sample config");
        var zipPath = Path.Combine(_tempRoot, "source.zip");
        ZipFile.CreateFromDirectory(zipContentDir, zipPath);

        var downloader = new FakeHttpDownloader
        {
            OnDownload = (_, path) => { File.Copy(zipPath, path); return Task.CompletedTask; },
        };
        var service = new UefiSevenDownloadService(downloader);
        var destinationDirectory = Path.Combine(_tempRoot, "download");
        var asset = new UefiSevenReleaseAsset("UefiSeven-1.30.zip", "https://example.invalid/UefiSeven-1.30.zip");

        var result = await service.DownloadBootloaderAsync(asset, destinationDirectory);

        Assert.Equal("bootx64.efi", Path.GetFileName(result));
        Assert.Equal("fake compiled efi", File.ReadAllText(result));
    }

    [Fact]
    public async Task DownloadBootloaderAsync_throws_when_the_zip_asset_has_no_efi_file_inside()
    {
        var zipContentDir = Path.Combine(_tempRoot, "zip-source-empty");
        Directory.CreateDirectory(zipContentDir);
        File.WriteAllText(Path.Combine(zipContentDir, "readme.txt"), "no efi here");
        var zipPath = Path.Combine(_tempRoot, "empty.zip");
        ZipFile.CreateFromDirectory(zipContentDir, zipPath);

        var downloader = new FakeHttpDownloader
        {
            OnDownload = (_, path) => { File.Copy(zipPath, path); return Task.CompletedTask; },
        };
        var service = new UefiSevenDownloadService(downloader);
        var asset = new UefiSevenReleaseAsset("empty.zip", "https://example.invalid/empty.zip");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DownloadBootloaderAsync(asset, Path.Combine(_tempRoot, "download2")));
    }

    [Fact]
    public void FindEfiFileInExtractedFolder_prefers_a_file_named_like_bootx64()
    {
        var dir = Path.Combine(_tempRoot, "prefer-bootx64");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "other.efi"), "x");
        File.WriteAllText(Path.Combine(dir, "bootx64.efi"), "y");

        var found = UefiSevenDownloadService.FindEfiFileInExtractedFolder(dir);

        Assert.Equal("bootx64.efi", Path.GetFileName(found));
    }
}
