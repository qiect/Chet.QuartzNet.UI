# GitHub Actions 自动发布 NuGet 包工作流方案

## 任务概述
创建一个 GitHub Actions 工作流，实现自动构建前端项目并发布 NuGet 包，包括主包和数据库存储相关包。

## 工作流设计

### 触发条件
- 当创建格式为 `*.*.*` 的标签时自动构建和发布
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
- 通过 Trusted Publishing（OIDC）获取临时 API Key
- 发布所有打包好的 NuGet 包，无需长期密钥

## 工作流文件内容

```yaml
name: Build and Publish NuGet Packages

on:
  push:
    tags:
      - '*.*.*'
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
      contents: read
      id-token: write   # Trusted Publishing 需要 OIDC 令牌
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

      - name: Install pnpm
        uses: pnpm/action-setup@v4
        with:
          version: latest

      - name: Cache pnpm dependencies
        uses: actions/cache@v4
        with:
          path: ~/.pnpm-store
          key: ${{ runner.os }}-pnpm-${{ hashFiles('**/pnpm-lock.yaml') }}
          restore-keys: |
            ${{ runner.os }}-pnpm-

      # 2. 前端项目构建
      - name: Install frontend dependencies (root)
        working-directory: src/Chet.QuartzNet.Web
        run: pnpm install --no-frozen-lockfile

      - name: Generate stubs for workspace packages
        working-directory: src/Chet.QuartzNet.Web
        run: pnpm -r run stub --if-present

      - name: Build frontend project
        working-directory: src/Chet.QuartzNet.Web
        run: pnpm run build:antd

      # 3. NuGet 包版本管理
      - name: Extract version from tag or input
        id: extract-version
        run: |
          # 从标签或输入获取版本号
          if [[ $GITHUB_REF == refs/tags/* ]]; then
            VERSION=${GITHUB_REF#refs/tags/}
          else
            VERSION=${{ github.event.inputs.version }}
          fi
          # 移除版本号中的 'v' 前缀（如果存在）
          VERSION=${VERSION#v}
          echo "VERSION=$VERSION" >> $GITHUB_ENV
          echo "VERSION=$VERSION" >> $GITHUB_OUTPUT

      - name: Update nuspec version
        run: |
          # 更新根目录下的nuspec文件
          sed -i "s/<version>.*<\/version>/<version>$VERSION<\/version>/" ./Chet.QuartzNet.UI.nuspec

      # 4. 主包打包
      - name: Build main package
        run: dotnet build src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj --configuration Release -p:PackageVersion=$VERSION

      - name: Pack main package
        run: dotnet pack src/Chet.QuartzNet.UI/Chet.QuartzNet.UI.csproj --configuration Release --no-build --output ./artifacts -p:PackageVersion=$VERSION

      # 5. 数据库存储包打包
      - name: Build MySQL package
        run: dotnet build src/Chet.QuartzNet.EFCore.MySql/Chet.QuartzNet.EFCore.MySQL.csproj --configuration Release -p:PackageVersion=$VERSION
      - name: Pack MySQL package
        run: dotnet pack src/Chet.QuartzNet.EFCore.MySql/Chet.QuartzNet.EFCore.MySQL.csproj --configuration Release --output ./artifacts -p:PackageVersion=$VERSION
      
      - name: Build PostgreSQL package
        run: dotnet build src/Chet.QuartzNet.EFCore.PostgreSql/Chet.QuartzNet.EFCore.PostgreSQL.csproj --configuration Release -p:PackageVersion=$VERSION
      - name: Pack PostgreSQL package
        run: dotnet pack src/Chet.QuartzNet.EFCore.PostgreSql/Chet.QuartzNet.EFCore.PostgreSQL.csproj --configuration Release --output ./artifacts -p:PackageVersion=$VERSION
      
      - name: Build SQLite package
        run: dotnet build src/Chet.QuartzNet.EFCore.SQLite/Chet.QuartzNet.EFCore.SQLite.csproj --configuration Release -p:PackageVersion=$VERSION
      - name: Pack SQLite package
        run: dotnet pack src/Chet.QuartzNet.EFCore.SQLite/Chet.QuartzNet.EFCore.SQLite.csproj --configuration Release --output ./artifacts -p:PackageVersion=$VERSION
      
      - name: Build SqlServer package
        run: dotnet build src/Chet.QuartzNet.EFCore.SqlServer/Chet.QuartzNet.EFCore.SqlServer.csproj --configuration Release -p:PackageVersion=$VERSION
      - name: Pack SqlServer package
        run: dotnet pack src/Chet.QuartzNet.EFCore.SqlServer/Chet.QuartzNet.EFCore.SqlServer.csproj --configuration Release --output ./artifacts -p:PackageVersion=$VERSION

      # 6. NuGet 包发布 (Trusted Publishing：OIDC 换取临时 API Key)
      - name: NuGet login (OIDC → temp API key)
        id: nuget-login
        uses: NuGet/login@v1
        with:
          user: qct   # nuget.org 用户名（个人资料名），不是邮箱

      - name: Publish NuGet packages
        run: dotnet nuget push ./artifacts/*.nupkg --source https://api.nuget.org/v3/index.json --api-key ${{ steps.nuget-login.outputs.NUGET_API_KEY }} --skip-duplicate
```

## 配置说明

### 工作流触发
- **自动触发**: 当创建格式为 `*.*.*` 或 `v*.*.*` 的标签时
- **手动触发**: 支持通过 GitHub Actions 界面手动触发，并指定版本号

### 版本号管理
- 优先从 GitHub 标签中提取版本号（如 `v1.2.3` 提取为 `1.2.3`）
- 手动触发时可自定义版本号
- 自动更新 `Chet.QuartzNet.UI.nuspec` 文件中的版本号

