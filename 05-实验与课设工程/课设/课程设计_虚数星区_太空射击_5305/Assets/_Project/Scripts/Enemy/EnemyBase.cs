using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 敌人基类：状态机骨架 + 血量 + 死亡结算。
    ///
    /// 三种敌人共用同一套 FSM（Spawning → Active → Attacking → Dying），
    /// 差异只体现在两个虚方法上：TickActive（怎么移动）和 TickAttack（怎么攻击）。
    /// 这样加第四种敌人只需要新增一个派生类，不用改基类。
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour, IDamageable, IPoolable
    {
        public EnemyConfig Config { get; private set; }

        protected EnemyState state = EnemyState.Spawning;
        protected float hp;
        protected bool alive;
        protected float stateTimer;
        protected float flashTimer;

        protected SpriteRenderer sr;
        private Sprite sourceSprite;   // 池子里拿到的原始贴图（重绘成双色调之前）
        protected Rigidbody2D rb;
        protected string poolKey;

        /// <summary>
        /// 个体尺寸系数。默认 1，只有陨石这类「同一配置要出大小不一」的敌人会改它：
        /// 位置、碰撞体半径都跟着 localScale 一起放大，所以血量/伤害也按它去缩，
        /// 否则大陨石和小陨石一个血量，玩家会觉得大的那块"怎么这么脆"。
        /// </summary>
        protected float sizeMultiplier = 1f;

        /// <summary>
        /// 对外只读的体量系数。命中特效要用它：同样一颗陨石，打碎的块头
        /// 必须和石头本身成正比，大石头崩出小碎渣、小石头崩出大石板都不可信。
        /// </summary>
        public float BodySize { get { return sizeMultiplier; } }

        /// <summary>个体的基准染色。灼烧变亮之后要还原到它，不能一律写回 HostileTint（陨石是白的）</summary>
        private Color baseTint = Color.white;

        /// <summary>灼烧亮度，命中时顶到 1，之后按固定速率衰减</summary>
        private float burnLevel;
        private float burnGlow;

        /// <summary>当前这一帧叠在位置上的抖动位移</summary>
        private Vector3 shakeOffset;
        private float shakeTimer;
        private float shakeAmp;

        private const float SpawnDuration = 0.25f;
        private const float HitFlashDuration = 0.08f;

        /// <summary>受击抖动时长</summary>
        private const float ShakeDuration = 0.12f;

        public float ContactDamage { get { return Config != null ? Config.contactDamage : 10f; } }
        public bool IsAlive { get { return alive; } }
        public EnemyState State { get { return state; } }

        protected virtual void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            // 记下池子里的原始贴图：敌人贴图会被 EnemyArt 重绘成双色调，
            // 池复用时要回到原图再重绘一次，否则会在已染色的图上反复叠加。
            if (sr != null) sourceSprite = sr.sprite;
            rb = GetComponent<Rigidbody2D>();
        }

        public void Setup(EnemyConfig config)
        {
            Config = config;
            poolKey = PoolKeys.EnemyOf(config.kind);
        }

        // ------------------------------------------------------------ 生命周期

        // 敌对阵营的染色。玩家是冷白 + 青，敌人用赤热金属色，两边一眼分得开。
        //
        // 为什么走 sr.color 乘法而不是直接改 png：
        // 1. 美术资源保持 Kenney 原图不动，想回退只改这一行；
        // 2. 乘法只压 G/B 通道、保住 R 通道，原图的明暗结构（面板、护罩轮廓）全部保留，
        //    比双色调重映射干净得多——试过重映射，三架敌机全变成一块"三文鱼色"贴纸。
        // 另外别用柔光晕做阵营标识：星云本身就是品红/紫的，红色柔光铺上去等于没加。
        //
        // 公开出来是因为敌人图鉴也得用同一份颜色：图鉴以前直接摆原图，
        // 橙色机身、白面板，而游戏里是实心赤红——玩家一眼就看出"这不是同一个东西"。
        public static readonly Color HostileTint = new Color(1f, 0.42f, 0.34f, 1f);

        // 灼烧时朝这个色推：>1 的分量让贴图过曝，读起来像"被打得发白"
        private static readonly Color BurnHot = new Color(2.1f, 1.9f, 1.75f, 1f);

        /// <summary>
        /// 每种敌人自己的染色。阵营色（赤红）留给最凶的突进者，
        /// 炮台走琥珀、追踪导弹走**石墨银灰**、陨石只把冷白压一点点——四种敌人同屏时
        /// 玩家靠"颜色 + 剪影"两路信息就能分清，不用先看清模型。
        ///
        /// 这里只剩图鉴在用（`EnemyCodexPanel.TintOf`）：局内敌机的颜色由 `EnemyArt` 的双色调
        /// 直接画进贴图，OnSpawn 时 `sr.color` 是白的，不再乘色。所以改配色要**两边一起改**，
        /// 否则图鉴里是一个颜色、打起来是另一个——追踪导弹的紫色就是这么留下来的。
        /// </summary>
        public static Color TintFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Turret: return new Color(1f, 0.70f, 0.34f, 1f);      // 琥珀：哨戒火力
                case EnemyKind.Tracker: return new Color(0.72f, 0.76f, 0.84f, 1f);  // 石墨银灰：锁定追踪
                case EnemyKind.Meteor: return new Color(0.93f, 0.96f, 1f, 1f);      // 冷白：环境障碍
                default: return new Color(1f, 0.44f, 0.40f, 1f);                    // 珊瑚红：冲撞
            }
        }

        public virtual void OnSpawn()
        {
            alive = true;
            hp = Config != null ? Config.maxHp : 10f;
            state = EnemyState.Spawning;
            stateTimer = 0f;
            flashTimer = 0f;

            // 池复用必须把个体尺寸清回 1，否则上一颗大陨石的尺寸会传给下一颗
            sizeMultiplier = 1f;

            // 上一轮留下的抖动位移要先撤掉，否则新敌人在出生点旁边偏半个身位
            transform.position -= shakeOffset;
            shakeOffset = Vector3.zero;
            shakeTimer = 0f;
            shakeAmp = 0f;
            burnLevel = 0f;
            burnGlow = 0f;

            transform.localScale = Vector3.one * (Config != null ? Config.bodyScale : 1f) * 0.25f;
            // 陨石是环境障碍、不是敌对阵营，染色会和岩石本身的材质打架，所以不染
            if (sr != null)
            if (sr != null)
            {
                EnemyKind kind = Config != null ? Config.kind : EnemyKind.Rusher;
                Sprite tinted = EnemyArt.Get(kind, sourceSprite != null ? sourceSprite : sr.sprite);
                if (tinted != null)
                    sr.sprite = tinted;
                sr.color = Color.white;   // 颜色已经画进贴图，这里不再乘色
            }
            baseTint = sr != null ? sr.color : Color.white;
            if (rb != null) rb.velocity = Vector2.zero;

            OnSpawned();
        }

        public virtual void OnDespawn()
        {
            StopAllCoroutines();
            if (rb != null) rb.velocity = Vector2.zero;

            // 回池前把抖动位移抹掉，不然池里的实例位置会越来越偏
            transform.position -= shakeOffset;
            shakeOffset = Vector3.zero;
            shakeTimer = 0f;
            if (sr != null) sr.color = baseTint;

            // 飞出屏幕被回收、以及重开一局时被强制清理，都不会走 Die()。
            // 这里补发一次「敌人离场」通知，否则关卡管理器的存活计数会永远清不零，卡在无限等待波次结束。
            if (alive)
            {
                alive = false;
                EventBus.RaiseEnemyKilled(this);
            }
        }

        /// <summary>派生类在这里做出生初始化（例如锁定方向、设置悬停高度）</summary>
        protected virtual void OnSpawned() { }

        protected virtual void Update()
        {
            if (!alive) return;

            float dt = Time.deltaTime;

            switch (state)
            {
                case EnemyState.Spawning:
                    stateTimer += dt;
                    if (stateTimer >= SpawnDuration) ChangeState(EnemyState.Active);
                    break;

                case EnemyState.Active:
                    TickActive();
                    break;

                case EnemyState.Attacking:
                    TickAttack();
                    break;
            }

            UpdateScale(dt);
        }

        /// <summary>出生时从小到大弹出 + 受击时短暂放大，两个效果叠加在同一个 scale 上</summary>
        private void UpdateScale(float dt)
        {
            float spawnT = state == EnemyState.Spawning ? Mathf.Clamp01(stateTimer / SpawnDuration) : 1f;
            float hitT = flashTimer > 0f ? Mathf.Clamp01(flashTimer / HitFlashDuration) : 0f;
            float baseScale = (Config != null ? Config.bodyScale : 1f) * sizeMultiplier;
            float scale = baseScale * Mathf.Lerp(0.25f, 1f, spawnT) * (1f + 0.18f * hitT);
            transform.localScale = new Vector3(scale, scale, 1f);

            if (flashTimer > 0f) flashTimer -= dt;

            ApplyBurn(dt);
            ApplyShake(dt);
        }

        /// <summary>
        /// 被光束持续灼烧时，机体染色朝白提一档，光束移开后再慢慢烧尽。
        ///
        /// 为什么不用现成的受击闪白（flashTimer）：那个是"打一发闪一下"，
        /// 光束每 0.09 秒结算一次，逐次闪白会连成常亮，反而看不出在掉血。
        ///
        /// 也不做成"提亮—回落"的循环：11Hz 的明暗循环会变成频闪，
        /// 看着像灯管坏了。这里只在命中时把亮度顶到 1，之后按固定速率衰减，
        /// 连续命中时亮度在 0.78~1.0 之间轻轻起伏，读起来才是"一直烧着"。
        /// </summary>
        private void ApplyBurn(float dt)
        {
            if (sr == null) return;

            if (burnLevel <= 0f)
            {
                if (sr.color != baseTint) sr.color = baseTint;
                return;
            }

            burnLevel = Mathf.MoveTowards(burnLevel, 0f, dt * 2.4f);   // 冷却约 0.4 秒
            // 贴图本身已经有颜色，受击灼烧改成"朝超白推"（颜色分量可以 >1，SpriteRenderer 会过曝）
            sr.color = Color.Lerp(baseTint, BurnHot, burnLevel * burnGlow * 0.42f);
        }

        /// <summary>
        /// 受击抖动。每帧先把上一帧叠上去的位移撤掉，再加新的。
        ///
        /// 必须这么做：三种敌人都是在 TickActive 里直接改 transform.position，
        /// 如果只是"加一点偏移"而不撤销，这点偏移会跟着速度一路累积，
        /// 被打两秒就能被顶出半个屏幕。
        ///
        /// 横向抖得比纵向多一点——光束是从下往上打，机体上的着力点在上下，
        /// 纵向抖看起来像在"蹦"，横向抖才像是"被烧得发抖"。
        /// </summary>
        private void ApplyShake(float dt)
        {
            transform.position -= shakeOffset;
            shakeOffset = Vector3.zero;

            if (shakeTimer <= 0f) return;

            shakeTimer -= dt;
            float t = Mathf.Clamp01(shakeTimer / ShakeDuration);
            float amp = shakeAmp * t;

            shakeOffset = new Vector3(Random.Range(-amp, amp), Random.Range(-amp, amp) * 0.45f, 0f);
            transform.position += shakeOffset;
        }

        protected void ChangeState(EnemyState next)
        {
            state = next;
            stateTimer = 0f;
            OnStateEnter(next);
        }

        protected virtual void OnStateEnter(EnemyState next) { }

        protected virtual void TickActive() { }
        protected virtual void TickAttack() { }

        // ------------------------------------------------------------ 伤害与死亡

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, true);
        }

        /// <summary>
        /// flash=false 的用法只有一个：连续光束每 0.09 秒结算一次伤害。
        /// 那种频率下再触发受击闪白，敌人会一直白着，看不出血条在掉；
        /// 光束自己会画命中光斑，视觉反馈由它负责。
        /// </summary>
        public void TakeDamage(float amount, bool flash)
        {
            if (!alive) return;

            hp -= amount;
            if (flash) flashTimer = HitFlashDuration;

            if (hp <= 0f) Die();
        }

        /// <summary>
        /// 被光束顶着烧的反馈：机体朝白提一档 + 抖一下。
        ///
        /// 单独开一个方法而不是塞进 TakeDamage(flash=true)，是因为两者节奏不一样：
        /// 子弹的闪白是"一下"，光束是每 0.09 秒来一次、要连成一片，参数得单独给。
        ///
        /// 注意调用时机：必须在 TakeDamage 之前调。伤害可能把它打死，
        /// 那之后对象已经回池，再动它的 transform 会把池里的实例位置搞歪。
        /// </summary>
        /// <param name="glow">朝白提亮的强度，0~1</param>
        /// <param name="shake">抖动幅度，世界单位（0.05 左右刚好能看出来，0.1 就晃得晕）</param>
        public void AddBurnFeedback(float glow, float shake)
        {
            if (!alive) return;

            burnLevel = 1f;
            burnGlow = Mathf.Max(burnGlow, Mathf.Clamp01(glow));

            // 抖幅按材质加权：同一个数字给四种敌人，手感就变成了"打谁都一样"。
            //   炮台 被整块顶住，机体在震，抖得最狠
            //   突进者 一下一下被打穿，轻快
            //   导弹 轻，被推着走
            //   陨石 体量大，抖一点点就够，抖多了像果冻
            float weight = 1f;
            if (Config != null)
            {
                if (Config.kind == EnemyKind.Turret) weight = 1.60f;
                else if (Config.kind == EnemyKind.Tracker) weight = 0.80f;
                else if (Config.kind == EnemyKind.Meteor) weight = 0.50f;
            }

            shakeTimer = ShakeDuration;
            shakeAmp = Mathf.Max(shakeAmp, shake * weight);
        }

        protected virtual void Die()
        {
            alive = false;
            ChangeState(EnemyState.Dying);

            if (FXManager.Instance != null)
                FXManager.Instance.SpawnExplosion(transform.position, Config != null ? Config.tint : Color.white, 1f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayExplosion();

            EventBus.RaiseEnemyKilled(this);
            if (GameManager.Instance != null && Config != null)
                GameManager.Instance.RegisterKill(Config.score);

            DespawnSelf();
        }

        protected void DespawnSelf()
        {
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(poolKey, gameObject);
        }

        /// <summary>供关卡管理器在重开一局时强制清场</summary>
        public void DespawnImmediately()
        {
            DespawnSelf();
        }

        /// <summary>派生类发子弹的统一入口</summary>
        protected void FireBullet(Vector2 direction, float damage, float speed, float lifeTime)
        {
            if (PoolManager.Instance == null) return;

            Quaternion rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            GameObject go = PoolManager.Instance.Spawn(PoolKeys.EnemyBullet, transform.position, rot);
            if (go == null) return;

            BulletBase bullet = go.GetComponent<BulletBase>();
            if (bullet != null) bullet.Launch(direction.normalized, damage, speed, lifeTime, false);
        }
    }
}
