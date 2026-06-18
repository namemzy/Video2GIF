using System.Windows;

namespace Video2GIF;

/// <summary>
/// App.xaml 的交互逻辑
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 注册全局异常处理
        DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show(
                $"发生未处理的异常：\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show(
                $"发生严重错误：\n\n{ex?.Message}\n\n{ex?.StackTrace}",
                "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }
}
