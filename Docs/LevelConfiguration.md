# 关卡配置与 Unity 搭建工具

## v0.2 当前交付（2026-10-24）

本节优先于后面的 v0.1 历史说明。保留旧 `DemoLevel.json` 及全部 `.meta`，新增九关、整轮参数、版本校验和材质编辑；共享 schema 仍由 `Core/DemoContracts.cs`、`RoguelikeContracts.cs` 定义。本模块未改 Core、SceneBuilder、项目设置，也未启动 Unity 或执行 Git 操作。

### 入口和版本

| 入口 | 内容 |
| --- | --- |
| `Assets/Content/RunConfiguration.json` | version=1 的整轮配置；正式路线、四练习的名称/说明/默认构筑、全部 AbilitySettings 数值 |
| `Assets/Content/Levels/C01.json`～`C05.json` | version=2 的五个正式关；顺序 C01→C05，各 3 发，目标数 2/3/3/2/4 |
| `Assets/Content/Levels/P01.json`～`P04.json` | version=2 的独立练习关，各 3 发，目标数 3/2/3/2；构筑定义在整轮配置中 |
| `Assets/Content/DemoLevel.json` | v0.1 旧配置，保持原样可载入，不是 v0.2 九关之一 |
| `RunConfiguration.Parse/Validate/Serialize` | 新的整轮参数 API，使用公共 RunConfigurationData |
| `RunConfiguration.ValidateReferences(data, IDictionary<string,LevelData>)` | 对已载入目录检查缺关、ID 不一致、单关错误及三发规则，不在运行时扫描磁盘 |
| `LevelConfiguration.Parse/Validate/Expand/Serialize` | 同时支持关卡 v1 和 v2；已有签名不变 |
| `MaterialDefaults.Apply(part, material)` | 在保持身份、尺寸、角色、固定/地面/不可破坏标记的前提下切换材质数值 |

整轮 version=1 和单关 version=2 属于两个不同文件格式。JSON 所有标量及设置对象必须显式提供；向量需要数字 x/y。不会接受 `"y":"-4.1"` 这样的字符串数字。校验从公共字段反射，不维护第二套 DTO；未知拼写、重复 JSON 字段、漏字段、错误类型、整数溢出和非有限 float 均报具体路径。

关卡 v1 可以省略 id/material/isGround/indestructible。缺失材质为 Legacy=0，按原来的静态/Platform 不可破坏及 Platform 地面规则解释。v2 必须有 id，且每个零件必须写出 material、isGround、indestructible，即使值为 false 也不能省略；不依赖 JSON 属性顺序。v2 不允许 Legacy。语义校验还检查库和模板引用、展开 ID、初始位置、发射弹体重叠及构件的矩形穿插；穿插检测包含旋转，容许 0.02m 接触误差。接触稳定性仍需要物理实测，几何通过不等于开场稳定。

### 材质与独立覆盖

MaterialId：Wood=1、Glass=2、Stone=3、Metal=4。v2 的 isStatic 仅控制是否固定，indestructible 控制是否可破坏，isGround 控制地面规则。固定木墙可以被破坏；动态石块可以不可破坏；三者不按名称或材质自动互相推导。关卡目标必须动态、可破坏且不是地面；地面必须固定。普通石材预设耐久较高但可破坏，只有显式 indestructible=true 的掩体无损。

| 切换预设 | mass | hitPoints | impulseThreshold | damageMultiplier | friction | bounciness |
| --- | --- | --- | --- | --- | --- | --- |
| Wood | 1.4 | 8 | 1.5 | 1 | 0.70 | 0 |
| Glass | 0.65 | 3 | 0.7 | 1.35 | 0.45 | 0 |
| Stone | 5 | 28 | 3 | 0.65 | 0.80 | 0 |
| Metal | 3 | 16 | 3 | 0.75 | 0.65 | 0 |

这些是每件零件的作者预设，不按体积自动换算质量或耐久。已有 JSON 各字段都是明确覆盖值；更改 JSON 的 material 整数不会隐式覆盖其他数字，避免修改一种数值时意外重平衡全部属性。要执行“切换材质并采用默认参数”，在代码调用 Apply 后再单独改字段，或使用编辑器材质菜单：

```csharp
MaterialDefaults.Apply(part, MaterialId.Stone);
part.mass = 3.5f;        // 仅覆盖质量
part.hitPoints = 20;     // 仅覆盖耐久，其余仍用石材预设
```

