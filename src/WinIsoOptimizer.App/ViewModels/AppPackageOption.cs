using WinIsoOptimizer.Core.Imaging;

namespace WinIsoOptimizer.App.ViewModels;

/// <summary>A provisioned app found in the loaded image, plus whether the user has checked it for removal.</summary>
public sealed class AppPackageOption : ViewModelBase
{
    private bool _isSelected;

    public AppPackageOption(ProvisionedAppxPackage package, string friendlyName)
    {
        Package = package;
        FriendlyName = friendlyName;
    }

    public ProvisionedAppxPackage Package { get; }
    public string FriendlyName { get; }
    public string PackageName => Package.PackageName;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>Text read by a screen reader for this checkbox row: friendly name plus the real
    /// package name, so the choice is unambiguous even when two entries share a friendly name.</summary>
    public string AccessibleDescription => $"{FriendlyName} ({PackageName})";
}
