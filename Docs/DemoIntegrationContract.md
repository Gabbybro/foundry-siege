# Demo 集成契约（2026-10-24）

## v0.2 当前契约（优先于下方 v0.1 历史）

公共类型以 `Assets/Scripts/Core/DemoContracts.cs`、`RoguelikeContracts.cs`、`PartSurface.cs` 为准，管理者独占。M0 已实际运行 `pwsh -File Tools/Unity.ps1 Validate`，通过旧配置读写、编译与引用检查；不代表新能力完成。当前开发分支 `codex/roguelike-demo`；v0.1 标签和旧包保留。

### 数据约定

- 保留 BattleState/PartKind 旧枚举值。PartDefinition 新增 material（MaterialId：Legacy=0、Wood=1、Glass=2、Stone=3、Metal=4）、isGround、indestructible；isStatic 只决定是否固定。Legacy 沿用 v1 静态/Platform 不可破坏与 Platform 地面语义，新显式材质不依赖名称推断。LevelData 新增 id，旧 v1 可省略，v2 必填。
- 工厂为所有构件（含固定构件）添加 PartSurface.Initialize(part)。新材质的固定可破坏体也应有 DamageableBody，无 Rigidbody2D 即静态；不可破坏体可动态。DamageableBody.Initialize 内按 PartSurface 相同兼容公式判定不可破坏，静止承重继续由接近速度过滤。
- 能力数值唯一入口 AbilitySettings，各字段说明见源码；split.spreadAngle 为中线到单侧角，shards.spreadAngle 为两破片的总夹角。质量守恒、3 子弹与 2 破片是规则，不改为数量参数。石面角度从表面切线算，0 擦撞/90 正撞。RadialEffect 衰减为 pow(clamp01(1-distance/radius),falloffPower)。
- B 提供 `RunConfiguration.Parse(string) -> RunConfigurationData`、`Validate(RunConfigurationData) -> List<string>`、`Serialize(RunConfigurationData) -> string`，沿用严格 JSON 门禁；顶层能力参数文件 `Assets/Content/RunConfiguration.json`，九关 `Assets/Content/Levels/C01.json` 至 C05/P01 至 P04。campaignLevels 顺序引用 LevelData.id；practice.build 为默认构筑，允许练习入口重选。保留旧 DemoLevel.json。
- 所有枚举用 JSON 整数；B 校验未知 ID、数值有限及范围、正式路线和练习唯一性、材质字段，保留旧 v1 读写。新 v2 不许漏掉显式材质/ground/indestructible（门禁需要区分版本）。

### A 战斗接入

- A 提供 `CombatRuntime : MonoBehaviour, ICombatRuntime`，接口、CombatContext、ShotContext、DamageSource、CombatEvent 已定义。管理者每关先 EndShot、generation++、DamageableBody.ResetSession、重建工厂，再 Configure(context)。BeginShot 在原弹设为 Dynamic 并赋初速度后调用。root 负责弹药、关卡边界、装填/超时、奖励。
- Configure 清除旧 generation 的回调/队列；订阅该 world 的 DamageableBody 破坏，支持链式冲击。EndShot 清除本发原弹/子弹/破片/待触发能力，不移除构件，不产生替换/超时爆炸。ActiveProjectiles 排除装填弹；HasUnsettledEffects 包含运动弹体、支撑未停稳、停驻倒计时和未执行效果，不能因空中顶点低速误判已结算。
- 每个原始发射用独立 ShotContext，activeUsed/shardsUsed 整发共享；A 自行实现 CombatProjectile 保存 projectileId/ProjectileKind 与独立穿木、反弹、石面、俯冲状态，并公开只读属性供真实运行检查。分裂子弹无新主动，破片不继承但伤敌能死亡冲击。测试可读取 CurrentShot 和实体只读状态，不能加运行时作弊入口。
- 原弹由 root 创建并已有 ProjectileProbe，A 给它挂能力组件；分裂/破片由 A 创建，调用 context.onProjectileCreated(go,kind) 由 root 装饰。A 不调用 Presentation；视觉通过 onEvent 回调送 CombatEvent，必须带 generation/shotId。
- DamageableBody 保留现有 API，新增 `ApplyDirectDamage(float amount, DamageSource source)` 和 `LastDamageSource`，撞击可增重载传来源。Eliminate 明确 Boundary；旧 ApplyImpact 仍表示普通碰撞。撞击去重→一次普通伤害→穿木/反弹→已发生破坏/死亡效果，链效果用有界队列，先快照遮挡/范围再伤害，最后推幸存者。
- LauncherController 负责新主动指针输入和 Escape 暂停。会话可 cast 到 IRunSession；启动主动必须是发射之后的新 MouseDown，绝不能在同一 MouseUp 误触发。保留已修好的 OnGUI 原始事件坐标方式。每次交互调用 IsPointerBlocked；纯界面弹窗或非 Battle 阶段不可发射。暂停/重试/失焦取消手势。
- A 可创建 `Assets/Scripts/Combat/Validation/CombatRegression.cs`，提供 `public static IEnumerator Run(BattleController battle)`（内部失败抛 InvalidOperationException）供管理者接入 runtime smoke；不得依赖尚未约定的 root 私有成员，不启动 Unity。

