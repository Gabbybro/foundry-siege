# Demo 集成契约（2026-10-24）

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
