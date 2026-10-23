# Foundry Siege · 射击肉鸽基础战斗 Demo

Unity **6000.3.27f1**（revision `672d562a42a7`），C#、Built-In Render Pipeline、2D 物理，Windows x64 / Mono。首版是单关基础战斗，肉鸽成长后移。

私有仓库：[Gabbybro/foundry-siege](https://github.com/Gabbybro/foundry-siege)。完整执行记录见 [DemoExecutionPlan](Docs/DemoExecutionPlan.md)，Git 约定见 [GitWorkflow](Docs/GitWorkflow.md)。

## 试玩

本机运行 `Builds/Windows/FoundrySiege.exe`，需要保留旁边的 Data、MonoBleedingEdge 和 DLL。Git 不保存构建包；克隆后按下述命令构建。

按住左侧无人机向左下拖拽，松手发射。拉距只选择轻／中／重三档，角度连续；缩回取消区、右键或 Esc 取消，不扣弹药。上／下方 HUD 区域不参与发射。

默认 3 发、3 个装甲目标、3 组独立梁材结构。等待物理稳定或超时后装填，最后一发结算完才判断失败；清空目标胜利。右上角可重新开始，关闭窗口退出。所有参数仍是首轮试玩初值。

## Unity 与命令

打开 `Assets/Scenes/FoundryDemo.unity`。`InstallationCheck.unity` 仅保留安装验证用途。只有管理聊天运行 Unity；批处理前关闭本项目 Editor。

```powershell
pwsh -File Tools/Unity.ps1 Setup
pwsh -File Tools/Unity.ps1 DemoSetup
pwsh -File Tools/Unity.ps1 Validate
pwsh -File Tools/Unity.ps1 Build
pwsh -File Tools/Unity.ps1 Smoke
```

- `Setup`：环境配置；保留安装测试场景，正式场景存在时更新正式生成场景。
- `DemoSetup`：通过 Editor API 从 JSON 生成并绑定正式场景。
- `Validate`：检查版本、引用、真实 JsonUtility、配置异常与场景载入/保存回归。
- `Build`：生成 `Builds/Windows/FoundrySiege.exe`。
- `Smoke`：实际启动 Windows 玩家，验证碰撞、承重免伤、倒塌伤害、失败/胜利、重开、超时和末发结算。日志在 `Logs`。
- `Open`：打开正式场景。

锁定版本和依赖，不自动升级。`.setup`、`Library`、`Logs`、`Builds` 和 `UserSettings` 不进入 Git。

## 配置与搭建

统一数据源为 `Assets/Content/DemoLevel.json`，包含零件库、结构模板、摆放、发射与结算参数。AI 和 Unity 手调读写同一格式。

Unity 菜单 `Siege Demo/Level` 提供载入、保存、校验、快照重置、重置试射。手动摆放后先保存 JSON 再试射；Play 模式禁止写回。详细字段、样例和异常提示见 [LevelConfiguration](Docs/LevelConfiguration.md)。

本版使用程序化工业视觉和中文 HUD；不包含主动技能、实体碎片、独立游戏内编辑器、正式音效或肉鸽能力。验收范围与未测项见 [DemoDelivery](Docs/DemoDelivery.md)。
