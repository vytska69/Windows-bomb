using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Processes;
using WinIsoOptimizer.Core.Setup;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class AdkDeploymentToolsInstallerTests : IDisposable
{
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("wioo-adk-test-").FullName;

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    [Fact]
    public void IsOscdimgAvailable_reflects_whether_the_file_exists()
    {
        var missingPath = new ExternalToolPaths { Oscdimg = Path.Combine(_tempRoot, "does-not-exist.exe") };
        Assert.False(AdkDeploymentToolsInstaller.IsOscdimgAvailable(missingPath));

        var presentFile = Path.Combine(_tempRoot, "oscdimg.exe");
        File.WriteAllText(presentFile, "x");
        var presentPath = new ExternalToolPaths { Oscdimg = presentFile };
        Assert.True(AdkDeploymentToolsInstaller.IsOscdimgAvailable(presentPath));
    }

    [Fact]
    public async Task SilentInstallDeploymentToolsAsync_uses_documented_quiet_deploymenttools_arguments()
    {
        var runner = new FakeProcessRunner();
        var installer = new AdkDeploymentToolsInstaller(runner);

        await installer.SilentInstallDeploymentToolsAsync(@"C:\Downloads\adksetup.exe");

        var request = Assert.Single(runner.Requests);
        Assert.Equal(@"C:\Downloads\adksetup.exe", request.FileName);
        Assert.Equal(new[] { "/quiet", "/features", "OptionId.DeploymentTools", "/ceip", "off", "/norestart" }, request.Arguments);
    }

    [Fact]
    public async Task SilentInstallDeploymentToolsAsync_throws_on_nonzero_exit_code()
    {
        var runner = new FakeProcessRunner { DefaultResult = new ProcessResult(1603, "", "Fatal error during installation") };
        var installer = new AdkDeploymentToolsInstaller(runner);

        await Assert.ThrowsAsync<ExternalToolException>(() => installer.SilentInstallDeploymentToolsAsync("adksetup.exe"));
    }

    [Fact]
    public async Task InstallAndVerifyAsync_returns_true_when_oscdimg_appears_after_install()
    {
        var runner = new FakeProcessRunner();
        var oscdimgPath = Path.Combine(_tempRoot, "oscdimg.exe");
        runner.Responders["adksetup.exe"] = _ =>
        {
            // Simulate the real installer actually placing oscdimg.exe at the expected path.
            File.WriteAllText(oscdimgPath, "x");
            return new ProcessResult(0, "", "");
        };
        var installer = new AdkDeploymentToolsInstaller(runner);
        var tools = new ExternalToolPaths { Oscdimg = oscdimgPath };

        var found = await installer.InstallAndVerifyAsync("adksetup.exe", tools);

        Assert.True(found);
    }

    [Fact]
    public async Task InstallAndVerifyAsync_returns_false_when_oscdimg_still_missing_after_install()
    {
        var runner = new FakeProcessRunner(); // "succeeds" but never creates the file — e.g. installed to a custom path
        var installer = new AdkDeploymentToolsInstaller(runner);
        var tools = new ExternalToolPaths { Oscdimg = Path.Combine(_tempRoot, "oscdimg.exe") };

        var found = await installer.InstallAndVerifyAsync("adksetup.exe", tools);

        Assert.False(found);
    }

    [Fact]
    public async Task DownloadInstallerAsync_delegates_to_the_downloader_with_the_given_url()
    {
        var downloader = new FakeHttpDownloader();
        var installer = new AdkDeploymentToolsInstaller(new FakeProcessRunner(), downloader);

        await installer.DownloadInstallerAsync("https://example.invalid/adksetup.exe", @"C:\Downloads\adksetup.exe");

        var request = Assert.Single(downloader.Requests);
        Assert.Equal("https://example.invalid/adksetup.exe", request.Url);
        Assert.Equal(@"C:\Downloads\adksetup.exe", request.DestinationPath);
    }
}
