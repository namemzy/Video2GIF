# Video2GIF — 轻量化视频转 GIF 工具

## TL;DR
一个轻量级 Windows 桌面应用，支持将 MP4 等视频格式转换为 GIF，可自定义分辨率、帧率、速度和时间范围，内置视频预览功能。

## 技术栈
- **框架**：.NET 6.0 + WPF
- **架构**：MVVM（手动实现 INotifyPropertyChanged + RelayCommand）
- **视频处理**：CliWrap 封装 FFmpeg 命令行
- **UI**：现代化 WPF 样式（圆角卡片、阴影、主色调）

## 功能特性
- ✅ MP4/AVI/MKV/MOV/WMV → GIF 转换
- ✅ 输出分辨率默认与原视频一致，支持手动修改（可锁定宽高比）
- ✅ 帧率调整（1-30 FPS 滑块）
- ✅ 播放速度（0.25x / 0.5x / 1x / 1.5x / 2x）
- ✅ 时间范围截取（起始/结束时间）
- ✅ 视频预览（播放/暂停/停止 + 进度条）
- ✅ 拖拽文件打开
- ✅ 转换进度显示 + 取消支持
- ✅ 宽高偶数自动调整（FFmpeg 要求）

## 项目结构
```
F:\mzy\code\Video2GIF\
├── Video2GIF.sln
├── src/Video2GIF/
│   ├── Video2GIF.csproj
│   ├── App.xaml / App.xaml.cs          # 应用入口 + 全局样式资源
│   ├── MainWindow.xaml / .xaml.cs      # 主窗口（预览+设置+转换）
│   ├── Models/ConvertSettings.cs       # 转换设置模型
│   ├── Services/FFmpegService.cs       # FFmpeg 命令封装
│   ├── ViewModels/MainViewModel.cs     # 主 ViewModel
│   └── Helpers/RelayCommand.cs         # MVVM 命令实现
```

## 使用前提
需要自行下载 [FFmpeg](https://ffmpeg.org/download.html) 并在应用中通过「浏览」按钮指定 `ffmpeg.exe` 路径。

## 启动命令
```bash
cd F:\mzy\code\Video2GIF
dotnet run --project src/Video2GIF
```

## QA 验证结果
- 编译状态：✅ 0 错误，0 警告
- 代码质量检查：15 项检查全部通过
- 已修复问题：进度条拖拽事件、分辨率验证、起始时间逻辑
