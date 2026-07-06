using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using WinIsoOptimizer.App.ViewModels;

// UseWindowsForms is also enabled (only for FolderBrowserDialog, which WPF has no equivalent of),
// so System.Windows.Forms is in scope alongside Microsoft.Win32 — both define OpenFileDialog and
// SaveFileDialog, so every use below is qualified explicitly rather than relying on a `using`
// directive precedence rule (that assumption is what caused the previous CS0104 fix attempt here).

namespace WinIsoOptimizer.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.StatusMessage)) return;

        // AutomationProperties.LiveSetting marks the region as a live region, but WPF does not
        // automatically raise the UIA event when its bound text changes — that has to be raised
        // explicitly so NVDA/JAWS/Narrator actually announce each new status line as it arrives.
        if (UIElementAutomationPeer.FromElement(StatusLiveRegion) is { } peer)
        {
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    private void AccountPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // PasswordBox.Password is intentionally not a dependency property (so it can't be data-bound
        // or shoulder-surfed via a binding trace) — wiring it to the view model has to go through code-behind.
        ViewModel.LocalAccountPassword = AccountPasswordBox.Password;
    }

    private void BrowseSourceIso_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a Windows ISO file",
            Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.SourceIsoPath = dialog.FileName;
        }
    }

    private void BrowseOutputIso_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Where to save the optimized ISO file",
            Filter = "ISO files (*.iso)|*.iso",
            DefaultExt = ".iso",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.OutputIsoPath = dialog.FileName;
        }
    }

    private void BrowseOscdimg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select oscdimg.exe (Windows ADK Deployment Tools)",
            Filter = "oscdimg.exe|oscdimg.exe|Executable files (*.exe)|*.exe",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.OscdimgPath = dialog.FileName;
        }
    }

    private void BrowseAdkSetup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select the downloaded adksetup.exe",
            Filter = "adksetup.exe|adksetup.exe|Executable files (*.exe)|*.exe",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.AdkSetupExePath = dialog.FileName;
        }
    }

    private void BrowseIsoDestination_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Where to save the downloaded ISO file",
            Filter = "ISO files (*.iso)|*.iso",
            DefaultExt = ".iso",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.IsoDownloadDestinationPath = dialog.FileName;
        }
    }

    private void BrowseUefiSevenEfi_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a UefiSeven .efi bootloader you've already downloaded and reviewed",
            Filter = "EFI bootloader (*.efi)|*.efi|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.UefiSevenEfiPath = dialog.FileName;
        }
    }

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder("Select the working folder", path => ViewModel.WorkingDirectory = path);

    private void BrowseDriverFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder("Select the driver folder", path => ViewModel.DriverExportFolder = path);

    private void BrowseFolder(string title, Action<string> onSelected)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            onSelected(dialog.SelectedPath);
        }
    }
}
