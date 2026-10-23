# Git 记录与交付约定

2026-10-24：用户要求优先将工程上传 GitHub 私有仓库 `foundry-siege`，再推进 Demo。

## 分支与当前状态

- `main`：保留通过验收的里程碑，首个可玩版本标记 `demo-v0.1`。安装验证基线仍可通过提交 `5374abd` 回看。
- `codex/demo-foundation`：首版开发分支；历史提交 `3fdbced` 是暂停时不能编译的快照，后续已提交三个模块与集成修复。不要把历史快照状态误当成分支当前状态。
- 私有远程：`https://github.com/Gabbybro/foundry-siege`。两个分支已上传并核对提交哈希；备份完成后已恢复三个执行聊天。当前共享工作区只由管理者执行 Git 分支、提交、合并和推送；模块聊天只编辑归属文件。

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
git push -u origin codex/demo-foundation
```

查看旧版本优先使用 `git show <commit>:<path>`。不要在多个聊天同时编辑时切换整个共享目录；回退前先保留当前进度。
