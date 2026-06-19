using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Video2GIF.ViewModels;

namespace Video2GIF.Views;

/// <summary>
/// BatchConvertView.xaml 的交互逻辑
/// </summary>
public partial class BatchConvertView : UserControl
{
    private readonly DispatcherTimer _timer;
    private bool _isDraggingSlider;

    public BatchConvertView()
    {
        InitializeComponent();

        // 创建定时器，用于更新播放进度
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += Timer_Tick;

        // 加载完成后初始化
        Loaded += BatchConvertView_Loaded;
        Unloaded += BatchConvertView_Unloaded;
    }

    /// <summary>
    /// 控件加载完成事件
    /// </summary>
    private void BatchConvertView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BatchViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    /// <summary>
    /// 控件卸载事件
    /// </summary>
    private void BatchConvertView_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        if (DataContext is BatchViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    /// <summary>
    /// ViewModel 属性变更事件处理
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is not BatchViewModel viewModel) return;

        switch (e.PropertyName)
        {
            case nameof(BatchViewModel.IsPlaying):
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

            case nameof(BatchViewModel.CurrentPosition):
                if (!_isDraggingSlider)
                {
                    mediaPlayer.Position = TimeSpan.FromSeconds(viewModel.CurrentPosition);
                }
                break;

            case nameof(BatchViewModel.VideoSource):
                // 视频源改变后，等待 MediaOpened 事件
                break;
        }
    }

    /// <summary>
    /// 定时器 Tick 事件 - 更新播放进度
    /// </summary>
    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is BatchViewModel viewModel && !_isDraggingSlider)
        {
            viewModel.UpdatePosition(mediaPlayer.Position.TotalSeconds);
        }
    }

    /// <summary>
    /// MediaElement 打开视频完成事件
    /// </summary>
    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (DataContext is BatchViewModel viewModel)
        {
            // 使用 MediaElement 的自然视频尺寸更新进度条最大值
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                progressSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            }
        }
    }

    /// <summary>
    /// MediaElement 播放结束事件
    /// </summary>
    private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BatchViewModel viewModel)
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
        if (DataContext is BatchViewModel viewModel && _isDraggingSlider)
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
        if (DataContext is BatchViewModel viewModel)
        {
            mediaPlayer.Position = TimeSpan.FromSeconds(viewModel.CurrentPosition);
        }
    }
}
