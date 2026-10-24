# Combat 模块 v0.2

日期：2026-10-24（Asia/Shanghai）。当前实现以 `DemoIntegrationContract.md` 的 v0.2 契约、`RoguelikeContracts.cs` 和白皮书快照 12.2/12.6/12.7 为准。下方保留 v0.1 历史记录，冲突处以本节为准。

## v0.2 文件和接入

新增 `CombatRuntime.cs`、`CombatRuntime.Effects.cs`、`CombatProjectile.cs`、`CombatRules.cs`、`PiercingGeometry.cs`、`Validation/CombatRegression.cs`；增量修改 `DamageableBody.cs`、`ImpactCollision.cs`、`LauncherController.cs`。现有 `.meta` 保留，新文件由管理者统一导入。未修改 Core、场景、构建、Packages 或 Git 状态。

```csharp
DamageableBody.ResetSession(); // root 同时清旧世界并从入关快照重建。
// 工厂对每个构件 Initialize PartSurface / DamageableBody，包括固定构件。
combat.Configure(new CombatContext {
    world = world, generation = generation, level = level,
    build = build, settings = runConfiguration.abilities,
    onEvent = message => HandleFeedback(message),
    onProjectileCreated = (go, kind) => DecorateFromRoot(go, kind)
});

// root 已将原弹切 Dynamic，赋予一次初速度，而且原弹已有 ProjectileProbe。
combat.BeginShot(originalBody, shotId);
bool accepted = combat.TryUseActive(clickedWorldPoint, out string reason);
bool stillResolving = combat.HasUnsettledEffects;

// 装填、超时、胜利收尾、重试、换关或退出时清理本发。
combat.EndShot(); // 不制造爆炸；不删场景构件。
```

`Configure` 要求经过配置模块校验的完整 AbilitySettings、BuildLoadout 和 LevelData；内部复制构筑，并保留参数对象供当前关使用。先清旧本发，再解除旧世界订阅，绑定新世界。`BeginShot` 为每次原发新建 ShotContext；projectileId 在当前关单调递增，generation/shotId 随所有事件与弹体保留。调用方不应在一次能力执行的回调中再次改变物理参数；`onProjectileCreated` 用于装饰，不创建另一份能力逻辑。

## 两主动与输入

- 分裂：只允许仍在空中的原弹，每原发一次。读取发动瞬间 **实际** `Body.linearVelocity` 的方向，近零速才回退最近有效方向。母体禁用并移除，在同一中心创建三个较小子体，分别为实际质量的 1/3、固定配置速度、方向为中线及 ±spreadAngle；子体两两忽略碰撞。出生位置不向前平移，避免凭生成越过障碍。替换不触发停驻／死亡／爆破；子体没有新主动。
- 冲刺：有效点击方向必须落在画面固定的中心角／范围宽度内；点击中心、非法坐标或越界方向不扣主动。速度为发动瞬间实际速率加 speedIncrement，沿点击方向只赋一次速度，随后正常重力、阻尼、碰撞。初始 360° 可由配置限制。
- 保留 v0.1 已验证的 OnGUI 原始 MouseDown/Drag/Up 坐标流程，避免快拖起点被轮询终点覆盖。发射只由 MouseUp，主动只由后续新 MouseDown，且不接受发射同一帧的主动。实际收到发射 MouseUp 后无需额外空闲帧，可在下一帧接受新按下。
- 会话为 IRunSession 时，每次指针交互调用 `IsPointerBlocked`，并检查 Battle phase、IsPaused、相机范围、IMGUI 捕获；旧单关仍保留固定 HUD 区域保护。Escape 在 Battle 切换暂停，即使 Time.timeScale 已为 0 也可继续；暂停、右键、失焦、重试与禁用组件取消手势。

## 七被动和单发额度

