using System;
using UnityEngine;
using static SiegeDemo.DemoPrimitives;

namespace SiegeDemo
{
    internal sealed class DemoHud
    {
        private readonly Font font;
        private readonly bool ownsFont;
        private GUIStyle small, body, title, number, button, damage;
        private bool compact;
        private int maximumAmmo;
        private float stateTime;
        private static readonly Color Panel = new Color(.025f, .045f, .06f, .95f);
        private static readonly Color Muted = new Color(.52f, .62f, .66f);
        private static readonly Color Success = new Color(.4f, .94f, .72f);
        private static readonly string[] Tiers = { "轻", "中", "重" };

        internal DemoHud()
        {
            // Windows build: Microsoft YaHei supplies the Chinese glyphs. Font data is read
            // from the OS at runtime; no unlicensed system font is copied into Assets.
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Noto Sans CJK SC", "Arial" }, 20);
            ownsFont = font;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stateTime = Time.unscaledTime;
        }

        internal GUIStyle DamageStyle { get { EnsureStyles(); return damage; } }
        internal void StateChanged() { stateTime = Time.unscaledTime; }

        private GUIStyle Style(int size, TextAnchor anchor, bool bold = false)
        {
            var style = new GUIStyle
            {
                font = font, fontSize = size, alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                clipping = TextClipping.Clip, richText = false,
                padding = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private void EnsureStyles()
        {
            bool nextCompact = Screen.width < 1100;
            if (small != null && compact == nextCompact) return;
            compact = nextCompact;
            small = Style(compact ? 11 : 13, TextAnchor.MiddleLeft);
            body = Style(compact ? 14 : 17, TextAnchor.MiddleLeft);
            title = Style(compact ? 18 : 23, TextAnchor.MiddleLeft, true);
            number = Style(32, TextAnchor.MiddleRight, true);
            button = Style(compact ? 14 : 16, TextAnchor.MiddleCenter, true);
            button.hover.textColor = Cyan;
            button.active.textColor = Orange;
            damage = Style(19, TextAnchor.MiddleCenter, true);
        }

        private static void Fill(Rect rect, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Frame(Rect rect, Color tint)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 1), tint);
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), tint);
            Fill(new Rect(rect.x, rect.y, 1, rect.height), tint);
            Fill(new Rect(rect.xMax - 1, rect.y, 1, rect.height), tint);
        }

        private static void Text(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        internal bool Draw(IBattleSession session)
        {
            EnsureStyles();
            Color previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;
            GUI.color = Color.white;
            float width = Screen.width, height = Screen.height;
            float titleWidth = Mathf.Min(compact ? 265 : 390, width * .32f);
            Rect titleRect = new Rect(16, 16, titleWidth, 76);
            Fill(titleRect, Panel);
            Fill(new Rect(16, 16, 3, 76), Orange);
            Text(new Rect(32, 19, titleWidth - 30, 19), "FOUNDRY SIEGE  /  01", small, Muted);
            Text(new Rect(32, 39, titleWidth - 30, 31), "工业区 · 战术试射", title, PaleSteel);
            string levelName = session == null ? "等待战斗连接" : session.LevelName;
            Text(new Rect(32, 69, titleWidth - 30, 18), levelName ?? "测试场", small, Muted);

            float targetWidth = compact ? 164 : 205;
            Rect targetRect = new Rect((width - targetWidth) * .5f, 16, targetWidth, 65);
            Fill(targetRect, Panel);
            Fill(new Rect(targetRect.x, targetRect.yMax - 2, targetWidth, 2), Cyan);
            Text(new Rect(targetRect.x + 15, 28, targetWidth - 76, 30), "剩余目标", body, PaleSteel);
            Text(new Rect(targetRect.xMax - 71, 22, 54, 47), session == null ? "--" : Mathf.Max(0, session.TargetsRemaining).ToString("00"), number, Cyan);

            Rect restart = new Rect(width - 150, 20, 128, 54);
            Fill(restart, Panel);
            Frame(restart, new Color(.4f, .5f, .54f, .7f));
            Fill(new Rect(restart.x, restart.yMax - 3, 28, 3), Orange);
            GUI.enabled = previousEnabled && session != null;
            bool restartClicked = GUI.Button(restart, "↻  重新开始", button);
            GUI.enabled = previousEnabled;

            if (session != null)
            {
                if (session.State == BattleState.Preparing || maximumAmmo < session.Ammunition) maximumAmmo = Mathf.Max(0, session.Ammunition);
                DrawBottom(session, width, height);
                if (session.State == BattleState.Won || session.State == BattleState.Lost) DrawResult(session, width);
            }
            else
            {
                Fill(new Rect(16, height - 92, width - 32, 76), Panel);
                Text(new Rect(32, height - 80, width - 64, 40), "战斗尚未连接，请检查关卡配置。", body, Orange);
            }
            GUI.color = previousColor;
            GUI.enabled = previousEnabled;
            return restartClicked;
        }

        private void DrawBottom(IBattleSession session, float width, float height)
        {
            float y = height - 94;
            Rect bar = new Rect(16, y, width - 32, 78);
            Fill(bar, Panel);
            Fill(new Rect(16, y, width - 32, 1), new Color(.38f, .49f, .53f));
            float ammoWidth = Mathf.Clamp(width * .19f, 150, 280);
            float helpWidth = Mathf.Clamp(width * .3f, 210, 420);
            float centerX = 16 + ammoWidth + 20;
            float helpX = width - 16 - helpWidth;
            Fill(new Rect(centerX - 12, y + 14, 1, 50), new Color(.2f, .28f, .31f));
            Fill(new Rect(helpX - 15, y + 14, 1, 50), new Color(.2f, .28f, .31f));

            Text(new Rect(31, y + 7, ammoWidth - 20, 23), compact ? "冲击型无人机" : "01  /  冲击型无人机", body, PaleSteel);
            int ammo = Mathf.Max(0, session.Ammunition);
            Text(new Rect(31, y + 34, 85, 31), ammo.ToString("00") + " / " + maximumAmmo.ToString("00"), body, Cyan);
            int slots = Mathf.Min(maximumAmmo, Mathf.Clamp(Mathf.FloorToInt((ammoWidth - 105) / (compact ? 11 : 18)), 0, 6));
            for (int i = 0; i < slots; i++)
                Fill(new Rect(121 + i * (compact ? 11 : 18), y + 45, compact ? 7 : 12, 12), i < ammo ? Cyan : new Color(.19f, .27f, .3f));

            var aim = session.Aim;
            int selected = aim.valid && session.State == BattleState.Ready ? Mathf.Clamp(aim.tier, 1, 3) : 0;
            float centerWidth = helpX - centerX - 25;
            float labelWidth = compact ? 42 : 72;
            Text(new Rect(centerX, y + 9, labelWidth, 27), compact ? "档位" : "输出档位", small, Muted);
            float chipWidth = Mathf.Min(compact ? 42 : 58, Mathf.Max(24, (centerWidth - labelWidth - 16) / 3));
            for (int i = 0; i < 3; i++)
            {
                var rect = new Rect(centerX + labelWidth + i * (chipWidth + 5), y + 9, chipWidth, 27);
                Color color = selected == i + 1 ? (selected == 3 ? Orange : Cyan) : new Color(.16f, .23f, .26f);
                Fill(rect, color);
                Text(rect, Tiers[i], button, selected == i + 1 ? Ink : Muted);
            }
            string detail;
            if (selected > 0 && Finite(aim.angle) && Finite(aim.speed)) detail = aim.angle.ToString("0.0") + "°  /  " + aim.speed.ToString("0.00") + " m/s";
            else if (session.State == BattleState.Ready) detail = aim.pullRatio > 0 ? "取消区 · 松手不消耗弹药" : "拖拽瞄准 · 三档固定力度";
            else detail = session.State == BattleState.Resolving ? "无人机与倒塌物继续结算" : "等待下一次指令";
            Text(new Rect(centerX, y + 42, Mathf.Max(0, centerWidth), 25), detail, small, selected == 3 ? Orange : PaleSteel);

            string status = StateLabel(session.State);
            Color statusColor = session.State == BattleState.Won ? Success : session.State == BattleState.Lost || session.State == BattleState.Resolving ? Orange : Cyan;
            Text(new Rect(helpX, y + 7, helpWidth - 12, 26), status, body, statusColor);
            if (session.State == BattleState.Resolving)
            {
                Text(new Rect(helpX, y + 34, helpWidth - 12, 20), "等待稳定后装填；倒塌仍可消灭目标", small, Muted);
                Fill(new Rect(helpX, y + 63, helpWidth - 18, 3), new Color(.16f, .23f, .26f));
                float progress = Finite(session.ResolveProgress) ? Mathf.Clamp01(session.ResolveProgress) : 0;
                Fill(new Rect(helpX, y + 63, (helpWidth - 18) * progress, 3), Orange);
            }
            else
            {
                string help = session.State == BattleState.Ready ? (compact ? "反向拖拽 · 松开发射 · 缩回取消" : "按住无人机向左下拖拽，松开发射；缩回取消")
                    : session.State == BattleState.Preparing ? "结构就位后自动装填第一发" : "点击右上角「重新开始」再次试射";
                Text(new Rect(helpX, y + 38, helpWidth - 12, 29), help, small, Muted);
            }
        }

        private static string StateLabel(BattleState state)
        {
            switch (state)
            {
                case BattleState.Preparing: return "准备中  /  结构稳定";
                case BattleState.Ready: return "待发射  /  拖拽无人机";
                case BattleState.Resolving: return "结算中  /  等待物理稳定";
                case BattleState.Won: return "任务完成  /  目标已清空";
                case BattleState.Lost: return "弹药耗尽  /  试射结束";
                default: return "等待指令";
            }
        }

        private void DrawResult(IBattleSession session, float width)
        {
            bool won = session.State == BattleState.Won;
            Color tint = won ? Success : Orange;
            float panelWidth = Mathf.Min(460, width - 48);
            Rect panel = new Rect((width - panelWidth) * .5f, 116, panelWidth, 84);
            Fill(panel, Panel);
            Fill(new Rect(panel.x, panel.y, 4, panel.height), tint);
            Text(new Rect(panel.x + 21, panel.y + 8, panel.width - 42, 37), won ? "任务完成  /  区域肃清" : "试射结束  /  弹药耗尽", title, tint);
            Text(new Rect(panel.x + 21, panel.y + 46, panel.width - 42, 25), won ? "全部装甲目标已消灭。右上角可重新挑战。" : "还有目标存活。调整角度或冲击承重梁后重试。", small, PaleSteel);
            // A short accent acknowledges a single state transition; no gameplay timing changes.
            float pulse = Mathf.Clamp01(1 - (Time.unscaledTime - stateTime) / .8f);
            Fill(new Rect(panel.x, panel.yMax - 2, panel.width * pulse, 2), tint);
        }

        internal void Dispose() { if (ownsFont) DemoPrimitives.Dispose(font); }
    }
}
