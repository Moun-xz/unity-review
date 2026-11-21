using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 星区选择：一列「战术终端」式的航线表。
    ///
    /// 版面结构：
    ///   Scrim + Grid  →  Sheet(02 · 选择星区)  →  Header / Footer
    ///   正文：顶部航线统计小标签 + 每关一行 UIChrome.Action(Row)
    ///         行内 = 序号 / 星区名 / 波次与敌人种类元信息 / 箭头
    ///   选中行铺 AccentWashStrong 并换强调描边，键盘上下可移动。
    ///
    /// 为什么不再写第二行说明：波次和敌人种类塞进行尾的元信息里，
    /// 行高就能统一到 84，整列读起来是表格而不是四张各写各的卡片。
    /// </summary>
    public class LevelSelectPanel : UIPanelBase
    {
        public Action<int, bool> Chosen;
        public Action Closed;

        private const float SheetW = 1000f;
        private const float RowW = 852f;
        private const float RowH = 84f;
        private const float RowStep = 96f;

        private sealed class Row
        {
            public UIAction Action;
            public int LevelIndex;
            public bool BossOnly;
            public Color Accent;
        }

        private readonly List<Row> rows = new List<Row>();
        private UISheet sheet;
        private int cursor;

        // ================================================================== 构建

        public void Build(LevelConfig[] configs, BossConfig boss)
        {
            if (sheet != null) return;

            int levelCount = configs != null ? configs.Length : 0;
            int rowCount = levelCount + 1;
            float sheetH = Mathf.Clamp(rowCount * RowStep + 290f, 620f, 920f);

            UIChrome.Scrim(transform);
            UIChrome.GridBackdrop(transform, "Grid", 0.05f);

            sheet = UIChrome.Sheet(transform, "Sheet", new Vector2(SheetW, sheetH), Vector2.zero);
            UIChrome.Header(sheet, "01", "选择星区", "SECTOR SELECT");
            UIChrome.Footer(sheet, "↑ ↓ 选择 · 回车进入 · ESC 返回主菜单", "IMAGINARY SECTOR");

            RectTransform content = sheet.Content;
            BuildTopBand(content, rowCount);

            float firstY = (rowCount - 1) * RowStep * 0.5f;

            for (int i = 0; i < levelCount; i++)
            {
                LevelConfig config = configs[i];
                Row row = new Row();
                row.LevelIndex = i;
                row.BossOnly = false;
                row.Accent = UITheme.Cyan;
                row.Action = UIChrome.Action(content, "Row" + Pad(i + 1), Pad(i + 1),
                    SectorName(config, i), DescribeWaves(config),
                    new Vector2(RowW, RowH), new Vector2(0f, firstY - i * RowStep),
                    UIActionStyle.Row, row.Accent);

                StyleRow(row);
                Row captured = row;
                row.Action.Button.onClick.AddListener(delegate { Activate(captured); });
                rows.Add(row);
            }

            Row bossRow = new Row();
            bossRow.LevelIndex = levelCount > 0 ? levelCount - 1 : 0;
            bossRow.BossOnly = true;
            bossRow.Accent = UITheme.Magenta;
            bossRow.Action = UIChrome.Action(content, "Row" + Pad(rowCount), Pad(rowCount),
                boss != null && !string.IsNullOrEmpty(boss.displayName) ? boss.displayName : "守卫者 · WARDEN-01",
                "FINAL BOSS",
                new Vector2(RowW, RowH), new Vector2(0f, firstY - levelCount * RowStep),
                UIActionStyle.Row, bossRow.Accent);

            StyleRow(bossRow);
            Row bossCaptured = bossRow;
            bossRow.Action.Button.onClick.AddListener(delegate { Activate(bossCaptured); });
            rows.Add(bossRow);

            BuildBottomBand(content, configs);

            // 右下角「返回」：鼠标点得到，不必非得记住 ESC（ESC 依然有效，走同一个 Close）
            // 面板高随关卡数变，按钮的 y 按内容区实际半高算，永远贴着内容区底边内缩 40
            float contentHalfH = (sheetH - 140f) * 0.5f;
            UIAction back = UIChrome.Action(content, "BtnBack", "", "返回", "ESC",
                new Vector2(260f, 60f),
                new Vector2(SheetW * 0.5f - UIChrome.PadSide - 130f, 40f - contentHalfH),
                UIActionStyle.Ghost, UITheme.Cyan);
            back.Button.onClick.AddListener(Close);

            InitGroup(false);
            ApplyCursor();
        }

        private void BuildTopBand(RectTransform content, int rowCount)
        {
            UIChrome.Eyebrow(content, "Routes", "AVAILABLE ROUTES",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -6f),
                new Vector2(420f, 20f), TextAnchor.MiddleLeft);

            UIChrome.Eyebrow(content, "RouteCount", Pad(rowCount) + " ROUTES",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f),
                new Vector2(320f, 20f), TextAnchor.MiddleRight);

            UIChrome.Hairline(content, "TopRule", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -30f), SheetW - UIChrome.PadSide * 2f, UITheme.EdgeSoft);
        }

        private void BuildBottomBand(RectTransform content, LevelConfig[] configs)
        {
            int sectors = configs != null ? configs.Length : 0;
            int waves = 0;
            int enemies = 0;

            if (configs != null)
            {
                for (int i = 0; i < configs.Length; i++)
                {
                    if (configs[i] == null) continue;
                    waves += configs[i].waves != null ? configs[i].waves.Length : 0;
                    enemies += configs[i].TotalEnemies;
                }
            }

            // 底部这条统计带收进左侧：右下角这一格要留给「返回」按钮
            UIChrome.Hairline(content, "BottomRule", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 44f), 600f, UITheme.EdgeHair);

            UIChrome.Eyebrow(content, "Totals",
                Pad(sectors) + " SECTORS · " + Pad(waves) + " WAVES · " + Pad(enemies) + " HOSTILES",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 14f),
                new Vector2(640f, 20f), TextAnchor.MiddleLeft, UITheme.TextFaint);
        }

        /// <summary>行内自绘的部件归我们自己管：关掉悬停对文字 / 竖条的覆盖，选中态才不会被鼠标抹掉</summary>
        private static void StyleRow(Row row)
        {
            UIAction action = row.Action;
            action.Fx.label = null;
            action.Fx.index = null;
            action.Fx.accent = null;

            if (action.Meta != null)
            {
                action.Meta.rectTransform.sizeDelta = new Vector2(320f, 22f);
            }
        }

        // ================================================================== 交互

        public void Open()
        {
            cursor = 0;
            ApplyCursor();
            SetVisible(true);
        }

        public void Close()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            SetVisible(false);
            if (Closed != null) Closed();
        }

        protected override void Update()
        {
            base.Update();

            if (group == null || group.alpha <= 0.01f) return;
            if (rows.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                Move(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                Move(1);
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Activate(rows[cursor]);
            }
        }

        private void Move(int delta)
        {
            cursor = (cursor + delta + rows.Count) % rows.Count;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            ApplyCursor();
        }

        private void Activate(Row row)
        {
            if (row == null) return;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            SetVisible(false);
            if (Chosen != null) Chosen(row.LevelIndex, row.BossOnly);
        }

        private void ApplyCursor()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool on = i == cursor;
                UIAction action = row.Action;

                action.Body.color = on ? UITheme.AccentWashStrong : UITheme.A(UITheme.Surface, 0.14f);
                action.Edge.color = on ? UITheme.A(row.Accent, 0.5f) : UITheme.EdgeHair;
                action.Label.color = on ? UITheme.TextMain : UITheme.TextDim;

                if (action.Index != null)
                {
                    action.Index.color = on ? UITheme.A(row.Accent, 0.95f) : UITheme.TextFaint;
                }

                if (action.Chevron != null)
                {
                    action.Chevron.color = on ? UITheme.A(row.Accent, 0.95f) : UITheme.A(row.Accent, 0.5f);
                }
            }
        }

        // ================================================================== 数据描述

        /// <summary>星区名优先取副标题「SECTOR 01 · 掠影带」里的中文部分，没有就退回显示名</summary>
        private static string SectorName(LevelConfig config, int index)
        {
            if (config == null) return "未知星区";

            string subtitle = config.subtitle;
            if (!string.IsNullOrEmpty(subtitle))
            {
                int dot = subtitle.LastIndexOf('·');
                if (dot >= 0 && dot + 1 < subtitle.Length)
                {
                    string name = subtitle.Substring(dot + 1).Trim();
                    if (name.Length > 0) return name;
                }
            }

            return string.IsNullOrEmpty(config.displayName) ? "星区 " + Pad(index + 1) : config.displayName;
        }

        /// <summary>行尾元信息：波次数量 + 出现的敌人种类数，全部按关卡数据算</summary>
        private static string DescribeWaves(LevelConfig config)
        {
            if (config == null || config.waves == null || config.waves.Length == 0) return "NO WAVES";

            bool[] seen = new bool[16];
            int types = 0;

            for (int i = 0; i < config.waves.Length; i++)
            {
                WaveDefinition wave = config.waves[i];
                if (wave == null) continue;

                if (wave.kinds != null && wave.kinds.Length > 0)
                {
                    for (int k = 0; k < wave.kinds.Length; k++)
                    {
                        MarkKind(seen, ref types, wave.kinds[k]);
                    }
                }
                else
                {
                    MarkKind(seen, ref types, wave.kind);
                }
            }

            return Pad(config.waves.Length) + " WAVES · " + Pad(types) + " TYPES";
        }

        private static void MarkKind(bool[] seen, ref int types, EnemyKind kind)
        {
            int id = (int)kind;
            if (id < 0 || id >= seen.Length || seen[id]) return;
            seen[id] = true;
            types++;
        }

        private static string Pad(int value)
        {
            return value < 10 ? "0" + value : value.ToString();
        }
    }
}
