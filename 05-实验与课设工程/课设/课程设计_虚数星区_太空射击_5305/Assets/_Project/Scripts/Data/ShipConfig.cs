using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 战机配置。机型差异全部由数据表达，玩法代码只有一份。
    ///
    /// 为什么把「射击间隔倍率」和「伤害倍率」乘在 WeaponConfig 上，而不是给每艘船
    /// 各写一套武器：武器是共用的（玩家在局内按 1/2 换），船只决定「打得快还是打得疼」。
    /// 这样将来加一艘新船不用复制两套武器数值，也不会出现「换了船但武器没跟着变」的漏洞。
    /// </summary>
    [CreateAssetMenu(fileName = "ShipConfig", menuName = "PixelGravity/Ship Config", order = 5)]
    public class ShipConfig : ScriptableObject
    {
        public string displayName = "重锤";
        public string callsign = "HAMMER";

        [TextArea(2, 4)]
        public string description = "";

        [Header("生存")]
        public float maxHp = 100f;

        [Header("输出")]
        [Tooltip("射击间隔倍率。小于 1 = 射得更快，大于 1 = 射得更慢")]
        public float fireRateScale = 1f;

        [Tooltip("单发伤害倍率")]
        public float damageScale = 1f;

        [Header("机动")]
        public float moveSpeed = 8f;
        public float acceleration = 55f;

        [Header("外观")]
        [Tooltip("机身缩放。素材宽度不一样，靠这个把各条船的画面尺寸拉齐")]
        public float bodyScale = 1.25f;

        [Tooltip("双炮口到机身中线的距离")]
        public float muzzleSpread = 0.30f;

        public Color accent = new Color(0.35f, 0.95f, 1f, 1f);

        [Tooltip("第二色：机翼外侧与翼尖的配色。和 accent 组成双色调，避免整架飞机只有一个颜色")]
        public Color accentAlt = new Color(0.18f, 0.42f, 1f, 1f);

        /// <summary>火力评分（0~1），选择界面画条用</summary>
        public float FireRating
        {
            get { return Mathf.Clamp01(damageScale / fireRateScale / 1.6f); }
        }

        /// <summary>装甲评分（0~1）</summary>
        public float ArmorRating
        {
            get { return Mathf.Clamp01(maxHp / 160f); }
        }

        /// <summary>机动评分（0~1）</summary>
        public float SpeedRating
        {
            get { return Mathf.Clamp01(moveSpeed / 10.5f); }
        }
    }
}
