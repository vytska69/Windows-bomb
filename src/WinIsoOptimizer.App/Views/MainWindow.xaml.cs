using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using Microsoft.Win32;
using WinIsoOptimizer.App.ViewModels;

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
        var dialog = new OpenFileDialog
        {
            Title = "Pasirinkite Windows ISO failą",
            Filter = "ISO failai (*.iso)|*.iso|Visi failai (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.SourceIsoPath = dialog.FileName;
        }
    }

    private void BrowseOutputIso_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Kur išsaugoti optimizuotą ISO failą",
            Filter = "ISO failai (*.iso)|*.iso",
            DefaultExt = ".iso",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.OutputIsoPath = dialog.FileName;
        }
    }

    private void BrowseOscdimg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pasirinkite oscdimg.exe (Windows ADK Deployment Tools)",
            Filter = "oscdimg.exe|oscdimg.exe|Vykdomieji failai (*.exe)|*.exe",
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.OscdimgPath = dialog.FileName;
        }
    }

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder("Pasirinkite darbinį aplanką", path => ViewModel.WorkingDirectory = path);

    private void BrowseDriverFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder("Pasirinkite draiverių aplanką", path => ViewModel.DriverExportFolder = path);

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
