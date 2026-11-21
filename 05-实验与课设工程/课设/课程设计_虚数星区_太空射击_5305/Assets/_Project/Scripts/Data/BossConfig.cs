using UnityEngine;

namespace PixelGravity
{
    /// <summary>Boss 配置：血量、各阶段切换阈值、弹幕参数。</summary>
    [CreateAssetMenu(fileName = "BossConfig", menuName = "PixelGravity/Boss Config", order = 4)]
    public class BossConfig : ScriptableObject
    {
        public string displayName = "WARDEN-01";

        [Header("生存")]
        public float maxHp = 1500f;
        public Color tint = new Color(0.85f, 0.25f, 0.95f, 1f);

        [Header("阶段阈值（剩余血量比例）")]
        [Tooltip("低于第一个阈值进入二阶段，低于第二个进入三阶段")]
        public float phase2Threshold = 0.66f;
        public float phase3Threshold = 0.33f;

        [Header("弹幕")]
        public float bulletSpeed = 7f;
        public float bulletDamage = 12f;
        public int fanCount = 5;             // 一阶段扇形弹数量
        public float fanAngle = 18f;         // 相邻弹夹角

        [Header("结算")]
        public int score = 2000;
    }
}
