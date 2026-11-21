using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 游戏中的整屏取景框：四个角各一个短括号，顶部/底部两条极细的标尺线 + 刻度。
    ///
    /// 它是 HUD 的"底气"：战斗时画面里信息很多（血条、连击、Boss 条），
    /// 四角的括号把这一堆东西框进一个"取景器"里，让玩家潜意识知道哪里是画面边界；
    /// 上下标尺线则给正在高速移动的战机一个不动的参照，减少横向拖影带来的眩晕。
    ///
    /// 只在 Playing/Paused 显示：菜单、结算、开机画面都不该有战斗取景框。
    /// 全部是 1–2px 的线，没有底、没有面板。
    /// </summary>
    public class ScreenFrame : UIPanelBase
    {
        private const float CornerInset = 28f;
        private const float CornerLength = 64f;
        private const float RuleOffset = 46f;
        private const float RuleWidth = 1860f;
        private const float TickSpan = 880f;
        private const int TickCount = 16;

        public void Build()
        {
            InitGroup(false);

            BuildCorners();
            BuildRulers();
        }

        private void BuildCorners()
        {
            RectTransform root = (RectTransform)transform;
            Color color = UITheme.A(UITheme.Cyan, 0.30f);

            UIChrome.CornerBrackets(root, UICorner.TopLeft, UICorner.BottomRight,
                color, CornerLength, 2f, CornerInset);
            UIChrome.CornerBrackets(root, UICorner.TopRight, UICorner.BottomLeft,
                color, CornerLength, 2f, CornerInset);
        }

        /// <summary>
        /// 顶部/底部标尺：一条横线 + 16 个刻度。
        /// 每 4 格一个长刻度，读起来像测量区，而不是装饰性的虚线。
        /// </summary>
        private void BuildRulers()
        {
            Color lineColor = UITheme.A(UITheme.Cyan, 0.14f);
            Color majorColor = UITheme.A(UITheme.Cyan, 0.26f);
            Color minorColor = UITheme.A(UITheme.Cyan, 0.13f);

            UIChrome.Hairline(transform, "RulerTop",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -RuleOffset), RuleWidth, lineColor, 1f);

            UIChrome.Hairline(transform, "RulerBottom",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, RuleOffset), RuleWidth, lineColor, 1f);

            for (int i = 0; i <= TickCount; i++)
            {
                float x = -TickSpan + (TickSpan * 2f) * i / TickCount;
                bool major = (i % 4 == 0);
                float height = major ? 9f : 5f;
                Color color = major ? majorColor : minorColor;

                UIChrome.VLine(transform, "TickTop" + i,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, -RuleOffset), height, color, 1f);

                UIChrome.VLine(transform, "TickBottom" + i,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(x, RuleOffset), height, color, 1f);
            }

            // 左右两条很淡的竖线，把取景框从"四角"补成"四边"
            UIChrome.VLine(transform, "RulerLeft",
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(CornerInset, 0f), 940f, UITheme.A(UITheme.Cyan, 0.06f), 1f);

            UIChrome.VLine(transform, "RulerRight",
                new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-CornerInset, 0f), 940f, UITheme.A(UITheme.Cyan, 0.06f), 1f);
        }

        // ------------------------------------------------------------ 状态

        private void OnEnable()
        {
            EventBus.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            EventBus.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            // OnEnable 可能在 Build 之前就跑（AddComponent 立刻触发），group 还没建好
            if (group == null) return;
            SetVisible(state == GameState.Playing || state == GameState.Paused);
        }
    }
}