| 被动 | 已实现的规则 |
| --- | --- |
| 穿木 | 每原弹／每分裂子弹各一次，只穿一块 BoxCollider2D 木构件。先正常碰撞伤害，再沿入射方向按比例保速越过；门槛不足不耗次数。厚度沿入射射线在旋转后的真实盒体内计算；出口考虑弹体半径，CircleCast 检查出口路径，发现后方／重叠其他实体则拒绝穿越，不跳过第二堵墙。不额外扣穿透伤害。 |
| 触地反弹 | 每体一次。明确 IsGround 且从上方接触、撞前向下时发动；竖直按向下速率乘系数转向上，受最低起跳速度约束；水平按比例保留。同接触为地面时不再尝试石面跳弹。 |
| 石面跳弹 | 每体一次，按相对表面切线的夹角判定。成功擦撞才耗次数，向上且保持原水平前进侧；正撞走普通物理且不耗机会。 |
| 顶点俯冲 | 每体一次，水平乘保留比例，竖直设为负 downSpeed。自然上升到下降的跨越才触发；任何碰撞／冲刺改向都会重置顶点观察基线，强制向下不会被自然顶点逻辑覆盖。反弹上升可重新等待自然顶点，但不刷新已用机会。每个分裂子体机会重新获得；出生下降的子体立即俯冲，可能覆盖其固定分裂速度。 |
| 停驻爆破 | 每原弹／子体一次。有接触支撑并持续低速及低角速 stableSeconds，才开始 countdownSeconds；空中顶点、穿木期不算。重新运动清空稳定和倒计时；到时把效果入队并移除弹体。没有此被动的弹体在有支撑且稳定后清理。 |
| 玻璃破片 | 只有原弹／分裂子弹直接碰撞造成玻璃致命伤才触发，整原发共享一次、固定两枚。沿撞前方向 ±shards.spreadAngle/2，固定速度／质量／半径／寿命；不继承主动或任何弹体被动，也不继续制造破片。倒塌、范围直接伤害压碎玻璃均不产生破片。 |
| 死亡冲击 | 当前原发进行期间，原弹、子体、破片、倒塌物或直接伤害杀敌均可触发；每敌一次。采用队列继续连锁，无同步递归。Eliminate 明确为 Boundary，不触发。 |

分裂只刷新子体的穿木／触地／石面／俯冲等每体状态；activeUsed、shardsUsed 由同一个 ShotContext 共享。破片的碰撞伤害仍使用正常冲量入口，破片杀敌仍可触发世界层的 DeathShock。

撞前方向／速度在 FixedUpdate 缓存时加入本步自由飞行重力和阻尼，供原生碰撞回调使用；分裂、冲刺主动始终读取发动瞬间实际速度，不用这份碰撞缓存。当前工厂重力倍率为 1、线性阻尼为 0。未来若使用自定义物理求解、力控制器或额外速度限制，应同步核查撞前缓存。

## 伤害顺序、遮挡和生命周期

`DamageableBody` 新增 `ApplyDirectDamage(float, DamageSource)`、带来源的 `ApplyImpact(float,float,DamageSource)` 重载、`LastDamageSource` 和只读 `IsIndestructible`。原 API 保留；旧 ApplyImpact 仍为普通碰撞。不可破坏判定与 PartSurface 相同：显式 indestructible，或 Legacy 材质且旧 isStatic/Platform。新材质的固定件可以被破坏，不可破坏的动态件仍正常倒塌与受推。

原生碰撞顺序为：固定步碰撞器对去重 → 缓存仍存活的材质／几何／入射信息 → 接触点法向冲量合计并各扣一次普通伤害 → 穿木／反弹改向 → LateUpdate 执行已排队的破坏／死亡效果。没有第二份能力碰撞伤害。静止承重门槛、死亡去重、事件重入防护及会话版本隔离保留。

停驻爆破／死亡冲击分别读各自 RadialEffectSettings。每次效果先对当前世界范围内的实体构件和敌人确定最近实体表面距离、衰减和遮挡快照，再统一结算直接伤害，最后推仍存活的动态刚体。衰减为 `pow(clamp01(1-distance/radius), falloffPower)`。实体墙、静态或动态、可破坏或不可破坏均遮挡；触发时挡路的墙即使被这次效果杀死，也不能让该次效果穿到墙后。后续独立效果会重新快照。目标按实体分组，本次效果每体只直接扣一次；后续真实碰撞仍可正常受伤。

