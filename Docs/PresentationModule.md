# Presentation 模块接入与验收

交付状态：可集成实现；独立 C# 编译通过。Unity 导入、正式场景、画面、交互和 Windows 运行由管理聊天统一检查，本模块未启动 Unity。日期：2026-10-24。

## 文件与职责

| 文件（均位于 Assets/Scripts/Presentation） | 职责 |
| --- | --- |
| DemoPresentation.cs | 契约入口、IBattleSession 绑定、背景与反馈生命周期 |
| DemoPrimitives.cs | 共享程序化 Sprite、节点与绘制工具、工业色板 |
| DemoVisualFactory.cs | 梁材、平台、装甲目标、无人机、发射器组合造型 |
| DemoDroneVisual.cs | 仅旋转视觉子节点、飞行推进器动画 |
| DemoLauncherVisual.cs | 发射导轨随实际瞄准角度旋转 |
| DemoEnvironment.cs | 深色工业天际线、高架栈桥、雾层、地面立面 |
| DemoImpactEffects.cs | 冲击环、火花、无物理碎屑、伤害与摧毁文字 |
| DemoHud.cs | 中文状态、目标、弹药、档位、角度/速度、重开与操作提示 |

素材在代码中生成，未添加 AI 位图或外部依赖，不需要 Assets/Art/Demo 中的资源。未修改公共代码、场景、项目设置、Packages 或构建脚本，未手工生成 .meta；Unity 导入及 Git 记录由管理者完成。

## 配置与调用

命名空间为 SiegeDemo，严格保留 DemoIntegrationContract.md 指定签名。唯一运行依赖是 DemoContracts.cs；未引用 Combat、Configuration 或具体 BattleController。

```csharp
// 管理器在场景相机和会话已经建立后配置一次；重复配置会复用背景并清空反馈。
var presentation = controller.gameObject.AddComponent<DemoPresentation>();
presentation.Configure(session, battleCamera); // session : IBattleSession

// 工厂先配置物理实体，再增加造型。size 为物理实体的实际米制尺寸。
DemoPresentation.DecoratePart(partObject, partDefinition);
DemoPresentation.DecorateProjectile(projectileObject);
DemoPresentation.CreateLauncher(levelData.launcher, launcherRoot);

// 由管理器已有的伤害/摧毁回调转发。damage 为实际扣除值。
DemoPresentation.PlayImpact(body.transform.position, actualDamage, false);
DemoPresentation.PlayImpact(body.transform.position, 0, true);

// 常规 Restart 切到 Preparing，表现层下一次 Update 自动清空全部旧反馈。
// 若自定义流程在同一帧完成重建并直接跳到 Ready，可在重建前显式调用：
DemoPresentation.ClearFeedback(); // 兼容增量 API，可选
session.Restart();
```

与当前 DemoLevelFactory 和 BattleController 的调用相容。DecoratePart、DecorateProjectile 在实体下面替换名为 `__DemoVisual` 的子节点；CreateLauncher 在指定 parent 下替换 `__DemoLauncher`。旧节点先禁用，再按编辑/运行模式销毁，重复调用不会产生重叠的活动造型。装饰不修改实体 Transform、刚体、材质、碰撞体、耐久、发射参数或弹药。

PartDefinition.kind 决定造型，size 决定梁/目标尺寸；梁沿长轴布置槽钢、斜撑、端板和铆钉；目标为橙色胸甲、红色光学器、装甲腿；无人机为浅色装甲、青色光学器、翼片和推进器。无人机视觉按实际刚体速度转向，装填时按 Aim.angle 转向。实体本身的物理旋转不受影响。投射物视觉按默认 .32 m 半径设计，翼片/尾焰属于装饰。

CreateLauncher 的 origin 使用世界坐标。发射器相对 origin 向下约 1.2 m，默认 level.launcher.y=-2.4 时底座落在地面 -3.6 附近。背景立面按契约 ground top=-3.6 绘制；改变关卡地面高度时需同步调整 DemoEnvironment 的纯视觉立面。背景没有地面碰撞，必须由关卡配置提供实体地面。

## HUD 与输入

- HUD 使用 IMGUI，原始屏幕像素布局。顶部占 y=16～92，底部占距底边 16～94 px；与 Combat 顶部 110 px、底部 100 px 的输入排除区相容。
- 唯一可点击区域是右上重开按钮：`Rect(Screen.width-150,20,128,54)`。HUD 没有全屏捕获层、Canvas、EventSystem 或透明按钮。伤害文字与结果提示只是绘制，不捕获拖拽。
- 目标与弹药直接读取会话；轻/中/重高亮和数值直接读取 Aim，不另行计算力度。取消区不高亮任何档位，并提示松手不消耗；结算时提示倒塌仍然计入结果。进度条表示 ResolveProgress，即到结算超时上限的进度，不宣称物理稳定百分比。
- 提示语包括反向拖拽、松开发射、缩回取消、结构稳定、物理结算、胜利/弹尽和重开。首版只展示一种冲击型无人机。
- 设计目标是 1600×900，1100 px 以下使用紧凑字号，建议集成检查 1600×900、1280×720、960×540。HUD 不修改相机大小或视角，窄纵横比下关卡是否完整可见由场景相机负责。
- 中文通过运行系统的动态字体显示，优先 Microsoft YaHei / Microsoft YaHei UI / SimHei / Noto Sans CJK SC。标准中文 Windows 是目标环境；未复制系统字体。缺少这些字体的系统需在集成时确认中文字形，必要时另行引入有分发授权的字体。后备 LegacyRuntime 不保证中文。

