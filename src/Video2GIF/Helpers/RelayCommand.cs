using System.Windows.Input;

namespace Video2GIF.Helpers;

/// <summary>
/// 简单的 RelayCommand 实现，用于 MVVM 命令绑定
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// 初始化 RelayCommand
    /// </summary>
    /// <param name="execute">执行动作</param>
    /// <param name="canExecute">是否可执行的判断逻辑</param>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// 初始化 RelayCommand（无参数版本）
    /// </summary>
    /// <param name="execute">执行动作</param>
    /// <param name="canExecute">是否可执行的判断逻辑</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    /// <summary>
    /// CanExecute 变化事件
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// 判断命令是否可执行
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <summary>
    /// 触发 CanExecute 重新评估
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>
/// 异步 RelayCommand 实现，支持 async/await 模式
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// 初始化 AsyncRelayCommand（无参数版本）
    /// </summary>
    /// <param name="execute">异步执行函数</param>
    /// <param name="canExecute">是否可执行的判断逻辑</param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    /// <summary>
    /// 初始化 AsyncRelayCommand（带参数版本）
    /// </summary>
    /// <param name="execute">异步执行函数</param>
    /// <param name="canExecute">是否可执行的判断逻辑</param>
    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// CanExecute 变化事件
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// 判断命令是否可执行（正在执行时返回 false）
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    /// <summary>
    /// 执行异步命令
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;

        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 触发 CanExecute 重新评估
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
