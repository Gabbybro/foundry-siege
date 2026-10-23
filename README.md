# 愤怒的小鸟复刻项目

> `codex/demo-foundation` 分支是 2026-10-24 暂停时的 Demo 开发快照。流程框架已写，Combat、Configuration、Presentation 模块尚未落盘，当前快照不能编译。已通过 Setup 的安装基线在 `main` 的 `5374abd`；以下安装说明描述该基线，不代表 Demo 已完成。Git 约定见 `Docs/GitWorkflow.md`。

开发环境：Unity **6.3 LTS / 6000.3.27f1**，C#，Built-In 渲染管线，2D 物理。首个验证平台为 Windows 64 位，使用 Mono 后端。

当前是项目初始化和安装验证阶段，尚不是完整游戏。`Assets/Scenes/InstallationCheck.unity` 是环境测试场景：点击 **Launch bird** 发射红色圆形小鸟，撞击木块；点击 **Reset scene** 重置。场景中的图形是程序生成的占位素材。

## 打开项目

在 Unity Hub 中添加本目录，选择 **6000.3.27f1**，打开 `Assets/Scenes/InstallationCheck.unity` 后点击 Play。也可在本目录运行：

```powershell
pwsh -File .\Tools\Unity.ps1 Open
```

## 自动化命令

运行以下批处理命令前，关闭正在打开此项目的 Unity Editor。

```powershell
pwsh -File .\Tools\Unity.ps1 Setup
pwsh -File .\Tools\Unity.ps1 Validate
pwsh -File .\Tools\Unity.ps1 Build
pwsh -File .\Tools\Unity.ps1 Smoke
```

- `Setup`：配置项目并生成测试场景，已有测试场景不会被覆盖。
- `Validate`：检查编辑器版本、场景脚本引用和渲染管线。
- `Build`：输出 `Builds/Windows/BirdReplica.exe`。
- `Smoke`：运行打包后的程序，自动发射小鸟，必须撞击动态目标才能通过。
- 执行日志保存在 `Logs`，成功时会输出明确的完成标记。

安装包和下载校验信息暂存在 `.setup`，不进入 Git。依赖版本由 `Packages/manifest.json` 与 Unity 生成的 `packages-lock.json` 固定。

## 后续玩法开发

弹弓拖拽、轨迹预览、伤害与破坏、关卡数据、胜负结算、计分，以及正式美术和音效将在此基础上开发。
