using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 敌人行为①：直线冲锋。
    /// 出生瞬间锁定玩家当时的位置方向，之后不再转向——所以玩家只要横向位移就能躲开，
    /// 这也是给玩家的第一个"可以学会"的威胁。
    /// </summary>
    public class StraightRusher : EnemyBase
    {
        private Vector2 moveDirection = Vector2.down;

        protected override void OnSpawned()
        {
            Vector2 toPlayer = Refs.PlayerPosition2D - (Vector2)transform.position;
            moveDirection = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.down;

            // 精灵默认朝下，因此让"局部 -Y 轴"对齐移动方向
            float angle = Mathf.Atan2(moveDirection.x, -moveDirection.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        protected override void TickActive()
        {
            transform.position += (Vector3)(moveDirection * Config.moveSpeed * Time.deltaTime);

            // 飞出屏幕就回收，避免看不见的对象继续占用池
            if (GameArea.IsOutside(transform.position, 1.2f)) DespawnSelf();
        }
    }
}