在 Edit 模式 Load 任一 v2 JSON，选中带 Marker 的零件，执行 `Siege Demo/Level/Material/Wood defaults`（也有 Glass/Stone/Metal）。工具为选中对象创建独立零件库条目，不修改所有共用原 partId 的对象；尺寸、位置和三种标记保留，替换数值立即在预览重建。改动先留在 workingJson 草稿，只有 Save 才写磁盘。Reset 恢复载入/上次保存快照。源文件被外部改动仍会阻止覆盖，不会把材质草稿误判为外部冲突。

### 能力字段与首轮参数

所有参数位于 RunConfiguration.json 的 abilities 中，下面列出全部字段。除列出的角度/比例范围外，正值必须有限且 >0，非负值必须有限且 >=0。

| 分组 | 字段和当前值 | 含义/校验 |
| --- | --- | --- |
| split | speed=18，spreadAngle=18，radiusScale=0.58 | 固定子弹速度；中线到单侧夹角 0～89.9°；半径比例 >0 且 <1。3 枚和质量各 1/3 是规则，非数量参数 |
| dash | speedIncrement=10，centerAngle=0，angleWidth=360 | 当前速率加正增量；中心角为有限角度，宽度 >0 且 <=360°；不配置无重力阶段 |
| piercing | minimumSpeed=6，maximumThickness=1.2，speedRetention=0.82 | 最低速度非负、最大厚度正值、保速 0～1；一次只穿一个木构件 |
| groundBounce | verticalMultiplier=1，minimumUpSpeed=9，horizontalRetention=0.85 | 竖直系数非负、最小起跳速度正值、水平保留 0～1 |
| stoneRicochet | maximumGlancingAngle=30，speedRetention=0.85，minimumUpSpeed=9 | 相对表面切线的角度 0～89.9°；保速 0～1、最小向上速度正值 |
| apexDive | downSpeed=18，horizontalRetention=0.55 | 向下速度正值、水平保留 0～1；只赋值一次 |
| rest | stableSeconds=0.6，countdownSeconds=1，maximumSpeed=0.3，maximumAngularSpeed=20 | 两个时长正值；低速阈值非负；支撑/打断规则由正式能力实现 |
| explosion | radius=4，damage=12，impulse=9，falloffPower=1 | 半径和衰减幂正值，直接伤害与推力非负且彼此独立 |
| deathShock | radius=3.4，damage=9，impulse=8，falloffPower=1 | 同上；衰减为 pow(clamp01(1-distance/radius),falloffPower) |
| shards | speed=18，spreadAngle=30，mass=0.25，radius=0.1，lifetime=6 | 速度/质量/半径/寿命正值；两破片总夹角 0～180°。2 枚和每原始发射一次是规则 |

主动 ID 为 Split=1、Dash=2；练习默认不允许 None=0。被动 ID：WoodPiercing=0、GroundBounce=1、RestExplosion=2、DeathShock=3、GlassShards=4、StoneRicochet=5、ApexDive=6。被动列表不能含未知值或重复值。正式路线与练习入口校验唯一性和本版固定 ID；练习必须有名称、操作说明及明确构筑。正式升级的等概率、三选一、已有项移池由 Core 流程实现，不在 JSON 添加一套权重/刷新规则。

### 九关布局与预计路线

所有地面顶部 -3.6，发射点 (-9,-2.4)，最大速度 20；大部分木承重柱宽 0.4～0.45m，C03 的宽桥墩为 1.6m、P03 的矮木墩为 1.25m，弹性均为 0。布局是独立数据，不以改名称复用相同场景。以下均为供集成试玩验证的预计解法，并非已通过结论。

