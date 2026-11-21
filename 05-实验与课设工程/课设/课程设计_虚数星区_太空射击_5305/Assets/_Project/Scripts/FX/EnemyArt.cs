using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 敌人贴图的「双色调」重绘。
    ///
    /// 为什么需要它：Kenney 那批敌机是白色剪影模型，进游戏只靠 <c>SpriteRenderer.color</c> 乘一次色——
    /// 结果是四个敌人各自一整块单色，同屏时像四张不同颜色的贴纸，谈不上配色。
    /// 这里把原图按亮度重映射到「暗部色 → 亮部色」的渐变上（duotone），
    /// 面板、护罩、引擎口的明暗结构全部保留，但机身同时拥有两个色相，
    /// 近看是「橙色外壳 + 暗红酒红内衬」这种搭配，而不是"整体刷了一层漆"。
    ///
    /// 为什么用 RenderTexture 读像素：Kenney 的 png 在导入时没开 Read/Write，
    /// 直接 <c>texture.GetPixels()</c> 会抛异常；先 Blit 到临时 RT 再 ReadPixels 是唯一不需要改导入设置的路子。
    /// 每种敌人只做一次，结果按「种类 + 源图名」缓存。
    /// </summary>
    public static class EnemyArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(8);

        // 退出 Play / 重新进入时清掉静态缓存：纹理随域重载一起销毁，只留字典会指向已销毁对象
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }

        private struct Tone
        {
            public Color Shadow;
            public Color Light;
            public Color Accent;   // 高光/镜头的第二色相：和机身色相对撞，读起来才像"设计过的配色"
        }

        /// <summary>
        /// 四套双色调。暗部色比亮部色更深、更饱和，两色保持同一冷/暖族，
        /// 这样重映射后仍然是"金属+涂装"，不会像互补色那样变成霓虹塑料。
        /// </summary>
        private static Tone ToneFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Turret:
                    return new Tone
                    {
                        Shadow = new Color(0.20f, 0.075f, 0.02f, 1f),   // 深焦褐
                        Light = new Color(0.96f, 0.74f, 0.40f, 1f),    // 亮琥珀
                        Accent = new Color(0.42f, 0.76f, 1.00f, 1f),   // 钢蓝：琥珀外壳上的冷色镜头
                    };
                case EnemyKind.Tracker:
                    // 导弹就该是金属弹体：石墨灰的壳 + 一点热尾焰的暖色反光。
                    // 早先这里是紫罗兰（深靛紫 + 亮紫），本意是"科技感"，实际在深色星空里
                    // 是一块发紫的塑料——紫的饱和度压过了机体的明暗结构，看不出是金属。
                    // 灰度也刻意比陨石低一档（陨石 0.84/0.90/1.00 是冷白），
                    // 同屏时"亮的圆的是石头，暗的细长的是导弹"，靠亮度就能分开。
                    return new Tone
                    {
                        Shadow = new Color(0.105f, 0.115f, 0.135f, 1f),  // 深石墨
                        Light = new Color(0.70f, 0.74f, 0.80f, 1f),      // 中性银灰
                        Accent = new Color(1.00f, 0.58f, 0.24f, 1f),     // 热橙：弹头与尾焰反光
                    };
                case EnemyKind.Meteor:
                    return new Tone
                    {
                        Shadow = new Color(0.14f, 0.17f, 0.24f, 1f),   // 冷岩影
                        Light = new Color(0.84f, 0.90f, 1.00f, 1f),    // 冷白高光
                        Accent = new Color(0.66f, 0.84f, 1.00f, 1f),   // 冰蓝：陨石受光的边缘
                    };
                default: // Rusher
                    return new Tone
                    {
                        Shadow = new Color(0.24f, 0.035f, 0.06f, 1f),   // 暗酒红
                        Light = new Color(1.00f, 0.50f, 0.36f, 1f),    // 珊瑚亮部
                        Accent = new Color(0.55f, 0.90f, 1.00f, 1f),   // 冷青：珊瑚红机身上的眼睛与边缘反光
                    };
            }
        }

        /// <summary>取某种敌人的双色调贴图。源图传原图（不要传已经重绘过的结果）。</summary>
        public static Sprite Get(EnemyKind kind, Sprite source)
        {
            if (source == null)
                return null;

            string key = kind + ":" + (string.IsNullOrEmpty(source.name)
                ? source.GetInstanceID().ToString()
                : source.name);

            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Sprite made = Build(kind, source);
            Cache[key] = made != null ? made : source;
            return Cache[key];
        }

        /// <summary>销毁缓存里的 Sprite / Texture2D（切场景或退出 Play 时由 GameBootstrap 调用）。</summary>
        public static void Clear()
        {
            foreach (KeyValuePair<string, Sprite> kv in Cache)
            {
                Sprite sprite = kv.Value;
                if (sprite == null)
                    continue;
                Texture2D tex = sprite.texture;
                if (Application.isPlaying)
                {
                    Object.Destroy(sprite);
                    if (tex != null)
                        Object.Destroy(tex);
                }
                else
                {
                    Object.DestroyImmediate(sprite);
                    if (tex != null)
                        Object.DestroyImmediate(tex);
                }
            }
            Cache.Clear();
        }

        // ------------------------------------------------------------------ 生成

        private static Sprite Build(EnemyKind kind, Sprite src)
        {
            Texture2D srcTex = src.texture;
            if (srcTex == null)
                return null;

            int sw = srcTex.width;
            int sh = srcTex.height;
            Color[] pixels = ReadPixels(srcTex);
            if (pixels == null || pixels.Length != sw * sh)
                return null;

            Rect r = src.rect;
            int rw = Mathf.Max(1, Mathf.RoundToInt(r.width));
            int rh = Mathf.Max(1, Mathf.RoundToInt(r.height));
            int rx = Mathf.Clamp(Mathf.RoundToInt(r.x), 0, sw - rw);
            int ry = Mathf.Clamp(Mathf.RoundToInt(r.y), 0, sh - rh);

            Tone tone = ToneFor(kind);
            Vector3 shadow = new Vector3(tone.Shadow.r, tone.Shadow.g, tone.Shadow.b);
            Vector3 light = new Vector3(tone.Light.r, tone.Light.g, tone.Light.b);
            Vector3 accentTint = new Vector3(tone.Accent.r, tone.Accent.g, tone.Accent.b);

            Texture2D tex = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            tex.name = "EnemyArt_" + kind;
            tex.filterMode = srcTex.filterMode;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] outPixels = new Color[rw * rh];
            for (int y = 0; y < rh; y++)
            {
                int srcRow = (ry + y) * sw + rx;
                for (int x = 0; x < rw; x++)
                {
                    Color c = pixels[srcRow + x];
                    float a = c.a;
                    if (a <= 0.004f)
                    {
                        outPixels[y * rw + x] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    // 0 = 暗部（阴影色），1 = 亮部（涂装色）。0.10 起跳是为了让描边保持深色。
                    float t = Mathf.Clamp01((lum - 0.10f) / 0.80f);
                    t = t * t * (3f - 2f * t);
                    Vector3 col = Vector3.Lerp(shadow, light, t);

                    // 保留一点原图的结构：暗部再压深、亮部抬亮，避免重映射后变成"平平一块色"
                    col *= 0.74f + 0.54f * lum;
                    // 高光侧加一点自发光感，金属面板的边缘才亮得起来
                    col += light * (t * t * t) * 0.12f;

                    // 第二色相只落在最亮的一小撮像素（镜头、护罩边、超亮面板），
                    // 面积小才像"反光/发光件"，面积一大就变成两种颜色在打架。
                    float hi = Mathf.Clamp01((t - 0.90f) / 0.10f);
                    hi = hi * hi * (3f - 2f * hi);
                    col = Vector3.Lerp(col, accentTint, hi * 0.32f);

                    outPixels[y * rw + x] = new Color(
                        Mathf.Clamp01(col.x), Mathf.Clamp01(col.y), Mathf.Clamp01(col.z), a);
                }
            }

            tex.SetPixels(outPixels);
            tex.Apply(false, false);

            Vector2 pivot = new Vector2(
                r.width > 0f ? src.pivot.x / r.width : 0.5f,
                r.height > 0f ? src.pivot.y / r.height : 0.5f);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, rw, rh), pivot, src.pixelsPerUnit);
            sprite.name = "EnemyArt_" + kind;
            return sprite;
        }

        private static Color[] ReadPixels(Texture2D tex)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;

            Texture2D copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, tex.width, tex.height), 0, 0);
            copy.Apply(false, false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Color[] px = copy.GetPixels();
            Object.Destroy(copy);
            return px;
        }
    }
}
