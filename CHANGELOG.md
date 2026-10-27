# 更新日志

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/) 与 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 格式。
## [1.0.3] - 2026-10-27

### 修复

- **开机自启在版本升级后失效**：注册表 `Run\KeyTrace` 里存的是旧 exe 的绝对路径，
  升级/移动目录后该文件已不存在，Windows 开机时静默跳过，而设置页仍显示"已开启"。
  现在：
  - 启动时校验自启项是否指向当前 exe、目标文件是否存在，不一致即自动改写为当前程序
    （运行在临时目录或 dotnet 调试时不改写）；
  - 开启自启时同时清除 `StartupApproved` 里的禁用标记，避免"任务管理器里被禁用过就永远不启动"；
  - 设置页在开关下方显示实际指向的路径，失效时用警告色提示。
- 新增 16 个自启相关单元测试（注册表读写使用内存替身，不污染真实系统）。

### 优化

- 托盘隐藏时把界面刷新从 2 秒放宽到约 6 秒，并跳过页面级刷新，减少后台空转。
- "累计"卡片改为每 10 秒刷新一次（原来是每 2 秒一次跨表 `SUM`）。
- CSV 导出移出 UI 线程，导出期间按钮显示"导出中…"并自动禁用，大库不再卡界面。
- 托盘 tooltip 内容未变化时不再写入，减少无意义的 Shell IPC。
- Release 构建开启 `TreatWarningsAsErrors`，把警告挡在发布之外。

## [1.0.2] - 2026-10-27

### 变更

- **SQLite 改为使用 Windows 自带的 `winsqlite3.dll`**（`Microsoft.Data.Sqlite.Core` +
  `SQLitePCLRaw.bundle_winsqlite3`），程序自身不再携带 `e_sqlite3.dll` 等原生库。
  仍可通过 `-p:KeyTraceBundledSqlite=true` 切回随包携带 SQLite 的模式。
- 移除 `IncludeAllContentForSelfExtract`：单文件启动时只解包 WPF 必需的 5 个原生 DLL
  （约 8 MB），不再把 237 个托管文件一起解包。
- 新增「框架依赖」发布形态：`./scripts/publish.ps1 -SelfContained:$false`，
  产物约 2.7 MB，**完全不产生任何临时解包**，只需目标机安装 .NET 8 Desktop Runtime。
- `docs/BUILD.md` 增加「单文件与原生库」章节，说明两个解包相关属性的区别与三种解法。


## [1.0.1] - 2026-10-27

### 修复

- **启动时数据目录不可写会导致程序崩溃**：`%AppData%\KeyTrace` 无法创建时，之前会直接抛出
  `UnauthorizedAccessException` 并弹出 Windows「应用程序错误」，且不产生任何日志。
  现在会依次尝试 `KEYTRACE_HOME` → `%AppData%` → `%LocalAppData%` → 程序目录下的
  `KeyTraceData` → `%TEMP%`，用真实写探针确认后自动选用第一个可写目录。
- 异常处理提前到启动最开始，初始化阶段的任何失败都会写日志并弹出可读的中文提示，
  而不是 CLR 崩溃对话框。
- 新增 `%TEMP%\KeyTrace-fatal.log`，记录"连数据目录都建不出来"的失败原因。

## [1.0.0] - 2026-10-27

首个可用版本。

### 新增

- **采集**：基于 `SetWindowsHookEx` 的全局键盘 / 鼠标低级钩子，独立消息泵线程，只观察不拦截。
- **聚合**：按键连发去重、鼠标欧氏距离累加（过滤 >4000px 的瞬移）、滚轮格数换算、活跃分钟去重、实时 WPM 估算。
- **存储**：SQLite（WAL）按小时 UPSERT，`daily_stats` / `key_daily` / `hourly_dist` / `combo_daily` / `click_zone` / `active_minute` / `achievements` 七张表，带 schema 版本与幂等建表。
- **界面**：今日概览、键盘分析、鼠标分析、时间分析、设置五个页面；自绘折线图 / 柱状图 / 环形图 / 热力网格 / 月历，无第三方图表依赖。
- **键盘**：QWERTY 热图、Top 10 按键、快捷键组合统计、WPM。
- **鼠标**：7 天里程折线、左/右/中占比、滚轮、可选点击热区。
- **时间**：7×24 热力网格、月历热力图、连续天数、本周 vs 上周。
- **成就**：16 个徽章与解锁持久化。
- **隐私**：敏感进程 / 标题关键词自动静默；只记录 VK 码，不记录输入内容；可选忽略注入输入。
- **工程**：开机自启、托盘常驻、关闭最小化、单实例唤醒、CSV 导出、一键清空、文件日志、单文件自包含发布。
- **测试**：90 个 xUnit 单元测试覆盖核心逻辑。

### 已知限制

- 仅支持 Windows 10 / 11（x64）。
- 未做代码签名，首次运行会触发 SmartScreen 提示。
- 未提供安装包，直接分发单文件 exe。
