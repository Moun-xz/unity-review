namespace PixelGravity
{
    /// <summary>全局游戏状态。由 GameManager 统一驱动，任何面板都不允许自行切换状态。</summary>
    public enum GameState
    {
        Boot,       // 启动中，还没建好场景
        MainMenu,   // 主菜单
        Playing,    // 战斗中
        Paused,     // 暂停
        GameOver,   // 玩家阵亡
        Victory     // 通关
    }

    /// <summary>
    /// 敌人的行为类型，配置表里用这个字段决定生成哪种 FSM 派生类。
    /// 前三种是「会主动打你」的敌机；陨石是环境威胁——不瞄准、不射击，
    /// 靠体积和惯性占住空间，逼玩家在躲弹幕之外还要躲地形。
    /// </summary>
    public enum EnemyKind
    {
        Rusher,   // 直线冲锋
        Turret,   // 停驻弹幕
        Tracker,  // 追踪自爆
        Meteor    // 陨石：漂流、自转、体量不一
    }

    /// <summary>敌人状态机的四个状态。</summary>
    public enum EnemyState
    {
        Spawning,   // 出生动画 / 出场保护
        Active,     // 按各自的移动策略行动
        Attacking,  // 进入攻击循环
        Dying       // 死亡收尾
    }

    /// <summary>两种玩家武器。</summary>
    public enum WeaponKind
    {
        Laser,    // 普通激光：快、单发、低伤
        Spread    // 双轨机炮：连射、双条平行弹链、单发低伤
    }
}
