# Foundry Siege · Demo v0.2 交付与验收

日期：2026-10-24（Asia/Shanghai）。Unity **6000.3.27f1 / 672d562a42a7**，Windows x64 / Mono，Built-In，Development / StrictMode 构建。

## 运行与操作

- 本机程序：`Builds/Windows-v0.2/FoundrySiege.exe`，保留同目录 Data、MonoBleedingEdge 和 DLL。
- 分发包：`Builds/FoundrySiege-demo-v0.2-Windows.zip`；旁边的 `.sha256.txt` 校验压缩包，包内 `build-manifest.json` 记录源码提交和文件 SHA-256。
- 正式局先选空中分裂或空中冲刺，再连续挑战 C01—C05，每关 3 发。前四关各选一个被动，第五关进入整轮完成。
- 按住左侧无人机反向拖拽，松手发射；轻／中／重是固定三档，角度连续。缩回或右键取消；Esc 取消瞄准并切换暂停。
- 发射松手后，飞行中用新的左键点击发动主动，每次原始发射一次。分裂沿当前飞行方向展开，冲刺朝点击位置改向；点击 UI 不消耗主动。
- 等待本发弹体、延迟效果与倒塌结算，最长 14 秒。末发继续结算，清空目标仍可获胜。
- “重试本关”保留入关构筑和奖励随机状态，恢复地图与 3 发；“重开整轮”经确认后清空构筑并重选主动。返回主菜单会放弃当前进度，关闭程序后不能续局。

压缩包 61,500,128 字节、242 项；241 个载荷文件已逐项核对 ZIP 内大小与 SHA-256，另含 manifest。ZIP SHA-256：`33AFC6ECA65848982BDCCAE968F8B55B67A1B7CAED7C72491EBAF1C21E23870D`。包内交付说明是在生成 ZIP 校验值前复制的同版说明，因此该校验值只记录在包外文档与校验文件中。

## 内容与配置入口

两主动：空中分裂、空中冲刺。七被动：穿木一次、触地反弹、停驻爆破、死亡冲击、玻璃破片、石面跳弹、顶点俯冲。规则细节见 `CombatWhitepaperSnapshot.md` 和 `CombatModule.md`。

五个正式关有独立布局，初始敌人数为 2/3/3/2/4；四个独立练习关初始敌人数为 3/2/3/2。练习可选择主动及任意已有被动，其构筑与奖励不进入正式局。

| 入口 | 用途 |
| --- | --- |
| `Assets/Content/RunConfiguration.json` | 正式顺序、练习默认构筑、所有能力数值 |
| `Assets/Content/Levels/C01.json`—`C05.json` | 五个正式关的零件、材质、摆放、发射与结算参数 |
| `Assets/Content/Levels/P01.json`—`P04.json` | 四组组合练习的实际关卡数据 |
| `Siege Demo/Level` 菜单 | Unity 载入、摆放、材质预设、保存、校验和基础物理试射 |
| `Assets/Content/DemoLevel.json` | 保留 v0.1 旧关格式与基础试射兼容 |

配置作为 TextAsset 编入游戏。包内 Reference 文件供策划和 AI 参考，**不是 Windows 热加载入口**；修改后需在锁定 Unity 中 Validate、Build，再运行 Smoke。Play 中禁止把倒塌现场保存为初始关卡，详细步骤见 `LevelConfiguration.md`。

## 实际验证

管理者使用锁定 Editor 和实际 Windows 玩家执行。模块独立编译、托管断言与原生物理检查分开记录。

| 范围 | 已运行的检查与证据 |
| --- | --- |
| 场景与配置 | DemoSetup 已生成并绑定场景；最终 Build 内再次 Validate。真实 JsonUtility、九关引用/布局/目标数、全部能力字段、未知/遗漏/错误类型/非法值，以及旧关格式通过。`PROJECT_VALIDATION_SUCCESS`、`ROGUELIKE_CONFIGURATION_CHECKS_SUCCESS` |
| 作者数据读写 | 实际 Unity 预览→移动→保存→重载通过；非法载入/重复标记拒绝且原文件保留。`LEVEL_AUTHORING_ROUNDTRIP_SUCCESS` |
| 基础物理 | 双方碰撞伤害去重、静止承重免伤、梁材坠落杀敌、重复死亡幂等；原生刚体与碰撞器，未模拟物理回调 |
| 两主动 | 实际速度、分裂三枚和总质量、当前方向/固定速度、兄弟互不碰撞、每体额度刷新；冲刺实际速率加增量、无效方向不扣、随后继续受重力 |
| 七被动与伤害 | 穿一块木但被第二块阻挡、厚木不耗次数；地面优先于石面、擦撞与正撞；自然顶点与强制下冲区别、下降出生分裂；停驻计时打断和爆破；两破片共享额度/不继承；范围伤害与推力遮挡快照、死亡冲击去重与出界无冲击。`COMBAT_REGRESSION_SUCCESS` |
| 九关开场 | 每关静置 4 游戏秒，所有配置物体存在、初始 HP/目标数不变，位移 <0.15m、转角 <4°；覆盖整轮换关和重试后的进入 |
| 正式流程 | 主动选择、连续五关、四次三选一、不包含已获被动、候选互异、只授予一次、第五关无多余奖励；流程夹具使用明确的 Boundary 消灭事件，与下表真实通关解法区分 |
| 重试、暂停、结算 | 飞行/分裂中重试、停驻爆破倒计时中重试、失败后重试；入关构筑/3 发/RNG 恢复、旧 generation 清理；整轮重开清构筑；暂停冻结时间和物理；真实末发倒塌获胜；超时取消弹体和待触发效果 |
| 完整玩家回归 | `Tools/Unity.ps1 Smoke` 的 `ROGUELIKE_SMOKE_SUCCESS`，包含四练习动作顺序和上述 CombatRegression |

