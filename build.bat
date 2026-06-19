@echo off
setlocal

echo ========================================
echo   Video2GIF 打包脚本
echo ========================================
echo.

set PROJECT=src\Video2GIF\Video2GIF.csproj
set OUTDIR=publish

:: 清理旧输出
if exist "%OUTDIR%\Lite" rmdir /s /q "%OUTDIR%\Lite"
if exist "%OUTDIR%\Full" rmdir /s /q "%OUTDIR%\Full"
if exist "%OUTDIR%\Portable" rmdir /s /q "%OUTDIR%\Portable"

:: ---- 精简版（内置精简 FFmpeg ~131MB）----
echo [1/3] 正在打包精简版...
dotnet publish "%PROJECT%" -c Release -p:BundleFFmpeg=lite -o "%OUTDIR%\Lite" --nologo -v q
if errorlevel 1 (
    echo 精简版打包失败！
    exit /b 1
)
echo 精简版打包完成
echo.

:: ---- 完整版（内置完整 FFmpeg ~195MB）----
echo [2/3] 正在打包完整版...
dotnet publish "%PROJECT%" -c Release -p:BundleFFmpeg=full -o "%OUTDIR%\Full" --nologo -v q
if errorlevel 1 (
    echo 完整版打包失败！
    exit /b 1
)
echo 完整版打包完成
echo.

:: ---- 便携版（不打包 FFmpeg，首次运行自动下载）----
echo [3/3] 正在打包便携版...
dotnet publish "%PROJECT%" -c Release -p:BundleFFmpeg=false -o "%OUTDIR%\Portable" --nologo -v q
if errorlevel 1 (
    echo 便携版打包失败！
    exit /b 1
)
:: 确保不包含 ffmpeg
if exist "%OUTDIR%\Portable\ffmpeg.exe" del /q "%OUTDIR%\Portable\ffmpeg.exe"
echo 便携版打包完成
echo.

:: ---- 打包 ZIP ----
echo 正在压缩 ZIP...
powershell -command "Compress-Archive -Path '%OUTDIR%\Lite\*' -DestinationPath '%OUTDIR%\Video2GIF-Lite.zip' -Force"
powershell -command "Compress-Archive -Path '%OUTDIR%\Full\*' -DestinationPath '%OUTDIR%\Video2GIF-Full.zip' -Force"
powershell -command "Compress-Archive -Path '%OUTDIR%\Portable\*' -DestinationPath '%OUTDIR%\Video2GIF-Portable.zip' -Force"

echo.
echo ========================================
echo   打包完成！
echo ========================================
echo   精简版:  %OUTDIR%\Video2GIF-Lite.zip     (内置精简 FFmpeg)
echo   完整版:  %OUTDIR%\Video2GIF-Full.zip     (内置完整 FFmpeg)
echo   便携版:  %OUTDIR%\Video2GIF-Portable.zip (首次运行自动下载)
echo ========================================

endlocal