### 如何给 main 分支打标签

#### 方法一：使用 GitHub 网页界面

1. **进入仓库页面**
   - 登录 GitHub，进入项目仓库
   - 点击顶部导航栏中的 `Releases` 标签

2. **创建新版本**
   - 点击 `Create a new release` 按钮
   - 在 `Choose a tag` 下拉菜单中，输入新标签名称（格式：`v*.*.*`，如 `v1.2.3`）
   - 确保 `Target` 选择为 `main` 分支

3. **填写发布信息**
   - 在 `Release title` 中填写版本标题（建议与标签名一致或更具描述性）
   - 可选：在 `Describe this release` 中填写版本说明，包括新增功能、修复的bug等
   - 可选：勾选 `Set as the latest release` 标记为最新版本

4. **发布版本**
   - 点击 `Publish release` 按钮
   - 发布成功后，GitHub Actions 工作流会自动触发

#### 方法二：使用 Git 命令行

1. **克隆仓库（如果未克隆）**
   ```bash
   git clone https://github.com/qiect/Chet.QuartzNet.UI.git
   cd Chet.QuartzNet.UI
   ```

2. **切换到 main 分支**
   ```bash
   git checkout main
   git pull origin main
   ```

3. **创建标签**
   ```bash
   git tag v1.2.3
   ```

4. **推送标签到远程仓库**
   ```bash
   git push origin v1.2.3
   ```
   推送成功后，GitHub Actions 工作流会自动触发

#### 标签命名规范
- 标签格式：`v*.*.*`（如 `v1.0.0`、`v1.2.3`、`v2.0.0-beta.1`）
- 建议遵循 [语义化版本规范](https://semver.org/)
- 主要版本号：不兼容的 API 变更
- 次要版本号：向下兼容的功能性新增
- 修订号：向下兼容的问题修正

#### 注意事项
- 标签一旦创建并推送，会自动触发 GitHub Actions 工作流
- 建议在创建标签前，确保 main 分支包含所有需要发布的更改
- 标签创建后不可修改，如需修改需删除原标签并重新创建
- 每个标签对应一个特定版本的代码快照，便于版本回溯和管理

### NuGet Trusted Publishing（OIDC）配置

#### 优势
- **无需长期密钥**: 工作流用 GitHub OIDC 令牌换取 1 小时有效的临时 API Key，不存在密钥过期或泄露问题
- **自动轮换**: 每次运行都获取新凭证，无需定期更换
- **仓库绑定**: nuget.org 只信任指定仓库和指定工作流文件，安全性高于可复制的 API Key

#### 配置步骤

##### 1. 在 nuget.org 创建 Trusted Publishing 策略
1. 登录 [NuGet.org](https://www.nuget.org/)
2. 点击右上角头像，选择 `Trusted Publishing`
3. 创建策略并填写：
   - **Repository Owner**: `qiect`
   - **Repository**: `Chet.QuartzNet.UI`
   - **Workflow File**: `publish-nuget.yml`（只填文件名，不要带 `.github/workflows/` 路径）
   - **Environment**: 留空（工作流未使用 GitHub Environments）
   - **Scopes**: 勾选 `Push`，选择 `Push new packages and package versions`
   - **Glob Patterns and Packages**: `Chet.*`（覆盖全部 5 个包）
4. 保存策略

> 私有仓库首次创建的策略有 7 天试用期，需要在 7 天内成功发布一次才会永久生效。

##### 2. GitHub 侧配置
- 不需要新增任何 Secret，旧的 `NUGET_API_KEY` 可以删除
- 工作流需要声明 `id-token: write` 权限，用于申请 GitHub OIDC 令牌
- 策略按工作流文件名匹配，不要随意重命名 `publish-nuget.yml`

##### 3. 工作流配置说明
- OIDC 登录步骤：`NuGet/login@v1`，临时 Key 输出到 `steps.nuget-login.outputs.NUGET_API_KEY`
- 发布命令格式：`dotnet nuget push ./artifacts/*.nupkg --source https://api.nuget.org/v3/index.json --api-key ${{ steps.nuget-login.outputs.NUGET_API_KEY }} --skip-duplicate`
- 临时 Key 有效期 1 小时，登录步骤必须放在打包之后、推送之前

#### 安全最佳实践
- **最小权限**: 策略只勾选 `Push`，Glob 限定为 `Chet.*`
- **固定工作流文件**: 策略按文件名匹配，重命名后需同步更新策略
- **不要回退到长期 Key**: 长期 API Key 会过期，且一旦泄露影响所有包

### 注意事项
- 工作流文件必须命名为 `publish-nuget.yml`，否则 nuget.org 策略匹配失败
- 工作流必须声明 `id-token: write` 权限，否则无法获取 OIDC 令牌
- 临时 API Key 有效期只有 1 小时，登录步骤应紧挨发布步骤
- 发布命令中的 `--skip-duplicate` 参数用于跳过已存在的版本，避免发布失败
- 新建的 Trusted Publishing 策略需在 7 天内成功发布一次才能永久激活

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
3. 首次运行前确保已在 nuget.org 创建 Trusted Publishing 策略，且 Workflow File 与实际文件名一致
4. 建议先在测试环境中验证工作流的正确性
5. 策略的 Scope 需允许 `Push new packages and package versions`，Glob 需覆盖要发布的包
6. 工作流文件必须位于 `.github/workflows/` 目录下
7. 不要随意重命名发布工作流文件，否则需同步修改 nuget.org 策略

这个工作流方案完全符合用户的需求，能够自动化完成从前端构建到 NuGet 包发布的整个流程，提高开发效率并确保发布的一致性。