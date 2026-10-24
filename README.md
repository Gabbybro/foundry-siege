# Foundry Siege · 射击肉鸽 Demo v0.2

Unity **6000.3.27f1**（revision `672d562a42a7`），C#、Built-In Render Pipeline、2D 物理，Windows x64 / Mono。

本版包含两主动、七被动、正式五关与四个独立练习关。私有仓库：[Gabbybro/foundry-siege](https://github.com/Gabbybro/foundry-siege)。执行记录见 [RoguelikeDemoExecutionPlan](Docs/RoguelikeDemoExecutionPlan.md)，验收和限制见 [RoguelikeDemoDelivery](Docs/RoguelikeDemoDelivery.md)，版本约定见 [GitWorkflow](Docs/GitWorkflow.md)。

## 试玩

本机运行 `Builds/Windows-v0.2/FoundrySiege.exe`，保留旁边的 Data、MonoBleedingEdge 和 DLL。分发包为 `Builds/FoundrySiege-demo-v0.2-Windows.zip`。Git 保存源码与配置，构建包单独交付；克隆后按下述命令构建。旧 `Builds/Windows`、v0.1 ZIP 和 `demo-v0.1` 标签保留。

1. 开始正式作战，选择空中分裂或空中冲刺。每关 3 发，前四关获胜各从三个不同、未拥有的被动中选一个，第五关完成整轮。
2. 按住左侧无人机反向拖拽，松手发射。拉距选择轻／中／重三档，角度连续；缩回或右键取消，不扣弹药。Esc 打开暂停并取消瞄准。
3. 发射松手后，在飞行中用一次新的左键点击发动主动。分裂沿当时飞行方向展开三枚子弹；冲刺朝点击位置改向。每次原始发射只能发动一次，HUD 点击不会发动。
4. 等待弹体、延迟效果与倒塌结算后自动装填，最长 14 秒。末发继续结算，之后才判断失败。
5. 暂停或失败界面的“重试本关”恢复入关地图、3 发及构筑；“重开整轮”确认后清空构筑并重选主动。返回主菜单会放弃当前进度，关闭程序后不能续局。

四个练习入口可直接选配已有能力；默认分别体验分裂＋穿木、冲刺＋触地反弹、玻璃破片＋死亡冲击、石面跳弹＋顶点俯冲。练习不发正式奖励，也不把构筑带入正式局。

## Unity 与命令

打开 `Assets/Scenes/FoundryDemo.unity`；`InstallationCheck.unity` 仅用于环境检查。共享工程只由管理聊天运行 Unity，批处理前关闭本项目 Editor，构建前关闭正在运行的该版本游戏。

```powershell
pwsh -File Tools/Unity.ps1 Setup
pwsh -File Tools/Unity.ps1 DemoSetup
pwsh -File Tools/Unity.ps1 Validate
pwsh -File Tools/Unity.ps1 Build
pwsh -File Tools/Unity.ps1 Smoke
```

- `Setup` 配置环境；`DemoSetup` 用 Editor API 生成并绑定主场景、整轮参数和九关。
- `Validate` 检查锁定版本、引用、真实 JsonUtility、非法配置、场景载入/保存和奖励随机状态。
- `Build` 输出 `Builds/Windows-v0.2/FoundrySiege.exe`。
- `Smoke` 实际启动 Windows 玩家，验证基础物理、两主动/七被动、九关开场、四组合、正式奖励流程、两种重试、暂停、末发和超时。流程检查与真实通关路径分别记录，详见交付文档。
- `Open` 打开正式场景。日志统一位于 `Logs`。

锁定 Editor 与依赖，不自动升级。`.setup`、`Library`、`Logs`、`Builds`、`UserSettings` 和安装包不进入 Git。

## 配置与搭建

关卡数据为 `Assets/Content/Levels/C01.json` 至 `C05.json`、`P01.json` 至 `P04.json`；整轮顺序、练习默认构筑与能力参数位于 `Assets/Content/RunConfiguration.json`。旧 `DemoLevel.json` 保持兼容。

Unity 菜单 `Siege Demo/Level` 提供载入、保存、材质预设、校验和单关基础物理试射。先保存作者数据再试射，Play 中禁止写回倒塌现场。能力组合从正常主菜单进入练习验证。详细字段及 AI 编辑流程见 [LevelConfiguration](Docs/LevelConfiguration.md)。

首次配置可打开[地图与参数截图教程](Docs/Tutorial/地图与参数截图教程.html)：17 张实际界面截图，覆盖地图摆放、材质预设、保存与试射、发射和物理参数、两主动七被动及练习构筑。下载后用浏览器打开 HTML，并保留旁边的 `images` 文件夹。

配置作为 TextAsset 编入玩家包，修改后需重新 Validate、Build；包内 Reference JSON 仅供参考，不是热加载入口。程序化工业画面和中文 HUD 已接入；正式音效、跨程序续局、任意切割和游戏内编辑器未包含。
