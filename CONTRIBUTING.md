# 贡献指南

感谢你愿意参与 KeyTrace。这个项目的第一原则是**隐私**：任何会读取输入内容、窗口标题或屏幕内容的改动都会被拒绝。

## 开始之前

- 目标框架：**.NET 8**（`net8.0-windows`），只支持 Windows 10/11。
- 需要 .NET 8 SDK；建议使用 Visual Studio 2022 17.8+ 或 Rider。
- 先跑一遍构建与测试：

```powershell
dotnet build
dotnet test
```

## 目录约定

| 目录 | 放什么 | 注意 |
| --- | --- | --- |
| `src/KeyTrace.Core` | 与 UI 无关的业务逻辑 | 不引用 WPF；新增逻辑尽量放这里并补单元测试 |
| `src/KeyTrace.App` | WPF、ViewModel、自绘控件 | 视图只做绑定，不写业务逻辑 |
| `tests/KeyTrace.Core.Tests` | xUnit 测试 | 每个修复/特性都应带测试 |

## 代码风格

- 4 空格缩进，文件级命名空间，`enable` 可空引用类型。
- 事件/命令处理不要阻塞钩子线程；任何 IO 都必须离开 `IInputSink` 回调。
- 新增 SQL 必须使用参数化查询，并且写入语句保持幂等（`ON CONFLICT ... DO UPDATE`）。
- 面向用户的文本使用中文，代码注释可中英混排。

## 提交信息

使用 Conventional Commits 前缀：

```
feat: 增加分应用统计
fix: 修复跨天时活跃分钟被重复计数
docs: 补充 DPI 校准说明
test: 补充 CSV 转义用例
chore: 升级 Microsoft.Data.Sqlite
```

## Pull Request 检查清单

- [ ] `dotnet build` 无警告无错误
- [ ] `dotnet test` 全部通过
- [ ] 新行为有对应测试
- [ ] 没有新增网络请求、遥测或第三方上传逻辑
- [ ] 没有把 `data.db`、日志、`bin/`、`obj/` 提交进仓库
- [ ] UI 改动附截图（`docs/screenshots/`）

## 报告 Bug

请使用 Issue 模板，并尽量附上：

- Windows 版本、KeyTrace 版本
- `%AppData%\KeyTrace\logs\app.log` 中的相关片段（**请先确认没有可识别个人信息**）
- 复现步骤与实际/预期行为

## 许可证

贡献的代码默认以 [MIT](../LICENSE) 协议发布。
