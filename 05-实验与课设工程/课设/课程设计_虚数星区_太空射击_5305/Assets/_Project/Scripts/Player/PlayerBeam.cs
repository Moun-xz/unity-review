using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 玩家主武器的连续光束。
    ///
    /// 之前激光是"每 0.12 秒从两个炮口各吐一发子弹"，在屏幕上是一串离散的小亮点，
    /// 看着像断断续续的虚线。现在改成**一根从机头一直射到屏幕上沿的光束**，
    /// 按住不放就一直在，松开才断——这才像"激光"。
    ///
    /// 三层贴图叠出来的：
    ///   glow  外晕，宽 0.5 单位，低亮度，负责"光洒在周围"
    ///   core  核心，宽 0.10 单位，接近纯白，是看得见的那根线
    ///   tip   光束顶端的收口，在屏幕外（多射出去 0.8），只负责别让画面边缘看到"断头"
    ///
    /// 伤害不是子弹，是每 0.09 秒做一次竖直矩形 OverlapBox。
    /// 用定长 tick 而不是逐帧结算，是因为 EnemyBase.TakeDamage 会触发受击闪白，
    /// 逐帧调等于让敌人一直白着；0.09 秒一结算，闪白读起来是"被烧得一闪一闪"。
    ///
    /// 命中反馈分四层，缺一层都会显得"光停在敌人身上，但敌人没反应"：
    ///   接触光斑  常驻，每帧跟着敌人的外壳走（不是每 tick 跳一次）
    ///   火星      按随机间隔一颗颗往下溅，不是每 tick 齐射一把
    ///   微闪      tick 上放一颗最小的四芒星，作为"这一下打实了"的节拍
    ///   敌人侧    机体朝白提一档 + 抖一下（EnemyBase.AddBurnFeedback）
    /// </summary>
    public class PlayerBeam : MonoBehaviour
    {
        /// <summary>伤害结算间隔。和 HitFlashDuration 同量级，闪白才不会糊成常亮</summary>
        private const float DamageTick = 0.09f;

        /// <summary>光束比屏幕顶再多冲出去一点，免得屏幕边缘看到"断头"</summary>
        private const float Overshoot = 0.8f;

        private SpriteRenderer glow;
        private SpriteRenderer core;
        private SpriteRenderer tip;
        private SpriteRenderer hitGlow;

        /// <summary>最近一次结算锁定的敌体。接触光斑每帧拿它重算落点，不然会跟着 tick 一跳一跳</summary>
        private Collider2D contact;

        /// <summary>最近一次结算里最靠近玩家的接触点，用来放灼烧光斑</summary>
        private Vector3 contactPoint;
        private bool hasContact;

        private float length = 12f;
        private float dps = 52f;
        private float tickTimer;
        private float sparkTimer;
        private float pulse;
        private float noiseSeed;
        private bool firing;
        private Color tint = new Color(0.40f, 0.95f, 1f, 1f);

        /// <summary>"这一下打实了"的冲击值，每个伤害 tick 置 1，随后快速回落，用来给接触光斑做脉冲</summary>
        private float impactPulse;

        /// <summary>命中音效的节流计时。0.09 秒一拍全播会糊成一片白噪，见 ApplyDamage 里的说明</summary>
        private float hitSoundTimer;

        /// <summary>
        /// 接触点用的颜色：武器色和目标材质色的混合。
        /// 光束本体永远是武器的青色，但接触点崩出来的碎屑应该跟着材质走——
        /// 烧石头是灰的、烧导弹是橙的。由 ApplyDamage 每拍重算。
        /// </summary>
        private Color contactColor = new Color(1f, 1f, 1f, 1f);

        public bool Firing { get { return firing; } }

        // ------------------------------------------------------------ 构建

        /// <summary>挂在玩家飞船下面，跟着机身走。只在局内存在一份</summary>
        public static PlayerBeam Create(Transform parent)
        {
            GameObject go = new GameObject("PlayerBeam");
            go.transform.SetParent(parent, false);
            // 从机头出，不是从机身中心：从中心射出去的话上半截会被自己的贴图盖住
            go.transform.localPosition = new Vector3(0f, 0.18f, 0f);

            PlayerBeam beam = go.AddComponent<PlayerBeam>();
            beam.Build();
            return beam;
        }

        private void Build()
        {
            // 排序分两组，这是"像不像激光"的关键：
            //
            // 光束本体（glow/core/tip）压在敌人（8）**之下**。之前三层都在 10~12，
            // 一根青色竖条会直接盖在红色敌机身上穿过去，看起来像给敌机贴了条荧光胶带；
            // 压到敌机之下以后，光束在机体轮廓处自然断掉，穿透的敌机后面再露出来，
            // 读起来才是"从我这儿射出去、被它挡住了"。
            //
            // 命中光斑反而要压在敌人**之上**（13），它得贴在敌机的壳上，
            // 被机体挡住就等于没画。
            glow = MakeLayer("Glow", SpriteFactory.BeamGlow(), 4, new Color(1f, 1f, 1f, 0.30f));
            core = MakeLayer("Core", SpriteFactory.BeamCore(), 5, new Color(1f, 1f, 1f, 0.95f));
            tip = MakeLayer("Tip", SpriteFactory.BeamTip(), 5, new Color(1f, 1f, 1f, 0.55f));

            // 打在敌人身上的灼烧光斑。贴在接触点而不是屏幕顶：
            // 光束是穿透的，只在上沿冒火星的话，打在身上完全没有反馈。
            hitGlow = MakeLayer("HitGlow", SpriteFactory.BeamTip(), 13, new Color(1f, 1f, 1f, 0f));
            hitGlow.enabled = false;

            noiseSeed = Random.Range(0f, 100f);
            SetVisible(false);
        }

        private SpriteRenderer MakeLayer(string name, Sprite sprite, int order, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------ 外部接口

        public void Configure(float damagePerSecond, Color color)
        {
            dps = Mathf.Max(1f, damagePerSecond);
            tint = color;
        }

        public void SetFiring(bool on)
        {
            if (firing == on) return;

            firing = on;
            SetVisible(on);

            if (on)
            {
                // 每次起手都从"最亮"开始，再回落：按住不放时这一下就是"点火"
                pulse = 1.35f;
                tickTimer = 0f;
                sparkTimer = 0f;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayLaser();
            }
        }

        /// <summary>重开一局时强制收掉，避免上一局的光束留在屏幕上</summary>
        public void ResetForGame()
        {
            firing = false;
            hasContact = false;
            contact = null;
            impactPulse = 0f;
            hitSoundTimer = 0f;
            SetVisible(false);
        }

        private void SetVisible(bool on)
        {
            if (glow != null) glow.enabled = on;
            if (core != null) core.enabled = on;
            if (tip != null) tip.enabled = on;

            // 灼烧光斑由 UpdateContactGlow 自己控开关：被收掉时先清成不可见
            if (!on && hitGlow != null)
            {
                hitGlow.enabled = false;
                hitGlow.color = new Color(1f, 1f, 1f, 0f);
            }

            if (!on) impactPulse = 0f;
        }

        // ------------------------------------------------------------ 每帧

        /// <summary>
        /// 用 LateUpdate：玩家在 Update 里移动，光束要在移动之后才摆位置，
        /// 否则会有一帧的错位，快速左右平移时能看出来光束在"拖影"。
        /// </summary>
        private void LateUpdate()
        {
            if (!firing) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                ResetForGame();
                return;
            }

            pulse = Mathf.MoveTowards(pulse, 1f, Time.deltaTime * 6f);

            if (hitSoundTimer > 0f) hitSoundTimer -= Time.deltaTime;
            if (impactPulse > 0f) impactPulse = Mathf.MoveTowards(impactPulse, 0f, Time.deltaTime * 7f);

            // 长度跟着屏幕走：换分辨率、切全屏都不用改常数
            float top = GameArea.HalfHeight + Overshoot;
            length = Mathf.Max(1f, top - transform.position.y);

            Layout();
            ApplyDamage();
            UpdateContactGlow();
            UpdateSparks(Time.deltaTime);
        }

        private void Layout()
        {
            float flicker = 1f + Mathf.Sin((Time.time + noiseSeed) * 47f) * 0.03f;

            // 核心：宽 0.10 单位。BeamCore 是 24×256，ppu 取 256，
            // 所以 scale=1 时世界宽 24/256=0.094，这里乘 1.06 正好到 0.10
            float coreW = 1.06f * pulse * flicker;
            core.transform.localScale = new Vector3(coreW, length, 1f);
            core.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
            core.color = new Color(tint.r + (1f - tint.r) * 0.72f,
                                   tint.g + (1f - tint.g) * 0.72f,
                                   tint.b + (1f - tint.b) * 0.72f,
                                   0.95f);

            // 外晕：64/256=0.25 宽，乘 2.0 ≈ 0.50 单位。
            // 原来是 0.85，比核心宽八倍多，在屏幕上是"一条宽宽的半透明柱子"，
            // 边缘还是直的——看起来像贴了块玻璃板，而不是光在扩散。
            float glowW = 2.0f * pulse * flicker;
            glow.transform.localScale = new Vector3(glowW, length, 1f);
            glow.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
            glow.color = new Color(tint.r, tint.g, tint.b, 0.30f);

            // 收口贴在光束顶端（屏幕外），只为盖住"断头"，不参与命中
            float tipSize = 0.34f + 0.06f * Mathf.Sin(Time.time * 19f);
            tip.transform.localScale = Vector3.one * tipSize * pulse;
            tip.transform.localPosition = new Vector3(0f, length, 0f);
            tip.color = new Color(tint.r, tint.g, tint.b, 0.55f);
        }

        private void ApplyDamage()
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer > 0f) return;
            tickTimer = DamageTick;

            Vector2 center = new Vector2(transform.position.x, transform.position.y + length * 0.5f);
            Vector2 size = new Vector2(0.52f, length);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);

            float amount = dps * DamageTick;
            bool anyHit = false;
            float lowestY = float.MaxValue;
            Collider2D nearest = null;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                // 玩家自己也在光柱里（机头那一段），必须先排掉
                if (hit.GetComponent<PlayerHealth>() != null) continue;

                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                BossController boss = null;
                if (enemy == null) boss = hit.GetComponent<BossController>();

                bool alive = enemy != null ? enemy.IsAlive : (boss != null && boss.IsAlive);
                if (!alive) continue;

                Vector2 point = ImpactPoint(hit);

                // 材质和体量都必须在这一下结算之前取走：它可能被这一下打死，
                // 回池后 OnSpawn 会把 sizeMultiplier 重置回 1，
                // 之后读到的永远是"小石头"，大石头崩出来的块就和小的一个样了。
                EnemyKind kind = (enemy != null && enemy.Config != null) ? enemy.Config.kind : EnemyKind.Rusher;
                float bodySize = enemy != null ? enemy.BodySize : 1f;
                contactColor = Color.Lerp(tint, FXManager.HitAccent(kind), 0.62f);

                // 先给反馈再结算：这一下可能把它打死，打死之后对象已经回池了
                if (enemy != null) enemy.AddBurnFeedback(0.55f, 0.055f);

                if (enemy != null) enemy.TakeDamage(amount, false);
                else boss.TakeDamage(amount, false);

                anyHit = true;
                if (point.y < lowestY)
                {
                    lowestY = point.y;
                    contactPoint = point;
                    nearest = hit;
                }

                // 烧的是什么，崩出来的就是什么颜色：石头是灰的、导弹是橙的、装甲是白的。
                // 以前不管烧谁都是同一道青色火星，等于没做区分。
                if (FXManager.Instance != null)
                    FXManager.Instance.SpawnBeamTick(point, kind, enemy != null ? 1f : 1.35f, bodySize);
            }

            // 这一拍没打到东西就丢掉接触体，光斑交给 UpdateContactGlow 淡出
            hasContact = anyHit;
            contact = nearest;

            if (!anyHit) return;

            // 「打实了」的那一下：白闪 + 冲击脉冲。
            // 放在循环外只放一次——同时烧到两个敌人时不放两团白闪，否则屏幕中央糊成一片。
            impactPulse = 1f;
            if (FXManager.Instance != null)
                FXManager.Instance.SpawnBeamImpact(contactPoint, contactColor);

            // 命中音效。光束每秒结算 11 拍（0.09 秒一次），每拍都播的话 11 个音头挨在一起，
            // 听感是一片没有起伏的沙沙声，等于没做反馈。节流到 0.18~0.26 秒一次之后，
            // 每一下能听出独立的"叮"，连着烧又不会断，读起来是"一直在烧"而不是"在敲鼓"。
            if (hitSoundTimer <= 0f && AudioManager.Instance != null)
            {
                // 1.35 倍：命中音是这套音效里最轻的一个（0.4 底），光束又是持续声，
                // 不抬一点会被射击音和后坐力的低频压住，听起来像"没响"。
                AudioManager.Instance.PlayHit(1.35f);
                hitSoundTimer = Random.Range(0.18f, 0.26f);
            }
        }

        /// <summary>
        /// 光束打在敌人身上的"入口点"。
        ///
        /// 从机体下方 0.25 处朝它探，ClosestPoint 会给出朝向玩家的那一面轮廓——
        /// 那才是光束真正该亮起来的位置。之前拿包围盒中心当探针，敌人是实心的，
        /// 点落在体内，ClosestPoint 原样返回中心点，光斑就贴在敌人肚子中间，
        /// 看起来像给它贴了张发光贴纸。
        /// </summary>
        private Vector2 ImpactPoint(Collider2D hit)
        {
            Bounds b = hit.bounds;
            Vector2 probe = new Vector2(transform.position.x, b.min.y - 0.25f);
            return hit.ClosestPoint(probe);
        }

        /// <summary>
        /// 接触点的灼烧光斑。
        ///
        /// 两个关键点：
        /// 1. **每帧跟着敌人重算**。以前只在伤害 tick 里算一次位置，敌人每秒走五个单位，
        ///    光斑就是"每 0.09 秒瞬移半米"，播放起来像贴纸在跳，完全不像光打在动的东西上。
        /// 2. **小而硬**。以前 0.66 单位、还是平滑正弦呼吸，比光束核心宽六倍多，
        ///    看起来是一团会鼓起来的模糊光球。现在 0.30 单位、高频抖动，
        ///    是"焊点"而不是"气球"。
        /// </summary>
        private void UpdateContactGlow()
        {
            if (hitGlow == null) return;

            if (contact != null && contact.gameObject.activeInHierarchy)
                contactPoint = ImpactPoint(contact);
            else
                hasContact = false;

            if (!hasContact)
            {
                if (!hitGlow.enabled) return;

                Color fading = hitGlow.color;
                fading.a = Mathf.MoveTowards(fading.a, 0f, Time.deltaTime * 9f);
                hitGlow.color = fading;
                if (fading.a <= 0.01f) hitGlow.enabled = false;
                return;
            }

            hitGlow.enabled = true;

            // 位置留一点点高频游移：纯粹钉死在接触点上，看起来像镶了一颗珠子
            float jx = Mathf.Sin(Time.time * 71f + noiseSeed) * 0.012f;
            float jy = Mathf.Sin(Time.time * 53f + noiseSeed) * 0.010f;
            hitGlow.transform.position = contactPoint + new Vector3(jx, jy, 0f);

            // 61 / 97 两个互质的频率叠加，抖动不会两下就重复；
            // 幅度小、频率高才是"电流在跳"，大而慢就变成了"呼吸"
            float flick = 0.82f
                        + 0.10f * Mathf.Sin(Time.time * 61f + noiseSeed)
                        + 0.06f * Mathf.Sin(Time.time * 97f);

            // 尺寸吃两样东西：高频抖动 flick（电流在跳），和每拍的冲击脉冲 impactPulse
            // （打实那一下鼓起来）。只留抖动的话，光斑始终是一个"恒定的焊点"，
            // 打没打中在视觉上没有节拍；只留脉冲又会变成呼吸球。两者叠起来才像在"烧"。
            float s = 0.46f * flick * (1f + impactPulse * 0.55f);
            // 横向拉长、纵向压扁：射线打上来，溅开的口子是横向的
            hitGlow.transform.localScale = new Vector3(s * 1.30f, s * 0.86f, 1f);

            // 打实那一拍往白里提，回落过程退回敌人材质色——脉冲结束正好完成"白 → 材质色"的过渡
            float whiten = 0.42f + impactPulse * 0.38f;
            hitGlow.color = new Color(contactColor.r + (1f - contactColor.r) * whiten,
                                      contactColor.g + (1f - contactColor.g) * whiten,
                                      contactColor.b + (1f - contactColor.b) * whiten,
                                      (0.80f + impactPulse * 0.18f) * flick);
        }

        /// <summary>
        /// 接触点的火星：按随机间隔一颗一颗往下溅。
        ///
        /// 只在伤害 tick 上放的话，火星是"每 0.09 秒齐射一把"，节拍感强得像机器；
        /// 改成不定时掉一颗，才是"一直在烧"。方向朝下（迎着光束来的方向），
        /// 左右各散 70 度——被烧化的东西是往外、往回溅的。
        /// </summary>
        private void UpdateSparks(float dt)
        {
            if (!hasContact || FXManager.Instance == null) return;

            sparkTimer -= dt;
            if (sparkTimer > 0f) return;
            sparkTimer = Random.Range(0.035f, 0.085f);

            float angle = Random.Range(-70f, 70f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));

            FXManager.Instance.SpawnBeamSpark(contactPoint, contactColor, dir,
                Random.Range(2.4f, 5.2f), Random.value < 0.25f ? 0.7f : 1f);
        }
    }
}
