using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Video2GIF.Helpers;
using Video2GIF.Models;
using Video2GIF.Services;

namespace Video2GIF.ViewModels;

/// <summary>
/// 主窗口 ViewModel，管理所有 UI 交互逻辑
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    #region 私有字段

    private CancellationTokenSource? _convertCts;
    private ConvertSettings _settings = new();
    private Uri? _videoSource;
    private double _currentPosition;
    private string _durationText = "00:00:00";
    private string _positionText = "00:00:00";
    private double _convertProgress;
    private string _convertStatusText = "就绪";
    private bool _isConverting;
    private bool _hasVideo;
    private bool _isPlaying;
    private string _videoInfoText = string.Empty;
    private bool _showDropHint = true;

    #endregion

    #region 属性变更通知

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion

    #region 可观察属性

    /// <summary>
    /// 转换设置
    /// </summary>
    public ConvertSettings Settings
    {
        get => _settings;
        set
        {
            if (SetProperty(ref _settings, value))
            {
                // 设置变更时通知相关命令更新
                OnPropertyChanged(nameof(CanConvert));
            }
        }
    }

    /// <summary>
    /// 当前选中抖动算法的说明
    /// </summary>
    public string DitherDesc
    {
        get
        {
            foreach (var item in DitherOptions)
            {
                if (item.Value == Settings.DitherAlgorithm) return item.Desc;
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// 当前选中调色板模式的说明
    /// </summary>
    public string StatsModeDesc
    {
        get
        {
            foreach (var item in StatsModeOptions)
            {
                if (item.Value == Settings.PaletteStatsMode) return item.Desc;
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// 视频源路径（用于 MediaElement 绑定）
    /// </summary>
    public Uri? VideoSource
    {
        get => _videoSource;
        set => SetProperty(ref _videoSource, value);
    }

    /// <summary>
    /// 当前播放时间（秒）
    /// </summary>
    public double CurrentPosition
    {
        get => _currentPosition;
        set => SetProperty(ref _currentPosition, value);
    }

    /// <summary>
    /// 视频总时长显示文本
    /// </summary>
    public string DurationText
    {
        get => _durationText;
        set => SetProperty(ref _durationText, value);
    }

    /// <summary>
    /// 当前位置显示文本
    /// </summary>
    public string PositionText
    {
        get => _positionText;
        set => SetProperty(ref _positionText, value);
    }

    /// <summary>
    /// 转换进度百分比 (0-100)
    /// </summary>
    public double ConvertProgress
    {
        get => _convertProgress;
        set => SetProperty(ref _convertProgress, value);
    }

    /// <summary>
    /// 转换状态文本
    /// </summary>
    public string ConvertStatusText
    {
        get => _convertStatusText;
        set => SetProperty(ref _convertStatusText, value);
    }

    /// <summary>
    /// 是否正在转换
    /// </summary>
    public bool IsConverting
    {
        get => _isConverting;
        set
        {
            if (SetProperty(ref _isConverting, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// 是否已加载视频
    /// </summary>
    public bool HasVideo
    {
        get => _hasVideo;
        set
        {
            if (SetProperty(ref _hasVideo, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// 是否正在播放
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    /// <summary>
    /// 视频元数据信息显示
    /// </summary>
    public string VideoInfoText
    {
        get => _videoInfoText;
        set => SetProperty(ref _videoInfoText, value);
    }

    /// <summary>
    /// 拖拽提示是否可见
    /// </summary>
    public bool ShowDropHint
    {
        get => _showDropHint;
        set => SetProperty(ref _showDropHint, value);
    }

    #endregion

    #region 速度选项

    /// <summary>
    /// 可用的播放速度选项
    /// </summary>
    public double[] SpeedOptions { get; } = new double[] { 0.25, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0 };

    #endregion

    #region 算法选项

    /// <summary>
    /// 通用选项类，用于 ComboBox 绑定
    /// </summary>
    public class OptionItem
    {
        public string Value { get; }
        public string Label { get; }
        public string Desc { get; }
        public OptionItem(string value, string label, string desc)
        {
            Value = value;
            Label = label;
            Desc = desc;
        }
    }

    /// <summary>
    /// 抖动算法选项
    /// </summary>
    public OptionItem[] DitherOptions { get; } = new OptionItem[]
    {
        new("sierra2_4a",    "Sierra Lite",     "画质好，速度均衡（推荐）"),
        new("floyd_steinberg","Floyd-Steinberg", "经典算法，画质好"),
        new("sierra2",       "Sierra 2",        "画质更好，速度稍慢"),
        new("sierra3",       "Sierra 3",        "画质最好，速度最慢"),
        new("bayer",         "Bayer",           "速度最快，有规则纹理"),
        new("none",          "无抖动",           "无抖动处理，可能有色带"),
    };

    /// <summary>
    /// 调色板统计模式选项
    /// </summary>
    public OptionItem[] StatsModeOptions { get; } = new OptionItem[]
    {
        new("full",   "全局优化", "分析全部帧，调色板最准（推荐）"),
        new("diff",   "差异优化", "仅帧间差异，适合静态背景"),
        new("single", "单帧采样", "只分析首帧，速度最快"),
    };

    #endregion

    #region 命令

    /// <summary>
    /// 打开文件命令
    /// </summary>
    public ICommand OpenFileCommand { get; }

    /// <summary>
    /// 播放命令
    /// </summary>
    public ICommand PlayCommand { get; }

    /// <summary>
    /// 暂停命令
    /// </summary>
    public ICommand PauseCommand { get; }

    /// <summary>
    /// 停止命令
    /// </summary>
    public ICommand StopCommand { get; }

    /// <summary>
    /// 设置起始时间命令
    /// </summary>
    public ICommand SetStartTimeCommand { get; }

    /// <summary>
    /// 设置结束时间命令
    /// </summary>
    public ICommand SetEndTimeCommand { get; }

    /// <summary>
    /// 选择 FFmpeg 路径命令
    /// </summary>
    public ICommand SelectFFmpegPathCommand { get; }

    /// <summary>
    /// 选择输出路径命令
    /// </summary>
    public ICommand SelectOutputPathCommand { get; }

    /// <summary>
    /// 开始转换命令
    /// </summary>
    public ICommand ConvertCommand { get; }

    /// <summary>
    /// 取消转换命令
    /// </summary>
    public ICommand CancelConvertCommand { get; }

    #endregion

    #region 构造函数

    public MainViewModel()
    {
        OpenFileCommand = new RelayCommand(OpenFile);
        PlayCommand = new RelayCommand(Play);
        PauseCommand = new RelayCommand(Pause);
        StopCommand = new RelayCommand(Stop);
        SetStartTimeCommand = new RelayCommand(SetStartTime);
        SetEndTimeCommand = new RelayCommand(SetEndTime);
        SelectFFmpegPathCommand = new RelayCommand(SelectFFmpegPath);
        SelectOutputPathCommand = new RelayCommand(SelectOutputPath);
        ConvertCommand = new AsyncRelayCommand(ConvertAsync, () => CanConvert);
        CancelConvertCommand = new RelayCommand(CancelConvert);

        // 监听 Settings 属性变更，更新算法说明
        _settings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ConvertSettings.DitherAlgorithm))
            {
                OnPropertyChanged(nameof(DitherDesc));
            }
            else if (e.PropertyName == nameof(ConvertSettings.PaletteStatsMode))
            {
                OnPropertyChanged(nameof(StatsModeDesc));
            }
        };

        // 自动检测 FFmpeg 路径（带异常处理）
        try
        {
            string? detectedPath = FFmpegService.AutoDetectFFmpegPath();
            if (!string.IsNullOrEmpty(detectedPath))
            {
                Settings.FFmpegPath = detectedPath;
            }
        }
        catch (Exception ex)
        {
            // FFmpeg 自动检测失败不影响应用启动
            System.Diagnostics.Debug.WriteLine($"FFmpeg 自动检测失败: {ex.Message}");
        }
    }

    #endregion

    #region 命令实现

    /// <summary>
    /// 判断是否可以执行转换
    /// </summary>
    public bool CanConvert => HasVideo && !IsConverting;

    /// <summary>
    /// 打开文件
    /// </summary>
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择视频文件",
            Filter = "视频文件 (*.mp4;*.avi;*.mkv;*.mov;*.wmv)|*.mp4;*.avi;*.mkv;*.mov;*.wmv|MP4 文件 (*.mp4)|*.mp4|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            LoadVideo(dialog.FileName);
        }
    }

    /// <summary>
    /// 播放
    /// </summary>
    private void Play()
    {
        IsPlaying = true;
    }

    /// <summary>
    /// 暂停
    /// </summary>
    private void Pause()
    {
        IsPlaying = false;
    }

    /// <summary>
    /// 停止
    /// </summary>
    private void Stop()
    {
        IsPlaying = false;
        CurrentPosition = 0;
    }

    /// <summary>
    /// 设置起始时间为当前播放位置
    /// </summary>
    private void SetStartTime()
    {
        // 当 EndTime 为默认值 0 时，直接使用 CurrentPosition；否则取两者较小值
        Settings.StartTime = Settings.EndTime > 0
            ? Math.Min(CurrentPosition, Settings.EndTime)
            : CurrentPosition;
    }

    /// <summary>
    /// 设置结束时间为当前播放位置
    /// </summary>
    private void SetEndTime()
    {
        Settings.EndTime = Math.Max(CurrentPosition, Settings.StartTime);
    }

    /// <summary>
    /// 选择 FFmpeg 路径
    /// </summary>
    private void SelectFFmpegPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 FFmpeg 可执行文件",
            Filter = "FFmpeg (ffmpeg.exe)|ffmpeg.exe|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            Settings.FFmpegPath = dialog.FileName;
        }
    }

    /// <summary>
    /// 选择输出路径
    /// </summary>
    private void SelectOutputPath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "选择输出位置",
            Filter = "GIF 文件 (*.gif)|*.gif",
            DefaultExt = ".gif",
            FileName = Path.GetFileNameWithoutExtension(Settings.InputFilePath) + ".gif"
        };

        if (dialog.ShowDialog() == true)
        {
            Settings.OutputFilePath = dialog.FileName;
        }
    }

    /// <summary>
    /// 开始转换
    /// </summary>
    private async Task ConvertAsync()
    {
        // 验证 FFmpeg 路径
        if (string.IsNullOrWhiteSpace(Settings.FFmpegPath) || !File.Exists(Settings.FFmpegPath))
        {
            MessageBox.Show("请先设置有效的 FFmpeg 路径！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 验证输出路径
        if (string.IsNullOrWhiteSpace(Settings.OutputFilePath))
        {
            MessageBox.Show("请先设置输出文件路径！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 验证输出宽高
        if (Settings.OutputWidth <= 0 || Settings.OutputHeight <= 0)
        {
            MessageBox.Show("请先设置有效的输出分辨率！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsConverting = true;
        ConvertProgress = 0;
        ConvertStatusText = "正在转换...";
        _convertCts = new CancellationTokenSource();

        try
        {
            var ffmpegService = new FFmpegService(Settings.FFmpegPath);

            double duration = Settings.EndTime - Settings.StartTime;
            if (duration <= 0)
            {
                duration = Settings.TotalDuration - Settings.StartTime;
            }

            await ffmpegService.ConvertAsync(
                inputPath: Settings.InputFilePath,
                outputPath: Settings.OutputFilePath,
                width: Settings.OutputWidth,
                height: Settings.OutputHeight,
                fps: Settings.FrameRate,
                speed: Settings.Speed,
                startTime: Settings.StartTime,
                duration: duration,
                ditherAlgorithm: Settings.DitherAlgorithm,
                paletteStatsMode: Settings.PaletteStatsMode,
                progressCallback: (progress, timeStr) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ConvertProgress = progress;
                        ConvertStatusText = string.Format("转换中... {0:F1}%", progress);
                    });
                },
                cancellationToken: _convertCts.Token
            );

            ConvertProgress = 100;
            ConvertStatusText = "转换完成！";
            MessageBox.Show(
                string.Format("GIF 已成功生成！\n保存位置：{0}", Settings.OutputFilePath),
                "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            ConvertStatusText = "转换已取消";
            ConvertProgress = 0;
        }
        catch (Exception ex)
        {
            ConvertStatusText = "转换失败";
            ConvertProgress = 0;
            MessageBox.Show(string.Format("转换失败：{0}", ex.Message), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsConverting = false;
            _convertCts?.Dispose();
            _convertCts = null;
        }
    }

    /// <summary>
    /// 取消转换
    /// </summary>
    private void CancelConvert()
    {
        _convertCts?.Cancel();
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 加载视频文件（供拖拽和打开文件共用）
    /// </summary>
    /// <param name="filePath">视频文件路径</param>
    public async void LoadVideo(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            // 设置视频源
            Settings.InputFilePath = filePath;
            VideoSource = new Uri(filePath, UriKind.Absolute);
            HasVideo = true;
            ShowDropHint = false;

            // 自动生成输出路径
            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            Settings.OutputFilePath = Path.Combine(directory, string.Format("{0}.gif", fileName));

            // 读取视频元数据
            if (!string.IsNullOrWhiteSpace(Settings.FFmpegPath) && File.Exists(Settings.FFmpegPath))
            {
                try
                {
                    var ffmpegService = new FFmpegService(Settings.FFmpegPath);
                    var metadata = await ffmpegService.GetMetadataAsync(filePath);

                    Settings.OutputWidth = metadata.Width;
                    Settings.OutputHeight = metadata.Height;
                    Settings.TotalDuration = metadata.Duration;
                    Settings.EndTime = metadata.Duration;
                    Settings.AspectRatio = metadata.Width > 0 && metadata.Height > 0
                        ? (double)metadata.Width / metadata.Height
                        : 1.0;

                    VideoInfoText = string.Format(
                        "{0}x{1} | {2:F1}fps | {3}",
                        metadata.Width, metadata.Height,
                        metadata.FrameRate,
                        FormatTime(metadata.Duration));
                }
                catch (Exception ex)
                {
                    VideoInfoText = string.Format("无法读取视频信息: {0}", ex.Message);
                }
            }
            else
            {
                VideoInfoText = "请先设置 FFmpeg 路径以读取视频信息";
            }

            DurationText = FormatTime(Settings.TotalDuration);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format("加载视频失败：{0}", ex.Message), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 更新当前播放位置（由 View 调用）
    /// </summary>
    /// <param name="position">当前时间（秒）</param>
    public void UpdatePosition(double position)
    {
        CurrentPosition = position;
        PositionText = FormatTime(position);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 将秒数格式化为 HH:MM:SS 格式
    /// </summary>
    private static string FormatTime(double totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
    }

    #endregion
}
