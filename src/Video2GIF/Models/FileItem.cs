using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Video2GIF.Models;

/// <summary>
/// 文件项状态枚举
/// </summary>
public enum FileItemStatus
{
    /// <summary>等待转换</summary>
    Waiting,
    /// <summary>正在转换</summary>
    Converting,
    /// <summary>转换完成</summary>
    Done,
    /// <summary>转换失败</summary>
    Failed
}

/// <summary>
/// 批量转换中的单个文件项
/// </summary>
public class FileItem : INotifyPropertyChanged
{
    private string _filePath = string.Empty;
    private FileItemStatus _status = FileItemStatus.Waiting;
    private double _progress;
    private string? _errorMessage;

    /// <summary>属性变更通知事件</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发属性变更通知（公开给外部调用以更新计算属性）
    /// </summary>
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
    /// 文件完整路径
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    /// <summary>
    /// 文件名（仅名称，不含路径）
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// 文件项状态
    /// </summary>
    public FileItemStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>
    /// 转换进度百分比 (0-100)
    /// </summary>
    public double Progress
    {
        get => _progress;
        set
        {
            if (SetProperty(ref _progress, value))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    /// <summary>
    /// 错误信息（转换失败时有值）
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// 状态显示文本
    /// </summary>
    public string StatusText
    {
        get
        {
            return Status switch
            {
                FileItemStatus.Waiting => "等待",
                FileItemStatus.Converting => string.Format("转换中 {0:F0}%", Progress),
                FileItemStatus.Done => "完成 ✓",
                FileItemStatus.Failed => string.IsNullOrEmpty(ErrorMessage) ? "失败 ✗" : string.Format("失败: {0}", ErrorMessage),
                _ => string.Empty
            };
        }
    }

    /// <summary>
    /// 进度显示文本
    /// </summary>
    public string ProgressText
    {
        get
        {
            if (Status == FileItemStatus.Converting)
            {
                return string.Format("{0:F0}%", Progress);
            }
            return string.Empty;
        }
    }
}