### C 界面接入

- IRunSession 扩展 IBattleSession。Phase 区分菜单/选主动/战斗/奖励/整轮完成/练习完成；BattleState 保留单关状态，IsPaused 独立。LevelIndex 为 0 起算，UI 显示 +1；LevelCount 正式 5，练习 1；Build/RewardChoices/Practices 返回只读用途的数据快照。
- 命令 StartCampaign→ActiveSelection；SelectActive→C01；SelectReward→下一关；StartPractice(levelId,active,passives) 使用用户选择的构筑。RetryLevel 恢复入场快照；RestartRun 回主动选择并清空构筑；ReturnToMenu 放弃当前轮。菜单/失败提供两种重试，放弃/整轮重开由 C 清楚提示，管理者不弹第二份确认。
- HUD 每帧 SetUiBlockingRects(Rect[])，矩形为 GUI 左上坐标；root 的 IsPointerBlocked 输入为 Unity 左下屏幕坐标并检查 Phase/暂停。中心模态对话必须挡全屏，不依赖 OnGUI 执行顺序防误射。新增反馈订阅 CombatFeedback，显示 CanUseActive、ActiveProjectiles、Feedback。TryUseActive 参数是世界坐标，由 A Launcher 输入转化，HUD 不重复发主动。
- 保留现有 DemoPresentation 静态 API，新增可选 `DecorateProjectile(GameObject,ProjectileKind)` 重载可由 root 调用；不改变物理、伤害或参数。材质视觉读取 PartDefinition.material，保持工业风。

### 所有权与阶段交付

A 仅 Combat/、Docs/CombatModule.md；B 仅 Configuration/、Editor/LevelAuthoring/、Content/、Docs/LevelConfiguration.md；C 仅 Presentation/、Art/Demo/、Docs/PresentationModule.md。root 独占其余共享文件、编辑器/构建/测试/Git。M0 为旧配置兼容调整的 ConfigurationJson 已完成，派发后归 B。文件变动先说明，不能跨边界修他人代码。

第一批先接两主动、C01/P01 和新 HUD，尽快实际构建；后续补齐七被动、九关与完整反馈。模块完成后写清已实现、静态检查和待 root 实测，不以未运行结果写通过。root 主动 wait_threads 取结果，不要求模块回发消息。

---

## v0.1 历史契约

公共命名空间 `SiegeDemo`，类型已在 `Assets/Scripts/Core/DemoContracts.cs` 实现，仅管理者改动。所有执行聊天共用当前目录，无 worktree；不启动 Unity、不修改公共文件、不提交其他人的文件。各模块先读 AGENTS.md 与 DemoExecutionPlan.md。基线提交 5374abd。

## 文件归属与 API

