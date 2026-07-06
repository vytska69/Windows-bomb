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
        // The fallback-bootloader fix only solves "firmware can't find a bootloader" — it must not
        // imply Windows 7 will fully boot on CSM-less ("UEFI Class 3") hardware, which needs the
        // separate Int10h emulation fix (UefiSeven) this tool doesn't auto-apply.
        Assert.Contains("UefiSeven", assessment.Explanation);
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

    [Fact]
    public void ApplyUefiSevenChainload_throws_when_fallback_bootloader_is_missing()
    {
        var uefiSevenEfi = Path.Combine(_tempRoot, "uefiseven-download", "bootx64.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(uefiSevenEfi)!);
        File.WriteAllText(uefiSevenEfi, "uefiseven bytes");

        Assert.Throws<InvalidOperationException>(() => LegacyUefiBootInjector.ApplyUefiSevenChainload(_tempRoot, uefiSevenEfi));
    }

    [Fact]
    public void ApplyUefiSevenChainload_preserves_the_real_bootloader_and_installs_uefiseven()
    {
        var msBootloader = Path.Combine(_tempRoot, "efi", "microsoft", "boot", "bootmgfw.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(msBootloader)!);
        File.WriteAllText(msBootloader, "the real bootmgfw contents");
        LegacyUefiBootInjector.ApplyFallbackBootloaderFix(_tempRoot);

        var uefiSevenEfi = Path.Combine(_tempRoot, "uefiseven-download", "bootx64.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(uefiSevenEfi)!);
        File.WriteAllText(uefiSevenEfi, "uefiseven chainload bytes");

        LegacyUefiBootInjector.ApplyUefiSevenChainload(_tempRoot, uefiSevenEfi);

        var fallback = Path.Combine(_tempRoot, "efi", "boot", "bootx64.efi");
        var original = Path.Combine(_tempRoot, "efi", "boot", "bootx64.original.efi");
        Assert.Equal("uefiseven chainload bytes", File.ReadAllText(fallback));
        Assert.Equal("the real bootmgfw contents", File.ReadAllText(original));
        Assert.True(LegacyUefiBootInjector.IsUefiSevenChainloadApplied(_tempRoot));
    }

    [Fact]
    public void ApplyUefiSevenChainload_reapplied_with_a_newer_build_does_not_overwrite_the_preserved_original()
    {
        var msBootloader = Path.Combine(_tempRoot, "efi", "microsoft", "boot", "bootmgfw.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(msBootloader)!);
        File.WriteAllText(msBootloader, "the real bootmgfw contents");
        LegacyUefiBootInjector.ApplyFallbackBootloaderFix(_tempRoot);

        var uefiSevenEfiV1 = Path.Combine(_tempRoot, "v1", "bootx64.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(uefiSevenEfiV1)!);
        File.WriteAllText(uefiSevenEfiV1, "uefiseven v1");
        LegacyUefiBootInjector.ApplyUefiSevenChainload(_tempRoot, uefiSevenEfiV1);

        var uefiSevenEfiV2 = Path.Combine(_tempRoot, "v2", "bootx64.efi");
        Directory.CreateDirectory(Path.GetDirectoryName(uefiSevenEfiV2)!);
        File.WriteAllText(uefiSevenEfiV2, "uefiseven v2");
        LegacyUefiBootInjector.ApplyUefiSevenChainload(_tempRoot, uefiSevenEfiV2);

        var fallback = Path.Combine(_tempRoot, "efi", "boot", "bootx64.efi");
        var original = Path.Combine(_tempRoot, "efi", "boot", "bootx64.original.efi");
        Assert.Equal("uefiseven v2", File.ReadAllText(fallback));
        Assert.Equal("the real bootmgfw contents", File.ReadAllText(original));
    }

    [Fact]
    public void IsUefiSevenChainloadApplied_is_false_when_never_applied()
    {
        Assert.False(LegacyUefiBootInjector.IsUefiSevenChainloadApplied(_tempRoot));
    }
}
