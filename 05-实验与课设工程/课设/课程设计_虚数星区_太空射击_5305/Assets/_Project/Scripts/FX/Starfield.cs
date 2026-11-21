using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 太空背景。
    ///
    /// 层次（由远及近、sortingOrder 由小到大）：
    ///   星区天幕 → 三层视差星点 → 透视网格 → 地平线辉光 → 流星
    ///
    /// 天幕走 ArtLibrary 加载的星云照片（bg_nebula_01~03），
    /// 照片缺失或导入设置坏掉时回退到 BackdropArt 的程序化绘制，保证背景不会是纯黑。
    ///
    /// UsePhotoBackdrop = true 时**不再建程序化行星与星云薄雾层**：
    /// 照片本身已经带行星（星区 01 右下、星区 02 左下）和云气，
    /// 再叠一层 BackdropArt.Planet / Wisps 等于在照片上又画了个球、又糊了层烟，
    /// 两套景深打架，画面既脏又分不出哪层是真的。
    /// 星点仍保留——照片里的星是烤在贴图上的，随背景一起缩放会糊，
    /// 而三层视差星点是"在飞"的动感来源，不能省。
    /// </summary>
    public class Starfield : MonoBehaviour
    {
        /// <summary>
        /// 天幕是否使用星云照片。置 false 则回到 BackdropArt 的纯程序化天幕（含程序化行星与薄雾）。
        /// </summary>
        private const bool UsePhotoBackdrop = true;

        private class StarNode
        {
            public Transform tr;
            public float speed;
        }

        // 地平线高度（世界坐标）。网格从这条线向下展开。
        private const float HorizonY = -1.15f;

        private readonly List<StarNode> stars = new List<StarNode>();
        private readonly List<StarNode> meteors = new List<StarNode>();

        private SpriteRenderer backdrop;      // 当前星区底图
        private SpriteRenderer backdropNext;  // 过渡用：新星区叠在上面淡入
        private float backdropBlend = 1f;
        private int currentSector = -1;

        private Transform planet;
        private SpriteRenderer planetRenderer;
        private float planetSpeed;
        private SpriteRenderer planetNext;   // 过渡用：新行星叠在上面淡入
        private float planetBlend = 1f;

        // 星云薄雾层：比天幕更近的一层彩色烟，慢慢飘，负责画面呼吸感
        private SpriteRenderer nebula;
        private SpriteRenderer nebulaNext;
        private float nebulaBlend = 1f;
        private float nebulaTime;

        private const float PlanetAlpha = 0.80f;

        private Transform[] gridH;
        private SpriteRenderer[] gridHsr;
        private SpriteRenderer horizonSr;
        private Color sectorTint = new Color(0.38f, 0.72f, 1f, 0.07f);
        private float gridScroll;

        private float wrapTop;
        private float wrapBottom;

        // ------------------------------------------------------------ 构建

        public void Build(int starCount)
        {
            wrapTop = GameArea.HalfHeight * 1.15f;
            wrapBottom = -GameArea.HalfHeight * 1.15f;

            BuildBackdrop();

            // 照片天幕自带行星与云气，这两层只在程序化模式下才建（见类注释）
            if (!UsePhotoBackdrop)
            {
                BuildNebula();
                BuildPlanet();
            }

            BuildStars(starCount);
            BuildGrid();
            BuildHorizonGlow();
            BuildMeteors(4);

            SetSector(0, 1f);
        }

        private GameObject NewSprite(string name, Sprite sprite, int order, Color tint)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = order;
            return go;
        }

        /// <summary>把一张 sprite 拉伸到覆盖整个可视范围（留一点余量给震动）</summary>
        private void FitToView(Transform target, Sprite sprite, float margin)
        {
            Vector3 size = sprite.bounds.size;
            target.localScale = new Vector3(
                GameArea.HalfWidth * 2f * margin / Mathf.Max(0.0001f, size.x),
                GameArea.HalfHeight * 2f * margin / Mathf.Max(0.0001f, size.y),
                1f);
        }

        private void BuildBackdrop()
        {
            // 两张渲染器：一张显示当前星区，另一张专门用来淡入新星区。
            // 只留一张的话，换星区时必须先淡出到全黑再淡入，玩家会看到一次闪黑。
            backdrop = NewSprite("Backdrop", null, -120, Color.white).GetComponent<SpriteRenderer>();
            backdropNext = NewSprite("BackdropNext", null, -119, new Color(1f, 1f, 1f, 0f)).GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 星区天幕。优先取 Art/Resources/Sprites 下的星云照片（bg_nebula_01 ~ 03），
        /// 取不到再回退程序化绘制——和 ArtLibrary 其它调用一样「允许失败」，
        /// 单张图被误删时那一关会退回渐变天幕，而不是整片黑屏。
        /// </summary>
        private static Sprite BackdropSprite(int index)
        {
            if (UsePhotoBackdrop)
            {
                Sprite photo = ArtLibrary.Get("bg_nebula_0" + (index + 1));
                if (photo != null) return photo;
            }
            return BackdropArt.Sky(index);
        }

        /// <summary>
        /// 切换星区底图。index 0/1 对应两个关卡，2 是 Boss 战。
        /// fadeSpeed &gt;= 1 表示瞬间切换（初始化时用），否则按速度交叉淡入。
        /// 同一星区重复调用会被忽略，所以关卡流程里随便调都不会闪烁。
        /// </summary>
        public void SetSector(int index, float fadeSpeed = 0.9f)
        {
            if (backdrop == null) return;
            if (index == currentSector) return;

            index = Mathf.Clamp(index, 0, 2);

            Sprite sprite = BackdropSprite(index);
            if (sprite == null)
            {
                // 极端情况（照片和程序化都失败）也不能让背景变成透明
                sprite = SpriteFactory.Backdrop();
            }

            // 行星与星云薄雾也要跟着换，但不能瞬间跳：和天幕一样走交叉淡化。
            bool instant = fadeSpeed >= 1f;
            CrossfadePlanet(index, instant);
            CrossfadeNebula(index, instant);


            // 地平线雾带和透视网格跟着星区换色：写死青色的话，
            // 压到红色 Boss 星区上就是一道突兀的蓝线。
            Color tint = BackdropArt.HorizonTint(index);
            sectorTint = tint;
            if (horizonSr != null) horizonSr.color = new Color(tint.r, tint.g, tint.b, 0.07f);

            currentSector = index;
            backdropNext.sprite = sprite;
            FitToView(backdropNext.transform, sprite, 1.02f);

            if (fadeSpeed >= 1f)
            {
                // 首帧直接显示，不做过渡
                backdrop.sprite = sprite;
                FitToView(backdrop.transform, sprite, 1.02f);
                backdropNext.color = new Color(1f, 1f, 1f, 0f);
                backdropBlend = 1f;
                sectorFadeSpeed = 1f;
                return;
            }

            backdropBlend = 0f;
            sectorFadeSpeed = Mathf.Max(0.05f, fadeSpeed);
            backdropNext.color = new Color(1f, 1f, 1f, 0f);
        }

        private float sectorFadeSpeed = 0.9f;

        private void UpdateBackdrop(float dt)
        {
            if (backdrop == null || backdropNext == null) return;
            if (backdropBlend >= 1f) return;

            backdropBlend = Mathf.Min(1f, backdropBlend + dt * sectorFadeSpeed);
            backdropNext.color = new Color(1f, 1f, 1f, backdropBlend);

            if (backdropBlend >= 1f)
            {
                // 过渡完成，把结果搬到主渲染器上，下一张再从 0 开始
                backdrop.sprite = backdropNext.sprite;
                FitToView(backdrop.transform, backdrop.sprite, 1.02f);
                backdropNext.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        private void BuildPlanet()
        {
            // 行星也是程序化画的（BackdropArt.Planet）：倾斜云带 + 硬明暗交界 + 临边压暗
            // + 受光侧细亮弧。旧版是一个平滑渐变的青球，缩到 440 像素就是一张贴纸。
            Sprite sprite = BackdropArt.Planet(0);

            GameObject go = NewSprite("Planet", sprite, -105, new Color(0.90f, 0.95f, 1f, 0.80f));

            // 按「希望占多大」换算缩放，不依赖素材本身是多少像素、多少 PPU
            //
            // 直径 4.6 → 6.4、中心推到 (0.88, 0.52)：让球被画面右上角切掉一块。
            // 整颗球完整落在框里时，怎么打光都像"贴上去的一张贴纸"；
            // 被边界裁掉之后读的是"一颗巨大的远景行星"，而不是"画面里放了个球"。
            const float diameter = 6.4f;
            float scale = diameter / Mathf.Max(0.0001f, sprite.bounds.size.x);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.localPosition = new Vector3(GameArea.HalfWidth * 0.88f, GameArea.HalfHeight * 0.52f, 0f);

            planetRenderer = go.GetComponent<SpriteRenderer>();
            planet = go.transform;
            planetSpeed = 0.07f;

            // 第二张行星渲染器只用于星区切换的交叉淡化，平时 alpha = 0
            GameObject next = NewSprite("PlanetNext", sprite, -104, new Color(1f, 1f, 1f, 0f));
            next.transform.SetParent(transform, false);
            next.transform.localScale = go.transform.localScale;
            next.transform.localPosition = go.transform.localPosition;
            planetNext = next.GetComponent<SpriteRenderer>();
        }

        private void BuildNebula()
        {
            // 薄雾层压在天幕之上、行星之下：静止贴图靠它"活"起来
            Sprite wisp = BackdropArt.Wisps(0);
            nebula = NewSprite("NebulaDrift", wisp, -110, Color.white).GetComponent<SpriteRenderer>();
            FitToView(nebula.transform, wisp, 1.10f);

            nebulaNext = NewSprite("NebulaDriftNext", wisp, -109, new Color(1f, 1f, 1f, 0f)).GetComponent<SpriteRenderer>();
            FitToView(nebulaNext.transform, wisp, 1.10f);
        }

        private void CrossfadePlanet(int index, bool instant)
        {
            if (planetRenderer == null || planetNext == null) return;

            Sprite sprite = BackdropArt.Planet(index);
            planetNext.sprite = sprite;
            planetNext.transform.localScale = planetRenderer.transform.localScale;

            if (instant)
            {
                planetRenderer.sprite = sprite;
                planetRenderer.color = new Color(1f, 1f, 1f, PlanetAlpha);
                planetNext.color = new Color(1f, 1f, 1f, 0f);
                planetBlend = 1f;
                return;
            }

            planetBlend = 0f;
            planetNext.color = new Color(1f, 1f, 1f, 0f);
        }

        private void CrossfadeNebula(int index, bool instant)
        {
            if (nebula == null || nebulaNext == null) return;

            Sprite wisp = BackdropArt.Wisps(index);
            nebulaNext.sprite = wisp;
            FitToView(nebulaNext.transform, wisp, 1.10f);

            if (instant)
            {
                nebula.sprite = wisp;
                FitToView(nebula.transform, wisp, 1.10f);
                nebulaNext.color = new Color(1f, 1f, 1f, 0f);
                nebulaBlend = 1f;
                return;
            }

            nebulaBlend = 0f;
            nebulaNext.color = new Color(1f, 1f, 1f, 0f);
        }

        private void UpdateNebula(float dt)
        {
            if (nebula == null) return;

            nebulaTime += dt;

            // 极慢漂移 + 微旋转：单帧看不出来，连起来看画面在呼吸
            float x = Mathf.Sin(nebulaTime * 0.05f) * 0.55f;
            float y = Mathf.Cos(nebulaTime * 0.037f) * 0.35f;
            nebula.transform.localPosition = new Vector3(x, y, 0f);
            nebula.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(nebulaTime * 0.021f) * 1.6f);

            if (nebulaNext == null) return;

            nebulaNext.transform.localPosition = nebula.transform.localPosition;
            nebulaNext.transform.localRotation = nebula.transform.localRotation;

            if (nebulaBlend >= 1f) return;

            nebulaBlend = Mathf.Min(1f, nebulaBlend + dt * 0.8f);
            nebulaNext.color = new Color(1f, 1f, 1f, nebulaBlend);

            if (nebulaBlend >= 1f)
            {
                nebula.sprite = nebulaNext.sprite;
                FitToView(nebula.transform, nebula.sprite, 1.10f);
                nebula.color = Color.white;
                nebulaNext.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        private void BuildStars(int starCount)
        {
            float halfW = GameArea.HalfWidth;

            int farCount = Mathf.RoundToInt(starCount * 0.54f);
            int midCount = Mathf.RoundToInt(starCount * 0.32f);
            int nearCount = Mathf.Max(6, starCount - farCount - midCount);

            // 三层：远层小而暗、落得慢；近层大而亮、落得快，并沿 y 拉长成流线。
            //
            // 注意缩放值：localScale 乘在素材尺寸上。素材 fx_star.png 是 128×128、
            // 100 PPU（1.28 单位），所以 scale 0.05 只有 0.064 单位、约 6 像素——
            // 换素材之后这些数字必须按 sprite.bounds.size 重算，照抄旧的会全画歪。
            //
            // 用柔和光点而不是 Kenney 的十字星：十字星是卡通风格，
            // 压在写实星云图上会像撒了一把亮片，很跳。
            Sprite starSprite = ArtLibrary.Get("fx_star", SpriteFactory.Star());

            AddStarLayer(farCount, 0.030f, 0.070f, 0.16f, 0.30f, 0.14f, 0.42f,
                starSprite, false, -100, halfW);
            AddStarLayer(midCount, 0.070f, 0.120f, 0.45f, 0.80f, 0.32f, 0.62f,
                starSprite, false, -98, halfW);
            AddStarLayer(nearCount, 0.120f, 0.200f, 1.10f, 2.10f, 0.60f, 0.92f,
                starSprite, true, -96, halfW);
        }

        private void AddStarLayer(int count, float minScale, float maxScale,
                                  float minSpeed, float maxSpeed,
                                  float minAlpha, float maxAlpha,
                                  Sprite sprite, bool streak, int order, float halfW)
        {
            for (int i = 0; i < count; i++)
            {
                float scale = Random.Range(minScale, maxScale);
                Color tint = Random.Range(0.82f, 1f) * new Color(0.86f, 0.93f, 1f, 1f);
                tint.a = Random.Range(minAlpha, maxAlpha);

                GameObject go = NewSprite("Star", sprite, order, tint);

                // 最近的一层把星点沿 y 拉长，配合更快的下落速度做出“飞行”的动感
                float stretch = streak ? Random.Range(1.5f, 2.6f) : 1f;
                go.transform.localScale = new Vector3(scale, scale * stretch, 1f);
                go.transform.localPosition = new Vector3(
                    Random.Range(-halfW * 1.15f, halfW * 1.15f),
                    Random.Range(wrapBottom, wrapTop),
                    0f);

                StarNode node = new StarNode();
                node.tr = go.transform;
                node.speed = Random.Range(minSpeed, maxSpeed);
                stars.Add(node);
            }
        }

        private Transform CreateLine(Vector2 from, Vector2 to, float thickness, Color color, int order)
        {
            GameObject go = NewSprite("GridLine", SpriteFactory.Pixel(), order, color);

            Vector2 delta = to - from;
            float length = delta.magnitude;

            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localScale = new Vector3(thickness, length, 1f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg);
            return go.transform;
        }

        private void BuildGrid()
        {
            // 竖向汇聚线已经删掉：太空里没有「地面」，那些线只会把画面划成一块
            // Tron 地板，和星云、行星抢注意力。现在只留横向透视线当空间提示。

            // 横向线：位置每帧按透视重算，向观众滚动
            const int hCount = 7;
            gridH = new Transform[hCount];
            gridHsr = new SpriteRenderer[hCount];

            for (int i = 0; i < hCount; i++)
            {
                GameObject go = NewSprite("GridH", SpriteFactory.Pixel(), -91, new Color(0.38f, 0.82f, 1f, 0.06f));
                gridH[i] = go.transform;
                gridHsr[i] = go.GetComponent<SpriteRenderer>();
            }

            UpdateGrid(0f);
        }

        private void BuildHorizonGlow()
        {
            // 高度 0.42 → 1.6、alpha 0.12 → 0.07：原来那根 40 像素高的椭圆被拉成
            // 4 倍屏宽后，上下边缘变成一条硬邦邦的横线，在干净的天幕上非常扎眼。
            // 摊高、压淡之后它才是一条"地面附近的雾"，而不是一条线。
            GameObject go = NewSprite("HorizonGlow", SpriteFactory.EngineGlow(), -93,
                new Color(0.35f, 0.72f, 1f, 0.07f));

            go.transform.localPosition = new Vector3(0f, HorizonY, 0f);
            go.transform.localScale = new Vector3(GameArea.HalfWidth * 4.2f, 1.6f, 1f);
            horizonSr = go.GetComponent<SpriteRenderer>();
        }

        private void BuildMeteors(int count)
        {
            float halfW = GameArea.HalfWidth;

            Sprite streakSprite = ArtLibrary.Get("fx_star", SpriteFactory.Star());

            for (int i = 0; i < count; i++)
            {
                Color tint = new Color(0.75f, 0.92f, 1f, Random.Range(0.25f, 0.55f));
                GameObject go = NewSprite("Meteor", streakSprite, -80, tint);

                // 沿 y 拉长成流线。素材 1.28 单位，所以这里的数值很小才有细长条的观感
                float scale = Random.Range(0.016f, 0.028f);
                go.transform.localScale = new Vector3(scale, scale * Random.Range(9f, 16f), 1f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(6f, 18f) * (Random.value < 0.5f ? 1f : -1f));
                go.transform.localPosition = RandomMeteorStart(halfW);

                StarNode node = new StarNode();
                node.tr = go.transform;
                node.speed = Random.Range(9f, 17f);
                meteors.Add(node);
            }
        }

        private Vector3 RandomMeteorStart(float halfW)
        {
            return new Vector3(
                Random.Range(-halfW * 1.15f, halfW * 1.15f),
                Random.Range(wrapTop, wrapTop + 4f),
                0f);
        }

        // ------------------------------------------------------------ 每帧

        private void Update()
        {
            float dt = Time.deltaTime;
            float halfW = GameArea.HalfWidth;

            UpdateStars(dt, halfW);
            UpdateBackdrop(dt);
            UpdatePlanet(dt);
            UpdateNebula(dt);
            UpdateGrid(dt);
            UpdateMeteors(dt, halfW);
        }

        private void UpdateStars(float dt, float halfW)
        {
            for (int i = 0; i < stars.Count; i++)
            {
                StarNode node = stars[i];
                if (node.tr == null) continue;

                Vector3 position = node.tr.localPosition;
                position.y -= node.speed * dt;

                if (position.y < wrapBottom)
                {
                    position.y = wrapTop;
                    position.x = Random.Range(-halfW * 1.15f, halfW * 1.15f);
                }

                node.tr.localPosition = position;
            }
        }

        private void UpdatePlanet(float dt)
        {
            if (planet == null) return;

            Vector3 position = planet.localPosition;
            position.x -= planetSpeed * dt;

            if (position.x < -GameArea.HalfWidth * 1.4f) position.x = GameArea.HalfWidth * 1.4f;

            planet.localPosition = position;

            // 换星区时两张行星叠着交叉淡化：旧球淡出、新球淡入，避免"啪"地跳一下
            if (planetNext != null)
            {
                planetNext.transform.localPosition = position;
                planetNext.transform.localScale = planet.localScale;

                if (planetBlend < 1f)
                {
                    planetBlend = Mathf.Min(1f, planetBlend + dt * 0.85f);
                    Color oldColor = planetRenderer.color;
                    oldColor.a = PlanetAlpha * (1f - planetBlend);
                    planetRenderer.color = oldColor;
                    planetNext.color = new Color(1f, 1f, 1f, PlanetAlpha * planetBlend);

                    if (planetBlend >= 1f)
                    {
                        planetRenderer.sprite = planetNext.sprite;
                        oldColor.a = PlanetAlpha;
                        planetRenderer.color = oldColor;
                        planetNext.color = new Color(1f, 1f, 1f, 0f);
                    }
                }
            }
        }
        /// <summary>
        /// 横向网格线按 p 的平方分布：靠近地平线处密集、越靠近观众越稀疏，
        /// 这是透视投影的直接结果（屏幕 y 与地面距离不是线性关系）。
        /// </summary>
        private void UpdateGrid(float dt)
        {
            if (gridH == null) return;

            gridScroll += dt * 0.17f;

            float bottomY = -GameArea.HalfHeight - 0.9f;
            int count = gridH.Length;

            for (int i = 0; i < count; i++)
            {
                Transform line = gridH[i];
                if (line == null) continue;

                float p = Mathf.Repeat(gridScroll + i / (float)count, 1f);
                float t = p * p;

                float y = Mathf.Lerp(HorizonY, bottomY, t);
                float halfWidth = Mathf.Lerp(0.30f, GameArea.HalfWidth * 1.6f, t);

                line.localPosition = new Vector3(0f, y, 0f);
                line.localScale = new Vector3(halfWidth * 2f, 0.028f, 1f);

                if (gridHsr[i] != null)
                {
                    Color c = sectorTint;
                    c.a = Mathf.Lerp(0.004f, 0.055f, t);
                    gridHsr[i].color = c;
                }
            }
        }

        private void UpdateMeteors(float dt, float halfW)
        {
            for (int i = 0; i < meteors.Count; i++)
            {
                StarNode node = meteors[i];
                if (node.tr == null) continue;

                Vector3 position = node.tr.localPosition;
                position.y -= node.speed * dt;

                if (position.y < -GameArea.HalfHeight * 1.4f)
                {
                    position = RandomMeteorStart(halfW);
                }

                node.tr.localPosition = position;
            }
        }
    }
}
