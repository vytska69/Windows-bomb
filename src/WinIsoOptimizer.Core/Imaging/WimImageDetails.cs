namespace WinIsoOptimizer.Core.Imaging;

/// <summary>Extended per-index metadata from `dism /Get-WimInfo /WimFile:X /Index:N` — unlike the plain
/// edition listing (<see cref="WimImageInfo"/>), querying a specific index also reports the underlying
/// NT kernel version, which is the reliable way to tell exactly which Windows release an image is
/// (the human-readable Name/Description strings are inconsistent across languages/editions and don't
/// distinguish Windows 10 from 11 at all — both call themselves "10.0" internally).</summary>
public sealed record WimImageDetails(
    int Index,
    string Name,
    string? Version,
    string? EditionId,
    string? InstallationType,
    string? Architecture);
