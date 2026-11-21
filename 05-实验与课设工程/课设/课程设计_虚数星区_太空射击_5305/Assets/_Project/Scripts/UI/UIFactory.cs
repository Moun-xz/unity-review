using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// UGUI 构建工具。
    ///
    /// 所有界面都在代码里搭，不依赖预制体。
    /// 字体优先取系统里的中文字体（微软雅黑 / 黑体 / 等线 …），
    /// 这样界面可以直接写中文，也不用往工程里放字体文件（避免版权问题）。
    /// 只有系统里一个中文字体都找不到时才回退到内置 LegacyRuntime（那时候中文会变方块，会在 Console 里报警告）。
    /// </summary>
    public static class UIFactory
    {
        private static Font defaultFont;

        /// <summary>按优先级尝试的字体名，中英文名都列上：不同语言版本的 Windows 注册名不一样</summary>
        private static readonly string[] PreferredFonts =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
            "SimHei", "黑体",
            "DengXian", "等线",
            "Source Han Sans SC", "Noto Sans CJK SC", "思源黑体",
            "SimSun", "宋体"
        };

        public static Font DefaultFont
        {
            get
            {
                if (defaultFont == null) defaultFont = ResolveFont();
                return defaultFont;
            }
        }

        private static Font ResolveFont()
        {
            // 先用系统字体列表做精确匹配，这样能确认「真的存在」，而不是拿到一个渲染不出中文的替身
            string[] installed = Font.GetOSInstalledFontNames();

            for (int i = 0; i < PreferredFonts.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (!string.Equals(installed[j], PreferredFonts[i], System.StringComparison.OrdinalIgnoreCase)) continue;

                    Font matched = Font.CreateDynamicFontFromOSFont(installed[j], 32);
                    if (matched != null) return matched;
                }
            }

            // 精确匹配失败时再让 Unity 自己挑一次
            Font direct = Font.CreateDynamicFontFromOSFont(PreferredFonts, 32);
            if (direct != null) return direct;

            Debug.LogWarning("[UIFactory] 系统里没找到中文字体，已回退到内置字体，中文会显示为方块。");

            Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin == null) builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (builtin == null) builtin = Font.CreateDynamicFontFromOSFont("Arial", 24);
            return builtin;
        }

        // ------------------------------------------------------------ 画布

        public static Canvas CreateCanvas(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>补齐事件系统，否则以后加按钮点不动</summary>
        public static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;

            GameObject go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // ------------------------------------------------------------ 布局

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        // ------------------------------------------------------------ 文本

        public static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, Color color, bool bold = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Text text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;

            return text;
        }

        /// <summary>
        /// 给文字加一层投影。可重复调用——已有就直接改参数，不会叠加出好几个组件
        /// （结算面板每次 Show 都会换配色，重复 AddComponent 会越堆越多）。
        /// </summary>
        public static void AddShadow(Graphic graphic, Color color, Vector2 distance)
        {
            if (graphic == null) return;

            Shadow shadow = graphic.GetComponent<Shadow>();
            // Outline 是 Shadow 的子类，GetComponent 会先拿到它，这里要排除掉
            if (shadow == null || shadow is Outline) shadow = graphic.gameObject.AddComponent<Shadow>();

            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        /// <summary>给文字加描边（标题用，让字从背景里跳出来）</summary>
        public static void AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            if (graphic == null) return;

            Outline outline = graphic.GetComponent<Outline>();
            if (outline == null) outline = graphic.gameObject.AddComponent<Outline>();

            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        // ------------------------------------------------------------ 图形

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.sprite = SpriteFactory.Pixel();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>创建一条填充式进度条，返回填充层（调用方自行设置 fillAmount）</summary>
        public static Image CreateBar(Transform parent, string name, Color fillColor)
        {
            Image fill = CreateImage(parent, name, fillColor);

            // 必须给一个 sprite：Image 在没有 sprite 时会退回成纯色矩形，
            // 此时 Type.Filled 的 fillAmount 不会生效，血条就会永远满格。
            fill.sprite = SpriteFactory.Pixel();

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        public static CanvasGroup AddCanvasGroup(GameObject go)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            return group;
        }

        // ------------------------------------------------------------ 等宽字体（数值用）

        private static Font monoFont;
        private static bool monoResolved;

        /// <summary>
        /// 等宽字体。分数、伤害、时间这类数字用等宽体才有"仪表盘"的味道：
        /// 变宽体（雅黑）里 1 和 0 宽度不同，数字滚动时整行会左右抖。
        /// 都找不到就退回中文字体，只是失去对齐感，不影响显示。
        /// </summary>
        public static Font MonoFont
        {
            get
            {
                if (!monoResolved)
                {
                    monoResolved = true;
                    monoFont = ResolveMono();
                }
                return monoFont;
            }
        }

        private static readonly string[] PreferredMono =
        {
            // Cascadia Mono 的字形更方正、0 带点，和「战术终端」这套界面最搭；
            // 机器上没有就退回 Consolas，再没有才用系统默认。
            "Cascadia Mono", "Cascadia Code", "Consolas", "DejaVu Sans Mono",
            "Courier New", "Lucida Console", "JetBrains Mono", "Menlo"
        };

        private static Font ResolveMono()
        {
            string[] installed = Font.GetOSInstalledFontNames();

            for (int i = 0; i < PreferredMono.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (!string.Equals(installed[j], PreferredMono[i], System.StringComparison.OrdinalIgnoreCase)) continue;

                    Font matched = Font.CreateDynamicFontFromOSFont(installed[j], 32);
                    if (matched != null) return matched;
                }
            }

            return DefaultFont;
        }

        /// <summary>用指定字体排版的一段文字</summary>
        public static Text CreateTextFont(Transform parent, string name, string content, Font font,
                                          int fontSize, TextAnchor anchor, Color color, bool bold = true)
        {
            Text text = CreateText(parent, name, content, fontSize, anchor, color, bold);
            if (font != null) text.font = font;
            return text;
        }

        /// <summary>等宽数值文字</summary>
        public static Text CreateTextMono(Transform parent, string name, string content,
                                          int fontSize, TextAnchor anchor, Color color, bool bold = true)
        {
            return CreateTextFont(parent, name, content, MonoFont, fontSize, anchor, color, bold);
        }

        /// <summary>
        /// 逐字插入一个空格；已有空格处再加两个，让词与词之间留出明显断口。
        ///
        /// 为什么词距要单独处理：等宽字体里一个空格和"字距"宽度一样，
        /// "IMAGINARY SECTOR" 拉开后看起来就是一整串字母，读不出是两个词。
        /// 词间给三倍空隙，扫描时才能一眼分块。
        /// </summary>
        public static string SpaceOut(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length * 2);

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                sb.Append(c);

                // 原有空格：补足三格，形成词间隔
                if (c == ' ')
                {
                    sb.Append(' ');
                    sb.Append(' ');
                    continue;
                }

                if (i >= value.Length - 1) break;
                if (value[i + 1] == ' ') continue;

                sb.Append(' ');
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------ 基础容器 / 贴图

        /// <summary>一个空的 RectTransform 容器（0×0，调用方自己摆）</summary>
        public static RectTransform NewUI(Transform parent, string name)
        {
            return NewRect(parent, name);
        }

        /// <summary>平铺贴图（网格、斜纹等需要 Image.Type.Tiled 的场合）</summary>
        public static Image CreateTiled(Transform parent, string name, Sprite sprite, Color color)
        {
            Image image = CreateImage(parent, name, color);
            image.sprite = sprite;
            image.type = Image.Type.Tiled;
            return image;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