队列最多 512 个待执行效果，每 LateUpdate 最多 32 个；达到队列上限会明确记录 COMBAT_EFFECT_LIMIT 错误，不递归或无限扩散。九关正常内容远低于该限制；如将来扩大敌人数，应连同内容门禁重新审查。每敌死亡一次、每体爆炸一次、整发破片一次是主要的自然终止边界。

`HasUnsettledEffects` 包括未执行队列、停驻倒计时、所有尚未回收的运动／空中弹体和未稳定的场景动态构件；空中最高点仍算未结算。破片到寿命回收，其他无爆破弹体在有支撑稳定后回收；出界由 root 清理。`EndShot` 可重复调用：标记 ended，清队列与本发实体，不改构件，不产生超时／替换爆炸。Configure、换关与重试会解绑旧回调。暂停采用 scaled time，物理与能力计时一起停止。

## 可读取的测试入口

`CombatRuntime` 的公开入口完全实现 ICombatRuntime：Configure、BeginShot、TryUseActive、ActiveAvailable、ActiveProjectiles、PendingEffects、HasUnsettledEffects、CurrentShot、EndShot。

`CombatProjectile` 提供只读 Body、Collider、Shot、ProjectileId、Kind、IsRetired、WoodPiercingUsed、GroundBounceUsed、StoneRicochetUsed、DiveUsed、IsCountingDown、IsPiercing、RestStableTime、RestCountdownRemaining、Radius、EffectiveDirection、LastIncidentVelocity、IsAirborne。没有修改额度的调试接口。`CombatRules` 提供 Rotate、InDirectionArc、RadialFalloff 的确定性计算，便于纯托管检查。

```csharp
// 仅由管理者的显式 development-player smoke 启动。
yield return CombatRegression.Run(battle);
// 失败抛 InvalidOperationException；成功输出 COMBAT_REGRESSION_SUCCESS。
```

回归使用真实 Unity 刚体／碰撞器与公开 Combat API，在 x≈75～90 的独立 Fixture world 中运行，不改当前关地图、目标计数或构筑。临时用 1 倍时间，结束／失败均恢复原 Time.timeScale；每个夹具清理。直接伤害与二次来源额度测试明确通过公开伤害 API 构造，不宣称它们也是自然通关路径。

## v0.2 验收和当前证据

| 类别 | 检查 | 状态／证据 |
| --- | --- | --- |
| 正常 | 两主动实际速率、质量守恒、当前方向、三枚互不碰撞、子体额度刷新 | CombatRegression 已实现；root 首次 smoke 发现缓存方向滞后一固定步，已改为瞬时实际速度；修正后待统一复跑 |
| 正常 | 木材真实穿过／第二块阻挡、厚木不耗；地面优先；擦石与正撞；自然顶点与下降分裂 | 真实 player 夹具已实现，不能以独立编译当作物理通过 |
| 正常 | 左向玻璃入射产生固定两片、无继承；停驻爆破直接扣血并回收 | 真实 player 夹具已实现，待 root 运行 |
| 重复 | 连续主动、共享破片额度、多回调碰撞、同敌死亡与连锁 | 回归含一次主动、第二块玻璃、Boundary 与有界队列检查；原 v0.1 去重保留 |
| 重置 | 运动清倒计时、重复 EndShot 无爆炸；Configure 清旧 generation、重试清旧本发 | 回归含倒计时运动重置与重复 EndShot；root smoke 负责完整入关快照／RNG／跨关 |
| 异常 | 冲刺中心／超弧／NaN 不扣次数；UI／暂停／失焦不误射；发射 MouseUp 不触发主动 | 方向与非法值纯函数已覆盖；真实键鼠和焦点由 root UI 运行检查 |
| 遮挡 | 一次冲击击碎墙仍不伤／推墙后目标；下次效果重新可见 | 真实快照夹具已实现；待 root 运行 |
| 终止 | 空中低速不结算；破片寿命、稳定清理；超时取消剩余效果 | 空中与 EndShot 夹具已实现；root 控制最终超时与输赢 |

