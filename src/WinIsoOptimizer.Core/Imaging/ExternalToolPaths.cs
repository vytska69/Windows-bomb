namespace WinIsoOptimizer.Core.Imaging;

/// <summary>
/// Paths to the external tools this app shells out to. Dism and reg ship with every Windows install,
/// so they default to just the bare command name and resolve via PATH. oscdimg.exe only ships with
/// the Windows ADK "Deployment Tools" component, so its path is usually not on PATH and the caller
/// (GUI settings, or setup wizard) is expected to supply it explicitly.
/// </summary>
public sealed class ExternalToolPaths
{
    public string Dism { get; init; } = "dism.exe";
    public string Reg { get; init; } = "reg.exe";

    /// <summary>
    /// Typical default install location for oscdimg.exe when the Windows ADK is installed with
    /// default settings. Not guaranteed to exist; callers should verify with <see cref="File.Exists"/>
    /// and let the user browse to it otherwise.
    /// </summary>
    public string Oscdimg { get; init; } =
        @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe";
}
