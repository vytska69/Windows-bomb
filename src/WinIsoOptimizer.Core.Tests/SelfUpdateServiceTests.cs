using System.Security.Cryptography;
using System.Text;
using WinIsoOptimizer.Core.Updates;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class SelfUpdateServiceTests : IDisposable
{
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("wioo-selfupdate-test-").FullName;

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task DownloadAndVerifyAsync_succeeds_when_hash_matches()
    {
        var content = Encoding.UTF8.GetBytes("pretend this is an installer");
        var expectedHash = Sha256Hex(content);
        var destination = Path.Combine(_tempRoot, "Setup.exe");
        var downloader = new FakeHttpDownloader { OnDownload = (_, path) => { File.WriteAllBytes(path, content); return Task.CompletedTask; } };
        var service = new SelfUpdateService(downloader);

        await service.DownloadAndVerifyAsync("https://example.invalid/Setup.exe", destination, expectedHash);

        Assert.True(File.Exists(destination));
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_is_case_insensitive_about_the_expected_hash()
    {
        var content = Encoding.UTF8.GetBytes("pretend this is an installer");
        var expectedHash = Sha256Hex(content).ToUpperInvariant();
        var destination = Path.Combine(_tempRoot, "Setup.exe");
        var downloader = new FakeHttpDownloader { OnDownload = (_, path) => { File.WriteAllBytes(path, content); return Task.CompletedTask; } };
        var service = new SelfUpdateService(downloader);

        await service.DownloadAndVerifyAsync("https://example.invalid/Setup.exe", destination, expectedHash);

        Assert.True(File.Exists(destination));
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_deletes_the_file_and_throws_when_hash_does_not_match()
    {
        var destination = Path.Combine(_tempRoot, "Setup.exe");
        var downloader = new FakeHttpDownloader { OnDownload = (_, path) => { File.WriteAllBytes(path, Encoding.UTF8.GetBytes("tampered content")); return Task.CompletedTask; } };
        var service = new SelfUpdateService(downloader);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DownloadAndVerifyAsync("https://example.invalid/Setup.exe", destination, expectedSha256: new string('0', 64)));

        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void IsRunningFromInstalledLocation_true_when_an_uninstaller_is_present()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "unins000.exe"), "x");

        Assert.True(SelfUpdateService.IsRunningFromInstalledLocation(_tempRoot));
    }

    [Fact]
    public void IsRunningFromInstalledLocation_false_for_a_plain_extracted_folder()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "WinIsoOptimizer.exe"), "x");

        Assert.False(SelfUpdateService.IsRunningFromInstalledLocation(_tempRoot));
    }

    [Fact]
    public void IsRunningFromInstalledLocation_false_for_a_nonexistent_folder()
    {
        Assert.False(SelfUpdateService.IsRunningFromInstalledLocation(Path.Combine(_tempRoot, "does-not-exist")));
    }
}
