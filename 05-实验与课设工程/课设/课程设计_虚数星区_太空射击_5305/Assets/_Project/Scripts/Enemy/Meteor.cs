using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 陨石：环境威胁，不瞄准也不射击。
    ///
    /// 和三种敌机的区别有三点：
    /// 1. 体量随机 —— 同一份配置要出大中小三种。尺寸系数在出生时掷骰子，
    ///    血量、移动速度都跟着体积走：大的更抗打也更慢，小的快、一碰就碎。
    /// 2. 自转 —— 陨石不朝任何方向，只按自己的角速度滚。配上不规则轮廓，
    ///    同一个 sprite 在不同角度看起来就是另一颗石头，等于把形状数量又翻了几倍。
    /// 3. 漂移 —— 主方向向下，叠加一个固定横向漂移 + 一条正弦摆动，
    ///    所以它不是笔直下坠：玩家能预判大方向，但仍要提前绕。
    ///
    /// 碰撞伤害走 EnemyBase.ContactDamage，PlayerHealth 那侧不需要任何改动就能认。
    /// </summary>
    public class Meteor : EnemyBase
    {
        private float spinSpeed;    // 自转角速度，度/秒
        private float driftX;       // 横向漂移，世界单位/秒
        private float swingPhase;   // 正弦摆动的相位
        private float swingAmp;     // 正弦摆动的幅度

        // 体积与血量的联动区间。公开出去是因为图鉴要照实写「血量 24~92」，
        // 两边各存一份常数迟早会写歪。
        public const float MinSize = 0.55f;
        public const float MaxSize = 1.55f;
        public const float HpScaleSmall = 0.6f;
        public const float HpScaleLarge = 2.3f;

        protected override void OnSpawned()
        {
            sizeMultiplier = Random.Range(MinSize, MaxSize);
            float t = Mathf.InverseLerp(MinSize, MaxSize, sizeMultiplier);

            // 血量随体积走。不这么做的话「大石头三枪就碎」会很出戏，
            // 而小石头又硬得莫名其妙——玩家对体量和耐打程度的直觉很强。
            hp *= Mathf.Lerp(HpScaleSmall, HpScaleLarge, t);

            spinSpeed = Random.Range(-64f, 64f);
            driftX = Random.Range(-1.1f, 1.1f);
            swingPhase = Random.Range(0f, 6.2831f);
            swingAmp = Random.Range(0.18f, 0.55f);

            // 初始朝向随机：同一张石头贴图转个角度就是另一颗
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }

        protected override void TickActive()
        {
            float dt = Time.deltaTime;

            // 自转不受局部缩放影响，直接转本节点
            transform.Rotate(0f, 0f, spinSpeed * dt);

            float t = Mathf.InverseLerp(MinSize, MaxSize, sizeMultiplier);
            float fallSpeed = Config.moveSpeed / Mathf.Lerp(0.85f, 1.35f, t);   // 越大越慢

            Vector3 p = transform.position;
            p.y -= fallSpeed * dt;
            p.x += (driftX + Mathf.Sin(Time.time * 0.8f + swingPhase) * swingAmp) * dt;
            transform.position = p;

            // 横着飘出屏幕也算离场，否则会在屏幕外一直占着存活计数
            if (GameArea.IsOutside(transform.position, 1.8f)) DespawnSelf();
        }
    }
}
