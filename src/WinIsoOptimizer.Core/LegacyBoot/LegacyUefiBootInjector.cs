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
    /// <summary>
    /// manatails/uefiseven — a chainloading .efi that emulates the legacy BIOS Int10h video interrupt
    /// Windows 7's early boot/graphics init calls, which simply doesn't exist on "UEFI Class 3"
    /// firmware (no CSM at all — the norm on hardware from roughly the last several years). Without
    /// it, Windows 7 can freeze at "Starting Windows" or fail with 0xc000000d even after the fallback-
    /// bootloader fix below gets its EFI bootloader found and running — that fix only solves "the
    /// firmware can find a bootloader," not "the bootloader's OS can actually finish booting." This
    /// project does not bundle, mirror, or redistribute a copy of UefiSeven itself — its repository
    /// publishes no LICENSE file, so there is no stated permission for this project to do that.
    /// <see cref="UefiSevenReleaseFetcher"/> and <see cref="UefiSevenDownloadService"/> instead fetch
    /// the compiled binary directly from the upstream GitHub release, on explicit user request, the
    /// same way a browser download would — and <see cref="ApplyUefiSevenChainload"/> only ever chainloads
    /// it in front of the real bootloader (never replacing or modifying Windows's own files). See
    /// docs/LEGACY-UEFI-BOOT.md.
    /// </summary>
    public const string UefiSevenProjectUrl = "https://github.com/manatails/uefiseven";

    private static readonly string[] MicrosoftEfiBootloaderRelativeSegments = { "efi", "microsoft", "boot", "bootmgfw.efi" };
    private static readonly string[] FallbackEfiBootloaderRelativeSegments = { "efi", "boot", "bootx64.efi" };

    /// <summary>Name UefiSeven's own README instructs users to rename the real bootloader to before
    /// installing UefiSeven in its place — reused here so the result looks exactly like a manual
    /// install to anyone who knows the project's own convention.</summary>
    private static readonly string[] OriginalBootloaderRelativeSegments = { "efi", "boot", "bootx64.original.efi" };

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
                "Microsoft-signed allow list on modern firmware. Note this only fixes firmware *finding* " +
                "a bootloader — on newer \"UEFI Class 3\" hardware with no CSM at all, Windows 7 setup can " +
                "still freeze at \"Starting Windows\" or fail with 0xc000000d because it separately needs a " +
                "legacy BIOS Int10h video interrupt that doesn't exist there; projects like UefiSeven " +
                "(" + UefiSevenProjectUrl + ") exist specifically for that second, deeper problem — this " +
                "tool doesn't apply that fix automatically, see docs/LEGACY-UEFI-BOOT.md.");
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

    /// <summary>
    /// Installs UefiSeven as a chainload in front of the fallback bootloader, following the exact steps
    /// from the project's own README (steps 2–3, the install-media half — this tool never touches an
    /// already-installed machine's hard drive, only extracted source media): the real bootloader is
    /// preserved as \EFI\Boot\bootx64.original.efi, and UefiSeven's own compiled .efi takes over the
    /// \EFI\Boot\bootx64.efi slot, so it runs first and chainloads to the real one afterwards. Requires
    /// the fallback-bootloader fix to already be applied (\EFI\Boot\bootx64.efi must exist) — UefiSeven
    /// solves a second, deeper problem (missing Int10h emulation on "UEFI Class 3" hardware), not the
    /// "firmware can't find a bootloader" problem that fix addresses. Safe to call again with a newer
    /// UefiSeven build: the preserved original is left alone on repeat calls.
    /// </summary>
    public static void ApplyUefiSevenChainload(string extractedSourceFolder, string uefiSevenEfiSourcePath, IProgress<string>? progress = null)
    {
        var fallbackPath = CombinePath(extractedSourceFolder, FallbackEfiBootloaderRelativeSegments);
        if (!File.Exists(fallbackPath))
        {
            throw new InvalidOperationException(
                "No \\EFI\\Boot\\bootx64.efi found on this media yet. Apply the fallback-bootloader fix " +
                "first (see ApplyFallbackBootloaderFix) — UefiSeven chainloads in front of that bootloader, " +
                "it does not replace the need for it.");
        }

        var originalPath = CombinePath(extractedSourceFolder, OriginalBootloaderRelativeSegments);
        if (!File.Exists(originalPath))
        {
            progress?.Report($"Preserving the real bootloader as \\{string.Join('\\', OriginalBootloaderRelativeSegments)}...");
            File.Move(fallbackPath, originalPath);
        }

        progress?.Report($"Installing UefiSeven as \\{string.Join('\\', FallbackEfiBootloaderRelativeSegments)} (chainloads to the preserved original)...");
        File.Copy(uefiSevenEfiSourcePath, fallbackPath, overwrite: true);
    }

    /// <summary>True if <see cref="ApplyUefiSevenChainload"/> has already been applied to this media.</summary>
    public static bool IsUefiSevenChainloadApplied(string extractedSourceFolder) =>
        File.Exists(CombinePath(extractedSourceFolder, OriginalBootloaderRelativeSegments));

    private static string CombinePath(string root, string[] relativeSegments) =>
        Path.Combine(new[] { root }.Concat(relativeSegments).ToArray());
}
