using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 敌人行为③：追踪导弹。
    ///
    /// 和"会追人的敌机"的区别不在参数，在**过程**：
    ///   锁定（减速 + 快速对准 + 锁定圈收束）→ 点火（加速到末速）→ 撞击自爆。
    /// 玩家因此能读出两个节点：「它盯上我了」和「它要冲了」，
    /// 而不是一个圆盘一直匀速跟着你、贴上来就炸。
    ///
    /// 三段的速度曲线是刻意反着来的：先刹车再加速。
    /// 一上来就全速追，玩家只会觉得"有个东西黏着我"；
    /// 先顿一下再扑上来，那一下才有"被锁定"的威胁感。
    ///
    /// turnSpeed 仍然是 110°/s（有限转向），这是它能被绕开的前提：
    /// 一直横向拉扯，它转向跟不上，会在你身后画一个大弧。
    /// 转向无限的话这枚导弹就是必杀，游戏当场变成"等死"。
    ///
    /// 两个子物体（尾焰 / 锁定圈）只在第一次出生时创建——
    /// 对象池会反复复用同一个实例，每次 OnSpawned 都 new 的话，复用几次就挂一串。
    /// </summary>
    public class Tracker : EnemyBase
    {
        [Tooltip("进入该距离后引爆。弹尖在局部 -0.725，1.05 相当于还差 0.32 就贴到；"
               + "末速 6.08 时每帧走 0.10，那点缝会被爆炸闪光盖掉，读起来就是撞上了")]
        public float explodeRadius = 1.05f;

        [Tooltip("爆炸的实际伤害半径，比引爆距离略大")]
        public float blastRadius = 1.6f;

        private const float LockDuration = 0.50f;   // 锁定：边减速边对准
        private const float BurnDuration = 1.10f;   // 点火：从慢速加速到末速
        private const float BoostFactor = 1.9f;     // 末速 = 起飞速度 × 这个
        private const float LockTurnSpeed = 240f;   // 锁定期间几乎瞬间对准，否则会歪头锁定

        // 弹尾在局部 +Y（前进方向是 -Y）。这个数不能凭贴图高度除二：
        // enemy_seeker.png 是 320 高（世界 1.60），但轮廓只画到第 35 行，
        // 实测弹尾在 +0.625、弹尖在 -0.725。按 0.80 摆尾焰，火焰会悬在弹尾外 27px，
        // 看着像一根飘着的火线，而不是从喷管里喷出来的。
        private const float TailY = 0.625f;

        private enum Phase { Locking, Burning }

        private Phase phase;
        private float phaseTimer;
        private float speed;
        private float flicker;
        private float ringSpin;

        private SpriteRenderer flame;
        private SpriteRenderer lockRing;

        protected override void OnSpawned()
        {
            phase = Phase.Locking;
            phaseTimer = 0f;
            speed = Config != null ? Config.moveSpeed : 3f;
            flicker = Random.Range(0f, 6.283f);

            // 普通敌人从屏幕外（HalfHeight + 0.8）生成，导弹不行：
            // 按 3.2 的速度算，锁定那 0.5 秒它还在屏幕外——玩家只看到一枚已经冲下来的东西，
            // "减速对准"这个动作等于白做。所以把出生点压到屏幕上沿以内。
            // 0.85 = 弹体半长 0.80 + 一点余量：整枚弹连同尾焰都要在画面里，不然"进入视线"变成"凭空出现"。
            if (GameArea.HalfHeight > 0f)
            {
                Vector3 p = transform.position;
                float cap = GameArea.HalfHeight - 0.85f;
                if (p.y > cap) p.y = cap;
                transform.position = p;
            }

            // 出生就对准玩家。不这么做的话它会带着 0° 朝向往下飘、再慢慢转头，
            // 看着像"刚睡醒"，不像已经锁好目标的导弹。
            AimInstant();

            if (flame == null) BuildParts();
            SetFlame(0.30f);
            SetRing(0f);
        }

        private void BuildParts()
        {
            GameObject flameGo = new GameObject("Flame");
            flameGo.transform.SetParent(transform, false);
            flame = flameGo.AddComponent<SpriteRenderer>();
            flame.sprite = SpriteFactory.Flame();
            // 压在本体（8）之下：喷口那一段被弹体挡住，才像从喷管里出来
            flame.sortingOrder = 7;
            flame.color = new Color(1f, 0.94f, 0.82f, 0.7f);

            GameObject ringGo = new GameObject("LockRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            lockRing = ringGo.AddComponent<SpriteRenderer>();
            lockRing.sprite = SpriteFactory.LockRing();
            lockRing.sortingOrder = 12;   // 压在弹体上，不然会被机身盖掉一半
            lockRing.color = new Color(1f, 0.32f, 0.24f, 0f);
        }

        protected override void TickActive()
        {
            float dt = Time.deltaTime;
            phaseTimer += dt;

            if (phase == Phase.Locking)
            {
                // 踩刹车：从起飞速度降到 28%。这"一顿"就是锁定那一下
                float k = Mathf.Clamp01(phaseTimer / LockDuration);
                speed = Mathf.Lerp(Config.moveSpeed, Config.moveSpeed * 0.28f, k);
                Steer(LockTurnSpeed, dt);
                SetRing(k);
                SetFlame(0.34f);

                if (phaseTimer >= LockDuration)
                {
                    phase = Phase.Burning;
                    phaseTimer = 0f;
                }
            }
            else
            {
                float k = Mathf.Clamp01(phaseTimer / BurnDuration);
                speed = Mathf.Lerp(Config.moveSpeed * 0.28f, Config.moveSpeed * BoostFactor, k);
                Steer(Config.turnSpeed, dt);
                SetRing(1f);      // 已锁定，圈收掉
                SetFlame(1f);
            }

            Vector3 forward = -transform.up;
            transform.position += forward * speed * dt;

            // 判的是中心到玩家的距离，-explodeRadius 里已经算进了弹尖的 0.725
            Vector2 toPlayer = Refs.PlayerPosition2D - (Vector2)transform.position;
            if (toPlayer.magnitude <= explodeRadius) Explode();
            else if (GameArea.IsOutside(transform.position, 1.5f)) DespawnSelf();
        }

        private void Steer(float turnSpeed, float dt)
        {
            Vector2 toPlayer = Refs.PlayerPosition2D - (Vector2)transform.position;
            float targetAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, turnSpeed * dt);
            transform.rotation = Quaternion.Euler(0f, 0f, next);
        }

        private void AimInstant()
        {
            Vector2 toPlayer = Refs.PlayerPosition2D - (Vector2)transform.position;
            float targetAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
        }

        /// <summary>
        /// 锁定圈：直径从 1.35 收到 0.62，前 18% 亮起来，之后回落到三成亮，k=1 时收掉。
        ///
        /// 尺寸是按"框住弹体中段"定的：弹体是 0.56 宽 × 1.60 长。
        /// 上一版收到 0.28——比弹体还窄，收到最后一步整个圈已经缩进弹体内部了，
        /// 看上去是"环消失了"而不是"锁定完成了"。改成 0.62：正好卡在弹体两侧，
        /// 收束的最后一下读作"框到位了"，然后才隐去。
        ///
        /// 转起来是必要的：只缩放不转，玩家看到的是一次"变小"；加上旋转，
        /// 四段弧的缺口在移动，才读成一台正在扫描、正在收敛的瞄准器。
        /// </summary>
        private void SetRing(float k)
        {
            if (lockRing == null) return;

            if (k >= 1f)
            {
                if (lockRing.enabled) lockRing.enabled = false;
                return;
            }

            lockRing.enabled = true;
            float s = Mathf.Lerp(1.35f, 0.62f, k);
            lockRing.transform.localScale = new Vector3(s, s, 1f);

            ringSpin += Time.deltaTime * 120f;
            lockRing.transform.localRotation = Quaternion.Euler(0f, 0f, ringSpin);

            float a = Mathf.Clamp01(k / 0.18f) * Mathf.Lerp(0.92f, 0.32f, k);
            lockRing.color = new Color(1f, 0.40f, 0.30f, a);
        }

        /// <summary>
        /// 尾焰。长度随强度从 0.5 拉到 1.0，并且每帧抖一下——
        /// 火焰的"在烧"是靠缩放抖出来的，画在贴图上的条纹不会动，只会像贴纸。
        /// 尾巴的 y 要跟着长度一起走，否则火焰短的时候会缩进弹体里。
        ///
        /// 宽度 0.62~0.92：Flame 贴图世界宽 0.4，乘上去 0.25~0.37（24~35px），
        /// 和 27px 粗的弹体核心差不多宽——火焰比弹体还宽会变成一根火柱。
        /// 之前给 0.36~0.54 时，叠加贴图里平方衰减的横截面，实见只有 10px，像根火线。
        /// </summary>
        private void SetFlame(float intensity)
        {
            if (flame == null) return;

            flicker += Time.deltaTime * 27f;
            float pulse = 0.86f + Mathf.Sin(flicker) * 0.14f;

            float len = Mathf.Lerp(0.50f, 1.00f, intensity) * pulse;
            float wid = Mathf.Lerp(0.62f, 0.92f, intensity) * pulse;

            flame.transform.localScale = new Vector3(wid, len, 1f);
            flame.transform.localPosition = new Vector3(0f, TailY + len * 0.45f, 0.05f);
            flame.color = new Color(1f, 0.94f, 0.82f, Mathf.Lerp(0.5f, 1f, intensity));
        }

        private void Explode()
        {
            if (alive)
            {
                alive = false;

                if (Refs.Player != null)
                {
                    float distance = Vector2.Distance(Refs.PlayerPosition2D, transform.position);
                    if (distance <= blastRadius)
                    {
                        PlayerHealth playerHealth = Refs.Player.GetComponent<PlayerHealth>();
                        if (playerHealth != null) playerHealth.TakeDamage(Config.contactDamage);
                    }
                }

                if (FXManager.Instance != null) FXManager.Instance.SpawnExplosion(transform.position, Config.tint, 1.6f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.32f, 0.22f);

                EventBus.RaiseEnemyKilled(this);
                if (GameManager.Instance != null) GameManager.Instance.RegisterKill(Config.score);
            }

            DespawnSelf();
        }
    }
}
