using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 全局引用点：只放「到处都要用、且生命周期跟随本体」的东西。
    /// 这不是单例业务逻辑，只是避免每个敌人各自 FindGameObjectWithTag("Player")。
    /// </summary>
    public static class Refs
    {
        public static Transform Player;
        public static Camera Camera;

        /// <summary>星空背景。关卡切换时要通知它换星区底图，所以放在这里</summary>
        public static Starfield Starfield;

        /// <summary>
        /// 公告横幅（关卡开场 / Boss 警告）。关卡调度要播演出，
        /// 但 LevelManager 不该反向持有 UI 引用，走这个注册点。
        /// </summary>
        public static Announcement Announcement;

        public static Vector3 PlayerPosition
        {
            get { return Player != null ? Player.position : Vector3.zero; }
        }

        public static Vector2 PlayerPosition2D
        {
            get { return Player != null ? (Vector2)Player.position : Vector2.zero; }
        }
    }

    /// <summary>
    /// 对象池的键名常量。集中管理，避免各处手写字符串拼错导致取到空池。
    /// </summary>
    public static class PoolKeys
    {
        public const string LaserBullet = "LaserBullet";
        public const string SpreadBullet = "SpreadBullet";
        public const string EnemyBullet = "EnemyBullet";

        public const string EnemyRusher = "Enemy_Rusher";
        public const string EnemyTurret = "Enemy_Turret";
        public const string EnemyTracker = "Enemy_Tracker";
        public const string EnemyMeteor = "Enemy_Meteor";

        public const string Boss = "Boss";

        public static string BulletOf(WeaponKind kind)
        {
            return kind == WeaponKind.Laser ? LaserBullet : SpreadBullet;
        }

        public static string EnemyOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Turret: return EnemyTurret;
                case EnemyKind.Tracker: return EnemyTracker;
                case EnemyKind.Meteor: return EnemyMeteor;
                default: return EnemyRusher;
            }
        }
    }
}
