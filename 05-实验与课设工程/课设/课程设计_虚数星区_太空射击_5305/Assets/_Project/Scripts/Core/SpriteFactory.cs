using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 程序化贴图工厂。
    ///
    /// 为什么不用美术资源包：本项目要求「零外部依赖、打开即跑」。
    /// 所有飞船、敌机、子弹、光晕都在运行时用数学函数画出来，
    /// 好处是风格绝对统一、不涉及任何素材版权、也不会因为贴图导入设置错而导致粉块。
    /// 之后如果想换成正式美术，只要把各工厂方法里的 SpriteFactory.Xxx() 换成 Resources.Load 即可。
    /// </summary>
    public static class SpriteFactory
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static Texture2D glowTexture;

        /// <summary>
        /// 关掉域重载（Enter Play Mode Options）时静态字段不会复位：
        /// 上一轮生成的 Sprite 还留在 cache 里，但它们的纹理已经随播放模式结束被销毁，
        /// 第二次 Play 拿到的就是一堆 MissingReference。进播放模式前清一次。
        ///
        /// ArtLibrary / ShipArt / EnemyArt 都有同样的钩子，这里之前漏了。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache.Clear();
            glowTexture = null;
        }

        /// <summary>粒子系统使用的光晕贴图（与 Glow() 共用同一张）</summary>
        public static Texture2D GlowTexture
        {
            get
            {
                if (glowTexture == null)
                {
                    Sprite s = Glow();
                    glowTexture = s != null ? s.texture : Texture2D.whiteTexture;
                }
                return glowTexture;
            }
        }

        // ---------------------------------------------------------------- 生成核心

        /// <summary>
        /// 根据一个「像素着色函数」生成 Sprite。
        /// shader(u,v) 接收 [-1,1] 的归一化坐标，返回该点的颜色，alpha=0 表示透明。
        /// ppu 取 size，保证每张贴图都在世界坐标里是 1×1 单位，缩放交给 transform.localScale。
        /// </summary>
        private static Sprite Build(string key, int size, Func<float, float, Color> shader)
        {
            return BuildRect(key, size, size, shader);
        }

        /// <summary>
        /// 非正方形贴图的版本。
        ///
        /// ppu 默认取 height，让贴图在世界里恒为 1 单位高（宽高比由像素数自然决定）。
        /// 需要「和 png 导入设置（100 ppu）对齐」的时候显式传 pixelsPerUnit：
        /// 同一张图有「png 优先、程序化兜底」两条路径，两条路径在屏幕上必须一样大，
        /// 否则一旦 png 丢了，回退出来的图会突然变大 1.5 倍。
        /// </summary>
        private static Sprite BuildRect(string key, int width, int height, Func<float, float, Color> shader,
                                        float pixelsPerUnit = 0f)
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
            tex.SetPixels(pixels);
            tex.Apply();

            float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : height;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), ppu);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        private static Color Clear { get { return new Color(0f, 0f, 0f, 0f); } }

        /// <summary>边缘抗锯齿：距离边界还剩多少，映射到 0~1 的 alpha</summary>
        private static float EdgeAlpha(float inside, float falloff)
        {
            return Mathf.Clamp01(inside / Mathf.Max(0.0001f, falloff));
        }

        // ---------------------------------------------------------------- 玩家飞船

        /// <summary>
        /// 玩家战机：细长机身 + 三角后掠翼 + 鸭翼 + 尾翼，深色描边勾轮廓。
        ///
        /// 上一版看起来「像一块糊掉的蓝斑」，原因是只有一个连续公式 + 大范围渐变，没有部件边界。
        /// 这一版把轮廓拆成四段（机身 / 主翼 / 鸭翼 / 尾翼），每个像素先判断属于哪个部件，
        /// 再按部件上色，最后压一圈深色描边把剪影从星空里拉出来。
        /// </summary>
        public static Sprite PlayerShip()
        {
            Color outline = new Color(0.02f, 0.05f, 0.13f, 1f);
            Color hullDark = new Color(0.06f, 0.10f, 0.20f, 1f);
            Color hullMid = new Color(0.15f, 0.30f, 0.50f, 1f);
            Color accent = new Color(0.24f, 0.80f, 0.97f, 1f);
            Color accentHot = new Color(0.82f, 0.99f, 1f, 1f);
            Color glassHot = new Color(0.88f, 1f, 1f, 1f);
            Color glassDeep = new Color(0.20f, 0.68f, 0.95f, 1f);
            Color engineHot = new Color(1f, 0.62f, 0.28f, 1f);

            return Build("PlayerShip", 256, delegate(float u, float v)
            {
                float au = Mathf.Abs(u);

                // ---- 轮廓：每个纵坐标处的最大半宽（v = +1 是机头）
                float hull;
                if (v > 0.70f) hull = Mathf.Lerp(0.125f, 0.008f, (v - 0.70f) / 0.30f);
                else if (v > 0.08f) hull = Mathf.Lerp(0.195f, 0.125f, (v - 0.08f) / 0.62f);
                else if (v > -0.56f) hull = Mathf.Lerp(0.195f, 0.170f, (0.08f - v) / 0.64f);
                else if (v > -0.92f) hull = Mathf.Lerp(0.170f, 0.060f, (-0.56f - v) / 0.36f);
                else hull = 0f;

                // 主翼：前缘张开到最宽，再沿后缘收回
                float wing = 0f;
                if (v <= 0.36f && v >= -0.06f) wing = Mathf.Lerp(0.20f, 0.94f, (0.36f - v) / 0.42f);
                else if (v < -0.06f && v >= -0.68f) wing = Mathf.Lerp(0.94f, 0.24f, (-0.06f - v) / 0.62f);

                // 鸭翼：机头两侧的小前翼
                float canard = 0f;
                if (v <= 0.52f && v >= 0.34f) canard = Mathf.Lerp(0.46f, 0.20f, (v - 0.34f) / 0.18f);

                // 尾翼
                float tail = 0f;
                if (v <= -0.60f && v >= -0.94f) tail = Mathf.Lerp(0.34f, 0.09f, (-0.60f - v) / 0.34f);

                float half = Mathf.Max(Mathf.Max(hull, wing), Mathf.Max(canard, tail));
                if (half < 0.004f) return Clear;

                float edge = half - au;
                if (edge < 0f) return Clear;

                // 硬边 + 极窄过渡：像素风剪影要清晰，不能糊
                float alpha = Mathf.Clamp01(edge / 0.010f);
                Color c;

                if (edge < 0.028f)
                {
                    c = outline;
                }
                else if (au > hull + 0.006f)
                {
                    // 翼面：靠机身亮、翼尖暗，沿展向压几条装饰细纹
                    float span = Mathf.Clamp01((au - hull) / Mathf.Max(0.001f, half - hull));
                    c = Color.Lerp(hullMid, hullDark, span * 0.9f);
                    if (Mathf.Repeat(span * 3.4f, 1f) < 0.10f) c = Color.Lerp(c, accent, 0.45f);
                    if (span > 0.86f) c = Color.Lerp(c, accentHot, (span - 0.86f) / 0.14f * 0.9f);
                }
                else
                {
                    float t = au / Mathf.Max(0.001f, hull);
                    c = Color.Lerp(hullMid, hullDark, t * t * 0.85f);
                }

                // 座舱：亮玻璃 + 深色窗框
                if (au < 0.105f && v > 0.12f && v < 0.58f)
                {
                    float t = au / 0.105f;
                    c = t > 0.82f ? outline : Color.Lerp(glassHot, glassDeep, t * t);
                }

                // 中轴高光
                if (au < 0.016f && v > -0.52f && v < 0.70f) c = Color.Lerp(c, accentHot, 0.8f);

                // 双引擎喷口
                if (v < -0.70f)
                {
                    float dr = Mathf.Min(
                        Mathf.Sqrt((u - 0.095f) * (u - 0.095f) + (v + 0.84f) * (v + 0.84f)),
                        Mathf.Sqrt((u + 0.095f) * (u + 0.095f) + (v + 0.84f) * (v + 0.84f)));
                    if (dr < 0.062f) c = Color.Lerp(engineHot, accentHot, dr / 0.062f);
                }

                c.a = alpha;
                return c;
            });
        }

        /// <summary>飞船尾部推进器光斑（叠在机身下面）</summary>
        public static Sprite EngineGlow()
        {
            Color hot = new Color(0.55f, 0.9f, 1f, 1f);
            return Build("EngineGlow", 64, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v * 0.35f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                Color c = Color.Lerp(hot, new Color(0.1f, 0.5f, 1f, 1f), Mathf.Clamp01(r));
                c.a = a * 0.9f;
                return c;
            });
        }

        // ---------------------------------------------------------------- 敌人

        public static Sprite Enemy(EnemyKind kind, Color tint)
        {
            string key = "Enemy_" + kind;
            Color edge = Color.Lerp(tint, Color.white, 0.55f);
            Color dark = Color.Lerp(tint, Color.black, 0.45f);

            if (kind == EnemyKind.Rusher)
            {
                // 倒三角 + 尾翼，视觉上就是「俯冲撞击」
                return Build(key, 96, delegate(float u, float v)
                {
                    float ay = Mathf.Abs(u);
                    float half = -1f;
                    if (v > -0.95f && v < 0.25f) half = (v + 0.95f) * 0.62f;
                    if (v >= 0.25f && v < 0.85f) half = 0.14f + (0.85f - v) * 0.5f;

                    if (half < 0f) return Clear;
                    float alpha = EdgeAlpha(half - ay, 0.05f);
                    if (alpha <= 0f) return Clear;

                    float t = Mathf.Clamp01(ay / Mathf.Max(0.001f, half));
                    Color c = Color.Lerp(dark, edge, t);
                    if (Mathf.Abs(v + 0.1f) < 0.06f) c = Color.Lerp(c, Color.white, 0.8f);
                    c.a = alpha;
                    return c;
                });
            }

            if (kind == EnemyKind.Turret)
            {
                // 六边形炮台，带中心核心
                return Build(key, 96, delegate(float u, float v)
                {
                    float d = Mathf.Abs(u) * 0.78f + Mathf.Abs(v) * 0.78f;
                    // 用切角把菱形变成六边形
                    float cut = Mathf.Max(Mathf.Abs(u) * 0.95f, Mathf.Abs(v) * 0.95f) * 0.55f;
                    float shape = Mathf.Min(d, cut);
                    float alpha = EdgeAlpha(0.86f - shape, 0.05f);
                    if (alpha <= 0f) return Clear;

                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c = Color.Lerp(edge, dark, Mathf.Clamp01(r / 0.85f));
                    if (r < 0.22f) c = Color.Lerp(new Color(1f, 0.95f, 0.9f, 1f), edge, r / 0.22f);
                    c.a = alpha;
                    return c;
                });
            }

            // Tracker：圆形追踪雷，外圈带三个尖刺
            return Build(key, 96, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float alpha = EdgeAlpha(0.72f - r, 0.05f);
                float spike = EdgeAlpha(0.95f - r, 0.02f) * (Mathf.Abs(Mathf.Sin(Mathf.Atan2(v, u) * 1.5f)) > 0.82f ? 1f : 0f);
                alpha = Mathf.Max(alpha, spike * 0.85f);
                if (alpha <= 0f) return Clear;

                Color c = Color.Lerp(edge, dark, Mathf.Clamp01(r / 0.9f));
                if (r < 0.26f) c = Color.Lerp(Color.white, edge, r / 0.26f);
                c.a = alpha;
                return c;
            });
        }

        // ---------------------------------------------------------------- 陨石

        /// <summary>
        /// 陨石（8 种轮廓由 seed 决定）。
        ///
        /// 前几版都翻车，问题记在这儿免得再犯：
        /// 1. 半径只叠 sin(3θ)/sin(5θ)/sin(9θ)，三瓣对称 → 转起来像四叶草、像蝴蝶。
        /// 2. 三个大圆坑摆在中心附近、半径相近 → 两个亮点并排，整颗石头变成一张脸。
        /// 3. 坑缘围一圈均匀亮边 = 描边，越描越像贴纸。
        /// 4. 边缘光用青色 (0.40, 0.86, 1.0)——那是霓虹的颜色，贴在星云上像「发光塑料」。
        /// 5. 八颗都固定 8 个半平面、同一个噪点幅度，缩到实际尺寸后彼此太像。
        ///
        /// 这一版：
        /// · 轮廓走「6~9 个半平面求交」，数量由 seed 决定，得到边数不同的多边形碎块，
        ///   再叠极小的低频噪声把棱角磨毛——直边才是石头，圆边是气球。半平面朝向必须
        ///   均匀铺开再抖动，否则会留出 100° 的空档，那里算出来的半径会爆成尖刺。
        /// · 明暗用 smoothstep 拉出清晰的明暗交界线，暗面压到接近黑，亮面才立得住。
        /// · 坑改成椭圆（长宽比、朝向都由 seed 定），且只有坑内壁「朝光的那半圈」亮、
        ///   另一半落暗。这是凹坑该有的明暗关系，也是它和「两只眼睛」的区别。
        /// · 加两条短裂纹（高斯窗截断长度 + 沿线正弦扰动），石头才有被撞碎的性格。
        /// · 表面颗粒从两段加到三段（斑块 / 碎屑 / 砂），高频权重加大，才不像塑料。
        /// · 边缘光转冷灰蓝、收窄到 0.032、强度降到 0.55，只负责把轮廓从黑底上拎出来。
        /// </summary>
        public static Sprite Meteor(int seed)
        {
            string key = "Meteor_" + seed;

            float elongAxis = Seed(seed, 1) * 6.2831f;
            float elong = 0.05f + 0.21f * Seed(seed, 2);      // 区间放宽：有的近乎圆、有的明显扁长
            float a1 = Seed(seed, 3) * 6.2831f;
            float a2 = Seed(seed, 4) * 6.2831f;
            float a3 = Seed(seed, 5) * 6.2831f;

            // 半平面数量 6~9 由 seed 决定。边数一变，棱的折数就变，
            // 八颗石头才不会像同一个模子倒出来的——之前固定 8 个平面，形状太像。
            int planeCount = 6 + (int)(Seed(seed, 91) * 3.999f);
            float[] planePhi = new float[planeCount];
            float[] planeDist = new float[planeCount];
            for (int k = 0; k < planeCount; k++)
            {
                planePhi[k] = k * 6.2831f / planeCount
                            + (Seed(seed, 100 + k) - 0.5f) * (6.2831f / planeCount) * 0.75f;
                planeDist[k] = 0.36f + 0.24f * Seed(seed, 120 + k);
            }

            // 三处窄凹陷：石头被撞掉一块的样子
            const int dentCount = 3;
            float[] dentAng = new float[dentCount];
            float[] dentW = new float[dentCount];
            float[] dentDep = new float[dentCount];
            for (int k = 0; k < dentCount; k++)
            {
                dentAng[k] = Seed(seed, 10 + k) * 6.2831f;
                dentW[k] = 0.16f + 0.14f * Seed(seed, 20 + k);
                dentDep[k] = 0.040f + 0.055f * Seed(seed, 30 + k);
            }

            // 四处陨石坑：椭圆、各自朝向
            const int pitCount = 4;
            float[] pitX = new float[pitCount];
            float[] pitY = new float[pitCount];
            float[] pitRa = new float[pitCount];
            float[] pitRb = new float[pitCount];
            float[] pitAng = new float[pitCount];
            for (int k = 0; k < pitCount; k++)
            {
                float pa = Seed(seed, 40 + k) * 6.2831f;
                float pd = 0.06f + 0.28f * Seed(seed, 50 + k);
                pitX[k] = Mathf.Cos(pa) * pd;
                pitY[k] = Mathf.Sin(pa) * pd;
                pitRa[k] = 0.045f + 0.062f * Seed(seed, 60 + k);
                pitRb[k] = pitRa[k] * (0.48f + 0.30f * Seed(seed, 70 + k));
                pitAng[k] = Seed(seed, 80 + k) * 3.1416f;
            }

            // 两条裂纹。贯穿整块的直线只会被读成「镜头上划了一道」，
            // 所以长度用高斯窗截断、走向用沿线的正弦扰动，才像裂开的。
            const int crackCount = 2;
            float[] crackX = new float[crackCount];
            float[] crackY = new float[crackCount];
            float[] crackAng = new float[crackCount];
            float[] crackW = new float[crackCount];
            for (int k = 0; k < crackCount; k++)
            {
                crackAng[k] = Seed(seed, 150 + k) * 3.1416f;
                crackX[k] = (Seed(seed, 160 + k) - 0.5f) * 0.34f;
                crackY[k] = (Seed(seed, 170 + k) - 0.5f) * 0.34f;
                crackW[k] = 0.0055f + 0.0040f * Seed(seed, 180 + k);
            }

            // 先把 720 个方向扫一遍取最大半径，用来把每颗石头归一到同样的外接尺寸。
            // 不归一的话有的石头只有别人的三分之二大，同一波看起来像两种敌人。
            float maxRadius = 0.0001f;
            for (int i = 0; i < 720; i++)
            {
                float th = i * 6.2831f / 720f;
                float rr = MeteorRadius(th, planePhi, planeDist, elong, elongAxis, a1, a3,
                                        dentAng, dentW, dentDep);
                if (rr > maxRadius) maxRadius = rr;
            }
            float norm = 0.62f / maxRadius;

            Color rock = new Color(0.072f, 0.082f, 0.125f, 1f);
            Color bright = new Color(0.700f, 0.750f, 0.860f, 1f);
            Color dim = new Color(0.010f, 0.013f, 0.026f, 1f);
            // 原来是 (0.40, 0.86, 1.0) 的青色边缘光——那是「霓虹」，不是石头。
            // 换成冷灰蓝之后，边缘只负责把轮廓从黑底上拎出来，不再负责发光。
            Color rimColor = new Color(0.40f, 0.50f, 0.64f, 1f);
            Vector2 light = new Vector2(-0.55f, 0.72f).normalized;

            // 256 而不是 128：最大的陨石在屏幕上能到 300 像素，
            // 128 的贴图放大两倍多会糊成一团，棱边全没了。
            return Build(key, 256, delegate(float u, float v)
            {
                float theta = Mathf.Atan2(v, u);
                float r = Mathf.Sqrt(u * u + v * v);
                float radius = MeteorRadius(theta, planePhi, planeDist, elong, elongAxis, a1, a3,
                                            dentAng, dentW, dentDep) * norm;

                float alpha = EdgeAlpha(radius - r, 0.020f);
                if (alpha <= 0f) return Clear;

                // 把碎块当半球打光：法线的 xy 取归一化坐标，z 由 1-r² 补
                float safe = Mathf.Max(0.001f, radius);
                float nr = Mathf.Clamp01(r / safe);
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nr * nr));
                float nx = u / safe;
                float ny = v / safe;
                float facing = Mathf.Clamp01(nx * light.x + ny * light.y);
                float raw = facing * 0.80f + nz * 0.42f;

                // 硬过渡：把中间灰阶压掉，亮面和暗面之间才有明确的明暗交界线。
                // 均匀发灰是「一团黏土」的根源，不是颜色选得不对。
                float t = Mathf.Clamp01((raw - 0.24f) / 0.74f);
                t = t * t * (3f - 2f * t);

                Color c = Color.Lerp(rock, bright, t);

                // 石面颗粒：三段——低频是斑块、中频是碎屑、高频是砂。
                // 高频权重从 0.38 提到 0.55、幅度从 0.32 提到 0.40：
                // 原来那点噪点缩到实际尺寸根本看不见，所以只剩「光滑」。
                float grain = 0.30f * Hash(u * 3.2f + a1, v * 3.2f + a2)
                            + 0.42f * Hash(u * 11.0f + a3, v * 11.0f + a1)
                            + 0.28f * Hash(u * 29.0f + a2, v * 29.0f + a3);
                c *= 0.80f + grain * 0.40f;

                // 裂纹：先压暗，再在受光一侧补一道细高光（缝的一边是阴影、一边是棱）
                for (int i = 0; i < crackCount; i++)
                {
                    float dx = u - crackX[i];
                    float dy = v - crackY[i];
                    float ca = Mathf.Cos(crackAng[i]);
                    float sa = Mathf.Sin(crackAng[i]);
                    float along = dx * ca + dy * sa;
                    float perp = (-dx * sa + dy * ca)
                               + 0.013f * Mathf.Sin(along * 24f + crackX[i] * 9f);
                    float len = Mathf.Exp(-(along / 0.17f) * (along / 0.17f));
                    float inside = Mathf.Clamp01((radius - r) / 0.10f);
                    float cw = crackW[i];

                    float body = Mathf.Clamp01(1f - (Mathf.Abs(perp) - cw) / cw) * inside * len;
                    body *= 0.45f + 0.55f * Hash(along * 9f, perp * 3f);
                    c *= 1f - body * 0.58f;

                    float hl = Mathf.Clamp01(1f - (Mathf.Abs(perp + cw * 2.6f) - cw * 0.50f) / (cw * 0.50f))
                             * inside * len;
                    c = Color.Lerp(c, bright, hl * 0.20f);
                }

                // 陨石坑
                for (int i = 0; i < pitCount; i++)
                {
                    float dx = u - pitX[i];
                    float dy = v - pitY[i];
                    float ca = Mathf.Cos(pitAng[i]);
                    float sa = Mathf.Sin(pitAng[i]);
                    float lx = dx * ca + dy * sa;          // 坑的长轴方向
                    float ly = -dx * sa + dy * ca;
                    float q = Mathf.Sqrt((lx / pitRa[i]) * (lx / pitRa[i])
                                       + (ly / pitRb[i]) * (ly / pitRb[i]));
                    if (q > 2f) continue;

                    // 坑底整体压暗，越靠中心越暗
                    if (q < 1f)
                    {
                        float k = 1f - q;
                        c = Color.Lerp(c, dim, k * 0.60f);
                    }

                    // 坑壁：内壁朝光的那半圈亮、背光的半圈落暗。
                    // 「眼睛」和「凹坑」的差别全在这两句——圆的对称亮边只会被读成瞳孔。
                    float wnx = lx / pitRa[i];
                    float wny = ly / pitRb[i];
                    float wl = Mathf.Sqrt(wnx * wnx + wny * wny) + 0.000001f;
                    float faceWall = -(wnx * light.x + wny * light.y) / wl;   // > 0 = 内壁朝光
                    float wallT = (q - 0.92f) / 0.30f;
                    float wall = Mathf.Exp(-wallT * wallT);
                    float brightWall = Mathf.Pow(Mathf.Clamp01(faceWall), 1.3f) * wall;
                    float darkWall = Mathf.Pow(Mathf.Clamp01(-faceWall), 1.3f) * wall;
                    c = Color.Lerp(c, bright, brightWall * 0.30f);
                    c = Color.Lerp(c, dim, darkWall * 0.34f);
                }

                // 轮廓内侧压一圈暗边，石头才有厚度、不像剪纸
                float edgeDark = Mathf.Clamp01(1f - (radius - r) / 0.070f);
                c *= 1f - edgeDark * 0.44f;

                // 边缘光：宽度 0.050 → 0.032、强度 0.75 → 0.55、颜色转冷。
                // 而且必须跟着光走——全周一圈均匀亮边是「描边」，只会让石头变成贴纸。
                float rim = Mathf.Clamp01((r - radius + 0.032f) / 0.032f);
                rim = Mathf.Pow(rim, 1.5f) * (0.22f + 0.78f * facing);
                c = Color.Lerp(c, rimColor, rim * 0.55f);

                c.a = alpha;
                return c;
            });
        }

        /// <summary>
        /// 陨石在某个方向上的半径。半平面求交 → 多边形，再拉长、磨毛、挖两处凹陷。
        /// 单独抽出来是因为着色函数里要用，预扫最大半径时也要用，两边必须同一套公式。
        /// </summary>
        private static float MeteorRadius(float theta, float[] phi, float[] dist,
                                          float elong, float elongAxis, float a1, float a3,
                                          float[] dentAng, float[] dentW, float[] dentDep)
        {
            float radius = 9f;
            for (int i = 0; i < phi.Length; i++)
            {
                float c = Mathf.Cos(theta - phi[i]);
                // cos 太小的地方这个平面根本限不住（方向背对法线），不能参与 min
                if (c > 0.20f) radius = Mathf.Min(radius, dist[i] / Mathf.Max(c, 0.20f));
            }

            // 拉长成不规则长条，碎块本来就是扁的
            radius *= 1f + elong * Mathf.Cos(2f * (theta - elongAxis));

            // 棱角磨毛一点，别像切出来的多面体；幅度很小，直边仍然是直边
            radius *= 1f + 0.030f * Mathf.Sin(theta * 3f + a1) * Mathf.Sin(theta * 2f - a3);

            for (int i = 0; i < dentAng.Length; i++)
            {
                float d = Mathf.Repeat(theta - dentAng[i] + Mathf.PI, 6.2831f) - Mathf.PI;
                radius -= dentDep[i] * Mathf.Exp(-(d / dentW[i]) * (d / dentW[i]));
            }

            return radius;
        }

        /// <summary>
        /// 由 seed 推出来的确定性伪随机（0~1）。同一个 seed 永远出同一颗陨石。
        /// 用 double 算：浮点在乘 43758 之后小数位会失真，取整部分一变形状就完全不同。
        /// </summary>
        private static float Seed(int seed, int k)
        {
            double h = Math.Sin(seed * 12.9898 + k * 78.233) * 43758.5453;
            return (float)(h - Math.Floor(h));
        }


        /// <summary>
        /// 边界警戒带：横向 alpha 从陡到平的一条纯白渐变，颜色交给 Image.color 上。
        /// 左边界直接用，右边界把 localScale.x 取负镜像——一张图两条边，不用做两张。
        ///
        /// 指数 1.9 而不是线性的原因：线性渐变在 76px 里会「整条均匀发红」，
        /// 看起来像画框；指数越大越贴边，露出来的只有紧贴屏幕外沿的一窄条，
        /// 才像「再往外就是禁区」而不是「这里加了装饰」。
        /// </summary>
        public static Sprite EdgeGradient()
        {
            return BuildRect("EdgeGradient", 128, 8, delegate(float u, float v)
            {
                float t = Mathf.Clamp01((u + 1f) * 0.5f);    // 0 = 左边缘，1 = 内侧
                float a = Mathf.Pow(1f - t, 1.9f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>
        /// 受击红闪用的边缘环：中间全透明、靠近屏幕外沿才不透明，白色（颜色交给 Image.color 上）。
        ///
        /// 为什么不能直接用 ui_vignette.png：那张图是「近黑 + alpha 渐变」，
        /// 拿红色去乘只会得到更黑的红，闪不出来。白图才能被染成任何颜色。
        ///
        /// 衰减用 max(|u|,|v|) 而不是半径：半径在四角比边中长 1.41 倍，
        /// 画出来四个角比四条边亮一截，像漏光；方形距离才能保证一圈均匀。
        /// </summary>
        public static Sprite DamageVignette()
        {
            return Build("DamageVignette", 192, delegate(float u, float v)
            {
                float m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                // 环带只占最外侧 10%。这个数试过三档：0.70 起步糊掉半个屏幕，
                // 0.78 还是宽得像画框，0.90 才只贴着屏幕边缘一条细光。
                float e = Mathf.Clamp01((m - 0.90f) / 0.10f);
                float a = e * e * (3f - 2f * e);      // smoothstep，边缘不要有硬起点
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>确定性噪声，用于石面颗粒。不用 Random 是为了同一 seed 可复现</summary>
        private static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>
        /// 追踪导弹的锁定圈：四段**细**弧 + 四个对角短钉，读起来是一台正在对焦的瞄准器。
        ///
        /// 上一版是「四段粗弧的整圈」，弧带 3.5% 半径宽，缩到弹体大小后像一根实心铁环
        /// 套在导弹腰上——玩家看到的不是「被锁定」，而是「那枚导弹戴了个环」。
        /// 这一版把弧带压到 1.6%、弧长缩到 34°（缺口比弧还长），再补四个指向中心的短钉，
        /// 缺口感出来之后，"扫描"这个动作才成立。
        ///
        /// 静态图只负责形状：缩束与自转由 Tracker.SetRing 驱动。
        /// </summary>
        public static Sprite LockRing()
        {
            return Build("LockRing", 160, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float ax = Mathf.Abs(u);
                float ay = Mathf.Abs(v);

                // 四段细弧：每段 34°，缺口 56°
                float band = Mathf.Clamp01((r - 0.905f) / 0.016f) * Mathf.Clamp01((0.945f - r) / 0.016f);
                float ang = Mathf.Atan2(v, u) / (2f * Mathf.PI) + 0.5f;
                float seg = Mathf.Repeat(ang * 4f, 1f);
                float arc = Mathf.Clamp01(seg / 0.09f) * Mathf.Clamp01((0.34f - seg) / 0.09f);

                // 对角短钉：|u|≈|v| 的两条对角线上，半径 0.60~0.88 之间
                float diag = Mathf.Clamp01(1f - Mathf.Abs(ax - ay) / 0.075f);
                float span = Mathf.Clamp01((r - 0.58f) / 0.06f) * Mathf.Clamp01((0.88f - r) / 0.06f);
                float tick = diag * span;

                // 极淡外晕：星云很亮的地方光靠白线会看不见
                float halo = r > 0.945f ? Mathf.Clamp01((1.03f - r) / 0.09f) * 0.14f : 0f;

                float a = Mathf.Max(band * arc, tick * 0.85f) + halo;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            });
        }

        /// <summary>
        /// 导弹尾焰：口部最宽最白，向上迅速收窄并淡掉。
        /// 图高 160 → 世界高恒为 1 单位（ppu 取 height），
        /// 实际大小和拉伸交给子物体的 localScale，运行时再随速度脉动。
        ///
        /// 纵向不要做正弦条纹：静态图上有条纹、动起来却不会闪，看着像贴花；
        /// 跳动的感觉来自每帧改 localScale，不是画在贴图上。
        /// </summary>
        public static Sprite Flame()
        {
            return BuildRect("Flame", 64, 160, delegate(float u, float v)
            {
                // v = -1 是喷口，+1 是焰尖
                float t = Mathf.Clamp01((v + 1f) * 0.5f);

                // 平方收窄：口部宽、往上一下子就没，才不会拖成一根长条
                float halfWidth = Mathf.Lerp(0.92f, 0.10f, t * t);

                // 横截面用开方而不是平方：平方会让边缘两下就掉到 0，
                // 38px 宽的框里只剩中间 8px 有亮度，画出来是一根火线而不是火焰。
                // 开方是"中间一大片满亮、边上快速收到 0"，才是火苗的横截面。
                float core = Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(0.001f, halfWidth));
                float body = Mathf.Pow(core, 0.55f);

                float fade = Mathf.Clamp01(1f - t * 1.12f);
                float hot = Mathf.Clamp01(1f - t * 2.4f);

                Color c = Color.Lerp(new Color(1f, 0.38f, 0.08f), new Color(1f, 0.97f, 0.82f), hot);
                return new Color(c.r, c.g, c.b, Mathf.Clamp01(body * fade));
            });
        }

        /// <summary>
        /// 跃迁过渡用的一根光条（竖直方向）。
        ///
        /// 只做"一根两端渐隐的细线"，长度和角度全部交给运行时的 localScale / rotation：
        /// 星流的速度感来自"越飞越长"，画死在贴图里就没法拉了。
        /// 横截面要够细（核心不到 8px 宽）——粗一点就会从"光速拉丝"变成"一堆白棍子"。
        /// </summary>
        public static Sprite Streak()
        {
            return BuildRect("Streak", 16, 128, delegate(float u, float v)
            {
                float core = Mathf.Clamp01(1f - Mathf.Abs(u) / 0.44f);
                core *= core;

                // 两端渐隐：贴图两端是硬的，直接拉伸会看到两条整齐的断口
                float ends = Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Abs(v)));

                return new Color(1f, 1f, 1f, Mathf.Clamp01(core * ends));
            });
        }

        // ---------------------------------------------------------------- Boss
        public static Sprite Boss(Color tint)
        {
            Color edge = Color.Lerp(tint, Color.white, 0.5f);
            Color dark = Color.Lerp(tint, Color.black, 0.55f);

            return Build("Boss", 256, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float hex = Mathf.Max(Mathf.Abs(u) * 0.82f, Mathf.Abs(v) * 0.82f) * 0.62f
                          + Mathf.Abs(u) * 0.28f + Mathf.Abs(v) * 0.28f;

                float bodyAlpha = EdgeAlpha(0.92f - hex, 0.03f);
                // 外环
                float ring = EdgeAlpha(0.05f - Mathf.Abs(r - 0.93f), 0.02f) * 0.85f;
                float alpha = Mathf.Max(bodyAlpha, ring);
                if (alpha <= 0.01f) return Clear;

                Color c = Color.Lerp(edge, dark, Mathf.Clamp01(r / 0.95f));
                if (r < 0.3f) c = Color.Lerp(Color.white, edge, r / 0.3f);
                if (Mathf.Abs(r - 0.62f) < 0.035f) c = Color.Lerp(c, Color.white, 0.65f);
                c.a = alpha;
                return c;
            });
        }

        // ---------------------------------------------------------------- 子弹与通用

        /// <summary>
        /// 激光弹（胶囊）。双轨机炮那种"真子弹"形状走 BulletStream()，两者不共用：
        /// 一个是能量束、一个是实体弹，剪影必须能分开，否则换武器只是换颜色。
        /// </summary>
        public static Sprite Bullet(Color color)
        {
            string key = "Bullet_" + ColorUtility.ToHtmlStringRGB(color);
            Color hot = Color.Lerp(color, Color.white, 0.75f);

            return Build(key, 64, delegate(float u, float v)
            {
                float ay = Mathf.Abs(u);
                float av = Mathf.Abs(v);

                // 胶囊：中间矩形 + 两端圆头
                float alpha;
                if (av <= 0.62f) alpha = EdgeAlpha(0.20f - ay, 0.05f);
                else
                {
                    float dv = av - 0.62f;
                    float dr = Mathf.Sqrt(ay * ay + dv * dv);
                    alpha = EdgeAlpha(0.20f - dr, 0.05f);
                }

                if (alpha <= 0f) return Clear;
                float core = EdgeAlpha(0.09f - ay, 0.04f);
                Color c = Color.Lerp(color, hot, Mathf.Clamp01(core));
                c.a = alpha;
                return c;
            });
        }

        /// <summary>
        /// 连射子弹（双轨机炮）：直线锥弹头 + 等宽弹体 + 压暗的弹壳段。
        ///
        /// 上一版这颗弹是 24×34 的胶囊，乘 1.8 缩放后是屏幕上 42×59 的绿球——
        /// 玩家原话「球状很丑」。问题不在颜色，在**剪影**：圆球没有方向信息，像一颗糖。
        /// 现在剪影按真子弹的段来分：锥形弹头（直线斜边，不是圆弧）、等宽弹体、
        /// 略粗一圈的弹壳、弹壳底缘。缩到 15×34 像素也还认得出是"子弹"。
        ///
        /// 贴图竖着画：v = +1 是弹尖，游戏里靠 transform.rotation 转到飞行方向。
        /// </summary>
        public static Sprite BulletStream(Color color)
        {
            string key = "BulletStream_" + ColorUtility.ToHtmlStringRGB(color);

            Color ink = new Color(0.03f, 0.06f, 0.05f, 1f);
            Color hot = Color.Lerp(color, Color.white, 0.90f);
            Color body = Color.Lerp(color, ink, 0.08f);
            Color caseShell = Color.Lerp(color, Color.black, 0.38f);   // 弹壳比弹体暗一档：一段暗色就够说明"壳"
            Color deep = Color.Lerp(color, ink, 0.62f);                // 两侧压暗，等于自带描边

            // 30×68 px、ppu 100（和 png 的导入设置一致）→ 世界里 0.30×0.68 单位；
            // 游戏里再乘 bulletScale 0.5，屏幕上就是 15×34 像素。
            return BuildRect(key, 30, 68, delegate(float u, float v)
            {
                const float shoulder = 0.30f;     // 弹头与弹体的交界
                const float tip = 0.96f;
                const float caseTop = -0.35f;     // 弹壳口
                const float caseBottom = -0.78f;  // 弹壳与底缘的交界
                const float baseY = -0.95f;   // base 是 C# 关键字，不能当变量名

                float hw;
                if (v > shoulder)
                {
                    // 直线锥。这里不能用椭圆弹头：椭圆缩到 15 像素宽就成了一个圆帽子，
                    // 读起来和旧版那颗球没区别。子弹的识别特征就是这点直的斜边。
                    hw = Mathf.Clamp01(1f - (v - shoulder) / (tip - shoulder));
                }
                else if (v > caseTop) hw = 1f;
                else if (v > caseBottom) hw = 1.06f;   // 弹壳比弹体粗一圈，是第二个识别点
                else if (v > baseY) hw = 1.02f;
                else return Clear;

                const float halfWidth = 0.46f;
                float reach = hw * halfWidth;
                float outside = reach - Mathf.Abs(u);
                if (outside <= 0f) return Clear;

                float alpha = EdgeAlpha(outside, 0.05f);

                // ---- 圆柱感：横向位置归一化到 -1~1，两侧压暗，靠左给一条高光
                float nx = Mathf.Clamp(u / reach, -1f, 1f);
                float round = Mathf.Sqrt(Mathf.Clamp01(1f - nx * nx));

                Color tint = v > caseTop ? body : caseShell;
                Color c = Color.Lerp(deep, tint, 0.10f + 0.90f * round);

                float stripe = Mathf.Clamp01(1f - Mathf.Abs(nx + 0.50f) / 0.26f);
                c = Color.Lerp(c, hot, 0.30f * stripe * round);

                // ---- 白热弹尖：这一截是曳光，也是"它往哪飞"的提示。
                // 锥顶越窄，圆柱明暗越会把顶点压成黑的（看着像铅笔芯），
                // 所以这里必须靠"发光盖住明暗"，而不是叠在明暗上。
                if (v > 0.45f)
                {
                    float t = Mathf.Clamp01((v - 0.45f) / (tip - 0.45f));
                    c = Color.Lerp(c, hot, 0.92f * t * t);
                }

                // ---- 弹壳口一道暗缝、底缘一道亮线、弹底压暗
                if (v <= caseTop && v > caseTop - 0.045f)
                    c = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f);
                else if (v <= caseBottom && v > caseBottom - 0.04f)
                    c = Color.Lerp(c, hot, 0.30f);
                else if (v <= baseY + 0.03f)
                    c = new Color(c.r * 0.72f, c.g * 0.72f, c.b * 0.72f, 1f);

                c.a = alpha;
                return c;
            }, 100f);
        }

        /// <summary>
        /// 敌方能量弹。
        ///
        /// 上一版直接复用玩家子弹的圆形贴图，问题是：圆点放在密集弹幕里毫无辨识度，
        /// 而且视觉上没有任何「危险」暗示。这一版做成白热核心 + 亮边 + 四向尖刺 + 外围柔光晕，
        /// 尖刺负责辨识度、光晕负责在暗背景上被看清。
        /// </summary>
        public static Sprite EnemyOrb(Color color)
        {
            string key = "EnemyOrb_" + ColorUtility.ToHtmlStringRGB(color);
            Color hot = Color.Lerp(color, Color.white, 0.92f);
            Color bright = Color.Lerp(color, Color.white, 0.45f);

            return Build(key, 64, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float au = Mathf.Abs(u);
                float av = Mathf.Abs(v);

                // 柔光晕：弹幕一密，纯实心弹就会糊成一片，必须有外发光把它们分开
                float halo = Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * 0.45f;

                // 本体
                float body = Mathf.Clamp01((0.50f - r) / 0.025f);

                // 四向尖刺：给弹丸一个方向感，也让它看起来「扎人」
                float spikeH = Mathf.Clamp01((0.74f - au) / 0.03f) * Mathf.Clamp01((0.058f - av) / 0.058f);
                float spikeV = Mathf.Clamp01((0.74f - av) / 0.03f) * Mathf.Clamp01((0.058f - au) / 0.058f);
                float spike = Mathf.Max(spikeH, spikeV) * 0.85f;

                float alpha = Mathf.Max(Mathf.Max(body, spike), halo);
                if (alpha <= 0.012f) return Clear;

                Color c;
                if (r < 0.19f) c = hot;                                            // 白热核心
                else if (r < 0.43f) c = Color.Lerp(hot, color, (r - 0.19f) / 0.24f); // 过渡
                else if (r < 0.505f) c = bright;                                   // 高亮边
                else c = color;                                                    // 光晕与尖刺

                c.a = alpha;
                return c;
            });
        }

        /// <summary>柔和圆形光晕，粒子、星云、引擎光都用它</summary>
        public static Sprite Glow()
        {
            return Build("Glow", 128, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.4f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>星点：中心亮、边缘快速衰减</summary>
        public static Sprite Star()
        {
            return Build("Star", 32, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 3.2f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        // ---------------------------------------------------------------- 背景

        /// <summary>
        /// 全屏底色渐变：顶上深蓝、中段偏紫、底下近黑。
        /// 单色纯黑背景是最显“空”的地方，压一层渐变立刻有空间感。
        /// 竖条纹理，横向无变化，所以只要 16 像素宽。
        /// </summary>
        public static Sprite Backdrop()
        {
            return BuildRect("Backdrop", 16, 256, delegate(float u, float v)
            {
                float t = v * 0.5f + 0.5f;                       // 0 = 底，1 = 顶

                Color deep = new Color(0.010f, 0.014f, 0.032f, 1f);
                Color mid = new Color(0.055f, 0.028f, 0.098f, 1f);
                Color high = new Color(0.016f, 0.034f, 0.076f, 1f);

                Color c = t < 0.55f
                    ? Color.Lerp(deep, mid, t / 0.55f)
                    : Color.Lerp(mid, high, (t - 0.55f) / 0.45f);

                return c;
            });
        }

        /// <summary>远景行星：球面光照 + 气态条带 + 边缘光</summary>
        public static Sprite Planet(Color body, Color rim)
        {
            string key = "Planet_" + ColorUtility.ToHtmlStringRGB(body);
            Vector3 lightDir = new Vector3(-0.42f, 0.62f, 0.66f).normalized;

            return Build(key, 256, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                if (r >= 1f) return Clear;

                float alpha = Mathf.Clamp01((1f - r) / 0.015f);

                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - r * r));
                float ndl = Mathf.Clamp01(Vector3.Dot(new Vector3(u, v, z), lightDir));

                // 横向条带：气态行星的层次感
                float band = 0.80f + 0.20f * Mathf.Sin(v * 9.5f + Mathf.Sin(u * 2.2f) * 0.7f);

                Color c = Color.Lerp(body * 0.14f, body * band, Mathf.Pow(ndl, 0.65f));

                // 边缘光：只在轮廓附近亮起来，像被恒星从侧面照到
                float rimFactor = Mathf.Pow(r, 14f);
                c = Color.Lerp(c, rim, rimFactor * 0.85f);

                c.a = alpha;
                return c;
            });
        }

        /// <summary>
        /// 命中闪光：四芒星 + 软心。粒子里用它做"啪"的那一下。
        ///
        /// 上一版命中只有一个柔光圆点，Additive 叠在亮星云上几乎看不见，
        /// 叠在暗背景上又是一团灰雾——所以整套命中去改成"星芒 + 火星"的结构。
        /// 芒的权重故意大于中心：让它是"炸开"的，而不是"亮一下"的。
        /// </summary>
        public static Sprite HitFlash()
        {
            return Build("HitFlash", 128, delegate(float u, float v)
            {
                float au = Mathf.Abs(u);
                float av = Mathf.Abs(v);
                float r = Mathf.Sqrt(u * u + v * v);

                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 3.2f), 2.2f);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - r * 1.10f), 3.4f) * 0.40f;

                float spikeH = Mathf.Pow(Mathf.Clamp01(1f - au * 10f), 2.4f)
                             * Mathf.Pow(Mathf.Clamp01(1f - av * 2.6f), 1.8f);
                float spikeV = Mathf.Pow(Mathf.Clamp01(1f - av * 10f), 2.4f)
                             * Mathf.Pow(Mathf.Clamp01(1f - au * 2.6f), 1.8f);

                // 斜向两道短芒：只有十字的话太小的时候会读成一个"+"号
                float d1 = Mathf.Abs(u - v) * 0.7071f;
                float d2 = Mathf.Abs(u + v) * 0.7071f;
                float diag = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Min(d1, d2) * 22f), 2.2f)
                           * Mathf.Clamp01(1f - r * 1.7f) * 0.30f;

                float a = core + halo + spikeH * 0.70f + spikeV * 0.50f + diag;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            });
        }

        /// <summary>
        /// 火星：一条从亮头收向尾尖的细条，给 StretchedBillboard 沿速度拉长用。
        /// v=+1 是头（最亮最宽），v=-1 是尾（淡出）。
        ///
        /// 两头都留了一点余量，不贴到贴图边上——粒子放大后边缘会被双线性插值削掉，
        /// 正好贴边的亮头会变成平口，看起来像被切了一刀。
        /// </summary>
        public static Sprite Spark()
        {
            return BuildRect("Spark", 16, 96, delegate(float u, float v)
            {
                float t = Mathf.Clamp01((v + 1f) * 0.5f);

                float w = Mathf.Lerp(0.10f, 0.66f, Mathf.SmoothStep(0f, 1f, t));
                float a = Mathf.Clamp01(1f - Mathf.Abs(u) / w);
                a = Mathf.Pow(a, 1.45f);
                a *= Mathf.Lerp(0.14f, 1f, t);
                a *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t * 2.2f));

                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>
        /// 冲击环：一圈很细的白环、中间全透明，命中时"啪"的那一下用它。
        ///
        /// 为什么值得单独做一张：命中读不读得出来，主要看"有没有把这一下框住"。
        /// 火星是散的、闪光是一团糊的，只有一圈正在扩开的环能明确说
        /// "冲击波从这里向外走"。所有敌人共用同一个环，颜色交给 Image.color。
        /// </summary>
        public static Sprite HitRing()
        {
            return Build("HitRing", 128, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);

                // 实环：0.62~0.70，内外各 0.02 过渡，128² 上才不锯齿
                float ring = Mathf.Clamp01((r - 0.62f) / 0.020f)
                           * Mathf.Clamp01((0.70f - r) / 0.020f);

                // 环内侧补一点柔光：缩到十几像素时纯 2px 的环会被双线性插值滤没
                float d = (r - 0.66f) / 0.085f;
                float soft = Mathf.Exp(-d * d) * 0.32f;

                return new Color(1f, 1f, 1f, Mathf.Clamp01(ring + soft));
            });
        }

        /// <summary>
        /// 碎屑：一块崩下来的不规则小片，给金属装甲、石块当"被啃掉的实体"用。
        ///
        /// 为什么不能拿 Glow 顶着：光点是"能量"，碎屑是"物质"。
        /// 打石头、打装甲如果只出光点，一眼就知道少了点东西——被打中的那块材料没有掉下来。
        ///
        /// 做法和陨石同源：几个半平面求交得到凸多边形，棱边就是碎的断面。
        /// 受光方向固定从左上，这样翻起来的时候明暗跟着变，才看得出它在转。
        /// </summary>
        public static Sprite Shard()
        {
            // 64 而不是 32：碎块在游戏里能放大到 0.4 世界单位（≈39 像素），
            // 32 的分辨率贴上去边缘全是锯齿，看着像纸屑不像金属。
            return Build("Shard", 64, delegate(float u, float v)
            {
                float theta = Mathf.Atan2(v, u);
                float r = Mathf.Sqrt(u * u + v * v);

                float radius = 9f;
                for (int i = 0; i < 5; i++)
                {
                    float phi = i * 1.2566f + 0.35f;
                    float c = Mathf.Cos(theta - phi);
                    if (c > 0.18f)
                    {
                        float dist = 0.60f + 0.15f * Mathf.Sin(i * 2.7f + 0.8f);
                        radius = Mathf.Min(radius, dist / Mathf.Max(c, 0.18f));
                    }
                }

                float alpha = Mathf.Clamp01((radius - r) / 0.030f);
                if (alpha <= 0f) return Clear;

                // 左上受光：碎片是凸的，把法线当球面推一下就有明暗
                float facing = Mathf.Clamp01(u * -0.62f + v * 0.78f);
                float shade = 0.38f + 0.62f * facing;

                // 断口亮边：越贴近轮廓越亮一点点，像新鲜金属断面的反光。
                // 没有这一道，碎片翻起来就是一整块灰纸板在转。
                float edge = Mathf.Clamp01(1f - (radius - r) / 0.13f);
                shade = Mathf.Min(1f, shade + 0.24f * edge * edge);

                return new Color(shade, shade, shade, alpha);
            });
        }

        /// <summary>
        /// 光束核心：中间一条硬边亮线，横向按高斯衰减。
        ///
        /// 为什么不是"一条等宽的矩形"：等宽矩形贴在深色背景上两端会变成刀切一样的硬边，
        /// 看起来像画了根白棍子。高斯衰减让边缘有 2~3 像素的过渡，
        /// 再叠一倍宽的柔光层，才有"在发光"的观感。
        ///
        /// 竖向不做衰减：这条图会被 y 方向拉伸到整屏高，两端各由机头和命中点盖住。
        /// </summary>
        public static Sprite BeamCore()
        {
            return BuildRect("BeamCore", 24, 256, delegate(float u, float v)
            {
                float a = Mathf.Exp(-(u * u) / (2f * 0.34f * 0.34f));
                // 中间再压一道更亮的心：单靠高斯在缩到几像素宽时会被滤成一片灰
                a = Mathf.Clamp01(a * 0.72f + Mathf.Clamp01(1f - Mathf.Abs(u) * 2.6f) * 0.9f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>光束外晕：比核心宽一倍多，亮度低，负责"光洒在周围"</summary>
        public static Sprite BeamGlow()
        {
            return BuildRect("BeamGlow", 64, 256, delegate(float u, float v)
            {
                float a = Mathf.Exp(-(u * u) / (2f * 0.30f * 0.30f));
                return new Color(1f, 1f, 1f, a * 0.55f);
            });
        }

        /// <summary>光束命中点的光斑：十字星 + 中心圆，贴在屏幕顶和敌人身上</summary>
        public static Sprite BeamTip()
        {
            return Build("BeamTip", 128, delegate(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float au = Mathf.Abs(u);
                float av = Mathf.Abs(v);

                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 2.6f), 2f);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - r * 1.05f), 3f) * 0.55f;
                float spikeH = Mathf.Pow(Mathf.Clamp01(1f - au * 11f), 2f) * Mathf.Pow(Mathf.Clamp01(1f - av), 1.2f) * 0.5f;

                return new Color(1f, 1f, 1f, Mathf.Clamp01(core + halo + spikeH));
            });
        }

        /// <summary>1×1 纯白，UI 与纯色块用它</summary>
        public static Sprite Pixel()
        {
            return Build("Pixel", 4, delegate(float u, float v) { return Color.white; });
        }

    }
}