四组练习以真实发射和正式能力实现验证：

| 练习 | 实际动作与观察 |
| --- | --- |
| P01 | 重档 12°，原弹穿外墙后分裂；至少两个不同子弹再次穿内层木墙，刷新机会实际生效 |
| P02 | 重档 40°越墙后向 `(2.8,-3.6)` 冲刺；随后触地反弹并命中木台底部，目标清空 |
| P03 | 重档 5°击碎玻璃；破片直接杀首敌→死亡冲击拆左支撑→屋顶/目标真实碰撞→后续死亡冲击。一发清空三目标，8 次伤害、3 次死亡冲击；检查等到整发结算，不在首个效果后提前结束 |
| P04 | 重档 5°先擦石升高，再经过自然顶点触发俯冲，随后 `rear-roof`/`rear-right` 受伤；明确要求伤害发生在俯冲之后 |

正式关使用同一玩家、真实作者布局，在**不使用主动和被动**时搜索到下列基础路径。全部为重档角度，最多三发；自动搜索使用加速时间，证明路径存在，不承诺手动误差下每次结果相同。

| 关卡 | 实测重档角度序列 | 日志 |
| --- | --- | --- |
| C01 | 5° | `Logs/v02-final-solutions.log` |
| C02 | 5°、7.5°、5° | 同上 |
| C03 | 7.5°、5° | `Logs/v02-final-c03-solution.log`，最终桥下敌人 HP=4 |
| C04 | 5° | `Logs/v02-final-solutions.log` |
| C05 | 12.5°、7.5° | 同上 |

`v02-final-solutions.log` 中 C03 的旧 HP=5 搜索失败记录保留，未改写成成功；其余四关与最终配置相同。C03 单独在最终数据重建后实际通过。自动程序使用公开发射 API，未直接扣血或替换刚体结果来证明这些解法。

原生 1600×900 窗口已操作：主菜单、P01 配装和进入、拖拽发射、发射后独立点击分裂/冲刺、C01 实际获胜、奖励三选一进入 C02、短拖取消、三发落空后失败、失败界面重试恢复 C02/3 发/冲刺＋穿木、重开整轮确认后回 C01 并清空被动。`Logs/v02-final-ui.log` 保留输入、结果、构筑和相同入关 RNG。后续变化仅为 C03 关卡初值及验收脚本，玩法和 UI 实现相同；最终数据另经玩家物理回归。四分辨率 76 项布局断言属于静态布局检查，只有 1600×900 实际窗口已逐项操作。

复现入口：

```powershell
pwsh -File Tools/Unity.ps1 Validate
pwsh -File Tools/Unity.ps1 Build
pwsh -File Tools/Unity.ps1 Smoke
```

显式开发测试参数还包括 `-practiceTest P03`、`-combatTest`、`-solveRun`（可加 `-level C03`），配合 `-batchmode -nographics -logFile <绝对路径>`。聚焦检查成功为 `ROGUELIKE_FOCUSED_SMOKE_SUCCESS`，不能用它代替完整 Smoke。最终程序集 SHA-256：`7C95F806901E11E0A89893E202EF8CCFDBA9863A5E071EBD19CC76ABD22D63FD`。

## 已知限制

- 本版是可玩 Demo：无正式音效、跨程序续局、任意切割或游戏内编辑器。普通破坏碎屑只做表现，能力玻璃破片有实际物理和伤害。
- Development Build 与当前 Unity 许可证水印保留，未改变授权设置。
- 没有穷举两主动与所有被动子集在每关的全部发射路径；已测基础解法、四组合和能力边界。难度、手感、最终美术仍待用户试玩反馈。
- 未全面覆盖不同 DPI、多显示器、所有按住鼠标切窗方式；其他分辨率只有布局断言，未冒充原生窗口验收。
- 作者工具底层载入/保存已实际验证，材质 GUI、Undo/Redo、Play 试射引用恢复和所有冲突保护的菜单组合尚未逐项点击。
- 当前每实体一个碰撞器；未来复合碰撞器需扩展去重边界。不保证物理轨迹跨平台、帧率逐位一致。

## 版本与留存

私有仓库：[Gabbybro/foundry-siege](https://github.com/Gabbybro/foundry-siege)。开发分支 `codex/roguelike-demo`；运行源码为 `4aa2a150077fcb2ac874a88aca401683cee67c72`，交付说明另作提交。最终通过版本标记 `demo-v0.2` 并合入 `main`。包内 manifest 记录对应源码提交。源码、JSON、场景与 `.meta` 入库，缓存、安装包、日志及构建包不入库。

旧 `demo-v0.1=6c8bed6`、`Builds/Windows`、`FoundrySiege-demo-v0.1-Windows.zip` 和 `Docs/DemoDelivery.md` 保留；不改写历史。旧验收文档中的命令与范围属于当时版本，重现旧构建应使用旧标签的独立 checkout。
