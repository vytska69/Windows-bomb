namespace WinIsoOptimizer.Core.Download;

/// <summary>
/// One selectable Windows release, with the Microsoft internal "product edition ID(s)" needed to
/// drive the official public download flow at microsoft.com/software-download. One ID per
/// architecture-session: Windows 11 needs a separate session for x64 vs ARM64, Windows 10 only needs one.
/// </summary>
public sealed record WindowsIsoRelease(string DisplayName, IReadOnlyList<int> EditionIds);

/// <summary>
/// Windows releases currently offered through Microsoft's public consumer ISO download flow (Home,
/// Pro, Education — no Enterprise, that's only ever available through paid Volume Licensing / MVS,
/// which this deliberately does not attempt to reach).
///
/// There is no discovery API for the numeric edition IDs below — they're Microsoft's internal
/// identifiers for this specific download flow and rotate every time Microsoft ships a new build, with
/// no advance notice. Every tool that automates this (pbatard/Fido, Rufus, this one) has to hardcode
/// current values and refresh them periodically; there's no way around that. The values here were
/// copied directly from Fido's $WindowsVersions table (https://github.com/pbatard/Fido/blob/master/Fido.ps1),
/// the actively-maintained open-source reference implementation of this exact flow, as of the build
/// listed in each entry's DisplayName. If a release here starts failing at the language-list step,
/// check Fido's current script for updated IDs — that's the canonical source, not any value guessed
/// here.
/// </summary>
public static class WindowsIsoCatalog
{
    public static IReadOnlyList<WindowsIsoRelease> Releases { get; } = new[]
    {
        new WindowsIsoRelease("Windows 11 Home/Pro/Education — 25H2 v2 (Build 26200.8037, 2026.03)", new[] { 3321, 3324 }),
        new WindowsIsoRelease("Windows 10 Home/Pro/Education — 22H2 v1 (Build 19045.2965, 2023.05)", new[] { 2618 }),
    };
}
