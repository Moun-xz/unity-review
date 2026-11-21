using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 运行时对象工厂：把「拼装一个游戏对象」的代码集中到一处。
    ///
    /// 这个项目没有用 Prefab 资产，所有对象都是这里用 AddComponent 拼出来的。
    /// 好处是项目零资源依赖、打开就能跑；代价是 Inspector 里看不到可视化拼装。
    /// 如果后续要改成 Prefab 工作流，把这里的每个方法替换成 Instantiate(prefab) 即可，
    /// 上层逻辑（玩家、子弹、敌人）完全不需要改动。
    /// </summary>
    public static class ObjectFactory
    {
        // ------------------------------------------------------------ 玩家

        public static GameObject CreatePlayer(ShipConfig ship)
        {
            if (ship == null) ship = GameConfig.Ships()[0];

            GameObject go = new GameObject("Player");

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            // 素材缺失时退回程序化战机——不会出现一架隐形的飞机
            sr.sprite = ShipArt.Get(ship);
            sr.sortingOrder = 10;

            // 舰体背光：深色金属机身压在同样深的星空上会"糊"掉，垫一层很淡的机翼色光晕
            // 把剪影托起来。它比机身低一个层级，只在边缘露出来。
            SpriteRenderer hullGlow = AddHullGlow(go.transform, ship.accent);

            // 尾焰：三层柔光（核心 / 主焰 / 外层晕），长度随速度拉伸。
            // 坐标是贴图单位（ShipArt 的 HAMMER 喷口在 ±0.486, -0.804），子物体会跟 bodyScale 一起放大。
            ThrusterFlame flame = ThrusterFlame.Create(go.transform, ship.accent);

            // 翼尖航行灯：左右不同色不同相，慢慢闪；尾灯白冷色，快速双闪。
            // 坐标同样按 0.5 换算：翼尖 u=±0.80/v=-0.03 → 世界偏移 ±0.40/-0.015；尾灯在 u=0/v=-0.90
            NavLight.Create(go.transform, "NavLightL", new Vector2(-0.400f, -0.015f),
                new Color(0.55f, 0.98f, 1f, 1f), 2.6f, 0.00f, 0.055f);
            NavLight.Create(go.transform, "NavLightR", new Vector2(0.400f, -0.015f),
                new Color(1f, 0.72f, 0.30f, 1f), 2.6f, 0.46f, 0.055f);
            NavLight.Create(go.transform, "NavLightTail", new Vector2(0f, -0.450f),
                new Color(0.86f, 0.95f, 1f, 1f), 1.3f, 0.15f, 0.045f);

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 判定半径比机身外形小一圈：弹幕游戏里「小判定」是惯例，玩家会感谢这一点
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;

            PlayerController controller = go.AddComponent<PlayerController>();
            PlayerHealth health = go.AddComponent<PlayerHealth>();
            PlayerShooter shooter = go.AddComponent<PlayerShooter>();

            // 具体数值由 PlayerRig 统一刷一遍：开局前玩家在机库里换船，
            // 也是走同一个入口重新 Apply，不会出现「工厂里一套、换船时另一套」的双份逻辑。
            PlayerRig rig = go.AddComponent<PlayerRig>();
            rig.Bind(sr, null, controller, health, shooter, hullGlow, flame);
            rig.Apply(ship);

            return go;
        }

        /// <summary>
        /// 舰体背光是一团压在机身下面的染色柔光：让深色剪影从深色背景里浮出来，
        /// 同时把每架船的强调色（青 / 绿 / 琥珀 / 品红）带到游戏画面里。
        /// </summary>
        private static SpriteRenderer AddHullGlow(Transform parent, Color accent)
        {
            GameObject glow = new GameObject("HullGlow");
            glow.transform.SetParent(parent, false);
            glow.transform.localPosition = new Vector3(0f, -0.02f, 0.05f);
            glow.transform.localScale = new Vector3(1.55f, 1.30f, 1f);

            SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Glow();
            sr.color = new Color(accent.r, accent.g, accent.b, 0.16f);
            sr.sortingOrder = 9;
            return sr;
        }

        // ------------------------------------------------------------ 子弹

        public static GameObject CreatePlayerBullet(string poolKey, WeaponConfig weapon)
        {
            GameObject go = new GameObject(poolKey);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = weapon.kind == WeaponKind.Laser
                ? ArtLibrary.Get("bullet_laser", SpriteFactory.Bullet(weapon.bulletColor))
                : ArtLibrary.Get("bullet_spread", SpriteFactory.BulletStream(weapon.bulletColor));
            sr.sortingOrder = 6;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            // 半径是局部值，运行时会再乘 bulletScale，所以这里要按「贴图半宽 / 缩放」倒推。
            // 激光 14px 宽 ×1.5 = 屏幕上 20px → 半宽 10px = 0.104 单位，0.07×1.5 正好；
            // 机炮弹 30×68 px 的贴图 ×0.5 → 屏幕上 15×34，半宽 7.5px = 0.078 单位，
            // 判定给到 0.17×0.5 = 0.085（约 8 像素），比外形略宽：
            // 一秒十几发的武器，不该让玩家觉得"明明扫过去了却没掉血"。
            col.radius = weapon.kind == WeaponKind.Laser ? 0.07f : 0.17f;

            BulletBase bullet;
            if (weapon.kind == WeaponKind.Laser) bullet = go.AddComponent<LaserBullet>();
            else bullet = go.AddComponent<SpreadBullet>();

            bullet.PoolKey = poolKey;
            // 命中火花跟着武器色走：双轨是金、光束是青，一眼能分出正在用哪把
            bullet.SetHitColor(weapon.bulletColor);
            go.transform.localScale = Vector3.one * weapon.bulletScale;
            return go;
        }

        public static GameObject CreateEnemyBullet()
        {
            GameObject go = new GameObject(PoolKeys.EnemyBullet);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ArtLibrary.Get("bullet_enemy", SpriteFactory.EnemyOrb(new Color(1f, 0.26f, 0.54f, 1f)));
            sr.sortingOrder = 7;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.2f;

            EnemyBullet bullet = go.AddComponent<EnemyBullet>();
            bullet.PoolKey = PoolKeys.EnemyBullet;
            bullet.SetHitColor(new Color(1f, 0.42f, 0.55f, 1f));
            go.transform.localScale = Vector3.one * 0.62f;
            return go;
        }

        // ------------------------------------------------------------ 敌人

        public static GameObject CreateEnemy(EnemyKind kind, EnemyConfig config)
        {
            string key = PoolKeys.EnemyOf(kind);
            GameObject go = new GameObject(key);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            if (kind == EnemyKind.Meteor)
            {
                // 陨石不用 png，也不该用同一张图：池里每个实例建的时候就随机一颗，
                // 8 个实例就是 8 种轮廓，再乘上出生时随机转的角度，同屏不会撞脸。
                sr.sprite = SpriteFactory.Meteor(Random.Range(0, 8));
                sr.sortingOrder = 8;
            }
            else
            {
                sr.sprite = ArtLibrary.Get(ArtLibrary.EnemySpriteName(kind), SpriteFactory.Enemy(kind, config.tint));
                sr.sortingOrder = 8;
            }

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            // 陨石的贴图里岩石只占图宽的 62%，判定要按这个收，
            // 否则看起来还隔着一截就掉血。其它敌人是满幅的机体，保持 0.42。
            //
            // 追踪导弹例外：贴图 112×320（PPU 200 → 世界 0.56 × 1.60），
            // 但实测轮廓只有 0.28 宽（±0.14）、翼尖撑到 ±0.25、全长 1.35。
            // 沿用 0.42 的话判定比机体宽出两倍多，子弹从旁边飞过去也算命中。
            // 收到 0.22：包住弹体核心、够到一半翼展，又不会在弹侧留一圈空气判定。
            if (kind == EnemyKind.Meteor) col.radius = 0.30f;
            else if (kind == EnemyKind.Tracker) col.radius = 0.22f;
            else col.radius = 0.42f;

            EnemyBase enemy;
            if (kind == EnemyKind.Turret) enemy = go.AddComponent<TurretShooter>();
            else if (kind == EnemyKind.Tracker) enemy = go.AddComponent<Tracker>();
            else if (kind == EnemyKind.Meteor) enemy = go.AddComponent<Meteor>();
            else enemy = go.AddComponent<StraightRusher>();

            enemy.Setup(config);
            return go;
        }

        // ------------------------------------------------------------ Boss

        public static GameObject CreateBoss(BossConfig config)
        {
            GameObject go = new GameObject(PoolKeys.Boss);

            // 背后的辉光：Boss 得在满屏弹幕里一眼被认出来，单靠轮廓不够
            GameObject halo = new GameObject("BossHalo");
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = Vector3.one * 1.2f;
            SpriteRenderer haloSr = halo.AddComponent<SpriteRenderer>();
            haloSr.sprite = SpriteFactory.Glow();
            haloSr.color = new Color(0.92f, 0.28f, 0.95f, 0.38f);
            haloSr.sortingOrder = 7;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ArtLibrary.Get("enemy_boss", SpriteFactory.Boss(config.tint));
            sr.sortingOrder = 8;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            // 注意：BossController 会把根节点放大到 2.6 倍，碰撞体半径也会跟着 ×2.6。
            // 所以这里必须填「局部」半径：0.42 × 2.6 ≈ 1.09 世界单位，正好贴合 2.18 宽的外形。
            // 之前填 1.1 会让实际判定直径达到 5.7 世界单位，玩家离得老远就掉血。
            col.radius = 0.42f;

            BossController boss = go.AddComponent<BossController>();
            boss.Setup(config);
            return go;
        }

        // ------------------------------------------------------------ 背景元素

        public static GameObject CreateStar(Color color, float scale, int sortingOrder)
        {
            GameObject go = new GameObject("Star");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Star();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }
    }
}