## 反馈、重复调用与清理

每个 DemoPresentation 同时维护最多 128 个可复用的火花/碎屑/环节点、16 条浮字，每帧最多 12 个有效反馈请求。所有碎屑仅通过 Transform 做短时运动与淡出，最多约 .65 秒，浮字约 .85 秒；无 Rigidbody2D、Collider2D、伤害或二次物理行为。独立 System.Random 避免消耗游戏 UnityEngine.Random 序列。

同帧同位置（0.05 m 量化）同类反馈自动去重。伤害紧接摧毁时保留冲击与破坏效果，浮字合并成“摧毁”，避免叠字。跨帧的实体伤害/死亡事件去重仍由战斗模块负责，表现层不会改变事件计数或胜负。

进入 Preparing 的每个 Update 会清空反馈，禁用和销毁组件也会清理；HUD 重开按钮在调用 Restart 前同步清理。重复 Configure 不重复建立背景和反馈根。一个运行场景使用一个活动 DemoPresentation；静态装饰函数可在配置 UI 之前调用。共享 Sprite 缓存最多五种形状，运行生成资源不写入资产或关卡。编辑器预览需要通过工厂重新生成，不能把程序化临时 Sprite 当作持久美术资产。

空 go/part/parent、非法尺寸/位置、负数或非有限伤害会安全忽略；零伤害普通命中不产生虚假扣血提示；destroyed=true、damage=0 仍产生破坏反馈。PlayImpact 在表现层未配置或禁用时安全忽略。空 session 显示连接提示且禁用重开；缺少 camera 时尝试 Camera.main，仍缺少时保留 HUD，跳过世界浮字。

## 验收清单与实际验证

实际执行：使用固定 Unity 6000.3.27f1 安装目录中的 DotNetSdkRoslyn/csc.dll 和 NetStandard 2.1 / UnityEngine 参考程序集，对 DemoContracts.cs + 本目录 8 个 C# 文件做独立库编译，启用 `-warnaserror+`；最终结果为退出码 0、零错误、零警告。输出位于忽略目录 `.setup/PresentationCheck/`。该检查没有启动 Unity。另做文件边界与组件扫描，装饰仅 AddComponent SpriteRenderer、DemoDroneVisual、DemoLauncherVisual，无新增物理组件，无刚体状态写入。

以下为待管理者运行的验收，未报告为已通过：

| 类别 | 操作 | 预期 |
| --- | --- | --- |
| 正常 | 进入正式战斗场景，等待 Ready | 工业背景、导轨、可辨识梁/装甲目标/无人机均显示，中文无方框；弹药 03，目标数与会话一致 |
| 正常 | 拖过轻/中/重三档，改变角度 | 档位与 Aim.tier 一致，角度/速度与实际参数一致，导轨和装填无人机方向一致 |
| 正常 | 缩回取消区后松手 | 无档位高亮，提示取消，不产生虚假发射/扣弹药；具体输入由 Combat 验证 |
| 正常 | 命中与破坏部件 | 青色冲击环/橙色火花、伤害浮字、摧毁提示；碎屑穿过实体且不参与伤害 |
| 正常 | 等待结算、胜利或弹尽失败 | HUD 状态和进度与会话同步；只显示结果提示，右上重开持续可用 |
| 重复 | 对同一实体连续 Decorate 两次；CreateLauncher 两次 | 每个根仅有一个活动装饰，无叠影，原始物理组件数量不变 |
| 重复 | 同帧同位置重复 PlayImpact 1000 次 | 相同种类只产生一次 burst；活动反馈节点不超过 128，浮字不超过 16，无异常 |
| 重复 | 连续 Configure 两次 | 不重复背景/反馈根；会话相机引用更新，旧反馈清空 |
| 重置 | 在瞄准/结算/胜负时点击重开，再重复点击 | 旧浮字碎屑同步清空，Preparing 后弹药/目标刷新，导轨回默认角度，不残留结果面板 |
| 重置 | 外部 Restart 进入 Preparing；必要时 ClearFeedback | 下一 Update 清空旧反馈；同帧直接切 Ready 的流程通过增量 API 同步清理 |
| 异常 | Configure(null,null)、无 Rigidbody 的投射物、空装饰参数 | HUD 给连接提示或装饰安全退化，无空引用异常 |
| 异常 | NaN/Infinity 位置、伤害，size<=0，负伤害 | 请求安全忽略，无巨幅渲染或异常；实体参数不变 |
| 输入 | 点右上按钮、上/下 HUD，再在无人机处拖拽 | 重开只一次，UI 点击不发射/扣弹药，发射点和目标区域不被 HUD 遮挡 |
| 屏幕 | 1600×900、1280×720、960×540 实机/调整窗口 | 信息不重叠，按钮保留在顶部安全区；相机仍能看见发射点与目标 |
| 构建 | Windows Build / Smoke / 手动一次完整战斗重开 | 无丢失 Shader/Sprite/字体/脚本，真实运行无表现异常；检查日志 |

尚未运行 Unity Validate、Build、Smoke，未对最终画面作实机截图验收。管理者首轮整体编译发现的 Light 命名歧义已统一改为 PaleSteel，并通过上述独立编译复核。
