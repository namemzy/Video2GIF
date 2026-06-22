# Video2GIF 🎬

视频转 GIF 工具，基于 .NET 6 WPF + FFmpeg，支持单个/批量转换、实时预览、精细参数调节。

## 功能特性

- **单个转换** — 拖拽视频文件直接导入，实时预览，自定义起止时间
- **批量转换** — 多文件队列，逐个处理，整体进度追踪
- **输出分辨率** — 自动匹配原视频，支持手动调整，锁定宽高比
- **帧率** — 1 ~ 30 FPS 可调
- **播放速度** — 0.25x / 0.5x / 1x / 1.5x / 2x / 3x / 4x
- **抖动算法** — Sierra Lite / Floyd-Steinberg / Sierra 2 / Sierra 3 / Bayer / 无抖动
- **调色板模式** — 全局优化 / 差异优化 / 单帧采样
- **两步法编码** — palettegen + paletteuse，高质量 GIF 输出
- **FFmpeg 内嵌** — 无需额外安装，开箱即用

## 下载

从 [Releases](https://github.com/namemzy/Video2GIF/releases) 页面下载，提供两个版本：

| 版本 | 文件 | 体积 | 说明 |
|------|------|------|------|
| **精简版** | `Video2GIF-Lite.zip` | ~50MB | 内置精简 FFmpeg，推荐大多数用户 |
| **完整版** | `Video2GIF-Full.zip` | ~71MB | 内置完整 FFmpeg，包含所有编解码器 |

下载后解压，直接运行 `Video2GIF.exe`，无需安装 .NET 运行时。

## 从源码构建

```bash
# 克隆仓库
git clone https://github.com/namemzy/Video2GIF.git
cd Video2GIF

# 运行（需要 .NET 6 SDK）
dotnet run --project src/Video2GIF
```

**打包（需要 FFmpeg 二进制文件在 `src/Video2GIF/` 目录下）：**

```bash
# 一键打包精简版 + 完整版
build.bat

# 或手动打包单个版本
dotnet publish src/Video2GIF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:BundleFFmpeg=lite -o publish/Lite
dotnet publish src/Video2GIF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:BundleFFmpeg=full -o publish/Full
```

**环境要求：**

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- `ffmpeg-lite.exe`（精简版，~43MB）和/或 `ffmpeg.exe`（完整版，~195MB）放在 `src/Video2GIF/` 目录下

## 使用说明

### 单个转换

1. 启动应用，拖拽视频文件到预览区域，或点击「打开文件」
2. 拖动进度条或使用播放控制预览视频
3. 点击「设为起点」/「设为终点」或手动输入时间
4. 调整右侧参数（分辨率、帧率、速度、抖动算法、调色板模式）
5. 点击「开始转换」

### 批量转换

1. 点击顶部「批量转换」切换模式
2. 添加多个视频文件
3. 选中文件可预览，统一设置输出参数
4. 点击「开始批量转换」

## 技术栈

- .NET 6 + WPF
- CliWrap（FFmpeg 进程调用）
- FFmpeg（视频解码 + GIF 编码，内嵌为 EmbeddedResource）
- MVVM 架构

## 许可证

MIT
