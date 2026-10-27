# 架构说明

## 分层

```
┌─────────────────────────────────────────────────────────────┐
│ 展示层  KeyTrace.App (WPF)                                   │
│   Views / ViewModels / Controls(自绘图表) / TrayService      │
└───────────────▲──────────────────────────────┬──────────────┘
                │ TodayLive / DailyStat          │ LoadAsync
┌───────────────┴──────────────────────────────▼──────────────┐
│ 应用服务 KeyTrace.Core                                       │
│   LiveStatsProvider · CollectionService · AchievementService│
│   SettingsService · StartupRegistrar                        │
└───────┬───────────────▲──────────────────────▲──────────────┘
        │ 累加           │ Drain(delta)          │ UPSERT / SELECT
┌───────▼───────────────┴──────────┐   ┌───────┴──────────────┐
│ 处理层 DataAggregator            │   │ 存储层 SQLite         │
│  按小时计数器 / 活跃分钟 / WPM    │   │  SqliteStatsRepository│
└───────▲──────────────────────────┘   └──────────────────────┘
        │ 原始事件（int / enum）
┌───────┴──────────────────────────┐
│ 采集层 GlobalHookInputSource      │
│  WH_KEYBOARD_LL / WH_MOUSE_LL     │
└──────────────────────────────────┘
```

## 线程模型

| 线程 | 职责 | 约束 |
| --- | --- | --- |
| UI 线程（STA） | WPF、DispatcherTimer（2s 刷新状态） | 不做重 IO |
| 钩子线程（STA + 消息泵） | `SetWindowsHookEx` 回调 → `IInputSink` | 回调内只做数值累加，禁止阻塞/UI |
| 定时器线程（ThreadPool） | `CollectionService.Flush()` 批量写库 | 与钩子线程通过 `DataAggregator` 的锁串行化 |

`DataAggregator` 的所有公开方法都持有同一把锁；锁内只做字典/累加，临界区在微秒级。
`KeyTraceDatabase` 使用单连接 + 锁，`Flush` 与查询天然串行，避免 SQLITE_BUSY。

## 关键算法

### 1. 按键连发去重

钩子只能看到 `WM_KEYDOWN` 序列，长按会产生约 30ms 间隔的重复 KeyDown。采集服务维护
`_pressedKeys` 集合：同一个 VK 在集合中时，800ms 内的 KeyDown 视为自动连发丢弃；
收到 KeyUp 后移除。若超过窗口仍处于"按下"状态（漏了 KeyUp），按新按键处理。

### 2. 鼠标距离

```
Δ = √((x₁-x₀)² + (y₁-y₀)²)
if Δ > 4000px: 丢弃（SetCursorPos / 切换显示器会瞬移）
距离 += Δ
```

**米 = 像素 × 校准系数**。系数由用户"划 10cm"反推：

```
metersPerPixel = 0.10 m / 实测像素距离
```

默认值 `0.0254 / 96`（96 DPI 假设）。数据库同时保存像素（权威）与写入时换算的米，
界面始终用**当前**校准值重新计算，因此重新校准后历史数据自动更新。

### 3. 活跃分钟

每个输入事件把 `hour * 60 + minute` 写入当天的 `HashSet<int>`；落盘时写入
`active_minute` 表（`INSERT OR IGNORE`），再用 `COUNT(*)` 刷新 `daily_stats.active_min`。
重复落盘、跨天、重启都不会重复计数。

### 4. WPM

按秒维护 60 秒环形缓冲，只统计"打字键"（字母/数字/标点/空格/退格/回车/小键盘）：

```
WPM = (窗口内字符数 / 5) / (窗口秒数 / 60)
窗口秒数 = clamp(最老的字符距今秒数 + 1, 5, 60)
```

### 5. 隐私静默

`ForegroundWatcher` 以 1 秒缓存读取 `GetForegroundWindow` + 进程名 + 标题，
`PrivacyFilter` 与用户名单比对。命中时：

1. 所有输入事件直接返回（连 KeyUp 状态机也只维护不计数）；
2. `DataAggregator.ResetPointerAnchor()` 丢弃坐标锚点；
3. 退出静默后第一帧只重建锚点，不把跨窗口的"跳变"算进距离。

## 数据库

```sql
daily_stats(date PK, total_keys, mouse_m, total_clicks, active_min,
            mouse_px, left_clicks, right_clicks, middle_clicks, scroll_steps)
key_daily(date, key_code, key_name, count, PK(date, key_code))
hourly_dist(date, hour, key_count, mouse_m, mouse_px, PK(date, hour))
combo_daily(date, combo, count, PK(date, combo))
click_zone(date, x_bucket, y_bucket, count, PK(date, x_bucket, y_bucket))
active_minute(date, minute, PK(date, minute))
achievements(id PK, unlocked_at)
```

- `PRAGMA user_version` 记录 schema 版本；当前 v1，`Schema.EnsureCreated` 幂等。
- 所有写语句为 `INSERT ... ON CONFLICT DO UPDATE SET x = x + excluded.x`。
- 单文件发布时 SQLite 原生库需要解包到临时目录（.NET 单文件机制），见 [BUILD.md](BUILD.md)。

## 视图模型

```
MainViewModel
├── Pages: ObservableCollection<PageViewModel>
│   ├── DashboardViewModel   4 卡片 + 24h 柱状 + 周对比 + 成就
│   ├── KeyboardViewModel    热图 + Top10 + 快捷键 + WPM
│   ├── MouseViewModel       7 天折线 + 环形占比 + 点击热区
│   ├── TimeViewModel        7×24 网格 + 月历 + 连续天数
│   └── SettingsViewModel    开关 / 名单 / 校准 / 导出 / 清空
└── DispatcherTimer(2s) → 刷新状态、托盘 tooltip、当前页 OnLiveTick
```

页面切换由 `ContentControl + DataTemplate` 完成；`MouseViewModel` 通过
`SettingsRequested` 事件请求跳转到设置页（避免 ViewModel 互相引用）。

## 扩展点

| 想加什么 | 改哪里 |
| --- | --- |
| 新的统计维度 | `DataAggregator` 加计数器 → `AggregateDelta`/`HourAggregate` → `SqliteStatsRepository.ApplyDelta` → 新表 |
| 新的成就 | `AchievementCatalog.All` 加一条定义 + `AchievementContext` 需要的字段 |
| 新的图表 | `Controls/` 继承 `FrameworkElement`，重写 `OnRender`，DP 加 `AffectsRender` |
| 新的日期范围 | `LiveStatsProvider.GetRecentDays` / `IStatsRepository.GetDailyRange` |
| 分应用统计 | 在 `CollectionService` 中按前台进程名聚合（注意不要把标题写入数据库） |
