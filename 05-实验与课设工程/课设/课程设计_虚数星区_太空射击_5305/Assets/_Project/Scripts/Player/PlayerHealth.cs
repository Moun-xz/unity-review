using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 玩家生命值。
    /// 关键设计：受击后进入 1.5 秒无敌并闪烁，否则撞到敌机的同一帧里会被连续触发多次，
    /// 血条瞬间见底——这是 2D 弹幕游戏里最常见的手感事故。
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public int maxHp = 100;
        public float invulnerableTime = 1.5f;

        private int hp;
        private float invulnTimer;
        private bool alive = true;
        private SpriteRenderer sr;

        public bool IsAlive { get { return alive; } }
        public int Hp { get { return hp; } }
        public int MaxHp { get { return maxHp; } }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            hp = maxHp;
        }

        /// <summary>换船时更新血量上限并回满。由 PlayerRig 在开局前调用</summary>
        public void SetMaxHp(int value)
        {
            maxHp = Mathf.Max(1, value);
            hp = maxHp;
            alive = true;
            invulnTimer = 0f;
            if (sr != null) sr.color = Color.white;
            EventBus.RaisePlayerHp(hp, maxHp);
        }

        /// <summary>开新局时由 GameManager 调用，把血量与无敌状态重置</summary>
        public void ResetHealth()
        {
            hp = maxHp;
            alive = true;
            invulnTimer = 0f;
            if (sr != null) sr.color = Color.white;
            EventBus.RaisePlayerHp(hp, maxHp);
        }

        public void TakeDamage(float amount)
        {
            if (!alive || invulnTimer > 0f) return;

            hp -= Mathf.RoundToInt(amount);
            if (hp < 0) hp = 0;
            invulnTimer = invulnerableTime;

            EventBus.RaisePlayerHp(hp, maxHp);

            if (FXManager.Instance != null) FXManager.Instance.SpawnHit(transform.position, new Color(1f, 0.45f, 0.45f));
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.35f, 0.25f);
            if (HitStop.Instance != null) HitStop.Instance.Freeze(0.05f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayHurt();

            if (hp <= 0) Die();
        }

        private void Die()
        {
            alive = false;
            if (FXManager.Instance != null) FXManager.Instance.SpawnExplosion(transform.position, new Color(0.5f, 0.9f, 1f), 1.8f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.7f, 0.6f);

            gameObject.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.PlayerDied();
        }

        private void Update()
        {
            if (invulnTimer > 0f)
            {
                invulnTimer -= Time.deltaTime;
                // 闪烁：用正弦在完全可见与半透明之间来回，提示"现在处于无敌"
                float alpha = Mathf.Sin(Time.time * 34f) > 0f ? 1f : 0.25f;
                if (sr != null) sr.color = new Color(1f, 1f, 1f, alpha);
            }
            else if (sr != null && sr.color.a < 1f)
            {
                sr.color = Color.white;
            }
        }

        /// <summary>与敌机碰撞：由 PLAYER 这一侧接收，因为玩家是动态刚体，触发器回调可靠</summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                TakeDamage(enemy.ContactDamage);
                return;
            }

            // 撞到 Boss 本体同样要掉血，否则玩家可以贴上去无伤输出
            BossController boss = other.GetComponent<BossController>();
            if (boss != null) TakeDamage(25f);
        }
    }
}
