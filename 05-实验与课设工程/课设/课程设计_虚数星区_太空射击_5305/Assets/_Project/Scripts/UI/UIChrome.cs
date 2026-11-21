using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    public enum UICorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public enum UIActionStyle
    {
        /// <summary>列表行：左序号 + 标题 + 右侧说明 + 箭头，悬停铺底</summary>
        Row,

        /// <summary>主按钮：切角实心，居中粗体</summary>
        Primary,

        /// <summary>次按钮：切角描边，居中常规</summary>
        Ghost
    }

    /// <summary>一块切角面板的各个部件，改动配色时不用再重新找子物体</summary>
    public sealed class UISheet
    {
        public RectTransform Root;
        public RectTransform Content;
        public RectTransform Header;
        public Image Body;
        public Image Edge;
        public Image Glow;
        public Image Shadow;
        public Image HeaderRule;
        public Image FooterRule;
        /// <summary>顶边那两段强调线（长的一段、短的品红段），改选中态时要用</summary>
        public Image TopLine;
        public Image TopLine2;
        /// <summary>四角取景括号的 4 个 Image，顺序是 先角一的横、竖，再角二的横、竖</summary>
        public Image[] Brackets;
        public float Cut;
        public Color Accent;
    }

    /// <summary>列表行 / 按钮的各个部件</summary>
    public sealed class UIAction
    {
        public RectTransform Root;
        public Button Button;
        public Image Body;
        public Image Edge;
        public Image Fill;
        public Image Bar;
        public Text Index;
        public Text Label;
        public Text Meta;
        public Text Chevron;
        public UIButtonFx Fx;
    }

    /// <summary>小标签（"已装备""Boss""新"之类）</summary>
    public sealed class UITag
    {
        public RectTransform Root;
        public Image Body;
        public Image Edge;
        public Text Text;
    }

    /// <summary>
    /// 界面骨架：把"一块面板该长什么样"收在一个地方。
    ///
    /// 上一版每个面板都是「一大块半透明底 + 四条边 + 居中的标题」，所以十几个界面看起来是同一个模子。
    /// 这一版统一成"切角玻璃板 + 顶部信息栏 + 底部提示条 + 不对称取景框"：
    ///   · 顶部信息栏带序号徽章、标题、右侧元信息，用一条细线隔开 —— 界面立刻有了"仪表盘"的读法
    ///   · 底部提示条把操作说明从正文里挪出来，正文就不用为了写按键留空
    ///   · 取景框只画对角两个角（左下 + 右上），对称反而显得呆板
    ///   · 面板底色是四档里的 Surface，浮起的卡片用 SurfaceRaised，越靠前越亮
    ///
    /// 尺寸约定：面板内容区 = 面板减去四周内边距与顶部信息栏，可以直接用 sheet.Content 做锚点。
    /// </summary>
    public static class UIChrome
    {
        public const float HeaderHeight = 68f;
        public const float PadSide = 34f;
        public const float PadBottom = 30f;

        // ================================================================== 面板

        public static UISheet Sheet(Transform parent, string name, Vector2 size, Vector2 position,
                                    Color? accent = null, float cut = UITheme.CutLarge, bool glow = true)
        {
            return SheetAt(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           position, size, accent, cut, glow);
        }

        public static UISheet SheetAt(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                      Vector2 position, Vector2 size,
                                      Color? accent = null, float cut = UITheme.CutLarge, bool glow = true)
        {
            Color a = accent ?? UITheme.Cyan;

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Place(go, anchor, pivot, position, size);

            UISheet sheet = new UISheet();
            sheet.Root = go.GetComponent<RectTransform>();
            sheet.Cut = cut;
            sheet.Accent = a;

            if (glow)
            {
                // 外发光：一层很淡的径向光，让面板"浮"在星空上而不是贴在上面
                Image g = StretchImage(sheet.Root, "Glow", UITheme.A(a, 0.075f), SpriteFactory.Glow(), Image.Type.Simple);
                g.rectTransform.offsetMin = new Vector2(-90f, -90f);
                g.rectTransform.offsetMax = new Vector2(90f, 90f);
                sheet.Glow = g;
            }

            // 投影：切角形状的一份深色副本，向下偏 13px
            Image shadow = StretchImage(sheet.Root, "Shadow", new Color(0f, 0f, 0f, 0.42f), UISprites.Sheet(cut), Image.Type.Sliced);
            shadow.rectTransform.offsetMin = new Vector2(-8f, -22f);
            shadow.rectTransform.offsetMax = new Vector2(8f, -4f);
            sheet.Shadow = shadow;

            // 面板主体
            // 面板底板几乎不透明：主题色是 Surface，但 alpha 单独抬到 0.985。
            // 半透明底板会让背后的行星/星云透进来，字压在上面像隔着毛玻璃，
            // 「战术终端」要的是一块实心的深色屏。
            Image body = StretchImage(sheet.Root, "Body",
                new Color(UITheme.Surface.r, UITheme.Surface.g, UITheme.Surface.b, 0.985f),
                UISprites.Sheet(cut), Image.Type.Sliced);
            sheet.Body = body;

            // 顶部微亮：竖向渐变，从顶到底逐渐变暗，玻璃的厚度感全靠这层
            Image sheen = StretchImage(body.transform, "Sheen", new Color(1f, 1f, 1f, 0.055f), UISprites.Gradient(), Image.Type.Simple);
            sheen.rectTransform.offsetMin = new Vector2(cut * 0.75f, cut * 0.75f);
            sheen.rectTransform.offsetMax = new Vector2(-cut * 0.75f, -cut * 0.75f);

            // 描边环
            Image edge = StretchImage(sheet.Root, "Edge", UITheme.Edge, UISprites.SheetEdge(cut, 1.15f), Image.Type.Sliced);
            sheet.Edge = edge;

            // 顶边两段强调线：一长一短、一青一品红，这是整套界面最"赛博"的一笔
            Image topLine = UIFactory.CreateImage(sheet.Root, "TopLine", UITheme.A(a, 0.8f));
            UIFactory.Place(topLine.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(cut + 4f, -1.5f), new Vector2(96f, 2f));

            Image topLine2 = UIFactory.CreateImage(sheet.Root, "TopLine2", UITheme.A(UITheme.Magenta, 0.55f));
            UIFactory.Place(topLine2.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-(cut + 4f), -1.5f), new Vector2(38f, 2f));

            sheet.TopLine = topLine;
            sheet.TopLine2 = topLine2;

            // 顶部信息栏容器
            RectTransform header = UIFactory.NewUI(sheet.Root, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, HeaderHeight);
            sheet.Header = header;

            // 内容区
            RectTransform content = UIFactory.NewUI(sheet.Root, "Content");
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.offsetMin = new Vector2(PadSide, PadBottom);
            content.offsetMax = new Vector2(-PadSide, -(HeaderHeight + 6f));
            sheet.Content = content;

            sheet.Brackets = CornerBrackets(sheet.Root, UICorner.BottomLeft, UICorner.TopRight,
                UITheme.A(a, 0.5f), 26f, 2f, -3f);

            return sheet;
        }

        /// <summary>画面板顶部信息栏：序号徽章 + 标题 + 右侧元信息 + 分隔细线</summary>
        public static void Header(UISheet sheet, string index, string title, string meta, Color? accent = null)
        {
            if (sheet == null || sheet.Header == null) return;

            Color a = accent ?? sheet.Accent;
            RectTransform header = sheet.Header;

            if (!string.IsNullOrEmpty(index))
            {
                Image badgeBody = UIFactory.CreateImage(header, "IndexBadge", Color.white);
                badgeBody.sprite = UISprites.Sheet(UITheme.CutChip);
                badgeBody.type = Image.Type.Sliced;
                badgeBody.color = UITheme.A(a, 0.16f);
                UIFactory.Place(badgeBody.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(PadSide, 0f), new Vector2(42f, 26f));

                StretchImage(badgeBody.transform, "Edge", UITheme.A(a, 0.55f),
                    UISprites.SheetEdge(UITheme.CutChip, 1f), Image.Type.Sliced);

                Text badge = UIFactory.CreateTextMono(badgeBody.transform, "Index", index,
                    UITheme.SizeMicro, TextAnchor.MiddleCenter, UITheme.A(a, 0.95f), true);
                UIFactory.Stretch(badge.rectTransform);
            }

            float titleX = string.IsNullOrEmpty(index) ? PadSide : PadSide + 54f;

            Text heading = UIFactory.CreateText(header, "Title", title, UITheme.SizeH3,
                TextAnchor.MiddleLeft, UITheme.TextMain, true);
            UIFactory.Place(heading.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(titleX, 1f), new Vector2(560f, 40f));

            if (!string.IsNullOrEmpty(meta))
            {
                Text metaText = UIFactory.CreateTextMono(header, "Meta", meta,
                    UITheme.SizeLabel, TextAnchor.MiddleRight, UITheme.TextDim, true);
                UIFactory.Place(metaText.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-PadSide, 1f), new Vector2(520f, 28f));
            }

            Image rule = UIFactory.CreateImage(header, "Rule", UITheme.EdgeSoft);
            RectTransform ruleRect = rule.rectTransform;
            ruleRect.anchorMin = new Vector2(0f, 0f);
            ruleRect.anchorMax = new Vector2(1f, 0f);
            ruleRect.pivot = new Vector2(0.5f, 0.5f);
            ruleRect.offsetMin = new Vector2(PadSide, 0f);
            ruleRect.offsetMax = new Vector2(-PadSide, 1f);
            sheet.HeaderRule = rule;

            Image tick = UIFactory.CreateImage(header, "RuleTick", UITheme.A(a, 0.85f));
            UIFactory.Place(tick.gameObject, new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                new Vector2(PadSide, 0f), new Vector2(64f, 2f));

            // 页眉右侧空出来给 meta 用：原先这里还有一行 "// 0X" 的系统标识，
            // 它挂在页眉下缘以外，正好和每个面板顶部那排 eyebrow（"SECTOR SELECT" 之类）撞在一起，
            // 叠出"03 RO//01 UTS"这种乱码。序号已经由左边徽章表达，这行删掉更干净。
        }

        /// <summary>底部提示条：左侧操作说明、右侧版本/署名，并把内容区底部让开</summary>
        public static void Footer(UISheet sheet, string left, string right)
        {
            if (sheet == null || sheet.Root == null) return;

            RectTransform content = sheet.Content;
            if (content != null) content.offsetMin = new Vector2(content.offsetMin.x, 66f);

            Image rule = UIFactory.CreateImage(sheet.Root, "FooterRule", UITheme.EdgeHair);
            RectTransform ruleRect = rule.rectTransform;
            ruleRect.anchorMin = new Vector2(0f, 0f);
            ruleRect.anchorMax = new Vector2(1f, 0f);
            ruleRect.pivot = new Vector2(0.5f, 0.5f);
            ruleRect.offsetMin = new Vector2(PadSide, 56f);
            ruleRect.offsetMax = new Vector2(-PadSide, 57f);
            sheet.FooterRule = rule;

            if (!string.IsNullOrEmpty(left))
            {
                Text l = UIFactory.CreateText(sheet.Root, "FooterLeft", left, UITheme.SizeCaption,
                    TextAnchor.MiddleLeft, UITheme.TextDim);
                UIFactory.Place(l.gameObject, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(PadSide, 30f), new Vector2(760f, 26f));
            }

            if (!string.IsNullOrEmpty(right))
            {
                Text r = UIFactory.CreateTextMono(sheet.Root, "FooterRight", right,
                    UITheme.SizeCaption, TextAnchor.MiddleRight, UITheme.TextDim, true);
                UIFactory.Place(r.gameObject, new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-PadSide, 30f), new Vector2(560f, 26f));
            }
        }

        // ================================================================== 按钮 / 行

        public static UIAction Action(Transform parent, string name, string index, string label, string meta,
                                      Vector2 size, Vector2 position, UIActionStyle style = UIActionStyle.Row,
                                      Color? accent = null)
        {
            Color a = accent ?? UITheme.Cyan;

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

            UIAction action = new UIAction();
            action.Root = go.GetComponent<RectTransform>();

            // 切角 10px 在 264×58 的按钮上几乎看不出来，读起来只是"圆角矩形被啃了一小口"。
            // 14 才和面板的 CutCard(12) 成比例，斜切这条线也才承担得起"机械面板"的语义。
            float cut = style == UIActionStyle.Row ? 8f : 14f;

            Image body = go.AddComponent<Image>();
            body.sprite = UISprites.Sheet(cut);
            body.type = Image.Type.Sliced;
            body.raycastTarget = true;
            body.color = style == UIActionStyle.Row ? UITheme.A(UITheme.Surface, 0.14f)
                         : style == UIActionStyle.Primary ? UITheme.AccentWashStrong
                         : UITheme.A(UITheme.Surface, 0.30f);
            action.Body = body;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;
            action.Button = button;

            // 悬停铺底：单独一层，alpha 从 0 插值上来
            Image fill = UIFactory.CreateImage(go.transform, "Fill", UITheme.A(a, 0f));
            fill.sprite = UISprites.Sheet(cut);
            fill.type = Image.Type.Sliced;
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1.5f, 1.5f);
            fillRect.offsetMax = new Vector2(-1.5f, -1.5f);
            action.Fill = fill;

            Image edge = StretchImage(go.transform, "Edge", UITheme.EdgeSoft, UISprites.SheetEdge(cut, 1f), Image.Type.Sliced);
            action.Edge = edge;

            if (style == UIActionStyle.Row)
            {
                edge.color = UITheme.EdgeHair;

                // 行底线：比描边更实一点，让一列行读成"表格"
                Image underline = UIFactory.CreateImage(go.transform, "Underline", UITheme.EdgeHair);
                RectTransform u = underline.rectTransform;
                u.anchorMin = new Vector2(0f, 0f);
                u.anchorMax = new Vector2(1f, 0f);
                u.pivot = new Vector2(0.5f, 0.5f);
                u.offsetMin = new Vector2(10f, 0f);
                u.offsetMax = new Vector2(-10f, 1f);

                Image bar = UIFactory.CreateImage(go.transform, "Bar", a);
                RectTransform barRect = bar.rectTransform;
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(0f, 1f);
                barRect.pivot = new Vector2(0f, 0.5f);
                barRect.anchoredPosition = new Vector2(0f, 0f);
                barRect.sizeDelta = new Vector2(2f, -18f);
                action.Bar = bar;

                if (!string.IsNullOrEmpty(index))
                {
                    action.Index = UIFactory.CreateTextMono(go.transform, "Index", index, UITheme.SizeMicro,
                        TextAnchor.MiddleLeft, UITheme.TextFaint);
                    UIFactory.Place(action.Index.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(18f, 0f), new Vector2(36f, 24f));
                }

                action.Label = UIFactory.CreateText(go.transform, "Label", label, UITheme.SizeH3,
                    TextAnchor.MiddleLeft, UITheme.TextDim);
                UIFactory.Place(action.Label.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(60f, 0f), new Vector2(size.x - 60f - 64f, 40f));

                if (!string.IsNullOrEmpty(meta))
                {
                    action.Meta = UIFactory.CreateTextMono(go.transform, "Meta", meta,
                        UITheme.SizeMicro, TextAnchor.MiddleRight, UITheme.TextDim, true);
                    UIFactory.Place(action.Meta.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-46f, 0f), new Vector2(180f, 22f));
                }

                action.Chevron = UIFactory.CreateText(go.transform, "Chevron", "›", UITheme.SizeH3,
                    TextAnchor.MiddleRight, UITheme.A(a, 0.5f));
                UIFactory.Place(action.Chevron.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-14f, 0f), new Vector2(28f, 32f));
            }
            else
            {
                bool primary = style == UIActionStyle.Primary;

                // ---- 底：主按钮是一块"板"，Ghost 什么都不铺 ----
                // 上一版两者用同一个切角框、只差填充透明度。色相相同、明度也几乎相同，
                // 结果是一圈亮边包着两块和背景分不开的空——读起来像"两个对话框"，
                // 主操作没有重量。现在把它们拆成两种东西：一块抬起来的板 / 一行悬着的字。
                body.color = primary ? UITheme.BtnPrimaryFill : UITheme.A(UITheme.Surface, 0f);

                // ---- 描边：主按钮 1px 亮青；Ghost 平时无框 ----
                // Ghost 再画一圈框必然和主按钮撞款——两个都在喊"我是按钮"，主次就没了。
                // 它的边留给 UIButtonFx 在悬停时拉起来；这里必须把 RGB 设成青色、
                // 只把 alpha 归零，否则悬停浮起来的会是一圈黑边。
                edge.color = primary ? UITheme.A(a, 0.38f) : UITheme.A(a, 0f);

                // ---- 内部顶光：让实底不是一整片死色 ----
                // 6% 的径向光铺在偏上位置，读起来像"板面被上方光源打亮"。
                // 看不见，但去掉之后按钮会重新变成一块塑料片。
                // 顺序在厚度线之前：光在下、受光线在上，线才不会被雾蒙掉。
                if (primary)
                    InnerGlow(go.transform, "Sheen", Color.white,
                        new Vector2(size.x * 0.85f, size.y * 1.5f), new Vector2(-14f, 8f), 0.06f);

                // ---- 厚度：顶沿受光、底沿积影（和卡片同一套手法）----
                // 少这两条，方块只是一块填了色的矩形；有了它才读得出"板浮在面板上"。
                if (primary) CardShade(action.Root, cut + 4f);

                // ---- 左侧竖条：只有主按钮有 ----
                // Ghost 的文字是居中的，贴一条左缘竖线会离字很远，读成两件不相干的东西。
                // 主按钮的条不一样：它的文字和条被同一块实底圈在一起，条是"封边"。
                if (primary)
                {
                    Image bar = UIFactory.CreateImage(go.transform, "Bar", a);
                    RectTransform barRect = bar.rectTransform;
                    barRect.anchorMin = new Vector2(0f, 0.5f);
                    barRect.anchorMax = new Vector2(0f, 0.5f);
                    barRect.pivot = new Vector2(0f, 0.5f);
                    barRect.anchoredPosition = Vector2.zero;
                    // 高度扣掉两个切角，竖条才不会从斜切边上探出一截小角
                    barRect.sizeDelta = new Vector2(3f, size.y - cut * 2f);
                    action.Bar = bar;
                }

                // 主按钮的字走到近白，不再用青色：标题、副行、键帽已经全是青的，
                // 字再青一遍就和面板糊成一个色相，按钮立不住。身份交给描边和左条，
                // 字只管读得清。
                action.Label = UIFactory.CreateText(go.transform, "Label", label,
                    primary ? UITheme.SizeH3 : UITheme.SizeBody,
                    TextAnchor.MiddleCenter,
                    primary ? UITheme.TextMain : UITheme.TextDim, primary);
                // 文字框左右各扣掉「键帽 48 + 间距 13」，文字最终仍落在按钮中心
                UIFactory.Place(action.Label.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(size.x - 122f, 44f));

                // ---- 键帽：把 "ESC" 从第二行小字改成一枚真正的键 ----
                if (!string.IsNullOrEmpty(meta))
                    action.Meta = KeyCap(go.transform, meta, primary, a);
            }

            UIButtonFx fx = go.AddComponent<UIButtonFx>();
            fx.fill = fill;
            fx.edge = edge;
            fx.accent = action.Bar;
            fx.label = action.Label;
            fx.index = action.Index;
            fx.chevron = action.Chevron == null ? null : action.Chevron.rectTransform;
            fx.fillAlpha = style == UIActionStyle.Primary ? 0.16f : 0.085f;
            fx.chevronSlide = 6f;

            if (style == UIActionStyle.Row)
            {
                fx.idleWidth = 2f;
                fx.hoverWidth = 5f;
            }
            else
            {
                // 主按钮的竖条本来就有 3px，悬停拉到 6 才有"整条亮起来"的幅度；
                // Ghost 的 2px 拉到 5，幅度小一些，免得一行字忽然变粗抢戏。
                bool primary = style == UIActionStyle.Primary;
                fx.idleWidth = primary ? 3f : 2f;
                fx.hoverWidth = primary ? 6f : 5f;
                fx.idleColor = primary ? UITheme.TextMain : UITheme.TextDim;
                fx.hoverColor = primary ? Color.white : UITheme.TextMain;

                // 主按钮：描边从 38% 拉到 68%，悬停像"整块亮起来"；
                // Ghost：平时没有边，悬停才浮出 16%，只是"可以点"的暗示。
                fx.idleEdgeAlpha = primary ? 0.38f : 0f;
                fx.hoverEdgeAlpha = primary ? 0.68f : 0.16f;
            }

            action.Fx = fx;

            return action;
        }

        /// <summary>
        /// 键帽：右端一枚小方块，里面是等宽键名。
        ///
        /// 键位提示原来被塞成主文字下面的一行 11px 小字——等于让按钮里出现两行文字，
        /// 主字被顶高、下面空出 20px，重心永远压在上半截。
        /// 键位本来就是一枚键：给它一个键的轮廓摆在右端，按钮回到"一行字 + 一个附件"的简单结构。
        /// </summary>
        private static Text KeyCap(Transform parent, string key, bool primary, Color accent)
        {
            GameObject cap = new GameObject("KeyCap", typeof(RectTransform));
            cap.transform.SetParent(parent, false);
            // 48×26：键名 "ESC" 在等宽 16 号下是 27px 宽，塞进原来的 42 宽只剩左右各 7.5px，
            // 键面被字撑满就失去"一枚键"的轮廓。48 给到 10.5px，和 TagPadX 的比例一致。
            UIFactory.Place(cap, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-13f, 0f), new Vector2(48f, 26f));

            Image bg = cap.AddComponent<Image>();
            bg.sprite = UISprites.Sheet(UITheme.CutChip);
            bg.type = Image.Type.Sliced;
            bg.color = primary ? UITheme.BtnKeyCap : UITheme.A(UITheme.SurfaceDeep, 0.52f);
            bg.raycastTarget = false;

            Image capEdge = UIFactory.CreateImage(cap.transform, "Edge",
                primary ? UITheme.A(accent, 0.32f) : UITheme.A(accent, 0.13f));
            capEdge.sprite = UISprites.SheetEdge(UITheme.CutChip, 1f);
            capEdge.type = Image.Type.Sliced;
            UIFactory.Stretch(capEdge.rectTransform);
            capEdge.raycastTarget = false;

            Text text = UIFactory.CreateTextMono(cap.transform, "Meta", key, UITheme.SizeNano,
                TextAnchor.MiddleCenter,
                primary ? UITheme.A(UITheme.CyanBright, 0.88f) : UITheme.A(UITheme.TextDim, 0.70f), true);
            UIFactory.Stretch(text.rectTransform);
            text.raycastTarget = false;
            return text;
        }

        /// <summary>小标签</summary>
        public static UITag Tag(Transform parent, string name, string text, Vector2 size, Vector2 position,
                                Color? accent = null, bool filled = false)
        {
            Color a = accent ?? UITheme.Cyan;

            UITag tag = new UITag();

            Image body = UIFactory.CreateImage(parent, name, filled ? UITheme.A(a, 0.16f) : UITheme.A(UITheme.Surface, 0.55f));
            body.sprite = UISprites.Sheet(UITheme.CutChip);
            body.type = Image.Type.Sliced;
            RectTransform rect = body.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image edge = StretchImage(rect, "Edge", UITheme.A(a, filled ? 0.6f : 0.22f),
                UISprites.SheetEdge(UITheme.CutChip, 1f), Image.Type.Sliced);

            Text label = UIFactory.CreateText(rect, "Label", text, UITheme.SizeCaption,
                TextAnchor.MiddleCenter, UITheme.A(a, 0.95f), true);
            UIFactory.Stretch(label.rectTransform);
            // 文字框让出 TagPadX，宽度不够时文字会溢出框外（horizontalOverflow 是 Overflow，不裁剪），
            // 表现就是"字压边"。尺寸由调用点给，这里只核一遍：真放不下就在编辑器里喊出来，
            // 别等到实机截图才发现。
            label.rectTransform.offsetMin = new Vector2(UITheme.TagPadX, 0f);
            label.rectTransform.offsetMax = new Vector2(-UITheme.TagPadX, 0f);
#if UNITY_EDITOR
            // 容差 2px：preferredWidth 是排版器算出来的，和手算的字宽会差零点几像素，
            // 差这么点就报警会把每一枚 chip 都刷成黄字，反而看不见真的溢出。
            float need = label.preferredWidth + UITheme.TagPadX * 2f;
            if (need > size.x + 2f)
                Debug.LogWarning(string.Format(
                    "[UIChrome.Tag] \"{0}\" 需要 {1:0}px 宽才放得下（现给 {2:0}px），文字会压到切角边。",
                    text, need, size.x));
            if (size.y < UITheme.TagHeight - 2f)
                Debug.LogWarning(string.Format(
                    "[UIChrome.Tag] \"{0}\" 高度 {1:0}px 小于标准 {2:0}px，上下会贴边。",
                    text, size.y, UITheme.TagHeight));
#endif

            tag.Root = rect;
            tag.Body = body;
            tag.Edge = edge;
            tag.Text = label;
            return tag;
        }

        // ================================================================== 装饰

        /// <summary>对角取景框：只画两个角，比四角对称更有"取景器"味道</summary>
        public static Image[] CornerBrackets(RectTransform target, UICorner first, UICorner second,
                                             Color color, float length = 26f, float thickness = 2f, float inset = -3f)
        {
            Image[] a = Bracket(target, first, color, length, thickness, inset);
            Image[] b = Bracket(target, second, color, length, thickness, inset);
            return new[] { a[0], a[1], b[0], b[1] };
        }

        public static Image[] Bracket(RectTransform target, UICorner corner, Color color,
                                      float length, float thickness, float inset)
        {
            if (target == null) return new Image[0];

            float ax = corner == UICorner.TopLeft || corner == UICorner.BottomLeft ? 0f : 1f;
            float ay = corner == UICorner.TopLeft || corner == UICorner.TopRight ? 1f : 0f;
            Vector2 anchor = new Vector2(ax, ay);
            float sx = ax < 0.5f ? 1f : -1f;
            float sy = ay > 0.5f ? -1f : 1f;

            // pivot 与 anchor 重合，所以 inset 就是"从角点向内多少"（负数会跑到框外）
            Vector2 offset = new Vector2(inset * sx, inset * sy);

            Image h = UIFactory.CreateImage(target, "BracketH", color);
            UIFactory.Place(h.gameObject, anchor, anchor, offset, new Vector2(length, thickness));

            Image v = UIFactory.CreateImage(target, "BracketV", color);
            UIFactory.Place(v.gameObject, anchor, anchor, offset, new Vector2(thickness, length));

            return new[] { h, v };
        }

        /// <summary>一条细横线（默认 1px）</summary>
        public static Image Hairline(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                     Vector2 position, float width, Color color, float thickness = 1f)
        {
            Image line = UIFactory.CreateImage(parent, name, color);
            UIFactory.Place(line.gameObject, anchor, pivot, position, new Vector2(width, thickness));
            return line;
        }

        public static Image VLine(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                                  Vector2 position, float height, Color color, float thickness = 1f)
        {
            Image line = UIFactory.CreateImage(parent, name, color);
            UIFactory.Place(line.gameObject, anchor, pivot, position, new Vector2(thickness, height));
            return line;
        }

        /// <summary>一段带菱形端点的分隔线（比整条横线更像仪器刻度）</summary>
        public static RectTransform Rule(Transform parent, Vector2 position, float width, Color? color = null)
        {
            Color c = color ?? UITheme.Edge;

            RectTransform root = UIFactory.NewUI(parent, "Rule");
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;

            Image line = UIFactory.CreateImage(root, "Line", c);
            UIFactory.Place(line.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(width, 1f));

            Image dot = UIFactory.CreateImage(root, "Dot", c);
            UIFactory.Place(dot.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position + new Vector2(-width * 0.5f, 0f), new Vector2(7f, 7f));
            dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            return root;
        }

        /// <summary>
        /// 带菱形端点的竖线（横版是 Rule）。
        ///
        /// 一条裸线段读起来是"切了一刀"，两端各加一颗 45° 菱形之后变成"刻度尺"：
        /// 它不再是分区线，而是一件测量仪器上的刻度。
        /// </summary>
        public static RectTransform VRule(Transform parent, Vector2 position, float height,
                                          Color? color = null, float dotSize = 5f)
        {
            Color c = color ?? UITheme.Edge;

            RectTransform root = UIFactory.NewUI(parent, "VRule");
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;

            Image line = UIFactory.CreateImage(root, "Line", c);
            UIFactory.Place(line.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(1f, height));

            for (int i = 0; i < 2; i++)
            {
                float sy = i == 0 ? 1f : -1f;
                Image dot = UIFactory.CreateImage(root, "Tip" + i, c);
                UIFactory.Place(dot.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    position + new Vector2(0f, sy * height * 0.5f), new Vector2(dotSize, dotSize));
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            return root;
        }

        /// <summary>
        /// 内屏光：贴着面板一角的一团极淡径向光。
        ///
        /// 面板底色只有一种颜色时，整块屏看起来像"贴上去的一张纸"。
        /// 加一层 5% 的径向光后，左上略亮、右下略暗，"屏幕自己在发光"的观感就出来了。
        /// alpha 到 0.08 就开始像脏渍，0.05 是"几乎看不见、但去掉立刻变平"的临界值。
        /// </summary>
        public static Image InnerGlow(Transform parent, string name, Color color, Vector2 size,
                                      Vector2 position, float alpha = 0.05f)
        {
            Image glow = UIFactory.CreateImage(parent, name, UITheme.A(color, alpha));
            glow.sprite = SpriteFactory.Glow();
            glow.type = Image.Type.Simple;
            UIFactory.Place(glow.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, size);
            return glow;
        }

        /// <summary>
        /// 卡片的"厚度"：顶沿一条 1px 受光、底沿一条 1px 积影。
        ///
        /// 平面方块和"浮起的读数窗"差的常常就是这两条线——
        /// 现实里的面板总有上沿受光、下沿积影，少了它们，卡片只是一块被填了色的矩形。
        /// </summary>
        public static void CardShade(RectTransform card, float inset = 18f)
        {
            if (card == null) return;

            Image top = UIFactory.CreateImage(card, "ShadeTop", new Color(1f, 1f, 1f, 0.055f));
            RectTransform t = top.rectTransform;
            t.anchorMin = new Vector2(0f, 1f);
            t.anchorMax = new Vector2(1f, 1f);
            t.pivot = new Vector2(0.5f, 1f);
            t.offsetMin = new Vector2(inset, -1f);
            t.offsetMax = new Vector2(-inset, 0f);

            Image bottom = UIFactory.CreateImage(card, "ShadeBottom", new Color(0f, 0f, 0f, 0.20f));
            RectTransform b = bottom.rectTransform;
            b.anchorMin = new Vector2(0f, 0f);
            b.anchorMax = new Vector2(1f, 0f);
            b.pivot = new Vector2(0.5f, 0f);
            b.offsetMin = new Vector2(inset, 0f);
            b.offsetMax = new Vector2(-inset, 1f);
        }

        // ================================================================== 文本

        /// <summary>通用标签：字号 / 对齐 / 锚点 / 位置 / 尺寸 / 粗体 / 等宽</summary>
        public static Text CreateLabel(Transform parent, string name, string content, int fontSize,
                                       TextAnchor align, Color color, Vector2 anchor, Vector2 pivot,
                                       Vector2 position, Vector2 box, bool bold = true, bool mono = false)
        {
            Text text = mono
                ? UIFactory.CreateTextMono(parent, name, content, fontSize, align, color, bold)
                : UIFactory.CreateText(parent, name, content, fontSize, align, color, bold);

            UIFactory.Place(text.gameObject, anchor, pivot, position, box);
            return text;
        }

        /// <summary>全大写拉丁小标签（"SECTOR 02"），字距拉开、等宽、极淡</summary>
        public static Text Eyebrow(Transform parent, string name, string content, Vector2 anchor, Vector2 pivot,
                                   Vector2 position, Vector2 box, TextAnchor align, Color? color = null, bool spaced = true)
        {
            // 分区标签是"要读的功能名"，不是装饰铭牌。等宽大写英文在 18px 以下
            // 会散成字母点阵，中文界面里也没有信息价值，所以这里统一走中文黑体粗体。
            // 品牌铭牌（IMAGINARY SECTOR 之类）走 Header.Meta / Footer.Right，仍是等宽终端味。
            // spaced 保留兼容旧调用点，但不再插空格；键帽、HUD 状态词请直接用 CreateTextMono。
            Color c = color ?? UITheme.TextDim;

            // 按内容分流：中文功能标签走黑体 22px；纯拉丁的品牌铭牌
            // （IMAGINARY SECTOR / SCORE / WAVE）仍走等宽 20px，保持终端味但不插空格。
            bool hasCjk = false;
            for (int i = 0; i < content.Length; i++)
                if (content[i] > 0x2E7F) { hasCjk = true; break; }

            Text text = hasCjk
                ? UIFactory.CreateText(parent, name, content, UITheme.SizeLabel, align, c, true)
                : UIFactory.CreateTextMono(parent, name, content, UITheme.SizeCaption, align, c, true);

            // box 高必须照调用点原值：Eyebrow 大多是顶部对齐（pivot.y=1），
            // 把 box 撑高会把文字中心一起往下推，正好压到下方的分区横线。
            // Text 的 verticalOverflow 是 Overflow，22px 装 20px box 不会裁字。
            UIFactory.Place(text.gameObject, anchor, pivot, position, box);
            return text;
        }

        // ================================================================== 背景

        /// <summary>整屏压暗层</summary>
        public static Image Scrim(Transform parent, string name = "Scrim", Color? color = null)
        {
            Image image = UIFactory.CreateImage(parent, name, color ?? new Color(0.006f, 0.011f, 0.022f, 0.62f));
            UIFactory.Stretch(image.rectTransform);
            return image;
        }

        /// <summary>整屏薄网格 + 上下渐隐，给纯色背景一点"玻璃板"的细节</summary>
        public static Image GridBackdrop(Transform parent, string name = "Grid", float alpha = 0.05f)
        {
            Image grid = UIFactory.CreateTiled(parent, name, UISprites.Grid(), new Color(1f, 1f, 1f, alpha));
            UIFactory.Stretch(grid.rectTransform);
            return grid;
        }

        // ================================================================== 内部

        private static Image StretchImage(Transform parent, string name, Color color, Sprite sprite, Image.Type type)
        {
            Image image = UIFactory.CreateImage(parent, name, color);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = type;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }
    }
}
