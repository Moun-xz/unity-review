using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 操作说明（v2 战术终端）：一块 920×86" 的 Sheet，做成两段"键位表 + 任务规则0。
    ///
    /// 为什么是键帽 + 说明，而不是一整段话：
    ///   玩家打开这一页只想知道"按哪个键"，键帽是这一页唯一需要"一眼定位"的东西；
    ///   说明文字缩在右侧，扫一眼就够了。全部写成段落的话，四行一挤就没人读。
    ///
    /// 键帽上的字必须是拉丁字符：等宽字体（Consolas 一族）没有汉字，
    /// 中文挤进去会变成方块。所以键帽只写 W / A / S / D / SPACE / ESC，
    /// 中文说明一律放到右边的描述列。
    ///
    /// 这一页不进 GameState 状态机：它只是盖在主菜单上的一层，谁打开谁负责关，
    /// ESC 和「返回」走同一个 Close()。
    /// </summary>
    public class HelpPanel : UIPanelBase
    {
        /// <summary>关闭时回调给打开它的 MenuPanel</summary>
        public Action Closed;

        private const float SheetW = 920f;
        private const float SheetH = 860f;

        private const float LabelX = -330f;     // 左列（中文名）左缘
        private const float KeyX = -180f;       // 键帽列起点
        private const float DescX = 90f;        // 说明列左缘

        /// <summary>内容区半宽：Sheet 去掉左右留白后的一半。所有右边界/文字宽度上限都按它算</summary>
        private const float ContentHalf = (SheetW - UIChrome.PadSide * 2f) * 0.5f;

        /// <summary>
        /// 任务规则正文左缘。行首菱形在 KeyX（-180），正文从这里起才有 566px 可用宽——
        /// 原来从 DescX - 46 起（+30），一行 SizeBody 的说明会顶出内容区右缘。
        /// </summary>
        private const float NoteBodyX = -140f;

        private const float CapH = 34f;
        private const float CapGap = 8f;
        private const float GroupGap = 20f;

        // ================================================================== 构建

        public void Build()
        {
            if (sheetReady) return;

            InitGroup(false);

            UIChrome.Scrim(transform);
            UIChrome.GridBackdrop(transform, "Grid", 0.04f);

            UISheet sheet = UIChrome.Sheet(transform, "Sheet", new Vector2(SheetW, SheetH), Vector2.zero);
            UIChrome.Header(sheet, "03", "操作说明", "BRIEFING");
            UIChrome.Footer(sheet, "ESC 返回主菜单", "FIELD MANUAL · V2");

            RectTransform content = sheet.Content;
            sheetReady = true;

            // ---------------------------------------------------------- 第一段：飞行控制

            UIChrome.Eyebrow(content, "Flight", "飞行操作",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -2f),
                new Vector2(460f, 20f), TextAnchor.MiddleLeft);

            UIChrome.Eyebrow(content, "FlightCount", "4 组按键",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f),
                new Vector2(320f, 20f), TextAnchor.MiddleRight, UITheme.TextFaint);

            UIChrome.Hairline(content, "FlightRule", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -30f), SheetW - UIChrome.PadSide * 2f, UITheme.EdgeSoft);

            MoveRow(content, "移动", 258f, "方向键同样可用");
            KeyRow(content, "射击", 170f, new[] { new[] { "SPACE" }, new[] { "LMB" } },
                "按住不放：连射 / 持续光束");
            KeyRow(content, "换武器", 114f, new[] { new[] { "1", "Q" }, new[] { "2", "E" } },
                "Tab、滚轮也可切换");
            KeyRow(content, "暂停", 62f, new[] { new[] { "ESC" } }, "战斗中随时冻结时间");

            // ---------------------------------------------------------- 第二段：任务规则
            //
            // 这一段整块往下让 36px：上面「移动」改成了两行键帽簇（W 压在 A S D 上方），
            // 飞行段因此高了 33px，不挪的话「任务规则」的标题会贴到「暂停」那行上。
            UIChrome.Eyebrow(content, "Mission", "任务规则",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -366f),
                new Vector2(460f, 20f), TextAnchor.MiddleLeft);

            UIChrome.Eyebrow(content, "MissionCount", "4 条说明",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -366f),
                new Vector2(320f, 20f), TextAnchor.MiddleRight, UITheme.TextFaint);

            UIChrome.Hairline(content, "MissionRule", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -394f), SheetW - UIChrome.PadSide * 2f, UITheme.EdgeHair);

            NoteRow(content, "目标", -60f, "清空当前星区的全部敌机，共两个星区", UITheme.Cyan);
            NoteRow(content, "连击", -128f, "3 秒内连续击毁，每层 +10% 得分，最高 3 倍", UITheme.Amber);
            NoteRow(content, "边界", -196f, "飞出警戒线会持续掉血，把机体拉回场内", UITheme.Danger);
            NoteRow(content, "敌人", -264f, "四种敌人，行为与应对见主菜单「敌人图鉴」", UITheme.Magenta);

            // 底部一条：左提示、右「返回」。提示条原先居中，会压到新加的返回按钮上，所以贴内容左缘摆
            // 436×34 = 文字 380 + 左右各 28、字号 20 + 上下各 7。
            // 原来 460 宽摆在 x=-211，左缘落在 -441，比内容区左边界（-426）还出去 15px，
            // 整块 chip 顶在面板边框上；宽度收一档后按内容左缘对齐（-426 + 218）。
            UIChrome.Tag(content, "Tip", "看不清敌机时先拉开距离，别在弹幕里贴脸",
                new Vector2(436f, 34f), new Vector2(-208f, -320f), UITheme.Cyan);

            // 右下角「返回」：鼠标点得到，不必非得记住 ESC（ESC 依然有效，走同一个 Close）
            UIAction back = UIChrome.Action(content, "BtnBack", "", "返回", "ESC",
                new Vector2(260f, 60f), new Vector2(296f, -320f), UIActionStyle.Ghost, UITheme.Cyan);
            back.Button.onClick.AddListener(Close);
        }

        private bool sheetReady;

        // ================================================================== 构件

        /// <summary>
        /// 一行键位：左中文名 + 中间键帽序列 + 右说明。
        ///
        /// 键帽按**组**传（`groups`），组与组之间插一道极淡的竖线：
        ///   「1 Q | 2 E」读作两把武器各有两种按法；
        ///   「SPACE | LMB」读作键盘和鼠标都能开火。
        /// 早先用的是"按键数判断要不要插分隔线"（`keys.Length > 2`），
        /// 结果只有换武器那一行分得开，射击行两枚键帽连排、看着像组合键。
        ///
        /// 键帽宽按字数推：等宽字体每个字符宽度一样，按 12px/字加左右各 10px 内边距，
        /// 不用去测 Text.preferredWidth（那要等布局跑完，Build 里拿不到）。
        /// 键帽上的字不拉字距——键面应当紧凑，SPACE 拉开成「S P A C E」会顶出胶囊。
        /// </summary>
        private void KeyRow(Transform parent, string label, float y, string[][] groups, string description)
        {
            DrawLabel(parent, label, y);

            float x = KeyX;

            for (int g = 0; g < groups.Length; g++)
            {
                if (g > 0)
                {
                    // 锚点必须和键帽同一套（0.5, 0.5）：原来这里写的是 (0, 0.5)，即相对父物体**左缘**
                    // 定位，而 x 是按内容中心算的，两者差着半块面板，那条线其实一直在面板外面——
                    // 截图里看得见的竖线是背景网格，不是它。
                    Image sep = UIFactory.CreateImage(parent, "Sep_" + label + g, UITheme.EdgeHair);
                    UIFactory.Place(sep.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(x + GroupGap * 0.5f, y), new Vector2(1f, 20f));
                    x += GroupGap;
                }

                for (int i = 0; i < groups[g].Length; i++)
                {
                    string key = groups[g][i];
                    float w = CapWidth(key);

                    DrawCap(parent, label + g + "_" + i, key, x, y, w);
                    x += w + CapGap;
                }

                x -= CapGap;   // 组内已加过一个间距，组尾要退回来，否则组间会多 6px
            }

            DrawDesc(parent, label, description, y);
        }

        /// <summary>
        /// 「移动」这一行单独做，因为 W A S D 不是一串四个键，是四个**方向**。
        ///
        /// 平铺成「W A S D」一排，读起来像"依次按下这四个键"；照键盘实际位置摆成
        /// W 压在 A S D 上方中间，一眼就是"上下左右"。这是整页唯一需要二维排布的键位，
        /// 所以单独开一个方法，而不是把 KeyRow 改成能接不规则布局。
        /// </summary>
        private void MoveRow(Transform parent, string label, float y, string description)
        {
            DrawLabel(parent, label, y);

            const float key = 36f;
            float step = key + CapGap;
            float half = (CapH + CapGap) * 0.5f;   // 上下两排的中心各偏 half
            float colMid = KeyX + step;            // 中间那一列 = S 的左缘，也是 W 的左缘

            DrawCap(parent, "W", "W", colMid, y + half, key);
            DrawCap(parent, "A", "A", KeyX, y - half, key);
            DrawCap(parent, "S", "S", colMid, y - half, key);
            DrawCap(parent, "D", "D", KeyX + step * 2f, y - half, key);

            DrawDesc(parent, label, description, y);
        }

        /// <summary>
        /// 一枚键帽。四件东西：切角底、描边、等宽字，外加顶亮/底暗两道 1px 线。
        ///
        /// 那两道线是让键帽"立起来"的全部成本：只有底和描边时，它是一块贴在面板上的色斑；
        /// 加上顶边一道冷色高光、底边一道暗线，看起来才是"有厚度、按得下去"的键。
        /// </summary>
        private void DrawCap(Transform parent, string name, string key, float x, float y, float w)
        {
            Image cap = UIFactory.CreateImage(parent, "Cap_" + name, UITheme.A(UITheme.SurfaceRaised, 0.95f));
            cap.sprite = UISprites.Sheet(UITheme.CutChip);
            cap.type = Image.Type.Sliced;
            UIFactory.Place(cap.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(x, y), new Vector2(w, CapH));

            Image edge = UIFactory.CreateImage(cap.transform, "Edge", UITheme.EdgeSoft);
            edge.sprite = UISprites.SheetEdge(UITheme.CutChip, 1f);
            edge.type = Image.Type.Sliced;
            UIFactory.Stretch(edge.rectTransform);

            // 顶边高光：贴在上缘内侧、左右各让 6px，避开切角，不然会从斜角里探出来
            Image top = UIFactory.CreateImage(cap.transform, "TopLight", UITheme.A(UITheme.Cyan, 0.20f));
            RectTransform tr = top.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.offsetMin = new Vector2(7f, -3f);
            tr.offsetMax = new Vector2(-7f, -1f);

            // 底边暗线：键帽与面板之间的一道"缝"
            Image bottom = UIFactory.CreateImage(cap.transform, "BottomShade", new Color(0f, 0f, 0f, 0.42f));
            RectTransform br = bottom.rectTransform;
            br.anchorMin = new Vector2(0f, 0f);
            br.anchorMax = new Vector2(1f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.offsetMin = new Vector2(7f, 1f);
            br.offsetMax = new Vector2(-7f, 3f);

            Text text = UIFactory.CreateTextMono(cap.transform, "Key", key,
                UITheme.SizeCaption, TextAnchor.MiddleCenter, UITheme.A(UITheme.Cyan, 0.95f), true);
            UIFactory.Stretch(text.rectTransform);
        }

        private void DrawLabel(Transform parent, string label, float y)
        {
            UIChrome.CreateLabel(parent, label + "Row", label, UITheme.SizeH3,
                TextAnchor.MiddleLeft, UITheme.TextMain,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LabelX, y), new Vector2(150f, 34f));
        }

        private void DrawDesc(Transform parent, string label, string description, float y)
        {
            UIChrome.CreateLabel(parent, label + "Desc", description, UITheme.SizeLabel,
                TextAnchor.MiddleLeft, UITheme.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(DescX, y), new Vector2(ContentHalf - DescX, 26f));
        }

        /// <summary>
        /// 一行任务规则：左中文名（暗一档）+ 右侧一条完整说明。
        /// 说明用 SizeBody 而不是 SizeCaption：这一段的文字是要被读的，
        /// 不像键位表那样只需要"扫"。
        /// </summary>
        private void NoteRow(Transform parent, string label, float y, string body, Color accent)
        {
            UIChrome.CreateLabel(parent, label + "Row", label, UITheme.SizeLabel,
                TextAnchor.MiddleLeft, UITheme.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LabelX, y), new Vector2(150f, 30f));

            // 行首标记改成小菱形（与 UIChrome.Rule 的端点同一形状语言）：
            // 原来是一根 2×16 的竖线，孤零零浮在中间，读起来像误画的游标。
            Image tick = UIFactory.CreateImage(parent, label + "Tick", UITheme.A(accent, 0.75f));
            UIFactory.Place(tick.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(KeyX, y), new Vector2(7f, 7f));
            tick.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

            UIChrome.CreateLabel(parent, label + "Body", body, UITheme.SizeBody,
                TextAnchor.MiddleLeft, UITheme.TextMain,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(NoteBodyX, y), new Vector2(ContentHalf - NoteBodyX - 10f, 32f));
        }

        /// <summary>
        /// 键帽宽度：20px 粗体等宽字约 12px/字，左右各留 11px 内边距，最短 36px（单字符键接近方形）。
        /// 多字符键名自然变宽（SPACE 是一枚横向长键帽），和真实键盘上的空格键一致；
        /// 不会再出现「字排得开、框还是单字宽」的溢出。
        /// </summary>
        private static float CapWidth(string key)
        {
            int len = key != null ? key.Length : 0;
            return Mathf.Max(36f, 22f + 12f * len);
        }

        // ================================================================== 交互

        public void Open()
        {
            SetVisible(true);
        }

        public void Close()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            // 先关自己再回调：反过来会让 MenuPanel 在面板还开着的时候重开菜单，
            // 遮罩把点击全吃掉（机库返回按钮踩过这个坑）
            SetVisible(false);
            if (Closed != null) Closed();
        }

        protected override void Update()
        {
            base.Update();

            if (group == null || group.alpha <= 0.01f) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }
    }
}
