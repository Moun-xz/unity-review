using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 敌人行为②：弹幕发射。
    /// 先降落到屏幕上半区的一个停驻高度，然后进入 Attacking 状态，
    /// 在停驻点上做小幅上下浮动并周期性地打出三发扇形弹幕。
    /// 这也是 StateMachine 里 Active → Attacking 状态迁移的实际用例。
    /// </summary>
    public class TurretShooter : EnemyBase
    {
        /// <summary>开火前多久开始预警。0.45 秒大约够玩家横移一个身位</summary>
        private const float Telegraph = 0.45f;

        private float stationY;
        private float fireTimer;
        private float phaseOffset;

        private SpriteRenderer chargeLamp;

        protected override void OnSpawned()
        {
            // 停驻高度：屏幕上方 62% 处，随机下浮一点避免全挤在同一条线上
            stationY = GameArea.HalfHeight * 0.62f - Random.Range(0f, 1.4f);
            fireTimer = Config.fireInterval * 0.7f;
            phaseOffset = Random.Range(0f, 6.28f);

            // 灯是子物体、跟着机体一起被池回收，所以只在第一次创建，
            // 每次出生只把亮度清零——否则池里复用一次就多挂一盏灯。
            if (chargeLamp == null) BuildChargeLamp();
            SetCharge(0f);
        }

        /// <summary>
        /// 炮口预警灯。
        ///
        /// 弹幕游戏里「不知道什么时候会挨打」比「挨打」更让人恼火，所以开火前用一个
        /// 由暗到亮、由小到大的橙色光点预告。用光点而不是光晕：星云背景本身就是
        /// 橙红/品红的，大面积柔光会被背景吃掉，只有小面积的亮点才跳得出来。
        /// </summary>
        private void BuildChargeLamp()
        {
            GameObject go = new GameObject("ChargeLamp");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.34f, 0.1f);
            go.transform.localScale = Vector3.one * 0.16f;

            chargeLamp = go.AddComponent<SpriteRenderer>();
            chargeLamp.sprite = SpriteFactory.Star();
            chargeLamp.sortingOrder = 9;          // 压在机体之上，否则被炮塔的护罩挡掉
            chargeLamp.color = new Color(1f, 0.72f, 0.30f, 0f);
        }

        private void SetCharge(float t)
        {
            if (chargeLamp == null) return;
            float a = Mathf.Clamp01(t);
            chargeLamp.color = new Color(1f, 0.72f, 0.30f, a * 0.95f);
            float s = Mathf.Lerp(0.16f, 0.32f, a);
            chargeLamp.transform.localScale = new Vector3(s, s, 1f);
        }

        protected override void TickActive()
        {
            Vector3 position = transform.position;
            position.y -= Config.moveSpeed * Time.deltaTime;
            transform.position = position;

            if (position.y <= stationY) ChangeState(EnemyState.Attacking);
        }

        protected override void TickAttack()
        {
            Vector3 position = transform.position;
            position.y = stationY + Mathf.Sin(Time.time * 1.5f + phaseOffset) * 0.28f;
            transform.position = position;

            fireTimer -= Time.deltaTime;

            // 越接近开火，灯越亮越大。t=0 刚开完火，t=1 即将开火
            SetCharge(1f - Mathf.Clamp01(fireTimer / Telegraph));

            if (fireTimer <= 0f)
            {
                fireTimer = Config.fireInterval;
                FireFan();
                SetCharge(0f);
            }
        }

        /// <summary>三发扇形弹幕，以正下方为中心左右各偏 20 度</summary>
        private void FireFan()
        {
            for (int i = -1; i <= 1; i++)
            {
                float angle = i * 20f;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.down;
                FireBullet(direction, Config.bulletDamage, Config.bulletSpeed, 6f);
            }
        }
    }
}
