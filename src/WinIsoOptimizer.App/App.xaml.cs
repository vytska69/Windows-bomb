namespace WinIsoOptimizer.App;

// UseWindowsForms is also enabled (for FolderBrowserDialog), so both System.Windows and
// System.Windows.Forms are in scope here — each defines its own "Application" class, so the base
// class has to be qualified explicitly rather than relying on `using System.Windows;` alone.
public partial class App : System.Windows.Application
{
}
