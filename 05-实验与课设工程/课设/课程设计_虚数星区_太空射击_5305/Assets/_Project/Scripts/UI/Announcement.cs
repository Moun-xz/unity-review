using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 关卡开头 / Boss 警告的字幕层。
    ///
    /// 它故意不是面板：这里没有任何可点的东西，也不该挡住战场。
    /// 做法只有"字 + 两条细线"：上面一行等宽小标、中间一行大字、下面一行副标，
    /// 两条强调线从短拉到长，把文字夹在中间——像投影在太空里的一块标牌，而不是 UI 窗口。
    ///
    /// 时间线：淡入 0.26s（同时下滑 20px）→ 保持 → 淡出 0.42s（上浮 12px）。
    /// Boss 警告在保持段里脉冲 N 次，颜色用 Danger；关卡播报只做极轻微的呼吸。
    /// 暂停（GameState.Paused）时协程原地等待，不推进时间，也不会在玩家看菜单时错过。
    /// </summary>
    public class Announcement : MonoBehaviour
    {
        private const float FadeInDuration = 0.26f;
        private const float FadeOutDuration = 0.42f;
        private const float SlideDistance = 20f;
        private const float BaseY = 30f;
        private const float RuleWidth = 520f;
        private const float RuleMinWidth = 60f;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private CanvasGroup group;
        private RectTransform content;
        private Text eyebrow;
        private Text bigText;
        private Text smallText;
        private Image ruleTop;
        private Image ruleBottom;

        private Coroutine routine;
        private Color accent = UITheme.Cyan;
        private int pulses;
        private float hold = 1f;

        public void Build()
        {
            group = UIFactory.AddCanvasGroup(gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            content = UIFactory.NewUI(transform, "Content");
            UIFactory.Place(content.gameObject, Center, Center,
                new Vector2(0f, BaseY), new Vector2(1500f, 300f));

            eyebrow = UIChrome.Eyebrow(content, "Eyebrow", "", Center, Center,
                new Vector2(0f, 86f), new Vector2(1000f, 20f), TextAnchor.MiddleCenter,
                UITheme.TextFaint);

            ruleTop = UIChrome.Hairline(content, "RuleTop", Center, Center,
                new Vector2(0f, 64f), RuleMinWidth, UITheme.A(UITheme.Cyan, 0.55f), 1f);
            ruleBottom = UIChrome.Hairline(content, "RuleBottom", Center, Center,
                new Vector2(0f, -84f), RuleMinWidth, UITheme.A(UITheme.Cyan, 0.55f), 1f);

            bigText = UIChrome.CreateLabel(content, "Headline", "", UITheme.SizeH1,
                TextAnchor.MiddleCenter, UITheme.TextMain, Center, Center,
                new Vector2(0f, 8f), new Vector2(1400f, 70f), true);
            UIFactory.AddShadow(bigText, UITheme.A(UITheme.SurfaceDeep, 0.9f), new Vector2(0f, -3f));

            smallText = UIChrome.CreateLabel(content, "Sub", "", UITheme.SizeBody,
                TextAnchor.MiddleCenter, UITheme.TextDim, Center, Center,
                new Vector2(0f, -50f), new Vector2(1200f, 34f));
        }

        // ------------------------------------------------------------ 对外入口

        public void ShowLevel(string title, string subtitle)
        {
            // eyebrow 走等宽字体（Consolas，无 CJK 字形），所以只放拉丁文。
            Play(title, subtitle, "SECTOR CLEAR // AREA SECURED",
                UITheme.Cyan, 1.15f, 0, true);
        }

        public void ShowBossWarning(string bossName)
        {
            string sub = string.IsNullOrEmpty(bossName)
                ? "检测到高强度信号"
                : "检测到高强度信号　·　" + bossName;

            Play("W A R N I N G", sub, "THREAT DETECTED // HOSTILE SIGNAL",
                UITheme.Danger, 2.0f, 3, false);
        }

        public void Clear()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (group != null) group.alpha = 0f;
            if (content != null) content.anchoredPosition = new Vector2(0f, BaseY);
        }

        // ------------------------------------------------------------ 播放

        private void Play(string headline, string sub, string eyebrowText,
                          Color color, float holdTime, int pulseCount, bool level)
        {
            accent = color;
            hold = holdTime;
            pulses = pulseCount;

            if (eyebrow != null)
            {
                eyebrow.text = eyebrowText;
                eyebrow.color = UITheme.TextDim;
            }

            if (bigText != null)
            {
                bigText.text = headline;
                bigText.fontSize = level ? UITheme.SizeH1 : UITheme.SizeH2;
                bigText.color = accent;
            }

            if (smallText != null)
            {
                smallText.text = sub;
                smallText.color = UITheme.TextDim;
            }

            SetRules(RuleMinWidth, 0f);

            if (group != null) group.alpha = 0f;
            if (content != null) content.anchoredPosition = new Vector2(0f, BaseY - SlideDistance);

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            // ---- 淡入 ----
            float t = 0f;
            while (t < FadeInDuration)
            {
                if (IsPaused()) { yield return null; continue; }

                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / FadeInDuration);
                float eased = 1f - (1f - k) * (1f - k);

                if (group != null) group.alpha = eased;
                if (content != null)
                {
                    content.anchoredPosition = new Vector2(0f, BaseY - SlideDistance * (1f - eased));
                }

                SetRules(Mathf.Lerp(RuleMinWidth, RuleWidth, eased), eased);
                yield return null;
            }

            if (group != null) group.alpha = 1f;
            if (content != null) content.anchoredPosition = new Vector2(0f, BaseY);
            SetRules(RuleWidth, 1f);

            // ---- 保持（Boss 警告在这里脉冲） ----
            float held = 0f;
            while (held < hold)
            {
                if (IsPaused()) { yield return null; continue; }

                held += Time.unscaledDeltaTime;

                float pulse;
                if (pulses > 0)
                {
                    // 尖峰型脉冲：|sin| 的 6 次方，只在峰顶亮一下，不会整段闪成 LED
                    pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(held * 5.0f)), 6f);
                    if (bigText != null)
                    {
                        bigText.color = UITheme.A(accent, 1f - 0.55f * pulse);
                    }
                }
                else
                {
                    // 关卡播报：轻微呼吸，避免完全静止像贴图
                    pulse = (Mathf.Sin(held * 2.2f) + 1f) * 0.5f;
                    if (bigText != null)
                    {
                        bigText.color = UITheme.A(accent, Mathf.Lerp(0.92f, 1f, pulse));
                    }
                }

                float ruleAlpha = 0.55f * (1f - 0.25f * pulse);
                if (ruleTop != null) ruleTop.color = UITheme.A(accent, ruleAlpha);
                if (ruleBottom != null) ruleBottom.color = UITheme.A(accent, ruleAlpha);

                yield return null;
            }

            if (bigText != null) bigText.color = accent;

            // ---- 淡出 ----
            float outT = 0f;
            while (outT < FadeOutDuration)
            {
                if (IsPaused()) { yield return null; continue; }

                outT += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(outT / FadeOutDuration);
                float eased = k * k;

                if (group != null) group.alpha = 1f - eased;
                if (content != null)
                {
                    content.anchoredPosition = new Vector2(0f, BaseY + 12f * eased);
                }

                SetRules(Mathf.Lerp(RuleWidth, RuleWidth * 0.55f, eased), 1f - eased);
                yield return null;
            }

            if (group != null) group.alpha = 0f;
            routine = null;
        }

        // ------------------------------------------------------------ 工具

        private static bool IsPaused()
        {
            return GameManager.Instance != null && GameManager.Instance.State == GameState.Paused;
        }

        private void SetRules(float width, float alphaMul)
        {
            if (ruleTop != null)
            {
                ruleTop.rectTransform.sizeDelta = new Vector2(width, 1f);
                ruleTop.color = UITheme.A(accent, 0.55f * Mathf.Clamp01(alphaMul));
            }

            if (ruleBottom != null)
            {
                ruleBottom.rectTransform.sizeDelta = new Vector2(width, 1f);
                ruleBottom.color = UITheme.A(accent, 0.55f * Mathf.Clamp01(alphaMul));
            }
        }
    }
}
