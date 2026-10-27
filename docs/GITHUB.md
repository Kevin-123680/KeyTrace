# 上传到 GitHub

这个目录已经是一份可以直接推送的仓库骨架（源码、测试、CI、Issue 模板、许可证齐全）。
下面两种方式任选其一。

## 方式一：命令行（推荐）

项目本身不需要 Git，但推送需要一个装好 Git 的环境。

```powershell
cd KeyTrace

git init
git add .
git commit -m "feat: KeyTrace 1.0.0 - 本地键鼠统计"

# 在 GitHub 网页上新建一个空仓库（不要勾选 README / .gitignore / License），然后：
git branch -M main
git remote add origin https://github.com/<你的账号>/KeyTrace.git
git push -u origin main
```

首次推送需要 Personal Access Token 或在 `git credential manager` 中登录。

## 方式二：网页拖拽

1. 在 GitHub 新建空仓库。
2. 进入仓库 → **Add file → Upload files**。
3. 把 `KeyTrace` 目录里**除 `bin/`、`obj/`、`artifacts/` 之外的所有文件和文件夹**拖进去
   （GitHub 网页单次最多 100 个文件，本项目源码约 107 个，分两次拖入即可：先根目录 + `src/`，
   再 `tests/` + `docs/` + `.github/`）。
4. Commit。

> 注意：不要上传 `bin/`、`obj/`、`artifacts/`、`data.db*`、`logs/`、`settings.json`。
> 它们已经在 `.gitignore` 里，命令行方式会自动忽略。

## 推送后建议做的三件事

1. **改掉占位地址**：`src/KeyTrace.Core/AppInfo.cs` 里的 `RepositoryUrl`，以及
   `.github/ISSUE_TEMPLATE/config.yml` 里的讨论区链接，换成你的仓库地址。
2. **启用 Actions**：仓库 → Settings → Actions → General → Allow all actions。
   `build` 工作流会在每次 push 时构建、跑 90 个测试并上传 `KeyTrace.exe`。
3. **发第一个 Release**：

   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

   `release` 工作流会自动打包单文件 exe 并创建 Release。

## 可选：添加仓库徽章

把 `README.md` 顶部的静态徽章换成动态的：

```markdown
[![build](https://github.com/<你的账号>/KeyTrace/actions/workflows/build.yml/badge.svg)](https://github.com/<你的账号>/KeyTrace/actions/workflows/build.yml)
```

## 检查清单

- [ ] `git status` 里没有 `bin/`、`obj/`、`artifacts/`
- [ ] 没有提交任何 `data.db` / 日志 / 个人路径
- [ ] `LICENSE` 中的年份与署名符合你的需求
- [ ] `AppInfo.RepositoryUrl` 已替换
- [ ] CI 在 windows-latest 上通过
