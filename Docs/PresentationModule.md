# Presentation 模块 v0.2

日期：2026-10-24。状态：流程 HUD、输入屏蔽、材质和能力反馈已实现，通过独立编译及布局检查，待管理者 Unity 集成与窗口验证。本模块未启动 Unity，未运行本轮 Validate、Build、Smoke。

## 文件与边界

仅修改 Assets/Scripts/Presentation/ 与本文；未修改 Core、Combat、配置、场景、项目设置、依赖、构建脚本或 Git。保留已有 .meta，由管理者统一导入新脚本。没有新增外部美术或字体资源。

| 文件（Assets/Scripts/Presentation/） | 本轮职责 |
| --- | --- |
| DemoRunHud.cs（新增，DemoHud partial） | 流程界面、练习配装、两种重试、单份放弃确认、构筑与主动状态 |
| DemoUiLayout.cs（新增） | GUI 左上坐标的输入阻挡区域，独立于 OnGUI 事件顺序 |
| DemoAbilityText.cs（新增） | 两主动/七被动中文名称与说明，不包含能力数值/实现 |
| DemoPresentation.cs | IRunSession 检测、反馈订阅/解绑、每帧屏蔽登记、暂停保护、弹体种类重载 |
| DemoVisualFactory.cs | 木/玻璃/石材、属性标记、分裂子弹与有伤害玻璃破片造型 |
| DemoImpactEffects.cs | 十类 CombatEventKind 反馈，复用无物理对象池 |
| DemoHud.cs | partial 支持，保留旧 IBattleSession HUD，新流程无含混重开文案 |
| DemoDroneVisual.cs | 推进器动画使用游戏时间，暂停时冻结 |

继续复用 DemoPrimitives、DemoEnvironment、DemoLauncherVisual；表现目录共 11 个 C# 文件。既有 DemoPresentation 静态 API 全部保留。仅 IBattleSession 的调用者仍使用旧 HUD，实现 IRunSession 后自动启用新流程；未新建相似公共接口。

## 接入与配置

```csharp
// session 实现公共 IRunSession（继承 IBattleSession）。
controller.gameObject.AddComponent<DemoPresentation>().Configure(session, battleCamera);
DemoPresentation.DecoratePart(partObject, partDefinition);
DemoPresentation.DecorateProjectile(original); // 旧签名仍可用
DemoPresentation.CreateLauncher(level.launcher, launcherRoot);

// root 在 CombatContext.onProjectileCreated 转发种类；Presentation 不依赖 Combat 实现。
context.onProjectileCreated = (go, kind) => DemoPresentation.DecorateProjectile(go, kind);

// root 经 IRunSession.CombatFeedback 转发本 generation 的事件，视图自动订阅。
// 原伤害/破坏回调继续转发：
DemoPresentation.PlayImpact(position, damage, false);
DemoPresentation.PlayImpact(position, 0, true);

// 可选：自定义重置在同一帧跳过 Preparing 时同步清理旧视觉。
DemoPresentation.ClearFeedback();
```

重复 Configure 先解绑旧会话再绑定新会话，启用时避免重复订阅，禁用/销毁解绑。背景和反馈根复用；Configure、Preparing、界面发起的换关/重试/整轮重开清理旧反馈。root 负责 generation 筛选和每实体事件去重；IRunSession 没有暴露当前 generation，表现层不猜测旧事件是否有效。

## 界面与准确命令

| 操作 | 命令/行为 |
| --- | --- |
| 主菜单开始正式作战 | StartCampaign()，转主动二选一 |
| 空中分裂/空中冲刺 | SelectActive(active)，不写 Build 快照 |
| C01—C04 奖励 | 读取 RewardChoices，SelectReward(passive)，无本地抽奖、刷新或跳过 |
| C05 完成 | Phase.RunComplete 展示整轮完成，不额外奖励 |
| 四练习入口 | 名称、ID、说明、默认组合读取 Practices 前四项，进入独立配装 |
| 练习配装 | 主动二选一，七被动任意开关，可恢复本关默认组合 |
| 进入练习 | StartPractice(levelId,active,passives[])，传排序后的新数组 |
| 练习完成 | Phase.PracticeComplete，可重试、重开整轮或返回菜单 |
| 战斗暂停/点击构筑摘要 | SetPaused(true)，展示完整构筑与说明 |
| 继续战斗 | SetPaused(false)，不重建现场 |
| 暂停/失败：重试本关 | RetryLevel()，直接恢复入场快照 |
| 暂停/失败：重开整轮 | 单份放弃确认后 RestartRun() |
| 返回主菜单 | 单份放弃现场确认后 ReturnToMenu() |

