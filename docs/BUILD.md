# 构建与发布

## 环境要求

| 项 | 版本 |
| --- | --- |
| 操作系统 | Windows 10 1809+ / Windows 11 |
| SDK | .NET 8 SDK（8.0.100+） |
| 运行时（仅开发） | Microsoft.WindowsDesktop.App 8.0.x |
| 可选 | Python 3 + Pillow（重新生成图标） |

```powershell
dotnet --info
```

## 常用命令

```powershell
# 还原 + 构建
dotnet build

# 单元测试（90 个）
dotnet test

# 直接运行（开发）
dotnet run --project src/KeyTrace.App/KeyTrace.App.csproj

# 使用独立数据目录运行，避免污染真实统计
$env:KEYTRACE_HOME = "$PWD\artifacts\dev-home"
dotnet run --project src/KeyTrace.App/KeyTrace.App.csproj
```

## 发布单文件 exe

```powershell
./scripts/publish.ps1                      # 默认 win-x64 自包含单文件
./scripts/publish.ps1 -Runtime win-arm64   # 其它架构
./scripts/publish.ps1 -FrameworkDependent  # 依赖运行时的小体积版本
```

等价的原始命令：

```powershell
dotnet publish src/KeyTrace.App/KeyTrace.App.csproj `
  -c Release `
  -p:PublishSingleFile=true `
  -r win-x64 `
  --self-contained true `
  -o artifacts/publish
```

产物：`artifacts/publish/KeyTrace.exe`（约 65 MB，目标机无需安装 .NET）。

### 单文件解包说明

`Microsoft.Data.Sqlite` 携带原生 `e_sqlite3.dll`，单文件应用启动时会把它解包到
`%TEMP%\.net\KeyTrace\<hash>\`。如果目标机器的 `%TEMP%` 不可写，会看到：

```
Failed to determine location for extracting embedded files.
DOTNET_BUNDLE_EXTRACT_BASE_DIR is not set, and a read-write cache directory couldn't be created.
```

解决办法：设置一个可写目录后启动。

```powershell
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "$env:LOCALAPPDATA\KeyTrace\bundle"
.\KeyTrace.exe
```

（在正常桌面环境下无需任何设置。）

## 图标

```powershell
python scripts/make_icon.py
```

会用 Pillow 生成 `src/KeyTrace.App/Assets/app.ico`（16–256 共 9 个尺寸）与 `app.png` 预览图。
脚本只依赖 Pillow，不下载任何素材。

## 代码质量

- `Nullable` 全开，`dotnet build` 默认 0 警告。
- `.editorconfig` 统一缩进与命名空间风格。
- CI（`.github/workflows/build.yml`）在 `windows-latest` 上执行 restore → build → test → publish，并上传 exe。

## 常见构建问题

| 现象 | 原因 / 解决 |
| --- | --- |
| `NETSDK1135 SupportedOSPlatformVersion ... higher than TargetPlatformVersion` | 本项目的 TFM 是 `net8.0-windows`，不要额外提高 `SupportedOSPlatformVersion` |
| `MSB3021 / 文件被占用` | 关闭正在运行的 KeyTrace.exe 后重试 |
| 发布时下载 runtime pack 很慢 | 首次 `-r win-x64 --self-contained` 需要从 NuGet 拉取运行时包，之后走缓存 |
| XAML 报 `MC3074 找不到资源` | 检查 `App.xaml` 里合并的资源字典路径是否与文件名一致 |

## 目录约定

- 源码：`src/`；测试：`tests/`；文档：`docs/`；脚本：`scripts/`。
- 构建产物（`bin/`、`obj/`、`artifacts/`）已在 `.gitignore` 中忽略，**不要提交**。
