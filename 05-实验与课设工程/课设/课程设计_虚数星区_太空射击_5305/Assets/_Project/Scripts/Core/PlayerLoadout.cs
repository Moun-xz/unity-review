using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 玩家在机库里选中的战机。
    ///
    /// 它只是一个静态的「当前选择」存放点，不是单例业务逻辑——没有生命周期、
    /// 不发事件、不碰游戏状态，所以不违反「全局只有四个管理器单例」的约定。
    ///
    /// 选择结果写进 PlayerPrefs，下次打开游戏还是上次选的那艘。
    /// </summary>
    public static class PlayerLoadout
    {
        private const string IndexKey = "pg_ship_index";

        private static ShipConfig[] ships;
        private static int selected = -1;

        /// <summary>由 GameBootstrap 注入（GameConfig.Ships()）</summary>
        public static void Setup(ShipConfig[] configs)
        {
            ships = configs;
            selected = -1;   // 逼一次重新读取，避免沿用上一次运行的缓存
        }

        public static ShipConfig[] Ships { get { return ships; } }

        public static int Count { get { return ships != null ? ships.Length : 0; } }

        public static int SelectedIndex
        {
            get
            {
                if (ships == null || ships.Length == 0) return 0;

                if (selected < 0)
                {
                    selected = Mathf.Clamp(PlayerPrefs.GetInt(IndexKey, 0), 0, ships.Length - 1);
                }
                return selected;
            }
            set
            {
                if (ships == null || ships.Length == 0) return;

                selected = Mathf.Clamp(value, 0, ships.Length - 1);
                PlayerPrefs.SetInt(IndexKey, selected);
            }
        }

        public static ShipConfig Selected
        {
            get
            {
                if (ships == null || ships.Length == 0) return null;
                return ships[SelectedIndex];
            }
        }
    }
}
