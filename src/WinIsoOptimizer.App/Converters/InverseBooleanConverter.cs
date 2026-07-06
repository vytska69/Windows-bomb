using System.Globalization;
using System.Windows.Data;

namespace WinIsoOptimizer.App.Converters;

/// <summary>Used for e.g. `IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBoolean}}"` —
/// controls should be disabled while a job is running, not enabled.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is true);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is true);
}
