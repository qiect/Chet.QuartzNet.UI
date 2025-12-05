@echo off
chcp 65001 >nul
echo.
echo ========================================
echo Chet.QuartzNet.UI NuGet 包打包脚本
echo ========================================
echo.

REM 检查是否安装了 .NET SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误：未安装 .NET SDK，请先安装 .NET 8.0 SDK
    exit /b 1
)

REM 清理之前的构建
echo 正在清理之前的构建...
dotnet clean src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj --configuration Release
dotnet clean src/Chet.QuartzNet.Core/Chet.QuartzNet.Core.csproj --configuration Release
dotnet clean src/Chet.QuartzNet.Models/Chet.QuartzNet.Models.csproj --configuration Release
dotnet clean src/Chet.QuartzNet.EFCore/Chet.QuartzNet.EFCore.csproj --configuration Release

REM 还原 NuGet 包
echo 正在还原 NuGet 包...
dotnet restore

REM 构建所有项目
echo 正在构建项目...
dotnet build src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj --configuration Release --no-restore
dotnet build src/Chet.QuartzNet.Core/Chet.QuartzNet.Core.csproj --configuration Release --no-restore
dotnet build src/Chet.QuartzNet.Models/Chet.QuartzNet.Models.csproj --configuration Release --no-restore
dotnet build src/Chet.QuartzNet.EFCore/Chet.QuartzNet.EFCore.csproj --configuration Release --no-restore

if %errorlevel% neq 0 (
    echo 错误：项目构建失败
    exit /b 1
)

REM 创建输出目录
if not exist "nupkgs" mkdir nupkgs

REM 打包 NuGet 包
echo 正在打包 NuGet 包...
dotnet pack src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj --configuration Release --no-build --output nupkgs

if %errorlevel% neq 0 (
    echo 错误：NuGet 打包失败
    echo 请确保已安装 NuGet CLI 工具
    exit /b 1
)

echo.
echo ========================================
echo 打包完成！
echo 输出目录：nupkgs
echo ========================================
echo.

REM 显示生成的包
dir nupkgs\*.nupkg


pause