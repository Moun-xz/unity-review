using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 粒子特效管理。
    ///
    /// 所有系统常驻，触发时只改位置和颜色再 Emit，不新建对象——
    /// 同屏几十次命中和爆炸也不会产生 GC。
    ///
    /// **命中特效按"形态"分类，不按"贴图"分类。**
    ///
    /// 走过三段弯路，前两段是同一个毛病——拿一张图去表达一次碰撞：
    ///   ① 软光团（GlowTexture 叠加）——亮星云上糊成一团白棉花，看不出打中了什么；
    ///   ② 硬边星芒 / 冲击环——形状越"设计"，越像有人把一张贴纸按在敌机身上。
    /// 第三段弯路更隐蔽：把四种敌人全部改成"掉碎块"。方向是对的（掉的是实体），
    /// 但**碎块是石头的语言**，四种敌人一起掉渣，屏幕上就只剩一种特效了。
    ///
    /// 所以现在按"飞出去的东西是什么形状、往哪儿走"来分：
    ///   突进者 薄壳打穿   → **线**：一捧细长火花，全周向快散，0.2 秒内消失
    ///   炮台   重甲挡住   → **被压下去的线**：火花少、粗、短，全部朝玩家方向溅
    ///   导弹   燃料舱起火 → **团**：橙黄火团膨胀上翻 + 黑烟 + 燃屑
    ///   陨石   石头崩裂   → **块**：实体碎块受重力往下抛 + 扬灰，**大小跟石头体量成正比**
    ///
    /// 线 / 压下去的线 / 团 / 块——四种形态，远看轮廓就不一样，不用靠颜色认。
    /// </summary>
    public class FXManager : MonoBehaviour
    {
        public static FXManager Instance { get; private set; }

        /// <summary>爆炸和枪口还在用的软光团。命中路径已经不用它了（见类注释）</summary>
        private ParticleSystem hitFlash;

        private ParticleSystem hitSpark;
        private ParticleSystem hitPuff;

        /// <summary>Boss 挨揍时的冲击环。只有它用：杂兵战每 0.09 秒一个新命中，圆环会连成一片</summary>
        private ParticleSystem hitRing;

        private ParticleSystem explosionPuff;
        private ParticleSystem explosionSpark;

        /// <summary>光束接触点的火星，方向和速度由调用方给，所以单独一套</summary>
        private ParticleSystem beamSpark;

        // ------------------------------------------------------------ 三种命中形态
        // 突进者和炮台都是火花，但参数完全分开写：一个"散开"，一个"被压下去"。
        // 合成一套再按参数区分，改的时候会互相牵扯（上一次就是这么糊成一片的）。
        private ParticleSystem sparkFan;      // 线：细长、快、不落地
        private ParticleSystem sparkHeavy;    // 压下去的线：短、粗、带重力
        private ParticleSystem flameBurst;    // 团：膨胀、上翻、会发光（它真的在烧）

        // 块：只有陨石用。加上粉尘，就是"石头崩了"的全部表达。
        private ParticleSystem rockChip;
        private ParticleSystem dust;

        /// <summary>薄壳燃屑。只有导弹用：块小、量少，是火的配角不是主角</summary>
        private ParticleSystem hullShard;

        /// <summary>导弹燃料的黑烟。负重力 = 被热气托着往上翻</summary>
        private ParticleSystem smoke;

        /// <summary>Boss 护盾碎片，自发光，和杂兵的实体碎片区分开</summary>
        private ParticleSystem metalShard;

        private ParticleSystem muzzleFlash;

        /// <summary>按贴图缓存材质：Additive 材质换了贴图就是另一种粒子，不能共用一份</summary>
        private static readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();

        /// <summary>Alpha 混合的材质另存一份：粉尘、烟不能用叠加，否则会自己发光</summary>
        private static readonly Dictionary<Texture, Material> softMaterials = new Dictionary<Texture, Material>();

        private void Awake()
        {
            Instance = this;

            // ------------------------------------------------------------ 命中三层
            hitFlash = NewSystem("FX_HitFlash", SpriteFactory.HitFlash().texture,
                ParticleSystemRenderMode.Billboard, 34,
                0.09f, 0.13f, 0f, 0f, 0.60f, 0.60f, 0.02f, 64);
            SizeCurve(hitFlash, 0.52f, 1.20f);
            AlphaFade(hitFlash, 0.12f);
            // 随机自转：连打时闪光朝向各不相同，看不出是同一张贴图。
            // main 是结构体属性，不能直接改成员，要先取出来再写回去
            ParticleSystem.MainModule flashMain = hitFlash.main;
            flashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 0.85f);

            hitSpark = NewSystem("FX_HitSpark", SpriteFactory.Spark().texture,
                ParticleSystemRenderMode.Stretch, 34,
                0.14f, 0.26f, 7f, 13f, 0.100f, 0.165f, 0.02f, 420);
            SizeCurve(hitSpark, 1f, 0.30f);
            AlphaFade(hitSpark, 0.18f);
            TuneStretch(hitSpark, 0.055f, 4.6f);

            hitPuff = NewSystem("FX_HitPuff", SpriteFactory.GlowTexture,
                ParticleSystemRenderMode.Billboard, 32,
                0.20f, 0.34f, 1.0f, 2.6f, 0.08f, 0.18f, 0.03f, 200);
            SizeCurve(hitPuff, 1f, 0.12f);
            AlphaFade(hitPuff, 0.10f);

            // ------------------------------------------------------------ 爆炸
            explosionPuff = NewSystem("FX_ExplosionPuff", SpriteFactory.GlowTexture,
                ParticleSystemRenderMode.Billboard, 28,
                0.40f, 0.95f, 1.2f, 4.6f, 0.12f, 0.40f, 0.16f, 400);
            SizeCurve(explosionPuff, 1f, 0.10f);
            AlphaFade(explosionPuff, 0.12f);

            explosionSpark = NewSystem("FX_ExplosionSpark", SpriteFactory.Spark().texture,
                ParticleSystemRenderMode.Stretch, 29,
                0.20f, 0.50f, 5f, 12f, 0.10f, 0.16f, 0.10f, 500);
            SizeCurve(explosionSpark, 1f, 0.20f);
            AlphaFade(explosionSpark, 0.55f);
            TuneStretch(explosionSpark, 0.050f, 4.8f);

            // ------------------------------------------------------------ 光束接触点
            // 单开一套而不是复用 hitSpark：hitSpark 走的是 shape 的球形随机方向，
            // 在 2D 里有一半速度被甩进了 Z 轴（看不见），而且方向不受控。
            // 光束的溅射必须"朝下、左右散开"，所以这套用 EmitParams 逐颗指定速度。
            beamSpark = NewSystem("FX_BeamSpark", SpriteFactory.Spark().texture,
                ParticleSystemRenderMode.Stretch, 34,
                0.14f, 0.28f, 0f, 0f, 0.05f, 0.09f, 0f, 260);
            SizeCurve(beamSpark, 1f, 0.22f);
            AlphaFade(beamSpark, 0.15f);
            TuneStretch(beamSpark, 0.045f, 4.2f);

            // ------------------------------------------------------------ 命中碎块
            // 全部 Alpha 混合。碎片是实体，用叠加模式会变成"自己发光的碎块"，
            // 暗背景上像萤火虫、亮星云上直接消失——两种都不像金属。

            // ------------------------------------------------ 形态① 线：突进者
            // 薄壳被打穿，出来的是"火星"——细长的线，速度高，0.2 秒内烧完消失。
            // 关键是它**不落地、不翻滚、不留残骸**：线是能量，块才是材料。
            sparkFan = NewSystem("FX_SparkFan", SpriteFactory.Spark().texture,
                ParticleSystemRenderMode.Stretch, 36,
                0.12f, 0.24f, 14f, 26f, 0.058f, 0.100f, 0f, 360);
            SizeCurve(sparkFan, 1f, 0.20f);
            AlphaFade(sparkFan, 0.05f);
            // 速度比 hitSpark 快一倍，velocityScale 跟着减半，线长才不被拉成面条
            TuneStretch(sparkFan, 0.030f, 4.0f);
            ParticleSystem.MainModule fanMain = sparkFan.main;
            fanMain.gravityModifier = 0.20f;

            // ------------------------------------------------ 形态② 被压下去的线：炮台
            // 重甲把火花压回玩家这一侧：数量少、断得干脆、带重力往下沉。
            // 和突进者的区别不是"块大块小"，是**方向**——那些火花根本飞不远。
            sparkHeavy = NewSystem("FX_SparkHeavy", SpriteFactory.Spark().texture,
                ParticleSystemRenderMode.Stretch, 36,
                0.09f, 0.15f, 6f, 11f, 0.115f, 0.180f, 0.03f, 240);
            SizeCurve(sparkHeavy, 1f, 0.32f);
            AlphaFade(sparkHeavy, 0.10f);
            TuneStretch(sparkHeavy, 0.042f, 1.7f);
            ParticleSystem.MainModule heavyMain = sparkHeavy.main;
            heavyMain.gravityModifier = 2.4f;

            // ------------------------------------------------ 形态③ 团：导弹
            // 燃料烧起来，这团火**该发光**——它就是在烧。但它会膨胀、会被热气托着上翻，
            // 和"往敌机身上贴一张亮贴纸"完全是两回事：贴纸不长大，也不往上走。
            flameBurst = NewSystem("FX_Flame", SpriteFactory.GlowTexture,
                ParticleSystemRenderMode.Billboard, 36,
                0.17f, 0.29f, 1.0f, 2.8f, 0.17f, 0.30f, 0.04f, 200);
            SizeCurve(flameBurst, 0.50f, 1.85f);
            AlphaFade(flameBurst, 0.02f);
            ParticleSystem.MainModule flameMain = flameBurst.main;
            flameMain.gravityModifier = -1.15f;

            // 燃屑：火里崩出来的小碎块。量很小（4 颗），只是给火一点实体感
            hullShard = NewSystem("FX_HullShard", SpriteFactory.Shard().texture,
                ParticleSystemRenderMode.Billboard, 35,
                0.26f, 0.44f, 4.0f, 8.0f, 0.085f, 0.150f, 0.03f, 200, false);
            SizeCurve(hullShard, 1f, 0.55f);
            AlphaFade(hullShard, 0.40f);
            Tumble(hullShard, 260f, 700f);
            ParticleSystem.MainModule hullMain = hullShard.main;
            hullMain.gravityModifier = 0.4f;

            // 冲击环：只有 Boss 用。杂兵每 0.1 秒挨一下，环会连成一整片圆圈，
            // 远看像给敌人套了个项圈——试过，比不加还难看。
            hitRing = NewSystem("FX_HitRing", SpriteFactory.HitRing().texture,
                ParticleSystemRenderMode.Billboard, 33,
                0.18f, 0.26f, 0f, 0f, 0.55f, 0.55f, 0f, 140);
            SizeCurve(hitRing, 0.35f, 2.20f);
            AlphaFade(hitRing, 0.02f);
            ParticleSystem.MainModule ringMain = hitRing.main;
            ringMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 1.57f);

            // Boss 护盾碎片：自发光，和杂兵的实体碎片刻意分开——打 Boss 打的是护盾，不是装甲
            metalShard = NewSystem("FX_MetalShard", SpriteFactory.Shard().texture,
                ParticleSystemRenderMode.Billboard, 35,
                0.28f, 0.52f, 3.6f, 8.5f, 0.130f, 0.220f, 0.05f, 300);
            SizeCurve(metalShard, 1f, 0.55f);
            AlphaFade(metalShard, 0.45f);
            Tumble(metalShard, 220f, 620f);
            ParticleSystem.MainModule shardMain = metalShard.main;
            shardMain.gravityModifier = 0.9f;

            // 石屑：暗、沉、掉。基础尺寸是"最小那颗石头"的碎渣，
            // 打大石头时由 SpawnHit 按体量把 sizeMultiplier 乘上去。
            rockChip = NewSystem("FX_RockChip", SpriteFactory.Shard().texture,
                ParticleSystemRenderMode.Billboard, 34,
                0.36f, 0.70f, 1.8f, 4.6f, 0.130f, 0.220f, 0.06f, 240, false);
            SizeCurve(rockChip, 1f, 0.70f);
            AlphaFade(rockChip, 0.55f);
            Tumble(rockChip, 140f, 420f);
            ParticleSystem.MainModule chipMain = rockChip.main;
            chipMain.gravityModifier = 2.4f;

            // 粉尘：一大团灰，慢慢摊开。打石头时它才是主角——没有火光，只有扬起来的灰。
            dust = NewSystem("FX_Dust", SpriteFactory.GlowTexture,
                ParticleSystemRenderMode.Billboard, 30,
                0.46f, 0.90f, 0.3f, 1.3f, 0.20f, 0.36f, 0.12f, 160, false);
            SizeCurve(dust, 0.85f, 1.95f);
            AlphaFade(dust, 0.10f);

            // 烟：导弹燃料那一下的黑烟。负重力 = 被热气托着往上翻，
            // 和石头的灰（往下压、散得慢）完全是两种走法。
            smoke = NewSystem("FX_Smoke", SpriteFactory.GlowTexture,
                ParticleSystemRenderMode.Billboard, 33,
                0.55f, 0.95f, 0.5f, 1.7f, 0.17f, 0.30f, 0.09f, 180, false);
            SizeCurve(smoke, 0.90f, 2.40f);
            AlphaFade(smoke, 0.06f);
            ParticleSystem.MainModule smokeMain = smoke.main;
            smokeMain.gravityModifier = -0.55f;

            // ------------------------------------------------------------ 枪口
            // 排序必须**低于飞船**（飞船 10），这是"飞船被染色"问题的根源：
            // 原来这里是 31，炮口焰画在机身之上，而两个炮口就长在翼根（±0.22 世界单位），
            // 双轨机炮每秒 18 发 × 2 门 = 每秒 36 次绿光合成的加法光直接糊在机身上，
            // 看起来就是"一开火飞船自己发绿光"。压到 9 之后火光只在机身轮廓外沿溢出来，
            // 该亮的是炮口，不是整架飞机。
            muzzleFlash = NewSystem("FX_Muzzle", SpriteFactory.HitFlash().texture,
                ParticleSystemRenderMode.Billboard, 9,
                0.05f, 0.07f, 0f, 0f, 0.26f, 0.26f, 0.01f, 32);
            SizeCurve(muzzleFlash, 0.70f, 1.25f);
            AlphaFade(muzzleFlash, 0.10f);
            ParticleSystem.MainModule muzzleMain = muzzleFlash.main;
            muzzleMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 1.57f);
        }

        // ------------------------------------------------------------ 构建

        /// <summary>
        /// 造一套"平时不喷、只在 Emit 时炸一下"的粒子系统。
        /// emission 模块直接关掉：只靠手动 Emit，不会有残留的持续喷射。
        /// </summary>
        private ParticleSystem NewSystem(string name, Texture texture, ParticleSystemRenderMode mode, int order,
                                         float lifeMin, float lifeMax,
                                         float speedMin, float speedMax,
                                         float sizeMin, float sizeMax,
                                         float radius, int maxParticles,
                                         bool additive = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // AddComponent 会让粒子系统立刻进入播放状态，此时改 duration 会刷警告
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.randomDirectionAmount = 1f;

            ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = GetMaterial(texture, additive);
            psRenderer.renderMode = mode;
            psRenderer.sortingOrder = order;

            ps.Stop();
            return ps;
        }

        /// <summary>尺寸随时间变化：from → to，中段先走完大半，看起来是"炸开后定住"</summary>
        private static void SizeCurve(ParticleSystem ps, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;

            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, from),
                new Keyframe(0.35f, Mathf.Lerp(from, to, 0.78f)),
                new Keyframe(1f, to));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>透明度：前 hold 段保持不透明，之后淡到 0</summary>
        private static void AlphaFade(ParticleSystem ps, float hold)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp01(hold)),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>
        /// StretchedBillboard 的两个参数。没有它们，火星就是一堆小圆点——
        /// 而"被弹飞的火星"必须是一道道细线。
        /// </summary>
        private static void TuneStretch(ParticleSystem ps, float velocityScale, float lengthScale)
        {
            ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            psRenderer.velocityScale = velocityScale;
            psRenderer.lengthScale = lengthScale;
        }

        /// <summary>
        /// 翻滚：碎屑必须自己转，否则放大之后就是一块贴纸在平移。
        /// 单位是弧度/秒（Unity 这个模块吃弧度，Inspector 里显示成度）。
        /// </summary>
        private static void Tumble(ParticleSystem ps, float minDegPerSec, float maxDegPerSec)
        {
            ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(minDegPerSec * Mathf.Deg2Rad,
                                                        maxDegPerSec * Mathf.Deg2Rad);
        }

        /// <summary>
        /// 材质。additive = false 走 Alpha 混合——粉尘、石屑、烟必须用它，
        /// 叠加模式会把灰色粉尘加亮，飘在暗背景上是一团发光的雾，比不画还假。
        /// </summary>
        private static Material GetMaterial(Texture texture, bool additive = true)
        {
            Dictionary<Texture, Material> cache = additive ? materials : softMaterials;

            Material cached;
            if (texture != null && cache.TryGetValue(texture, out cached) && cached != null)
                return cached;

            Shader shader = Shader.Find(additive
                ? "Legacy Shaders/Particles/Additive"
                : "Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            Material material = new Material(shader);
            material.mainTexture = texture != null ? texture : SpriteFactory.GlowTexture;
            if (texture != null) cache[texture] = material;
            return material;
        }

        // ------------------------------------------------------------ 对外接口

        /// <summary>
        /// 炸一下：雾团 + 火星 + 中心闪光。
        /// 闪光用 hitFlash 而不是另开一套：它寿命 0.1 秒，放大 2 倍正好是"起爆"那一下。
        /// </summary>
        public void SpawnExplosion(Vector3 position, Color color, float scale)
        {
            float s = Mathf.Max(0.3f, scale);
            float amount = Mathf.Clamp(s, 0.5f, 3f);

            Burst(explosionPuff, position, color, s, Mathf.RoundToInt(14f * amount));
            Burst(explosionSpark, position, Color.Lerp(color, Color.white, 0.55f), s,
                  Mathf.RoundToInt(30f * amount));
            Burst(hitFlash, position, Color.Lerp(color, Color.white, 0.70f), s * 2.2f, 1);
        }

        /// <summary>子弹命中：玩家和敌人的子弹都走这里，颜色由子弹自己带进来</summary>
        public void SpawnHit(Vector3 position, Color color, float scale = 1f)
        {
            float s = Mathf.Max(0.2f, scale);

            Burst(hitFlash, position, Color.Lerp(color, Color.white, 0.45f), s * 0.90f, 1);
            Burst(hitSpark, position, Color.Lerp(color, Color.white, 0.25f), s,
                  Mathf.Max(3, Mathf.RoundToInt(8f * s)));
            Burst(hitPuff, position, color, s * 0.85f, Mathf.Max(1, Mathf.RoundToInt(2f * s)));
        }

        /// <summary>
        /// 不同敌人被打中，飞出来的东西**形状**都不一样——这才是能一眼分开的东西。
        ///
        /// 判断依据不是颜色，也不是尺寸，是"这一下发生了什么物理"：
        ///   突进者 薄壳被打穿 → 火花向四面八方炸开（**线**，全向，0.2 秒烧完）
        ///   炮台   重甲硬挡住   → 火花被压回玩家这一侧（**线**，朝下，带重力往下沉）
        ///   导弹   燃料舱着火   → 火团膨胀上翻 + 黑烟 + 几粒燃屑（**团**）
        ///   陨石   石头崩裂     → 碎块被抛出去落地 + 扬灰，大小跟石头体量成正比（**块**）
        ///
        /// 上一版把前三种也做成了"掉碎块"，结果四种敌人全是石头渣——
        /// 碎块是石头的语言，别的材质不该说这句话。
        ///
        /// 这个重载**不吃武器色**：飞出来的是敌人自己的东西，颜色由材质定。
        /// 早先把武器色混进去，青色光束打石头会崩出青色的渣，反而露馅。
        /// </summary>
        public void SpawnHit(Vector3 position, float scale, EnemyKind kind, float bodySize = 1f)
        {
            float s = Mathf.Max(0.2f, scale);

            switch (kind)
            {
                case EnemyKind.Meteor:
                {
                    // 碎块大小必须和石头本身成正比。
                    // 大石头崩出瓜子大的渣、小石头崩出半个身子大的石板，都会立刻穿帮——
                    // 玩家对"这一下应该崩掉多大一块"的直觉非常准。
                    float t = Mathf.InverseLerp(Meteor.MinSize, Meteor.MaxSize, bodySize);
                    float chunk = Mathf.Lerp(0.78f, 2.15f, t);

                    Burst(rockChip, position, new Color(0.60f, 0.63f, 0.70f, 1f),
                          s * chunk, Mathf.Max(3, Mathf.RoundToInt(8f * s)));
                    Burst(dust, position, new Color(0.62f, 0.66f, 0.74f, 1f),
                          s * Mathf.Lerp(0.75f, 1.40f, t), Mathf.RoundToInt(Mathf.Lerp(1f, 4f, t)));
                    break;
                }

                case EnemyKind.Tracker:
                {
                    // 火团叠两层：外面橙、里面白，才是"有火心的火"而不是一个橙色圆盘。
                    // 向上的部分交给 gravityModifier = -1.15，不用手动算速度。
                    Burst(flameBurst, position, new Color(1f, 0.62f, 0.26f, 1f), s * 1.05f,
                          Mathf.Max(3, Mathf.RoundToInt(4f * s)));
                    Burst(flameBurst, position, new Color(1f, 0.90f, 0.62f, 1f), s * 0.62f, 3);

                    // 燃屑往上翻：被热气带着走，和石头的"往下掉"正好相反
                    Spray(hullShard, position, new Color(1f, 0.78f, 0.42f, 1f),
                          Mathf.Max(2, Mathf.RoundToInt(4f * s)), 90f, 55f, s * 1.10f);

                    Burst(smoke, position + Vector3.up * 0.06f, new Color(0.44f, 0.38f, 0.36f, 1f),
                          s * 1.05f, Mathf.Max(1, Mathf.RoundToInt(2f * s)));
                    break;
                }

                case EnemyKind.Turret:
                {
                    // 装甲：**一颗碎块都不掉**。掉块会立刻归进"石头"那一类。
                    // 重甲的反馈应该是"打不动"——火花被压回脚下，机体猛震一下。
                    Spray(sparkHeavy, position, new Color(1f, 0.94f, 0.84f, 1f),
                          Mathf.Max(6, Mathf.RoundToInt(10f * s)), -90f, 88f, s * 1.10f);
                    Spray(sparkHeavy, position, new Color(1f, 0.70f, 0.34f, 1f),
                          Mathf.Max(3, Mathf.RoundToInt(4f * s)), -90f, 112f, s * 0.78f);

                    // 撞击扬尘：薄薄一层，说明"有东西被震松了"，但没有火光
                    Burst(dust, position, new Color(0.72f, 0.70f, 0.66f, 1f), s * 0.62f, 1);
                    break;
                }

                default:
                {
                    // 薄壳：全周向炸开。用 Spray 手动指定 XY 平面内的方向——
                    // shape 的球面随机会把一半火星甩进 Z 轴，屏幕上看就是"火星很少"。
                    Spray(sparkFan, position, new Color(1f, 0.90f, 0.76f, 1f),
                          Mathf.Max(5, Mathf.RoundToInt(11f * s)), 0f, 180f, s * 1.05f);
                    Spray(sparkFan, position, new Color(1f, 0.60f, 0.30f, 1f),
                          Mathf.Max(2, Mathf.RoundToInt(5f * s)), 0f, 180f, s * 0.72f);
                    break;
                }
            }
        }

        /// <summary>
        /// Boss 挨揍：护盾被打出涟漪的感觉——面积大、颜色偏紫，和杂兵的橙红分得开。
        /// Boss 是全屏最大的目标，用杂兵那点碎屑打上去像蚊子叮。
        /// </summary>
        public void SpawnBossHit(Vector3 position, Color color, float scale)
        {
            float s = Mathf.Max(0.4f, scale);
            Color shield = new Color(0.86f, 0.46f, 1f, 1f);

            Burst(hitRing, position, new Color(shield.r, shield.g, shield.b, 0.80f), s * 1.5f, 1);
            Burst(hitFlash, position, Color.Lerp(color, Color.white, 0.55f), s * 2.0f, 1);
            Burst(hitSpark, position, shield, s * 1.6f, Mathf.RoundToInt(10f * s));
            Burst(metalShard, position, Color.Lerp(shield, Color.white, 0.45f), s * 1.5f,
                  Mathf.RoundToInt(4f * s));
        }

        /// <summary>
        /// 连射子弹命中（双轨机炮，每秒十几发）。
        ///
        /// 为什么不直接用 SpawnHit：那是"整发子弹炸开"的量，一发一秒十几回，
        /// 屏幕上会糊成一团圆片，敌人整个被自己的碎屑盖住。
        /// 这里只比光束的一拍重一档：材质碎屑照旧按敌人种类给（语言不能乱），
        /// 额外加一颗很小的白闪当作"这颗弹打实了"。
        /// </summary>
        public void SpawnBulletHit(Vector3 position, float scale, EnemyKind kind, float bodySize = 1f)
        {
            float s = Mathf.Max(0.2f, scale);

            SpawnBeamTick(position, kind, s * 1.15f, bodySize);
            Burst(hitFlash, position, new Color(1f, 0.96f, 0.86f, 1f), s * 0.42f, 1);
        }

        /// <summary>
        /// Boss 被连续火力啃。和 SpawnBossHit 的区别是**不放环**：
        /// 一秒十几发的环叠在一起，等于给 Boss 套了个不停转的项圈。
        /// </summary>
        public void SpawnBossTick(Vector3 position, float scale, Color color)
        {
            float s = Mathf.Max(0.2f, scale);
            Color shield = new Color(0.86f, 0.46f, 1f, 1f);

            Burst(hitFlash, position, Color.Lerp(color, shield, 0.55f), s * 0.55f, 1);
            Burst(hitSpark, position, shield, s * 1.10f, 2);
        }

        /// <summary>
        /// 每种敌人的"材质色"。光束的溅射火星、接触光斑都拿它上色，
        /// 这样一道青色光束烧在石头上，崩下来的是灰的、不是青的。
        /// </summary>
        public static Color HitAccent(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Meteor: return new Color(0.66f, 0.70f, 0.78f, 1f);
                case EnemyKind.Tracker: return new Color(1f, 0.76f, 0.34f, 1f);
                case EnemyKind.Turret: return new Color(1f, 0.80f, 0.46f, 1f);
                default: return new Color(1f, 0.52f, 0.26f, 1f);
            }
        }

        /// <summary>
        /// 光束烧在敌人身上的每一拍（0.09 秒一次）。
        ///
        /// 光束是持续接触而不是"打一发"，所以这里只负责"又烧掉一点"的节拍；
        /// 主体是玩家侧的灼烧光斑（PlayerBeam.hitGlow）和敌人自己的发亮抖动。
        ///
        ///   突进者 每拍溅一两根线，朝下和两侧（光束从下往上，火星被挤到旁边）
        ///   炮台   二十几拍才蹦一根——装甲扛烧，这也是一种反馈
        ///   导弹   火一直在舔，隔几拍冒一口烟，说明它在被点着
        ///   陨石   掉灰 + 掉渣，大小跟体量走
        /// </summary>
        public void SpawnBeamTick(Vector3 position, EnemyKind kind, float scale = 1f, float bodySize = 1f)
        {
            float s = Mathf.Max(0.2f, scale);

            switch (kind)
            {
                case EnemyKind.Meteor:
                {
                    // 大石头掉大渣：这里也要跟体量走，否则大石头被烧的时候崩的还是小瓜子。
                    float t = Mathf.InverseLerp(Meteor.MinSize, Meteor.MaxSize, bodySize);
                    Burst(dust, position, new Color(0.62f, 0.66f, 0.74f, 1f),
                          s * Mathf.Lerp(0.35f, 0.62f, t), 1);
                    if (Random.value < 0.30f)
                        Burst(rockChip, position, new Color(0.56f, 0.59f, 0.66f, 1f),
                              s * Mathf.Lerp(0.35f, 1.10f, t), 1);
                    break;
                }

                case EnemyKind.Tracker:
                    // 火一直在舔，烟隔几拍冒一次
                    if (Random.value < 0.70f)
                        Burst(flameBurst, position, new Color(1f, 0.66f, 0.30f, 1f), s * 0.42f, 1);
                    if (Random.value < 0.22f)
                        Burst(smoke, position, new Color(0.44f, 0.38f, 0.36f, 1f), s * 0.55f, 1);
                    break;

                case EnemyKind.Turret:
                    // 扛烧：偶尔才被崩出一根，而且是往下沉的粗的
                    if (Random.value < 0.28f)
                        Spray(sparkHeavy, position, new Color(1f, 0.90f, 0.78f, 1f),
                              1, -90f, 60f, s * 0.62f);
                    break;

                default:
                    // 薄壳：每拍一两根细线，往两侧飞
                    Spray(sparkFan, position, new Color(1f, 0.86f, 0.70f, 1f),
                          Random.value < 0.5f ? 2 : 1, -90f, 72f, s * 0.60f);
                    break;
            }
        }

        /// <summary>
        /// 光束"打实了"的那一下：白闪 + 一圈朝外溅的火花。
        ///
        /// 和 SpawnBeamTick 分工明确：那个只负责"烧的是什么材质"（灰渣 / 火苗 / 硬屑），
        /// 每 0.09 秒按敌人种类出不同东西，单颗、很小；真正让玩家看见"我打中了"的是这里——
        /// 一颗提白的四芒星，尺寸是 tick 碎屑的两三倍，亮度和光束核心同档。
        ///
        /// 之前整个命中表现只有 tick 那几颗按材质出的碎屑：打突进者时一半概率只出 1 根细线，
        /// 在 1080p 上不到 20 像素，连续光柱压过去几乎看不见，读起来像"光停在敌人身上"。
        /// </summary>
        public void SpawnBeamImpact(Vector3 position, Color color, float scale = 1f)
        {
            float s = Mathf.Max(0.3f, scale);

            Burst(hitFlash, position, Color.Lerp(color, Color.white, 0.80f), s * 1.15f, 1);
            Burst(hitSpark, position, Color.Lerp(color, Color.white, 0.35f), s * 0.85f,
                  Mathf.Max(2, Mathf.RoundToInt(3f * s)));
        }

        /// <summary>
        /// 光束接触点的一颗火星。
        ///
        /// 用 EmitParams 直接指定速度，而不是交给 shape 模块：
        /// shape 的随机方向是球面随机，在 2D 里有一半速度指向屏幕里（看不见），
        /// 火星因此又慢又散。指定 XY 平面内的速度之后，每一颗都朝着指定方向拉成一道线。
        /// </summary>
        public void SpawnBeamSpark(Vector3 position, Color color, Vector2 direction, float speed, float scale = 1f)
        {
            if (beamSpark == null) return;

            beamSpark.transform.position = position;
            beamSpark.Play();

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            emit.position = new Vector3(position.x + Random.Range(-0.05f, 0.05f),
                                        position.y + Random.Range(-0.04f, 0.02f),
                                        0f);
            emit.velocity = direction * speed;
            emit.startLifetime = Random.Range(0.14f, 0.28f);
            emit.startSize = Random.Range(0.06f, 0.10f) * Mathf.Max(0.2f, scale);
            emit.startColor = Color.Lerp(color, Color.white, 0.55f);

            beamSpark.Emit(emit, 1);
        }

        /// <summary>
        /// 炮口焰。只有玩家在用（敌人开火走的是各自的 SpawnHit / 弹道）。
        ///
        /// 颜色往白里提得多（0.72）而不是直接用武器色：连续武器一秒三十六次叠加，
        /// 全按武器色加会在地面拖出一条饱和的色带；提白之后读作"灼热"，绿色只留一点点暗示。
        /// 原来这里还叠了两颗 hitSpark（order 34，压在飞船之上），
        /// 等于在机身上撒绿色火星，一并去掉——炮口焰本身已经够了。
        /// </summary>
        public void SpawnMuzzle(Vector3 position, Color color)
        {
            Burst(muzzleFlash, position, Color.Lerp(color, Color.white, 0.72f), 0.90f, 1);
        }

        public void SpawnMuzzle(Vector3 position)
        {
            SpawnMuzzle(position, new Color(0.70f, 0.95f, 1f, 1f));
        }

        /// <summary>
        /// 手动撒一把带方向的粒子。
        ///
        /// 为什么不用 shape 模块：Circle shape 的发射方向是球面随机，在 2D 里有一半速度
        /// 被甩进 Z 轴（看不见），结果就是"火星又慢又少"，而且方向完全不受控——
        /// 炮台的火花要朝玩家压回来，靠 shape 根本做不到。
        ///
        /// centerDeg 是主方向（-90 = 正下方，90 = 正上方），spreadDeg 是左右半角
        /// （180 就是全周向）。寿命和速度区间从 main 里读，参数仍然集中在 Awake 定义。
        /// </summary>
        private void Spray(ParticleSystem ps, Vector3 position, Color color, int count,
                           float centerDeg, float spreadDeg, float sizeScale)
        {
            if (ps == null || count <= 0) return;

            ParticleSystem.MainModule main = ps.main;
            float lifeMin = main.startLifetime.constantMin;
            float lifeMax = main.startLifetime.constantMax;
            float speedMin = main.startSpeed.constantMin;
            float speedMax = main.startSpeed.constantMax;

            float k = Mathf.Max(0.05f, sizeScale);
            float sizeMin = main.startSize.constantMin * k;
            float sizeMax = main.startSize.constantMax * k;

            ps.transform.position = position;
            ps.Play();

            for (int i = 0; i < count; i++)
            {
                float deg = centerDeg + Random.Range(-spreadDeg, spreadDeg);
                float rad = deg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
                emit.position = position + (Vector3)(dir * Random.Range(0f, 0.05f));
                emit.velocity = dir * Random.Range(speedMin, speedMax);
                emit.startLifetime = Random.Range(lifeMin, lifeMax);
                emit.startSize = Random.Range(sizeMin, sizeMax);
                emit.startColor = color;
                ps.Emit(emit, 1);
            }
        }

        /// <summary>
        /// 放一簇粒子。用 startSizeMultiplier 而不是直接改 startSize：
        /// 每套系统都配了 min~max 的随机尺寸区间，写死成标量会把这点随机性抹掉。
        /// </summary>
        private void Burst(ParticleSystem ps, Vector3 position, Color color, float sizeScale, int count)
        {
            if (ps == null || count <= 0) return;

            ps.transform.position = position;
            ParticleSystem.MainModule main = ps.main;
            main.startSizeMultiplier = Mathf.Max(0.05f, sizeScale);
            main.startColor = color;

            ps.Play();
            ps.Emit(count);
        }
    }
}
