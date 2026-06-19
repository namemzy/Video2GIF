@echo off
setlocal

echo ========================================
echo   Video2GIF 打包脚本（单文件 EXE）
echo ========================================
echo.

set PROJECT=src\Video2GIF\Video2GIF.csproj
set OUTDIR=publish
set RUNTIME=win-x64

:: 清理旧输出
if exist "%OUTDIR%\Lite" rmdir /s /q "%OUTDIR%\Lite"
if exist "%OUTDIR%\Full" rmdir /s /q "%OUTDIR%\Full"

:: ---- 精简版（内嵌精简 FFmpeg ~43MB）----
echo [1/2] 正在打包精简版...
dotnet publish "%PROJECT%" -c Release -r %RUNTIME% --self-contained -p:PublishSingleFile=true -p:BundleFFmpeg=lite -o "%OUTDIR%\Lite" --nologo -v q
if errorlevel 1 (
    echo 精简版打包失败！
    exit /b 1
)
echo 精简版打包完成
echo.

:: ---- 完整版（内嵌完整 FFmpeg ~195MB）----
echo [2/2] 正在打包完整版...
dotnet publish "%PROJECT%" -c Release -r %RUNTIME% --self-contained -p:PublishSingleFile=true -p:BundleFFmpeg=full -o "%OUTDIR%\Full" --nologo -v q
if errorlevel 1 (
    echo 完整版打包失败！
    exit /b 1
)
echo 完整版打包完成
echo.

:: ---- 打包 ZIP ----
echo 正在压缩 ZIP...
powershell -command "Compress-Archive -Path '%OUTDIR%\Lite\*' -DestinationPath '%OUTDIR%\Video2GIF-Lite.zip' -Force"
powershell -command "Compress-Archive -Path '%OUTDIR%\Full\*' -DestinationPath '%OUTDIR%\Video2GIF-Full.zip' -Force"

:: 清理解压目录
rmdir /s /q "%OUTDIR%\Lite"
rmdir /s /q "%OUTDIR%\Full"

echo.
echo ========================================
echo   打包完成！
echo ========================================
echo   精简版: %OUTDIR%\Video2GIF-Lite.zip
echo   完整版: %OUTDIR%\Video2GIF-Full.zip
echo ========================================

endlocal
