using System;
using System.Collections.Generic;
using UnityEngine;
using static SiegeDemo.DemoPrimitives;

namespace SiegeDemo
{
    internal sealed partial class DemoHud
    {
        private enum PendingCommand { None, RestartRun, ReturnToMenu }
        private PendingCommand pendingCommand;
        private RunPhase? displayedPhase;
        private bool resumeAfterCancel;
        private string practiceId;
        private ActiveAbility practiceActive;
        private readonly HashSet<PassiveAbility> practicePassives = new HashSet<PassiveAbility>();
        private int lastCommandFrame = -1;
        private int observedAmmunition = -1;
        private bool activeWasUsed;
        private GUIStyle runHeading, runBody, runCaption, runCardTitle, runCardBody, runAction, runDetail;
        private static readonly PassiveAbility[] AllPassives = (PassiveAbility[])Enum.GetValues(typeof(PassiveAbility));

        internal void ResetRunUi()
        {
            pendingCommand = PendingCommand.None;
            displayedPhase = null;
            practiceId = null;
            practicePassives.Clear();
            maximumAmmo = 0;
            lastCommandFrame = -1;
            observedAmmunition = -1;
            activeWasUsed = false;
        }

        private void SyncRunUi(IRunSession session)
        {
            if (displayedPhase != session.Phase)
            {
                displayedPhase = session.Phase;
                pendingCommand = PendingCommand.None;
                practiceId = null;
                resumeAfterCancel = false;
                maximumAmmo = Mathf.Max(0, session.Ammunition);
                stateTime = Time.unscaledTime;
                activeWasUsed = false;
            }
            if (observedAmmunition != session.Ammunition || session.State == BattleState.Preparing || session.State == BattleState.Ready) activeWasUsed = false;
            observedAmmunition = session.Ammunition;
        }

        internal void ObserveCombatFeedback(IRunSession session, CombatEvent feedback)
        {
            if (feedback.kind != CombatEventKind.Split && feedback.kind != CombatEventKind.Dash) return;
            activeWasUsed = true;
            observedAmmunition = session.Ammunition;
        }

        internal void MaintainModalPause(IRunSession session)
        {
            // Launcher owns Escape. If it toggles pause while an abandon confirmation is
            // visible, keep that modal paused before the next physics step.
            if (session != null && pendingCommand != PendingCommand.None && session.Phase == RunPhase.Battle && !session.IsPaused)
                session.SetPaused(true);
        }

        private bool IsModal(IRunSession session) => session.Phase != RunPhase.Battle || session.IsPaused || session.State == BattleState.Lost || pendingCommand != PendingCommand.None;

        internal void PublishBlockingRects(IRunSession session)
        {
            if (session == null) return;
            SyncRunUi(session);
            session.SetUiBlockingRects(DemoUiLayout.BlockingRects(Screen.width, Screen.height, IsModal(session), !string.IsNullOrEmpty(session.Feedback)));
        }

