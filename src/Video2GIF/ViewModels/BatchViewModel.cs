using System.Collections.ObjectModel;
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
/// 批量转换页面 ViewModel，管理批量转换的文件列表、预览和转换逻辑
/// </summary>
public class BatchViewModel : INotifyPropertyChanged
{
    #region 私有字段

    private CancellationTokenSource? _batchCts;
    private ConvertSettings _settings = new();
    private Uri? _videoSource;
    private double _currentPosition;
    private string _durationText = "00:00:00";
    private string _positionText = "00:00:00";
    private double _batchProgress;
    private string _batchStatusText = "就绪";
    private bool _isConverting;
    private bool _isPlaying;
    private string _videoInfoText = string.Empty;
    private FileItem? _selectedFileItem;
    private int _completedCount;
    private int _totalCount;
    private string _outputFolder = string.Empty;

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
    /// 文件列表
    /// </summary>
    public ObservableCollection<FileItem> FileItems { get; } = new();

    /// <summary>
    /// 转换设置（与单个转换共享模型）
    /// </summary>
    public ConvertSettings Settings
    {
        get => _settings;
        set
        {
            if (SetProperty(ref _settings, value))
            {
                OnPropertyChanged(nameof(CanStartBatch));
            }
        }
    }

    /// <summary>
    /// 输出目录（为空时使用每个视频的同目录）
    /// </summary>
    public string OutputFolder
    {
        get => _outputFolder;
        set => SetProperty(ref _outputFolder, value ?? string.Empty);
    }

    /// <summary>
    /// 当前选中的文件项
    /// </summary>
    public FileItem? SelectedFileItem
    {
        get => _selectedFileItem;
        set
        {
            if (SetProperty(ref _selectedFileItem, value))
            {
                LoadPreview(value);
            }
        }
    }

    /// <summary>
    /// 视频源路径（用于 MediaElement 绑定）
    /// </summary>
    public Uri? VideoSource
    {
        get => _videoSource;
        set
        {
            if (SetProperty(ref _videoSource, value))
            {
                OnPropertyChanged(nameof(HasVideoSource));
            }
        }
    }

    /// <summary>
    /// 是否有视频源（用于控制无预览提示的显示）
    /// </summary>
    public bool HasVideoSource => VideoSource != null;

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
    /// 批量转换整体进度百分比 (0-100)
    /// </summary>
    public double BatchProgress
    {
        get => _batchProgress;
        set => SetProperty(ref _batchProgress, value);
    }

    /// <summary>
    /// 批量转换状态文本
    /// </summary>
    public string BatchStatusText
    {
        get => _batchStatusText;
        set => SetProperty(ref _batchStatusText, value);
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
                OnPropertyChanged(nameof(CanStartBatch));
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
    /// 是否有文件在列表中
    /// </summary>
    public bool HasFiles => FileItems.Count > 0;

    /// <summary>
    /// 是否可以开始批量转换
    /// </summary>
    public bool CanStartBatch => HasFiles && !IsConverting;

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
    /// 添加文件命令
    /// </summary>
    public ICommand AddFilesCommand { get; }

    /// <summary>
    /// 清空文件列表命令
    /// </summary>
    public ICommand ClearFilesCommand { get; }

    /// <summary>
    /// 删除单个文件命令
    /// </summary>
    public ICommand RemoveFileCommand { get; }

    /// <summary>
    /// 开始批量转换命令
    /// </summary>
    public ICommand StartBatchConvertCommand { get; }

    /// <summary>
    /// 取消批量转换命令
    /// </summary>
    public ICommand CancelBatchConvertCommand { get; }

    /// <summary>
    /// 选择输出目录命令
    /// </summary>
    public ICommand SelectOutputFolderCommand { get; }

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

    #endregion

    #region 构造函数

    public BatchViewModel()
    {
        AddFilesCommand = new RelayCommand(AddFiles);
        ClearFilesCommand = new RelayCommand(ClearFiles, () => HasFiles && !IsConverting);
        RemoveFileCommand = new RelayCommand(RemoveFile);
        StartBatchConvertCommand = new AsyncRelayCommand(StartBatchConvertAsync, () => CanStartBatch);
        CancelBatchConvertCommand = new RelayCommand(CancelBatchConvert);
        SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
        PlayCommand = new RelayCommand(Play);
        PauseCommand = new RelayCommand(Pause);
        StopCommand = new RelayCommand(Stop);

        // 监听文件列表变化
        FileItems.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(CanStartBatch));
        };