| ID/目标数 | 真实布局与预计基础路线 | 组合路线/观察 |
| --- | --- | --- |
| C01 / 2 | x=1、3 的木柱承 x=2 横梁，敌人在底层和梁上；低抛拆前柱压倒，剩余两发处理残敌 | 分裂打两柱，冲刺修正后柱；无被动也可拆支撑 |
| C02 / 3 | x=-0.8 固定可破坏木墙，底侧留 0.7m 缺口；后方 x=3 矮架有上下两敌，x=7 木架有一敌 | 基础高抛越墙，或首发拆墙再打内部架；穿木不是必需条件 |
| C03 / 3 | x=1.6、4.4 两宽木墩托玻璃桥，桥上敌、桥下敌与 x=7 侧架敌；桥左上放独立木配重；桥下敌独立 HP=4，另两敌 HP=5 | 基础直接碎桥/打支撑产生坠落；破片与死亡冲击仅在对应被动存在时额外触发 |
| C04 / 2 | x=-6.8 的 24° 石斜面，x=-1 不可破坏固定石墙；墙后显式地面条，x=5.4 木台上两敌 | 可直接高抛越墙拆木台；也可擦石抬升或墙后下冲反弹。掩体不靠 HP 数字冒充不可破坏 |
| C05 / 4 | 前排低石掩体，x=2.5 双柱玻璃顶有上下两敌，x=7 双柱木堡有上下两敌 | 三个弱点是前柱、玻璃顶、后承重点；预期靠一次倒塌覆盖多敌，以保留三发基础解法 |
| P01 / 3 | x=-2 固定可破坏木外墙；x=1.3 三段独立木内层；三个目标位于地面及两级固定窄台 | 默认 Split+[WoodPiercing]。墙前分裂，或母体穿外墙后分裂，检查子弹独立穿内层。上方保留高抛入口 |
| P02 / 2 | x=-2.8 石墙，墙后空落地区；x=5 木台下可攻击，台上两敌 | 默认 Dash+[GroundBounce]。越墙后向落地区下冲，触地向上打木台底部；可高抛直击 |
| P03 / 3 | x=0.3 薄玻璃，首敌 x=1.2；后方弱双柱托两敌，死亡冲击可破坏支撑；首敌独立 HP=1.5，后两敌 HP=2.4 | 默认 Dash+[GlassShards,DeathShock]。直接撞玻璃，破片杀近敌，再冲击弱架引发坠落。提供未放置的 occlusion-variant 遮挡板模板，作遮挡对照 |
| P04 / 2 | 发射侧 x=-6.8、24° 斜石，x=-1.5 高石墙；墙后两组木顶与下方目标 | 默认 Dash+[StoneRicochet,ApexDive]。约5°重档在首次顶点前擦石，新抛物线越墙后俯冲砸后木顶；保留高抛路线 |

P03 遮挡对照：在 structures 加 `{"id":"shield-test","templateId":"occlusion-variant","position":{"x":0,"y":0},"rotation":0}`，会在 x=1.8 放置落地的可移动木板。原始样例不放此板；对照版应另存。遮挡需验证直接伤害和推力都阻断，不能用观察到后续倒塌就断定直接伤害穿透。

### 修改、预览和试射流程

