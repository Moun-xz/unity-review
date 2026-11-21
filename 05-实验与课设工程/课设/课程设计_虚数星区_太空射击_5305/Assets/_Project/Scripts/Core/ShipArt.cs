using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 战机的程序化彩绘。
    ///
    /// 工程只有一架「重锤 HAMMER」（`GameConfig.Ships()` 返回的唯一一条），所以这里
    /// 只有一份设计图 `BuildHammer()`，`Get()` 直接调用它。要加新船时，照它的写法
    /// 再写一个 Build 方法，在 `Get()` 里按 callsign 分派即可。
    ///
    /// 512×512 逐像素生成（pixelsPerUnit = 512，即 1×1 世界单位，机头朝 +v），
    /// 深色金属战术终端语言：多边形剪影 + 角色材质 + 显式刻线/座舱/喷口 + SDF 倒角光照。
    /// 结果按 callsign 缓存，首次 Get() 时才生成；Clear() 负责销毁 Sprite 与 Texture2D。
    /// </summary>
    public static class ShipArt
    {
        // 唯一一架战机（重锤）需要在大尺寸下也扛得住近看：384 → 512，
        // 线条宽度是按像素给的，贴图变大后相同像素宽在面板里更细，正好当"刻线"用。
        private const int Size = 512;
        private const int Pixels = Size * Size;
        private const float Px = 2f / Size;          // 1 像素对应的归一化长度

        private const float Aa = 1.7f * Px;          // 剪影抗锯齿带宽
        private const float OutlineW = 2.0f * Px;    // 剪影外描边
        private const float RimW = 1.1f * Px;        // 内缘暗描边
        private const float BevelW = 2.8f * Px;      // 倒角高光带宽
        private const float LineAa = 0.55f * Px;     // 刻线抗锯齿带宽

        private const int RoleWing = 0;
        private const int RoleTail = 1;
        private const int RoleCanard = 2;
        private const int RoleNacelle = 3;
        private const int RoleHull = 4;
        private const int RoleIntake = 5;
        private const int RoleRidge = 6;
        private const int RolePlate = 7;

        private static readonly int[] RolePriority = { 10, 14, 16, 24, 40, 46, 48, 30 };

        private const int StyleSeam = 0;
        private const int StyleSeamFaint = 1;
        private const int StyleAo = 2;
        private const int StyleAccent = 3;
        private const int StyleAccentDim = 4;
        private const int StyleAccentHot = 5;
        private const int StyleLight = 6;
        private const int StyleDark = 7;

        private static readonly Vector3 Outline = C(0.020f, 0.032f, 0.058f);
        private static readonly Vector3 HullLit = C(0.505f, 0.585f, 0.675f);
        private static readonly Vector3 HullMid = C(0.300f, 0.360f, 0.445f);
        private static readonly Vector3 HullDark = C(0.140f, 0.175f, 0.240f);
        private static readonly Vector3 RidgeLit = C(0.600f, 0.685f, 0.790f);
        private static readonly Vector3 RidgeDark = C(0.210f, 0.260f, 0.340f);
        private static readonly Vector3 PlateLit = C(0.455f, 0.520f, 0.600f);
        private static readonly Vector3 PlateDark = C(0.155f, 0.185f, 0.245f);
        private static readonly Vector3 WingLit = C(0.330f, 0.395f, 0.480f);
        private static readonly Vector3 WingDark = C(0.120f, 0.150f, 0.210f);
        private static readonly Vector3 CanardLit = C(0.300f, 0.360f, 0.445f);
        private static readonly Vector3 CanardDark = C(0.105f, 0.132f, 0.188f);
        private static readonly Vector3 NacLit = C(0.315f, 0.375f, 0.460f);
        private static readonly Vector3 NacDark = C(0.105f, 0.135f, 0.185f);
        private static readonly Vector3 IntakeLit = C(0.185f, 0.215f, 0.275f);
        private static readonly Vector3 IntakeDark = C(0.060f, 0.075f, 0.105f);
        private static readonly Vector3 Bevel = C(0.780f, 0.870f, 1.000f);
        private static readonly Vector3 GlassHot = C(0.860f, 0.970f, 1.000f);
        private static readonly Vector3 GlassDeep = C(0.100f, 0.420f, 0.720f);
        private static readonly Vector3 Frame = C(0.040f, 0.058f, 0.092f);
        private static readonly Vector3 EngineHot = C(1.000f, 0.620f, 0.280f);
        private static readonly Vector3 EngineCore = C(1.000f, 0.975f, 0.895f);
        private static readonly Vector3 Housing = C(0.170f, 0.205f, 0.265f);
        private static readonly Vector3 DeckTint = C(0.720f, 0.800f, 0.900f);
        private static readonly Vector3 CanopyLip = C(0.550f, 0.660f, 0.800f);
        private static readonly Vector3 NozzleLip = C(0.620f, 0.720f, 0.850f);
        private static readonly Vector3 WhiteCore = C(1.000f, 1.000f, 0.985f);
        private static readonly Vector3 SunLight = new Vector2(-0.52f, 0.854f).normalized;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(4);

        // ------------------------------------------------------------------ 公开 API

        /// <summary>取战机 Sprite，首次调用时生成并缓存。callsign 只作缓存键用。</summary>
        public static Sprite Get(ShipConfig ship)
        {
            string key = ship != null && !string.IsNullOrEmpty(ship.callsign) ? ship.callsign : "HAMMER";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Design design = BuildHammer();
            Color accent = ship != null && ship.accent.a > 0.01f ? ship.accent : design.DefaultAccent;
            // 第二色：机翼外侧用另一种颜色，整架飞机才有"配色"而不是"单色滤镜"
            Color accentAlt = ship != null && ship.accentAlt.a > 0.01f ? ship.accentAlt : design.DefaultAccent;
            Sprite sprite = Build(design, accent, accentAlt);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>销毁缓存中的所有 Sprite 与 Texture2D。</summary>
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
                    UnityEngine.Object.Destroy(sprite);
                    if (tex != null)
                        UnityEngine.Object.Destroy(tex);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(sprite);
                    if (tex != null)
                        UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            Cache.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }

        // ------------------------------------------------------------------ 数据结构

        private sealed class Shape
        {
            public readonly int Role;
            public readonly Vector2[] Pts;
            public readonly Vector2[] Lead;
            public readonly Vector2[] MirLead;
            public readonly float Bx0, Bx1, By0, By1;
            public readonly float Cx, Hw;
            public readonly float[] Wtab;      // 中心线部件的逐行半宽表

            public Shape(int role, Vector2[] pts, Vector2[] lead)
            {
                Role = role;
                Pts = pts;
                Lead = lead;
                if (lead != null)
                {
                    MirLead = new Vector2[lead.Length];
                    for (int i = 0; i < lead.Length; i++)
                        MirLead[i] = new Vector2(-lead[i].x, lead[i].y);
                }
                float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 p = pts[i];
                    if (p.x < x0) x0 = p.x;
                    if (p.x > x1) x1 = p.x;
                    if (p.y < y0) y0 = p.y;
                    if (p.y > y1) y1 = p.y;
                }
                Bx0 = x0;
                Bx1 = x1;
                By0 = y0;
                By1 = y1;
                Cx = (x0 + x1) * 0.5f;
                Hw = Mathf.Max((x1 - x0) * 0.5f, 1e-4f);
                if (role == RoleHull || role == RoleIntake || role == RoleRidge)
                    Wtab = BuildWidthTable(pts);
            }
        }

        private sealed class Stroke
        {
            public readonly Vector2[] Pts;
            public readonly bool Mirror;
            public readonly float WidthPx;
            public readonly int Style;
            public readonly float Strength;

            public Stroke(Vector2[] pts, bool mirror, float widthPx, int style, float strength)
            {
                Pts = pts;
                Mirror = mirror;
                WidthPx = widthPx;
                Style = style;
                Strength = strength;
            }
        }

        private sealed class Nozzle
        {
            public readonly float Nx, Ny, Rx, Ry;

            public Nozzle(float nx, float ny, float rx, float ry)
            {
                Nx = nx;
                Ny = ny;
                Rx = rx;
                Ry = ry;
            }
        }

        private sealed class Slot
        {
            public readonly Vector2[] Pts;
            public readonly float HotPx;

            public Slot(Vector2[] pts, float hotPx)
            {
                Pts = pts;
                HotPx = hotPx;
            }
        }

        private sealed class Design
        {
            public string Name;
            public Color DefaultAccent;
            public readonly List<Shape> Shapes = new List<Shape>(10);
            public readonly List<Stroke> Strokes = new List<Stroke>(20);
            public readonly List<Nozzle> Nozzles = new List<Nozzle>(2);
            public readonly List<Slot> Slots = new List<Slot>(1);
            public float CockpitY;
            public float CockpitRx;
            public float CockpitRy;
            public float TintMix = 0.50f;   // 机体色掺入中间调的强度，封面那种深灰机身要更低
        }

        // ------------------------------------------------------------------ 生成入口

        private static Sprite Build(Design d, Color accentColor, Color accentAltColor)
        {
            Vector3 accent = new Vector3(accentColor.r, accentColor.g, accentColor.b);
            Vector3 accentHot = Clamp01(accent + (Vector3.one - accent) * 0.72f);
            Vector3 accentAlt = new Vector3(accentAltColor.r, accentAltColor.g, accentAltColor.b);

            float[] sdMax = new float[Pixels];
            for (int i = 0; i < Pixels; i++)
                sdMax[i] = -1f;
            int[] winner = new int[Pixels];
            int[] winPrio = new int[Pixels];
            for (int i = 0; i < Pixels; i++)
            {
                winner[i] = -1;
                winPrio[i] = -1;
            }

            // 1) 每个部件的 SDF：同时记录并集深度（用于倒角）与最高优先级归属（用于材质）。
            float[] sd = new float[Pixels];
            for (int si = 0; si < d.Shapes.Count; si++)
            {
                Shape s = d.Shapes[si];
                RasterizeSd(s, sd);
                int prio = RolePriority[s.Role];
                int ix0, ix1, iy0, iy1;
                SdBox(s, out ix0, out ix1, out iy0, out iy1);
                for (int y = iy0; y <= iy1; y++)
                {
                    int row = y * Size;
                    for (int x = ix0; x <= ix1; x++)
                    {
                        int idx = row + x;
                        float dd = sd[idx];
                        if (dd > sdMax[idx])
                            sdMax[idx] = dd;
                        if (dd > 0f && prio > winPrio[idx])
                        {
                            winPrio[idx] = prio;
                            winner[idx] = si;
                        }
                    }
                }
            }

            // 2) 部件底色
            Vector3[] img = new Vector3[Pixels];
            for (int y = 0; y < Size; y++)
            {
                int row = y * Size;
                for (int x = 0; x < Size; x++)
                {
                    int idx = row + x;
                    int w = winner[idx];
                    if (w >= 0)
                        img[idx] = BaseColor(d.Shapes[w], x, y);
                }
            }

            // 3) 面板刻线 / 强调色细线 / 环境光遮蔽
            for (int i = 0; i < d.Strokes.Count; i++)
                DrawStroke(img, sdMax, d.Strokes[i], accent, accentHot);

            // 4) 座舱
            DrawCockpit(img, sdMax, d, accent);

            // 5) 引擎喷口
            for (int i = 0; i < d.Nozzles.Count; i++)
                DrawNozzle(img, sdMax, d.Nozzles[i], accentHot);

            // 6) 引擎缝（尾缘发光槽）
            for (int i = 0; i < d.Slots.Count; i++)
                DrawSlot(img, sdMax, d.Slots[i], accentHot);

            // 7) 倒角高光 / 内缘暗边 / 外描边 / 抗锯齿
            return Finish(d, img, sdMax, accent, accentHot, accentAlt);
        }

        // ------------------------------------------------------------------ SDF

        private static void RasterizeSd(Shape s, float[] sd)
        {
            for (int i = 0; i < sd.Length; i++)
                sd[i] = -1f;
            int ix0, ix1, iy0, iy1;
            SdBox(s, out ix0, out ix1, out iy0, out iy1);
            Vector2[] pts = s.Pts;
            int n = pts.Length;
            for (int y = iy0; y <= iy1; y++)
            {
                float v = VOf(y);
                int row = y * Size;
                for (int x = ix0; x <= ix1; x++)
                {
                    float u = UOf(x);
                    float best = 1e9f;
                    bool inside = false;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 a = pts[i];
                        Vector2 b = pts[i + 1 == n ? 0 : i + 1];
                        float dx = b.x - a.x;
                        float dy = b.y - a.y;
                        float l2 = dx * dx + dy * dy;
                        float t = 0f;
                        if (l2 > 1e-14f)
                        {
                            t = ((u - a.x) * dx + (v - a.y) * dy) / l2;
                            if (t < 0f) t = 0f;
                            else if (t > 1f) t = 1f;
                        }
                        float qx = a.x + dx * t - u;
                        float qy = a.y + dy * t - v;
                        float dd = qx * qx + qy * qy;
                        if (dd < best) best = dd;
                        if ((a.y > v) != (b.y > v) && u < a.x + (v - a.y) * dx / dy)
                            inside = !inside;
                    }
                    float dist = Mathf.Sqrt(best);
                    sd[row + x] = inside ? dist : -dist;
                }
            }
        }

        private static void SdBox(Shape s, out int ix0, out int ix1, out int iy0, out int iy1)
        {
            const int Margin = 6;   // 覆盖倒角/描边/AA 全部带宽
            ix0 = Mathf.Clamp(Mathf.FloorToInt((s.Bx0 + 1f) / Px - 0.5f) - Margin, 0, Size - 1);
            ix1 = Mathf.Clamp(Mathf.CeilToInt((s.Bx1 + 1f) / Px - 0.5f) + Margin, 0, Size - 1);
            iy0 = Mathf.Clamp(Mathf.FloorToInt((s.By0 + 1f) / Px - 0.5f) - Margin, 0, Size - 1);
            iy1 = Mathf.Clamp(Mathf.CeilToInt((s.By1 + 1f) / Px - 0.5f) + Margin, 0, Size - 1);
        }

        private static float[] BuildWidthTable(Vector2[] pts)
        {
            float[] tab = new float[Size];
            int n = pts.Length;
            for (int j = 0; j < Size; j++)
            {
                float v = VOf(j);
                float best = 0f;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[i + 1 == n ? 0 : i + 1];
                    float dy = b.y - a.y;
                    if (Mathf.Abs(dy) < 1e-12f)
                        continue;
                    if ((a.y > v) != (b.y > v))
                    {
                        float x = a.x + (v - a.y) * (b.x - a.x) / dy;
                        if (Mathf.Abs(x) > best)
                            best = Mathf.Abs(x);
                    }
                }
                tab[j] = Mathf.Max(best, 1e-4f);
            }
            return tab;
        }

        // ------------------------------------------------------------------ 材质

        private static Vector3 BaseColor(Shape s, int x, int y)
        {
            float u = UOf(x);
            float v = VOf(y);
            int role = s.Role;
            float t;
            if (s.Wtab != null)
                t = Mathf.Clamp01(Mathf.Abs(u) / Mathf.Max(s.Wtab[y], 1e-4f));
            else if (role == RoleNacelle)
                t = Mathf.Clamp01(Mathf.Abs(u - s.Cx) / s.Hw);
            else
                t = Mathf.Clamp01((Mathf.Abs(u) - s.Bx0) / Mathf.Max(s.Bx1 - s.Bx0, 1e-4f));

            if (role == RoleHull)
            {
                Vector3 c = HullMid;
                c = c + (HullLit - c) * (1f - SmoothStep(0.10f, 0.72f, t));
                c = c + (HullDark - c) * SmoothStep(0.62f, 1f, t);
                // 机背受光带（约 6px 宽）
                float deck = Mathf.Exp(-(u / 0.028f) * (u / 0.028f)) * SmoothStep(-0.9f, 0.9f, v) * 0.13f;
                c = c + (DeckTint - c) * deck;
                // 纵深：机鼻略提亮、机尾入影（0.90 ~ 1.06），比一条均匀亮线更像有机体
                c *= 0.90f + 0.16f * SmoothStep(-0.95f, 0.75f, v);
                return c;
            }

            if (role == RoleRidge)
                return RidgeLit + (RidgeDark - RidgeLit) * SmoothStep(0f, 1f, t);

            if (role == RoleIntake)
            {
                Vector3 c = IntakeLit + (IntakeDark - IntakeLit) * t;
                c = c + (HullDark * 0.55f - c) * (Mathf.Clamp01(1f - t) * 0.35f);
                return c;
            }

            if (role == RoleNacelle)
            {
                Vector3 c = NacLit + (NacDark - NacLit) * Mathf.Pow(t, 0.85f);
                c *= 0.92f + 0.14f * ((v + 1f) * 0.5f);
                return c;
            }

            if (role == RolePlate)
            {
                // 明暗带与翼前缘平行：横向项 + 前后项按后掠角斜向合成（0.62 / -0.46 就是约 36° 的后掠斜切）
                float chord = (Mathf.Abs(u) - s.Bx0) / Mathf.Max(s.Bx1 - s.Bx0, 1e-4f);
                float skew = chord * 0.62f + (v - s.By0) * -0.46f;
                float k = Mathf.Clamp01(skew + 0.46f);
                Vector3 plate = PlateLit + (PlateDark - PlateLit) * Mathf.Pow(k, 1.15f);
                // 板面沿翼展方向轻微提亮，让"从翼根到翼尖"有一条极淡的走向
                plate *= 0.96f + 0.07f * Mathf.Clamp01(chord);
                return plate;
            }

            Vector3 lit = role == RoleCanard ? CanardLit : WingLit;
            Vector3 dark = role == RoleCanard ? CanardDark : WingDark;
            Vector3 wing;
            if (s.Lead != null)
            {
                float ld = PolylineDist(s.Lead, u, v);
                if (s.MirLead != null)
                    ld = Mathf.Min(ld, PolylineDist(s.MirLead, u, v));
                float ch = Mathf.Clamp01(ld / 0.30f);
                wing = lit + (dark - lit) * Mathf.Pow(ch, 1.25f);
            }
            else
            {
                float chn = Mathf.Clamp01((v - s.By0) / Mathf.Max(s.By1 - s.By0, 1e-4f));
                wing = dark + (lit - dark) * Mathf.Pow(chn, 1.2f);
            }
            float span = Mathf.Clamp01((Mathf.Abs(u) - s.Bx0) / Mathf.Max(s.Bx1 - s.Bx0, 1e-4f));
            wing *= 0.90f + 0.16f * (1f - span);
            wing *= 0.94f + 0.10f * ((v + 1f) * 0.5f);
            return wing;
        }

        // ------------------------------------------------------------------ 刻线

        private static void DrawStroke(Vector3[] img, float[] sdMax, Stroke st, Vector3 accent, Vector3 accentHot)
        {
            float hw = st.WidthPx * 0.5f * Px;
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            Vector2[] pts = st.Pts;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 p = pts[i];
                if (p.x < x0) x0 = p.x;
                if (p.x > x1) x1 = p.x;
                if (p.y < y0) y0 = p.y;
                if (p.y > y1) y1 = p.y;
                if (st.Mirror)
                {
                    float mx = -p.x;
                    if (mx < x0) x0 = mx;
                    if (mx > x1) x1 = mx;
                }
            }
            float m = hw + LineAa + Px;
            int ix0 = Mathf.Clamp(Mathf.FloorToInt((x0 - m + 1f) / Px - 0.5f), 0, Size - 1);
            int ix1 = Mathf.Clamp(Mathf.CeilToInt((x1 + m + 1f) / Px - 0.5f), 0, Size - 1);
            int iy0 = Mathf.Clamp(Mathf.FloorToInt((y0 - m + 1f) / Px - 0.5f), 0, Size - 1);
            int iy1 = Mathf.Clamp(Mathf.CeilToInt((y1 + m + 1f) / Px - 0.5f), 0, Size - 1);

            for (int y = iy0; y <= iy1; y++)
            {
                float v = VOf(y);
                int row = y * Size;
                for (int x = ix0; x <= ix1; x++)
                {
                    int idx = row + x;
                    if (sdMax[idx] <= 0f)
                        continue;
                    float u = UOf(x);
                    float d = StrokeDist(st, u, v);
                    float cov = Mathf.Clamp01((hw - d) / LineAa + 0.5f) * st.Strength;
                    if (cov <= 0f)
                        continue;
                    switch (st.Style)
                    {
                        case StyleSeam:
                            img[idx] *= 1f - 0.33f * cov;
                            break;
                        case StyleSeamFaint:
                            img[idx] *= 1f - 0.22f * cov;
                            break;
                        case StyleAo:
                            img[idx] *= 1f - 0.40f * Mathf.Pow(cov, 0.75f);
                            break;
                        case StyleAccent:
                            img[idx] = Mix(img[idx], accent, cov * 0.92f);
                            break;
                        case StyleAccentDim:
                            img[idx] = Mix(img[idx], accent * 0.85f, cov * 0.55f);
                            break;
                        case StyleAccentHot:
                            img[idx] = Mix(img[idx], accentHot, cov * 0.90f);
                            break;
                        case StyleLight:
                            img[idx] = Mix(img[idx], Bevel, cov * 0.55f);
                            break;
                        default:
                            img[idx] = Mix(img[idx], Outline, cov * 0.85f);
                            break;
                    }
                }
            }
        }

        private static float StrokeDist(Stroke st, float u, float v)
        {
            Vector2[] pts = st.Pts;
            float best = 1e9f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                best = Mathf.Min(best, SegDist(u, v, a.x, a.y, b.x, b.y));
                if (st.Mirror)
                    best = Mathf.Min(best, SegDist(u, v, -a.x, a.y, -b.x, b.y));
            }
            return best;
        }

        // ------------------------------------------------------------------ 座舱

        private static void DrawCockpit(Vector3[] img, float[] sdMax, Design d, Vector3 accent)
        {
            // 座舱玻璃跟着机体色走：热端偏机体色，深处压成机体色的暗调。
            Vector3 gHot = Mix(GlassHot, accent + (Vector3.one - accent) * 0.45f, 0.34f);
            Vector3 gDeep = Mix(GlassDeep, accent * 0.62f, 0.52f);

            float cy = d.CockpitY;
            float rx = d.CockpitRx;
            float ry = d.CockpitRy;
            float xExt = rx * 1.10f;
            float yExt = ry * 1.10f;
            int ix0 = Mathf.Clamp(Mathf.FloorToInt((-xExt + 1f) / Px - 0.5f), 0, Size - 1);
            int ix1 = Mathf.Clamp(Mathf.CeilToInt((xExt + 1f) / Px - 0.5f), 0, Size - 1);
            int iy0 = Mathf.Clamp(Mathf.FloorToInt((cy - yExt + 1f) / Px - 0.5f), 0, Size - 1);
            int iy1 = Mathf.Clamp(Mathf.CeilToInt((cy + yExt + 1f) / Px - 0.5f), 0, Size - 1);

            for (int y = iy0; y <= iy1; y++)
            {
                float v = VOf(y);
                int row = y * Size;
                for (int x = ix0; x <= ix1; x++)
                {
                    int idx = row + x;
                    if (sdMax[idx] <= 0f)
                        continue;
                    float lx = UOf(x) / rx;
                    float ly = (v - cy) / ry;
                    float q = Mathf.Sqrt(lx * lx + ly * ly);

                    // 1) 外围深色窗框
                    if (q < 1.06f && q > 0.78f)
                    {
                        float t = Mathf.Clamp01((1.06f - q) / 0.28f);
                        img[idx] = Mix(img[idx], Frame, t * 0.98f);
                    }
                    // 2) 框体内侧亮边（左上受光）
                    if (q < 1.00f && q > 0.86f && ly > -0.2f)
                    {
                        float t = Mathf.Clamp01((1.00f - q) / 0.14f) * Mathf.Clamp01(ly + 0.2f);
                        img[idx] = Mix(img[idx], CanopyLip, t * 0.42f);
                    }
                    // 3) 玻璃：左热右深渐变 + 斜向反光条 + 高光点 + 底部压暗
                    if (q <= 0.86f)
                    {
                        float b = Mathf.Clamp01(0.58f - 0.62f * lx - 0.54f * ly);
                        Vector3 col = gDeep + (gHot - gDeep) * Mathf.Pow(b, 1.3f);
                        float band = Mathf.Clamp01(1f - Mathf.Abs((lx * 0.72f + ly * 0.69f) - 0.06f) / 0.26f);
                        col = col + (gHot - col) * (Mathf.Pow(band, 1.7f) * 0.42f);
                        float ddx = lx + 0.34f;
                        float ddy = ly - 0.32f;
                        float dot = Mathf.Clamp01(1f - Mathf.Sqrt(ddx * ddx + ddy * ddy) / 0.14f);
                        col = col + (gHot - col) * (Mathf.Pow(dot, 1.3f) * 0.95f);
                        col *= 1f - 0.30f * Mathf.Clamp01((ly - 0.10f) / 0.9f);
                        img[idx] = col;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ 喷口

        private static void DrawNozzle(Vector3[] img, float[] sdMax, Nozzle n, Vector3 accentHot)
        {
            int signs = n.Nx > 1e-6f ? 2 : 1;
            for (int s = 0; s < signs; s++)
            {
                float sg = s == 0 ? 1f : -1f;
                float cx = n.Nx * sg;
                int ix0 = Mathf.Clamp(Mathf.FloorToInt((cx - n.Rx * 1.85f + 1f) / Px - 0.5f), 0, Size - 1);
                int ix1 = Mathf.Clamp(Mathf.CeilToInt((cx + n.Rx * 1.85f + 1f) / Px - 0.5f), 0, Size - 1);
                int iy0 = Mathf.Clamp(Mathf.FloorToInt((n.Ny - n.Ry * 1.85f + 1f) / Px - 0.5f), 0, Size - 1);
                int iy1 = Mathf.Clamp(Mathf.CeilToInt((n.Ny + n.Ry * 1.85f + 1f) / Px - 0.5f), 0, Size - 1);

                for (int y = iy0; y <= iy1; y++)
                {
                    float v = VOf(y);
                    int row = y * Size;
                    for (int x = ix0; x <= ix1; x++)
                    {
                        int idx = row + x;
                        if (sdMax[idx] <= 0f)
                            continue;
                        float u = UOf(x);
                        float ax = (u - cx) / n.Rx;
                        float ay = (v - n.Ny) / n.Ry;
                        float q = Mathf.Sqrt(ax * ax + ay * ay);

                        // 外围暖色辉光
                        float glow = Mathf.Pow(Mathf.Clamp01((1.85f - q) / 0.85f), 2.4f) * 0.20f;
                        img[idx] += accentHot * glow;

                        // 外环深金属舱体
                        if (q < 1.42f)
                        {
                            float t = Mathf.Clamp01((1.42f - q) / 0.42f);
                            Vector3 ink = Housing + (NacDark - Housing) * Mathf.Clamp01((q - 1.0f) / 0.42f);
                            img[idx] = Mix(img[idx], ink, t * 0.96f);
                        }
                        // 亮金属唇口
                        if (q > 1.16f && q < 1.30f)
                        {
                            float t = Mathf.Clamp01(1f - Mathf.Abs(q - 1.23f) / 0.07f);
                            img[idx] = Mix(img[idx], NozzleLip, t * 0.55f);
                        }
                        // 强调色内环
                        if (q < 1.02f && q > 0.84f)
                            img[idx] = Mix(img[idx], accentHot, 0.90f);
                        // 白热核心 + 同心刻痕
                        if (q <= 0.84f)
                        {
                            float tt = Mathf.Clamp01(q / 0.84f);
                            Vector3 col = EngineCore + (EngineHot - EngineCore) * Mathf.Pow(tt, 1.3f);
                            float rr = Mathf.Clamp01(1f - Mathf.Abs(tt - 0.58f) / 0.10f);
                            col *= 1f - 0.28f * rr;
                            float rr2 = Mathf.Clamp01(1f - Mathf.Abs(tt - 0.30f) / 0.08f);
                            col *= 1f - 0.16f * rr2;
                            float core = Mathf.Clamp01(1f - tt / 0.34f);
                            col = col + (WhiteCore - col) * Mathf.Pow(core, 1.2f);
                            img[idx] = img[idx] + (col - img[idx]) * 0.97f;
                        }
                    }
                }
            }
        }

        private static void DrawSlot(Vector3[] img, float[] sdMax, Slot slot, Vector3 accentHot)
        {
            Vector2[] pts = slot.Pts;
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 p = pts[i];
                if (p.x < x0) x0 = p.x;
                if (p.x > x1) x1 = p.x;
                if (p.y < y0) y0 = p.y;
                if (p.y > y1) y1 = p.y;
                float mx = -p.x;
                if (mx < x0) x0 = mx;
                if (mx > x1) x1 = mx;
            }
            float m = Px * 8f;
            int ix0 = Mathf.Clamp(Mathf.FloorToInt((x0 - m + 1f) / Px - 0.5f), 0, Size - 1);
            int ix1 = Mathf.Clamp(Mathf.CeilToInt((x1 + m + 1f) / Px - 0.5f), 0, Size - 1);
            int iy0 = Mathf.Clamp(Mathf.FloorToInt((y0 - m + 1f) / Px - 0.5f), 0, Size - 1);
            int iy1 = Mathf.Clamp(Mathf.CeilToInt((y1 + m + 1f) / Px - 0.5f), 0, Size - 1);

            for (int y = iy0; y <= iy1; y++)
            {
                float v = VOf(y);
                int row = y * Size;
                for (int x = ix0; x <= ix1; x++)
                {
                    int idx = row + x;
                    if (sdMax[idx] <= 0f)
                        continue;
                    float u = UOf(x);
                    float d = 1e9f;
                    for (int i = 0; i < pts.Length - 1; i++)
                    {
                        Vector2 a = pts[i];
                        Vector2 b = pts[i + 1];
                        d = Mathf.Min(d, SegDist(u, v, a.x, a.y, b.x, b.y));
                        d = Mathf.Min(d, SegDist(u, v, -a.x, a.y, -b.x, b.y));
                    }
                    float cov = Mathf.Clamp01((Px * 4.2f - d) / (Px * 0.8f) + 0.5f);
                    if (cov > 0f)
                        img[idx] *= 1f - 0.55f * cov;
                    cov = Mathf.Clamp01((Px * slot.HotPx - d) / (Px * 0.5f) + 0.5f);
                    if (cov > 0f)
                        img[idx] = Mix(img[idx], EngineCore, cov * 0.95f);
                    cov = Mathf.Clamp01((Px * (slot.HotPx + 3.5f) - d) / (Px * 0.8f) + 0.5f);
                    if (cov > 0f)
                        img[idx] += accentHot * (Mathf.Pow(cov, 2f) * 0.18f);
                }
            }
        }

        // ------------------------------------------------------------------ 输出

        private static Sprite Finish(Design design, Vector3[] img, float[] sdMax, Vector3 accent, Vector3 accentHot, Vector3 accentAlt)
        {
            Color32[] px = new Color32[Pixels];
            for (int y = 0; y < Size; y++)
            {
                int row = y * Size;
                int ym = y > 0 ? y - 1 : 0;
                int yp = y < Size - 1 ? y + 1 : Size - 1;
                for (int x = 0; x < Size; x++)
                {
                    int idx = row + x;
                    int xm = x > 0 ? x - 1 : 0;
                    int xp = x < Size - 1 ? x + 1 : Size - 1;
                    float d = sdMax[idx];

                    float gu = (sdMax[row + xp] - sdMax[row + xm]) * 0.5f;
                    float gv = (sdMax[yp * Size + x] - sdMax[ym * Size + x]) * 0.5f;
                    float mag = Mathf.Sqrt(gu * gu + gv * gv) + 1e-6f;
                    float nx = -gu / mag;
                    float ny = -gv / mag;
                    float lit = nx * SunLight.x + ny * SunLight.y;

                    Vector3 c = img[idx];
                    if (d >= 0f && d < RimW)
                    {
                        float k = 1f - d / RimW;
                        c = Mix(c, Outline, k * 0.90f);
                    }
                    else if (d >= RimW && d < RimW + BevelW)
                    {
                        float w = 1f - (d - RimW) / BevelW;
                        float hb = Mathf.Clamp01(lit) * w * 0.62f;
                        c = Mix(c, Bevel, hb);
                        float sh = Mathf.Clamp01(-lit) * w * 0.36f;
                        c *= 1f - sh;
                    }

                    float alpha = Mathf.Clamp01((d + OutlineW) / Aa);
                    if (d < 0f && alpha > 0f)
                        c = Outline;
                    if (alpha <= 0f)
                    {
                        px[idx] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    // 机体色分级：中性蓝灰金属 + 各自的强调色。
                    // 只作用在中间调到暗部（高光、倒角、描边保持中性），
                    // 否则整架飞机会变成塑料玩具；峰值落在 lum≈0.30 的机身平板区。
                    float lum = c.x * 0.299f + c.y * 0.587f + c.z * 0.114f;
                    float band = Mathf.Clamp01(1f - Mathf.Abs(lum - 0.34f) / 0.40f);
                    band = band * band * (3f - 2f * band);
                    band *= Mathf.Clamp01((lum - 0.055f) / 0.12f);   // 外描边与最暗处不染色
                    {
                        // 双色调：机身中线附近用主色，翼展外侧渐变到第二色。
                        // u 是归一化横向坐标（-1..1）：|u|≈0.30 起偏移，0.60 处完全切到 accentAlt。
                        float u = (x + 0.5f) / Size * 2f - 1f;
                        float outer = Mathf.Clamp01((Mathf.Abs(u) - 0.30f) / 0.30f);
                        Vector3 tone = Vector3.Lerp(accent, accentAlt, outer);
                        Vector3 hue = tone * (0.60f + 0.85f * lum);
                        c = Mix(c, hue, design.TintMix * band);
                    }

                    px[idx] = new Color32(ToByte(c.x), ToByte(c.y), ToByte(c.z), ToByte(alpha));
                }
            }

            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.name = "ShipArt_" + design.Name;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = "ShipArt_" + design.Name;
            return sprite;
        }

        // ------------------------------------------------------------------ 数学工具

        private static byte ToByte(float x)
        {
            return (byte)(Mathf.Clamp01(x) * 255f + 0.5f);
        }

        private static float UOf(int x)
        {
            return (x + 0.5f) * Px - 1f;
        }

        private static float VOf(int y)
        {
            return (y + 0.5f) * Px - 1f;
        }

        private static Vector3 C(float r, float g, float b)
        {
            return new Vector3(r, g, b);
        }

        private static Vector3 Clamp01(Vector3 c)
        {
            return new Vector3(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z));
        }

        private static Vector3 Mix(Vector3 a, Vector3 b, float t)
        {
            return a + (b - a) * t;
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(b - a, 1e-9f));
            return t * t * (3f - 2f * t);
        }

        private static float SegDist(float u, float v, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float l2 = dx * dx + dy * dy;
            float t = 0f;
            if (l2 > 1e-14f)
            {
                t = ((u - ax) * dx + (v - ay) * dy) / l2;
                if (t < 0f) t = 0f;
                else if (t > 1f) t = 1f;
            }
            float qx = ax + dx * t - u;
            float qy = ay + dy * t - v;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        private static float PolylineDist(Vector2[] pts, float u, float v)
        {
            float best = 1e9f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                float d = SegDist(u, v, a.x, a.y, b.x, b.y);
                if (d < best) best = d;
            }
            return best;
        }

        private static Vector2 P(float x, float y)
        {
            return new Vector2(x, y);
        }

        private static Vector2[] Path(params Vector2[] pts)
        {
            return pts;
        }

        private static Vector2[] MirrorPoly(Vector2[] right)
        {
            List<Vector2> all = new List<Vector2>(right.Length * 2);
            for (int i = 0; i < right.Length; i++)
                all.Add(right[i]);
            for (int i = right.Length - 1; i >= 0; i--)
                all.Add(new Vector2(-right[i].x, right[i].y));

            List<Vector2> res = new List<Vector2>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                Vector2 p = all[i];
                if (res.Count > 0)
                {
                    Vector2 q = res[res.Count - 1];
                    if (Mathf.Abs(q.x - p.x) < 1e-6f && Mathf.Abs(q.y - p.y) < 1e-6f)
                        continue;
                }
                res.Add(p);
            }
            if (res.Count > 1)
            {
                Vector2 a = res[0];
                Vector2 b = res[res.Count - 1];
                if (Mathf.Abs(a.x - b.x) < 1e-6f && Mathf.Abs(a.y - b.y) < 1e-6f)
                    res.RemoveAt(res.Count - 1);
            }
            return res.ToArray();
        }

        // ------------------------------------------------------------------ 数据装配

        private static void AddCenter(Design d, int role, params Vector2[] right)
        {
            d.Shapes.Add(new Shape(role, MirrorPoly(right), null));
        }

        private static void AddSide(Design d, int role, Vector2[] right)
        {
            d.Shapes.Add(new Shape(role, right, null));
            Vector2[] mir = new Vector2[right.Length];
            for (int i = 0; i < right.Length; i++)
                mir[i] = new Vector2(-right[i].x, right[i].y);
            d.Shapes.Add(new Shape(role, mir, null));
        }

        private static void AddSide(Design d, int role, Vector2[] right, Vector2[] lead)
        {
            d.Shapes.Add(new Shape(role, right, lead));
            Vector2[] mir = new Vector2[right.Length];
            Vector2[] mirLead = new Vector2[lead.Length];
            for (int i = 0; i < right.Length; i++)
                mir[i] = new Vector2(-right[i].x, right[i].y);
            for (int i = 0; i < lead.Length; i++)
                mirLead[i] = new Vector2(-lead[i].x, lead[i].y);
            d.Shapes.Add(new Shape(role, mir, mirLead));
        }

        private static void St(Design d, int style, float widthPx, params Vector2[] pts)
        {
            d.Strokes.Add(new Stroke(pts, true, widthPx, style, 1f));
        }

        private static void St(Design d, int style, float widthPx, float strength, params Vector2[] pts)
        {
            d.Strokes.Add(new Stroke(pts, true, widthPx, style, strength));
        }

        private static void StOne(Design d, int style, float widthPx, params Vector2[] pts)
        {
            d.Strokes.Add(new Stroke(pts, false, widthPx, style, 1f));
        }

        private static void StOne(Design d, int style, float widthPx, float strength, params Vector2[] pts)
        {
            d.Strokes.Add(new Stroke(pts, false, widthPx, style, strength));
        }

        private static void AddNozzle(Design d, float nx, float ny, float rx, float ry)
        {
            d.Nozzles.Add(new Nozzle(nx, ny, rx, ry));
        }

        // ------------------------------------------------------------------ HAMMER 重锤：短粗重装

        private static Design BuildHammer()
        {
            // 封面那艘：宽大的三角翼炮艇。轮廓拆成四层 —— 三角翼、中央脊线机身、
            // 两侧外挂引擎舱、机鼻进气格栅；细节全部是"长刻线 + 双线板厚"，
            // 因为它在 214px 的展示位上，只有长线还剩得下来。
            Design d = new Design
            {
                Name = "HAMMER",
                DefaultAccent = new Color(1f, 0.74f, 0.28f, 1f),
                TintMix = 0.30f,
                CockpitY = 0.300f,
                CockpitRx = 0.062f,
                CockpitRy = 0.155f,
            };

            // ---- 三角主翼：前缘一条近乎直的后掠线，翼尖切角，后缘向内收成锯齿 ----
            AddSide(d, RoleWing,
                Path(P(0.150f, 0.470f), P(0.330f, 0.360f), P(0.560f, 0.212f), P(0.760f, 0.070f),
                     P(0.900f, -0.048f), P(0.958f, -0.128f), P(0.940f, -0.238f), P(0.856f, -0.232f),
                     P(0.700f, -0.300f), P(0.540f, -0.392f), P(0.412f, -0.476f), P(0.338f, -0.596f),
                     P(0.296f, -0.672f), P(0.200f, -0.694f), P(0.170f, -0.560f), P(0.168f, -0.180f)),
                Path(P(0.150f, 0.470f), P(0.330f, 0.360f), P(0.560f, 0.212f),
                     P(0.760f, 0.070f), P(0.900f, -0.048f)));

            // ---- 两侧引擎舱：外挂圆柱，前半截埋进翼根，后半截露在机尾外 ----
            AddSide(d, RoleNacelle,
                Path(P(0.352f, 0.196f), P(0.470f, 0.236f), P(0.560f, 0.176f), P(0.596f, -0.060f),
                     P(0.612f, -0.420f), P(0.596f, -0.690f), P(0.548f, -0.782f), P(0.432f, -0.792f),
                     P(0.372f, -0.700f), P(0.352f, -0.400f), P(0.344f, -0.100f)),
                Path(P(0.352f, 0.196f), P(0.470f, 0.236f), P(0.560f, 0.176f)));

            // ---- 引擎舱进气：舱口前脸的暗色斜面 ----
            AddSide(d, RoleIntake,
                Path(P(0.372f, 0.196f), P(0.470f, 0.226f), P(0.545f, 0.170f),
                     P(0.520f, 0.028f), P(0.400f, 0.030f), P(0.362f, 0.096f)));

            // ---- 翼面装甲板：外侧一块梯形、内侧一块三角，反射率与机翼不同 ----
            AddSide(d, RolePlate, Path(
                P(0.430f, 0.268f), P(0.700f, 0.124f), P(0.826f, 0.026f),
                P(0.800f, -0.052f), P(0.660f, 0.014f), P(0.442f, 0.140f)));

            AddSide(d, RolePlate, Path(
                P(0.230f, 0.330f), P(0.424f, 0.258f), P(0.442f, 0.140f), P(0.276f, 0.214f)));

            // ---- 翼根整流罩：把翼根和引擎舱前缘之间的硬接缝补成一段斜切过渡 ----
            AddSide(d, RolePlate, Path(
                P(0.300f, 0.300f), P(0.404f, 0.244f), P(0.470f, 0.190f),
                P(0.424f, 0.108f), P(0.322f, 0.146f)));

            // ---- 中央机身：从尖机鼻一路收到机尾，最宽处压在脊线上 ----
            AddCenter(d, RoleHull, Path(
                P(0.000f, 0.962f), P(0.024f, 0.930f), P(0.060f, 0.868f), P(0.092f, 0.780f),
                P(0.116f, 0.660f), P(0.136f, 0.500f), P(0.150f, 0.300f), P(0.160f, 0.040f),
                P(0.168f, -0.240f), P(0.176f, -0.460f), P(0.170f, -0.640f), P(0.140f, -0.780f),
                P(0.084f, -0.852f), P(0.000f, -0.868f)));

            // ---- 机鼻格栅：进气口 + 分流隔条，压在机鼻中段 ----
            AddCenter(d, RoleIntake, Path(
                P(0.004f, 0.842f), P(0.030f, 0.812f), P(0.044f, 0.756f),
                P(0.034f, 0.700f), P(0.004f, 0.686f)));

            // ---- 背脊：一条抬起的窄平台，把机身分成左右两半 ----
            AddCenter(d, RoleRidge, Path(
                P(0.000f, 0.700f), P(0.040f, 0.640f), P(0.052f, 0.460f), P(0.058f, 0.180f),
                P(0.056f, -0.140f), P(0.048f, -0.420f), P(0.000f, -0.560f)));

            // ================= 刻线：先长后短，先主后辅 =================

            // 翼前缘双线：一条强调色 + 一条发丝线，做出"锐利的前缘"
            St(d, StyleAccent, 1.8f, 0.92f, Path(
                P(0.156f, 0.452f), P(0.336f, 0.342f), P(0.566f, 0.196f),
                P(0.766f, 0.056f), P(0.906f, -0.060f)));
            St(d, StyleSeamFaint, 1.0f, 0.85f, Path(
                P(0.176f, 0.418f), P(0.352f, 0.312f), P(0.580f, 0.168f),
                P(0.776f, 0.030f), P(0.912f, -0.082f)));

            // 翼尖航行灯：一小段最亮的强调线
            St(d, StyleAccentHot, 2.2f, 0.95f, Path(
                P(0.948f, -0.086f), P(0.952f, -0.146f), P(0.936f, -0.208f)));

            // 翼面放射状板线：从翼根斜着扫向翼尖，模拟蒙皮分块
            St(d, StyleSeamFaint, 1.2f, Path(P(0.170f, 0.330f), P(0.420f, 0.180f), P(0.700f, -0.010f), P(0.880f, -0.150f)));
            St(d, StyleSeamFaint, 1.2f, Path(P(0.182f, 0.100f), P(0.430f, 0.020f), P(0.700f, -0.150f), P(0.860f, -0.250f)));
            St(d, StyleSeamFaint, 1.2f, Path(P(0.190f, -0.130f), P(0.440f, -0.150f), P(0.690f, -0.290f), P(0.820f, -0.360f)));
            St(d, StyleSeam, 1.3f, Path(P(0.196f, -0.360f), P(0.460f, -0.330f), P(0.660f, -0.430f)));

            // 翼后缘锯齿：两段短折线，让翼尾不是一条直线
            St(d, StyleSeam, 1.4f, Path(P(0.560f, -0.318f), P(0.520f, -0.396f), P(0.560f, -0.420f)));
            St(d, StyleSeam, 1.4f, Path(P(0.700f, -0.246f), P(0.660f, -0.330f), P(0.700f, -0.352f)));

            // 机身长刻线：两条贯穿全机，是"分层面板"的主要手段
            St(d, StyleSeam, 1.5f, Path(P(0.098f, 0.786f), P(0.132f, 0.420f), P(0.142f, 0.040f), P(0.150f, -0.360f), P(0.140f, -0.700f)));
            St(d, StyleSeamFaint, 1.1f, Path(P(0.074f, 0.760f), P(0.106f, 0.400f), P(0.118f, 0.020f), P(0.126f, -0.380f)));

            // 机鼻横向切角 + 中段装甲带（双线做板厚）
            StOne(d, StyleSeam, 1.6f, Path(P(-0.062f, 0.792f), P(0.062f, 0.792f)));
            StOne(d, StyleSeam, 1.6f, Path(P(-0.120f, 0.436f), P(0.120f, 0.436f)));
            StOne(d, StyleSeamFaint, 0.9f, Path(P(-0.126f, 0.418f), P(0.126f, 0.418f)));
            StOne(d, StyleSeam, 1.6f, Path(P(-0.150f, 0.052f), P(0.150f, 0.052f)));
            StOne(d, StyleSeamFaint, 0.9f, Path(P(-0.156f, 0.034f), P(0.156f, 0.034f)));
            StOne(d, StyleSeam, 1.5f, Path(P(-0.164f, -0.352f), P(0.164f, -0.352f)));
            StOne(d, StyleSeamFaint, 0.9f, Path(P(-0.168f, -0.370f), P(0.168f, -0.370f)));

            // 机鼻进气格栅：竖隔条（镜像即可）
            St(d, StyleSeamFaint, 1.2f, Path(P(0.014f, 0.814f), P(0.014f, 0.612f)));
            St(d, StyleSeamFaint, 1.2f, Path(P(0.034f, 0.796f), P(0.034f, 0.606f)));
            St(d, StyleSeam, 1.3f, Path(P(0.006f, 0.830f), P(0.068f, 0.712f)));

            // 引擎舱：两道环形板线 + 检修盖板 + 散热格栅
            St(d, StyleSeam, 1.5f, Path(P(0.356f, 0.150f), P(0.470f, 0.190f), P(0.566f, 0.130f)));
            St(d, StyleSeamFaint, 1.0f, Path(P(0.352f, 0.126f), P(0.468f, 0.166f), P(0.566f, 0.106f)));
            St(d, StyleSeam, 1.5f, Path(P(0.352f, -0.300f), P(0.478f, -0.330f), P(0.598f, -0.296f)));
            St(d, StyleSeamFaint, 1.0f, Path(P(0.352f, -0.326f), P(0.478f, -0.356f), P(0.598f, -0.322f)));
            St(d, StyleSeam, 1.3f, Path(P(0.392f, -0.470f), P(0.560f, -0.500f)));
            St(d, StyleSeam, 1.3f, Path(P(0.396f, -0.548f), P(0.566f, -0.578f)));
            St(d, StyleSeamFaint, 1.1f, Path(P(0.386f, -0.610f), P(0.560f, -0.640f)));

            // 机尾散热片：三道短斜线，指向喷口
            St(d, StyleSeam, 1.2f, Path(P(0.148f, -0.700f), P(0.284f, -0.742f)));
            St(d, StyleSeamFaint, 1.0f, Path(P(0.140f, -0.768f), P(0.272f, -0.806f)));
            St(d, StyleSeamFaint, 0.9f, Path(P(0.132f, -0.828f), P(0.256f, -0.860f)));

            // 铆钉：沿两条主缝各打一排（2px 短划当成点）
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float rx = Mathf.Lerp(0.150f, 0.760f, t);
                float ry = Mathf.Lerp(0.392f, -0.176f, t);
                St(d, StyleSeamFaint, 1.6f, 0.72f, Path(P(rx, ry), P(rx + 0.005f, ry - 0.006f)));
            }
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                float ry = Mathf.Lerp(0.560f, -0.520f, t);
                St(d, StyleSeamFaint, 1.6f, 0.72f, Path(P(0.176f, ry), P(0.181f, ry - 0.006f)));
            }

            // 背脊高光边与机身暗部：脊线亮、两侧压暗，机身才有"厚度"
            StOne(d, StyleLight, 1.2f, 0.55f, Path(P(0f, 0.688f), P(0f, 0.400f), P(0f, 0.120f), P(0f, -0.180f)));
            St(d, StyleAo, 5.0f, 0.9f, Path(P(0.156f, 0.470f), P(0.176f, 0.040f), P(0.184f, -0.420f)));
            St(d, StyleAo, 4.0f, 0.7f, Path(P(0.344f, 0.176f), P(0.348f, -0.300f), P(0.360f, -0.660f)));

            // 喷口同心环：外环 + 内环，做出喷管厚度
            St(d, StyleSeam, 1.4f, Path(
                P(0.404f, -0.760f), P(0.442f, -0.812f), P(0.520f, -0.820f), P(0.564f, -0.762f)));
            St(d, StyleSeam, 1.1f, Path(
                P(0.420f, -0.776f), P(0.450f, -0.800f), P(0.514f, -0.806f), P(0.546f, -0.776f)));

            // 翼面装甲块：两片带倒角的板，边界只画内侧那几条（外侧是翼前缘强调线）
            St(d, StyleSeam, 1.2f, Path(
                P(0.300f, 0.250f), P(0.470f, 0.150f), P(0.620f, 0.040f)));
            St(d, StyleSeamFaint, 1.0f, Path(
                P(0.310f, 0.226f), P(0.468f, 0.130f), P(0.606f, 0.026f)));
            St(d, StyleSeam, 1.1f, Path(P(0.340f, 0.028f), P(0.520f, -0.070f), P(0.640f, -0.170f)));
            St(d, StyleSeamFaint, 1.0f, Path(P(0.470f, 0.244f), P(0.500f, 0.060f), P(0.470f, -0.140f)));
            St(d, StyleSeamFaint, 1.0f, Path(P(0.606f, -0.062f), P(0.634f, -0.180f)));

            // 机身侧板：一道横缝 + 一道斜缝，把机身分成前段/中段
            StOne(d, StyleSeam, 1.3f, Path(P(-0.100f, 0.606f), P(0.100f, 0.606f)));
            St(d, StyleSeamFaint, 1.0f, Path(P(0.104f, 0.588f), P(0.130f, 0.470f)));

            AddNozzle(d, 0.486f, -0.804f, 0.090f, 0.080f);
            return d;
        }
    }
}
