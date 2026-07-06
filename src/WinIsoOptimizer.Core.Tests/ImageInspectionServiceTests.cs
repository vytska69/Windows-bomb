using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Processes;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class ImageInspectionServiceTests
{
    [Fact]
    public async Task ListProvisionedAppsAsync_mounts_readonly_lists_then_discards()
    {
        var runner = new FakeProcessRunner();
        var service = new ImageInspectionService(new DismService(runner));

        await service.ListProvisionedAppsAsync("install.wim", 1, "/tmp/scratch");

        Assert.Equal(3, runner.Requests.Count);
        Assert.Equal("/Mount-Image", runner.Requests[0].Arguments[0]);
        Assert.Contains("/ReadOnly", runner.Requests[0].Arguments);
        Assert.Equal("/Get-ProvisionedAppxPackages", runner.Requests[1].Arguments[1]);
        Assert.Equal("/Unmount-Image", runner.Requests[2].Arguments[0]);
        Assert.Contains("/Discard", runner.Requests[2].Arguments);
    }

    [Fact]
    public async Task ListProvisionedAppsAsync_still_discards_mount_when_listing_fails()
    {
        var runner = new FakeProcessRunner();
        runner.Responders["dism.exe"] = request =>
            request.Arguments.Contains("/Get-ProvisionedAppxPackages")
                ? new ProcessResult(1, "", "boom")
                : new ProcessResult(0, "", "");
        var service = new ImageInspectionService(new DismService(runner));

        await Assert.ThrowsAsync<ExternalToolException>(() => service.ListProvisionedAppsAsync("install.wim", 1, "/tmp/scratch"));

        Assert.Equal(3, runner.Requests.Count);
        Assert.Equal("/Unmount-Image", runner.Requests[^1].Arguments[0]);
    }
}
