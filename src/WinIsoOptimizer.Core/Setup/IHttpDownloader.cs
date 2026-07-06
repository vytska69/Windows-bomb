namespace WinIsoOptimizer.Core.Setup;

/// <summary>Abstraction over downloading a file over HTTP(S), so download orchestration logic can be
/// unit tested without making a real network call.</summary>
public interface IHttpDownloader
{
    Task DownloadFileAsync(string url, string destinationPath, IProgress<string>? progress = null, CancellationToken ct = default);
}