新流程不调用含混的 IBattleSession.Restart()；仅旧 HUD 兼容分支保留。重开整轮明确说明清空构筑与五关进度，练习中明确说明将结束练习并进入正式开局。取消确认保留原暂停状态；从运行战斗打开确认时，取消后恢复现场。root 不应弹第二份确认。

练习配置仅修改视图临时集合，不修改 PracticeDefinition/Build 数组。LevelIndex 显示 +1，LevelCount 来自会话；RunPhase 与 BattleState 分开处理，暂停读取 IsPaused，失败读取 BattleState.Lost。非三项奖励显示提示并禁用选择，缺失练习项禁用，不伪造关卡。奖励/快照/随机状态/构筑隔离真正实现均归 root，HUD 只调用契约命令。

## 输入与暂停

战斗显示目标数、弹药、三档、角度/速度、结算进度、关卡阶段、构筑摘要、材质图例、活动弹体数与主动状态。CanUseActive 决定可用状态；成功 Split/Dash 事件显示 0/1，装填/Preparing/新发重置显示。未收到成功事件而不能发动时显示“本发尚未使用”，不把支撑条件误报成已消耗。无效方向等 Feedback 原样展示。

HUD 不调用 TryUseActive，也不轮询鼠标重复发动能力；世界点击与 Esc 暂停由 A LauncherController 处理。

DemoPresentation 使用 DefaultExecutionOrder(-1000)，Configure 和每个 Update 调用 SetUiBlockingRects，OnGUI 命令改变阶段后立即再登记。传递未缩放真实屏幕的 GUI 左上坐标；root 转换 Unity 左下指针坐标。普通战斗登记：

- 顶部 `(0,0,width,110)`，底部 `(0,height-100,width,100)`。
- BuildStrip(width)：左侧构筑摘要，y=111～145。
- ActivePanel(width)：右侧主动状态，y=111～183。
- 材质图例 `(16,151,280,25)`。
- Feedback 非空时增加中上部提示框，y=190～232；无提示时无此阻挡区域。

主菜单、配装、主动选择、奖励、暂停、失败、完成、放弃确认全部登记整个屏幕。中心界面按 1600×900 虚拟坐标等比缩放；屏蔽区域始终是真实全屏，不依赖 GUI.Button 吞事件。模态下底层战斗按钮禁用。若 Esc 在放弃确认中切换了暂停，LateUpdate 在下一物理步前保持暂停，直至确认/取消，防止确认背后继续战斗。

root 负责物理/能力计时暂停；视图效果使用游戏时间，IsPaused 时停止推进。暂停不清空反馈，继续后可延续。Phase/State/IsPaused 的输入门禁由 root/A 保留。

目标窗口：1600×900、1280×720、960×540、1920×1080。战斗上下栏使用原始像素，1100 px 以下紧凑字号，模态等比缩放。中文使用 OS 动态字体，优先 Microsoft YaHei / YaHei UI / SimHei / Noto Sans CJK SC，未复制系统字体；无这些字体的系统需另行检查中文。相机适配归管理者，本模块不改相机。

## 材质、弹体、反馈

PartDefinition.material 唯一决定构件造型，保持 size：Wood 暖褐木纹、木结、钢箍；Glass 半透明青板、反光斜线、端夹；Stone 灰色厚面、块缝、裂纹；Legacy/Metal 保留铆钉槽钢。地面绘绿色细线，不可破坏绘锁标志；固定与不可破坏独立，Legacy 按公共契约兼容。

原弹保持装甲无人机，Split 读取现有 CircleCollider2D.radius 缩放并加青条纹，Shard 为亮青/白三角实体破片。只读现有尺寸，不创建/修改碰撞体、质量、速度。能力物理破片由 Combat 创建；普通表现碎屑没有物理与伤害。

