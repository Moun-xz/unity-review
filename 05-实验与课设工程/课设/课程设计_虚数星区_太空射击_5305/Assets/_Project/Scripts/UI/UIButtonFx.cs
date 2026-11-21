using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 菜单按钮 / 列表行的悬停、按下反馈。
    ///
    /// 为什么不直接用 Button 的 ColorTint：它只能整体乘一层颜色，做出来是"整块底变亮"，很平。
    /// 这里自己插值，同时动四个东西：
    ///   fill   —— 悬停时从左往右铺一层淡色（alpha 0 → fillAlpha）
    ///   accent —— 左侧竖条变宽、变亮
    ///   index  —— 行首序号从灰变亮（列表行才有）
    ///   chevron—— 右侧箭头右移几像素
    /// 缩放分两段：按下 18 的速度砸下去、松开 10 的速度回位。两边同速会显得黏手。
    /// </summary>
    public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                              IPointerDownHandler, IPointerUpHandler
    {
        public Image glow;
        public Image accent;
        public Text label;

        // 新样式（列表行 / 主按钮）用的可选部件，不设就自动跳过
        public Image fill;
        public Text index;
        public RectTransform chevron;
        public Image edge;

        public float idleWidth = 3f;
        public float hoverWidth = 7f;
        public float fillAlpha = 0.10f;
        public float chevronSlide = 6f;

        // 文字的两端颜色。原来写死成"暗 → 亮"的灰阶插值，主按钮的青色标题
        // 一旦把鼠标移上去就被换成灰的、移开也回不来（插值到 current=0 是 TextDim，不是青色）。
        // 主按钮要靠这两个字段把青色留住。
        public Color idleColor = UITheme.TextDim;
        public Color hoverColor = UITheme.TextMain;

        // 描边只在悬停时变亮，不动颜色本身（RGB 保留调用方设的值）。
        // Ghost 按钮平时没有边，悬停才浮出极淡的一圈——这是"可以点"的提示，
        // 也是它和主按钮唯一的形状差异，所以幅度要小。
        public float idleEdgeAlpha = 0.055f;
        public float hoverEdgeAlpha = 0.14f;

        // 「注意态」：文字固定琥珀色，不受 hover 的青蓝渐变影响。
        // 退出确认这类"再点一次就真退了"的提示需要它，否则和普通按钮长得一样。
        private bool alert;

        private float target;
        private float current;
        private float pressTarget;
        private float pressCurrent;

        private bool homeCaptured;
        private Vector2 chevronHome;

        private const float Speed = 10f;
        private const float PressSpeed = 18f;      // 按下去要更跟手
        private const float HoverGlowAlpha = 0.14f;
        private const float HoverScale = 0.012f;
        private const float PressScale = 0.030f;

        public void SetAlert(bool on)
        {
            alert = on;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            target = 1f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            target = 0f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressTarget = 1f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressTarget = 0f;
        }

        private void OnDisable()
        {
            // 面板淡出时鼠标可能还停在按钮上，不归位的话下次打开会残留高亮 / 缩小状态
            target = 0f;
            current = 0f;
            pressTarget = 0f;
            pressCurrent = 0f;
            Apply();
        }

        private void Update()
        {
            bool moved = false;

            if (!Mathf.Approximately(current, target))
            {
                current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * Speed);
                moved = true;
            }

            if (!Mathf.Approximately(pressCurrent, pressTarget))
            {
                pressCurrent = Mathf.MoveTowards(pressCurrent, pressTarget, Time.unscaledDeltaTime * PressSpeed);
                moved = true;
            }

            if (moved) Apply();
        }

        private void Apply()
        {
            if (fill != null)
            {
                Color color = fill.color;
                color.a = Mathf.Lerp(0f, fillAlpha, current);
                fill.color = color;
            }

            if (glow != null)
            {
                Color color = glow.color;
                color.a = Mathf.Lerp(0f, HoverGlowAlpha, current);
                glow.color = color;
            }

            if (accent != null)
            {
                Color color = accent.color;
                color.a = Mathf.Lerp(0.40f, 1f, current);
                accent.color = color;

                Vector2 size = accent.rectTransform.sizeDelta;
                size.x = Mathf.Lerp(idleWidth, hoverWidth, current);
                accent.rectTransform.sizeDelta = size;
            }

            if (edge != null)
            {
                Color color = edge.color;
                color.a = Mathf.Lerp(idleEdgeAlpha, hoverEdgeAlpha, current);
                edge.color = color;
            }

            if (index != null)
            {
                index.color = Color.Lerp(UITheme.TextFaint, UITheme.Cyan, current * 0.92f);
            }

            if (chevron != null)
            {
                if (!homeCaptured)
                {
                    chevronHome = chevron.anchoredPosition;
                    homeCaptured = true;
                }

                Vector2 pos = chevronHome;
                pos.x += current * chevronSlide;
                chevron.anchoredPosition = pos;
            }

            if (label != null)
            {
                label.color = alert ? UITheme.Amber : Color.Lerp(idleColor, hoverColor, current);
            }

            // 悬停微放大、按下缩回去：按钮"被按进去了"靠这一下
            float scale = 1f + current * HoverScale - pressCurrent * PressScale;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
