using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 机炮子弹：两个炮口各射出一条平行的弹链，单发伤害低、射速快。
    ///
    /// 命中反馈刻意做得比激光的"每一拍"还轻：武器一秒十几发，
    /// 每一发都按"整颗子弹炸开"来处理，屏幕上只剩一团金雾，
    /// 反而看不出打中了什么。节拍交给射速本身，碎屑交给敌人材质。
    /// </summary>
    public class SpreadBullet : BulletBase
    {
        protected override float HitScale { get { return 0.85f; } }

        protected override void OnHitEffect(Collider2D other)
        {
            if (FXManager.Instance == null || other == null) return;

            Vector2 point = other.ClosestPoint(transform.position);

            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null && enemy.Config != null)
            {
                FXManager.Instance.SpawnBulletHit(point, HitScale, enemy.Config.kind, enemy.BodySize);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayHit();
                return;
            }

            if (other.GetComponent<BossController>() != null)
            {
                FXManager.Instance.SpawnBossTick(point, HitScale * 1.6f, hitColor);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayHit();
                return;
            }

            // 撞到环境（墙、边界之类）：没有材质可讲，只给一小片火花
            FXManager.Instance.SpawnHit(point, hitColor, HitScale * 0.6f);
        }
    }
}
