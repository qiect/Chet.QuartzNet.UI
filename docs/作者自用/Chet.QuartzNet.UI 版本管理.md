# 版本管理方案

## 1. 方案概述

为了确保项目版本更新的规范性、可追溯性和自动化，制定本版本管理方案。该方案涵盖版本号管理、CHANGELOG维护、发布流程和自动化脚本支持。

## 2. 版本号规则

采用**语义化版本号**（Semantic Versioning）规则：

```
MAJOR.MINOR.PATCH
```

- **MAJOR**: 不兼容的API变更
- **MINOR**: 向下兼容的功能性新增
- **PATCH**: 向下兼容的问题修正

### 示例

- `1.0.0`: 初始版本
- `1.1.0`: 新增功能，向下兼容
- `1.1.1`: 修复问题，向下兼容
- `2.0.0`: 重大变更，不兼容旧版本

## 3. 版本号统一管理

### 3.1 统一版本号存储

创建一个统一的版本号配置文件，避免在多个文件中手动维护版本号：

**版本配置文件**: `version.json`
```json
{
  "version": "1.1.3",
  "releaseDate": "2025-12-12"
}
```

### 3.2 自动化版本同步

创建PowerShell脚本 `sync-version.ps1`，用于将统一版本号同步到所有相关文件：

```powershell
# sync-version.ps1
param (
    [string]$newVersion
)

# 读取版本配置文件
$versionConfig = Get-Content -Path "version.json" -Raw | ConvertFrom-Json

# 如果提供了新版本，则更新
if ($newVersion) {
    $versionConfig.version = $newVersion
    $versionConfig.releaseDate = Get-Date -Format "yyyy-MM-dd"
    $versionConfig | ConvertTo-Json -Depth 10 | Set-Content -Path "version.json"
}

$version = $versionConfig.version
$releaseDate = $versionConfig.releaseDate

Write-Host "正在同步版本号: $version ($releaseDate)"

# 更新 csproj 文件
$csprojFiles = @(
    "src/Chet.QuartzNet.Core/Chet.QuartzNet.Core.csproj",
    "src/Chet.QuartzNet.Models/Chet.QuartzNet.Models.csproj",
    "src/Chet.QuartzNet.EFCore/Chet.QuartzNet.EFCore.csproj",
    "src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj"
)

foreach ($file in $csprojFiles) {
    $content = Get-Content -Path $file -Raw
    $content = $content -replace '<Version>(.*?)</Version>', "<Version>$version</Version>" -replace '<PackageVersion>(.*?)</PackageVersion>', "<PackageVersion>$version</PackageVersion>"
    Set-Content -Path $file -Value $content
    Write-Host "已更新: $file"
}

# 更新 nuspec 文件
$nuspecFile = "Chet.QuartzNet.UI.nuspec"
$content = Get-Content -Path $nuspecFile -Raw
$content = $content -replace '<version>(.*?)</version>', "<version>$version</version>"
$content = $content -replace '<releaseNotes>(.*?)</releaseNotes>', "<releaseNotes>v$version: 请参考CHANGELOG.md</releaseNotes>"
Set-Content -Path $nuspecFile -Value $content
Write-Host "已更新: $nuspecFile"

Write-Host "版本号同步完成！"
```

## 4. CHANGELOG 规范

### 4.1 标准格式

采用 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/) 格式，确保CHANGELOG的可读性和一致性：

```markdown
# 更新说明

## [版本号] - 发布日期

### 新增
- 功能1描述
- 功能2描述

### 修复
- 问题1描述
- 问题2描述

### 优化
- 优化1描述
- 优化2描述

### 变更
- 变更1描述
- 变更2描述

### 兼容性
- 兼容性说明
```

## 5. 版本发布流程

### 5.1 准备阶段

1. **更新版本号**
   ```powershell
   .\sync-version.ps1 -newVersion 1.1.3
   ```

