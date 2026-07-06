using WinIsoOptimizer.Core.Updates;
using Xunit;

namespace WinIsoOptimizer.Core.Tests;

public class GitHubReleaseUpdateCheckerTests
{
    [Theory]
    [InlineData("build-12", 12)]
    [InlineData("build-1", 1)]
    [InlineData("build-9999", 9999)]
    public void ParseBuildNumber_extracts_the_number_from_a_well_formed_tag(string tag, int expected)
    {
        Assert.Equal(expected, GitHubReleaseUpdateChecker.ParseBuildNumber(tag));
    }

    [Theory]
    [InlineData("v1.2.3")]
    [InlineData("build-")]
    [InlineData("build-abc")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseBuildNumber_returns_null_for_anything_else(string? tag)
    {
        Assert.Null(GitHubReleaseUpdateChecker.ParseBuildNumber(tag));
    }

    [Fact]
    public async Task CheckForUpdateAsync_reports_an_update_when_latest_build_is_newer()
    {
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("/repos/someone/somerepo/releases/latest", _ => FakeHttpMessageHandler.Json(
            """{ "tag_name": "build-15", "html_url": "https://github.com/someone/somerepo/releases/tag/build-15" }"""));
        var checker = new GitHubReleaseUpdateChecker("someone", "somerepo", new HttpClient(handler));

        var result = await checker.CheckForUpdateAsync(currentBuildNumber: 10);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(15, result.LatestBuildNumber);
        Assert.Equal("build-15", result.LatestVersionTag);
        Assert.Equal("https://github.com/someone/somerepo/releases/tag/build-15", result.ReleaseUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_reports_no_update_when_already_current()
    {
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("/releases/latest", _ => FakeHttpMessageHandler.Json(
            """{ "tag_name": "build-10", "html_url": "https://example.invalid" }"""));
        var checker = new GitHubReleaseUpdateChecker("someone", "somerepo", new HttpClient(handler));

        var result = await checker.CheckForUpdateAsync(currentBuildNumber: 10);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_reports_no_update_when_running_a_newer_build_than_latest_release()
    {
        // e.g. a local dev build ahead of the last published release, or a manually re-run older workflow.
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("/releases/latest", _ => FakeHttpMessageHandler.Json(
            """{ "tag_name": "build-5", "html_url": "https://example.invalid" }"""));
        var checker = new GitHubReleaseUpdateChecker("someone", "somerepo", new HttpClient(handler));

        var result = await checker.CheckForUpdateAsync(currentBuildNumber: 10);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_does_not_crash_when_the_latest_tag_is_not_a_build_tag()
    {
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("/releases/latest", _ => FakeHttpMessageHandler.Json(
            """{ "tag_name": "v2.0.0", "html_url": "https://example.invalid" }"""));
        var checker = new GitHubReleaseUpdateChecker("someone", "somerepo", new HttpClient(handler));

        var result = await checker.CheckForUpdateAsync(currentBuildNumber: 10);

        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.LatestBuildNumber);
    }

    [Fact]
    public async Task CheckForUpdateAsync_sends_a_user_agent_header_since_github_rejects_requests_without_one()
    {
        var handler = new FakeHttpMessageHandler();
        handler.WhenContains("/releases/latest", _ => FakeHttpMessageHandler.Json(
            """{ "tag_name": "build-1", "html_url": "https://example.invalid" }"""));
        var checker = new GitHubReleaseUpdateChecker("someone", "somerepo", new HttpClient(handler));

        await checker.CheckForUpdateAsync(currentBuildNumber: 1);

        var request = Assert.Single(handler.Requests);
        Assert.NotEmpty(request.Headers.UserAgent);
    }
}
