using System;

namespace PixelGravity
{
    /// <summary>
    /// 全局事件总线：逻辑层只管「喊一声」，表现层自己决定要不要听。
    /// UI 因此完全不需要引用 PlayerController / GameManager 等逻辑类，
    /// 也就不会出现「UI 改了导致玩法崩」这种连锁问题。
    /// 注意：订阅方必须在 OnDisable 里退订，否则对象销毁后仍被事件持有会造成空引用。
    /// </summary>
    public static class EventBus
    {
        /// <summary>玩家血量变化（当前值, 最大值）</summary>
        public static event Action<int, int> PlayerHpChanged;

        /// <summary>得分变化</summary>
        public static event Action<int> ScoreChanged;

        /// <summary>连击数变化（0 表示连击中断）</summary>
        public static event Action<int> ComboChanged;

        /// <summary>武器切换（索引, 显示名）——把名字一起带上，界面就不用再维护一份对照表</summary>
        public static event Action<int, string> WeaponChanged;

        /// <summary>Boss 血量（0~1 归一化）</summary>
        public static event Action<float> BossHpChanged;

        /// <summary>Boss 血条显隐（是否显示, Boss 名字）</summary>
        public static event Action<bool, string> BossBarVisible;

        /// <summary>关卡名变化</summary>
        public static event Action<string> LevelChanged;

        /// <summary>
        /// 星区推进（当前第几波, 总波数）。
        /// 玩家靠它判断「离下一个星区还有多远」——只显示关卡名的话，
        /// 一关打十分钟也不知道自己走到哪了。
        /// </summary>
        public static event Action<int, int> LevelProgressChanged;

        /// <summary>全局状态切换</summary>
        public static event Action<GameState> StateChanged;

        /// <summary>敌人被击杀（关卡管理器用它统计存活数量）</summary>
        public static event Action<EnemyBase> EnemyKilled;

        /// <summary>历史最高分变化</summary>
        public static event Action<int> HighScoreChanged;

        /// <summary>
        /// 机库里换船了。
        /// 菜单里战机是看得见的（停在左下角），换船后必须立刻换皮，
        /// 否则「当前战机：赤影」旁边还停着上一艘船。UI 只负责喊一声，
        /// 谁去改玩家由订阅方（PlayerRig）决定，界面不反向引用游戏逻辑。
        /// </summary>
        public static event Action LoadoutChanged;

        public static void RaisePlayerHp(int current, int max) { if (PlayerHpChanged != null) PlayerHpChanged(current, max); }
        public static void RaiseScore(int score) { if (ScoreChanged != null) ScoreChanged(score); }
        public static void RaiseCombo(int combo) { if (ComboChanged != null) ComboChanged(combo); }
        public static void RaiseWeapon(int index, string displayName) { if (WeaponChanged != null) WeaponChanged(index, displayName); }
        public static void RaiseBossHp(float normalized) { if (BossHpChanged != null) BossHpChanged(normalized); }
        public static void RaiseBossBar(bool visible, string displayName) { if (BossBarVisible != null) BossBarVisible(visible, displayName); }
        public static void RaiseLevel(string levelName) { if (LevelChanged != null) LevelChanged(levelName); }
        public static void RaiseLevelProgress(int current, int total) { if (LevelProgressChanged != null) LevelProgressChanged(current, total); }
        public static void RaiseState(GameState state) { if (StateChanged != null) StateChanged(state); }
        public static void RaiseEnemyKilled(EnemyBase enemy) { if (EnemyKilled != null) EnemyKilled(enemy); }
        public static void RaiseHighScore(int score) { if (HighScoreChanged != null) HighScoreChanged(score); }
        public static void RaiseLoadoutChanged() { if (LoadoutChanged != null) LoadoutChanged(); }

        /// <summary>
        /// 进入播放模式时把静态事件清空。
        /// 静态字段不会随场景卸载而重置，不清会在「关闭域重载」的编辑器设置下累积到下一次运行。
        /// 必须在所有订阅者创建之前调用（GameBootstrap.Awake 的第一行）。
        /// </summary>
        public static void Clear()
        {
            PlayerHpChanged = null;
            ScoreChanged = null;
            ComboChanged = null;
            WeaponChanged = null;
            BossHpChanged = null;
            BossBarVisible = null;
            LevelChanged = null;
            LevelProgressChanged = null;
            StateChanged = null;
            EnemyKilled = null;
            HighScoreChanged = null;
            LoadoutChanged = null;   // 之前漏了这一行，静态事件会跨域重载残留
        }
    }
}