1. 在 Unity 外编辑 JSON：关卡摆放/库参数改 Levels/*.json，共用能力值及练习构筑改 RunConfiguration.json。完整保留必填字段和数字类型。
2. 关卡使用 `Siege Demo/Level/Load JSON...`；场景调整位置/角度、复制后改 Marker ID、删除或用材质预设菜单。Save 会展开到 objects，保留 v2 id/材质/标记/库/模板和其余参数。
3. 选中 RunConfiguration TextAsset 后执行原 Validate 菜单，或执行 `Siege Demo/Run/Validate configuration and nine levels`，可同时检查整轮参数和目录。纯代码使用 Parse/ValidateReferences。
4. `Reset and test saved JSON` 是所选已保存单关的**无主动/被动基础物理试射**：临时清空控制器 runSource 走现有单关入口，退出 Play 后自动恢复原来的 levelSource/runSource 引用。它不伪造正式通关或奖励；组合请正常进入 Play 后从主菜单的对应练习入口进入，正式流程从开局选主动进入。引用恢复与菜单交互仍待管理端真实验收。
5. 合法变更必须在 Unity 导入并重新 Validate、Build、运行检查后才交付 Windows 包。TextAsset 被编入玩家数据，当前模块不实现 Windows 外置 JSON 热加载；改工程文件不会自动改变已经构建的 exe。

现有 Play 期间载入/保存/捕获禁止、初始快照与运行现场分离、外部文件冲突保护、写临时文件再替换等防护均保留。workingJson 仅是场景作者草稿；不从倒塌实体反推初始配置。

### 验证入口与证据边界

本轮模块实际执行：

- 外部编译：`.setup/ConfigurationChecks/ProductionCompile.csproj`，引用锁定 Unity Engine/Editor 程序集，模块全部代码 0 警告、0 错误；Core 集成类型使用签名占位。
- 外部回归：`.setup/ConfigurationChecks/RunChecks.csproj`，旧配置 41 项、新版 67 项通过。新版包括九关正确目标数/三发/完整读写/展开、目录引用、v2 漏字段、错误类型与材质、初始穿插、固定与不可破坏分离、材质预设及独立覆盖、能力非法值/拼写/重复被动。运行的是实际配置源码，原生 JsonUtility 使用 System.Text.Json 适配器替代，未启动 Unity。
- 模块范围 `git diff --check -- Assets/Scripts/Configuration Assets/Editor/LevelAuthoring Assets/Content Docs/LevelConfiguration.md` 通过。没有自行生成 `.meta`、提交或推送。

已交付供管理端运行的真实 Unity 检查：

- `SiegeDemo.Editor.LevelConfigurationChecks.Run()`：保留 v1 检查，文件存在时自动接续新版只读检查。
- `SiegeDemo.Editor.RoguelikeConfigurationChecks.Run()`：真实 JsonUtility 的九关完整读写/目标/引用/材质/非法配置；不生成场景、不修改配置。
- `SiegeDemo.Editor.RoguelikeConfigurationChecks.RunAuthoring()`：在没有 Level Preview 的活动场景中，复制 P01 到忽略的 .setup/validation，载入并把固定可破坏木外墙切为玻璃，验证尺寸/固定/可破坏保留、草稿不写盘、v2 Save/Load 保留身份/库/模板。原始 P01 不改。该入口会短暂创建预览并清理；由管理端统一调用。

| 验收类别 | 最终版本需验证的结果 |
| --- | --- |
| 正常 | 全九关 Parse、场景载入、开场稳定；全部数值写回一致；改一项合法 ability 参数后进入对应练习实际观察变化 |
| 重复 | 重复载入无多余根，重复保存不增库条目；重复 ID/练习引用/被动拒绝；材质切换只影响选中对象 |
| 重置 | 材质草稿/位置未保存时 Reset 回载入快照；试射不改源；退出 Play 恢复控制器引用；重复正式/练习重试不污染配置 |
| 异常 | v2 缺 id/material/isGround/indestructible、字符串坐标、未知材质/能力、非法有限值/比例、缺关、穿插均定位；错误加载不破坏旧预览，错误保存不改磁盘 |
| 物理与流程 | 正式九关布局实测、正式各三发基础解法、P01～P04 动作顺序、遮挡变体，以及两主动/七被动/整轮与奖励流程由 root 和战斗模块验证 |

本模块没有把上述 Unity 入口或物理路线标记为通过。v0.1 的历史成功日志不算 v0.2 的证据；管理端完成集成后按实际日志更新交付状态。

### P03 开场稳定修正（2026-10-24）

管理端新版玩家 Smoke 已确认 C01～C05 和 P01/P02 开场稳定，P01/P02 的组合顺序也通过；随后在 `Logs/player-smoke.log` 的 P03/weak-right 开场位移检查失败。该日志没有给出具体位移量，因此这里只记录失败门槛，不把原因断定为某一种物理求解器问题。

本次只改 P03 两根动态弱柱的支撑条件：宽 0.4→0.75m、质量 0.8→1.6、摩擦 0.7→0.85，柱中心 X 从 2.3/4.7 移到 2.9/4.2，与上方两个敌人的中心对齐，缩短偏心载荷经过横梁的力臂。柱高/接触高度、HP=2.5、伤害参数、动态且可破坏标记、横梁、敌人、玻璃、能力参数均保留；没有锁旋转或改为静态。

横梁及双敌的合成重心 X=3.525，处于两柱接触区间内；前柱破坏后，剩余右柱的左边缘 X=3.825，合成重心在其外侧，仍保留向左倾倒路径。首敌到前柱距离约 1.70m，当前死亡冲击无遮挡理论伤害约 4.50，高于柱 HP=2.5；这里的理论值不能代替遮挡快照与真实连锁实测。开场稳定和 P03/P04 组合仍需管理端复测。

第二次修正：管理端记录右柱由 (4.20,-3.05) 移至 (3.66,-3.04)，位移 0.5418m，角度变化仅 0.0618°，上一版未解决横向内滑。当前只在 P03 将弱柱改成宽 1.25m、质量 2.4、摩擦 1.0 的矮木墩，横梁摩擦 0.9；中心 X 保持 2.9/4.2，两墩之间留 0.05m，保留真实接触对内滑的阻挡。项目 defaultContactOffset=0.01m，因此初始叠层分别留 0.01m 间隙：墩中心 Y=-3.04，梁中心 -2.34，上层敌人中心 -1.69，减少一开始从几何零间隙产生的分离修正。所有标记、HP、能力参数和非 P03 数据均不改，未使用静态柱、关节、位置约束或门槛放宽。

新版横梁及双敌重心仍为 X=3.525；右墩左缘为 X=3.575，在左墩消失后重心仍在剩余支撑外侧，可向左倾倒。此为接触与静力检查，不宣称运行稳定；需管理端重新运行相同 Smoke 门槛，并继续验证破片杀首敌→死亡冲击→失撑倒塌。

接续管理端隔离进程证据：同一份上一版宽柱 P03 在 `Logs/v02-p03-isolated.log` 已完成开场与组合，但在全流程后进入仍内滑。因此不能用隔离进程单次成功替代重复进入回归。当前 1.25m 木墩版再将横梁中心 X=3.5→3.4，令梁及双敌的重心为 X=3.475；即使右墩在 0.05m 间隙内向左移动，剩余支撑左缘约 X=3.525，仍保留失去左墩后向左倾倒的余量。其余尺寸与数值不变，遮挡模板仍不与横梁穿插。最终以管理端新构建后的多次进入/重试实测为准。

最新全流程日志已出现 `ROGUELIKE_OPENING_STABLE: P03`，新版几何与完整初始 HP 通过；组合随后失败在“破片直接杀首敌”，死亡冲击来源 projectileId=0，尚不能把被推动后撞地死亡算为破片直接击杀。针对这一弱点只给 enemy-primer 增加独立零件定义 primer-enemy，HP=1.5；其尺寸、质量、免伤门槛等与原敌相同，后两敌仍使用 enemy 的 HP=2.4。支撑结构与能力参数不改。等待管理端复测真实破片死亡来源及后续连锁，不能仅以首敌最终死亡为通过条件。

### P04 俯冲接收位置修正（2026-10-24）

管理端实测 5° 重档先在 (-6.69,-2.20) 擦石，再于 (8.03,5.34) 触发顶点俯冲，但原本居中的后塔接不到随后约 X=10.4 的下落，木材没有实际受伤。本次不改全局能力值，只把 P04 后塔两柱、木顶和后敌整体向右平移 2.6m：柱中心 X=8.95/11.45，木顶和敌人 X=10.2，所有高度、内部相对位置、尺寸与物理属性保持。木顶水平范围为 8.7～11.7，可覆盖实测预计落点；前塔、斜石和墙保留。

普通高抛的解析检查：20m/s、75°、重力 9.81，从 (-9,-2.4) 发射，下降到木顶上表面加弹体半径的中心高度 -1.0 时，世界 X 约 11.0，仍位于新木顶范围；高抛在墙处高于墙顶。解析结果不包含碰撞求解，不代替实际路线验证；75° 弧线会短暂超出当前摄像机上边界。管理端应使用 `-practiceTest P04` 确认俯冲后的木材真实受伤，而非只检查伤害事件总数。正式五关及 RunConfiguration.json 没有修改。

### C03 多次进入时的桥柱稳定修正（2026-10-24）

管理端增加重试覆盖后，C03 右桥柱出现真实水平滑移：expected X=5，transform 与 Rigidbody.position 均为 X=4.09，停下时速度为 0、旋转仅约 0.075°。显式物理初值和延后一帧重建均未消除该现象。因此本次在管理者授权下只调整 C03 的 bridge-post 定义与两个桥柱位置，不改其他正式关、P03/P04、玻璃桥、配重、侧台、敌人或全局能力参数。

桥柱从 0.45×1.4m、质量1.5、摩擦0.7 改为 **1.6×1.38m、质量3.2、摩擦1.0**；HP=8、弹性0、动态且可破坏等标记保持。柱中心由 X=1/5 内移为 **1.6/4.4**，Y 仍为 -2.9。两柱顶面区间分别为 [0.8,2.4]、[3.6,5.2]，完整处于玻璃板 [0.8,5.2] 下方，左配重中心 X=1.6 对齐左柱。柱底 -3.59 与地面 -3.6、柱顶 -2.21 与桥底 -2.2 各留 0.01m 接触间隙。桥下目标位于 X=3、宽0.8，两侧各保留 0.2m 几何净空，无初始穿插。

这保留两个独立、可打碎的动态宽木墩，不使用固定/关节/锁位置。原先重档 7.5°/5° 的真实基础解法属于旧柱版本，不能沿用为新版本通过证据；需要管理者重跑多次进入/重试的开场位移、HP 与三发内基础解法验证。

最终平衡初值调整：管理端已确认宽墩版 C03 在整轮重复进入中开场稳定，但 `Logs/v02-final-solutions.log` 的无能力重档 12.5°、12.5°、5° 路径最终仅桥下 enemy-under 剩余 HP=0.32（初始5）。按管理者授权，仅为该对象新增独立零件 under-enemy，将 HP 设为4.0；尺寸、质量、阈值、材质、其他物理属性与原 enemy 相同。桥上及侧台仍使用 HP=5 的 enemy，最新稳定宽墩与其他关卡完全保留。此修改为基础路径增加少量伤害余量，最终三发解法与整体回归仍须管理端在新构建中真实确认；旧日志不能直接记为新版通关通过。

---

## v0.1 历史交付说明

Configuration 模块，2026-10-24。使用公共命名空间 `SiegeDemo` 和 `Core/DemoContracts.cs`，没有第二套数据模型。编辑器工具位于 `SiegeDemo.Editor`。目标版本固定为 Unity 6000.3.27f1。

## 文件与数据源

- `Assets/Content/DemoLevel.json`：唯一初始关卡数据，包含零件库、局部结构模板和结构实例。
- `Assets/Scripts/Configuration/LevelConfiguration.cs`：解析、语义校验、结构展开和序列化。
- `Assets/Scripts/Configuration/ConfigurationJson.cs`：严格 JSON 语法/字段校验，字段表直接来自公共类型。
- `Assets/Scripts/Configuration/LevelObjectMarker.cs`：对象身份 `objectId`、`partId`。
- `Assets/Scripts/Configuration/LevelPreviewSource.cs`：场景序列化的载入基线和源路径；进入 Play 时关闭预览，防止与战斗世界重复碰撞。
- `Assets/Editor/LevelAuthoring/LevelAuthoringTools.cs`：载入、场景位置回写、校验和重置试射菜单。
- `Assets/Editor/LevelAuthoring/LevelConfigurationChecks.cs`：使用真实 JsonUtility 的只读回归检查，供管理者集成调用。

样例为 3 发、3 个装甲目标、3 组相同的独立支撑结构：1 个固定地面 + 9 个动态梁材 + 3 个动态目标，共 13 个对象。地面顶部为 -3.6，发射点为 (-9,-2.4)，结构中心 X 为 1、4.3、7.6。每组两根立柱、一根横梁和一个站在地面的目标；横梁底部与柱顶接触，目标不接触横梁。分组仅展开摆放，不生成焊接或关节。梁材 HP 为 10/12、目标 HP 为 8；这些是首版调试初值，实际命中与通关由管理者的玩家 Smoke 确认。

## JSON 格式

版本 1 采用明确字段：所有标量、对象字段和向量 x/y 均必填；顶层 `objects`、`templates`、`structures` 可以省略、为 null 或为空数组。`parts` 必须非空。模板 `objects` 必须非空。完整示例直接复制 DemoLevel.json 后修改，避免丢失必填参数。

JSON 不接受注释、尾逗号、重复字段、未知字段和非有限数字。枚举使用整数，布尔值使用 true/false；不会把字符串数字自动转换。ID 区分大小写，只使用英文字母、数字、下划线、短横线和点，可用 `/` 分段。关卡名称可以使用中文。

| 字段 | 含义与校验 |
| --- | --- |
| version/name | 版本必须为 1；名称非空 |
| ammunition | 1～100 的整数；样例 3 |
| launcher | 发射点 x/y；必须在有效 X 范围内且高于 killY |
| killY/minX/maxX | 有限数值，minX < maxX；所有展开对象的初始中心均在 X 范围内且高于 killY |
| launch | maxSpeed、maxDrag、projectileMass、projectileRadius > 0；0 <= cancelRatio < mediumRatio < heavyRatio < 1；0 <= minAngle <= maxAngle <= 89.9 |
| settlement | minSeconds >= 0；stableSeconds、timeoutSeconds > 0；timeoutSeconds >= 1 且不小于前两项；线速度/角速度阈值非负 |
| parts[].id | 零件库中的唯一 ID |
| parts[].kind | 0=Beam，1=Target，2=Platform；目标必须动态，Platform 必须静态 |
| parts[].size | BoxCollider2D 的完整宽高，均 > 0；尺寸不使用 Transform 缩放表达 |
| parts[].mass/hitPoints | 均 > 0；静态零件也保留明确字段 |
| parts[].impulseThreshold/damageMultiplier | 冲量免伤门槛 >= 0，伤害系数 > 0 |
| parts[].friction/bounciness/isStatic | 摩擦和弹性均在 0～1；固定平台 isStatic=true |
| objects[] | 直接放置的对象；包含唯一 id、已存在的 partId、世界 position、角度 rotation |
| templates[] | 包含唯一 id 和局部 objects；局部对象 ID 在该模板内唯一，position 相对模板原点 |
| structures[] | 包含唯一 id、已存在的 templateId、世界 position、角度 rotation |

旋转使用度数和 Unity 2D 的 Z 轴正方向。展开公式是 `世界位置 = 实例位置 + 旋转(局部位置, 实例角度)`，`世界角度 = 实例角度 + 局部角度`，对象 ID 为 `实例ID/局部对象ID`。展开后再次检查全局重复；例如直接对象 `bay-a/roof` 与结构生成的同名对象不能共存。没有放置任何 Target 的关卡也会被拒绝，避免空关卡立即胜利。

添加一组结构的配置片段：

```json
{ "id": "bay-d", "templateId": "guard-bay", "position": { "x": 10.0, "y": -3.6 }, "rotation": 0.0 }
```

应把该项放进完整关卡的 structures 数组。这里展示字段调用，不表示额外结构已经加入首版。

## 代码调用

```csharp
LevelData data = LevelConfiguration.Parse(textAsset.text); // 无效时抛 FormatException
List<string> errors = LevelConfiguration.Validate(data);   // null 数据也返回具体错误
List<Placement> placed = LevelConfiguration.Expand(data);  // 无效时抛 FormatException
string json = LevelConfiguration.Serialize(data);          // 先校验，再格式化输出
DemoLevelFactory.Rebuild(data, worldRoot, gameplay: true);
```

`Expand` 每次产生新 Placement，既不修改输入的 objects，也不把结果追加到模板或结构。调用方修改展开结果不会改动初始关卡。Validate 会给出 `parts[1].mass`、`templates[0].objects[2].partId`、`expanded['bay-a/roof'].id` 等字段位置；JSON 语法错误还包含字符偏移。

编辑器自动化可以调用：

```csharp
GameObject preview = SiegeDemo.Editor.LevelAuthoringTools.LoadFile(
    "Assets/Content/DemoLevel.json");
// 在 Edit 模式调整 Marker 对象的 Transform.position 或 Z 轴角度。
LevelData captured = SiegeDemo.Editor.LevelAuthoringTools.CapturePreview();
SiegeDemo.Editor.LevelAuthoringTools.SavePreview("Assets/Content/DemoLevel.json");
// 管理端统一执行的真实 Unity 配置回归入口：
SiegeDemo.Editor.LevelConfigurationChecks.Run();
```

此模块未调用 DemoSceneBuilder.Configure，也未生成正式场景。场景生成、公共工厂、控制器、项目设置和构建始终由管理者集成。

## Unity 操作

菜单统一在 `Siege Demo/Level`：

1. **Load Demo JSON / Load JSON...**：先解析、验证并建立替换对象，成功后通过 Undo 替换当前活动场景中的 `Level Preview`。非法配置和构建异常保留原预览。重复载入不会积累多个预览根。
2. **Validate JSON and preview**：选中 TextAsset 时校验该文本；否则校验已载入预览；没有预览时校验默认 JSON。
3. **Save preview JSON / Save preview JSON as...**：从 Marker 读取世界位置及 Z 轴角度。保存后所有对象进入 objects，structures 清空，零件库、模板、发射与结算参数、关卡边界和弹药数量保留。写入采用同目录临时文件和原子替换，失败时原文件不变。
4. **Reset preview to loaded snapshot**：放弃未保存的位置调整，从场景中记录的载入/上次保存快照恢复；不读取物理倒塌状态。要获得外部编辑后的最新文件，请重新 Load。
5. **Reset and test saved JSON**：从磁盘重载、校验并恢复预览，为当前正式场景中唯一启用的 BattleController 绑定该 TextAsset，然后进入 Play。需要已生成并打开 FoundryDemo，且 battleCamera 已绑定。仅支持 Assets 内的 JSON。未保存的位置修改不会参与此次试射；先 Save 才会采用这些改动。退出 Play 后再次执行可从同一保存基线重试。
6. **Run configuration checks**：执行真实 JsonUtility 的只读配置回归，不生成场景、不启动 Play、不写配置。

全部载入、保存和捕获操作在 Play 或正在切换 Play 时拒绝执行。预览根进入 Play 后停用，战斗控制器只从保存的 TextAsset 建立独立运行世界。源文件被外部工具改动后，覆盖原路径保存会被拒绝；可重载或另存，避免 AI 修改被旧预览覆盖。

场景手工编辑范围是位置、Z 轴角度、删除和复制已有对象。复制后要修改 Marker.objectId，重复 ID 不能保存。尺寸、质量、材质和静态属性应在零件库 JSON 修改后重新载入；工具会拒绝非单位缩放、离开 XY 平面、禁用对象、不匹配的碰撞体/材质/刚体、无 Marker 碰撞体等无法通过当前 schema 完整表达的修改。结构展开后保存会失去实例与模板的关联，但保留模板定义；这是契约规定的显式展开，不会自动反推模板。

## 验收与当前证据

模块自行运行的检查：

- `dotnet build .setup/ConfigurationChecks/ProductionCompile.csproj --nologo --verbosity quiet`：对锁定 Unity 6000.3.27f1 的 Engine/Editor 程序集编译模块全部源文件，0 警告、0 错误。公共工厂与控制器用契约签名占位，仅为模块编译隔离。
- `dotnet run --project .setup/ConfigurationChecks/RunChecks.csproj -- Assets/Content/DemoLevel.json`：41 项通过，覆盖样例数量与支撑几何、读写一致、90° 模板旋转、独立展开、反复展开、展开后保存、重置、空值、重复 ID、缺引用、非法范围、NaN/Infinity、JSON 拼写/语法/类型/必填字段。此检查运行实际配置代码与 Unity Vector2，但用 System.Text.Json 替代原生 JsonUtility，仅是外部检查。检查工程位于忽略的 .setup 中，不作为游戏源码提交。

管理者已实际运行的集成证据（本模块读取 Logs/validate.log 核对）：

- 日志包含 `LEVEL_AUTHORING_ROUNDTRIP_SUCCESS`：真实 Unity LoadFile → 手动移动目标 → SavePreview → 重载已通过；展开保存保留模板与弹药参数；重复标记保存拒绝、失败保存不改原文件；非法载入保留原预览；捕获未修改原始配置。
- 同一日志包含 `PROJECT_VALIDATION_SUCCESS`；管理聊天通知首轮 Windows Build 已成功。此处不把首轮构建视为玩家物理验收通过。

以下验收步骤供管理者针对最终冻结版本执行：

| 场景 | 操作与期望 |
| --- | --- |
| 正常 | 打开 FoundryDemo，载入 Demo JSON；看到 13 个 Marker；移动/旋转一对象，保存重载后位置一致；parts/templates/launch/settlement 等参数保留 |
| 重复 | 连续载入三次，只存在一个 Level Preview；重复 Expand 的对象数和 ID 一致；复制 Marker 不改 ID 时保存失败，磁盘不变 |
| 重置 | 载入后移动对象，不保存，执行快照重置，恢复原位置；试射倒塌后退出 Play 再试射恢复全部初始对象、目标和弹药，源文件字节不变 |
| 异常 | 缺 partId/templateId、重复 ID、重复 JSON 字段、错拼字段、NaN/Infinity、负尺寸和错误档位分界均报告具体字段；非法 Load 保留旧预览 |
| 外部修改 | 载入后用文本编辑器改源文件；覆盖保存拒绝；另存新路径或重新 Load 能继续 |
| 编辑安全 | Play 中调用 Load/Save/Capture 被拒绝；缩放/材质/刚体修改被拒绝；不应把倒塌状态写回 JSON |

模块没有启动 Unity，也没有自行运行 Windows 玩家、Physics Smoke、交互式菜单、Undo/Redo、Play 模式写入阻止及源文件冲突的实机验收。真实 JsonUtility 的新增 `LevelConfigurationChecks.Run()` 入口已落盘，需由管理端统一运行；玩家命中与通关、重置试射和上述尚未覆盖的编辑器交互继续由管理端确认。
