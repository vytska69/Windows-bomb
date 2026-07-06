using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Processes;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class IsoServiceTests : IDisposable
{
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("wioo-iso-test-").FullName;

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private string CreateDummyOscdimg()
    {
        var path = Path.Combine(_tempRoot, "oscdimg.exe");
        File.WriteAllText(path, "not a real binary, just needs to exist");
        return path;
    }

    [Fact]
    public async Task BuildBootableIsoAsync_uses_dual_bootdata_when_both_boot_files_exist()
    {
        var sourceFolder = Path.Combine(_tempRoot, "source");
        var biosBootFile = Path.Combine(sourceFolder, "boot", "etfsboot.com");
        var uefiBootFile = Path.Combine(sourceFolder, "efi", "microsoft", "boot", "efisys.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(biosBootFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(uefiBootFile)!);
        File.WriteAllText(biosBootFile, "x");
        File.WriteAllText(uefiBootFile, "x");

        var runner = new FakeProcessRunner();
        var service = new IsoService(runner, new ExternalToolPaths { Oscdimg = CreateDummyOscdimg() });

        var outputIso = Path.Combine(_tempRoot, "out.iso");
        await service.BuildBootableIsoAsync(sourceFolder, outputIso, "MY_LABEL");

        var request = Assert.Single(runner.Requests);
        Assert.Contains("-m", request.Arguments);
        Assert.Contains("-lMY_LABEL", request.Arguments);
        Assert.Contains(request.Arguments, a => a.StartsWith("-bootdata:2#p0,e,b") && a.Contains("#pEF,e,b"));
        Assert.Equal(sourceFolder, request.Arguments[^2]);
        Assert.Equal(outputIso, request.Arguments[^1]);
    }

    [Fact]
    public async Task BuildBootableIsoAsync_falls_back_to_bios_only_bootdata_when_uefi_missing()
    {
        var sourceFolder = Path.Combine(_tempRoot, "source-bios-only");
        var biosBootFile = Path.Combine(sourceFolder, "boot", "etfsboot.com");
        Directory.CreateDirectory(Path.GetDirectoryName(biosBootFile)!);
        File.WriteAllText(biosBootFile, "x");

        var runner = new FakeProcessRunner();
        var service = new IsoService(runner, new ExternalToolPaths { Oscdimg = CreateDummyOscdimg() });

        await service.BuildBootableIsoAsync(sourceFolder, Path.Combine(_tempRoot, "out.iso"), "LBL");

        var request = Assert.Single(runner.Requests);
        Assert.Contains(request.Arguments, a => a == $"-bootdata:1#p0,e,b{biosBootFile}");
    }

    [Fact]
    public async Task BuildBootableIsoAsync_throws_when_neither_boot_file_exists()
    {
        var sourceFolder = Path.Combine(_tempRoot, "not-windows-media");
        Directory.CreateDirectory(sourceFolder);

        var runner = new FakeProcessRunner();
        var service = new IsoService(runner, new ExternalToolPaths { Oscdimg = CreateDummyOscdimg() });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BuildBootableIsoAsync(sourceFolder, Path.Combine(_tempRoot, "out.iso"), "LBL"));
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task BuildBootableIsoAsync_throws_when_oscdimg_missing()
    {
        var sourceFolder = Path.Combine(_tempRoot, "source2");
        var biosBootFile = Path.Combine(sourceFolder, "boot", "etfsboot.com");
        Directory.CreateDirectory(Path.GetDirectoryName(biosBootFile)!);
        File.WriteAllText(biosBootFile, "x");

        var runner = new FakeProcessRunner();
        var service = new IsoService(runner, new ExternalToolPaths { Oscdimg = Path.Combine(_tempRoot, "does-not-exist.exe") });

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.BuildBootableIsoAsync(sourceFolder, Path.Combine(_tempRoot, "out.iso"), "LBL"));
    }

    [Fact]
    public async Task ExtractAsync_mounts_copies_then_dismounts_even_if_copy_fails()
    {
        var runner = new FakeProcessRunner();
        runner.Responders["powershell.exe"] = request =>
            request.Arguments[^1].Contains("Get-Volume")
                ? new ProcessResult(0, "D", "")
                : new ProcessResult(0, "", "");
        runner.Responders["robocopy.exe"] = _ => new ProcessResult(16, "", "catastrophic error"); // >=8 means failure

        var service = new IsoService(runner);
        var destination = Path.Combine(_tempRoot, "extracted");

        await Assert.ThrowsAsync<ExternalToolException>(() => service.ExtractAsync(Path.Combine(_tempRoot, "fake.iso"), destination));

        Assert.Equal(4, runner.Requests.Count);
        Assert.Contains("Mount-DiskImage", runner.Requests[0].Arguments[^1]);
        Assert.Contains("Get-Volume", runner.Requests[1].Arguments[^1]);
        Assert.Equal("D:\\", runner.Requests[2].Arguments[0]);
        Assert.Contains("Dismount-DiskImage", runner.Requests[3].Arguments[^1]);
    }

    [Fact]
    public async Task ExtractAsync_dismounts_even_when_the_drive_letter_cannot_be_determined()
    {
        var runner = new FakeProcessRunner
        {
            DefaultResult = new ProcessResult(0, "", ""), // empty stdout: no drive letter
        };
        var service = new IsoService(runner);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExtractAsync(Path.Combine(_tempRoot, "fake.iso"), Path.Combine(_tempRoot, "extracted")));

        // Mounting itself already succeeded (it would have thrown ExternalToolException otherwise), so
        // the image must still be dismounted even though its drive letter couldn't be determined —
        // otherwise a failed extraction leaves a stale mounted image behind.
        Assert.Equal(3, runner.Requests.Count);
        Assert.Contains("Mount-DiskImage", runner.Requests[0].Arguments[^1]);
        Assert.Contains("Get-Volume", runner.Requests[1].Arguments[^1]);
        Assert.Contains("Dismount-DiskImage", runner.Requests[2].Arguments[^1]);
    }

    [Fact]
    public async Task ExtractAsync_does_not_attempt_to_dismount_when_mounting_itself_fails()
    {
        var runner = new FakeProcessRunner();
        runner.Responders["powershell.exe"] = request =>
            request.Arguments[^1].Contains("Mount-DiskImage") && !request.Arguments[^1].Contains("Get-Volume")
                ? new ProcessResult(1, "", "Mount-DiskImage : Access is denied. (Exception from HRESULT: 0x80070005 (E_ACCESSDENIED))")
                : new ProcessResult(0, "", "");

        var service = new IsoService(runner);

        var ex = await Assert.ThrowsAsync<ExternalToolException>(() =>
            service.ExtractAsync(Path.Combine(_tempRoot, "fake.iso"), Path.Combine(_tempRoot, "extracted")));

        Assert.Contains("Access is denied", ex.Message);
        // Nothing was actually mounted, so there is nothing to dismount and no copy should be attempted.
        Assert.Single(runner.Requests);
    }
}
