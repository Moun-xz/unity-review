using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 武器配置。两种武器的差异全部由数据表达，射击代码只有一份。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "PixelGravity/Weapon Config", order = 2)]
    public class WeaponConfig : ScriptableObject
    {
        public WeaponKind kind = WeaponKind.Laser;
        public string displayName = "PULSE LASER";

        [Header("射击节奏")]
        [Tooltip("两次射击之间的间隔（秒）。0.12 约等于每秒 8 发")]
        public float fireRate = 0.12f;

        [Header("弹道")]
        public int bulletCount = 1;
        [Tooltip("多发时相邻子弹的夹角（度）")]
        public float spreadAngle = 15f;

        [Header("伤害")]
        public float damage = 10f;
        public float bulletSpeed = 20f;
        public float bulletLife = 2.5f;

        [Tooltip("连续光束（kind = Laser）每秒伤害。子弹类武器不看这一项")]
        public float beamDps = 52f;

        [Header("外观")]
        public Color bulletColor = new Color(0.35f, 0.95f, 1f, 1f);
        public float bulletScale = 1f;

        /// <summary>每秒理论伤害，用于调平衡时快速比较两种武器</summary>
        public float Dps
        {
            get { return fireRate > 0f ? damage * bulletCount / fireRate : 0f; }
        }
    }
}
