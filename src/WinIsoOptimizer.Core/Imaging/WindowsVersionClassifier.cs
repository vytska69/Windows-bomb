namespace WinIsoOptimizer.Core.Imaging;

public enum WindowsMajorVersion
{
    Unknown,
    WindowsXp,
    WindowsVista,
    Windows7,
    Windows8,
    Windows8_1,
    Windows10,
    Windows11,
}

/// <summary>
/// Classifies the marketing OS name from the NT kernel version DISM reports for a specific WIM index
/// (e.g. "10.0.19045", "6.1.7601"). This is the reliable way to identify the OS — human-readable
/// Name/Description strings vary by language/edition and, critically, can't tell Windows 10 from 11 at
/// all: both report NT version "10.0" and are distinguished only by build number (11 is 22000+). The
/// NT-major.minor -> marketing name mapping below (Vista=6.0, 7=6.1, 8=6.2, 8.1=6.3, XP=5.1/5.2) is
/// fixed, well-documented Microsoft versioning history, not a heuristic.
/// </summary>
public static class WindowsVersionClassifier
{
    public static WindowsMajorVersion Classify(string? ntVersion)
    {
        if (string.IsNullOrWhiteSpace(ntVersion)) return WindowsMajorVersion.Unknown;

        var parts = ntVersion.Split('.');
        if (parts.Length < 2) return WindowsMajorVersion.Unknown;
        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor)) return WindowsMajorVersion.Unknown;
        var build = parts.Length >= 3 && int.TryParse(parts[2], out var b) ? b : 0;

        return (major, minor) switch
        {
            (5, 1) or (5, 2) => WindowsMajorVersion.WindowsXp,
            (6, 0) => WindowsMajorVersion.WindowsVista,
            (6, 1) => WindowsMajorVersion.Windows7,
            (6, 2) => WindowsMajorVersion.Windows8,
            (6, 3) => WindowsMajorVersion.Windows8_1,
            (10, 0) when build >= 22000 => WindowsMajorVersion.Windows11,
            (10, 0) => WindowsMajorVersion.Windows10,
            _ => WindowsMajorVersion.Unknown,
        };
    }

    public static string ToDisplayName(WindowsMajorVersion version) => version switch
    {
        WindowsMajorVersion.WindowsXp => "Windows XP",
        WindowsMajorVersion.WindowsVista => "Windows Vista",
        WindowsMajorVersion.Windows7 => "Windows 7",
        WindowsMajorVersion.Windows8 => "Windows 8",
        WindowsMajorVersion.Windows8_1 => "Windows 8.1",
        WindowsMajorVersion.Windows10 => "Windows 10",
        WindowsMajorVersion.Windows11 => "Windows 11",
        _ => "an unrecognized Windows version",
    };

    /// <summary>True for the versions this tool's telemetry/app-removal features actually target.</summary>
    public static bool IsModernOptimizationTarget(WindowsMajorVersion version) =>
        version is WindowsMajorVersion.Windows10 or WindowsMajorVersion.Windows11;

    /// <summary>True for the versions the Win7-x64/Vista-SP1+-x64 UEFI fallback-bootloader fix can apply to.</summary>
    public static bool IsLegacyUefiFixCandidate(WindowsMajorVersion version) =>
        version is WindowsMajorVersion.Windows7 or WindowsMajorVersion.WindowsVista;
}
