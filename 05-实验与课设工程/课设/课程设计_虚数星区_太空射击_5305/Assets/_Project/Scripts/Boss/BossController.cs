using System.Collections;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// Boss：三阶段战斗。
    ///
    /// 阶段切换只看血量比例（66% / 33%），切阶段时停掉当前攻击协程、起一个新的，
    /// 这样每种阶段的弹幕形态都是独立的一段代码，互不干扰，也方便单独调参。
    /// </summary>
    public class BossController : MonoBehaviour, IDamageable, IPoolable
    {
        public BossConfig Config { get; private set; }

        private float hp;
        private float maxHp;
        private int phase = 1;
        private bool alive;
        private bool entered;
        private float flashTimer;

        private Coroutine attackRoutine;
        private float ringAngle;

        public bool IsAlive { get { return alive; } }
        public int Phase { get { return phase; } }
        public float HpNormalized { get { return maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f; } }

        public void Setup(BossConfig config)
        {
            Config = config;
        }

        // ------------------------------------------------------------ 生命周期

        public void OnSpawn()
        {
            maxHp = Config.maxHp;
            hp = maxHp;
            phase = 1;
            alive = true;
            entered = false;
            flashTimer = 0f;
            ringAngle = 0f;

            transform.position = new Vector3(0f, GameArea.HalfHeight + 2.4f, 0f);
            transform.localScale = Vector3.one * 2.6f;

            // 血条不在这里升：Boss 是从屏幕外压进来的，入场要一秒多，
            // 这时候就把血条挂上去会出现「血条先到、Boss 姗姗来迟」的错位感。
            // 等它真正露出画面（entered = true）再升，和 BOSS 现身的瞬间对齐。
            EventBus.RaiseBossHp(1f);
        }

        public void OnDespawn()
        {
            StopAllCoroutines();
            attackRoutine = null;
            alive = false;
            EventBus.RaiseBossBar(false, null);
        }

        private void Update()
        {
            if (!alive) return;

            float dt = Time.deltaTime;

            if (!entered)
            {
                // 入场：从屏幕上方压下来。
                // 速度从 1.8 提到 3.0 —— 前面已经有 2 秒的 WARNING 铺垫，
                // 再慢慢悠悠飘两秒半就成了拖沓，入场要利落。
                Vector3 p = transform.position;
                p.y -= 3.0f * dt;
                transform.position = p;
                if (p.y <= GameArea.HalfHeight * 0.62f)
                {
                    entered = true;
                    // 血条在 Boss 露头的这一帧才升上来
                    EventBus.RaiseBossBar(true, Config != null ? Config.displayName : "BOSS");
                    StartPhase(1);
                }
                return;
            }

            MoveByPhase();
            UpdateScale(dt);
        }

        private void MoveByPhase()
        {
            Vector3 p = transform.position;

            if (phase == 1)
            {
                p.x = Mathf.Sin(Time.time * 0.85f) * GameArea.HalfWidth * 0.45f;
                p.y = Mathf.Lerp(p.y, GameArea.HalfHeight * 0.62f, Time.deltaTime * 2f);
            }
            else if (phase == 2)
            {
                p.x = Mathf.Lerp(p.x, 0f, Time.deltaTime * 2f);
                p.y = GameArea.HalfHeight * 0.62f + Mathf.Sin(Time.time * 1.1f) * 0.4f;
            }
            else
            {
                p.x = Mathf.Sin(Time.time * 1.7f) * GameArea.HalfWidth * 0.55f;
                p.y = Mathf.Lerp(p.y, GameArea.HalfHeight * 0.58f, Time.deltaTime * 2f);
            }

            transform.position = p;
        }

        private void UpdateScale(float dt)
        {
            float hit = flashTimer > 0f ? Mathf.Clamp01(flashTimer / 0.08f) : 0f;
            float scale = 2.6f * (1f + 0.06f * hit);
            transform.localScale = new Vector3(scale, scale, 1f);
            if (flashTimer > 0f) flashTimer -= dt;
        }

        // ------------------------------------------------------------ 伤害

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, true);
        }

        /// <summary>flash=false 专供连续光束：每 0.09 秒闪一次等于常亮</summary>
        public void TakeDamage(float amount, bool flash)
        {
            if (!alive) return;

            hp -= amount;
            if (flash) flashTimer = 0.08f;

            float normalized = HpNormalized;
            EventBus.RaiseBossHp(normalized);

            int nextPhase = 1;
            if (normalized <= Config.phase3Threshold) nextPhase = 3;
            else if (normalized <= Config.phase2Threshold) nextPhase = 2;

            if (nextPhase != phase)
            {
                phase = nextPhase;
                StartPhase(phase);
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.5f, 0.4f);
            }

            if (hp <= 0f) Die();
        }

        private void Die()
        {
            alive = false;
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = null;

            EventBus.RaiseBossHp(0f);

            if (FXManager.Instance != null)
            {
                FXManager.Instance.SpawnExplosion(transform.position, Config.tint, 3.2f);
                FXManager.Instance.SpawnExplosion(transform.position + Vector3.left * 1.2f, Config.tint, 2f);
                FXManager.Instance.SpawnExplosion(transform.position + Vector3.right * 1.2f, Config.tint, 2f);
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.9f, 0.9f);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterKill(Config.score);
                GameManager.Instance.BossDefeated();
            }

            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(PoolKeys.Boss, gameObject);
        }

        // ------------------------------------------------------------ 攻击

        private void StartPhase(int nextPhase)
        {
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(PhaseLoop(nextPhase));
        }

        private IEnumerator PhaseLoop(int currentPhase)
        {
            // 切阶段时给玩家半秒缓冲，不然弹幕突变会显得不讲道理
            yield return new WaitForSeconds(0.6f);

            int summonCounter = 0;

            while (alive && phase == currentPhase)
            {
                if (currentPhase == 1)
                {
                    FireFan(Config.fanCount, Config.fanAngle);
                    yield return new WaitForSeconds(1.5f);
                }
                else if (currentPhase == 2)
                {
                    FireAimed();
                    FireAimed();
                    summonCounter++;
                    if (summonCounter % 5 == 0) SummonAdds(2);
                    yield return new WaitForSeconds(1.15f);
                }
                else
                {
                    ringAngle += 27f;
                    FireRing(12, ringAngle);
                    yield return new WaitForSeconds(1.0f);
                }
            }
        }

        private void FireFan(int count, float angleStep)
        {
            float start = -angleStep * (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, start + angleStep * i) * Vector3.down;
                FireBullet(direction);
            }
        }

        private void FireAimed()
        {
            Vector2 toPlayer = Refs.PlayerPosition2D - (Vector2)transform.position;
            if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector2.down;
            FireBullet(toPlayer.normalized);
        }

        private void FireRing(int count, float angleOffset)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = angleOffset + 360f / count * i;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.down;
                FireBullet(direction);
            }
        }

        private void FireBullet(Vector2 direction)
        {
            if (PoolManager.Instance == null) return;

            Quaternion rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            GameObject go = PoolManager.Instance.Spawn(PoolKeys.EnemyBullet, transform.position, rot);
            if (go == null) return;

            BulletBase bullet = go.GetComponent<BulletBase>();
            if (bullet != null) bullet.Launch(direction.normalized, Config.bulletDamage, Config.bulletSpeed, 7f, false);
        }

        /// <summary>二阶段召唤两只冲锋怪。必须通知关卡管理器，否则波次清空判定会提前成立。</summary>
        private void SummonAdds(int count)
        {
            if (PoolManager.Instance == null) return;

            for (int i = 0; i < count; i++)
            {
                float offsetX = i % 2 == 0 ? -1.8f : 1.8f;
                Vector3 pos = transform.position + new Vector3(offsetX, -0.8f, 0f);
                PoolManager.Instance.Spawn(PoolKeys.EnemyRusher, pos, Quaternion.identity);

                if (LevelManager.Instance != null) LevelManager.Instance.RegisterSummonedEnemy();
            }
        }
    }
}