        internal void DrawRun(IRunSession session)
        {
            EnsureStyles();
            EnsureRunStyles();
            SyncRunUi(session);
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;
            try
            {
                GUI.color = Color.white;
                if (session.Phase == RunPhase.Battle)
                {
                    GUI.enabled = previousEnabled && !IsModal(session);
                    DrawRunBattle(session);
                    GUI.enabled = previousEnabled;
                }
                if (IsModal(session))
                {
                    Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.018f, .032f, .045f, .93f));
                    float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
                    GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) * .5f, (Screen.height - 900 * scale) * .5f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
                    if (pendingCommand != PendingCommand.None) DrawConfirmation(session);
                    else if (session.Phase == RunPhase.MainMenu)
                    {
                        if (practiceId != null) DrawPracticeSetup(session); else DrawMainMenu(session);
                    }
                    else if (session.Phase == RunPhase.ActiveSelection) DrawActiveSelection(session);
                    else if (session.Phase == RunPhase.RewardSelection) DrawRewards(session);
                    else if (session.Phase == RunPhase.RunComplete || session.Phase == RunPhase.PracticeComplete) DrawCompletion(session);
                    else DrawPauseOrFailure(session);
                }
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                GUI.enabled = previousEnabled;
                // Commands may change phase or open/close a popup in this very event.
                PublishBlockingRects(session);
            }
        }

        private void EnsureRunStyles()
        {
            if (runHeading != null) return;
            runHeading = Style(40, TextAnchor.MiddleLeft, true);
            runBody = Style(23, TextAnchor.MiddleLeft);
            runCaption = Style(19, TextAnchor.MiddleLeft);
            runCardTitle = Style(29, TextAnchor.MiddleLeft, true);
            runCardBody = Style(21, TextAnchor.UpperLeft);
            runCardBody.wordWrap = true;
            runDetail = Style(17, TextAnchor.UpperLeft);
            runDetail.wordWrap = true;
            runAction = Style(23, TextAnchor.MiddleCenter, true);
            runAction.hover.textColor = Cyan;
            runAction.active.textColor = Orange;
        }

        private bool ActionButton(Rect rect, string label, Color accent, bool enabled = true)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && enabled;
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, new Color(.08f, .13f, .16f, .98f));
            Frame(rect, enabled ? new Color(accent.r, accent.g, accent.b, hover ? 1 : .55f) : new Color(.18f, .25f, .28f));
            Fill(new Rect(rect.x, rect.yMax - 3, hover ? rect.width : 32, 3), enabled ? accent : Muted);
            bool pressed = GUI.Button(rect, label, runAction);
            GUI.enabled = previousEnabled;
            return pressed;
        }

        private void Execute(IRunSession session, Action command, bool clearFeedback = false)
        {
            if (lastCommandFrame == Time.frameCount) return;
            lastCommandFrame = Time.frameCount;
            if (clearFeedback) DemoPresentation.ClearFeedback();
            command();
            PublishBlockingRects(session);
        }

        private void Confirm(IRunSession session, PendingCommand command)
        {
            pendingCommand = command;
            resumeAfterCancel = session.Phase == RunPhase.Battle && !session.IsPaused && session.State != BattleState.Lost;
            if (resumeAfterCancel) session.SetPaused(true);
            PublishBlockingRects(session);
        }

        private void ModalHeader(string eyebrow, string heading, string subtitle)
        {
            Fill(new Rect(100, 52, 1400, 796), Panel);
            Frame(new Rect(100, 52, 1400, 796), new Color(.24f, .34f, .38f));
            Fill(new Rect(100, 52, 4, 100), Orange);
            Text(new Rect(140, 73, 1280, 28), eyebrow, runCaption, Muted);
            Text(new Rect(140, 107, 1280, 60), heading, runHeading, PaleSteel);
            Text(new Rect(140, 174, 1280, 40), subtitle, runBody, Muted);
        }

        private void DrawMainMenu(IRunSession session)
        {
            ModalHeader("FOUNDRY SIEGE  /  FIELD OPERATIONS", "铸造围攻", "选择正式五关挑战，或用独立练习测试能力组合。");
            Fill(new Rect(140, 260, 620, 380), new Color(.065f, .12f, .15f));
            Text(new Rect(170, 282, 550, 70), "01 — 05", runHeading, Cyan);
            Text(new Rect(170, 363, 550, 50), "正式作战 · 五关一轮", runCardTitle, PaleSteel);
            Text(new Rect(170, 425, 550, 100), "开局选择一种主动。每关 3 发；前四关胜利各获一次随机被动三选一。重试本关保留入关构筑。", runCardBody, Muted);
            if (ActionButton(new Rect(170, 550, 560, 62), "开始正式作战  →  选择主动", Cyan)) Execute(session, session.StartCampaign, true);

            Text(new Rect(810, 239, 620, 34), "独立练习  /  可直接选配主动与被动", runCaption, Muted);
            var practices = session.Practices ?? Array.Empty<PracticeDefinition>();
            for (int i = 0; i < 4; i++)
            {
                var definition = i < practices.Length ? practices[i] : null;
                string name = definition == null ? "练习关尚未配置" : definition.levelId + "  /  " + definition.name;
                Rect rect = new Rect(810, 284 + i * 89, 640, 72);
                if (ActionButton(rect, name, i % 2 == 0 ? Cyan : Orange, definition != null)) SelectPractice(definition);
            }
            Text(new Rect(140, 687, 1300, 40), "操作：反向拖拽无人机，松开发射；飞行中再次单击使用主动。Esc 暂停。", runBody, PaleSteel);
            Text(new Rect(140, 744, 1300, 38), "练习不推进正式进度。返回主菜单或关闭游戏后，当前现场不续存。", runCaption, Muted);
            DrawSessionFeedback(session, new Rect(140, 794, 1300, 30));
        }

        private void SelectPractice(PracticeDefinition definition)
        {
            if (definition == null) return;
            practiceId = definition.levelId;
            SetPracticeBuild(definition.build);
        }

        private void SetPracticeBuild(BuildLoadout build)
        {
            practiceActive = build != null && build.active != ActiveAbility.None ? build.active : ActiveAbility.Split;
            practicePassives.Clear();
            if (build?.passives != null)
                foreach (var passive in build.passives) practicePassives.Add(passive);
        }

        private PracticeDefinition FindPractice(IRunSession session)
        {
            foreach (var item in session.Practices ?? Array.Empty<PracticeDefinition>())
                if (item != null && item.levelId == practiceId) return item;
            return null;
        }

        private void DrawPracticeSetup(IRunSession session)
        {
            var definition = FindPractice(session);
            if (definition == null)
            {
                ModalHeader("PRACTICE", "练习配置不可用", "所选关卡已不在配置中，请返回后重新选择。");
                if (ActionButton(new Rect(140, 730, 360, 64), "返回练习列表", Cyan)) practiceId = null;
                return;
            }
            ModalHeader("PRACTICE LOADOUT  /  独立构筑", practiceId + "  /  " + definition.name, "修改只用于本次练习，不影响正式构筑和奖励池。");
            Text(new Rect(140, 220, 1320, 62), definition.instructions ?? "按默认组合练习，或自由选配下方能力。", runCardBody, PaleSteel);
            for (int i = 0; i < 2; i++)
            {
                ActiveAbility ability = i == 0 ? ActiveAbility.Split : ActiveAbility.Dash;
                if (ActionButton(new Rect(140 + i * 670, 294, 650, 62), (practiceActive == ability ? "已选  /  " : "选择  /  ") + DemoAbilityText.Name(ability), practiceActive == ability ? Cyan : Muted)) practiceActive = ability;
            }
            Text(new Rect(140, 365, 1320, 30), "被动配置  /  可多选、可全部取消", runCaption, Muted);
            for (int i = 0; i < AllPassives.Length; i++)
            {
                var ability = AllPassives[i];
                Rect rect = new Rect(140 + i % 4 * 334, 406 + i / 4 * 113, 316, 98);
                bool selected = practicePassives.Contains(ability);
                Fill(rect, new Color(.065f, .11f, .14f));
                Frame(rect, selected ? Cyan : new Color(.2f, .3f, .34f));
                Text(new Rect(rect.x + 13, rect.y + 7, rect.width - 26, 31), (selected ? "✓ " : "+ ") + DemoAbilityText.Name(ability), runBody, selected ? Cyan : PaleSteel);
                Text(new Rect(rect.x + 13, rect.y + 43, rect.width - 26, 48), DemoAbilityText.Description(ability), runDetail, Muted);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    if (!practicePassives.Add(ability)) practicePassives.Remove(ability);
                }
            }
            DrawSessionFeedback(session, new Rect(140, 650, 1310, 34));
            if (ActionButton(new Rect(140, 731, 300, 64), "返回练习列表", Muted)) practiceId = null;
            if (ActionButton(new Rect(460, 731, 340, 64), "恢复本关默认组合", Orange)) SetPracticeBuild(definition.build);
            if (ActionButton(new Rect(820, 731, 640, 64), "以当前构筑进入练习", Cyan))
            {
                var passives = new List<PassiveAbility>(practicePassives); passives.Sort();
                Execute(session, () => session.StartPractice(practiceId, practiceActive, passives.ToArray()), true);
            }
        }

        private void DrawActiveSelection(IRunSession session)
        {
            ModalHeader("CAMPAIGN  /  开局主动二选一", "为本轮选择主动", "每次原始发射只能使用一次。发射松手后，用新的单击发动。");
            for (int i = 0; i < 2; i++)
            {
                ActiveAbility ability = i == 0 ? ActiveAbility.Split : ActiveAbility.Dash;
                Color accent = i == 0 ? Cyan : Orange;
                Rect rect = new Rect(140 + i * 670, 270, 650, 390);
                Fill(rect, new Color(.06f, .105f, .14f)); Frame(rect, accent);
                Text(new Rect(rect.x + 30, rect.y + 24, 590, 44), i == 0 ? "01  /  MULTI VECTOR" : "02  /  VECTOR SHIFT", runCaption, accent);
                Text(new Rect(rect.x + 30, rect.y + 94, 590, 60), DemoAbilityText.Name(ability), runHeading, PaleSteel);
                Text(new Rect(rect.x + 30, rect.y + 170, 590, 115), DemoAbilityText.Description(ability), runCardBody, Muted);
                if (ActionButton(new Rect(rect.x + 30, rect.y + 300, 590, 62), "选择" + DemoAbilityText.Name(ability), accent)) Execute(session, () => session.SelectActive(ability), true);
            }
            Text(new Rect(140, 689, 1310, 34), "仍在空中且主动未使用时，碰撞后也可发动；UI 点击和无效方向不消耗次数。", runCaption, Muted);
            if (ActionButton(new Rect(140, 748, 340, 58), "返回主菜单", Muted)) Confirm(session, PendingCommand.ReturnToMenu);
            DrawSessionFeedback(session, new Rect(510, 750, 940, 46));
        }

        private void DrawRewards(IRunSession session)
        {
            ModalHeader("UPGRADE  /  随机三选一", "第 " + (session.LevelIndex + 1) + " 关完成 · 选择一项被动", "未获得的被动等概率提供三个不同候选；已选项移出本轮池，不刷新、不跳过。");
            var choices = session.RewardChoices ?? Array.Empty<PassiveAbility>();
            for (int i = 0; i < Mathf.Min(choices.Length, 3); i++)
            {
                var ability = choices[i];
                Rect card = new Rect(140 + i * 446, 280, 428, 365);
                Fill(card, new Color(.065f, .115f, .15f)); Frame(card, new Color(.28f, .43f, .48f));
                Text(new Rect(card.x + 24, card.y + 20, 380, 40), "0" + (i + 1) + "  /  PASSIVE", runCaption, Cyan);
                Text(new Rect(card.x + 24, card.y + 86, 380, 56), DemoAbilityText.Name(ability), runCardTitle, PaleSteel);
                Text(new Rect(card.x + 24, card.y + 156, 380, 120), DemoAbilityText.Description(ability), runCardBody, Muted);
                if (ActionButton(new Rect(card.x + 24, card.y + 286, 380, 58), "获得并进入下一关", Cyan, choices.Length == 3)) Execute(session, () => session.SelectReward(ability), true);
            }
            if (choices.Length != 3) Text(new Rect(140, 659, 1310, 40), "奖励候选尚未完整提供，请检查会话配置。", runBody, Orange);
            DrawSessionFeedback(session, new Rect(140, 703, 1310, 35));
            if (ActionButton(new Rect(140, 754, 340, 56), "放弃本轮并返回菜单", Muted)) Confirm(session, PendingCommand.ReturnToMenu);
        }

        private void DrawPauseOrFailure(IRunSession session)
        {
            bool failed = session.State == BattleState.Lost;
            ModalHeader(failed ? "MISSION FAILED  /  入关快照已保留" : "PAUSED  /  物理与能力计时暂停",
                failed ? "弹药耗尽 · 本关未完成" : "战斗已暂停",
                StageLabel(session) + "  /  " + (session.LevelName ?? "") + "  /  " + (failed ? "可保留入关构筑重试。" : "继续将保留当前现场。"));
            DrawOwnedBuild(session, 253);
            Text(new Rect(140, 650, 1310, 44), "重试本关：恢复本关地图、3 发与入关构筑；不额外领奖，不重抽已有候选。", runCaption, Muted);
            float buttonWidth = failed ? 428 : 316;
            float spacing = failed ? 446 : 334;
            int column = 0;
            if (!failed)
            {
                if (ActionButton(new Rect(140, 738, buttonWidth, 65), "继续战斗", Cyan)) Execute(session, () => session.SetPaused(false));
                column++;
            }
            if (ActionButton(new Rect(140 + column++ * spacing, 738, buttonWidth, 65), "重试本关", Cyan)) Execute(session, session.RetryLevel, true);
            if (ActionButton(new Rect(140 + column++ * spacing, 738, buttonWidth, 65), "重开整轮", Orange)) Confirm(session, PendingCommand.RestartRun);
            if (ActionButton(new Rect(140 + column * spacing, 738, buttonWidth, 65), "返回主菜单", Muted)) Confirm(session, PendingCommand.ReturnToMenu);
            DrawSessionFeedback(session, new Rect(140, 695, 1310, 31));
        }

        private void DrawCompletion(IRunSession session)
        {
            bool practice = session.Phase == RunPhase.PracticeComplete;
            ModalHeader(practice ? "PRACTICE COMPLETE" : "CAMPAIGN COMPLETE  /  05 — 05", practice ? "练习完成" : "整轮完成 · 区域肃清",
                practice ? "本次练习已结束；构筑与结果不会进入正式轮。" : "五关全部完成。以下为本轮构筑；最终关不再发放被动奖励。");
            DrawOwnedBuild(session, 253);
            if (practice && ActionButton(new Rect(140, 738, 428, 65), "重试本关", Cyan)) Execute(session, session.RetryLevel, true);
            if (ActionButton(new Rect(practice ? 586 : 140, 738, 428, 65), "重开整轮", Orange)) Confirm(session, PendingCommand.RestartRun);
            if (ActionButton(new Rect(practice ? 1032 : 586, 738, 428, 65), "返回主菜单", Muted)) Confirm(session, PendingCommand.ReturnToMenu);
            DrawSessionFeedback(session, new Rect(140, 675, 1310, 40));
        }

        private void DrawOwnedBuild(IRunSession session, float y)
        {
            var build = session.Build;
            Text(new Rect(140, y, 1300, 43), "本轮主动  /  " + DemoAbilityText.Name(build != null ? build.active : ActiveAbility.None), runCardTitle, Cyan);
            var passives = build?.passives ?? Array.Empty<PassiveAbility>();
            if (passives.Length == 0) Text(new Rect(140, y + 70, 1300, 50), "尚未获得被动。正式关卡胜利后，选择一项加入构筑。", runBody, Muted);
            for (int i = 0; i < Mathf.Min(passives.Length, 7); i++)
            {
                Rect card = new Rect(140 + i % 4 * 334, y + 67 + i / 4 * 123, 316, 108);
                Fill(card, new Color(.06f, .11f, .14f));
                Text(new Rect(card.x + 13, card.y + 9, 290, 34), DemoAbilityText.Name(passives[i]), runBody, PaleSteel);
                Text(new Rect(card.x + 13, card.y + 49, 290, 53), DemoAbilityText.Description(passives[i]), runDetail, Muted);
            }
        }

        private void DrawConfirmation(IRunSession session)
        {
            bool restart = pendingCommand == PendingCommand.RestartRun;
            ModalHeader("CONFIRM  /  放弃进度", restart ? "确认重开整轮？" : "确认返回主菜单？",
                restart ? (session.IsPractice ? "将结束练习，回到正式开局选择主动。" : "清空本轮构筑与五关进度，回到开局主动二选一。") : "将放弃当前轮或练习现场；首版不提供从主菜单恢复。");
            Text(new Rect(140, 315, 1310, 130), restart ? "已获得被动将被清空，后续奖励重新抽取。\n如果只想再试当前关，请取消并选择「重试本关」。" : "当前发数、物体位置与倒塌状态不会保存。\n如果希望保留当前现场，请取消并继续战斗。", runCardBody, PaleSteel);
            if (ActionButton(new Rect(140, 690, 620, 74), "取消 · 保留当前进度", Cyan))
            {
                pendingCommand = PendingCommand.None;
                if (resumeAfterCancel) Execute(session, () => session.SetPaused(false));
                resumeAfterCancel = false;
            }
            if (ActionButton(new Rect(810, 690, 650, 74), restart ? "确认清空并重开整轮" : "确认放弃并返回菜单", Orange))
            {
                pendingCommand = PendingCommand.None;
                Execute(session, restart ? (Action)session.RestartRun : session.ReturnToMenu, true);
            }
        }

        private void DrawSessionFeedback(IRunSession session, Rect rect)
        {
            if (!string.IsNullOrEmpty(session.Feedback)) Text(rect, session.Feedback, runCaption, Orange);
        }

        private static string StageLabel(IRunSession session) => session.IsPractice ? "独立练习  /  " + session.LevelId : "正式 " + (session.LevelIndex + 1).ToString("00") + " / " + session.LevelCount.ToString("00");

        private void DrawRunBattle(IRunSession session)
        {
            float width = Screen.width;
            float titleWidth = Mathf.Min(compact ? 265 : 390, width * .32f);
            Fill(new Rect(16, 16, titleWidth, 76), Panel);
            Fill(new Rect(16, 16, 3, 76), Orange);
            Text(new Rect(32, 19, titleWidth - 30, 20), StageLabel(session), small, Cyan);
            Text(new Rect(32, 40, titleWidth - 30, 30), session.LevelName ?? "工业区作战", title, PaleSteel);
            Text(new Rect(32, 70, titleWidth - 30, 17), session.IsPractice ? "PRACTICE / 练习不推进正式进度" : "CAMPAIGN / 五关连续作战", small, Muted);
            float targetWidth = compact ? 164 : 205;
            var target = new Rect((width - targetWidth) * .5f, 16, targetWidth, 65);
            Fill(target, Panel); Fill(new Rect(target.x, 79, target.width, 2), Cyan);
            Text(new Rect(target.x + 15, 28, target.width - 76, 30), "剩余目标", body, PaleSteel);
            Text(new Rect(target.xMax - 71, 22, 54, 47), Mathf.Max(0, session.TargetsRemaining).ToString("00"), number, Cyan);
            var pause = new Rect(width - 150, 20, 128, 54);
            Fill(pause, Panel); Frame(pause, Muted);
            if (GUI.Button(pause, "暂停 / 菜单", button)) Execute(session, () => session.SetPaused(true));
            if (session.State == BattleState.Preparing || maximumAmmo < session.Ammunition) maximumAmmo = Mathf.Max(0, session.Ammunition);
            DrawBottom(session, width, Screen.height);

            var build = session.Build;
            string summary = "构筑  /  " + DemoAbilityText.Name(build != null ? build.active : ActiveAbility.None);
            foreach (var passive in build?.passives ?? Array.Empty<PassiveAbility>()) summary += " · " + DemoAbilityText.Name(passive);
            var strip = DemoUiLayout.BuildStrip(width);
            Fill(strip, Panel);
            Text(new Rect(strip.x + 11, strip.y, strip.width - 22, strip.height), summary, small, PaleSteel);
            if (GUI.Button(strip, GUIContent.none, GUIStyle.none)) Execute(session, () => session.SetPaused(true));
            DrawMaterialLegend();
            var activeRect = DemoUiLayout.ActivePanel(width);
            Fill(activeRect, Panel); Fill(new Rect(activeRect.x, activeRect.y, 3, activeRect.height), session.CanUseActive ? Cyan : Muted);
            string activeLabel = session.CanUseActive ? "主动可用  1 / 1  ·  单击场景" : activeWasUsed ? "主动已使用  0 / 1" : session.State == BattleState.Ready ? "主动待发射  ·  每发一次" : "主动暂不可用（本发尚未使用）";
            Text(new Rect(activeRect.x + 13, activeRect.y + 7, activeRect.width - 23, 28), activeLabel, body, session.CanUseActive ? Cyan : Muted);
            Text(new Rect(activeRect.x + 13, activeRect.y + 38, activeRect.width - 23, 24), DemoAbilityText.Name(build != null ? build.active : ActiveAbility.None) + "  /  活动弹体 " + session.ActiveProjectiles, small, PaleSteel);
            if (!string.IsNullOrEmpty(session.Feedback))
            {
                Rect toast = DemoUiLayout.FeedbackPanel(width);
                Fill(toast, Panel); Fill(new Rect(toast.x, toast.y, 3, toast.height), Orange);
                Text(new Rect(toast.x + 14, toast.y + 3, toast.width - 28, toast.height - 6), session.Feedback, body, Orange);
            }
        }

        private void DrawMaterialLegend()
        {
            Fill(new Rect(16, 151, 280, 25), Panel);
            Color[] colors = { new Color(.68f, .4f, .2f), new Color(.32f, .83f, .9f), new Color(.62f, .64f, .67f) };
            string[] names = { "木材", "玻璃", "石材" };
            for (int i = 0; i < 3; i++)
            {
                Fill(new Rect(28 + i * 88, 159, 10, 10), colors[i]);
                Text(new Rect(44 + i * 88, 151, 66, 25), names[i], small, Muted);
            }
        }
    }
}
