# Video2GIF 🎬

视频转 GIF 工具，基于 .NET 6 WPF + FFmpeg，支持拖拽、实时预览、精细参数调节。

## 功能特性

- 拖拽视频文件直接导入，输出分辨率自动匹配
- 实时视频预览与播放控制
- 自定义起止时间、分辨率、帧率
- 6 种抖动算法可选（Sierra Lite / Floyd-Steinberg / Sierra 2/3 / Bayer / 无抖动）
- 3 种调色板模式可选（全局优化 / 差异优化 / 单帧采样）
- 支持变速播放（0.25x ~ 4x）
- 锁定宽高比（可选）
- 两步法高质量 GIF 编码（palettegen + paletteuse）

## 下载

从 [Releases](https://github.com/namemzy/Video2GIF/releases) 页面下载，提供三个版本：

| 版本 | 文件 | 体积 | 说明 |
|------|------|------|------|
| **精简版** | `Video2GIF-Lite.zip` | ~50MB | 内置精简 FFmpeg，推荐大多数用户 |
| **完整版** | `Video2GIF-Full.zip` | ~71MB | 内置完整 FFmpeg，包含所有编解码器 |
| **便携版** | `Video2GIF-Portable.zip` | ~214KB | 不含 FFmpeg，首次转换时自动下载 |

下载后解压，直接运行 `Video2GIF.exe`。

## 从源码构建

```bash
# 克隆仓库
git clone https://github.com/namemzy/Video2GIF.git
cd Video2GIF

# 运行（需要 .NET 6 SDK）
dotnet run --project src/Video2GIF

# 打包三个版本（需要 ffmpeg.exe 和 ffmpeg-lite.exe 在 src/Video2GIF/ 目录下）
build.bat
```

**打包参数：**

```bash
# 精简版（内置精简 FFmpeg）
dotnet publish src/Video2GIF -c Release -p:BundleFFmpeg=lite -o publish/Lite

# 完整版（内置完整 FFmpeg）
dotnet publish src/Video2GIF -c Release -p:BundleFFmpeg=full -o publish/Full

# 便携版（不含 FFmpeg）
dotnet publish src/Video2GIF -c Release -p:BundleFFmpeg=false -o publish/Portable
```

**环境要求：**

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- FFmpeg（精简版/完整版已内置，便携版首次运行自动下载）

## 使用说明

1. 启动应用后，拖拽视频文件到预览区域，或点击「打开文件」
2. 拖动进度条或使用播放控制预览视频
3. 设置起始/结束时间（支持手动输入或「设为起点/终点」按钮）
4. 调整右侧参数（分辨率、帧率、速度、抖动算法、调色板模式等）
5. 点击「开始转换」

## 技术栈

- .NET 6 + WPF
- CliWrap（FFmpeg 进程调用）
- FFmpeg（视频解码 + GIF 编码）
- MVVM 架构

## 许可证

MIT