本聊天实际执行：

1. 当前全部 `Assets/Scripts`（包含 CombatRegression 与真实 BattleController）用 Unity **6000.3.27f1** 安装目录的 netstandard/Unity 模块、C# 9、warnings-as-errors 独立编译：通过。回归文件最初的静态常量实例访问错误已修正并重新纳入全量编译。
2. 原始 Combat 源码加真实共享契约、真实 Unity 托管程序集在 .NET 9 运行 **150 条断言通过**：三档与角度、非法输入、伤害公式、无序 key、会话重置、扇形方向、分裂定速、角度跨 180°、径向衰减；没有模拟原生物理。
3. 本聊天未启动 Unity 或 Windows player。root 已实际开始构建／smoke，首次分裂方向失败已定位修正；当前最新完整回归结果应由 root 的 Logs/player-smoke.log 及交付记录确认，不能把先前版本运行记录写成当前机制全部通过。

临时托管检查工程仍在 `C:\Users\Gabby\AppData\Local\Temp\SiegeDemoCombatCheck\MathChecks.csproj`；执行 `dotnet run --project '<该绝对路径>' --configuration Release --no-launch-profile --no-restore`。该临时工程不进入 Git。正式可复跑的物理回归源码已在 Combat/Validation 交付。

限制：当前可配置构件均为盒体，穿木不支持任意多边形；复杂多碰撞器实体仍按碰撞器对处理普通碰撞，范围效果则按实体归并。遮挡采用到最近表面点的一条实体射线，不实现绕射或部分面积暴露；这属于明确的首版近似。真实手感、九关自然通关和全部高阶组合仍由管理者统一试玩／运行验证。

---

# v0.1 历史交付说明

日期：2026-10-24（Asia/Shanghai）。状态：代码与独立托管检查完成，Unity 集成与 Windows 实机检查由管理聊天统一执行。本模块没有启动 Unity、生成场景、改公共接口或执行 Git 写操作。

集成更新：管理者已完成实际 Unity 编译、Windows 物理 Smoke 和窗口鼠标试射，详见 `Docs/DemoDelivery.md`。快速拖拽现在通过 OnGUI 原始鼠标事件的位置发起和结束手势，Update 只负责连续预览与取消检查，避免轮询下压帧丢失起点；`-inputTrace` 可记录按键、焦点和事件坐标，仅用于诊断。下文“待 Unity 检查”保留模块交付时状态，最终结果以总交付记录为准。

## 文件与依赖

- `Assets/Scripts/Combat/LauncherController.cs`：反向拖拽、三档解算、输入保护、短轨迹。
- `Assets/Scripts/Combat/DamageableBody.cs`：部件／目标生命、伤害事件、破坏、每关状态重置。
- `Assets/Scripts/Combat/ImpactCollision.cs`：内部碰撞接触点合计、双方回调去重、伤害公式。
- `Assets/Scripts/Combat/ProjectileProbe.cs`：真实动态碰撞计数，并转发到统一伤害入口。

只依赖 `SiegeDemo` 公共契约和 Unity 自带 Core、Physics2D、InputLegacy、IMGUI 模块，不依赖 Presentation。没有预制件、场景或额外包；`.meta` 留给管理者统一导入生成。

## 发射配置与调用

```csharp
// session 实现 IBattleSession，负责投射物与弹药的唯一结算入口。
var input = controllerObject.AddComponent<LauncherController>();
input.Configure(session, battleCamera, level.launcher, level.launch);

// 无输入的确定性解算：reverseDrag = origin - mouseWorld。
LaunchSolution heavy = LauncherController.Evaluate(new Vector2(2.3f, .6f), level.launch);
LaunchSolution liveAim = input.CurrentAim;
input.CancelAim();
```

