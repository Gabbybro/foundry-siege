# Foundry Siege · Demo v0.1 交付与验收

日期：2026-10-24（Asia/Shanghai）。Unity 6000.3.27f1 / 672d562a42a7，Windows x64，Mono，Built-In。

## 运行与操作

- 直接运行 `Builds/Windows/FoundrySiege.exe`，保留同目录的 Data、MonoBleedingEdge 与 DLL。
- 完整分发包：`Builds/FoundrySiege-demo-v0.1-Windows.zip`。程序源版本 `91b0e4d`；包内 build-manifest.json 记录程序集与配置 SHA-256，Reference 内配置样例用于 Unity 作者流程，不是运行中的热加载文件。
- 按住左侧无人机向左下反向拖拽，力度为轻／中／重三档，角度连续；松手发射。缩回、右键或 Esc 取消。
- 默认 3 发、3 个目标。等待稳定后自动装填，最长等待 12 秒；最后一发后仍继续碰撞和倒塌结算。
- 右上角重新开始，关闭窗口退出。试射参考：重档约 10°、12.5°、5°；物理结果受具体瞄准及碰撞过程影响，不是固定动画。

## 当前范围

完成单关、无人机、机械发射平台、装甲目标、三组接触支撑梁材、冲量伤害和去重、无实体碎屑的破坏反馈、胜负及完整重开、中文 HUD。

统一配置 `Assets/Content/DemoLevel.json` 含 4 种零件预设、1 种结构模板、3 个结构实例、固定地面。梁柱宽度最终为 0.45m、无回弹，以避免重开后自行倾倒。配置字段和 AI/场景手调流程见 `Docs/LevelConfiguration.md`。

## 实际程序验收

以下均由管理者在锁定 Unity 版本上实际执行，不以模块独立编译代替实机验证：

| 项目 | 结果与证据 |
| --- | --- |
| 场景生成 | DemoSetup 成功，FoundryDemo 通过 Editor API 生成并绑定 JSON 和相机 |
| 最终 Validate | `PROJECT_VALIDATION_SUCCESS`；引用、管线、版本、三档/角度/取消、配置格式校验通过 |
| 真实配置回归 | `LEVEL_CONFIGURATION_CHECKS_SUCCESS`；真实 JsonUtility 读写、旋转展开、组合 ID、重复/缺引用/非法数值检查通过 |
| Unity 场景读写 | `LEVEL_AUTHORING_ROUNDTRIP_SUCCESS`；载入预览、移动、保存重载；重复 ID 保存和非法载入拒绝；原始数据保留 |
| Windows Build | `WINDOWS_BUILD_SUCCESS`；Development / StrictMode 构建成功 |
| 真实物理 | 玩家内实际碰撞双方各一次伤害；静止承重不扣血；梁材坠落伤害；死亡幂等并移除实体支撑 |
| 战斗流程 | 取消/非法参数不扣弹；重复发射拒绝；三次落空延迟失败；超时装填；飞行中重开；正常关卡三发获胜；单结构末发后获胜 |
| 重开稳定 | 开场、失败后重开、胜利后重开各静置 6 秒，初始物体偏移 <0.15m、转角 <4°；目标和初始生命恢复 |
| 最终 Smoke | `DEMO_SMOKE_SUCCESS`；原关卡实测 32 次伤害事件、11 次动态碰撞、三发获胜，随后末发胜利与重开通过 |

本机复现命令：`pwsh -File Tools/Unity.ps1 Validate`、`Build`、`Smoke`。完整日志位于忽略目录 `Logs/validate.log`、`Logs/build.log`、`Logs/player-smoke.log`。

实际窗口测试发现并修复了快速拖拽丢失按下起点的问题：使用原始鼠标事件位置发起/结束手势，逐帧更新预览，保留焦点丢失与 UI 保护。已在窗口中进行三发发射、目标清空、胜利显示和按钮重开；中文字体和 1600×900 HUD 可读。

最终包窗口回归日志 `Logs/final-ui.log`：小幅拖拽取消保持 3 发；鼠标重档约 9.9°、12.5°、5°，第三发后 `DEMO_RESULT: Won shots=3`；点击 HUD 重开恢复 3 发、3 目标。该验证使用最终加宽支撑配置。

## 已知限制与未覆盖项

- 首版没有肉鸽能力、空中主动技能、正式音效、任意切割、实体碎片或游戏内编辑器；程序化画面是可玩首版，不等同视觉参考图的最终美术质量。
- 当前运行的是开发构建，会显示 Development Build／环境许可证水印。没有更换或规避 Unity 授权。
- 人工手感、关卡难度仍需用户试玩决定。轻／中档遵循固定速度，不保证任何角度都能打到远端目标。
- 已实测默认 Windows 窗口；其他 DPI、多显示器、极小窗口未全面覆盖。焦点/右键/Esc 取消有实现与代码审查，尚未对所有按住鼠标切窗组合进行自动化穷举。
- 编辑器菜单按钮、Undo/Redo、Play 中写回拦截与外部修改冲突保护已有实现；底层载入/保存已实际运行，全部 GUI 菜单组合尚未逐项点击验收。
- 首版每实体一个碰撞器；未来复合碰撞器需要扩展跨碰撞器对合并。场景物理结果不承诺跨帧率/平台逐位一致。

## Git 与回退

私有仓库：`https://github.com/Gabbybro/foundry-siege`。`5374abd` 保留最初安装基线，`3fdbced` 保留暂停时未集成快照；模块与集成修复分别提交，最终通过版本以 `demo-v0.1` 标记。源码、配置、场景及 `.meta` 入库，安装包、缓存、日志、构建包不入库。
