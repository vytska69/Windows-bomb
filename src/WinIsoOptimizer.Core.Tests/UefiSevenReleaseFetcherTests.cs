using WinIsoOptimizer.Core.LegacyBoot;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class UefiSevenReleaseFetcherTests
{
    [Fact]
    public async Task FetchLatestReleaseAsync_parses_tag_url_and_assets()
    {
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("/repos/manatails/uefiseven/releases/latest", _ => FakeHttpMessageHandler.Json("""
            {
              "tag_name": "1.30",
              "html_url": "https://github.com/manatails/uefiseven/releases/tag/1.30",
              "assets": [
                { "name": "UefiSeven-1.30.zip", "browser_download_url": "https://example.invalid/UefiSeven-1.30.zip" }
              ]
            }
            """));
        var fetcher = new UefiSevenReleaseFetcher(new HttpClient(handler));

        var release = await fetcher.FetchLatestReleaseAsync();

        Assert.Equal("1.30", release.TagName);
        Assert.Equal("https://github.com/manatails/uefiseven/releases/tag/1.30", release.HtmlUrl);
        var asset = Assert.Single(release.Assets);
        Assert.Equal("UefiSeven-1.30.zip", asset.Name);
        Assert.Equal("https://example.invalid/UefiSeven-1.30.zip", asset.DownloadUrl);
    }

    [Fact]
    public async Task FetchLatestReleaseAsync_sends_a_user_agent_header_since_github_rejects_requests_without_one()
    {
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("/releases/latest", _ => FakeHttpMessageHandler.Json("""{ "tag_name": "1.30", "html_url": "https://example.invalid" }"""));
        var fetcher = new UefiSevenReleaseFetcher(new HttpClient(handler));

        await fetcher.FetchLatestReleaseAsync();

        var request = Assert.Single(handler.Requests);
        Assert.NotEmpty(request.Headers.UserAgent);
    }

    [Fact]
    public void SelectBootloaderAsset_prefers_a_direct_efi_file_over_a_zip()
    {
        var assets = new[]
        {
            new UefiSevenReleaseAsset("UefiSeven.zip", "https://example.invalid/zip"),
            new UefiSevenReleaseAsset("bootx64.efi", "https://example.invalid/efi"),
        };

        var selected = UefiSevenReleaseFetcher.SelectBootloaderAsset(assets);

        Assert.NotNull(selected);
        Assert.Equal("bootx64.efi", selected!.Name);
    }

    [Fact]
    public void SelectBootloaderAsset_falls_back_to_a_zip_when_no_efi_asset_exists()
    {
        var assets = new[] { new UefiSevenReleaseAsset("UefiSeven-1.30.zip", "https://example.invalid/zip") };

        var selected = UefiSevenReleaseFetcher.SelectBootloaderAsset(assets);

        Assert.NotNull(selected);
        Assert.Equal("UefiSeven-1.30.zip", selected!.Name);
    }

    [Fact]
    public void SelectBootloaderAsset_returns_null_when_nothing_usable_is_present()
    {
        var assets = new[] { new UefiSevenReleaseAsset("README.md", "https://example.invalid/readme") };

        Assert.Null(UefiSevenReleaseFetcher.SelectBootloaderAsset(assets));
    }
}
