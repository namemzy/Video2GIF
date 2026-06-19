using System.IO;
using System.Reflection;

namespace Video2GIF.Services;

/// <summary>
/// 从嵌入资源中提取 FFmpeg 到临时目录
/// </summary>
public static class FFmpegExtractor
{
    private static string? _extractedPath;

    /// <summary>
    /// 获取提取后的 FFmpeg 路径（首次调用时自动提取）
    /// </summary>
    public static string GetFFmpegPath()
    {
        if (_extractedPath != null && File.Exists(_extractedPath))
        {
            return _extractedPath;
        }

        string tempDir = Path.Combine(
            Path.GetTempPath(),
            "Video2GIF_ffmpeg");

        string exePath = Path.Combine(tempDir, "ffmpeg.exe");

        // 检查是否已提取且版本一致
        if (File.Exists(exePath))
        {
            _extractedPath = exePath;
            return exePath;
        }

        Directory.CreateDirectory(tempDir);

        // 从嵌入资源中提取
        var assembly = Assembly.GetExecutingAssembly();
        using var resourceStream = assembly.GetManifestResourceStream("Video2GIF.Resources.ffmpeg.exe");

        if (resourceStream == null)
        {
            throw new InvalidOperationException(
                "未找到内嵌的 FFmpeg 资源。请确认使用的是精简版或完整版构建。");
        }

        using var fileStream = new FileStream(exePath, FileMode.Create, FileAccess.Write);
        resourceStream.CopyTo(fileStream);

        _extractedPath = exePath;
        return exePath;
    }
}
