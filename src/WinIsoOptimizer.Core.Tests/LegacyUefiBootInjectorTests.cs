using WinIsoOptimizer.Core.LegacyBoot;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class LegacyUefiBootInjectorTests : IDisposable
{
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("wioo-legacyboot-test-").FullName;

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    [Fact]
    public void Assess_returns_NativeAlready_when_fallback_bootloader_present()
    {
        var fallback = Path.Combine(_tempRoot, "efi", "boot", "bootx64.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
        File.WriteAllText(fallback, "x");

        var assessment = LegacyUefiBootInjector.Assess(_tempRoot);

        Assert.Equal(LegacyUefiBootSupport.NativeAlready, assessment.Support);
    }

    [Fact]
    public void Assess_returns_Fixable_when_only_microsoft_bootloader_present()
    {
        var msBootloader = Path.Combine(_tempRoot, "efi", "microsoft", "boot", "bootmgfw.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(msBootloader)!);
        File.WriteAllText(msBootloader, "x");

        var assessment = LegacyUefiBootInjector.Assess(_tempRoot);

        Assert.Equal(LegacyUefiBootSupport.FixableFallbackBootloaderMissing, assessment.Support);
    }

    [Fact]
    public void Assess_returns_Unsupported_when_no_efi_bootloader_present_at_all()
    {
        var assessment = LegacyUefiBootInjector.Assess(_tempRoot);

        Assert.Equal(LegacyUefiBootSupport.Unsupported, assessment.Support);
    }

    [Fact]
    public void ApplyFallbackBootloaderFix_copies_microsoft_bootloader_to_fallback_path()
    {
        var msBootloader = Path.Combine(_tempRoot, "efi", "microsoft", "boot", "bootmgfw.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(msBootloader)!);
        File.WriteAllText(msBootloader, "the real bootmgfw contents");

        LegacyUefiBootInjector.ApplyFallbackBootloaderFix(_tempRoot);

        var fallback = Path.Combine(_tempRoot, "efi", "boot", "bootx64.efi");
        Assert.True(File.Exists(fallback));
        Assert.Equal("the real bootmgfw contents", File.ReadAllText(fallback));
    }

    [Fact]
    public void ApplyFallbackBootloaderFix_throws_when_media_is_unsupported()
    {
        Assert.Throws<InvalidOperationException>(() => LegacyUefiBootInjector.ApplyFallbackBootloaderFix(_tempRoot));
    }

    [Fact]
    public void ApplyFallbackBootloaderFix_throws_when_already_native()
    {
        var fallback = Path.Combine(_tempRoot, "efi", "boot", "bootx64.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
        File.WriteAllText(fallback, "x");

        Assert.Throws<InvalidOperationException>(() => LegacyUefiBootInjector.ApplyFallbackBootloaderFix(_tempRoot));
    }
}