2. **手动更新CHANGELOG.md**
   - 打开CHANGELOG.md文件
   - 添加新版本条目（如果不存在）
   - 在对应类型下添加条目

3. **提交代码**
   ```bash
   git add .
   git commit -m "chore: release version 1.1.3"
   ```

4. **创建标签**
   ```bash
   git tag -a v1.1.3 -m "Release version 1.1.3"
   git push origin v1.1.3
   ```

### 5.2 构建阶段

1. **运行构建脚本**
   ```powershell
   .\build-nuget.bat
   ```

2. **验证构建结果**
   - 检查 `nupkgs` 目录下的NuGet包
   - 验证包版本和内容

### 5.3 发布阶段

1. **发布NuGet包**
   ```powershell
   dotnet nuget push nupkgs\*.nupkg --api-key <YOUR_API_KEY> --source https://api.nuget.org/v3/index.json
   ```

2. **更新GitHub Release**
   - 从CHANGELOG.md复制版本说明
   - 上传NuGet包作为发布附件

## 6. 自动化集成

### 6.1 GitHub Actions 工作流

创建 `.github/workflows/release.yml`，实现自动化发布：

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --configuration Release --no-restore
      
      - name: Pack
        run: dotnet pack --configuration Release --no-build --output nupkgs
      
      - name: Push to NuGet.org
        run: dotnet nuget push nupkgs/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
      
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: nupkgs/*.nupkg
          body_path: CHANGELOG.md
```

### 6.2 预发布检查脚本

创建 `pre-release-check.ps1`，用于在发布前进行自动化检查：

```powershell
# pre-release-check.ps1
Write-Host "正在执行预发布检查..."

# 检查版本号一致性
Write-Host "\n1. 检查版本号一致性..."
$versionConfig = Get-Content -Path "version.json" -Raw | ConvertFrom-Json
$version = $versionConfig.version

$inconsistentFiles = @()

# 检查csproj文件
$csprojFiles = Get-ChildItem -Path "src" -Recurse -Filter "*.csproj"
foreach ($file in $csprojFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    if (-not ($content -match "<Version>$version</Version>" -or $content -match "<PackageVersion>$version</PackageVersion>")) {
        $inconsistentFiles += $file.FullName
    }
}

# 检查nuspec文件
$nuspecFile = "Chet.QuartzNet.UI.nuspec"
$content = Get-Content -Path $nuspecFile -Raw
if (-not ($content -match "<version>$version</version>")) {
    $inconsistentFiles += $nuspecFile
}

if ($inconsistentFiles.Count -gt 0) {
    Write-Host "❌ 版本号不一致的文件:"
    foreach ($file in $inconsistentFiles) {
        Write-Host "   - $file"
    }
    exit 1
} else {
    Write-Host "✅ 版本号一致"
}

# 检查CHANGELOG是否有当前版本
Write-Host "\n2. 检查CHANGELOG..."
$changelogContent = Get-Content -Path "CHANGELOG.md" -Raw
if (-not ($changelogContent -match "## \[$version\]")) {
    Write-Host "❌ CHANGELOG中未找到版本 $version"
    exit 1
} else {
    Write-Host "✅ CHANGELOG包含当前版本"
}

# 检查构建
Write-Host "\n3. 执行构建检查..."
dotnet build --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 构建失败"
    exit 1
} else {
    Write-Host "✅ 构建成功"
}

Write-Host "\n✅ 所有预发布检查通过！"
```

## 7. 最佳实践

### 7.1 版本管理原则

1. **统一版本号**: 所有项目组件使用相同的版本号，便于管理和追溯
2. **及时更新CHANGELOG**: 每次代码变更都应更新CHANGELOG，避免集中更新
3. **自动化优先**: 尽量使用脚本自动化版本管理流程，减少手动操作
4. **语义化版本**: 严格遵循语义化版本规则，清晰传达版本变更内容
5. **标签管理**: 每个版本都应创建git标签，便于代码回溯

### 7.2 发布频率建议

- **补丁版本**: 修复关键bug时立即发布
- **次版本**: 新增功能稳定后发布，建议每月1-2次
- **主版本**: 重大架构变更时发布，不频繁

### 7.3 回滚机制

1. 保留所有已发布的NuGet包
2. 每个git标签对应一个可回滚的版本
3. 在CHANGELOG中记录重大变更的回滚说明
4. 发布新版本前进行充分测试

## 8. 实施步骤

1. **创建配置文件**: 执行 `New-Item -ItemType File -Name version.json -Value '{"version": "1.1.3", "releaseDate": "2025-12-12"}'`
2. **创建脚本文件**: 复制上述PowerShell脚本到项目根目录
3. **更新CHANGELOG格式**: 将现有CHANGELOG.md转换为标准格式
4. **集成CI/CD**: 配置GitHub Actions工作流
5. **培训团队**: 确保团队成员了解并遵循版本管理流程
6. **定期审计**: 定期检查版本管理执行情况，持续优化流程

## 9. 常见问题处理

### 9.1 版本号冲突

**问题**: 本地版本与远程仓库版本冲突
**解决方案**: 
- 先拉取最新代码
- 执行 `sync-version.ps1` 重新同步版本号
- 解决代码冲突后再提交

### 9.2 CHANGELOG格式错误

**问题**: CHANGELOG条目格式不一致
**解决方案**: 
- 严格按照脚本生成的格式添加条目
- 定期执行格式检查脚本
- 使用Markdownlint工具验证格式

### 9.3 自动化脚本执行失败

**问题**: PowerShell脚本执行权限不足
**解决方案**: 
- 执行 `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`
- 或使用 `PowerShell -ExecutionPolicy Bypass -File script.ps1` 执行脚本

## 10. 附录

### 10.1 版本管理命令速查

| 命令 | 功能 | 示例 |
|------|------|------|
| `sync-version.ps1 -version 1.1.3` | 同步版本号 | `.\sync-version.ps1 -version 1.1.3` |
| `update-changelog.ps1` | 更新CHANGELOG | `.\update-changelog.ps1 -version 1.1.3 -type 新增 -description "功能描述"` |
| `pre-release-check.ps1` | 预发布检查 | `.\pre-release-check.ps1` |
| `git tag -a v1.1.3 -m "Release 1.1.3"` | 创建git标签 | `git tag -a v1.1.3 -m "Release 1.1.3"` |
| `git push origin v1.1.3` | 推送git标签 | `git push origin v1.1.3` |

### 10.2 版本号命名规范

| 场景 | 版本号示例 | 说明 |
|------|------------|------|
| 初始发布 | 1.0.0 | 第一个稳定版本 |
| 功能更新 | 1.1.0 | 新增功能，向下兼容 |
| Bug修复 | 1.1.1 | 修复问题，向下兼容 |
| 重大变更 | 2.0.0 | 不兼容的API变更 |
| 预发布版本 | 1.1.0-alpha.1 | 内部测试版本 |
| RC版本 | 1.1.0-rc.1 | 候选发布版本 |

### 10.3 CHANGELOG类型说明

| 类型 | 描述 |
|------|------|
| 新增 | 新功能、新API、新组件 |
| 修复 | Bug修复、问题解决 |
| 优化 | 性能优化、代码结构优化 |
| 变更 | 配置变更、依赖更新、API调整 |
| 兼容性 | 版本兼容性说明、迁移指南 |

## 11. 总结

本版本管理方案旨在通过标准化的流程和自动化工具，确保项目版本更新的规范性和可追溯性。实施该方案后，团队可以：

1. 统一管理版本号，避免不一致问题
2. 自动生成和维护CHANGELOG
3. 实现自动化构建和发布
4. 提高版本更新的效率和可靠性
5. 便于代码追溯和问题定位

通过严格遵循本方案，项目的版本管理将更加规范、高效和可靠，为项目的长期发展提供有力支持。