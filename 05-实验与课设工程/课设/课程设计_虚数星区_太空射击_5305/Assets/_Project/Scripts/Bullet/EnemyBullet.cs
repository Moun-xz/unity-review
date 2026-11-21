using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 敌方子弹。与玩家子弹共用飞行逻辑，区别是阵营标记为敌方、命中不播放额外音效
    /// （敌方弹幕密集时如果每发都响，会糊成一片噪音）。
    /// </summary>
    public class EnemyBullet : BulletBase
    {
        /// <summary>敌方弹幕很密，命中特效压低一档，不然屏幕上全是火花</summary>
        protected override float HitScale { get { return 0.9f; } }

        public override void OnSpawn()
        {
            base.OnSpawn();
            friendly = false;
        }
    }
}