### 战斗：Assets/Scripts/Combat/，Docs/CombatModule.md
- `LauncherController : MonoBehaviour`：`Configure(IBattleSession session, Camera camera, Vector2 origin, LaunchSettings settings)`；`LaunchSolution CurrentAim { get; }`；`void CancelAim()`；`static LaunchSolution Evaluate(Vector2 reverseDrag, LaunchSettings settings)`（reverseDrag = origin - mouseWorld）。只有 Ready 可拖动；HUD 上方 110px、下方 100px 和右上重开区拒绝输入，拖动起点距 origin ≤1。预测使用物理相同参数。
- `DamageableBody : MonoBehaviour`：`Initialize(PartDefinition definition)`；属性 `float HitPoints`, `bool IsDestroyed`, `bool IsTarget`；事件 `Action<DamageableBody> Destroyed` 与 `Action<DamageableBody,float> Damaged`；`void ApplyImpact(float impulse, float approachSpeed)` 与 `void Eliminate()`；销毁实体碰撞与支撑；有效冲量合并接触点、双方回调去重、静态支撑免伤（建议接近速度门槛1），静止承重不掉血。全局去重和事件计数每关重置：`static void ResetSession()`。
- 投射物没有 DamageableBody，伤害回调必须覆盖投射物碰撞受伤体及双受伤体；不手动清除首次撞击后的投射物。
- 可补 `ProjectileProbe : MonoBehaviour` 暴露 `int DynamicHits` 便于实际 smoke。不得依赖 Presentation。

### 配置：Assets/Scripts/Configuration/、Assets/Editor/LevelAuthoring/、Assets/Content/、Docs/LevelConfiguration.md
- 使用公共 LevelData，不再定义一套 schema。`LevelConfiguration.Parse(string json)` 返回 LevelData（非法抛出带字段的异常）；`Validate(LevelData data)` 返回 `List<string>` 错误；`Expand(LevelData data)` 返回展开的 `List<Placement>`，模板旋转及组合 ID 正确且重复检测；`Serialize(LevelData data)` 返回 JSON。
- 样例 `Assets/Content/DemoLevel.json`（TextAsset），3 发；ground top=-3.6，launcher=(-9,-2.4)，目标区域 x=0～10，两三组接触支撑结构。尺寸显式，建议梁 HP 8～15、目标 HP 6～10，先可调；20m/s 重档应可命中与通关。不要过度增加刚体。
- `LevelObjectMarker : MonoBehaviour` 暴露 `public string objectId, partId`。场景预览工具用根管理者 API `DemoLevelFactory.Rebuild(LevelData data, Transform root, bool gameplay)`（返回 void）。根命名 `Level Preview`；保存时展开结构变成 objects 保留库和模板，无损保留配置参数；禁止 Play 模式保存/载入，避免倒塌覆盖；提供载入/保存/校验/重置试射菜单。编辑器可引用 `DemoSceneBuilder.Configure()` 生成正式场景；仅管理者调用。
- 不生成最终场景和 .meta（Unity 由管理者统一导入生成），不改 root factory。

### 表现：Assets/Scripts/Presentation/、Assets/Art/Demo/、Docs/PresentationModule.md
- `DemoPresentation : MonoBehaviour`：`Configure(IBattleSession session, Camera camera)`；基础工业场景背景、HUD、弹药、目标、状态、档位/角度/速度、重开与清晰中文操作说明。可用内置 IMGUI（注意中文字体）；HUD 不能遮挡发射点/目标。
- `static void DecoratePart(GameObject go, PartDefinition part)`；`static void DecorateProjectile(GameObject go)`；`static void CreateLauncher(Vector2 origin, Transform parent)`；`static void PlayImpact(Vector2 position,float damage,bool destroyed)`。使用程序化 Sprite/线条组合装甲、金属梁、无人机，造型需可辨识；碎屑无碰撞。可订阅 root 已转发的 DamageableBody 事件或由 root 调用反馈。
- 不修改物理参数或增加伤害碰撞体，不依赖 root 具体实现，只使用 IBattleSession。参考图路径见计划。不需生成新 AI 图片；优先精致代码原生造型。

### 管理者独占
Assets/Scripts/Core/（流程、工厂、运行 smoke）、Assets/Editor/DemoSceneBuilder.cs、Tools/Unity.ps1、Assets/Scenes、ProjectSettings、Packages、README.md、执行计划。负责统一启动 Unity、编译、生成、构建、真实运行验收、集成修复与提交。

## 交付要求
各模块文档写配置/调用例、正常/重复/重置/异常验收，明确实际检查与待集成检查。完成后在聊天最终回复列文件和注意项即可，管理者主动 wait_threads 收集。不要等待许可开始，不要未经运行宣称实机通过。契约需要扩展时优先兼容增量并写文档说明，禁止破坏现有签名。
