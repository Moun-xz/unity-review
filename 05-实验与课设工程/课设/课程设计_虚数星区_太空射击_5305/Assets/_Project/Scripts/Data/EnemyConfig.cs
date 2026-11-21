using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 敌人配置。所有可调数值集中在这里，改难度不用改代码、不用重新编译。
    /// 用 ScriptableObject 而不是常量，是为了能在 Project 窗口里右键创建资产、
    /// 由策划（或者调参的你）直接改。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "PixelGravity/Enemy Config", order = 1)]
    public class EnemyConfig : ScriptableObject
    {
        [Header("身份")]
        public EnemyKind kind = EnemyKind.Rusher;
        public string displayName = "Rusher";

        [Header("生存")]
        public float maxHp = 30f;
        public float bodyScale = 0.9f;
        public Color tint = new Color(1f, 0.23f, 0.36f, 1f);

        [Header("移动")]
        public float moveSpeed = 5f;
        public float turnSpeed = 110f;      // 仅追踪型使用

        [Header("攻击")]
        public float fireInterval = 0f;     // 0 表示不远程攻击
        public float bulletSpeed = 8f;
        public float bulletDamage = 10f;
        public float contactDamage = 20f;

        [Header("结算")]
        public int score = 100;
    }
}
