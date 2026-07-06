namespace WinIsoOptimizer.Core.LegacyBoot;

public enum LegacyUefiBootSupport
{
    /// <summary>The media already boots natively via UEFI (and, for 8/8.1+, Secure Boot) — nothing to do.</summary>
    NativeAlready,

    /// <summary>The media ships a Microsoft EFI bootloader (bootmgfw.efi) but is missing the fallback
    /// \EFI\Boot\bootx64.efi that UEFI firmware loads by default with no NVRAM boot entry. Copying one
    /// from the other is a well-documented, real fix (used for Windows 7 x64 / Vista SP1+ x64 media).</summary>
    FixableFallbackBootloaderMissing,

    /// <summary>The media has no Microsoft EFI bootloader at all. This is the case for every Windows XP
    /// build and for Windows Vista RTM / any 32-bit media — Microsoft never shipped EFI boot support in
    /// them, full stop. Making this boot on UEFI firmware would require a third-party shim bootloader
    /// (e.g. Clover) chainloading the legacy MBR/NTLDR path, which is a fundamentally different,
    /// unsupported approach and is out of scope here rather than offered as fake/partial support.</summary>
    Unsupported,
}

public sealed record LegacyUefiBootAssessment(LegacyUefiBootSupport Support, string Explanation);

/// <summary>
/// Adds UEFI bootability to extracted Windows install media that predates reliable UEFI support, by
/// supplying the missing "\EFI\Boot\bootx64.efi" fallback loader that firmware looks for when there is
/// no existing NVRAM boot entry (e.g. booting fresh off a USB stick). See docs/LEGACY-UEFI-BOOT.md for
/// the full explanation, including why XP/Vista RTM/32-bit media cannot be supported this way.
/// </summary>
public static class LegacyUefiBootInjector
{
    private static readonly string[] MicrosoftEfiBootloaderRelativeSegments = { "efi", "microsoft", "boot", "bootmgfw.efi" };
    private static readonly string[] FallbackEfiBootloaderRelativeSegments = { "efi", "boot", "bootx64.efi" };

    public static LegacyUefiBootAssessment Assess(string extractedSourceFolder)
    {
        var fallbackPath = CombinePath(extractedSourceFolder, FallbackEfiBootloaderRelativeSegments);
        if (File.Exists(fallbackPath))
        {
            return new LegacyUefiBootAssessment(LegacyUefiBootSupport.NativeAlready,
                "This media already has \\EFI\\Boot\\bootx64.efi — it boots via UEFI as-is (this is the normal case for Windows 8/8.1, 10, and 11 media).");
        }

        var msBootloaderPath = CombinePath(extractedSourceFolder, MicrosoftEfiBootloaderRelativeSegments);
        if (File.Exists(msBootloaderPath))
        {
            return new LegacyUefiBootAssessment(LegacyUefiBootSupport.FixableFallbackBootloaderMissing,
                "Found \\EFI\\Microsoft\\Boot\\bootmgfw.efi but no \\EFI\\Boot\\bootx64.efi fallback loader " +
                "(typical for Windows 7 x64 and Windows Vista SP1+ x64 media). Copying the former to the " +
                "latter lets UEFI firmware find it with no NVRAM boot entry needed. Secure Boot must be " +
                "disabled in firmware first — this bootloader predates Secure Boot and is not in the " +
                "Microsoft-signed allow list on modern firmware.");
        }

        return new LegacyUefiBootAssessment(LegacyUefiBootSupport.Unsupported,
            "No Microsoft EFI bootloader was found anywhere in this media. This is expected for every " +
            "Windows XP release and for Windows Vista RTM / 32-bit media: Microsoft never shipped UEFI " +
            "boot support in them. There is no supported way to make this media boot natively via UEFI; " +
            "doing so would need a third-party shim bootloader chainloading the legacy BIOS/MBR boot " +
            "path, a different and much less reliable approach that is intentionally out of scope. Boot " +
            "this media via legacy BIOS/CSM mode instead.");
    }

    /// <summary>
    /// Performs the fix described by <see cref="LegacyUefiBootSupport.FixableFallbackBootloaderMissing"/>.
    /// Throws if <see cref="Assess"/> would not have returned that state — callers should call
    /// <see cref="Assess"/> first and only invoke this when it says the fix applies.
    /// </summary>
    public static void ApplyFallbackBootloaderFix(string extractedSourceFolder, IProgress<string>? progress = null)
    {
        var assessment = Assess(extractedSourceFolder);
        if (assessment.Support != LegacyUefiBootSupport.FixableFallbackBootloaderMissing)
        {
            throw new InvalidOperationException(
                $"Refusing to apply the UEFI fallback-bootloader fix: {assessment.Support} does not need or support it. {assessment.Explanation}");
        }

        var msBootloaderPath = CombinePath(extractedSourceFolder, MicrosoftEfiBootloaderRelativeSegments);
        var fallbackPath = CombinePath(extractedSourceFolder, FallbackEfiBootloaderRelativeSegments);

        Directory.CreateDirectory(Path.GetDirectoryName(fallbackPath)!);
        progress?.Report($"Copying \\{string.Join('\\', MicrosoftEfiBootloaderRelativeSegments)} -> \\{string.Join('\\', FallbackEfiBootloaderRelativeSegments)}...");
        File.Copy(msBootloaderPath, fallbackPath, overwrite: true);
    }

    private static string CombinePath(string root, string[] relativeSegments) =>
        Path.Combine(new[] { root }.Concat(relativeSegments).ToArray());
}
