using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 把一份 ShipConfig 套到玩家身上。
    ///
    /// 为什么单独抽一个组件而不是让 ObjectFactory 全包：
    /// 玩家对象在游戏启动时就造好了，而「选哪艘船」是在主菜单里决定的——
    /// 中间隔着一整个界面。与其在开局时销毁重建玩家（会丢引用、要重新注册池子），
    /// 不如让玩家自己能被重新刷一遍配置。这样换船只是改几个数，不动对象。
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        private SpriteRenderer body;
        private SpriteRenderer thruster;
        private ThrusterFlame flame;
        private SpriteRenderer glow;
        private PlayerController controller;
        private PlayerHealth health;
        private PlayerShooter shooter;

        public void Bind(SpriteRenderer bodyRenderer, SpriteRenderer thrusterRenderer,
                         PlayerController playerController, PlayerHealth playerHealth, PlayerShooter playerShooter,
                         SpriteRenderer hullGlow = null, ThrusterFlame thrusterFlame = null)
        {
            body = bodyRenderer;
            thruster = thrusterRenderer;
            flame = thrusterFlame;
            controller = playerController;
            health = playerHealth;
            shooter = playerShooter;
            glow = hullGlow;
        }

        public void Apply(ShipConfig ship)
        {
            if (ship == null) return;

            if (body != null) body.sprite = ShipArt.Get(ship);

            if (glow != null)
            {
                Color halo = ship.accent;
                halo.a = 0.16f;
                glow.color = halo;
            }

            // 各艘船素材宽度不一样（98~112 像素），用 bodyScale 把画面尺寸拉齐，
            // 不然换船后玩家会明显觉得「船突然变大/变小」，判定框却没变。
            transform.localScale = Vector3.one * ship.bodyScale;

            if (thruster != null)
            {
                Color trail = ship.accent;
                trail.a = thruster.color.a;      // 保留原来的透明度，只换色相
                thruster.color = trail;
            }

            if (flame != null) flame.SetAccent(ship.accent);

            if (controller != null)
            {
                controller.moveSpeed = ship.moveSpeed;
                controller.acceleration = ship.acceleration;
            }

            if (health != null) health.SetMaxHp(Mathf.RoundToInt(ship.maxHp));
            if (shooter != null) shooter.ApplyShip(ship);
        }

        /// <summary>
        /// 机库里换了船就立刻换皮。
        /// 主菜单里战机是看得见的（停在左下角的停机位），要是等开局才 Apply，
        /// 就会出现「当前战机：赤影 · WRAITH」旁边还停着上一艘船。
        /// </summary>
        private void OnEnable()
        {
            EventBus.LoadoutChanged += ApplySelected;
        }

        private void OnDisable()
        {
            EventBus.LoadoutChanged -= ApplySelected;
        }

        private void ApplySelected()
        {
            Apply(PlayerLoadout.Selected);
        }
    }
}
