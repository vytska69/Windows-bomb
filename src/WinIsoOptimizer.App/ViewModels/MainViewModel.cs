using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using WinIsoOptimizer.Core.Drivers;
using WinIsoOptimizer.Core.Imaging;
using WinIsoOptimizer.Core.Jobs;
using WinIsoOptimizer.Core.LegacyBoot;
using WinIsoOptimizer.Core.Processes;
using WinIsoOptimizer.Core.Setup;
using WinIsoOptimizer.Core.Telemetry;

namespace WinIsoOptimizer.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IsoOptimizationJob _job;
    private readonly ExternalToolPaths _toolPaths = new();
    private CancellationTokenSource? _runCancellation;
    private Task _appLoadTask = Task.CompletedTask;

    public MainViewModel()
    {
        _job = new IsoOptimizationJob(new SystemProcessRunner(), _toolPaths);

        WorkingDirectory = Path.Combine(Path.GetTempPath(), "WinIsoOptimizer");
        OscdimgPath = _toolPaths.Oscdimg; // setter also calls RefreshOscdimgStatus()
        AdkSetupExePath = Path.Combine(Path.GetTempPath(), "WinIsoOptimizer", "adksetup.exe");

        LoadSourceCommand = new RelayCommand(LoadSourceAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(SourceIsoPath));
        ExportDriversCommand = new RelayCommand(ExportDriversAsync, () => !IsBusy);
        RunCommand = new RelayCommand(RunAsync, () => !IsBusy && HasLoadedSource && !string.IsNullOrWhiteSpace(OutputIsoPath));
        CancelCommand = new RelayCommand(() => _runCancellation?.Cancel(), () => IsBusy);
        OpenAdkDownloadPageCommand = new RelayCommand(OpenAdkDownloadPage);
        InstallAdkDeploymentToolsCommand = new RelayCommand(InstallAdkDeploymentToolsAsync, () => !IsBusy && File.Exists(AdkSetupExePath));
    }

    public RelayCommand LoadSourceCommand { get; }
    public RelayCommand ExportDriversCommand { get; }
    public RelayCommand RunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenAdkDownloadPageCommand { get; }
    public RelayCommand InstallAdkDeploymentToolsCommand { get; }

    public ObservableCollection<string> LogMessages { get; } = new();
    public ObservableCollection<WimImageInfo> Editions { get; } = new();
    public ObservableCollection<AppPackageOption> AvailableApps { get; } = new();
    public ObservableCollection<ExportedDriverInfo> ExportedDrivers { get; } = new();

    private string _sourceIsoPath = string.Empty;
    public string SourceIsoPath
    {
        get => _sourceIsoPath;
        set => SetField(ref _sourceIsoPath, value);
    }

    private string _workingDirectory = string.Empty;
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetField(ref _workingDirectory, value);
    }

    private string _outputIsoPath = string.Empty;
    public string OutputIsoPath
    {
        get => _outputIsoPath;
        set => SetField(ref _outputIsoPath, value);
    }

    private string _volumeLabel = "CUSTOM_WIN";
    public string VolumeLabel
    {
        get => _volumeLabel;
        set => SetField(ref _volumeLabel, value);
    }

    private string _oscdimgPath = string.Empty;
    public string OscdimgPath
    {
        get => _oscdimgPath;
        set
        {
            if (SetField(ref _oscdimgPath, value))
            {
                // ExternalToolPaths is shared (by reference) with every Core service the running job
                // uses, and each of them reads .Oscdimg fresh at the point of use — so updating it here
                // is what actually makes a browsed-to path take effect, not just the display text.
                _toolPaths.Oscdimg = value;
                RefreshOscdimgStatus();
            }
        }
    }

    private string _adkSetupExePath = string.Empty;
    public string AdkSetupExePath
    {
        get => _adkSetupExePath;
        set => SetField(ref _adkSetupExePath, value);
    }

    private string _oscdimgStatusText = string.Empty;
    public string OscdimgStatusText
    {
        get => _oscdimgStatusText;
        set => SetField(ref _oscdimgStatusText, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    private bool _hasLoadedSource;
    public bool HasLoadedSource
    {
        get => _hasLoadedSource;
        set => SetField(ref _hasLoadedSource, value);
    }

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetField(ref _progressPercent, value);
    }

    private string _statusMessage = "Pasirinkite Windows ISO failą, tada spauskite \"Įkelti\".";
    /// <summary>The single most recent status line. Bound to a control with
    /// AutomationProperties.LiveSetting="Polite" so screen readers announce each update as it happens.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private WimImageInfo? _selectedEdition;
    public WimImageInfo? SelectedEdition
    {
        get => _selectedEdition;
        set
        {
            if (SetField(ref _selectedEdition, value) && value is not null)
            {
                _appLoadTask = LoadAppsForSelectedEditionAsync();
            }
        }
    }

    private string _legacyUefiBootStatus = string.Empty;
    public string LegacyUefiBootStatus
    {
        get => _legacyUefiBootStatus;
        set => SetField(ref _legacyUefiBootStatus, value);
    }

    // --- Telemetry / optimization toggles ---
    private bool _applyPrivacyRegistryTweaks = true;
    public bool ApplyPrivacyRegistryTweaks { get => _applyPrivacyRegistryTweaks; set => SetField(ref _applyPrivacyRegistryTweaks, value); }

    private bool _disableTelemetryServices = true;
    public bool DisableTelemetryServices { get => _disableTelemetryServices; set => SetField(ref _disableTelemetryServices, value); }

    private bool _disableTelemetryScheduledTasks = true;
    public bool DisableTelemetryScheduledTasks { get => _disableTelemetryScheduledTasks; set => SetField(ref _disableTelemetryScheduledTasks, value); }

    private bool _removeOneDriveSetup;
    public bool RemoveOneDriveSetup { get => _removeOneDriveSetup; set => SetField(ref _removeOneDriveSetup, value); }

    private bool _componentStoreCleanup = true;
    public bool ComponentStoreCleanup { get => _componentStoreCleanup; set => SetField(ref _componentStoreCleanup, value); }

    private bool _componentStoreResetBase;
    public bool ComponentStoreResetBase { get => _componentStoreResetBase; set => SetField(ref _componentStoreResetBase, value); }

    // --- Drivers ---
    private string _driverExportFolder = Path.Combine(Path.GetTempPath(), "WinIsoOptimizer", "drivers");
    public string DriverExportFolder { get => _driverExportFolder; set => SetField(ref _driverExportFolder, value); }

    private bool _injectDrivers;
    public bool InjectDrivers { get => _injectDrivers; set => SetField(ref _injectDrivers, value); }

    private bool _alsoInjectDriversIntoBootWim;
    public bool AlsoInjectDriversIntoBootWim { get => _alsoInjectDriversIntoBootWim; set => SetField(ref _alsoInjectDriversIntoBootWim, value); }

    private bool _forceUnsignedDrivers;
    public bool ForceUnsignedDrivers { get => _forceUnsignedDrivers; set => SetField(ref _forceUnsignedDrivers, value); }

    // --- Unattend / local account ---
    private bool _generateUnattend;
    public bool GenerateUnattend { get => _generateUnattend; set => SetField(ref _generateUnattend, value); }

    private string _localAccountName = "User";
    public string LocalAccountName { get => _localAccountName; set => SetField(ref _localAccountName, value); }

    private string _localAccountPassword = string.Empty;
    public string LocalAccountPassword { get => _localAccountPassword; set => SetField(ref _localAccountPassword, value); }

    private bool _applyLegacyUefiBootFix = true;
    public bool ApplyLegacyUefiBootFix { get => _applyLegacyUefiBootFix; set => SetField(ref _applyLegacyUefiBootFix, value); }

    private string ExtractedFolder => Path.Combine(WorkingDirectory, "extracted");

    private void RefreshOscdimgStatus()
    {
        OscdimgStatusText = AdkDeploymentToolsInstaller.IsOscdimgAvailable(_toolPaths)
            ? $"Rasta: {_toolPaths.Oscdimg}"
            : $"Nerasta: {_toolPaths.Oscdimg} — atsisiųskite Windows ADK ir įdiekite \"Deployment Tools\" komponentą (žr. žemiau).";
    }

    private static void OpenAdkDownloadPage()
    {
        // Opens the user's default browser at Microsoft's own, stable ADK download page. Intentionally
        // not a direct binary download link — Microsoft's direct link is versioned per ADK release and
        // changes over time, so hardcoding one here would eventually point at a stale/broken URL.
        Process.Start(new ProcessStartInfo(AdkDeploymentToolsInstaller.OfficialAdkDownloadPageUrl) { UseShellExecute = true });
    }

    private async Task InstallAdkDeploymentToolsAsync()
    {
        IsBusy = true;
        try
        {
            Log("Diegiamas Windows ADK \"Deployment Tools\" komponentas (tyliai, be perkrovimo)...");
            var found = await _job.AdkInstaller.InstallAndVerifyAsync(AdkSetupExePath, _toolPaths, new Progress<string>(Log)).ConfigureAwait(true);
            RefreshOscdimgStatus();
            Log(found
                ? "oscdimg.exe sėkmingai rastas — ISO kūrimas dabar galimas."
                : "Diegimas baigtas, bet oscdimg.exe vis tiek nerastas numatytoje vietoje — nurodykite jo kelią rankiniu būdu \"Kūrimas\" skirtuke.");
        }
        catch (Exception ex)
        {
            Log($"Klaida diegiant ADK Deployment Tools: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Log(string message)
    {
        LogMessages.Add(message);
        StatusMessage = message;
    }

    private IProgress<OptimizationProgress> MakeProgress() => new Progress<OptimizationProgress>(p =>
    {
        Log(p.Message);
        if (p.PercentComplete is { } percent) ProgressPercent = percent;
    });

    private async Task LoadSourceAsync()
    {
        IsBusy = true;
        HasLoadedSource = false;
        Editions.Clear();
        AvailableApps.Clear();
        LegacyUefiBootStatus = string.Empty;
        try
        {
            var progress = MakeProgress();
            var wimPath = await _job.PrepareSourceAsync(SourceIsoPath, WorkingDirectory, alreadyExtracted: false, progress).ConfigureAwait(true);

            var editions = await _job.Inspection.ListEditionsAsync(wimPath, new Progress<string>(Log)).ConfigureAwait(true);
            foreach (var edition in editions) Editions.Add(edition);
            SelectedEdition = Editions.FirstOrDefault();
            await _appLoadTask.ConfigureAwait(true);

            var assessment = LegacyUefiBootInjector.Assess(ExtractedFolder);
            LegacyUefiBootStatus = assessment.Explanation;

            HasLoadedSource = true;
            Log("ISO įkeltas. Pasirinkite leidimą, telemetrijos/optimizavimo parinktis ir programėles šalinimui.");
        }
        catch (Exception ex)
        {
            Log($"Klaida įkeliant ISO: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAppsForSelectedEditionAsync()
    {
        var selectedEdition = SelectedEdition;
        if (selectedEdition is null) return;
        AvailableApps.Clear();
        try
        {
            var wimPath = Path.Combine(ExtractedFolder, "sources", "install.wim");
            var scratchDir = Path.Combine(WorkingDirectory, "mount", $"inspect-{selectedEdition.Index}");
            var apps = await _job.Inspection.ListProvisionedAppsAsync(wimPath, selectedEdition.Index, scratchDir, new Progress<string>(Log)).ConfigureAwait(true);

            foreach (var app in apps)
            {
                var catalogEntry = TelemetryDebloatProfile.RemovableAppCatalog
                    .FirstOrDefault(c => app.PackageName.StartsWith(c.PackageNamePrefix, StringComparison.OrdinalIgnoreCase));
                var friendlyName = catalogEntry.FriendlyName ?? app.DisplayName;
                AvailableApps.Add(new AppPackageOption(app, friendlyName) { IsSelected = catalogEntry.FriendlyName is not null });
            }
        }
        catch (Exception ex)
        {
            Log($"Nepavyko nuskaityti programėlių sąrašo: {ex.Message}");
        }
    }

    private async Task ExportDriversAsync()
    {
        IsBusy = true;
        ExportedDrivers.Clear();
        try
        {
            Log("Eksportuojami veikiančios sistemos draiveriai...");
            var drivers = await _job.Drivers.ExportFromRunningSystemAsync(DriverExportFolder, new Progress<string>(Log)).ConfigureAwait(true);
            foreach (var driver in drivers) ExportedDrivers.Add(driver);
            Log($"Eksportuota draiverių: {drivers.Count}.");
        }
        catch (Exception ex)
        {
            Log($"Klaida eksportuojant draiverius: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunAsync()
    {
        IsBusy = true;
        ProgressPercent = 0;
        _runCancellation = new CancellationTokenSource();
        try
        {
            var selectedApps = AvailableApps.Where(a => a.IsSelected).Select(a => a.Package).ToList();

            var request = new IsoOptimizationRequest
            {
                SourceIsoPath = SourceIsoPath,
                OutputIsoPath = OutputIsoPath,
                WorkingDirectory = WorkingDirectory,
                VolumeLabel = VolumeLabel,
                SourceAlreadyExtracted = HasLoadedSource,
                EditionIndices = SelectedEdition is null ? null : new[] { SelectedEdition.Index },
                AppsToRemove = selectedApps,
                Optimizations = new OptimizationOptions
                {
                    ApplyPrivacyRegistryTweaks = ApplyPrivacyRegistryTweaks,
                    DisableTelemetryServices = DisableTelemetryServices,
                    DisableTelemetryScheduledTasks = DisableTelemetryScheduledTasks,
                    RemoveOneDriveSetup = RemoveOneDriveSetup,
                    ComponentStoreCleanup = ComponentStoreCleanup,
                    ComponentStoreResetBase = ComponentStoreResetBase,
                },
                DriverFolderToInject = InjectDrivers && Directory.Exists(DriverExportFolder) ? DriverExportFolder : null,
                AlsoInjectDriversIntoBootWim = AlsoInjectDriversIntoBootWim,
                ForceUnsignedDrivers = ForceUnsignedDrivers,
                Unattend = GenerateUnattend
                    ? new UnattendOptions { LocalAccountName = LocalAccountName, LocalAccountPassword = LocalAccountPassword }
                    : null,
                ApplyLegacyUefiBootFixIfApplicable = ApplyLegacyUefiBootFix,
            };

            await _job.RunAsync(request, MakeProgress(), _runCancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Log("Atšaukta.");
        }
        catch (Exception ex)
        {
            Log($"Klaida: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _runCancellation = null;
        }
    }
}
