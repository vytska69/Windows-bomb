using System.Xml.Linq;
using WinIsoOptimizer.Core.Telemetry;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class UnattendGeneratorTests
{
    [Fact]
    public void Generate_produces_well_formed_xml()
    {
        var xml = UnattendGenerator.Generate(new UnattendOptions());

        // Throws if malformed — the real risk with hand-built XML strings.
        var doc = XDocument.Parse(xml);
        Assert.Equal("unattend", doc.Root!.Name.LocalName);
    }

    [Fact]
    public void Generate_includes_local_account_with_base64_password_not_plaintext()
    {
        var xml = UnattendGenerator.Generate(new UnattendOptions { LocalAccountName = "Vytas", LocalAccountPassword = "hunter2" });

        Assert.Contains("<Name>Vytas</Name>", xml);
        Assert.DoesNotContain("hunter2", xml);
        Assert.Contains("<PlainText>false</PlainText>", xml);
    }

    [Fact]
    public void Generate_includes_oobe_skip_elements_when_requested()
    {
        var xml = UnattendGenerator.Generate(new UnattendOptions { SkipPrivacyAndAccountOobeScreens = true });

        Assert.Contains("<ProtectYourPC>3</ProtectYourPC>", xml);
        Assert.Contains("<HideOnlineAccountScreens>true</HideOnlineAccountScreens>", xml);
    }

    [Fact]
    public void Generate_omits_oobe_skip_elements_when_not_requested()
    {
        var xml = UnattendGenerator.Generate(new UnattendOptions { SkipPrivacyAndAccountOobeScreens = false });

        Assert.DoesNotContain("ProtectYourPC", xml);
    }

    [Fact]
    public void Generate_escapes_xml_special_characters_in_computer_name()
    {
        var xml = UnattendGenerator.Generate(new UnattendOptions { ComputerName = "PC<1>&\"2\"" });

        var doc = XDocument.Parse(xml); // would throw on unescaped '<' or '&'
        Assert.Contains(doc.Descendants().Where(e => e.Name.LocalName == "ComputerName"), e => e.Value == "PC<1>&\"2\"");
    }
}
