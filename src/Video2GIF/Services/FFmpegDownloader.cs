using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Video2GIF.Services;

/// <summary>
/// FFmpeg 下载器，精简版首次运行时自动下载 essentials 构建
/// </summary>
public static class FFmpegDownloader
{
    // gyan.dev 提供的 essentials 构建，约 30MB（仅含常用编解码器）
    private const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    /// <summary>
    /// 获取应用数据目录下的 FFmpeg 存储路径
    /// </summary>
    private static string GetFFmpegDataDir()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Video2GIF", "ffmpeg");
    }

    /// <summary>
    /// 获取已下载的 FFmpeg 路径，未下载返回 null
    /// </summary>
    public static string? GetDownloadedFFmpegPath()
    {
        string exePath = Path.Combine(GetFFmpegDataDir(), "ffmpeg.exe");
        return File.Exists(exePath) ? exePath : null;
    }

    /// <summary>
    /// 检查是否已下载 FFmpeg
    /// </summary>
    public static bool IsDownloaded()
    {
        return GetDownloadedFFmpegPath() != null;
    }

    /// <summary>
    /// 下载 FFmpeg essentials 构建
    /// </summary>
    /// <param name="progressCallback">进度回调 (0-100, 状态文本)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载成功后 ffmpeg.exe 的路径</returns>
    public static async Task<string> DownloadAsync(
        Action<double, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        string dataDir = GetFFmpegDataDir();
        string zipPath = Path.Combine(dataDir, "ffmpeg.zip");
        string exePath = Path.Combine(dataDir, "ffmpeg.exe");

        // 如果已下载，直接返回
        if (File.Exists(exePath))
        {
            progressCallback?.Invoke(100, "已就绪");
            return exePath;
        }

        Directory.CreateDirectory(dataDir);

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        try
        {
            progressCallback?.Invoke(5, "正在连接下载服务器...");

            // 下载 ZIP
            using var response = await httpClient.GetAsync(DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();
            long? totalBytes = response.Content.Headers.ContentLength;

            progressCallback?.Invoke(10, "正在下载 FFmpeg...");

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 8192, true);

            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    double progress = 10 + (double)downloadedBytes / totalBytes.Value * 70;
                    string sizeText = string.Format("{0:F1} / {1:F1} MB",
                        downloadedBytes / 1048576.0, totalBytes.Value / 1048576.0);
                    progressCallback?.Invoke(progress, sizeText);
                }
            }

            await fileStream.FlushAsync(cancellationToken);

            progressCallback?.Invoke(80, "正在解压...");

            // 解压 ZIP，找到 ffmpeg.exe
            string? extractedExe = ExtractFFmpegFromZip(zipPath, dataDir);

            if (extractedExe == null || !File.Exists(extractedExe))
            {
                throw new InvalidOperationException("ZIP 包中未找到 ffmpeg.exe");
            }

            // 清理 ZIP
            try { File.Delete(zipPath); } catch { /* 忽略 */ }

            progressCallback?.Invoke(100, "下载完成");
            return extractedExe;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 清理失败的下载
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            throw new InvalidOperationException(string.Format("FFmpeg 下载失败: {0}", ex.Message), ex);
        }
    }

    /// <summary>
    /// 从 ZIP 包中提取 ffmpeg.exe 到目标目录
    /// </summary>
    private static string? ExtractFFmpegFromZip(string zipPath, string targetDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        foreach (var entry in archive.Entries)
        {
            // ZIP 内结构通常是 ffmpeg-x.x.x-essentials_build/bin/ffmpeg.exe
            if (entry.FullName.EndsWith("bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith("bin\\ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            {
                string destPath = Path.Combine(targetDir, "ffmpeg.exe");
                entry.ExtractToFile(destPath, overwrite: true);
                return destPath;
            }
        }

        return null;
    }
}
