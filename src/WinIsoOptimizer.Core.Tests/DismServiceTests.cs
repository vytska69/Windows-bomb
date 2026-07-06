using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Processes;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class DismServiceTests
{
    private const string SampleWimInfoOutput = """
        Deployment Image Servicing and Management tool
        Version: 10.0.19041.844

        Details for image : D:\sources\install.wim

        Index : 1
        Name : Windows 10 Home
        Description : Windows 10 Home
        Size : 15,406,489,038 bytes

        Index : 2
        Name : Windows 10 Pro
        Description : Windows 10 Pro
        Size : 15,507,201,004 bytes

        The operation completed successfully.
        """;

    private const string SampleProvisionedAppxOutput = """
        Deployment Image Servicing and Management tool
        Version: 10.0.19041.844

        Image File : D:\sources\install.wim
        Image Index : 1

        DisplayName : Microsoft.BingWeather
        Version : 4.53.31411.0
        Architecture : neutral
        ResourceId : ~
        PackageName : Microsoft.BingWeather_4.53.31411.0_neutral_~_8wekyb3d8bbwe

        DisplayName : Microsoft.XboxApp
        Version : 12.44.15001.0
        Architecture : neutral
        ResourceId : ~
        PackageName : Microsoft.XboxApp_12.44.15001.0_neutral_~_8wekyb3d8bbwe

        The operation completed successfully.
        """;

    [Fact]
    public void ParseWimInfo_extracts_every_index()
    {
        var images = DismService.ParseWimInfo(SampleWimInfoOutput);

        Assert.Equal(2, images.Count);
        Assert.Equal(new WimImageInfo(1, "Windows 10 Home", "Windows 10 Home", 15_406_489_038), images[0]);
        Assert.Equal(new WimImageInfo(2, "Windows 10 Pro", "Windows 10 Pro", 15_507_201_004), images[1]);
    }

    [Fact]
    public void ParseProvisionedAppxPackages_extracts_every_package()
    {
        var packages = DismService.ParseProvisionedAppxPackages(SampleProvisionedAppxOutput);

        Assert.Equal(2, packages.Count);
        Assert.Equal("Microsoft.BingWeather", packages[0].DisplayName);
        Assert.Equal("Microsoft.BingWeather_4.53.31411.0_neutral_~_8wekyb3d8bbwe", packages[0].PackageName);
        Assert.Equal("Microsoft.XboxApp", packages[1].DisplayName);
    }

    [Fact]
    public async Task MountImageAsync_builds_expected_dism_arguments()
    {
        var runner = new FakeProcessRunner();
        var service = new DismService(runner);

        await service.MountImageAsync(@"C:\iso\sources\install.wim", 3, @"C:\mount", readOnly: true);

        var request = Assert.Single(runner.Requests);
        Assert.Equal("dism.exe", request.FileName);
        Assert.Equal(new[]
        {
            "/Mount-Image",
            @"/ImageFile:C:\iso\sources\install.wim",
            "/Index:3",
            @"/MountDir:C:\mount",
            "/ReadOnly",
        }, request.Arguments);
    }

    [Fact]
    public async Task RemoveProvisionedAppxPackageAsync_builds_expected_dism_arguments()
    {
        var runner = new FakeProcessRunner();
        var service = new DismService(runner);

        await service.RemoveProvisionedAppxPackageAsync(@"C:\mount", "Microsoft.BingWeather_4.53.31411.0_neutral_~_8wekyb3d8bbwe");

        var request = Assert.Single(runner.Requests);
        Assert.Equal(new[]
        {
            @"/Image:C:\mount",
            "/Remove-ProvisionedAppxPackage",
            "/PackageName:Microsoft.BingWeather_4.53.31411.0_neutral_~_8wekyb3d8bbwe",
        }, request.Arguments);
    }

    [Fact]
    public async Task AddDriverAsync_omits_optional_flags_when_disabled()
    {
        var runner = new FakeProcessRunner();
        var service = new DismService(runner);

        await service.AddDriverAsync(@"C:\mount", @"C:\drivers", recurse: false, forceUnsigned: false);

        var request = Assert.Single(runner.Requests);
        Assert.Equal(new[] { @"/Image:C:\mount", "/Add-Driver", @"/Driver:C:\drivers" }, request.Arguments);
    }

    [Fact]
    public async Task AddDriverAsync_includes_recurse_and_forceunsigned_when_enabled()
    {
        var runner = new FakeProcessRunner();
        var service = new DismService(runner);

        await service.AddDriverAsync(@"C:\mount", @"C:\drivers", recurse: true, forceUnsigned: true);

        var request = Assert.Single(runner.Requests);
        Assert.Equal(new[] { @"/Image:C:\mount", "/Add-Driver", @"/Driver:C:\drivers", "/Recurse", "/ForceUnsigned" }, request.Arguments);
    }

    [Fact]
    public async Task CleanupImageAsync_adds_resetbase_only_when_requested()
    {
        var runner = new FakeProcessRunner();
        var service = new DismService(runner);

        await service.CleanupImageAsync(@"C:\mount", resetBase: true);

        var request = Assert.Single(runner.Requests);
        Assert.Contains("/ResetBase", request.Arguments);
    }

    [Fact]
    public async Task Failing_dism_invocation_throws_ExternalToolException()
    {
        var runner = new FakeProcessRunner
        {
            DefaultResult = new(ExitCode: 87, StandardOutput: "", StandardError: "Error: 87 The parameter is incorrect."),
        };
        var service = new DismService(runner);

        var ex = await Assert.ThrowsAsync<ExternalToolException>(() => service.MountImageAsync("x", 1, "y"));
        Assert.Contains("87", ex.Message);
    }
}
