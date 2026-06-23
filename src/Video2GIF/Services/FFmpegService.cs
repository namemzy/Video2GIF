using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;

namespace Video2GIF.Services;

/// <summary>
/// FFmpeg 服务，封装 FFmpeg 命令行操作
/// </summary>
public class FFmpegService
{
    private readonly string _ffmpegPath;

    // 预编译正则表达式，用于解析 FFmpeg 输出
    private static readonly Regex ResolutionRegex = new(@"(\d{2,5})\s*x\s*(\d{2,5})", RegexOptions.Compiled);
    private static readonly Regex DurationRegex = new(@"Duration:\s*(\d{2}:\d{2}:\d{2}\.\d{2})", RegexOptions.Compiled);
    private static readonly Regex FpsRegex = new(@"([\d.]+)\s*fps", RegexOptions.Compiled);
    private static readonly Regex ProgressTimeRegex = new(@"time=\s*(\d{2}:\d{2}:\d{2}\.\d{2})", RegexOptions.Compiled);

    // stderr 输出截取长度限制（字符数）
    private const int MaxStderrLength = 2000;

    /// <summary>
    /// 转换进度回调委托
    /// </summary>
    /// <param name="progress">进度百分比 (0-100)</param>
    /// <param name="timeStr">当前处理时间字符串</param>
    public delegate void ProgressCallback(double progress, string timeStr);

