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

### 3.3 使用示例

1. **查看当前版本**：
   ```powershell
   .\sync-version.ps1
   ```

2. **更新版本号**：
   ```powershell
   .\sync-version.ps1 -newVersion 1.2.0
   ```

3. **检查版本配置**：
   ```powershell
   Get-Content -Path "version.json"
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

### 4.2 实际示例

```markdown
# 更新说明

## [1.1.3] - 2025-12-12

### 新增
- 支持Quartz 3.8.1版本
- 添加了作业执行历史查询功能

### 修复
- 修复了作业调度器初始化失败的问题
- 解决了任务执行日志丢失的bug

### 优化
- 优化了作业执行性能
- 改进了UI界面的响应速度

### 变更
- 更新了依赖包版本
- 调整了配置文件结构

## [1.1.2] - 2025-11-20

### 修复
- 修复了作业暂停功能失效的问题
```

## 5. 版本发布流程

### 5.1 准备阶段

1. **更新版本号**
   ```powershell
   .\sync-version.ps1 -newVersion 1.1.3
   ```

2. **手动更新说明README.md**
   - 打开README.md文件
   - 添加更新说明新版本条目（如果不存在）

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

1. **构建项目**
   ```powershell
   dotnet build -c Release
   ```

2. **生成NuGet包**
   ```powershell
   dotnet pack -c Release -o nupkgs
   ```

### 5.3 发布阶段

1. **发布NuGet包**
   ```powershell
   dotnet nuget push nupkgs\*.nupkg --api-key <YOUR_API_KEY> --source https://api.nuget.org/v3/index.json
   ```

2. **更新GitHub Release**
   - 从README.md的更新说明部分复制版本说明
   - 上传NuGet包作为发布附件

## 6. 自动化集成

### 6.1 GitHub Actions 工作流

使用 `.github/workflows/release.yml`，实现自动化构建、测试和发布：

```yaml
# .github/workflows/release.yml
name: Release

on:
  push:
    tags:
      - v*.*.*

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET Core
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Test
      run: dotnet test --configuration Release --no-build --verbosity normal
    
    - name: Pack
      run: dotnet pack --configuration Release --no-build -o nupkgs
    
    - name: Push to NuGet
      run: dotnet nuget push nupkgs\*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
      
    - name: Create GitHub Release
      uses: actions/create-release@v1
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      with:
        tag_name: ${{ github.ref }}
        release_name: Release ${{ github.ref }}
        draft: false
        prerelease: false
```

### 6.2 工作流触发条件

- 当推送符合 `v*.*.*` 格式的git标签时自动触发
- 执行构建、测试、打包和发布流程
- 自动创建GitHub Release

### 6.3 环境变量配置

| 环境变量 | 用途 | 配置位置 |
|----------|------|----------|
| `NUGET_API_KEY` | NuGet发布密钥 | GitHub仓库Settings → Secrets and variables → Actions |
| `GITHUB_TOKEN` | GitHub API访问令牌 | 自动生成，无需手动配置 |

## 7. 最佳实践

### 7.1 版本管理原则

- **统一版本号**: 所有项目组件使用相同的版本号，便于管理和追溯
- **及时更新CHANGELOG**: 每次代码变更都应更新CHANGELOG，避免集中更新
- **自动化优先**: 尽量使用脚本自动化版本管理流程，减少手动操作
- **语义化版本**: 严格遵循语义化版本规则，清晰传达版本变更内容
- **标签管理**: 每个版本都应创建git标签，便于代码回溯
- **版本锁定**: 关键依赖项应使用固定版本，避免意外更新

### 7.2 发布频率建议

| 版本类型 | 发布时机 | 建议频率 |
|----------|----------|----------|
| 补丁版本 | 修复关键bug时 | 立即发布 |
| 次版本 | 新增功能稳定后 | 每月1-2次 |
| 主版本 | 重大架构变更时 | 不频繁（季度或年度） |

### 7.3 回滚机制

1. **保留发布历史**: 保留所有已发布的NuGet包
2. **标签对应版本**: 每个git标签对应一个可回滚的版本
3. **变更记录**: 在CHANGELOG中记录重大变更的回滚说明
4. **测试验证**: 发布新版本前进行充分测试
5. **回滚流程**: 制定清晰的回滚步骤，包括代码回滚、NuGet包撤销和通知机制

### 7.4 团队协作建议

- **明确职责**: 指定专人负责版本管理和发布
- **沟通机制**: 发布前通知团队成员，确保大家了解版本变更
- **代码审查**: 所有版本相关的代码变更都应经过审查
- **文档同步**: 版本更新后及时同步相关文档

## 8. 常见问题处理

### 8.1 版本号冲突

**问题**: 本地版本与远程仓库版本冲突
**解决方案**: 
- 先拉取最新代码
- 执行 `sync-version.ps1` 重新同步版本号
- 解决代码冲突后再提交

### 8.2 CHANGELOG格式错误

**问题**: CHANGELOG条目格式不一致
**解决方案**: 
- 严格按照脚本生成的格式添加条目
- 定期执行格式检查脚本
- 使用Markdownlint工具验证格式

### 8.3 自动化脚本执行失败

**问题**: PowerShell脚本执行权限不足
**解决方案**: 
- 执行 `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`
- 或使用 `PowerShell -ExecutionPolicy Bypass -File script.ps1` 执行脚本

## 9. 附录

### 9.1 版本号命名规范

| 场景 | 版本号示例 | 说明 |
|------|------------|------|
| 初始发布 | 1.0.0 | 第一个稳定版本 |
| 功能更新 | 1.1.0 | 新增功能，向下兼容 |
| Bug修复 | 1.1.1 | 修复问题，向下兼容 |
| 重大变更 | 2.0.0 | 不兼容的API变更 |
| 预发布版本 | 1.1.0-alpha.1 | 内部测试版本 |
| RC版本 | 1.1.0-rc.1 | 候选发布版本 |

### 9.2 CHANGELOG类型说明

| 类型 | 描述 |
|------|------|
| 新增 | 新功能、新API、新组件 |
| 修复 | Bug修复、问题解决 |
| 优化 | 性能优化、代码结构优化 |
| 变更 | 配置变更、依赖更新、API调整 |
| 兼容性 | 版本兼容性说明、迁移指南 |

## 10. 总结

本版本管理方案旨在通过标准化的流程和自动化工具，确保项目版本更新的规范性和可追溯性。实施该方案后，团队可以：

1. 统一管理版本号，避免不一致问题
2. 自动生成和维护CHANGELOG
3. 实现自动化构建和发布
4. 提高版本更新的效率和可靠性
5. 便于代码追溯和问题定位

通过严格遵循本方案，项目的版本管理将更加规范、高效和可靠，为项目的长期发展提供有力支持。