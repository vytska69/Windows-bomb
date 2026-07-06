using WinIsoOptimizer.Core.Drivers;
using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Processes;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class DriverServiceTests : IDisposable
{
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("wioo-driver-test-").FullName;

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    [Fact]
    public void EnumerateExportedDrivers_finds_inf_files_recursively()
    {
        var nicDriver = Path.Combine(_tempRoot, "nic", "e1d68x64.inf");
        var storageDriver = Path.Combine(_tempRoot, "storage", "nested", "iastorac.inf");
        Directory.CreateDirectory(Path.GetDirectoryName(nicDriver)!);
        Directory.CreateDirectory(Path.GetDirectoryName(storageDriver)!);
        File.WriteAllText(nicDriver, "; inf");
        File.WriteAllText(storageDriver, "; inf");

        var service = new DriverService(new DismService(new FakeProcessRunner()));
        var found = service.EnumerateExportedDrivers(_tempRoot);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, d => d.InfPath == nicDriver);
        Assert.Contains(found, d => d.InfPath == storageDriver);
    }

    [Fact]
    public void EnumerateExportedDrivers_returns_empty_for_missing_folder()
    {
        var service = new DriverService(new DismService(new FakeProcessRunner()));

        var found = service.EnumerateExportedDrivers(Path.Combine(_tempRoot, "does-not-exist"));

        Assert.Empty(found);
    }

    [Fact]
    public async Task ExportFromRunningSystemAsync_calls_dism_export_driver_and_then_enumerates()
    {
        var runner = new FakeProcessRunner();
        runner.Responders["dism.exe"] = _ =>
        {
            // Simulate dism actually having written a driver out, so enumeration afterwards finds it.
            var infPath = Path.Combine(_tempRoot, "vendor", "driver.inf");
            Directory.CreateDirectory(Path.GetDirectoryName(infPath)!);
            File.WriteAllText(infPath, "; inf");
            return new ProcessResult(0, "", "");
        };
        var service = new DriverService(new DismService(runner));

        var found = await service.ExportFromRunningSystemAsync(_tempRoot);

        var request = Assert.Single(runner.Requests);
        Assert.Equal(new[] { "/Online", "/Export-Driver", $"/Destination:{_tempRoot}" }, request.Arguments);
        Assert.Single(found);
    }
}
