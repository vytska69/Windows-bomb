using WinIsoOptimizer.Core.Imaging;

namespace WinIsoOptimizer.Core.Telemetry;

/// <summary>
/// Curated, source-cited catalogue of Windows 10/11 telemetry/advertising registry policies,
/// telemetry-related scheduled tasks, and commonly-debloated provisioned apps.
///
/// This intentionally does NOT include Microsoft Edge/WebView2 removal: Edge's runtime is a
/// dependency of parts of Settings and other in-box apps on current builds, deleting it offline
/// is unsupported and prone to leaving a broken image, so it is out of scope rather than offered
/// as a half-working, image-breaking option.
/// </summary>
public static class TelemetryDebloatProfile
{
    /// <summary>
    /// Registry values under the SOFTWARE hive that reduce telemetry/advertising/"consumer
    /// experience" behavior. Applied via <see cref="OfflineRegistryService.ApplySoftwareHiveTweaksAsync"/>.
    /// </summary>
    public static IReadOnlyList<RegistryTweak> PrivacyRegistryTweaksSoftwareHive { get; } = new[]
    {
        // Diagnostic data level. 0 = "Security" (Enterprise/Education/IoT only; Home/Pro silently
        // clamp to 1 = "Basic"). Setting 0 is still correct here: it's the most restrictive value
        // and Windows itself enforces the real floor per edition.
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", RegistryValueKind.Dword, "0"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", RegistryValueKind.Dword, "1"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DisableOneSettingsDownloads", RegistryValueKind.Dword, "1"),

        // Advertising ID used to personalize ads across apps.
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", RegistryValueKind.Dword, "1"),

        // Start menu / lock screen suggested apps, tips, Spotlight ads, "consumer features" that
        // silently reinstall sponsored apps.
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", RegistryValueKind.Dword, "1"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableCloudOptimizedContent", RegistryValueKind.Dword, "1"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerAccountStateContent", RegistryValueKind.Dword, "1"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", RegistryValueKind.Dword, "1"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightFeatures", RegistryValueKind.Dword, "1"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", RegistryValueKind.Dword, "1"),

        // Cortana / web search in Start menu.
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", RegistryValueKind.Dword, "0"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", RegistryValueKind.Dword, "0"),
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", RegistryValueKind.Dword, "1"),

        // "How was your experience" feedback prompts.
        new RegistryTweak(@"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", RegistryValueKind.Dword, "0"),
        new RegistryTweak(@"SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds", RegistryValueKind.Dword, "0"),

        // Windows 11 Copilot entry point.
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", RegistryValueKind.Dword, "1"),

        // OneDrive auto-setup/sync nag (the setup binary itself is removed separately, see
        // OptimizationOptions.RemoveOneDriveSetup — this just stops it being offered again).
        new RegistryTweak(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", RegistryValueKind.Dword, "1"),
    };

    /// <summary>Registry values under the SYSTEM hive: disables the two services dedicated to telemetry upload.</summary>
    public static IReadOnlyList<RegistryTweak> TelemetryServiceDisableTweaksSystemHive { get; } = new[]
    {
        // Service Start values: 4 = Disabled, 3 = Manual, 2 = Automatic.
        new RegistryTweak(@"SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", RegistryValueKind.Dword, "4"),
        new RegistryTweak(@"SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", RegistryValueKind.Dword, "4"),
    };

    /// <summary>
    /// Scheduled task definition files to delete from the offline image, relative to
    /// "Windows\System32\Tasks\". These are the well-known telemetry/CEIP/compat-appraiser tasks;
    /// removing the XML definition offline is equivalent to disabling the task, since Windows Setup
    /// (re)builds its task cache from these files during specialize.
    /// </summary>
    public static IReadOnlyList<string> TelemetryScheduledTaskRelativePaths { get; } = new[]
    {
        @"Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
        @"Microsoft\Windows\Application Experience\ProgramDataUpdater",
        @"Microsoft\Windows\Application Experience\PcaPatchDbTask",
        @"Microsoft\Windows\Application Experience\MareBackup",
        @"Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
        @"Microsoft\Windows\Customer Experience Improvement Program\KernelCeipTask",
        @"Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
        @"Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
        @"Microsoft\Windows\Feedback\Siuf\DmClient",
        @"Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
        @"Microsoft\Windows\Windows Error Reporting\QueueReporting",
    };

    /// <summary>
    /// Provisioned AppX packages commonly considered removable bloat, matched against the
    /// <see cref="ProvisionedAppxPackage.PackageName"/> prefix (the part before the first
    /// underscore, which is stable across versions/architectures). Presented to the user as an
    /// opt-in checklist — nothing here is removed unless explicitly selected.
    /// </summary>
    public static IReadOnlyList<(string PackageNamePrefix, string FriendlyName)> RemovableAppCatalog { get; } = new[]
    {
        ("Microsoft.549981C3F5F10", "Cortana"),
        ("Microsoft.BingNews", "News"),
        ("Microsoft.BingSearch", "Bing Search (Win11)"),
        ("Microsoft.BingWeather", "Weather"),
        ("Microsoft.GamingApp", "Xbox App"),
        ("Microsoft.GetHelp", "Get Help"),
        ("Microsoft.Getstarted", "Tips"),
        ("Microsoft.Microsoft3DViewer", "3D Viewer"),
        ("Microsoft.MicrosoftOfficeHub", "Office Hub / \"Microsoft 365\" promo"),
        ("Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection"),
        ("Microsoft.MixedReality.Portal", "Mixed Reality Portal"),
        ("Microsoft.MSPaint", "Paint 3D"),
        ("Microsoft.OneConnect", "Mobile Plans"),
        ("Microsoft.OutlookForWindows", "New Outlook"),
        ("Microsoft.People", "People"),
        ("Microsoft.PowerAutomateDesktop", "Power Automate Desktop"),
        ("Microsoft.Print3D", "Print 3D"),
        ("Microsoft.SkypeApp", "Skype"),
        ("Microsoft.Todos", "Microsoft To Do"),
        ("Microsoft.WindowsFeedbackHub", "Feedback Hub"),
        ("Microsoft.WindowsMaps", "Maps"),
        ("Microsoft.WindowsSoundRecorder", "Voice Recorder"),
        ("Microsoft.Xbox.TCUI", "Xbox TCUI"),
        ("Microsoft.XboxApp", "Xbox (legacy)"),
        ("Microsoft.XboxGameOverlay", "Xbox Game Overlay"),
        ("Microsoft.XboxGamingOverlay", "Xbox Gaming Overlay"),
        ("Microsoft.XboxIdentityProvider", "Xbox Identity Provider"),
        ("Microsoft.XboxSpeechToTextOverlay", "Xbox Speech To Text Overlay"),
        ("Microsoft.YourPhone", "Phone Link"),
        ("Microsoft.ZuneMusic", "Media Player / Groove Music"),
        ("Microsoft.ZuneVideo", "Movies & TV"),
        ("MicrosoftTeams", "Teams (consumer, Win11 in-box)"),
        ("Clipchamp.Clipchamp", "Clipchamp"),
    };
}
