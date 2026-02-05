using System.Net;
using System.Net.Http.Headers;
using NvwUpd.Models;

namespace NvwUpd.Core;

/// <summary>
/// Downloads NVIDIA drivers with progress reporting.
/// </summary>
public class DriverDownloader : IDriverDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _downloadDirectory;

    public DriverDownloader()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NvwUpd/1.0");
        _httpClient.Timeout = TimeSpan.FromMinutes(30); // Large file download

        // Use temp directory for downloads
        _downloadDirectory = Path.Combine(Path.GetTempPath(), "NvwUpd", "Downloads");
        Directory.CreateDirectory(_downloadDirectory);
    }

    public async Task<DownloadResult> DownloadDriverAsync(
        DriverInfo driverInfo,
        DriverType driverType,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadUrl = GetDownloadUrl(driverInfo, driverType);
        var fileName = $"NVIDIA-Driver-{driverInfo.Version}-{driverType}.exe";
        var filePath = Path.Combine(_downloadDirectory, fileName);

        long existingSizeBytes = 0;
        if (File.Exists(filePath))
        {
            existingSizeBytes = new FileInfo(filePath).Length;
            if (driverInfo.FileSize > 0 && existingSizeBytes == driverInfo.FileSize)
            {
                progress?.Report(1.0);
                return new DownloadResult
                {
                    FilePath = filePath,
                    WasResumed = false,
                    WasRestarted = false
                };
            }
        }

        async Task<HttpResponseMessage> SendRequestAsync(long rangeStart, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            if (rangeStart > 0)
            {
                request.Headers.Range = new RangeHeaderValue(rangeStart, null);
            }
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }

        using var response = await SendRequestAsync(existingSizeBytes, cancellationToken);
        var wasResumed = response.StatusCode == HttpStatusCode.PartialContent && existingSizeBytes > 0;
        var wasRestarted = false;

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // Local file is larger than remote or invalid range; restart download
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            existingSizeBytes = 0;
            using var retryResponse = await SendRequestAsync(0, cancellationToken);
            var retryPath = await SaveResponseToFileAsync(retryResponse, filePath, existingSizeBytes, driverInfo.FileSize, progress, cancellationToken);
            ValidateFileSize(driverInfo.FileSize, retryPath);
            return new DownloadResult
            {
                FilePath = retryPath,
                WasResumed = false,
                WasRestarted = true
            };
        }

        if (response.StatusCode == HttpStatusCode.OK && existingSizeBytes > 0)
        {
            // Server ignored range; restart from scratch
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            existingSizeBytes = 0;
            wasResumed = false;
            wasRestarted = true;
        }

        var finalPath = await SaveResponseToFileAsync(response, filePath, existingSizeBytes, driverInfo.FileSize, progress, cancellationToken);
        ValidateFileSize(driverInfo.FileSize, finalPath);
        return new DownloadResult
        {
            FilePath = finalPath,
            WasResumed = wasResumed,
            WasRestarted = wasRestarted
        };
    }

    private static async Task<string> SaveResponseToFileAsync(
        HttpResponseMessage response,
        string filePath,
        long existingSizeBytes,
        long driverFileSize,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? driverFileSize;
        var totalBytes = contentLength;

        if (response.StatusCode == HttpStatusCode.PartialContent && existingSizeBytes > 0 && contentLength > 0)
        {
            totalBytes = existingSizeBytes + contentLength;
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var fileMode = existingSizeBytes > 0 ? FileMode.Append : FileMode.Create;
        await using var fileStream = new FileStream(filePath, fileMode, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                progress?.Report((double)(existingSizeBytes + totalRead) / totalBytes);
            }
        }

        progress?.Report(1.0);
        return filePath;
    }

    private static void ValidateFileSize(long expectedSize, string filePath)
    {
        if (expectedSize <= 0)
        {
            return;
        }

        var actualSize = new FileInfo(filePath).Length;
        if (actualSize != expectedSize)
        {
            throw new IOException($"Downloaded file size mismatch. Expected: {expectedSize}, Actual: {actualSize}");
        }
    }

    private static string GetDownloadUrl(DriverInfo driverInfo, DriverType driverType)
    {
        // For Studio drivers, we may need to modify the URL
        // Game Ready and Studio drivers typically have different URLs
        var url = driverInfo.DownloadUrl;

        if (driverType == DriverType.Studio && !url.Contains("nsd"))
        {
            // Try to convert GRD URL to SD URL (this is a simplified approach)
            // In practice, you'd fetch the Studio driver URL separately
            url = url.Replace("/GFE/", "/NSD/");
        }

        return url;
    }

    /// <summary>
    /// Cleans up old downloaded files.
    /// </summary>
    public void CleanupOldDownloads(int daysOld = 7)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-daysOld);
            foreach (var file in Directory.GetFiles(_downloadDirectory, "*.exe"))
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
