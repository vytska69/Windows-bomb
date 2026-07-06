using System.Text;

namespace WinIsoOptimizer.Core.Telemetry;

/// <summary>
/// Generates an autounattend.xml that, placed at the root of the ISO, drives Windows Setup through
/// OOBE automatically: local account instead of a forced Microsoft account, and (optionally) skipping
/// the privacy/telemetry consent screens by pre-answering them instead of leaving Microsoft's
/// defaults selected.
/// </summary>
public static class UnattendGenerator
{
    public static string Generate(UnattendOptions options)
    {
        var passwordBase64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(options.LocalAccountPassword + "Password"));
        var adminPasswordBase64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(options.LocalAccountPassword + "AdministratorPassword"));

        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="utf-8"?>""");
        sb.AppendLine("""<unattend xmlns="urn:schemas-microsoft-com:unattend">""");

        // windowsPE pass: language/locale selection during setup itself.
        sb.AppendLine("""  <settings pass="windowsPE">""");
        sb.AppendLine("""    <component name="Microsoft-Windows-International-Core-WinPE" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">""");
        sb.AppendLine($"""      <UILanguage>{Escape(options.UiLanguage)}</UILanguage>""");
        sb.AppendLine($"""      <InputLocale>{Escape(options.InputLocale)}</InputLocale>""");
        sb.AppendLine($"""      <SystemLocale>{Escape(options.UiLanguage)}</SystemLocale>""");
        sb.AppendLine($"""      <UserLocale>{Escape(options.UiLanguage)}</UserLocale>""");
        sb.AppendLine("""    </component>""");
        sb.AppendLine("""  </settings>""");

        // specialize pass: computer name and (optionally) Cortana voice-activation policy.
        sb.AppendLine("""  <settings pass="specialize">""");
        sb.AppendLine("""    <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">""");
        sb.AppendLine($"""      <ComputerName>{Escape(options.ComputerName)}</ComputerName>""");
        sb.AppendLine("""    </component>""");
        if (options.DisableCortanaVoiceActivation)
        {
            sb.AppendLine("""    <component name="Microsoft-Windows-Search" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">""");
            sb.AppendLine("""      <PreventRemoteQueries>true</PreventRemoteQueries>""");
            sb.AppendLine("""    </component>""");
        }
        sb.AppendLine("""  </settings>""");

        // oobeSystem pass: account creation and OOBE screen behavior.
        sb.AppendLine("""  <settings pass="oobeSystem">""");
        sb.AppendLine("""    <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">""");
        sb.AppendLine("""      <UserAccounts>""");
        sb.AppendLine("""        <LocalAccounts>""");
        sb.AppendLine("""          <LocalAccount wcm:action="add">""");
        sb.AppendLine($"""            <Name>{Escape(options.LocalAccountName)}</Name>""");
        sb.AppendLine("""            <Group>Administrators</Group>""");
        sb.AppendLine("""            <Password>""");
        sb.AppendLine($"""              <Value>{passwordBase64}</Value>""");
        sb.AppendLine("""              <PlainText>false</PlainText>""");
        sb.AppendLine("""            </Password>""");
        sb.AppendLine("""          </LocalAccount>""");
        sb.AppendLine("""        </LocalAccounts>""");
        sb.AppendLine("""        <AdministratorPassword>""");
        sb.AppendLine($"""          <Value>{adminPasswordBase64}</Value>""");
        sb.AppendLine("""          <PlainText>false</PlainText>""");
        sb.AppendLine("""        </AdministratorPassword>""");
        sb.AppendLine("""      </UserAccounts>""");
        sb.AppendLine($"""      <TimeZone>{Escape(options.TimeZone)}</TimeZone>""");

        if (options.SkipPrivacyAndAccountOobeScreens)
        {
            sb.AppendLine("""      <OOBE>""");
            sb.AppendLine("""        <HideEULAPage>true</HideEULAPage>""");
            sb.AppendLine("""        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>""");
            sb.AppendLine("""        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>""");
            sb.AppendLine("""        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>""");
            sb.AppendLine("""        <NetworkLocation>Home</NetworkLocation>""");
            // ProtectYourPC=3 means "off": skips the diagnostic-data/find-my-device/tailored
            // experiences consent pages entirely instead of leaving Microsoft's defaults selected.
            sb.AppendLine("""        <ProtectYourPC>3</ProtectYourPC>""");
            sb.AppendLine("""        <SkipMachineOOBE>true</SkipMachineOOBE>""");
            sb.AppendLine("""        <SkipUserOOBE>true</SkipUserOOBE>""");
            sb.AppendLine("""      </OOBE>""");
        }

        sb.AppendLine("""    </component>""");
        sb.AppendLine("""  </settings>""");
        sb.AppendLine("""</unattend>""");
        return sb.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
