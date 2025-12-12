# GitHub Actions 自动发布 NuGet 包工作流方案

## 任务概述
创建一个 GitHub Actions 工作流，实现自动构建前端项目并发布 NuGet 包，包括主包和数据库存储相关包。

## 工作流设计

### 触发条件
- 当推送到 `main` 分支时自动构建和发布
- 支持手动触发

### 环境设置
- **.NET 版本**: 8.0
- **Node.js 版本**: 20.x
- **包管理器**: pnpm

### 核心步骤

#### 1. 代码检查与依赖安装
- 检出代码仓库
- 设置 .NET 环境
- 设置 Node.js 环境
- 安装 pnpm

#### 2. 前端项目构建
- 进入 `src/Chet.QuartzNet.Web/apps/web-antd` 目录
- 安装前端依赖
- 执行 `pnpm build`，将构建结果输出到指定目录（vite会自动清空输出目录）

#### 3. NuGet 包版本管理
- 从 GitHub 标签或环境变量获取版本号
- 更新 `Chet.QuartzNet.UI.nuspec` 文件中的版本号

#### 4. 主包打包
- 构建 `src/Chet.QuartzNet.UI` 项目
- 使用 `dotnet pack` 命令打包

#### 5. 数据库存储包打包
- 构建并打包以下项目：
  - `src/Chet.QuartzNet.EFCore.MySql`
  - `src/Chet.QuartzNet.EFCore.PostgreSql`
  - `src/Chet.QuartzNet.EFCore.SQLite`
  - `src/Chet.QuartzNet.EFCore.SqlServer`

#### 6. NuGet 包发布
- 配置 NuGet 源
- 使用 API 密钥发布所有打包好的 NuGet 包

## 工作流文件内容

```yaml
name: Build and Publish NuGet Packages

on:
  push:
    branches:
      - main
    tags:
      - 'v*.*.*'
  workflow_dispatch:
    inputs:
      version:
        description: 'NuGet Package Version'
        required: true
        default: '1.0.0'

jobs:
  build-and-publish:
    runs-on: ubuntu-latest
    permissions:
      id-token: write
      contents: read
    steps:
      # 1. 代码检查与依赖安装
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET 8.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Setup Node.js 20
        uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: 'pnpm'

      - name: Install pnpm
        run: npm install -g pnpm

      # 2. 前端项目构建
      - name: Install frontend dependencies
        working-directory: src/Chet.QuartzNet.Web/apps/web-antd
        run: pnpm install

      - name: Build frontend project
        working-directory: src/Chet.QuartzNet.Web/apps/web-antd
        run: pnpm build

      # 3. NuGet 包版本管理
      - name: Extract version from tag
        id: extract-version
        run: |
          if [[ $GITHUB_REF == refs/tags/v* ]]; then
            VERSION=${GITHUB_REF#refs/tags/v}
          else
            VERSION=${{ github.event.inputs.version }}
          fi
          echo "VERSION=$VERSION" >> $GITHUB_ENV

      - name: Update nuspec version
        run: |
          sed -i "s/<version>.*<\/version>/<version>$VERSION<\/version>/" Chet.QuartzNet.UI.nuspec

      # 4. 主包打包
      - name: Build main package
        run: dotnet build src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj --configuration Release

      - name: Pack main package
        run: dotnet pack src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj --configuration Release --no-build --output ./artifacts

      # 5. 数据库存储包打包
      - name: Pack database packages
        run: |
          dotnet pack src/Chet.QuartzNet.EFCore.MySql/Chet.QuartzNet.EFCore.MySql.csproj --configuration Release --output ./artifacts
          dotnet pack src/Chet.QuartzNet.EFCore.PostgreSql/Chet.QuartzNet.EFCore.PostgreSql.csproj --configuration Release --output ./artifacts
          dotnet pack src/Chet.QuartzNet.EFCore.SQLite/Chet.QuartzNet.EFCore.SQLite.csproj --configuration Release --output ./artifacts
          dotnet pack src/Chet.QuartzNet.EFCore.SqlServer/Chet.QuartzNet.EFCore.SqlServer.csproj --configuration Release --output ./artifacts

      # 6. NuGet 包发布 (使用Trusted Publishing)
      - name: Publish NuGet packages
        run: dotnet nuget push ./artifacts/*.nupkg --source https://api.nuget.org/v3/index.json --skip-duplicate
```

## 配置说明

### 工作流触发
- **自动触发**: 当推送到 `main` 分支或创建格式为 `v*.*.*` 的标签时
- **手动触发**: 支持通过 GitHub Actions 界面手动触发，并指定版本号

