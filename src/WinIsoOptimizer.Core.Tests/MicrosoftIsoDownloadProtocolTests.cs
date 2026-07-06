using WinIsoOptimizer.Core.Download;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class MicrosoftIsoDownloadProtocolTests
{
    private static readonly Guid FixedSessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void BuildVlscppeTagsUrl_includes_org_id_and_session_id()
    {
        var url = MicrosoftIsoDownloadProtocol.BuildVlscppeTagsUrl(FixedSessionId);

        Assert.Equal("https://vlscppe.microsoft.com/tags?org_id=y6jn8c31&session_id=11111111-2222-3333-4444-555555555555", url);
    }

    [Fact]
    public void BuildOvDfChallengeUrl_includes_fixed_instance_id_and_session_id()
    {
        var url = MicrosoftIsoDownloadProtocol.BuildOvDfChallengeUrl(FixedSessionId);

        Assert.Equal("https://ov-df.microsoft.com/mdt.js?instanceId=560dc9f3-1aa5-4a2f-b63c-9e18f8d0e175&PageId=si&session_id=11111111-2222-3333-4444-555555555555", url);
    }

    [Fact]
    public void BuildOvDfReplyUrl_includes_all_challenge_parameters()
    {
        var url = MicrosoftIsoDownloadProtocol.BuildOvDfReplyUrl(FixedSessionId, "ABCDEF", "12345", 1_700_000_000_000);

        Assert.Equal(
            "https://ov-df.microsoft.com/?session_id=11111111-2222-3333-4444-555555555555&CustomerId=560dc9f3-1aa5-4a2f-b63c-9e18f8d0e175&PageId=si&w=ABCDEF&mdt=1700000000000&rticks=12345",
            url);
    }

    [Fact]
    public void BuildSkuInformationUrl_includes_profile_edition_locale_and_session()
    {
        var url = MicrosoftIsoDownloadProtocol.BuildSkuInformationUrl(3321, "en-US", FixedSessionId);

        Assert.Equal(
            "https://www.microsoft.com/software-download-connector/api/getskuinformationbyproductedition" +
            "?profile=606624d44113&productEditionId=3321&SKU=undefined&friendlyFileName=undefined&Locale=en-US&sessionID=11111111-2222-3333-4444-555555555555",
            url);
    }

    [Fact]
    public void BuildDownloadLinksUrl_includes_profile_sku_locale_and_session()
    {
        var url = MicrosoftIsoDownloadProtocol.BuildDownloadLinksUrl("abc-123", "en-US", FixedSessionId);

        Assert.Equal(
            "https://www.microsoft.com/software-download-connector/api/GetProductDownloadLinksBySku" +
            "?profile=606624d44113&productEditionId=undefined&SKU=abc-123&friendlyFileName=undefined&Locale=en-US&sessionID=11111111-2222-3333-4444-555555555555",
            url);
    }

    [Theory]
    [InlineData(@"var x = ""?w=1A2B3C&foo=bar""", "1A2B3C")]
    [InlineData(@"redirectUrl = ""https://ov-df.microsoft.com/?w=DEADBEEF""", "DEADBEEF")]
    public void ExtractWParameter_finds_hex_value_after_w_query_param(string body, string expected)
    {
        Assert.Equal(expected, MicrosoftIsoDownloadProtocol.ExtractWParameter(body));
    }

    [Fact]
    public void ExtractWParameter_throws_clearly_when_not_found()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MicrosoftIsoDownloadProtocol.ExtractWParameter("no match here"));
        Assert.Contains("ov-df.microsoft.com", ex.Message);
    }

    [Theory]
    [InlineData(@"rticks=""+123456789""", "123456789")]
    [InlineData(@"rticks=""987654321""", "987654321")]
    public void ExtractRTicksParameter_finds_digits(string body, string expected)
    {
        Assert.Equal(expected, MicrosoftIsoDownloadProtocol.ExtractRTicksParameter(body));
    }

    [Fact]
    public void ExtractRTicksParameter_throws_clearly_when_not_found()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MicrosoftIsoDownloadProtocol.ExtractRTicksParameter("no match here"));
        Assert.Contains("ov-df.microsoft.com", ex.Message);
    }

    [Theory]
    [InlineData(0, "x86")]
    [InlineData(1, "x64")]
    [InlineData(2, "ARM64")]
    [InlineData(99, "Unknown")]
    public void MapDownloadTypeToArchitecture_matches_known_values(int downloadType, string expected)
    {
        Assert.Equal(expected, MicrosoftIsoDownloadProtocol.MapDownloadTypeToArchitecture(downloadType));
    }
}