    /// <summary>
    /// 初始化 FFmpeg 服务
    /// </summary>
    /// <param name="ffmpegPath">FFmpeg 可执行文件路径</param>
    public FFmpegService(string ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            throw new ArgumentException("FFmpeg 路径不能为空", nameof(ffmpegPath));
        }
        _ffmpegPath = ffmpegPath;
    }

    /// <summary>
    /// 自动检测 FFmpeg 路径
    /// 优先级：已下载的 > 应用目录 > 进程目录 > 系统 PATH
    /// </summary>
    /// <returns>FFmpeg 可执行文件路径，未找到返回 null</returns>
    public static string? AutoDetectFFmpegPath()
    {
        // 0. 检查之前下载的 FFmpeg
        string? downloadedPath = FFmpegDownloader.GetDownloadedFFmpegPath();
        if (!string.IsNullOrEmpty(downloadedPath))
        {
            return downloadedPath;
        }

        // 1. 从应用程序目录查找
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string bundledPath = Path.Combine(appDir, "ffmpeg.exe");
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }

        // 2. 从当前进程目录查找
        string? processDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(processDir))
        {
            string processPath = Path.Combine(processDir, "ffmpeg.exe");
            if (File.Exists(processPath))
            {
                return processPath;
            }
        }

        // 3. 从系统 PATH 环境变量查找
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    string fullPath = Path.Combine(dir.Trim(), "ffmpeg.exe");
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    // 忽略无效路径
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 从视频文件中提取元数据信息
    /// </summary>
    /// <param name="filePath">视频文件路径</param>
    /// <returns>视频元数据（宽度、高度、时长、帧率）</returns>
    public async Task<VideoMetadata> GetMetadataAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("视频文件不存在", filePath);
        }

        var stdErrBuffer = new StringBuilder();

        await Cli.Wrap(_ffmpegPath)
            .WithArguments(new[] { "-i", filePath, "-hide_banner" })
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        // FFmpeg 将信息输出到 stderr
        string output = stdErrBuffer.ToString();

        var metadata = new VideoMetadata();

        // 解析分辨率 - 匹配如 "1920x1080" 或 "1920 x 1080"
        var resolutionMatch = ResolutionRegex.Match(output);
        if (resolutionMatch.Success)
        {
            int w, h;
            int.TryParse(resolutionMatch.Groups[1].Value, out w);
            int.TryParse(resolutionMatch.Groups[2].Value, out h);
            metadata.Width = w;
            metadata.Height = h;
        }

        // 解析时长 - 匹配如 "Duration: 00:01:23.45"
        var durationMatch = DurationRegex.Match(output);
        if (durationMatch.Success)
        {
            metadata.Duration = ParseTimeSpan(durationMatch.Groups[1].Value).TotalSeconds;
        }

        // 解析帧率 - 匹配如 "30 fps" 或 "29.97 fps"
        var fpsMatch = FpsRegex.Match(output);
        if (fpsMatch.Success)
        {
            double fps;
            double.TryParse(fpsMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out fps);
            metadata.FrameRate = fps;
        }

        return metadata;
    }

    /// <summary>
    /// 执行 MP4 转 GIF 转换
    /// 使用两步法：先生成调色板，再用调色板编码 GIF，确保高质量输出
    /// </summary>
    /// <param name="inputPath">输入视频路径</param>
    /// <param name="outputPath">输出 GIF 路径</param>
    /// <param name="width">输出宽度</param>
    /// <param name="height">输出高度</param>
    /// <param name="fps">帧率</param>
    /// <param name="speed">播放速度</param>
    /// <param name="startTime">起始时间（秒）</param>
    /// <param name="duration">持续时长（秒）</param>
    /// <param name="ditherAlgorithm">抖动算法</param>
    /// <param name="paletteStatsMode">调色板统计模式</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ConvertAsync(
        string inputPath,
        string outputPath,
        int width,
        int height,
        int fps,
        double speed,
        double startTime,
        double duration,
        string ditherAlgorithm,
        string paletteStatsMode,
        ProgressCallback? progressCallback,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("输入视频文件不存在", inputPath);
        }

        // 确保输出目录存在
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 确保宽高为偶数（FFmpeg 要求）
        int adjustedWidth = width % 2 == 0 ? width : width + 1;
        int adjustedHeight = height % 2 == 0 ? height : height + 1;

        // 构建基础视频滤镜（scale + fps + 可选速度调整）
        var filterParts = new List<string>
        {
            string.Format("scale={0}:{1}", adjustedWidth, adjustedHeight),
            string.Format("fps={0}", fps)
        };

        // 仅在速度不为 1.0 时添加 setpts
        if (Math.Abs(speed - 1.0) > 0.001 && speed > 0)
        {
            filterParts.Add(string.Format("setpts=PTS/{0:F2}", speed));
        }

        string baseFilter = string.Join(",", filterParts);

        // ===== 两步法转换 =====
        // 第一步：生成调色板
        // 第二步：使用调色板编码 GIF
        string tempPalette = Path.Combine(
            Path.GetTempPath(),
            string.Format("v2g_palette_{0}.png", Guid.NewGuid().ToString("N")));

        try
        {
            // ---- 第一步：生成调色板 ----
            string paletteVf = string.Format("{0},palettegen=stats_mode={1}", baseFilter, paletteStatsMode);

            // -t 放在 -i 之后（输入端），限制读取的原视频时长，
            // 不受 setpts 速度滤镜影响时间轴映射
            var paletteArgs = new[]
            {
                "-y",
                "-ss", startTime.ToString("F3", CultureInfo.InvariantCulture),
                "-t", duration.ToString("F3", CultureInfo.InvariantCulture),
                "-i", inputPath,
                "-vf", paletteVf,
                "-update", "1",
                tempPalette
            };

            progressCallback?.Invoke(10.0, "生成调色板...");

            string paletteStderr = await RunFFmpegAsync(paletteArgs, cancellationToken);

            if (!File.Exists(tempPalette))
            {
                string detail = GetStderrTail(paletteStderr);
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(detail)
                        ? "FFmpeg 调色板生成失败"
                        : string.Format("FFmpeg 调色板生成失败:\n{0}", detail));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // ---- 第二步：使用调色板编码 GIF ----
            string gifVf = string.Format("{0}[x];[x][1:v]paletteuse=dither={1}", baseFilter, ditherAlgorithm);

            // 同样将 -t 放在输入端，限制读取原视频时长
            // 第二个输入（调色板）不受 -t 影响
            var gifArgs = new[]
            {
                "-y",
                "-ss", startTime.ToString("F3", CultureInfo.InvariantCulture),
                "-t", duration.ToString("F3", CultureInfo.InvariantCulture),
                "-i", inputPath,
                "-i", tempPalette,
                "-lavfi", gifVf,
                "-loop", "0",
                outputPath
            };

            progressCallback?.Invoke(30.0, "编码 GIF...");

            // 使用 stderr 实时解析进度
            double lastProgress = 30.0;
            var stderrBuffer = new StringBuilder();

            var result = await Cli.Wrap(_ffmpegPath)
                .WithArguments(gifArgs)
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    lock (stderrBuffer)
                    {
                        stderrBuffer.AppendLine(line);
                    }

                    var timeMatch = ProgressTimeRegex.Match(line);
                    if (timeMatch.Success && duration > 0)
                    {
                        double currentTime = ParseTimeSpan(timeMatch.Groups[1].Value).TotalSeconds;
                        // 输出时间轴已受 setpts 影响，实际输出时长 = duration / speed
                        double outputDuration = speed > 0 ? duration / speed : duration;
                        double progress = Math.Min(99.0, 30.0 + (currentTime / outputDuration) * 69.0);
                        if (progress > lastProgress)
                        {
                            lastProgress = progress;
                            progressCallback?.Invoke(progress, timeMatch.Groups[1].Value);
                        }
                    }
                }))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken);

            if (result.ExitCode != 0)
            {
                string stderrTail = GetStderrTail(stderrBuffer.ToString());
                string errorMessage = string.IsNullOrEmpty(stderrTail)
                    ? string.Format("FFmpeg GIF 编码失败，退出码: {0}", result.ExitCode)
                    : string.Format("FFmpeg GIF 编码失败，退出码: {0}\n\nFFmpeg 输出:\n{1}", result.ExitCode, stderrTail);
                throw new InvalidOperationException(errorMessage);
            }

            progressCallback?.Invoke(100.0, "完成");
        }
        finally
        {
            // 清理临时调色板文件
            try
            {
                if (File.Exists(tempPalette))
                {
                    File.Delete(tempPalette);
                }
            }
            catch
            {
                // 忽略清理失败
            }
        }
    }

    /// <summary>
    /// 运行 FFmpeg 并返回完整 stderr 输出
    /// </summary>
    private async Task<string> RunFFmpegAsync(string[] args, CancellationToken cancellationToken)
    {
        var stderrBuffer = new StringBuilder();

        await Cli.Wrap(_ffmpegPath)
            .WithArguments(args)
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderrBuffer))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken);

        return stderrBuffer.ToString();
    }

    /// <summary>
    /// 获取 stderr 输出的末尾部分，用于错误诊断
    /// 返回最后 MaxStderrLength 个字符，并尝试截取到最近的完整行
    /// </summary>
    /// <param name="fullStderr">完整的 stderr 输出</param>
    /// <returns>截取后的 stderr 末尾内容</returns>
    private static string GetStderrTail(string fullStderr)
    {
        if (string.IsNullOrEmpty(fullStderr))
        {
            return string.Empty;
        }

        // 如果输出长度在限制范围内，直接返回
        if (fullStderr.Length <= MaxStderrLength)
        {
            return fullStderr.Trim();
        }

        // 截取末尾 MaxStderrLength 个字符
        string tail = fullStderr.Substring(fullStderr.Length - MaxStderrLength);

        // 尝试从第一个换行符之后开始，避免截断行
        int firstNewline = tail.IndexOf('\n');
        if (firstNewline >= 0 && firstNewline < tail.Length - 1)
        {
            tail = tail.Substring(firstNewline + 1);
        }

        return tail.Trim();
    }

    /// <summary>
    /// 解析时间字符串为 TimeSpan
    /// 支持格式: HH:MM:SS.ff
    /// </summary>
    private static TimeSpan ParseTimeSpan(string timeStr)
    {
        if (TimeSpan.TryParseExact(timeStr, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture, out var ts))
        {
            return ts;
        }
        if (TimeSpan.TryParse(timeStr, CultureInfo.InvariantCulture, out ts))
        {
            return ts;
        }
        return TimeSpan.Zero;
    }

    /// <summary>
    /// 视频元数据结构
    /// </summary>
    public class VideoMetadata
    {
        /// <summary>视频宽度（像素）</summary>
        public int Width { get; set; }

        /// <summary>视频高度（像素）</summary>
        public int Height { get; set; }

        /// <summary>视频时长（秒）</summary>
        public double Duration { get; set; }

        /// <summary>视频帧率</summary>
        public double FrameRate { get; set; }
    }
}
