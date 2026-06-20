using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Video2GIF.Models;

/// <summary>
/// 视频转GIF的转换设置模型
/// </summary>
public class ConvertSettings : INotifyPropertyChanged
{
    private int _outputWidth = 0;
    private int _outputHeight = 0;
    private bool _lockAspectRatio = false;
    private double _aspectRatio = 0.0;  // 0 表示未设置，加载视频后自动填充
    private int _frameRate = 10;
    private double _speed = 1.0;
    private double _startTime = 0.0;
    private double _endTime = 0.0;
    private double _totalDuration = 0.0;
    private string _ffmpegPath = string.Empty;
    private string _inputFilePath = string.Empty;
    private string _outputFilePath = string.Empty;
    private bool _suppressRatioSync = false;
    private string _ditherAlgorithm = "sierra2_4a";
    private string _paletteStatsMode = "full";

    /// <summary>属性变更通知事件</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发属性变更通知
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 设置属性值并触发通知
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 输出宽度（像素）
    /// </summary>
    public int OutputWidth
    {
        get => _outputWidth;
        set
        {
            if (SetProperty(ref _outputWidth, value))
            {
                if (LockAspectRatio && AspectRatio > 0 && value > 0 && !_suppressRatioSync)
                {
                    _suppressRatioSync = true;
                    // 确保宽度为偶数（FFmpeg 要求）
                    int adjustedWidth = value % 2 == 0 ? value : value + 1;
                    int newHeight = (int)(adjustedWidth / AspectRatio);
                    newHeight = newHeight % 2 == 0 ? newHeight : newHeight + 1;
                    if (newHeight > 0)
                    {
                        OutputHeight = newHeight;
                    }
                    _suppressRatioSync = false;
                }
            }
        }
    }

    /// <summary>
    /// 输出高度（像素）
    /// </summary>
    public int OutputHeight
    {
        get => _outputHeight;
        set
        {
            if (SetProperty(ref _outputHeight, value))
            {
                if (LockAspectRatio && AspectRatio > 0 && value > 0 && !_suppressRatioSync)
                {
                    _suppressRatioSync = true;
                    // 确保高度为偶数
                    int adjustedHeight = value % 2 == 0 ? value : value + 1;
                    int newWidth = (int)(adjustedHeight * AspectRatio);
                    newWidth = newWidth % 2 == 0 ? newWidth : newWidth + 1;
                    if (newWidth > 0)
                    {
                        OutputWidth = newWidth;
                    }
                    _suppressRatioSync = false;
                }
            }
        }
    }

    /// <summary>
    /// 是否锁定宽高比
    /// </summary>
    public bool LockAspectRatio
    {
        get => _lockAspectRatio;
        set => SetProperty(ref _lockAspectRatio, value);
    }

    /// <summary>
    /// 原始视频宽高比
    /// </summary>
    public double AspectRatio
    {
        get => _aspectRatio;
        set => SetProperty(ref _aspectRatio, value);
    }

    /// <summary>
    /// GIF 帧率（1-30）
    /// </summary>
    public int FrameRate
    {
        get => _frameRate;
        set => SetProperty(ref _frameRate, Math.Clamp(value, 1, 30));
    }

    /// <summary>
    /// 播放速度倍率
    /// </summary>
    public double Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    /// <summary>
    /// 起始时间（秒）
    /// </summary>
    public double StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, Math.Max(0, value));
    }

    /// <summary>
    /// 结束时间（秒）
    /// </summary>
    public double EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, Math.Max(0, value));
    }

    /// <summary>
    /// 视频总时长（秒）
    /// </summary>
    public double TotalDuration
    {
        get => _totalDuration;
        set => SetProperty(ref _totalDuration, value);
    }

    /// <summary>
    /// FFmpeg 可执行文件路径
    /// </summary>
    public string FFmpegPath
    {
        get => _ffmpegPath;
        set => SetProperty(ref _ffmpegPath, value ?? string.Empty);
    }

    /// <summary>
    /// 输入视频文件路径
    /// </summary>
    public string InputFilePath
    {
        get => _inputFilePath;
        set => SetProperty(ref _inputFilePath, value ?? string.Empty);
    }

    /// <summary>
    /// 输出 GIF 文件路径
    /// </summary>
    public string OutputFilePath
    {
        get => _outputFilePath;
        set => SetProperty(ref _outputFilePath, value ?? string.Empty);
    }

    /// <summary>
    /// 抖动算法（none, floyd_steinberg, sierra2_4a, sierra2, sierra3, bayer）
    /// </summary>
    public string DitherAlgorithm
    {
        get => _ditherAlgorithm;
        set => SetProperty(ref _ditherAlgorithm, value ?? "sierra2_4a");
    }

    /// <summary>
    /// 调色板统计模式（full, diff, single）
    /// </summary>
    public string PaletteStatsMode
    {
        get => _paletteStatsMode;
        set => SetProperty(ref _paletteStatsMode, value ?? "full");
    }
}