默认 `maxDrag=2.3`、`maxSpeed=20`、`cancelRatio=.15`、`mediumRatio=.4`、`heavyRatio=.7`、角度 `5°～75°`。比率使用反向拖拽向量的长度，最大钳制到 1：

| 拉距比率 p | 结果 | 默认速度 |
| --- | --- | --- |
| p < 0.15 | 取消 | 0 |
| 0.15 ≤ p < 0.40 | 轻档，tier=1 | 20/3 m/s |
| 0.40 ≤ p < 0.70 | 中档，tier=2 | 40/3 m/s |
| p ≥ 0.70 | 重档，tier=3 | 20 m/s |

15% 恰好进入轻档。反向向量必须有正 x 分量；零向量、向前拖拽和纯竖直拖拽无效。角度连续计算再钳制到配置范围，档内拉距变化不会改变速度。`Evaluate` 对 null、NaN、Infinity、错误比率顺序及非法速度等返回无效解；`Configure` 对非法依赖／参数抛出具体参数异常。`Configure` 保存参数快照，改关卡参数后应重新调用。

仅 `Ready` 且有弹药时可起手，起点距离发射原点必须 ≤1 世界单位。左键松开时只调用一次 `TryLaunch`，先清空拖拽状态再交给 session；本模块不直接扣弹药、生成投射物或反复赋速度。session 仍应检查状态并按 tier/angle 重建权威速度。

以下情况取消当前手势、隐藏轨迹并等待左键完全抬起：右键、Escape、失焦、暂停、`Time.timeScale<=0`、禁用组件、切离 Ready、移出窗口或摄像机区域、进入保护 UI。保护区域按屏幕实际像素计：顶部 110px、底部 100px、右上 220×160px；另外拒绝 `GUIUtility.hotControl != 0` 的 IMGUI 捕获输入。屏幕高度不够时保护区可能占满画面，应由场景／窗口尺寸满足基本可玩高度。恢复焦点时不会把已经按下的左键当成新拖拽。

轨迹采用 `CurrentAim.velocity`、`Physics2D.gravity` 与 `Time.fixedDeltaTime`，按重力后积分位置，匹配当前工厂投射物 `gravityScale=1`、`linearDamping=0`。预览最长 0.65 秒、5.5 米、64 点；不同档位有不同颜色。它展示首次碰撞前的短自由飞行段，不模拟后续反弹，也不做独立的伤害或物理预测。若管理者以后改变投射物重力／阻尼，需要同步预览参数。

## 伤害与破坏调用

```csharp
// 每次重建前调用一次；然后关闭／删除旧世界，创建并 Initialize 新实体。
DamageableBody.ResetSession();
var body = partObject.AddComponent<DamageableBody>();
body.Initialize(new PartDefinition {
    id = "armor", kind = PartKind.Target,
    mass = 2, hitPoints = 8, impulseThreshold = 2, damageMultiplier = 1
});
body.Damaged += (hit, removedHp) => Debug.Log("HP removed: " + removedHp);
body.Destroyed += hit => Debug.Log(hit.IsTarget ? "target eliminated" : "support removed");

// 自动 OnCollisionEnter2D / OnCollisionStay2D 路径无需手动调用。
// 以下只用于外部已合并的撞击或定向验证；不要再手动转发同一次原生碰撞。
body.ApplyImpact(6, 4); // (6-2)*1 = 4 HP
body.Eliminate();      // 越界消灭；没有 Damaged 事件，Destroyed 只触发一次。

// 无人机不挂 DamageableBody；首次碰撞后继续由 Rigidbody2D 正常运动。
var probe = droneObject.AddComponent<ProjectileProbe>();
int actualDynamicContacts = probe.DynamicHits;
```

