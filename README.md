# Video2GIF 🎬

视频转 GIF 工具，基于 .NET 6 WPF + FFmpeg，支持拖拽、实时预览、精细参数调节。

## 功能特性

- 拖拽视频文件直接导入
- 实时视频预览与播放控制
- 自定义起止时间、分辨率、帧率
- 多种抖动算法与调色板模式
- 支持变速播放（0.5x ~ 2x）

## 快速开始

### 方式一：下载 Release（推荐）

从 [Releases](https://github.com/namemzy/Video2GIF/releases) 页面下载最新版本的 zip 文件，解压后直接运行 `Video2GIF.exe`。

### 方式二：从源码构建

```bash
# 1. 克隆仓库
git clone https://github.com/namemzy/Video2GIF.git
cd Video2GIF

# 2. 运行（需要 .NET 6 SDK）
dotnet run --project src/Video2GIF

# 3. 发布独立 exe（可选）
dotnet publish src/Video2GIF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**环境要求：**

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)（仅源码构建方式需要）
- [FFmpeg](https://ffmpeg.org/download.html)（应用内可手动指定路径）

## 使用说明

1. 启动应用后，拖拽视频文件到预览区域，或点击「打开文件」
2. 拖动进度条或使用播放控制预览视频
3. 设置起始/结束时间（支持手动输入或「设为起点/终点」按钮）
4. 调整右侧参数（分辨率、帧率、速度、抖动算法等）
5. 指定 FFmpeg 路径和输出路径
6. 点击「开始转换」

## 技术栈

- .NET 6 + WPF
- FFmpeg（外部依赖）
- MVVM 架构

## 许可证

MIT
