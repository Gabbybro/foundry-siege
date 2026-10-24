# Git 记录与交付约定

2026-10-24：用户要求优先将工程上传 GitHub 私有仓库 `foundry-siege`，再推进 Demo。

## 分支与当前状态

- `main`：只保存通过验收的里程碑；本轮使用 `demo-v0.2` 标记。首个可玩版本 `demo-v0.1=6c8bed6` 和安装基线 `5374abd` 保留。
- `codex/roguelike-demo`：v0.2 开发分支；`4cfa07e` 公共契约、`6bf39e8` 早期 WIP、`4aa2a15` 实际验证后的运行源码。交付文档另作提交，包内 manifest 记录程序源版本。
- `codex/demo-foundation`：首版开发分支；历史提交 `3fdbced` 是暂停时不能编译的快照，后续已提交三个模块与集成修复。不要把历史快照状态误当成分支当前状态。
- 私有远程：`https://github.com/Gabbybro/foundry-siege`，2026-10-24 再次通过 GitHub API 核对 `private=true`。共享工作区只由管理者执行 Git 分支、提交、合并和推送；模块聊天只编辑归属文件。正常推送后逐一核对 main、开发分支和标签的远端对象。

## 后续记录方式

1. 每个可 review 的模块和修复保留一个清晰提交。未集成快照用 `wip:`，记录未测项；完整修复用 `fix:`，实现用 `feat:`。
2. 集成后依次运行 `Tools/Unity.ps1 DemoSetup`、`Validate`、`Build`、`Smoke`。实际通过后更新验收记录，再将里程碑合入 `main` 并推送。
3. 重要阶段同时保留本地提交和远程备份。正常推送，不强推、不改写共享历史。
4. 原场景、源码、ProjectSettings、Packages 锁文件与已有 `.meta` 纳入版本记录。新资产的 `.meta` 在 Unity 导入后一起提交，不重建已有 GUID。
5. `.setup`、`Library`、`Logs`、`Builds`、`UserSettings`、编辑器缓存和安装包由 `.gitignore` 排除；试玩包另行交付。

## 常用检查

```powershell
git status --short
git log --oneline --decorate -8
git remote -v
git diff --check
git push -u origin codex/roguelike-demo
```

查看旧版本优先使用 `git show <commit>:<path>`。不要在多个聊天同时编辑时切换整个共享目录；回退前先保留当前进度。