`Initialize` 只保存生命／材质参数，不创建碰撞器，不修改质量、摩擦、重力或刚体类型；这些仍由工厂根据同一 `PartDefinition` 设置。HP 必须有限且 >0，冲量阈值和伤害倍率必须有限且 ≥0，类型必须合法。固定部件与 Platform 不接受撞击伤害。对已经破坏的实例再 Initialize 会抛错；重开应从配置重建实体，不能只重填 HP 而保留倒塌姿态。

自动碰撞计算流程：

1. 只处理启用的实体接触，忽略触发器、同一刚体内部接触以及没有受伤体的碰撞。
2. 合计本次回调所有有效接触点的正 `normalImpulse`，形成 J。使用接触点的法向相对速率（包含旋转分量）作门槛，最大值 <1 m/s 时不造成伤害；切向擦过、静态支撑与轻触不靠总速度误判。
3. 用当前固定步时间与无序碰撞器 ID 对去重。受伤体双方回调、无人机 Probe 与受伤体回调共用同一入口，不会各扣一遍血。每个独立部件按自己的材质计算 `max(0, J-J0) × damageMultiplier`；能力倍率首版恒为 1。
4. 两个受伤体都会收到一次合计冲量；只有一方有生命也可计算，因此无需给无人机增加自身耐久。Stay 路径允许已有接触后来发生的新碰撞，但仍受速度与冲量门槛约束。

首版工厂每个部件只有一个实体 BoxCollider2D。若未来给一个部件增加多个实体碰撞器，仍会对每个碰撞器对分别去重与结算，未实现跨多个碰撞器对的整件冲量批处理。

