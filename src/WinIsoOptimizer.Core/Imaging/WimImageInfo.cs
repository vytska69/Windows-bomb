namespace WinIsoOptimizer.Core.Imaging;

/// <summary>One edition/index inside an install.wim or install.esd, as reported by `dism /Get-WimInfo`.</summary>
public sealed record WimImageInfo(int Index, string Name, string Description, long SizeBytes);

/// <summary>A provisioned AppX package (a Store app baked into the image), as reported by
/// `dism /Image:X /Get-ProvisionedAppxPackages`.</summary>
public sealed record ProvisionedAppxPackage(string DisplayName, string PackageName);
