using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Processes;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class OfflineRegistryServiceTests
{
    [Fact]
    public async Task ApplySoftwareHiveTweaksAsync_loads_edits_and_unloads_in_order()
    {
        var runner = new FakeProcessRunner();
        var service = new OfflineRegistryService(runner);
        var tweaks = new[]
        {
            new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", RegistryValueKind.Dword, "0"),
        };

        await service.ApplySoftwareHiveTweaksAsync(@"C:\mount", tweaks);

        Assert.Equal(3, runner.Requests.Count);
        Assert.Equal(new[] { "load", @"HKLM\WIOO_OFFLINE_SOFTWARE", Path.Combine(@"C:\mount", "Windows", "System32", "config", "SOFTWARE") }, runner.Requests[0].Arguments);
        Assert.Equal(new[]
        {
            "add", @"HKLM\WIOO_OFFLINE_SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            "/v", "AllowTelemetry", "/t", "REG_DWORD", "/d", "0", "/f",
        }, runner.Requests[1].Arguments);
        Assert.Equal(new[] { "unload", @"HKLM\WIOO_OFFLINE_SOFTWARE" }, runner.Requests[2].Arguments);
    }

    [Fact]
    public async Task Hive_is_unloaded_even_when_a_tweak_fails()
    {
        var runner = new FakeProcessRunner();
        runner.Responders["reg.exe"] = request =>
            request.Arguments[0] == "add"
                ? new ProcessResult(1, "", "ERROR: Access is denied.")
                : new ProcessResult(0, "", "");
        var service = new OfflineRegistryService(runner);
        var tweaks = new[] { new RegistryTweak(@"SOFTWARE\Foo", "Bar", RegistryValueKind.Dword, "1") };

        await Assert.ThrowsAsync<ExternalToolException>(() => service.ApplySoftwareHiveTweaksAsync(@"C:\mount", tweaks));

        // load, (failing) add, unload — the hive must never be left loaded even though "add" threw.
        Assert.Equal(3, runner.Requests.Count);
        Assert.Equal("unload", runner.Requests[^1].Arguments[0]);
    }

    [Fact]
    public async Task No_hive_operations_when_there_are_no_tweaks()
    {
        var runner = new FakeProcessRunner();
        var service = new OfflineRegistryService(runner);

        await service.ApplySoftwareHiveTweaksAsync(@"C:\mount", Array.Empty<RegistryTweak>());

        Assert.Empty(runner.Requests);
    }
}
