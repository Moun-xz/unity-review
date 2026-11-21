using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 开机画面：一块接近全黑的底 + 细扫描线 + 一行字标 + 底部进度轨。
    ///
    /// 为什么没有面板、没有边框：
    ///   这是游戏还"没启动"的一秒。任何卡片、切角、发光边都会立刻让观众意识到"哦，一个 UI 窗口"。
    ///   纯色底 + 扫描线 + 一条会走的进度轨，读起来更像设备自检，而不是一个加载框。
    ///
    /// 时序：内容 0.50s 显影（同时从 104% 缩回 100%）→ 保持 0.40s → 0.30s 淡出 → 回调 onDone → 销毁。
    /// 0.35s 之后按任意键可以提前跳过（SkipFade 0.18s）。onDone 只在淡出真正结束后触发一次。
    /// </summary>
    public class BootSplash : MonoBehaviour
    {
        private const float FadeInDuration = 0.50f;
        private const float HoldDuration = 0.40f;
        private const float FadeOutDuration = 0.30f;
        private const float SkipFadeDuration = 0.18f;
        private const float MinShowTime = 0.35f;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private CanvasGroup group;
        private CanvasGroup content;
        private RectTransform contentRect;
        private Image progress;
        private Image sweep;
        private Image centerLine;

        private Action onDone;
        private float clock;
        private float fadeStart = -1f;
        private float fadeDuration = FadeOutDuration;
        private float fadeFromScale = 1f;

        /// <summary>
        /// 在指定父节点下建一块开机画面。必须是 static：开机时还没有任何场景对象可以挂它。
        /// </summary>
        public static BootSplash Create(Transform parent, Action onDone)
        {
            GameObject go = new GameObject("BootSplash", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Stretch((RectTransform)go.transform);

            BootSplash splash = go.AddComponent<BootSplash>();
            splash.onDone = onDone;
            splash.Build();
            return splash;
        }

        private void Build()
        {
            group = UIFactory.AddCanvasGroup(gameObject);

            // 底色：SurfaceDeep 但 alpha 拉到 1。0.94 会漏出后面的主菜单，开机画面必须是实心的。
            Image bg = UIFactory.CreateImage(transform, "Bg", UITheme.A(UITheme.SurfaceDeep, 1f));
            UIFactory.Stretch(bg.rectTransform);
            bg.raycastTarget = true; // 吃掉点击，别让开机时的光标打到标题屏

            // 全屏扫描线：28 条 1px 横线、间距 40px。极淡，只在纯色底上才看得见。
            for (int i = 0; i < 28; i++)
            {
                float y = -540f + 20f + i * 40f;
                UIChrome.Hairline(transform, "Scan" + i, Center, Center,
                    new Vector2(0f, y), 1920f, UITheme.A(UITheme.Cyan, 0.045f), 1f);
            }

            // 一条从下往上扫过的亮线：开机时唯一"在动"的东西，进度轨之外的第二个活物
            sweep = UIChrome.Hairline(transform, "Sweep", Center, Center,
                new Vector2(0f, 560f), 1920f, UITheme.A(UITheme.Cyan, 0.20f), 2f);

            // 字标下方一条从 0 拉到 640 的细线，和 content 一起显影
            centerLine = UIChrome.Hairline(transform, "CenterLine", Center, Center,
                new Vector2(0f, -64f), 0f, UITheme.A(UITheme.Cyan, 0.35f), 1f);

            contentRect = UIFactory.NewUI(transform, "Content");
            UIFactory.Place(contentRect.gameObject, Center, Center,
                Vector2.zero, new Vector2(1400f, 600f));

            content = UIFactory.AddCanvasGroup(contentRect.gameObject);
            content.alpha = 0f;
            content.blocksRaycasts = false;
            content.interactable = false;

            UIChrome.Eyebrow(contentRect, "Eyebrow", "PIXEL GRAVITY // BOOT SEQUENCE",
                Center, Center, new Vector2(0f, 150f), new Vector2(900f, 20f),
                TextAnchor.MiddleCenter, UITheme.A(UITheme.Cyan, 0.70f));

            Text wordmark = UIFactory.CreateText(contentRect, "Wordmark", "虚 数 星 区",
                UITheme.SizeH2, TextAnchor.MiddleCenter, UITheme.TextMain, true);
            UIFactory.Place(wordmark.gameObject, Center, Center,
                new Vector2(0f, 62f), new Vector2(900f, 60f));
            UIFactory.AddShadow(wordmark, UITheme.A(UITheme.SurfaceDeep, 0.9f), new Vector2(0f, -3f));

            UIChrome.CreateLabel(contentRect, "Tagline", "赛博朋克 · 太空射击", UITheme.SizeCaption,
                TextAnchor.MiddleCenter, UITheme.TextDim, Center, Center,
                new Vector2(0f, 2f), new Vector2(700f, 28f));

            // 进度轨：Track 是一条暗线，Progress 是同位置的填充条，靠 fillAmount 走
            Image track = UIFactory.CreateImage(contentRect, "Track", UITheme.A(UITheme.Cyan, 0.14f));
            UIFactory.Place(track.gameObject, Center, Center,
                new Vector2(0f, -236f), new Vector2(420f, 2f));

            progress = UIFactory.CreateBar(contentRect, "Progress", UITheme.A(UITheme.Cyan, 0.75f));
            UIFactory.Place(progress.gameObject, Center, Center,
                new Vector2(0f, -236f), new Vector2(420f, 2f));
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillAmount = 0f;

            // 跳过提示含中文，用普通字体（Eyebrow 是 Consolas，无 CJK 字形会出方块）。
            UIChrome.CreateLabel(transform, "Skip", "按任意键跳过　/　SKIP",
                UITheme.SizeCaption, TextAnchor.MiddleRight, UITheme.TextFaint,
                Center, new Vector2(1f, 0.5f), new Vector2(880f, -492f),
                new Vector2(360f, 20f));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            clock += dt;

            if (fadeStart < 0f)
            {
                UpdateReveal();
                return;
            }

            UpdateFade();
        }

        private void UpdateReveal()
        {
            float k = Mathf.Clamp01(clock / FadeInDuration);
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);

            if (content != null) content.alpha = eased;
            if (progress != null)
            {
                progress.fillAmount = Mathf.Clamp01(clock / (FadeInDuration + HoldDuration));
            }

            if (centerLine != null)
            {
                centerLine.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 640f, eased), 1f);
            }

            // 扫描线从上扫到下，用 1.15s 走完；越往下越淡，像光在扫过面板
            if (sweep != null)
            {
                float s = Mathf.Clamp01(clock / 1.15f);
                sweep.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(560f, -560f, s));
                sweep.color = UITheme.A(UITheme.Cyan, 0.20f * (1f - s));
            }

            ApplyScale(Mathf.Lerp(1.04f, 1f, eased));

            if (clock >= MinShowTime && Input.anyKeyDown)
            {
                BeginFadeOut(SkipFadeDuration);
            }
            else if (clock >= FadeInDuration + HoldDuration)
            {
                BeginFadeOut(FadeOutDuration);
            }
        }

        private void UpdateFade()
        {
            float k = Mathf.Clamp01((clock - fadeStart) / fadeDuration);
            float eased = k * k;

            if (group != null) group.alpha = 1f - eased;
            if (progress != null) progress.fillAmount = 1f;
            ApplyScale(Mathf.Lerp(fadeFromScale, 1.05f, eased));

            if (k >= 1f)
            {
                // 先把回调取出来再置空：onDone 里可能又触发别的流程
                Action callback = onDone;
                onDone = null;
                if (callback != null) callback();
                Destroy(gameObject);
            }
        }

        private void BeginFadeOut(float duration)
        {
            if (fadeStart >= 0f) return;

            fadeStart = clock;
            fadeDuration = Mathf.Max(0.01f, duration);
            fadeFromScale = contentRect != null ? contentRect.localScale.x : 1f;
        }

        private void ApplyScale(float scale)
        {
            if (contentRect != null)
            {
                contentRect.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
