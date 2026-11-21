using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 数值配置中心：在运行时把 ScriptableObject 实例化出来。
    ///
    /// 为什么是运行时创建而不是 .asset 文件：
    /// 这样项目「打开即跑」，不依赖任何资源文件路径；
    /// 之后想改成可视化编辑，只要在 Project 窗口右键 Create → PixelGravity → Enemy Config，
    /// 再把 GameConfig 里的对应行换成引用即可，逻辑代码一行都不用动。
    /// </summary>
    public static class GameConfig
    {
        // ------------------------------------------------------------ 敌人

        public static EnemyConfig Rusher()
        {
            EnemyConfig c = ScriptableObject.CreateInstance<EnemyConfig>();
            c.name = "Enemy_Rusher";
            c.kind = EnemyKind.Rusher;
            c.displayName = "突进者";
            c.maxHp = 22f;
            c.bodyScale = 1.05f;
            c.tint = new Color(1f, 0.24f, 0.36f, 1f);
            c.moveSpeed = 5.2f;
            c.fireInterval = 0f;
            c.contactDamage = 20f;
            c.score = 100;
            return c;
        }

        public static EnemyConfig Turret()
        {
            EnemyConfig c = ScriptableObject.CreateInstance<EnemyConfig>();
            c.name = "Enemy_Turret";
            c.kind = EnemyKind.Turret;
            c.displayName = "哨戒炮台";
            c.maxHp = 36f;
            c.bodyScale = 1.15f;
            c.tint = new Color(0.85f, 0.22f, 0.95f, 1f);
            c.moveSpeed = 2.4f;
            c.fireInterval = 1.2f;
            c.bulletSpeed = 7f;
            c.bulletDamage = 10f;
            c.contactDamage = 20f;
            c.score = 150;
            return c;
        }

        /// <summary>
        /// 追踪导弹。
        ///
        /// 计划书要的是「追踪导弹」，不是「会追人的敌机」——这两者的区别在过程：
        /// 导弹得先锁、再点火、末段加速，玩家能读出「它盯上我了 / 它要冲了」两个节点。
        /// 所以行为拆成三段（见 Tracker.cs），这里的 moveSpeed 只是**起飞速度**，
        /// 末段会加速到它的 1.9 倍，避免"慢慢飘过来撞你"这种不像导弹的观感。
        ///
        /// 血量 22（杂兵里最脆）：它靠速度和追踪制造压力，不靠耐久。
        /// 一梭子弹就该能把它打下来——打不掉的追踪物只会让人烦躁。
        /// turnSpeed 110 是它可被绕开的关键：转向速度有限，一直横向拉扯就能甩脱。
        /// </summary>
        public static EnemyConfig Tracker()
        {
            EnemyConfig c = ScriptableObject.CreateInstance<EnemyConfig>();
            c.name = "Enemy_Tracker";
            c.kind = EnemyKind.Tracker;
            c.displayName = "追踪导弹";
            c.maxHp = 22f;
            c.bodyScale = 1.0f;
            c.tint = new Color(1f, 0.52f, 0.22f, 1f);
            c.moveSpeed = 3.2f;
            c.turnSpeed = 110f;
            c.contactDamage = 30f;
            c.score = 150;
            return c;
        }

        /// <summary>
        /// 陨石。注意 bodyScale 是「基准尺寸」，实际大小由 Meteor 在出生时再乘 0.55~1.55，
        /// 所以这里填 1.9 是让最大那颗明显大过战机、最小那颗只有战机一半。
        /// maxHp 同样会被体积系数缩放（0.6~2.3 倍），大的更抗打。
        ///
        /// 1.9 这个数不是随手填的：陨石贴图是 1×1 世界单位，但石头只占贴图的 62%
        /// （轮廓半径被归一到 0.62，好让 8 种形状的视觉体量一致），
        /// 所以实际直径 = 0.62 × 1.9 × 体积系数 ≈ 1.2~1.8 单位，和战机（1.4 单位）同一个量级。
        /// </summary>
        public static EnemyConfig Meteor()
        {
            EnemyConfig c = ScriptableObject.CreateInstance<EnemyConfig>();
            c.name = "Enemy_Meteor";
            c.kind = EnemyKind.Meteor;
            c.displayName = "陨石";
            c.maxHp = 40f;
            c.bodyScale = 1.9f;
            c.tint = new Color(0.62f, 0.68f, 0.82f, 1f);
            c.moveSpeed = 2.2f;
            c.fireInterval = 0f;
            c.contactDamage = 25f;
            c.score = 80;
            return c;
        }

        public static EnemyConfig Enemy(EnemyKind kind)
        {
            if (kind == EnemyKind.Turret) return Turret();
            if (kind == EnemyKind.Tracker) return Tracker();
            if (kind == EnemyKind.Meteor) return Meteor();
            return Rusher();
        }

        // ------------------------------------------------------------ 武器

        public static WeaponConfig LaserWeapon()
        {
            WeaponConfig w = ScriptableObject.CreateInstance<WeaponConfig>();
            w.name = "Weapon_Laser";
            w.kind = WeaponKind.Laser;
            w.displayName = "离子光束";
            w.fireRate = 0.12f;
            // 现在是一根连续光束，不再按发数算伤害，看 beamDps。
            // 52 ≈ 每秒烧掉两个突进者（22 血）多一点，比原来 83 的爆发低，
            // 但它是必中的——不需要预判，贴着瞄就行。
            w.damage = 5f;
            w.beamDps = 52f;
            w.bulletSpeed = 20f;
            w.bulletCount = 1;
            w.bulletLife = 2.5f;
            w.bulletColor = new Color(0.4f, 0.95f, 1f, 1f);
            w.bulletScale = 1.5f;
            return w;
        }

        public static WeaponConfig SpreadWeapon()
        {
            WeaponConfig w = ScriptableObject.CreateInstance<WeaponConfig>();
            w.name = "Weapon_Spread";
            w.kind = WeaponKind.Spread;
            w.displayName = "双轨机炮";
            // 连射：每秒 18 发，两个炮口各出一条弹链。
            //
            // 原来是一轮两发、0.45 秒一次（"齐射"），打起来是两个绿球慢慢飘上去，
            // 既不像炮也不像枪。拆成弹链之后视觉上是"两条不断线的火"：
            // 弹速 15.5 × 间隔 0.055 秒 = 相邻两颗相距 0.85 世界单位（约 82 像素），
            // 也就是 2.5 个弹身的空档——再疏就成点射，再密就糊成一条实线。
            //
            // 总伤害刻意保持在 53 左右：2 发 × 1.45 伤害 ÷ 0.055 秒 = 52.7，
            // 和原来 2×12÷0.45 = 53.3 基本一致——换的是手感，不是数值。
            w.fireRate = 0.055f;
            // 弹道仍然平行向上，不做扇形。扇形六条弹道互相插空，远处糊成一团，
            // 玩家判断不出"我这条线到底打不打得到"；两条平行弹链把两门炮的间距
            // 直接画在屏幕上，指哪儿打哪儿。
            w.damage = 1.45f;
            w.bulletSpeed = 15.5f;
            w.bulletCount = 1;
            w.spreadAngle = 0f;
            w.bulletLife = 2.5f;
            // 弹体色：金 / 琥珀，不是绿。
            //
            // 绿色在游戏语义里容易被读成「治疗 / 回复」，而且跟敌人那套橙红弹放在一起时，
            // 玩家的第一反应是「这颗是谁打的」。改金之后三色分工很清楚：
            // 玩家光束 = 青、玩家机炮 = 金、敌人 = 暗橙红，色相和明度都拉开了。
            // 主体取 (1.00, 0.78, 0.26)：比敌弹 (213,85,38) 更黄、更亮，形状又是一个「细梭」一个「圆球」，
            // 高速交叉时靠「亮金 vs 暗红」一句话就能分。
            w.bulletColor = new Color(1.00f, 0.78f, 0.26f, 1f);
            // 贴图是 30×68 px（ppu 100 → 0.30×0.68 单位），0.66 缩放后
            // 屏幕上 20×45 像素：和光束同宽（激光 20px），挤成一条链仍数得清发数。
            //
            // 原来给 0.5（15×34）偏小——弹链在 1080p 上细得像一根虚线，
            // 认不出是子弹。放大之后相邻两颗的间距（0.85 单位 ≈ 82px）
            // 仍有 1.8 个弹身，不至于糊成一条实线；要再疏就提 bulletSpeed。
            w.bulletScale = 0.66f;
            return w;
        }

        // ------------------------------------------------------------ 战机

        /// <summary>
        /// 四艘可选战机。设计上尽量让每条船的「强项」一眼看得出来：
        /// 疾风均衡、蜂刺机动、重锤抗揍输出猛、赤影快且狠但脆。
        /// </summary>
        /// <summary>
        /// 唯一可选战机：重锤 HAMMER。
        ///
        /// 原来有疾风 / 蜂刺 / 重锤 / 赤影四条船，机库里挑一艘。做个一个决定：
        /// 与其四条平均水平、每条都"能玩但不出彩"，不如只留一条，把它做扎实——
        /// 数值、模型、配色都按主角机来打磨，玩家也不用在菜单里做一次没有信息量的选择。
        /// </summary>
        public static ShipConfig[] Ships()
        {
            ShipConfig hammer = ScriptableObject.CreateInstance<ShipConfig>();
            hammer.name = "Ship_Hammer";
            hammer.displayName = "重锤";
            hammer.callsign = "HAMMER";
            hammer.description = "重装突击机。血厚、单发伤害高，代价是转向偏慢。";
            hammer.maxHp = 150f;
            hammer.fireRateScale = 1.15f;
            hammer.damageScale = 1.45f;
            hammer.moveSpeed = 6.6f;
            hammer.acceleration = 42f;
            hammer.bodyScale = 1.42f;
            hammer.muzzleSpread = 0.22f;
            hammer.accent = new Color(1f, 0.74f, 0.28f, 1f);
            hammer.accentAlt = new Color(0.30f, 0.42f, 0.62f, 1f);

            return new[] { hammer };
        }

        // ------------------------------------------------------------ 关卡

        /// <summary>
        /// 两个星区的波次表。
        ///
        /// 每一波都是**混编**的：同一波里轮着出不同种类的敌人。
        /// 之前一波只出一种，屏幕上是一整排在同一个位置往下压的同一个图形，
        /// 看着像刷怪表、打起来也只是"按住空格清场"；
        /// 混起来之后冲锋的负责逼你动、炮台负责封走位、导弹负责逼你回头看，
        /// 三种压力同时存在，视角和手感才不单调。
        ///
        /// 混合配比是按"这一波想教玩家什么"排的：
        /// 星区 01 前三波一种一种往里加，第四波开始三种混着来；
        /// 星区 02 一上来就是三种混编，且导弹出现频率明显变高。
        /// </summary>
        public static LevelConfig[] Levels()
        {
            LevelConfig l1 = ScriptableObject.CreateInstance<LevelConfig>();
            l1.name = "Level_01";
            l1.displayName = "星区 01";
            l1.subtitle = "SECTOR 01 · 掠影带";
            l1.hasBoss = false;
            l1.waves = new[]
            {
                // 第一波：先认「会撞你的」和「会追你的」两种，混一枚导弹探探反应
                NewWaveMix(Mix(EnemyKind.Rusher, EnemyKind.Rusher, EnemyKind.Tracker), 6, 0.55f, 1.0f),
                // 第二波：陨石混进冲锋队里，逼玩家在躲弹幕之外还要躲地形
                NewWaveMix(Mix(EnemyKind.Meteor, EnemyKind.Rusher), 4, 1.2f, 1.2f),
                // 第三波：炮台第一次出现，配两个冲锋的把它围起来，逼玩家先拆炮台
                NewWaveMix(Mix(EnemyKind.Turret, EnemyKind.Rusher, EnemyKind.Rusher), 4, 1.3f, 1.4f),
                // 第四波起四种混编，节奏开始真正交错
                NewWaveMix(Mix(EnemyKind.Rusher, EnemyKind.Meteor, EnemyKind.Tracker), 5, 0.5f, 1.0f),
                NewWaveMix(Mix(EnemyKind.Turret, EnemyKind.Tracker), 3, 1.1f, 1.0f),
                NewWaveMix(Mix(EnemyKind.Rusher, EnemyKind.Turret, EnemyKind.Tracker, EnemyKind.Meteor),
                    7, 0.45f, 1.0f)
            };

            LevelConfig l2 = ScriptableObject.CreateInstance<LevelConfig>();
            l2.name = "Level_02";
            l2.displayName = "星区 02";
            l2.subtitle = "SECTOR 02 · 赤潮";
            l2.hasBoss = true;
            l2.waves = new[]
            {
                NewWaveMix(Mix(EnemyKind.Rusher, EnemyKind.Rusher, EnemyKind.Meteor), 7, 0.42f, 1.0f),
                NewWaveMix(Mix(EnemyKind.Turret, EnemyKind.Rusher, EnemyKind.Tracker), 6, 1.0f, 1.0f),
                NewWaveMix(Mix(EnemyKind.Tracker, EnemyKind.Rusher, EnemyKind.Rusher), 6, 0.70f, 1.0f),
                NewWaveMix(Mix(EnemyKind.Meteor, EnemyKind.Turret, EnemyKind.Rusher), 6, 1.0f, 0.9f),
                NewWaveMix(Mix(EnemyKind.Turret, EnemyKind.Tracker, EnemyKind.Rusher), 6, 0.80f, 0.9f),
                NewWaveMix(Mix(EnemyKind.Rusher, EnemyKind.Tracker, EnemyKind.Meteor, EnemyKind.Turret),
                    8, 0.45f, 0.9f)
            };

            return new[] { l1, l2 };
        }

        private static EnemyKind[] Mix(params EnemyKind[] kinds)
        {
            return kinds;
        }

        private static WaveDefinition NewWave(EnemyKind kind, int count, float interval, float delay)
        {
            WaveDefinition w = new WaveDefinition();
            w.kind = kind;
            w.count = count;
            w.interval = interval;
            w.startDelay = delay;
            return w;
        }

        /// <summary>混编波次：按 kinds 轮转出兵</summary>
        private static WaveDefinition NewWaveMix(EnemyKind[] kinds, int count, float interval, float delay)
        {
            WaveDefinition w = NewWave(kinds != null && kinds.Length > 0 ? kinds[0] : EnemyKind.Rusher,
                count, interval, delay);
            w.kinds = kinds != null ? kinds : new EnemyKind[0];
            return w;
        }

        // ------------------------------------------------------------ Boss

        public static BossConfig Boss()
        {
            BossConfig b = ScriptableObject.CreateInstance<BossConfig>();
            b.name = "Boss_Warden";
            b.displayName = "守卫者 · WARDEN-01";
            b.maxHp = 1100f;
            b.phase2Threshold = 0.66f;
            b.phase3Threshold = 0.33f;
            b.bulletSpeed = 7f;
            b.bulletDamage = 12f;
            b.fanCount = 5;
            b.fanAngle = 18f;
            b.tint = new Color(0.9f, 0.25f, 0.95f, 1f);
            b.score = 2000;
            return b;
        }
    }
}
