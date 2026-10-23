# Combat 模块交付说明

日期：2026-10-24（Asia/Shanghai）。状态：代码与独立托管检查完成，Unity 集成与 Windows 实机检查由管理聊天统一执行。本模块没有启动 Unity、生成场景、改公共接口或执行 Git 写操作。

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
