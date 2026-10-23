# 关卡配置与 Unity 搭建工具

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
