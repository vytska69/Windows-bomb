namespace WinIsoOptimizer.Core.Setup;

/// <summary>Real <see cref="IHttpDownloader"/>, streaming the response straight to disk and reporting
/// progress every few megabytes rather than buffering the whole (multi-hundred-MB) file in memory.</summary>
public sealed class HttpDownloader : IHttpDownloader
{
    private const long ProgressReportIntervalBytes = 5 * 1024 * 1024;

    private readonly HttpClient _httpClient;

    public HttpDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadFileAsync(string url, string destinationPath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var httpStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = File.Create(destinationPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        long lastReportedAt = 0;
        int bytesRead;
        while ((bytesRead = await httpStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            totalRead += bytesRead;

            if (totalRead - lastReportedAt >= ProgressReportIntervalBytes)
            {
                lastReportedAt = totalRead;
                var totalMb = totalRead / 1024.0 / 1024.0;
                progress?.Report(totalBytes is { } total
                    ? $"Downloaded {totalMb:F0} MB of {total / 1024.0 / 1024.0:F0} MB..."
                    : $"Downloaded {totalMb:F0} MB...");
            }
        }
    }
}
