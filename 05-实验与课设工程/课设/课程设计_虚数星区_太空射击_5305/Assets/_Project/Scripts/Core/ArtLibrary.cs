using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 美术资源加载器。
    ///
    /// 资源放在 Assets/_Project/Art/Resources/Sprites/，用 Resources.Load 按名取。
    /// 为什么走 Resources 而不是在 Inspector 里拖引用：本项目所有对象都是运行时用代码拼出来的，
    /// 没有预制体可以拖引用，Resources 是这种情况下最直接的加载方式。
    ///
    /// 设计上刻意做成「允许失败」：找不到资源就返回 null，调用方回退到 SpriteFactory 的程序化贴图。
    /// 这样即使某个 png 被误删或者导入设置坏了，游戏依然能跑，只是那一处变回程序化绘制，
    /// 而不是满屏空物体、什么也看不见。
    /// </summary>
    public static class ArtLibrary
    {
        private const string Root = "Sprites/";

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>
        /// 每次进播放模式前清一次缓存。
        ///
        /// 静态字段在「Enter Play Mode Options 关掉域重载」时不会自动清空，
        /// 于是上一轮 Load 到的 Sprite 会一直被引用——重新导入的 png 在游戏里永远不生效，
        /// 表现出来就是「图换了但游戏里还是旧的」。
        /// SubsystemRegistration 阶段跑，早于任何 Awake。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache.Clear();
            missing.Clear();
        }

        // ------------------------------------------------------------ 通用加载

        public static Sprite Get(string name)
        {
            Sprite cached;
            if (cache.TryGetValue(name, out cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(Root + name);

            if (sprite == null)
            {
                // 兜底：如果这张图被导入成了普通贴图（Texture Type 不是 Sprite 2D and UI），
                // Resources.Load<Sprite> 会返回 null。这里直接取贴图自己切一张出来。
                Texture2D texture = Resources.Load<Texture2D>(Root + name);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = name;
                }
            }

            cache[name] = sprite;

            if (sprite == null && missing.Add(name))
            {
                Debug.LogWarning("[ArtLibrary] 缺少精灵资源 " + Root + name + "，该处将回退到程序化贴图。");
            }

            return sprite;
        }

        /// <summary>取资源，拿不到就用 fallback（通常是 SpriteFactory 的程序化版本）</summary>
        public static Sprite Get(string name, Sprite fallback)
        {
            Sprite sprite = Get(name);
            return sprite != null ? sprite : fallback;
        }

        // ------------------------------------------------------------ 敌人贴图映射

        /// <summary>
        /// 敌人贴图名。
        ///
        /// 单独抽出来是因为敌人图鉴也要用同一份映射：图鉴以前自己按名字去取图，
        /// 结果三格里有两格和局内对不上（比如追踪导弹取错了 key），
        /// 玩家看到的是"图鉴里长这样、游戏里长那样"。映射只能有一份。
        /// </summary>
        public static string EnemySpriteName(EnemyKind kind)
        {
            if (kind == EnemyKind.Turret) return "enemy_turret";
            if (kind == EnemyKind.Tracker) return "enemy_seeker";
            return "enemy_rusher";
        }

        /// <summary>播放结束/退出时清缓存，避免编辑器里反复进入播放模式残留引用</summary>
        public static void Clear()
        {
            cache.Clear();
            missing.Clear();
        }
    }
}
