using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Telemetry;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class TelemetryDebloatServiceTests : IDisposable
{
    private readonly string _mountDir = Directory.CreateTempSubdirectory("wioo-debloat-test-").FullName;

    public void Dispose() => Directory.Delete(_mountDir, recursive: true);

    private (FakeProcessRunner Runner, TelemetryDebloatService Service) CreateService()
    {
        var runner = new FakeProcessRunner();
        var dism = new DismService(runner);
        var registry = new OfflineRegistryService(runner);
        return (runner, new TelemetryDebloatService(registry, dism));
    }

    [Fact]
    public async Task ApplyAsync_skips_everything_when_all_options_disabled()
    {
        var (runner, service) = CreateService();
        var options = new OptimizationOptions
        {
            ApplyPrivacyRegistryTweaks = false,
            DisableTelemetryServices = false,
            DisableTelemetryScheduledTasks = false,
            RemoveOneDriveSetup = false,
            ComponentStoreCleanup = false,
        };

        await service.ApplyAsync(_mountDir, options);

        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task ApplyAsync_deletes_known_telemetry_scheduled_task_files_when_present()
    {
        var (_, service) = CreateService();
        var taskPath = Path.Combine(_mountDir, "Windows", "System32", "Tasks",
            "Microsoft", "Windows", "Customer Experience Improvement Program", "Consolidator");
        Directory.CreateDirectory(Path.GetDirectoryName(taskPath)!);
        File.WriteAllText(taskPath, "<Task/>");

        var options = new OptimizationOptions
        {
            ApplyPrivacyRegistryTweaks = false,
            DisableTelemetryServices = false,
            DisableTelemetryScheduledTasks = true,
            ComponentStoreCleanup = false,
        };
        await service.ApplyAsync(_mountDir, options);

        Assert.False(File.Exists(taskPath));
    }

    [Fact]
    public async Task ApplyAsync_does_not_touch_scheduled_tasks_not_in_the_profile()
    {
        var (_, service) = CreateService();
        var unrelatedTask = Path.Combine(_mountDir, "Windows", "System32", "Tasks", "Microsoft", "Windows", "Defrag", "ScheduledDefrag");
        Directory.CreateDirectory(Path.GetDirectoryName(unrelatedTask)!);
        File.WriteAllText(unrelatedTask, "<Task/>");

        var options = new OptimizationOptions { ApplyPrivacyRegistryTweaks = false, DisableTelemetryServices = false, DisableTelemetryScheduledTasks = true, ComponentStoreCleanup = false };
        await service.ApplyAsync(_mountDir, options);

        Assert.True(File.Exists(unrelatedTask));
    }

    [Fact]
    public async Task ApplyAsync_removes_onedrive_setup_binaries_when_requested()
    {
        var (_, service) = CreateService();
        var oneDrivePath = Path.Combine(_mountDir, "Windows", "System32", "OneDriveSetup.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(oneDrivePath)!);
        File.WriteAllText(oneDrivePath, "x");

        var options = new OptimizationOptions
        {
            ApplyPrivacyRegistryTweaks = false,
            DisableTelemetryServices = false,
            DisableTelemetryScheduledTasks = false,
            RemoveOneDriveSetup = true,
            ComponentStoreCleanup = false,
        };
        await service.ApplyAsync(_mountDir, options);

        Assert.False(File.Exists(oneDrivePath));
    }

    [Fact]
    public async Task RemoveSelectedAppsAsync_calls_dism_for_each_requested_package()
    {
        var (runner, service) = CreateService();
        var packages = new[]
        {
            new ProvisionedAppxPackage("Weather", "Microsoft.BingWeather_1.0_neutral_~_8wekyb3d8bbwe"),
            new ProvisionedAppxPackage("Xbox", "Microsoft.XboxApp_1.0_neutral_~_8wekyb3d8bbwe"),
        };

        await service.RemoveSelectedAppsAsync(_mountDir, packages);

        Assert.Equal(2, runner.Requests.Count);
        Assert.Contains(runner.Requests, r => r.Arguments.Contains("/PackageName:Microsoft.BingWeather_1.0_neutral_~_8wekyb3d8bbwe"));
        Assert.Contains(runner.Requests, r => r.Arguments.Contains("/PackageName:Microsoft.XboxApp_1.0_neutral_~_8wekyb3d8bbwe"));
    }
}