法向速率用相对速度沿法线投影的绝对值，并要求同时存在正法向求解器冲量，兼容双方回调方向。API 含义依据 Unity 官方的 [ContactPoint2D](https://docs.unity.cn/Documentation/ScriptReference/ContactPoint2D.html) 与 [relativeVelocity](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/contactpoint2d/relativevelocity)；本地编译使用项目锁定版本的程序集与 XML，而非升级依赖。

`Damaged` 的 float 为实际移除的 HP，最大不超过剩余生命；无效数字、负速度、低冲量、轻触与重复死亡不会生成事件。直接 `ApplyImpact` 的调用方负责合并／去重，它不会把两个明确的外部调用自动视为同一碰撞。

致命伤害先置 `IsDestroyed=true`、HP=0 并禁用全部实体碰撞器与刚体模拟，再依次发送 Damaged、Destroyed；随后立刻隐藏实体并延迟 Destroy。掉出场地调用 `Eliminate` 只发送 Destroyed。事件重入或反复 Eliminate 都不会重复结算；某个订阅者抛异常会记录日志并继续通知其他订阅者。视觉反馈订阅者应创建独立对象，不要依赖受伤体在回调后继续存活。模块不生成带碰撞的碎片。

`ResetSession()` 清空固定步去重表与 `ImpactEvents`、`DamageEvents`、`DestructionEvents` 诊断计数，并推进会话版本。旧实体不能继续影响新会话；重置发生在事件回调中时，后续旧订阅通知停止。仍然存活的 ProjectileProbe 在读取／下一次碰撞时也自动清零计数。重开调用者必须同时清理旧实体并重建，因为此方法不会恢复物理场景。

`ProjectileProbe.DynamicHits` 统计与动态刚体的 Enter 接触，静态地面不计；持续 Stay 不重复增加，同一固定步同一碰撞器对也不重复增加。分离后再次真正碰撞可以再次计数。它从不销毁、停住或调整无人机，也不额外制造伤害。

## 验收矩阵

| 类别 | 步骤 | 预期 | 本聊天实际状态 |
| --- | --- | --- | --- |
| 正常：三档 | 检查 p=.15/.151/.25/.399/.4/.55/.699/.7/.85/1/4 | 边界正确，固定为 1/3、2/3、1 倍速度，超拉钳制 | 托管断言通过 |
| 正常：角度 | 检查 -60/0/5/17.31/44.25/75/89.99° 反向向量 | 角度连续且限制到 5～75°，预览速度模长等于发射速度 | 托管断言通过 |
| 正常：公式 | J=6、J0=2、K=1.5，以及过量伤害 | 伤害 6；过量伤害只移除剩余 HP | 托管断言通过 |
| 正常：碰撞 | 无人机高速撞梁、梁撞目标、倒塌梁落向目标 | 双受伤体均掉血，无人机无自身耐久，倒塌产生有效新伤害 | 待 Unity 真实物理检查 |
| 正常：破坏 | 支撑梁受致命撞击／目标越界 | 支撑立即移除；目标计数只减少一次；独立视觉反馈可播放 | 待 Unity 检查 |
| 重复：双方回调 | 两个带 DamageableBody 的刚体碰撞；ProjectileProbe 与伤害体同时回调 | 一个碰撞器对每固定步只处理一次，每方一个 Damaged | 无序 key 断言通过；真实事件数待 Unity 检查 |
| 重复：静止接触 | 开场静置并等待；高速碰撞后观察后续 Stay | 承重／轻触不扣血；持续接触不累积重复撞击伤害 | 速度／阈值纯函数断言通过；物理行为待 Unity 检查 |
| 重复：死亡 | 连续 Eliminate、致命 Damaged 订阅中再次 Eliminate | Destroyed 一次，HP 不负数，所有实体支撑已禁用 | 代码路径审查；待 Unity 检查 |
| 重置 | 发射后连续重开、胜／负后重开 | 清输入、清计数与去重；从配置重建 HP 和位置；旧事件不影响新关 | 静态计数与版本断言通过；场景行为待 Unity 检查 |
| 异常：输入 | 取消区、向前／纯竖直、NaN／Infinity、null／非法参数 | Evaluate 无效，不产生可发射速度；非法 Configure 抛参数异常 | Evaluate 断言通过；组件调用待 Unity 检查 |
| 异常：交互 | 右键／Esc、UI 起手和拖入 UI、Alt-Tab、移出窗口、暂停、松开后重新按下 | 不发射不扣弹，必须新的有效拖拽才能发射 | 代码路径审查；待真实鼠标／焦点检查 |
| 异常：损伤 | NaN／Infinity 冲量或速度、门槛以下、有限值乘法溢出 | 不污染 HP；合法大数伤害钳制到剩余 HP | 托管断言通过 |

## 实际检查记录与集成注意

1. 使用 .NET SDK 的 Roslyn、`-nostdlib+ -langversion:9.0`，引用 **Unity 6000.3.27f1** 安装目录的 `NetStandard/ref/2.1.0/netstandard.dll` 与四个 Unity 模块，编译当前 Combat 源码及真实 `DemoContracts.cs`：通过，无编译错误。此操作没有启动 Unity。
2. 使用原始源码与相同安装目录的真实 Unity 托管程序集在 .NET 9 独立运行 99 条断言：`COMBAT_MANAGED_CHECKS_SUCCESS: 99 assertions`。没有替换 Unity 的数学类型；没有创建或模拟原生物理对象。覆盖边界、角度、输入非法值、伤害数学、无序去重 key、会话计数重置。
3. 临时检查工程在 `C:\Users\Gabby\AppData\Local\Temp\SiegeDemoCombatCheck\MathChecks.csproj`，可复跑：`dotnet run --project '<该绝对路径>' --configuration Release --no-launch-profile`。临时工程不纳入项目资源和 Git。
4. 尚未运行 Unity 导入／编译、Setup、Validate、Build、Smoke、实际鼠标与焦点测试。独立托管检查不能证明碰撞冲量回调、可玩手感、穿透防护或实机通关已通过。

管理者集成时保留当前工厂的投射物连续碰撞检测、显式质量和默认重力／零阻尼；每关先 ResetSession 再重建和 Initialize；事件订阅绑定到新实体。已有 Core PhysicsRegression 可覆盖真实的双方回调、重复死亡、静态支撑与倒塌伤害。默认参数仍需实际关卡试玩；如需调整，优先修改公共配置中的耐久、阈值或材质倍率，再统一复跑实际物理验证。