        // 从内嵌资源提取 FFmpeg
        try
        {
            Settings.FFmpegPath = FFmpegExtractor.GetFFmpegPath();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FFmpeg 提取失败: {ex.Message}");
        }
    }

    #endregion

    #region 命令实现

    /// <summary>
    /// 添加文件（多选）
    /// </summary>
    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择视频文件",
            Filter = "视频文件 (*.mp4;*.avi;*.mkv;*.mov;*.wmv)|*.mp4;*.avi;*.mkv;*.mov;*.wmv|MP4 文件 (*.mp4)|*.mp4|所有文件 (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (string filePath in dialog.FileNames)
            {
                // 避免重复添加
                bool exists = false;
                foreach (var item in FileItems)
                {
                    if (string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    FileItems.Add(new FileItem
                    {
                        FilePath = filePath,
                        Status = FileItemStatus.Waiting
                    });
                }
            }
        }
    }

    /// <summary>
    /// 清空文件列表
    /// </summary>
    private void ClearFiles()
    {
        FileItems.Clear();
        SelectedFileItem = null;
        VideoSource = null;
        VideoInfoText = string.Empty;
        DurationText = "00:00:00";
        PositionText = "00:00:00";
        CurrentPosition = 0;
    }

    /// <summary>
    /// 删除单个文件
    /// </summary>
    private void RemoveFile(object? parameter)
    {
        if (parameter is FileItem fileItem)
        {
            if (SelectedFileItem == fileItem)
            {
                SelectedFileItem = null;
                VideoSource = null;
                VideoInfoText = string.Empty;
            }
            FileItems.Remove(fileItem);
        }
    }

    /// <summary>
    /// 选择输出目录
    /// </summary>
    private void SelectOutputFolder()
    {
        var dialog = new FolderDialog();
        if (dialog.ShowDialog())
        {
            OutputFolder = dialog.SelectedPath ?? string.Empty;
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
    /// 开始批量转换
    /// </summary>
    private async Task StartBatchConvertAsync()
    {
        if (FileItems.Count == 0)
        {
            MessageBox.Show("请先添加视频文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 确保 FFmpeg 已提取
        if (string.IsNullOrWhiteSpace(Settings.FFmpegPath) || !File.Exists(Settings.FFmpegPath))
        {
            try
            {
                Settings.FFmpegPath = FFmpegExtractor.GetFFmpegPath();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("FFmpeg 初始化失败：{0}", ex.Message),
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        // 验证输出宽高
        if (Settings.OutputWidth <= 0 || Settings.OutputHeight <= 0)
        {
            MessageBox.Show("请先设置有效的输出分辨率！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsConverting = true;
        _batchCts = new CancellationTokenSource();
        _totalCount = FileItems.Count;
        _completedCount = 0;
        BatchProgress = 0;

        try
        {
            var ffmpegService = new FFmpegService(Settings.FFmpegPath);

            for (int i = 0; i < FileItems.Count; i++)
            {
                _batchCts.Token.ThrowIfCancellationRequested();

                var fileItem = FileItems[i];

                // 重置状态
                fileItem.Status = FileItemStatus.Converting;
                fileItem.Progress = 0;
                fileItem.ErrorMessage = null;

                // 生成输出路径
                string directory;
                if (!string.IsNullOrWhiteSpace(OutputFolder) && Directory.Exists(OutputFolder))
                {
                    // 使用用户指定的输出目录
                    directory = OutputFolder;
                }
                else
                {
                    // 使用视频同目录
                    directory = Path.GetDirectoryName(fileItem.FilePath) ?? string.Empty;
                }
                string fileName = Path.GetFileNameWithoutExtension(fileItem.FilePath);
                string outputPath = Path.Combine(directory, string.Format("{0}.gif", fileName));

                BatchStatusText = string.Format("转换中 {0}/{1}...", i + 1, _totalCount);

                try
                {
                    // 先读取该视频的元数据以获取时长
                    var metadata = await ffmpegService.GetMetadataAsync(fileItem.FilePath);
                    double duration = metadata.Duration;

                    // 使用当前 Settings 的输出分辨率（用户设置的统一尺寸）
                    await ffmpegService.ConvertAsync(
                        inputPath: fileItem.FilePath,
                        outputPath: outputPath,
                        width: Settings.OutputWidth,
                        height: Settings.OutputHeight,
                        fps: Settings.FrameRate,
                        speed: Settings.Speed,
                        startTime: 0.0,
                        duration: duration,
                        ditherAlgorithm: Settings.DitherAlgorithm,
                        paletteStatsMode: Settings.PaletteStatsMode,
                        progressCallback: (progress, timeStr) =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                fileItem.Progress = progress;
                                fileItem.OnPropertyChanged(nameof(FileItem.StatusText));
                                fileItem.OnPropertyChanged(nameof(FileItem.ProgressText));

                                // 更新整体进度
                                double overallProgress = ((_completedCount + progress / 100.0) / _totalCount) * 100.0;
                                BatchProgress = overallProgress;
                                BatchStatusText = string.Format("转换中 {0}/{1}... {2:F0}%", _completedCount + 1, _totalCount, overallProgress);
                            });
                        },
                        cancellationToken: _batchCts.Token
                    );

                    // 标记完成
                    fileItem.Status = FileItemStatus.Done;
                    fileItem.Progress = 100;
                    _completedCount++;
                }
                catch (OperationCanceledException)
                {
                    fileItem.Status = FileItemStatus.Waiting;
                    fileItem.Progress = 0;
                    throw; // 向上传递取消
                }
                catch (Exception ex)
                {
                    fileItem.Status = FileItemStatus.Failed;
                    fileItem.ErrorMessage = ex.Message;
                    // 失败后继续下一个
                }
            }

            // 全部完成
            BatchProgress = 100;
            BatchStatusText = string.Format("全部完成！成功 {0}/{1}", _completedCount, _totalCount);

            // 统计成功和失败数量
            int failedCount = 0;
            foreach (var item in FileItems)
            {
                if (item.Status == FileItemStatus.Failed) failedCount++;
            }

            if (failedCount == 0)
            {
                MessageBox.Show(
                    string.Format("批量转换完成！共转换 {0} 个文件。", _completedCount),
                    "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    string.Format("批量转换完成！成功 {0} 个，失败 {1} 个。", _completedCount, failedCount),
                    "完成", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            BatchStatusText = "转换已取消";
            BatchProgress = 0;
        }
        catch (Exception ex)
        {
            BatchStatusText = "转换失败";
            BatchProgress = 0;
            MessageBox.Show(string.Format("批量转换失败：{0}", ex.Message), "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsConverting = false;
            _batchCts?.Dispose();
            _batchCts = null;
        }
    }

    /// <summary>
    /// 取消批量转换
    /// </summary>
    private void CancelBatchConvert()
    {
        _batchCts?.Cancel();
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 加载视频预览（选中文件时调用）
    /// </summary>
    /// <param name="fileItem">选中的文件项，null 则清空预览</param>
    public async void LoadPreview(FileItem? fileItem)
    {
        if (fileItem == null || string.IsNullOrEmpty(fileItem.FilePath) || !File.Exists(fileItem.FilePath))
        {
            VideoSource = null;
            VideoInfoText = string.Empty;
            DurationText = "00:00:00";
            PositionText = "00:00:00";
            CurrentPosition = 0;
            return;
        }

        try
        {
            VideoSource = new Uri(fileItem.FilePath, UriKind.Absolute);
            IsPlaying = false;
            CurrentPosition = 0;

            // 确保 FFmpeg 已提取
            if (string.IsNullOrWhiteSpace(Settings.FFmpegPath) || !File.Exists(Settings.FFmpegPath))
            {
                Settings.FFmpegPath = FFmpegExtractor.GetFFmpegPath();
            }

            // 读取视频元数据
            try
            {
                var ffmpegService = new FFmpegService(Settings.FFmpegPath);
                var metadata = await ffmpegService.GetMetadataAsync(fileItem.FilePath);

                // 如果用户尚未设置输出分辨率，使用第一个视频的分辨率作为默认值
                if (Settings.OutputWidth <= 0 || Settings.OutputHeight <= 0)
                {
                    Settings.OutputWidth = metadata.Width;
                    Settings.OutputHeight = metadata.Height;
                }

                // 更新宽高比，用于锁定宽高比功能
                Settings.AspectRatio = metadata.Width > 0 && metadata.Height > 0
                    ? (double)metadata.Width / metadata.Height
                    : 0.0;

                double totalDuration = metadata.Duration;

                VideoInfoText = string.Format(
                    "{0}x{1} | {2:F1}fps | {3}",
                    metadata.Width, metadata.Height,
                    metadata.FrameRate,
                    FormatTime(totalDuration));

                DurationText = FormatTime(totalDuration);
            }
            catch (Exception ex)
            {
                VideoInfoText = string.Format("无法读取视频信息: {0}", ex.Message);
                DurationText = "00:00:00";
            }
        }
        catch (Exception ex)
        {
            VideoInfoText = string.Format("加载预览失败：{0}", ex.Message);
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

    /// <summary>
    /// 将秒数格式化为 HH:MM:SS 格式
    /// </summary>
    public static string FormatTime(double totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
    }

    #endregion
}
