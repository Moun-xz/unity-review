using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 子弹基类。三种子弹（激光、机炮、敌方弹）共用同一套飞行与命中逻辑，
    /// 差异只在命中特效上，由子类重写 OnHitEffect。
    ///
    /// 为什么用 Rigidbody2D 的 velocity 而不是 transform.Translate：
    /// 物理引擎按固定步长移动刚体，才能保证触发器检测不会因为帧率波动而漏判（高速穿透）。
    /// </summary>
    public class BulletBase : MonoBehaviour, IPoolable
    {
        [Header("由池管理器在创建时写入")]
        public string PoolKey;

        protected float damage = 10f;
        protected float speed = 20f;
        protected float life = 2.5f;
        protected bool friendly = true;
        protected Vector2 direction = Vector2.up;

        /// <summary>
        /// 命中特效的主色。由武器配置写进来（由 ObjectFactory 在建子弹时调 SetHitColor），
        /// 不能用 sr.color：子弹贴图本身就是带颜色的 PNG，sr.color 只会是白色或闪烁色。
        /// </summary>
        protected Color hitColor = Color.white;

        private float boundsGrace;

        protected Rigidbody2D rb;
        protected SpriteRenderer sr;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
        }

        /// <summary>发射：方向、伤害、速度、存活时间、是否友军</summary>
        public virtual void Launch(Vector2 dir, float dmg, float spd, float lifeTime, bool isFriendly)
        {
            direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            damage = dmg;
            speed = spd;
            life = lifeTime;
            friendly = isFriendly;

            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            if (rb != null) rb.velocity = direction * speed;
        }

        protected virtual void Update()
        {
            life -= Time.deltaTime;

            // 刚生成的子弹可能还在屏幕外（例如 Boss 从上方开火），
            // 给它 0.3 秒的宽限期再开始做越界回收，否则会一出膛就被回收。
            if (boundsGrace > 0f)
            {
                boundsGrace -= Time.deltaTime;
            }
            else if (GameArea.IsOutside(transform.position, 1.6f))
            {
                Despawn();
                return;
            }

            if (life <= 0f) Despawn();
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || !gameObject.activeSelf) return;

            IDamageable target = other.GetComponent<IDamageable>();
            if (target == null || !target.IsAlive) return;

            // 阵营判定：友军子弹不打玩家，敌方子弹只打玩家
            bool otherIsPlayer = other.GetComponent<PlayerHealth>() != null;
            if (friendly && otherIsPlayer) return;
            if (!friendly && !otherIsPlayer) return;

            target.TakeDamage(damage);
            OnHitEffect(other);
            Despawn();
        }

        /// <summary>
        /// 命中特效。把撞到的东西整个传进来，而不是只传一个点——
        /// 因为特效要按"打中的是什么材质"分（石头扬灰、装甲掉片、导弹着火），
        /// 只有 Collider 才拿得到 EnemyBase。
        /// </summary>
        protected virtual void OnHitEffect(Collider2D other)
        {
            if (FXManager.Instance == null || other == null) return;

            Vector2 point = other.ClosestPoint(transform.position);

            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null && enemy.Config != null)
            {
                FXManager.Instance.SpawnHit(point, HitScale, enemy.Config.kind, enemy.BodySize);
                return;
            }

            if (other.GetComponent<BossController>() != null)
            {
                FXManager.Instance.SpawnBossHit(point, hitColor, HitScale);
                return;
            }

            FXManager.Instance.SpawnHit(point, hitColor, HitScale);
        }

        /// <summary>命中特效的体量，子类按手感重写（散射比激光重、敌方弹比友军轻）</summary>
        protected virtual float HitScale { get { return 1f; } }

        /// <summary>由对象工厂在造子弹时写入武器色，命中火花才会跟着武器走</summary>
        public void SetHitColor(Color color)
        {
            hitColor = color;
        }

        protected void Despawn()
        {
            if (PoolManager.Instance == null) return;
            PoolManager.Instance.Despawn(PoolKey, gameObject);
        }

        public virtual void OnSpawn()
        {
            life = 2.5f;
            friendly = true;
            direction = Vector2.up;
            boundsGrace = 0.3f;
            if (rb != null) rb.velocity = Vector2.zero;
            if (sr != null) sr.color = Color.white;
        }

        public virtual void OnDespawn()
        {
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }
}
