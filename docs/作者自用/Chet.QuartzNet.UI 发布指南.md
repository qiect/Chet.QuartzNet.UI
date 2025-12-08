# Chet.QuartzNet.UI 发布指南

本文档详细描述了如何将 Chet.QuartzNet.UI 及其相关包发布到 NuGet 包管理器。

## 前提条件

1. **安装 .NET 8.0 SDK**：确保安装了 .NET 8.0 或更高版本的 SDK
2. **NuGet 工具**：
   - 安装 NuGet CLI 工具（可选，可以使用 dotnet CLI 替代）
   - 或使用 Visual Studio 内置的 NuGet 工具
3. **NuGet.org 账户**：拥有 NuGet.org 账户，并获取 API Key
   - API Key 需要具备推送新包和更新现有包的权限
   - 可以在 [NuGet.org](https://www.nuget.org/account/apikeys) 获取
4. **Git 配置**：已安装 Git 并配置好远程仓库
5. **构建脚本**：确保构建脚本（build-nuget.bat/build-nuget.sh）可执行

## 发布步骤

### 1. 更新版本号

#### 版本号策略

我们使用语义化版本控制（SemVer），格式为 `主版本号.次版本号.修订号`：

- **主版本号**：不兼容的 API 修改
- **次版本号**：向下兼容的功能性新增
- **修订号**：向下兼容的问题修正

#### 更新位置

在以下文件中更新版本号：

1. **项目文件**：
   - `src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj` 中的 `<Version>` 属性
   - `src/Chet.QuartzNet.Core/Chet.QuartzNet.Core.csproj` 中的 `<Version>` 属性
   - `src/Chet.QuartzNet.Models/Chet.QuartzNet.Models.csproj` 中的 `<Version>` 属性
   - `src/Chet.QuartzNet.EFCore/Chet.QuartzNet.EFCore.csproj` 中的 `<Version>` 属性

2. **NuSpec 文件**：
   - `Chet.QuartzNet.UI.nuspec` 文件中的 `<version>` 标签

3. **文档文件**：
   - 更新 `README.md` 中的版本信息
   - 更新 `CHANGELOG.md`，记录版本变更内容

#### 版本号格式

```xml
<!-- 在 .csproj 文件中 -->
<Version>1.1.0</Version>

<!-- 在 .nuspec 文件中 -->
<version>1.1.0</version>
```

### 2. 构建项目

#### 使用构建脚本

**Windows:**
```bash
build-nuget.bat
```

**Linux/macOS:**
```bash
chmod +x build-nuget.sh
./build-nuget.sh
```

#### 直接使用 dotnet CLI

```bash
# 清理解决方案
dotnet clean src/Chet.QuartzNet.UI.sln -c Release

# 构建解决方案
dotnet build src/Chet.QuartzNet.UI.sln -c Release

# 生成 NuGet 包
dotnet pack src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj -c Release -o ./nupkgs
dotnet pack src/Chet.QuartzNet.Core/Chet.QuartzNet.Core.csproj -c Release -o ./nupkgs
dotnet pack src/Chet.QuartzNet.Models/Chet.QuartzNet.Models.csproj -c Release -o ./nupkgs
dotnet pack src/Chet.QuartzNet.EFCore/Chet.QuartzNet.EFCore.csproj -c Release -o ./nupkgs
```

#### 验证构建结果

构建完成后，检查以下内容：

- `./nupkgs` 目录下是否生成了所有 NuGet 包文件（.nupkg 和 .snupkg）
- 包文件的版本号是否正确
- 包文件的大小是否合理

#### 生成的包文件

- `Chet.QuartzNet.UI.{版本}.nupkg` - 主包
- `Chet.QuartzNet.Core.{版本}.nupkg` - 核心功能包
- `Chet.QuartzNet.Models.{版本}.nupkg` - 数据模型包
- `Chet.QuartzNet.EFCore.{版本}.nupkg` - EF Core 数据访问包
- 对应的 .snupkg 符号包文件

### 3. 测试包

在发布到 NuGet.org 之前，强烈建议在本地或测试环境中测试生成的包：

#### 测试步骤

1. **创建本地测试项目**：
   ```bash
   dotnet new web -n QuartzUITest
   cd QuartzUITest
   ```

2. **添加本地 NuGet 源**：
   ```bash
   dotnet nuget add source "../nupkgs" -n "LocalPackages"
   ```

3. **安装测试包**：
   ```bash
   dotnet add package Chet.QuartzNet.UI --source "LocalPackages"
   dotnet add package Chet.QuartzNet.EFCore --source "LocalPackages"
   ```

4. **配置并测试功能**：
   - 在 Program.cs 中配置 Quartz UI 服务
   - 创建一个测试作业类并添加 `[QuartzJob]` 特性
   - 启动应用并访问 `/quartz-ui` 管理界面
   - 测试作业执行功能
   - 验证作业数据持久化（如果使用数据库存储）

5. **测试不同配置场景**：
   - 文件存储模式
   - 数据库存储模式（MySQL/SQL Server/PostgreSQL/SQLite）
   - Basic 认证功能
   - ClassJob 自动注册功能

6. **清理测试环境**：
   ```bash
   cd ..
   dotnet nuget remove source "LocalPackages"
   rm -rf QuartzUITest
   ```

### 4. 推送到 NuGet.org

#### 推送前准备

1. 确保已获取有效的 NuGet.org API Key
2. 确保所有包文件已正确生成
3. 确保已完成本地测试

#### 推送命令

使用 dotnet CLI 推送所有包：

```bash
# 推送主包
dotnet nuget push nupkgs/Chet.QuartzNet.UI.{版本}.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json

# 推送核心功能包
dotnet nuget push nupkgs/Chet.QuartzNet.Core.{版本}.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json

# 推送数据模型包
dotnet nuget push nupkgs/Chet.QuartzNet.Models.{版本}.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json

# 推送 EF Core 数据访问包
dotnet nuget push nupkgs/Chet.QuartzNet.EFCore.{版本}.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

或者使用 NuGet CLI：

```bash
nuget push nupkgs/*.nupkg -ApiKey YOUR_API_KEY -Source https://api.nuget.org/v3/index.json
```

#### 验证推送结果

1. **检查命令输出**：确保没有错误信息
2. **登录 NuGet.org**：访问 [NuGet.org](https://www.nuget.org/) 并登录您的账户
3. **查看包状态**：
   - 导航到「我的包」页面
   - 确认所有包已成功上传
   - 等待 NuGet.org 验证包（通常需要 10-15 分钟）

4. **发布预发布版本**（如果适用）：
   - 如果包处于预览状态，需要手动发布
   - 在 NuGet.org 包页面点击「Publish」按钮

#### 最终验证

推送完成后，创建一个新的测试项目并从 NuGet.org 安装已发布的包：

```bash
dotnet new web -n FinalTest
cd FinalTest
dotnet add package Chet.QuartzNet.UI
dotnet add package Chet.QuartzNet.EFCore
# 验证包是否正常工作
```

## 包结构

### Chet.QuartzNet.UI 包结构

```
Chet.QuartzNet.UI.{版本}.nupkg
├── lib/
│   └── net8.0/
│       ├── Chet.QuartzNet.UI.dll              # 主 UI 组件和控制器
│       ├── Chet.QuartzNet.UI.xml              # UI 组件 API 文档
│       └── [依赖项 DLL 文件]
├── contentFiles/
│   └── any/net8.0/
│       └── Resources/                        # 静态资源文件（CSS、JS、图片等）
├── views/                                    # Razor 视图文件
│   ├── QuartzUI/
│   │   ├── Index.cshtml
│   │   ├── _Layout.cshtml
│   │   └── [其他视图文件]
├── _rels/
└── [元数据文件]
```

### Chet.QuartzNet.Core 包结构

```
Chet.QuartzNet.Core.{版本}.nupkg
├── lib/
│   └── net8.0/
│       ├── Chet.QuartzNet.Core.dll           # 核心服务实现
│       ├── Chet.QuartzNet.Core.xml           # 核心服务 API 文档
│       └── [依赖项 DLL 文件]
└── [元数据文件]
```

### Chet.QuartzNet.Models 包结构

```
Chet.QuartzNet.Models.{版本}.nupkg
├── lib/
│   └── net8.0/
│       ├── Chet.QuartzNet.Models.dll         # 数据模型和 DTO
│       ├── Chet.QuartzNet.Models.xml         # 数据模型 API 文档
│       └── [依赖项 DLL 文件]
└── [元数据文件]
```

### Chet.QuartzNet.EFCore 包结构

```
Chet.QuartzNet.EFCore.{版本}.nupkg
├── lib/
│   └── net8.0/
│       ├── Chet.QuartzNet.EFCore.dll         # EF Core 数据访问层
│       ├── Chet.QuartzNet.EFCore.xml         # EF Core 数据访问 API 文档
│       └── [依赖项 DLL 文件]
└── [元数据文件]
```

### 包内容说明

- **DLL 文件**：编译后的程序集
- **XML 文档**：API 文档注释，用于 IDE 智能提示
- **Razor 视图**：Web UI 界面的 Razor 视图文件
- **静态资源**：CSS、JavaScript、图片等静态文件
- **元数据文件**：包含包的版本、依赖项、作者等信息

## 依赖项

### 核心依赖项

| 包名称 | 版本要求 | 用途 |
|--------|----------|------|
| Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation | 8.0.0+ | 支持 Razor 视图的运行时编译 |
| Microsoft.AspNetCore.StaticFiles | 8.0.0+ | 提供静态文件服务 |
| Quartz | 3.8.1+ | 作业调度核心库 |
| Quartz.Extensions.DependencyInjection | 3.8.1+ | Quartz 依赖注入扩展 |
| Quartz.Extensions.Hosting | 3.8.1+ | Quartz 宿主环境集成 |
| Microsoft.Extensions.Configuration.Abstractions | 8.0.0+ | 配置抽象接口 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.0+ | 依赖注入抽象接口 |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0+ | 日志记录抽象接口 |
| Newtonsoft.Json | 13.0.3+ | JSON 序列化/反序列化 |

### 数据库依赖项

| 包名称 | 版本要求 | 用途 |
|--------|----------|------|
| Microsoft.EntityFrameworkCore | 8.0.0+ | 实体框架核心库 |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.0+ | 内存数据库支持 |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.0+ | SQL Server 数据库支持 |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.0+ | SQLite 数据库支持 |
| Pomelo.EntityFrameworkCore.MySql | 8.0.0+ | MySQL 数据库支持 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.0+ | PostgreSQL 数据库支持 |

### UI 依赖项

| 包名称 | 版本要求 | 用途 |
|--------|----------|------|
| AntDesign | 0.17.0+ | Ant Design 组件库 |
| Vue.js | 3.0.0+ | Vue.js 前端框架 |
| Axios | 1.0.0+ | HTTP 客户端库 |

### 可选依赖项

| 包名称 | 版本要求 | 用途 |
|--------|----------|------|
| Swashbuckle.AspNetCore | 6.0.0+ | Swagger/OpenAPI 文档生成 |
| FluentValidation | 11.0.0+ | 数据验证库 |

## 版本管理

### 语义化版本控制

我们严格遵循 [语义化版本控制](https://semver.org/) 规范：

- **主版本号 (X.y.z)**：
  - 不兼容的 API 修改
  - 移除已标记为过时的功能
  - 重大架构调整

- **次版本号 (x.Y.z)**：
  - 向下兼容的功能性新增
  - 改进现有功能
  - 添加新的 API

- **修订号 (x.y.Z)**：
  - 向下兼容的错误修复
  - 性能优化
  - 文档更新
  - 非功能性更改

### 预发布版本

对于重大功能或变更，我们可能会发布预发布版本：

```
1.2.0-alpha.1  # 早期开发版本
1.2.0-beta.1   # 功能基本完整
1.2.0-rc.1     # 发布候选版本
```

### 版本发布频率

- **补丁版本**：根据需要发布，通常用于修复紧急问题
- **次版本**：每月或每季度发布，包含新功能和改进
- **主版本**：根据重大变更需求发布，通常间隔较长时间

### 版本标签

在 Git 仓库中，我们使用标签来标记发布版本：

```bash
# 创建标签
git tag v1.1.0

# 推送标签到远程仓库
git push origin v1.1.0
```

## 发布检查清单

### 代码和功能检查

- [ ] 更新所有项目文件中的版本号
- [ ] 更新 NuSpec 文件中的版本号
- [ ] 运行所有单元测试和集成测试
- [ ] 确保代码覆盖率达到目标
- [ ] 检查是否有未解决的重大问题
- [ ] 验证所有新功能都已正确实现
- [ ] 检查是否有已标记为过时的功能需要移除

### 文档和元数据检查

- [ ] 更新 CHANGELOG.md，记录所有变更
- [ ] 更新 README.md 中的版本信息
- [ ] 确保 API 文档完整且准确
- [ ] 检查项目文件中的包元数据（作者、描述、标签等）
- [ ] 更新示例项目以反映最新版本
- [ ] 验证构建脚本是否更新

### 构建和测试检查

- [ ] 清理并重新构建解决方案（Release 模式）
- [ ] 生成 NuGet 包并验证包结构
- [ ] 检查生成的包文件版本是否正确
- [ ] 在本地测试包安装和功能
- [ ] 测试不同配置场景（文件存储、数据库存储、认证等）
- [ ] 验证依赖项版本兼容性
- [ ] 确保符号包（.snupkg）正确生成

### 发布后检查

- [ ] 推送到 NuGet.org 并验证发布状态
- [ ] 在 NuGet.org 上发布预发布版本（如果适用）
- [ ] 测试已发布的包
- [ ] 创建 Git 标签并推送
- [ ] 更新 GitHub 仓库的发布信息
- [ ] 通知相关团队和用户
- [ ] 更新项目网站和文档（如果适用）
- [ ] 监控 NuGet.org 上的包下载情况

### 常见问题检查

- [ ] API Key 是否有效且具有正确权限
- [ ] 包版本号是否已被使用
- [ ] 依赖项版本是否兼容
- [ ] 包文件是否完整包含所有必要内容
- [ ] 静态资源和视图文件是否正确打包

## 常见问题

### Q: 包推送失败怎么办？

A: 根据错误信息进行排查：

- **401 Unauthorized**：检查 API Key 是否正确，确保具有推送权限
- **403 Forbidden**：API Key 权限不足，需要在 NuGet.org 上重新生成
- **409 Conflict**：包版本号已被使用，需要更新版本号
- **网络错误**：检查网络连接，尝试重新推送
- **包验证失败**：检查包内容是否符合 NuGet 规范

### Q: 如何在本地测试包？

A: 有两种方式：

1. **使用本地 NuGet 源**：
   ```bash
   dotnet nuget add source "./nupkgs" -n "LocalPackages"
   dotnet add package Chet.QuartzNet.UI --source "LocalPackages"
   ```

2. **直接引用 DLL**：
   - 在测试项目中直接引用生成的 DLL 文件
   - 需要确保所有依赖项都已正确引用

### Q: 如何处理依赖项冲突？

A: 可以尝试以下方法：

1. **更新依赖项版本**：
   ```bash
   dotnet add package SomeDependency -v 1.2.0
   ```

2. **使用依赖项版本范围**：
   ```xml
   <PackageReference Include="SomeDependency" Version="[1.0.0, 2.0.0)" />
   ```

3. **使用 NoWarn 属性暂时忽略警告**：
   ```xml
   <PackageReference Include="SomeDependency" Version="1.2.0" NoWarn="NU1605" />
   ```

4. **查看完整依赖树**：
   ```bash
   dotnet list package --include-transitive
   ```

### Q: 符号包（.snupkg）有什么用？

A: 符号包包含调试信息，允许用户在使用 NuGet 包时进行调试。它包含：

- PDB 文件（程序数据库）
- 源代码信息

用户可以在 Visual Studio 或其他 IDE 中启用符号服务器来使用这些符号包。

### Q: 如何处理包大小过大的问题？

A: 可以尝试以下方法：

- 减少不必要的依赖项
- 优化静态资源文件（压缩 CSS、JS、图片等）
- 考虑将大型资源拆分为单独的包
- 确保只包含必要的文件

### Q: 如何回滚发布的包？

A: 在 NuGet.org 上，您可以取消发布（unlist）已发布的包：

1. 登录 NuGet.org
2. 导航到包页面
3. 点击「Manage」按钮
4. 选择「Unlist package」

注意：已下载的包仍然可以使用，但新用户将无法在 NuGet.org 上看到或下载已取消发布的包。

## 相关链接

### NuGet 相关

- [NuGet 官方文档](https://docs.microsoft.com/en-us/nuget/)
- [发布包到 NuGet.org](https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package)
- [管理 NuGet API Keys](https://docs.microsoft.com/en-us/nuget/nuget-org/scoped-api-keys)
- [NuGet 包验证](https://docs.microsoft.com/en-us/nuget/nuget-org/package-validation)

### 技术文档

- [语义化版本控制](https://semver.org/)
- [.NET 包管理](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-pack)
- [Razor Class Libraries](https://docs.microsoft.com/en-us/aspnet/core/razor-pages/ui-class)