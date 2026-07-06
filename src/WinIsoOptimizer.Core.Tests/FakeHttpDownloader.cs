using WinIsoOptimizer.Core.Setup;

namespace WinIsoOptimizer.Core.Tests;

internal sealed class FakeHttpDownloader : IHttpDownloader
{
    public List<(string Url, string DestinationPath)> Requests { get; } = new();
    public Func<string, string, Task>? OnDownload { get; set; }

    public async Task DownloadFileAsync(string url, string destinationPath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Requests.Add((url, destinationPath));
        if (OnDownload is not null)
        {
            await OnDownload(url, destinationPath);
        }
    }
}
