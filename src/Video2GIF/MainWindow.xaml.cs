using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Video2GIF.ViewModels;

namespace Video2GIF;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private bool _isDraggingSlider;

    public MainWindow()
    {
        InitializeComponent();

        // 创建定时器，用于更新播放进度
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += Timer_Tick;

        // 窗口加载完成后初始化
        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// 窗口加载完成事件
    /// </summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 绑定 ViewModel 的属性变更事件
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    /// <summary>
    /// ViewModel 属性变更事件处理
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsPlaying):
                if (viewModel.IsPlaying)
                {
                    mediaPlayer.Play();
                    _timer.Start();
                }
                else
                {
                    mediaPlayer.Pause();
                    _timer.Stop();
                }
                break;

            case nameof(MainViewModel.CurrentPosition):
                if (!_isDraggingSlider)
                {
                    mediaPlayer.Position = TimeSpan.FromSeconds(viewModel.CurrentPosition);
                }
                break;

            case nameof(MainViewModel.VideoSource):
                // 视频源改变后，等待 MediaOpened 事件
                break;
        }
    }

    /// <summary>
    /// 定时器 Tick 事件 - 更新播放进度
    /// </summary>
    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel && !_isDraggingSlider)
        {
            viewModel.UpdatePosition(mediaPlayer.Position.TotalSeconds);
        }
    }

    /// <summary>
    /// MediaElement 打开视频完成事件
    /// </summary>
    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            // 使用 MediaElement 的自然视频尺寸作为分辨率回退
            if (mediaPlayer.NaturalVideoWidth > 0 && mediaPlayer.NaturalVideoHeight > 0)
            {
                // 仅当 FFmpeg 元数据未能读取到分辨率时才覆盖（OutputWidth 仍为默认值 0）
                if (viewModel.Settings.OutputWidth <= 0 || viewModel.Settings.OutputHeight <= 0)
                {
                    viewModel.Settings.OutputWidth = mediaPlayer.NaturalVideoWidth;
                    viewModel.Settings.OutputHeight = mediaPlayer.NaturalVideoHeight;
                    viewModel.Settings.AspectRatio = (double)mediaPlayer.NaturalVideoWidth / mediaPlayer.NaturalVideoHeight;
                }
            }

            // 如果元数据未能读取到时长，使用 MediaElement 的自然时长
            if (viewModel.Settings.TotalDuration <= 0 && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                viewModel.Settings.TotalDuration = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                viewModel.Settings.EndTime = viewModel.Settings.TotalDuration;
                viewModel.DurationText = FormatTime(viewModel.Settings.TotalDuration);
            }
        }
    }

    /// <summary>
    /// MediaElement 播放结束事件
    /// </summary>
    private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsPlaying = false;
            mediaPlayer.Position = TimeSpan.Zero;
            viewModel.UpdatePosition(0);
        }
    }

    /// <summary>
    /// 进度条值改变事件（用户拖动时）
    /// </summary>
    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DataContext is MainViewModel viewModel && _isDraggingSlider)
        {
            viewModel.UpdatePosition(e.NewValue);
        }
    }

    /// <summary>
    /// 进度条开始拖动
    /// </summary>
    private void ProgressSlider_MouseLeftButtonDown(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    /// <summary>
    /// 进度条结束拖动
    /// </summary>
    private void ProgressSlider_MouseLeftButtonUp(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _isDraggingSlider = false;
        if (DataContext is MainViewModel viewModel)
        {
            mediaPlayer.Position = TimeSpan.FromSeconds(viewModel.CurrentPosition);
        }
    }

    /// <summary>
    /// 窗口拖拽进入事件
    /// </summary>
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>
    /// 窗口拖拽释放事件 - 加载拖入的文件
    /// </summary>
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                string filePath = files[0];
                // 检查文件扩展名
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".mp4" || ext == ".avi" || ext == ".mkv" || ext == ".mov" || ext == ".wmv")
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.LoadVideo(filePath);
                    }
                }
                else
                {
                    MessageBox.Show(
                        "不支持的文件格式，请拖入视频文件（MP4, AVI, MKV, MOV, WMV）。",
                        "格式不支持", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    /// <summary>
    /// 格式化时间为 HH:MM:SS
    /// </summary>
    private static string FormatTime(double totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
    }
}
