using UnityEngine;

namespace PixelGravity
{
    /// <summary>一波敌人的描述。纯数据，没有行为。</summary>
    [System.Serializable]
    public class WaveDefinition
    {
        public EnemyKind kind = EnemyKind.Rusher;

        [Tooltip("混编波次：这一波按顺序轮着出这些敌人。留空就只用上面的 kind。"
               + "轮转而不是随机，是为了让每一波重复打的时候节奏一致——"
               + "随机会出现「这一把全出导弹」的极端情况，调试时根本没法复现")]
        public EnemyKind[] kinds = new EnemyKind[0];

        public int count = 5;
        [Tooltip("本波内两只敌人之间的间隔（秒）")]
        public float interval = 0.6f;
        [Tooltip("本波开始前的等待（秒）")]
        public float startDelay = 0.5f;

        /// <summary>这一波第 index 只该出哪种敌人</summary>
        public EnemyKind KindAt(int index)
        {
            if (kinds != null && kinds.Length > 0) return kinds[index % kinds.Length];
            return kind;
        }
    }

    /// <summary>
    /// 关卡配置：用「波次列表」描述一关的节奏，
    /// 关卡的难易程度完全由这个资产决定，不需要新写代码。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "PixelGravity/Level Config", order = 3)]
    public class LevelConfig : ScriptableObject
    {
        public string displayName = "SECTOR 01";

        [Tooltip("开场横幅的第二行：英文代号 + 一句地理描述，给星区一点实感")]
        public string subtitle = "SECTOR 01";

        public WaveDefinition[] waves = new WaveDefinition[0];

        [Header("Boss")]
        public bool hasBoss = false;

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                for (int i = 0; i < waves.Length; i++) total += waves[i].count;
                return total;
            }
        }
    }
}
