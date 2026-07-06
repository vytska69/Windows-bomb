namespace WinIsoOptimizer.Core.Telemetry;

public sealed record UnattendOptions
{
    public string ComputerName { get; init; } = "*";
    public string LocalAccountName { get; init; } = "User";

    /// <summary>Plain text only for as long as it lives in memory/this options object; written into
    /// the answer file as a base64'd "Password" element per the Microsoft-Windows-Shell-Setup schema,
    /// which is the standard (and only) way unattend.xml represents account passwords — it is
    /// obfuscation, not real protection, so treat the resulting autounattend.xml as sensitive.</summary>
    public string LocalAccountPassword { get; init; } = string.Empty;

    public string TimeZone { get; init; } = "UTC";
    public string UiLanguage { get; init; } = "en-US";
    public string InputLocale { get; init; } = "en-US";

    /// <summary>Skips the network/Microsoft-account-required OOBE pages (ProtectYourPC=3) and the
    /// telemetry/advertising-consent OOBE screens, going straight to a local account.</summary>
    public bool SkipPrivacyAndAccountOobeScreens { get; init; } = true;

    public bool DisableCortanaVoiceActivation { get; init; } = true;
}
