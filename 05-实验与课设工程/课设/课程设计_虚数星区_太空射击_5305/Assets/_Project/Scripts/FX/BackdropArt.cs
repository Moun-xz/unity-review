using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 程序化深空底图。
    ///
    /// 为什么不用下载的星云照片了：那张照片本身信息量很大，但和「战术终端」界面
    /// 完全是两套色彩语言——照片是写实摄影调，界面是冷色低饱和 + 单点强调色。
    /// 照片铺满全屏再加上一层实心行星之后，画面既亮又杂，文字压在上面必须靠暗坡
    /// 硬压才读得清。
    ///
    /// 这里改成自己画：一条从深到浅的天幕渐变 + 两三团极低饱和的星云烟 +
    /// 一条斜向的银河尘带 + 一层星尘噪点。色板全部取自星区主色，和界面统一。
    ///
    /// 所有贴图都缓存，第一次调用时逐像素生成（768×432 / 512×512，启动时几毫秒）。
    /// </summary>
    public static class BackdropArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        private static Color Transparent { get { return new Color(0f, 0f, 0f, 0f); } }

        // ---------------------------------------------------------------- 生成核心

        private static Sprite BuildRect(string key, int width, int height,
                                        System.Func<float, float, Color> shader,
                                        System.Action<Color[], int, int> post = null)
        {
            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "Tex_" + key;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width * 2f - 1f;
                    pixels[y * width + x] = shader(u, v);
                }
            }
            if (post != null) post(pixels, width, height);
            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, width, height),
                                          new Vector2(0.5f, 0.5f), height);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        // ---------------------------------------------------------------- 星区色板

        /// <summary>星区的两团星云主色 + 银河带颜色。</summary>
        private static void Palette(int index, out Color nebA, out Color nebB, out Color band)
        {
            switch (index)
            {
                case 1: // 星区 02「赤潮」：品红 + 紫罗兰
                    nebA = new Color(0.60f, 0.20f, 0.40f, 1f);
                    nebB = new Color(0.34f, 0.14f, 0.52f, 1f);
                    band = new Color(0.58f, 0.28f, 0.44f, 1f);
                    break;
                case 2: // Boss 战：暗红 + 灰紫
                    nebA = new Color(0.64f, 0.20f, 0.26f, 1f);
                    nebB = new Color(0.30f, 0.12f, 0.36f, 1f);
                    band = new Color(0.66f, 0.30f, 0.28f, 1f);
                    break;
                default: // 星区 01「掠影带」：青 + 深蓝
                    nebA = new Color(0.12f, 0.48f, 0.62f, 1f);
                    nebB = new Color(0.14f, 0.30f, 0.66f, 1f);
                    band = new Color(0.22f, 0.52f, 0.72f, 1f);
                    break;
            }
        }

        private static Color Add(Color baseColor, Color add, float k)
        {
            if (k <= 0f) return baseColor;
            return new Color(
                Mathf.Clamp01(baseColor.r + add.r * k),
                Mathf.Clamp01(baseColor.g + add.g * k),
                Mathf.Clamp01(baseColor.b + add.b * k),
                1f);
        }

        /// <summary>椭圆高斯：一片星云烟的浓度。</summary>
        private static float Blob(float u, float v, float cx, float cy, float rx, float ry, float amp)
        {
            float x = (u - cx) / rx;
            float y = (v - cy) / ry;
            float d2 = x * x + y * y;
            return Mathf.Exp(-d2 * 1.45f) * amp;
        }

        // ---------------------------------------------------------------- 噪声

        /// <summary>
        /// 哈希值噪声。**必须和预览脚本 `_tools/bg_art.py` 用同一段常数**，
        /// 否则"我看的图"和"实机跑出来的"又是两张画——这个坑前面栽过。
        /// </summary>
        private static float Hash2(float ix, float iy, float seed)
        {
            float v = Mathf.Sin(ix * 127.1f + iy * 311.7f + seed * 74.7f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        private static float ValueNoise(float x, float y, float seed)
        {
            float x0 = Mathf.Floor(x), y0 = Mathf.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float va = Hash2(x0, y0, seed);
            float vb = Hash2(x0 + 1f, y0, seed);
            float vc = Hash2(x0, y0 + 1f, seed);
            float vd = Hash2(x0 + 1f, y0 + 1f, seed);

            float top = va + (vb - va) * fx;
            float bot = vc + (vd - vc) * fx;
            return top + (bot - top) * fy;
        }

        private static float Fbm(float x, float y, int seed, int octaves, int baseCells)
        {
            float total = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int cells = baseCells << o;
                total += ValueNoise(x * cells, y * cells, seed + o * 977) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return total / Mathf.Max(0.0001f, norm);
        }

        /// <summary>
        /// 低频噪声场：先按 1/4 分辨率算一遍，再双线性放大。
        /// 逐像素算 5 倍频 fbm 是上千万次 sin，启动会明显卡一下；这样只有 1/16 的开销，
        /// 而星云本来就是低频的，放大看不出来。
        /// </summary>
        private static float[] FbmField(int w, int h, int seed, int octaves, int baseCells)
        {
            int cw = Mathf.Max(8, w / 4);
            int ch = Mathf.Max(8, h / 4);

            float[] coarse = new float[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    coarse[y * cw + x] = Fbm((x + 0.5f) / cw, (y + 0.5f) / ch, seed, octaves, baseCells);

            float[] field = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                float gy = ((y + 0.5f) / h) * ch - 0.5f;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(gy), 0, ch - 1);
                int y1 = Mathf.Min(y0 + 1, ch - 1);
                float ty = Mathf.Clamp01(gy - y0);

                for (int x = 0; x < w; x++)
                {
                    float gx = ((x + 0.5f) / w) * cw - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, cw - 1);
                    int x1 = Mathf.Min(x0 + 1, cw - 1);
                    float tx = Mathf.Clamp01(gx - x0);

                    float va = coarse[y0 * cw + x0], vb = coarse[y0 * cw + x1];
                    float vc = coarse[y1 * cw + x0], vd = coarse[y1 * cw + x1];
                    field[y * w + x] = Mathf.Lerp(Mathf.Lerp(va, vb, tx),
                                                  Mathf.Lerp(vc, vd, tx), ty);
                }
            }
            return field;
        }

        /// <summary>和 BuildRect 相同，但把像素下标也给 shader——噪声场要按下标取值。</summary>
        private static Sprite BuildRectIndexed(string key, int width, int height,
                                               System.Func<float, float, int, int, Color> shader)
        {
            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "Tex_" + key;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width * 2f - 1f;
                    pixels[y * width + x] = shader(u, v, x, y);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, width, height),
                                          new Vector2(0.5f, 0.5f), height);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 星区的地平线/网格色调。原来这两层写死成青色，压在「赤潮」或 Boss 的
        /// 红色星区上就是一道突兀的蓝线——它们必须跟着星区走。
        /// </summary>
        public static Color HorizonTint(int sector)
        {
            Color nebA, nebB, band;
            Palette(Mathf.Clamp(sector, 0, 2), out nebA, out nebB, out band);
            return band;
        }

        // ---------------------------------------------------------------- 天幕

        /// <summary>
        /// 星区天幕：深空渐变 + 一团带内核的星云 + 暗角。
        ///
        /// 上一版有三个毛病，全部是"贴图里烤了东西"造成的：
        ///   ① 768×432 的贴图上画了 150 颗星，铺到 1920×1080 要放大 2.5 倍，
        ///      星点变成一团团发灰的方块——画面看着脏，就是它们；
        ///   ② 星尘用 u*150 的 Perlin 取窄带，缩下去是满屏椒盐噪点；
        ///   ③ 斜向"银河带"横穿画面中段，任何角度都会被读成贴图接缝。
        /// 现在：星点交给 Starfield 的 170 个精灵（清晰的点、还带视差），
        /// 贴图里只留低频渐变和星云，分辨率提到 1024×576。
        /// </summary>
        public static Sprite Sky(int sector)
        {
            int index = Mathf.Clamp(sector, 0, 2);
            const int W = 1024, H = 576;
            string key = "SkyV2_" + index;

            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            Color nebA, nebB, band;
            Palette(index, out nebA, out nebB, out band);

            // base 3 而不是 2：基频太低时云团边缘是一整条光滑的弧线，
            // 在深色背景上会被眼睛读成"一道横向亮痕"（马赫带）。提一档就碎开了。
            float[] cloud = FbmField(W, H, 300 + index, 5, 3);

            return BuildRectIndexed(key, W, H, delegate(float u, float v, int px, int py)
            {
                float t = v * 0.5f + 0.5f;

                Color deep = new Color(0.004f, 0.006f, 0.014f, 1f);
                Color mid = new Color(0.009f, 0.015f, 0.031f, 1f);
                Color high = new Color(0.018f, 0.028f, 0.052f, 1f);

                Color c = t > 0.38f
                    ? Color.Lerp(mid, high, Mathf.Clamp01((t - 0.38f) / 0.62f))
                    : Color.Lerp(deep, mid, Mathf.Clamp01(t / 0.38f));

                // 云团浓度：对比必须拉开。均匀的 0.6~1.3 只是一层灰雾，
                // 拉到 0.1~2.4 才有"云"的密度差。
                float nn = cloud[py * W + px];
                float turb = Mathf.Pow(Mathf.Clamp(0.10f + 0.62f * 3.4f * nn, 0f, 2.4f), 1.25f);

                float fa = Blob(u, v, -0.66f, 0.26f, 0.62f, 0.48f, 1f) * turb;
                float faCore = Blob(u, v, -0.58f, 0.18f, 0.23f, 0.22f, 1f) * turb;
                float fb = Blob(u, v, -0.14f, -0.56f, 0.52f, 0.36f, 1f) * turb;
                float fc = Blob(u, v, 0.76f, -0.06f, 0.44f, 0.40f, 0.85f) * turb;
                float fd = Blob(u, v, 0.30f, 0.62f, 0.34f, 0.26f, 0.70f) * turb;

                c = Add(c, nebA, (fa * 0.30f + faCore * 0.26f + fb * 0.22f) * 1.15f);
                c = Add(c, nebB, (fc * 0.30f + fd * 0.18f + fa * 0.10f) * 1.15f);

                float rad = Mathf.Sqrt(u * u + v * v);
                float vig = Mathf.Clamp01(1f - 0.50f * Mathf.Pow(Mathf.Clamp01((rad - 0.40f) / 0.90f), 1.5f));
                c = new Color(c.r * vig, c.g * vig, c.b * vig, 1f);
                return c;
            });
        }

        // ---------------------------------------------------------------- 远景恒星星云薄雾

        /// <summary>
        /// 星云薄雾层：叠在天幕之上、行星之下的一层很淡的彩色烟，慢慢飘动。
        /// 天幕是静止的贴图，这层负责"画面在呼吸"——它比天幕亮，但 alpha 只到 0.3 左右，
        /// 不会把战斗区压暗，也不会和 HUD 抢注意力。
        /// </summary>
        public static Sprite Wisps(int sector)
        {
            int index = Mathf.Clamp(sector, 0, 2);
            string key = "Wisps_" + index;

            Color nebA, nebB, band;
            Palette(index, out nebA, out nebB, out band);

            return BuildRect(key, 1024, 576, delegate(float u, float v)
            {
                float turb = 0.62f + 0.38f * Mathf.Sin(u * 4.1f + v * 2.7f) * Mathf.Sin(u * 1.7f - v * 6.3f);

                // 三条细长的烟带：左上、右下、顶部各一条，中间留空给战斗
                float w1 = Blob(u, v, -0.66f, 0.30f, 1.10f, 0.26f, 1f) * turb;
                float w2 = Blob(u, v, 0.58f, -0.28f, 0.92f, 0.24f, 0.90f) * turb;
                float w3 = Blob(u, v, 0.12f, 0.66f, 0.62f, 0.20f, 0.62f) * turb;

                float total = w1 + w2 + w3;
                if (total < 0.0001f) return Transparent;

                Color col = Color.Lerp(nebB, nebA, w1 / total);
                col.a = Mathf.Clamp01(total * 0.34f);
                return col;
            });
        }

        // ---------------------------------------------------------------- 行星

        /// <summary>
        /// 远景气态巨行星：倾斜云带 + 硬明暗交界 + 临边昏暗 + 受光侧细亮弧 + 一层薄大气。
        ///
        /// 上一版是"一个平滑渐变的青球"——没有云带、没有临边压暗，缩到 440 像素就是一张贴纸。
        /// 现在四件事一起做：云带给出表面细节，临边压暗给出球感，明暗交界给出光源方向，
        /// 大气只在受光侧亮起来给出体积。环试过，在这个尺寸下细椭圆横穿球面会被读成划痕，撤掉了。
        /// </summary>
        public static Sprite Planet(int sector)
        {
            int index = Mathf.Clamp(sector, 0, 2);
            const int S = 640;
            string key = "SkyPlanetV2_" + index;

            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            Color lit, rim;
            switch (index)
            {
                case 1:
                    lit = new Color(0.52f, 0.24f, 0.38f, 1f);
                    rim = new Color(1f, 0.56f, 0.86f, 1f);
                    break;
                case 2:
                    lit = new Color(0.56f, 0.26f, 0.28f, 1f);
                    rim = new Color(1f, 0.62f, 0.54f, 1f);
                    break;
                default:
                    lit = new Color(0.22f, 0.48f, 0.58f, 1f);
                    rim = new Color(0.48f, 0.96f, 1f, 1f);
                    break;
            }

            Vector3 L = new Vector3(-0.52f, 0.40f, 0.75f).normalized;
            float tiltCos = Mathf.Cos(-14f * Mathf.Deg2Rad);
            float tiltSin = Mathf.Sin(-14f * Mathf.Deg2Rad);

            float[] fine = FbmField(S, S, 71 + index * 13, 5, 3);
            float[] wide = FbmField(S, S, 913 + index, 3, 2);

            Color dark = new Color(0.003f, 0.005f, 0.012f, 1f);
            Color toneA = lit * 0.62f;
            Color toneB = new Color(Mathf.Clamp01(lit.r * 1.10f + 0.05f),
                                    Mathf.Clamp01(lit.g * 1.10f + 0.05f),
                                    Mathf.Clamp01(lit.b * 1.10f + 0.05f), 1f);

            return BuildRectIndexed(key, S, S, delegate(float u, float v, int px, int py)
            {
                int i = py * S + px;
                float r = Mathf.Sqrt(u * u + v * v);

                // ---- 球体
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - r * r));
                float ndl = u * L.x + v * L.y + z * L.z;

                float term = Mathf.Clamp01((ndl + 0.03f) / 0.22f);
                term = term * term * (3f - 2f * term);

                // 云带：带数要少、边缘要碎。
                // 上一版 phase 系数 22 → 整颗球切成 11 条等距硬边，远看就是一片百叶窗；
                // 这里减到 11 条（相位跨度 ≈ 5.5 个周期），再用两层噪声把带边打碎，
                // 并且让纬度越高的地方带越淡 —— 真实气态行星的带在极区是收拢、模糊掉的。
                float py2 = v * tiltCos - u * tiltSin;
                float lat = Mathf.Asin(Mathf.Clamp(py2, -1f, 1f));
                float phase = lat * 11f + (fine[i] - 0.5f) * 3.4f + wide[i] * 1.2f + u * 0.35f;
                float bands = 0.5f + 0.5f * Mathf.Sin(phase);
                bands *= 0.70f + 0.45f * wide[i];
                bands = Mathf.Lerp(0.5f, Mathf.Clamp01(bands), 1f - Mathf.Abs(py2) * 0.55f);
                bands = Mathf.Pow(Mathf.Clamp01(bands), 0.95f);

                Color surf = Color.Lerp(toneA, toneB, bands);

                // 两颗风暴斑
                surf = Spot(surf, u, v, -0.34f, -0.14f, 0.17f, 0.09f, 0.55f);
                surf = Spot(surf, u, v, 0.30f, 0.34f, 0.12f, 0.07f, 0.30f);

                Color body = Color.Lerp(dark, surf, term * 0.96f);
                body = body * (0.34f + 0.66f * Mathf.Pow(Mathf.Clamp01(z), 0.5f));

                // 受光侧贴边的细亮弧
                float arc = Mathf.Pow(Mathf.Clamp01((r - 0.88f) / 0.12f), 2.2f)
                            * Mathf.Clamp01((ndl + 0.02f) / 0.42f) * 0.85f;
                body = Color.Lerp(body, rim, arc);

                if (r > 1f)
                {
                    // ---- 大气：只在受光侧亮起来
                    const float atmR = 1.045f;
                    if (r > atmR) { body.a = 0f; return body; }
                    float fall = Mathf.Pow(Mathf.Clamp01((atmR - r) / (atmR - 1f)), 2f);
                    float side = Mathf.Clamp01((u * L.x + v * L.y) / 0.80f + 0.50f);
                    Color atm = rim;
                    atm.a = fall * side * 0.50f;
                    return atm;
                }

                body.a = 1f;
                return body;
            });
        }

        private static Color Spot(Color surf, float u, float v, float cx, float cy,
                                  float rx, float ry, float k)
        {
            float dx = (u - cx) / rx;
            float dy = (v - cy) / ry;
            float spot = Mathf.Exp(-(dx * dx + dy * dy) * 2.4f) * k;
            if (spot <= 0.002f) return surf;
            Color hi = new Color(Mathf.Clamp01(surf.r * 1.45f + 0.10f),
                                 Mathf.Clamp01(surf.g * 1.45f + 0.10f),
                                 Mathf.Clamp01(surf.b * 1.45f + 0.10f), 1f);
            return Color.Lerp(surf, hi, spot);
        }

        // ---------------------------------------------------------------- 清理

        /// <summary>缓存里全是运行时创建的 Texture2D，退出播放时统一销毁，避免泄漏。</summary>
        public static void Clear()
        {
            foreach (KeyValuePair<string, Sprite> pair in cache)
            {
                Sprite sprite = pair.Value;
                if (sprite == null) continue;
                Texture2D tex = sprite.texture;
                Object.Destroy(sprite);
                if (tex != null) Object.Destroy(tex);
            }
            cache.Clear();
        }
    }
}
