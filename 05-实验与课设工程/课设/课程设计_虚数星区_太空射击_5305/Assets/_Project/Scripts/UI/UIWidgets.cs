using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 分段式进度条（血条 / Boss 条 / 波次进度）。
    ///
    /// 连续填充的条看起来像"游戏里的血条"，一段一段亮起来的条看起来像"仪表"。
    /// 这套 UI 走的是后者：20 格、每格之间留 2px 缝，掉血时是整格熄灭，
    /// 比连续条更容易一眼读出"还剩几成"。
    /// </summary>
    public class UISegBar : MonoBehaviour
    {
        private Image[] segments;
        private Color onColor;
        private Color offColor;
        private float ratio = 1f;

        public static UISegBar Create(Transform parent, string name, Vector2 size, Vector2 position,
                                      int count, Color on, Color off)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

            UISegBar bar = go.AddComponent<UISegBar>();
            bar.Build(count, on, off, size);
            return bar;
        }

        private void Build(int count, Color on, Color off, Vector2 size)
        {
            count = Mathf.Max(1, count);
            onColor = on;
            offColor = off;

            const float gap = 2f;
            float step = (size.x + gap) / count;
            float width = step - gap;

            segments = new Image[count];

            for (int i = 0; i < count; i++)
            {
                Image seg = UIFactory.CreateImage(transform, "Seg" + i, off);
                UIFactory.Place(seg.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(i * step, 0f), new Vector2(width, size.y));
                segments[i] = seg;
            }

            Apply();
        }

        public void Set(float value)
        {
            ratio = Mathf.Clamp01(value);
            Apply();
        }

        /// <summary>换亮色（血量见底时切成红色）</summary>
        public void SetOnColor(Color color)
        {
            onColor = color;
            Apply();
        }

        public void SetOffColor(Color color)
        {
            offColor = color;
            Apply();
        }

        private void Apply()
        {
            if (segments == null) return;

            float lit = ratio * segments.Length;

            for (int i = 0; i < segments.Length; i++)
            {
                segments[i].color = i < lit ? onColor : offColor;
            }
        }
    }

    /// <summary>
    /// 细长数值条（战机属性 / 关卡进度）。带独立刻度，读起来像仪器读数而不是血条。
    /// </summary>
    public class UIMeter : MonoBehaviour
    {
        private Image fill;
        private RectTransform fillRect;
        private Image track;
        private Color fillColor;

        public static UIMeter Create(Transform parent, string name, Vector2 size, Vector2 position,
                                     int ticks, Color accent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

            UIMeter meter = go.AddComponent<UIMeter>();
            meter.Build(size, ticks, accent);
            return meter;
        }

        private void Build(Vector2 size, int ticks, Color accent)
        {
            track = UIFactory.CreateImage(transform, "Track", UITheme.SurfaceSunken);
            track.sprite = UISprites.Sheet(2f);
            track.type = Image.Type.Sliced;
            UIFactory.Place(track.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size);

            fill = UIFactory.CreateImage(transform, "Fill", accent);
            fillColor = accent;
            fill.sprite = UISprites.Sheet(2f);
            fill.type = Image.Type.Sliced;
            fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(1f, 1.5f);
            fillRect.offsetMax = new Vector2(-1f, -1.5f);

            // 刻度：把一条连续填充切成可读的几档
            for (int i = 1; i < ticks; i++)
            {
                float t = i / (float)ticks;
                Image tick = UIFactory.CreateImage(transform, "Tick" + i, UITheme.A(UITheme.SurfaceDeep, 0.85f));
                RectTransform r = tick.rectTransform;
                r.anchorMin = new Vector2(t, 0f);
                r.anchorMax = new Vector2(t, 1f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(1.5f, -3f);
            }

            Set(1f);
        }

        public void Set(float value)
        {
            float v = Mathf.Clamp01(value);
            fillRect.anchorMax = new Vector2(v, 1f);
        }

        public void SetColor(Color color)
        {
            fillColor = color;
            if (fill != null) fill.color = color;
        }

        /// <summary>
        /// 轨道色。默认的 SurfaceSunken 在深空底上几乎看不见，读起来像"一截亮条浮在半空"，
        /// 分不出"已经走了多少"和"总共多长"。
        /// </summary>
        public void SetTrackColor(Color color)
        {
            if (track != null) track.color = color;
        }

        /// <summary>
        /// 整体压暗：机库里未选中的三条读数要比选中的那组退后一层，
        /// 但颜色本身不能改（改色会让四条卡的强调色失去对应关系）。
        /// </summary>
        public void SetOpacity(float k)
        {
            if (fill == null) return;
            Color c = fillColor;
            c.a *= Mathf.Clamp01(k);
            fill.color = c;
        }
    }

    /// <summary>
    /// 扫光：一条很淡的亮线定时掠过面板顶部，界面因此"活"着。
    /// 周期长、亮度低，不能抢内容。
    /// </summary>
    public class UISweep : MonoBehaviour
    {
        public float period = 6.5f;
        public float travel = 1.4f;
        public float delay;
        public float maxAlpha = 0.55f;

        private Image line;
        private RectTransform lineRect;
        private float width;
        private float time;

        public static UISweep Attach(RectTransform target, Color color, float y, float lineWidth, float maxAlpha = 0.5f)
        {
            GameObject go = new GameObject("Sweep", typeof(RectTransform));
            go.transform.SetParent(target, false);

            UISweep sweep = go.AddComponent<UISweep>();
            sweep.maxAlpha = maxAlpha;

            Image img = UIFactory.CreateImage(go.transform, "Line", UITheme.A(color, 0f));
            img.sprite = UISprites.Gradient();
            img.type = Image.Type.Simple;
            UIFactory.Place(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, y), new Vector2(lineWidth, 2f));

            sweep.line = img;
            sweep.lineRect = img.rectTransform;
            return sweep;
        }

        private void OnEnable()
        {
            time = 0f;
        }

        private void Update()
        {
            if (line == null) return;

            time += Time.unscaledDeltaTime;
            float phase = Mathf.Repeat(time - delay, period);

            if (phase >= travel)
            {
                Color idle = line.color;
                idle.a = 0f;
                line.color = idle;
                return;
            }

            float t = phase / travel;
            float half = lineRect.parent is RectTransform p ? p.rect.width * 0.5f : 400f;

            Color color = line.color;
            color.a = Mathf.Sin(t * Mathf.PI) * maxAlpha;
            line.color = color;

            Vector2 pos = lineRect.anchoredPosition;
            pos.x = Mathf.Lerp(-half, half, t);
            lineRect.anchoredPosition = pos;
        }
    }

    /// <summary>呼吸：整块明暗周期变化（"按任意键"提示、待机呼吸灯）</summary>
    public class UIBreathe : MonoBehaviour
    {
        public Graphic target;
        public float speed = 1.6f;
        public float min = 0.45f;
        public float max = 1f;
        public float phase;

        private void Update()
        {
            if (target == null) return;

            float t = (Mathf.Sin(Time.unscaledTime * speed + phase) + 1f) * 0.5f;

            Color color = target.color;
            color.a = Mathf.Lerp(min, max, t);
            target.color = color;
        }
    }

    /// <summary>
    /// 缓慢上下浮动：机库里选中那架战机轻轻悬停在展台上。
    ///
    /// 幅度只有 3px、周期 4 秒——目的是让静止的卡片"活着"，不是做一个弹跳动画。
    /// 幅度再大一点，四张卡里只有一张在动，眼睛会被它勾住，反而读不出别的卡。
    /// </summary>
    public class UIFloat : MonoBehaviour
    {
        public float amplitude = 3f;
        public float speed = 1.55f;      // 弧度/秒，约 4 秒一个来回
        public float phase;

        private RectTransform rect;
        private Vector2 home;

        private void Awake()
        {
            rect = transform as RectTransform;
            if (rect != null) home = rect.anchoredPosition;
        }

        private void Update()
        {
            if (rect == null) return;
            rect.anchoredPosition = home + new Vector2(0f, Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude);
        }

        /// <summary>改完位置再调一次，让浮动以新位置为基准</summary>
        public void Rebase()
        {
            if (rect != null) home = rect.anchoredPosition;
        }
    }

    /// <summary>极慢的网格漂移（背景层用，几乎察觉不到，但截图对比能看出"在动"）</summary>
    public class UIGridDrift : MonoBehaviour
    {
        public RectTransform target;
        public Vector2 speed = new Vector2(2.5f, 4f);
        public float tile = 64f;

        private void Update()
        {
            if (target == null) return;

            Vector2 pos = target.anchoredPosition + speed * Time.unscaledDeltaTime;

            // 位移超过一格就跳回去：网格是平铺的，跳回来看不出接缝
            if (pos.x > tile) pos.x -= tile;
            if (pos.y > tile) pos.y -= tile;

            target.anchoredPosition = pos;
        }
    }
}
