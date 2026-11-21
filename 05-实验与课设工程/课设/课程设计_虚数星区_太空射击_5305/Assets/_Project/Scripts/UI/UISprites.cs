using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 程序化 UI 精灵。
    ///
    /// 上一版的界面全是「一坨纯色矩形 + 四条细边框」，圆角、切角、渐变一个都没有，
    /// 所以怎么调颜色都像控制台窗口。这里补上真正决定「质感」的几样东西：
    ///
    /// 1) Sheet —— 45° 切角面板（九宫格）。切角是这套界面最核心的形状语言：
    ///    科幻界面里没有真正的圆角，转角都被"削"掉了。
    /// 2) SheetEdge —— 同一形状的描边环，单独一张图，这样填充色和描边色可以分别调。
    /// 3) Gradient —— 竖向渐变（顶部亮、底部暗），铺在面板上就有了玻璃的厚度。
    /// 4) Grid —— 可平铺的网格，铺在面板里当底纹。
    ///
    /// 都是白色蒙版（靠 Image.color 染色），一次生成、永久缓存。
    /// Sprite 的 border 用切角尺寸，UI 里的 1 像素 = 1 设计像素（PPU 100 = Canvas referencePixelsPerUnit）。
    /// </summary>
    public static class UISprites
    {
        private const float Ppu = 100f;

        private static readonly Dictionary<int, Sprite> fillCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> edgeCache = new Dictionary<int, Sprite>();

        private static Sprite gradient;
        private static Sprite gradientH;
        private static Sprite grid;

        // ------------------------------------------------------------------ 切角面板

        /// <summary>切角面板的填充蒙版（九宫格，可任意拉伸）</summary>
        public static Sprite Sheet(float cut)
        {
            int c = Mathf.Max(3, Mathf.RoundToInt(cut));
            Sprite cached;
            if (fillCache.TryGetValue(c, out cached) && cached != null) return cached;

            Sprite sprite = BuildChamfer(c, false, 0f);
            fillCache[c] = sprite;
            return sprite;
        }

        /// <summary>切角面板的描边环（九宫格）。thickness 是描边宽度（设计像素）</summary>
        public static Sprite SheetEdge(float cut, float thickness)
        {
            int c = Mathf.Max(3, Mathf.RoundToInt(cut));
            int t = Mathf.Max(1, Mathf.RoundToInt(thickness * 2f));   // 用 1/2 像素做键，1px 和 1.5px 不会互相覆盖
            int key = c * 16 + t;

            Sprite cached;
            if (edgeCache.TryGetValue(key, out cached) && cached != null) return cached;

            Sprite sprite = BuildChamfer(c, true, t * 0.5f);
            edgeCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 生成切角矩形的蒙版。
        ///
        /// 形状 = 矩形 ∩ 四条 45° 斜切。用有符号距离场（SDF）画：
        /// 通道 alpha = 覆盖率，所以斜边是抗锯齿的，不像直接画方块那样满屏锯齿。
        /// </summary>
        private static Sprite BuildChamfer(int cut, bool ring, float thickness)
        {
            int size = cut * 2 + 4;              // 中间留 4px 给九宫格拉伸区
            int border = cut;                    // 切角正好落在 border 里
            float half = size * 0.5f;
            float k = size - cut;                // 斜切线 |x| + |y| = k

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = ring ? "UISheetEdge" + cut : "UISheet" + cut;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;

                    float rect = Mathf.Max(Mathf.Abs(px) - half, Mathf.Abs(py) - half);
                    float chamfer = (Mathf.Abs(px) + Mathf.Abs(py) - k) * 0.70710678f;
                    float dist = Mathf.Max(rect, chamfer);   // >0 在形状外

                    float alpha;
                    if (ring)
                    {
                        // 描边带：dist ∈ [-thickness, 0]
                        float band = Mathf.Min(thickness + dist, -dist) + 0.5f;
                        alpha = Mathf.Clamp01(band);
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(0.5f - dist);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), Ppu, 0,
                                 SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        // ------------------------------------------------------------------ 渐变 / 贴图

        /// <summary>竖向渐变：顶部不透明、底部半透明。铺在面板上做"上亮下暗"的玻璃感</summary>
        public static Sprite Gradient()
        {
            if (gradient != null) return gradient;

            const int w = 8;
            const int h = 128;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "UIGradient";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);            // 0 = 底，1 = 顶
                float a = Mathf.Lerp(0.05f, 1f, Mathf.Pow(t, 1.35f));
                byte alpha = (byte)Mathf.RoundToInt(a * 255f);

                for (int x = 0; x < w; x++) pixels[y * w + x] = new Color32(255, 255, 255, alpha);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            gradient = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            return gradient;
        }

        /// <summary>
        /// 横向渐变：左端不透明、右端几乎透明。
        ///
        /// 和 Gradient() 分开而不是靠旋转：旋转一张整屏 sprite 时，纹理坐标会跟着转，
        /// 渐变的"有效宽度"变成屏幕的高而不是宽，暗坡会突然在半路断掉（主菜单左边那条硬边就是这么来的）。
        /// 直接生成横向纹理，宽度就是宽度。
        /// </summary>
        public static Sprite GradientH()
        {
            if (gradientH != null) return gradientH;

            const int w = 128;
            const int h = 8;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "UIGradientH";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);            // 0 = 左，1 = 右
                float a = Mathf.Lerp(1f, 0.02f, Mathf.Pow(t, 1.6f));
                byte alpha = (byte)Mathf.RoundToInt(a * 255f);

                for (int y = 0; y < h; y++) pixels[y * w + x] = new Color32(255, 255, 255, alpha);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            gradientH = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            return gradientH;
        }


        public static Sprite Grid()
        {
            if (grid != null) return grid;

            const int n = 64;

            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.name = "UIGrid";
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;

            Color32[] pixels = new Color32[n * n];
            Color32 clear = new Color32(255, 255, 255, 0);
            Color32 line = new Color32(255, 255, 255, 34);

            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            for (int x = 0; x < n; x++)
            {
                pixels[x] = line;                 // 底边
                pixels[x * n] = line;             // 左边
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            grid = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            return grid;
        }

        /// <summary>热重载 / 关掉游戏时清缓存，避免贴图泄漏</summary>
        public static void Clear()
        {
            foreach (Sprite s in fillCache.Values) DestroySprite(s);
            foreach (Sprite s in edgeCache.Values) DestroySprite(s);
            fillCache.Clear();
            edgeCache.Clear();

            DestroySprite(gradient); gradient = null;
            DestroySprite(gradientH); gradientH = null;
            DestroySprite(grid); grid = null;
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;

            Texture2D tex = sprite.texture;
            Object.Destroy(sprite);
            if (tex != null) Object.Destroy(tex);
        }
    }
}