### 版本号管理
- 优先从 GitHub 标签中提取版本号（如 `v1.2.3` 提取为 `1.2.3`）
- 手动触发时可自定义版本号
- 自动更新 `Chet.QuartzNet.UI.nuspec` 文件中的版本号

### NuGet Trusted Publishing (OIDC)

#### 优势
- **更安全**: 无需存储长期有效的 API 密钥
- **自动化**: 使用 GitHub Actions OIDC 自动获取短期令牌
- **简化配置**: 减少手动密钥管理
- **增强审计**: 更好的身份验证和授权跟踪

#### 配置步骤

##### 1. 在 NuGet.org 上配置 Trusted Publisher
1. 登录 [NuGet.org](https://www.nuget.org/)
2. 点击右上角头像，选择 `Account Settings`
3. 在左侧导航栏中选择 `Trusted Publishers`
4. 点击 `Add Trusted Publisher` 按钮
5. 选择 `GitHub` 作为 Publisher Type
6. 输入 GitHub 仓库所有者和仓库名称（如 `qiect/Chet.QuartzNet.UI`）
7. 选择要信任的工作流分支（建议选择 `main`）
8. 点击 `Add` 完成配置

##### 2. 在 GitHub 上配置工作流
1. 进入 GitHub 仓库
2. 点击 `Actions` 标签页
3. 点击 `New workflow` 按钮
4. 选择 `Set up a workflow yourself`
5. 将工作流文件内容复制到编辑器中
6. 确保工作流包含以下权限设置：
   ```yaml
   permissions:
     id-token: write
     contents: read
   ```
7. 配置触发条件：
   ```yaml
   on:
     push:
       branches:
         - main
       tags:
         - 'v*.*.*'
     workflow_dispatch:
       inputs:
         version:
           description: 'NuGet Package Version'
           required: true
           default: '1.0.0'
   ```
8. 保存工作流文件（建议命名为 `build.yml`）
9. 测试工作流：
   - 点击 `Actions` 标签页
   - 选择刚创建的工作流
   - 点击 `Run workflow` 按钮
   - 输入版本号，点击 `Run workflow`

#### 工作流权限说明
- `id-token: write`: 允许工作流获取 OIDC 令牌用于身份验证
- `contents: read`: 允许工作流读取仓库内容

#### 发布命令
```yaml
- name: Publish NuGet packages
  run: dotnet nuget push ./artifacts/*.nupkg --source https://api.nuget.org/v3/index.json --skip-duplicate
```

#### 注意事项
- Trusted Publishing 不需要配置 API 密钥
- dotnet CLI 8.0+ 自动支持 OIDC 身份验证
- 确保 NuGet.org 上的 Trusted Publisher 配置与 GitHub 仓库信息匹配
- 工作流文件必须位于 `.github/workflows/` 目录下

### 迁移指南

如果您之前使用 API 密钥发布，迁移到 Trusted Publishing 只需：
1. 在 NuGet.org 上配置 Trusted Publisher
2. 更新工作流，添加 OIDC 权限
3. 移除发布命令中的 `--api-key` 参数
4. 可选：删除 GitHub Secrets 中的 API 密钥

## 预期效果

1. **前端构建**: 成功将前端项目构建到指定目录
2. **包打包**: 生成所有必要的 NuGet 包
3. **自动发布**: 成功将 NuGet 包发布到 NuGet.org
4. **版本一致**: 所有包使用相同的版本号
5. **支持手动触发**: 允许手动指定版本号进行发布

## 后续扩展建议

1. **添加测试步骤**: 在打包前运行单元测试，确保代码质量
2. **多环境支持**: 区分预览版和正式版发布
3. **发布前验证**: 添加 NuGet 包验证步骤
4. **通知机制**: 发布成功后发送通知
5. **分支管理**: 支持不同分支发布不同版本

## 注意事项

1. 前端项目的构建输出目录需与主项目的静态资源目录匹配
2. 版本号格式需符合语义化版本规范
3. 首次运行时可能需要验证 NuGet.org 的 Trusted Publisher 权限
4. 建议先在测试环境中验证工作流的正确性
5. 确保 NuGet.org 上已正确配置 Trusted Publisher
6. 工作流文件必须位于 `.github/workflows/` 目录下

这个工作流方案完全符合用户的需求，能够自动化完成从前端构建到 NuGet 包发布的整个流程，提高开发效率并确保发布的一致性。