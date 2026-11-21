using UnityEngine;

namespace PixelGravity
{
    /// <summary>普通激光弹：高频、单发、命中火花较小。作为 BulletBase 的最简派生类存在。</summary>
    public class LaserBullet : BulletBase
    {
        protected override float HitScale { get { return 0.85f; } }

        protected override void OnHitEffect(Collider2D other)
        {
            base.OnHitEffect(other);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayHit();
        }
    }
}