| CombatEventKind | 反馈 |
| --- | --- |
| Split / Dash | 三向青色分流 / 橙色冲刺尾迹 |
| WoodPiercing | 木色穿透短线与暖色碎屑 |
| GroundBounce / StoneRicochet | 青色上弹折线 / 浅灰擦撞短线 |
| ApexDive | 橙色向下速度线 |
| RestCountdown / RestExplosion | 橙色计时环 / 橙色扩张环与火花 |
| DeathShock | 紫色冲击环与连锁提示 |
| GlassShards | 青色双向短线与亮色碎光 |

各事件均有中文浮字。环属于反馈，不标示真实伤害半径/遮挡；AbilitySettings/Combat 负责真实数值。没有新增音效。

池上限为 128 个火花/环/短线、16 条浮字、每帧 12 个有效 burst。普通/能力表现碎屑均无 Rigidbody2D/Collider2D；同帧同位置（0.05 m 量化）同类去重。停驻提示按 generation/shot/projectile 节流，最多 64 个计时键。独立 System.Random 不消耗游戏随机序列。伤害后紧接摧毁时合并浮字；跨帧伤害去重仍归 Combat。

## 已执行检查

1. 使用固定 Unity 6000.3.27f1 安装中的 DotNetSdkRoslyn/csc.dll，引用同目录 NetStandard 2.1 和 UnityEngine，编译 DemoContracts.cs + RoguelikeContracts.cs + 表现目录 11 个脚本，启用 `-warnaserror+`：最终退出码 0，零错误、零警告。没有运行 Unity。
2. 独立 .NET 执行真实 UnityEngine.CoreModule 的 Rect/Vector2 布局检查：四分辨率各 19 项，共 76 断言通过，输出 `PRESENTATION_UI_LAYOUT_PASS checks=76`。覆盖上/下栏、构筑、主动、图例、条件反馈框、默认发射点、世界目标区域、全屏模态角落、矩形边界。没有执行实际鼠标事件，不代表窗口交互通过。
3. 静态确认新流程两个重试命令分开，未调用 TryUseActive，未新增物理组件或写刚体状态；订阅成对解绑。git diff --check 未发现已有文件空白错误。

临时编译/检查产物位于忽略目录 `.setup/PresentationCheck/`，不提交缓存。不将 v0.1 检查结果计为本轮结果。

## 待集成验收

| 类别 | 操作与预期 |
| --- | --- |
| 正常流程 | 正式入口→两主动分别入 C01；前四关胜利奖励三选一并推进；C05 整轮完成无多余奖励 |
| 正常练习 | P01—P04 名称/说明/默认组合正确；换主动、增减被动、恢复默认、进入练习；完成不推进正式/不发升级 |
| 正常战斗 | Aim 数值/三档、构筑、弹体数、主动可用/已用状态同步；无效方向提示且不扣次数 |
| 正常重试 | 飞行/倒计时中暂停后，重试恢复本关/3发/入关构筑；重开整轮确认后清空重选；失败页有相同两命令 |
| 正常暂停 | 物理/能力/视觉冻结，继续保留现场；确认期间 Esc 不继续后台战斗，取消不丢进度 |
| 正常表现 | 木/玻璃/石、地面/锁标记、分裂子弹/有伤害破片可辨；十类事件各实际触发一次 |
| 重复输入 | 连点 UI 不误发射/扣弹/发主动；同帧命令不重复；奖励阶段改变后旧按钮失效 |
| 重复绑定 | Configure 两次、禁用再启用只收到一次反馈；更换/销毁后旧会话解绑 |
| 重复反馈 | 密集连锁不超过池容量，停驻提示不逐帧堆积，表现碎屑无伤害 |
| 重置 | 重试/重开/返回清理旧反馈与弹窗/练习临时状态；旧 generation 不进入新关 |
| 异常 | 空/不足四练习、找不到 ID、非三项奖励安全提示/禁用，不改坏会话快照 |
| 异常反馈 | NaN/Infinity 位置、非法伤害、缺相机、缺 Rigidbody 装饰安全退化，不改物理 |
| 窗口 | 四种目标分辨率/调整窗口后全屏模态不误射、HUD 不触发主动，发射点开放 |
| 最终包 | 实际 Windows 窗口发射/主动/奖励/练习/暂停/两种重试/返回菜单；检查字体、布局、Shader、日志，跑本轮 Validate/Build/Smoke |

本模块未运行 Unity 或最终包，上表未报告通过。沿用程序化工业方向，背景地面立面仍按 -3.6 m，改变关卡地面高度需同步视觉。