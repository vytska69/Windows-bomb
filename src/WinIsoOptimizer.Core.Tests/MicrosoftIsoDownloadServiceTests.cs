using WinIsoOptimizer.Core.Download;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class MicrosoftIsoDownloadServiceTests
{
    private static HttpClient BuildClient(FakeHttpMessageHandler handler) => new(handler);

    private static void WireHappyPathRoutes(FakeHttpMessageHandler handler, string skuJson)
    {
        handler.WhenContains("vlscppe.microsoft.com/tags", _ => FakeHttpMessageHandler.Text(""));
        handler.WhenContains("ov-df.microsoft.com/mdt.js", _ => FakeHttpMessageHandler.Text(@"redirect(""?w=ABCDEF""); rticks=""123"";"));
        handler.When(url => url.StartsWith("https://ov-df.microsoft.com/?", StringComparison.OrdinalIgnoreCase), _ => FakeHttpMessageHandler.Text(""));
        handler.WhenContains("getskuinformationbyproductedition", _ => FakeHttpMessageHandler.Json(skuJson));
    }

    [Fact]
    public async Task GetLanguagesAsync_runs_one_session_per_edition_id_and_merges_languages()
    {
        var handler = new FakeHttpMessageHandler();
        var skuJson = """
            {
              "Skus": [
                { "Id": "sku-en-us", "Language": "en-us", "LocalizedLanguage": "English (United States)" },
                { "Id": "sku-lt-lt", "Language": "lt-lt", "LocalizedLanguage": "Lithuanian" }
              ]
            }
            """;
        WireHappyPathRoutes(handler, skuJson);
        var service = new MicrosoftIsoDownloadService(BuildClient(handler));
        var release = new WindowsIsoRelease("Windows 11 test release", new[] { 3321, 3324 });

        var languages = await service.GetLanguagesAsync(release);

        // Two edition IDs, each contributing the same two languages -> each language ends up with 2 SKU references.
        Assert.Equal(2, languages.Count);
        var english = Assert.Single(languages, l => l.LanguageCode == "en-us");
        Assert.Equal("English (United States)", english.DisplayName);
        Assert.Equal(2, english.Skus.Count);

        var skuInfoRequests = handler.Requests.Count(r => r.RequestUri!.ToString().Contains("getskuinformationbyproductedition"));
        Assert.Equal(2, skuInfoRequests); // one per edition ID in release.EditionIds
    }

    [Fact]
    public async Task GetLanguagesAsync_throws_with_servers_error_message_when_api_reports_an_error()
    {
        var handler = new FakeHttpMessageHandler();
        WireHappyPathRoutes(handler, """{ "Skus": [], "Errors": [ { "Type": 9, "Value": "Sku Not Found" } ] }""");
        var service = new MicrosoftIsoDownloadService(BuildClient(handler));
        var release = new WindowsIsoRelease("Windows 11 test release", new[] { 3321 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetLanguagesAsync(release));
        Assert.Contains("Sku Not Found", ex.Message);
    }

    [Fact]
    public async Task GetDownloadLinksAsync_maps_download_type_to_architecture_and_sends_referer()
    {
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("GetProductDownloadLinksBySku", _ => FakeHttpMessageHandler.Json("""
            {
              "ProductDownloadOptions": [
                { "DownloadType": 1, "Uri": "https://software-download.microsoft.com/x64.iso" },
                { "DownloadType": 2, "Uri": "https://software-download.microsoft.com/arm64.iso" }
              ]
            }
            """));
        var service = new MicrosoftIsoDownloadService(BuildClient(handler));
        var language = new WindowsIsoLanguageOption("English (United States)", "en-us", new[]
        {
            new WindowsIsoSkuReference(Guid.NewGuid(), "sku-en-us"),
        });

        var links = await service.GetDownloadLinksAsync(language);

        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.Architecture == "x64" && l.Url == "https://software-download.microsoft.com/x64.iso");
        Assert.Contains(links, l => l.Architecture == "ARM64" && l.Url == "https://software-download.microsoft.com/arm64.iso");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(MicrosoftIsoDownloadProtocol.RefererUrl, request.Headers.Referrer?.ToString());
    }

    [Fact]
    public async Task GetDownloadLinksAsync_throws_with_servers_error_message_when_api_reports_an_error()
    {
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("GetProductDownloadLinksBySku", _ => FakeHttpMessageHandler.Json(
            """{ "ProductDownloadOptions": [], "Errors": [ { "Type": 1, "Value": "Session expired" } ] }"""));
        var service = new MicrosoftIsoDownloadService(BuildClient(handler));
        var language = new WindowsIsoLanguageOption("English", "en-us", new[] { new WindowsIsoSkuReference(Guid.NewGuid(), "sku") });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDownloadLinksAsync(language));
        Assert.Contains("Session expired", ex.Message);
    }
}
