using WinIsoOptimizer.Core.Imaging;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class WindowsVersionClassifierTests
{
    [Theory]
    [InlineData("5.1.2600", WindowsMajorVersion.WindowsXp)]
    [InlineData("5.2.3790", WindowsMajorVersion.WindowsXp)] // XP x64 / Server 2003 share this NT version
    [InlineData("6.0.6002", WindowsMajorVersion.WindowsVista)]
    [InlineData("6.1.7601", WindowsMajorVersion.Windows7)]
    [InlineData("6.2.9200", WindowsMajorVersion.Windows8)]
    [InlineData("6.3.9600", WindowsMajorVersion.Windows8_1)]
    [InlineData("10.0.19045", WindowsMajorVersion.Windows10)]
    [InlineData("10.0.19044", WindowsMajorVersion.Windows10)]
    [InlineData("10.0.22000", WindowsMajorVersion.Windows11)]
    [InlineData("10.0.26100", WindowsMajorVersion.Windows11)]
    public void Classify_maps_nt_version_to_marketing_name(string ntVersion, WindowsMajorVersion expected)
    {
        Assert.Equal(expected, WindowsVersionClassifier.Classify(ntVersion));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("10")]
    [InlineData("99.99.99999")]
    public void Classify_returns_unknown_for_unparseable_or_unrecognized_input(string? ntVersion)
    {
        Assert.Equal(WindowsMajorVersion.Unknown, WindowsVersionClassifier.Classify(ntVersion));
    }

    // The one genuinely easy-to-get-wrong case: 10.0 with a build number right at the Windows 11
    // cutoff (22000) must classify as 11, and 21999 must still be 10 — off-by-one here would be a
    // real, silent misclassification bug rather than a crash.
    [Theory]
    [InlineData("10.0.21999", WindowsMajorVersion.Windows10)]
    [InlineData("10.0.22000", WindowsMajorVersion.Windows11)]
    public void Classify_windows_10_vs_11_boundary_is_exactly_at_build_22000(string ntVersion, WindowsMajorVersion expected)
    {
        Assert.Equal(expected, WindowsVersionClassifier.Classify(ntVersion));
    }

    [Theory]
    [InlineData(WindowsMajorVersion.Windows10, true)]
    [InlineData(WindowsMajorVersion.Windows11, true)]
    [InlineData(WindowsMajorVersion.Windows7, false)]
    [InlineData(WindowsMajorVersion.WindowsXp, false)]
    [InlineData(WindowsMajorVersion.Unknown, false)]
    public void IsModernOptimizationTarget_only_true_for_10_and_11(WindowsMajorVersion version, bool expected)
    {
        Assert.Equal(expected, WindowsVersionClassifier.IsModernOptimizationTarget(version));
    }

    [Theory]
    [InlineData(WindowsMajorVersion.Windows7, true)]
    [InlineData(WindowsMajorVersion.WindowsVista, true)]
    [InlineData(WindowsMajorVersion.WindowsXp, false)]
    [InlineData(WindowsMajorVersion.Windows8_1, false)]
    [InlineData(WindowsMajorVersion.Windows10, false)]
    public void IsLegacyUefiFixCandidate_only_true_for_7_and_vista(WindowsMajorVersion version, bool expected)
    {
        Assert.Equal(expected, WindowsVersionClassifier.IsLegacyUefiFixCandidate(version));
    }

    [Fact]
    public void ToDisplayName_covers_every_enum_value_without_falling_back_to_unknown()
    {
        foreach (WindowsMajorVersion version in Enum.GetValues<WindowsMajorVersion>())
        {
            if (version == WindowsMajorVersion.Unknown) continue;
            Assert.NotEqual("an unrecognized Windows version", WindowsVersionClassifier.ToDisplayName(version));
        }
    }
}
