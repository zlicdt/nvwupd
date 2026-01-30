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

    public async Task<string> DownloadDriverAsync(
        DriverInfo driverInfo,
        DriverType driverType,
        IProgress<double>? progress = null)
    {
        var downloadUrl = GetDownloadUrl(driverInfo, driverType);
        var fileName = $"NVIDIA-Driver-{driverInfo.Version}-{driverType}.exe";
        var filePath = Path.Combine(_downloadDirectory, fileName);

        // Check if already downloaded
        if (File.Exists(filePath))
        {
            var existingSize = new FileInfo(filePath).Length;
            if (existingSize == driverInfo.FileSize || driverInfo.FileSize == 0)
            {
                progress?.Report(1.0);
                return filePath;
            }
            // Delete incomplete download
            File.Delete(filePath);
        }

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? driverInfo.FileSize;
        
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                progress?.Report((double)totalRead / totalBytes);
            }
        }

        progress?.Report(1.0);
        return filePath;
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
