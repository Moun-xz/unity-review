using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 游戏总装配器：场景里唯一需要手动挂的脚本。
    ///
    /// 它在 Awake 里按固定顺序把整个游戏拼出来：
    /// 摄像机 → 四大管理器 → 玩家 → 对象池 → 背景 → 界面 → 关卡 → 状态机启动。
    /// 顺序不能乱：对象池必须先把工厂注册好，关卡才能生成敌人；
    /// 界面必须先生成，GameManager 才能拿到面板引用并切到主菜单状态。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [Header("画面")]
        [Tooltip("正交摄像机半高，决定可视范围（世界单位）")]
        public float orthographicSize = 5.6f;

        [Header("背景")]
        public int starCount = 170;

        /// <summary>Boss 配置：CreatePools 里建、CreateLevelManager 里用来播警告横幅上的名字</summary>
        private BossConfig bossConfig;

        /// <summary>
        /// 关卡配置：只建**一份**，同时交给 LevelManager 和选关面板。
        ///
        /// 两边各调一次 GameConfig.Levels() 也能跑，但那样会出现两份可以各自修改的副本——
        /// 面板上写着「6 波」、真正跑起来 7 波，这种不一致排查起来很花时间。
        /// </summary>
        private LevelConfig[] levelConfigs;

        private void Awake()
        {
            Instance = this;

            // 静态事件在两个协程之间不会自动清空，必须在创建任何订阅者之前重置
            EventBus.Clear();

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            Physics2D.gravity = Vector2.zero;

            SetupCamera();
            CreateManagers();

            // 机库数据要在造玩家之前准备好——CreatePlayer 会读 PlayerLoadout.Selected
            PlayerLoadout.Setup(GameConfig.Ships());

            WeaponConfig laser = GameConfig.LaserWeapon();
            WeaponConfig spread = GameConfig.SpreadWeapon();

            CreatePlayer(laser, spread);
            CreatePools(laser, spread);
            CreateEnvironment();

            levelConfigs = GameConfig.Levels();

            HUDPanel hud;
            MenuPanel menu;
            PausePanel pause;
            ResultPanel result;
            SettingsPanel settings;
            CreateUI(out hud, out menu, out pause, out result, out settings);

            LevelManager level = CreateLevelManager(levelConfigs);

            GameManager gameManager = GameManager.Instance;
            gameManager.Initialize(
                hud, menu, pause, result,
                Refs.Player.GetComponent<PlayerHealth>(),
                Refs.Player.GetComponent<PlayerShooter>(),
                level);

            menu.UpdateBest(gameManager.HighScore);
        }

        // ------------------------------------------------------------ 摄像机

        private void SetupCamera()
        {
            Camera cam = Camera.main;

            if (cam == null)
            {
                GameObject camObject = new GameObject("Main Camera");
                camObject.tag = "MainCamera";
                cam = camObject.AddComponent<Camera>();
                camObject.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.02f, 0.045f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;

            Refs.Camera = cam;
            GameArea.Refresh(cam);

            if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();
            if (CameraShake.Instance != null) CameraShake.Instance.CaptureBasePosition();
        }

        // ------------------------------------------------------------ 管理器

        private void CreateManagers()
        {
            GameObject managers = new GameObject("_Managers");

            managers.AddComponent<PoolManager>();
            managers.AddComponent<AudioManager>();
            managers.AddComponent<GameManager>();

            GameObject fxRoot = new GameObject("FX");
            fxRoot.transform.SetParent(managers.transform, false);
            fxRoot.AddComponent<FXManager>();
            fxRoot.AddComponent<HitStop>();
        }

        // ------------------------------------------------------------ 玩家

        private void CreatePlayer(WeaponConfig laser, WeaponConfig spread)
        {
            GameObject player = ObjectFactory.CreatePlayer(PlayerLoadout.Selected);
            player.transform.position = new Vector3(0f, -GameArea.HalfHeight * 0.55f, 0f);

            PlayerShooter shooter = player.GetComponent<PlayerShooter>();
            if (shooter != null) shooter.Setup(new[] { laser, spread });

            Refs.Player = player.transform;
        }

        // ------------------------------------------------------------ 对象池

        private void CreatePools(WeaponConfig laser, WeaponConfig spread)
        {
            PoolManager pool = PoolManager.Instance;

            // 子弹：预热数量按"同屏可能出现的最大数量"估
            pool.Register(PoolKeys.LaserBullet, delegate { return ObjectFactory.CreatePlayerBullet(PoolKeys.LaserBullet, laser); }, 60);
            pool.Register(PoolKeys.SpreadBullet, delegate { return ObjectFactory.CreatePlayerBullet(PoolKeys.SpreadBullet, spread); }, 50);
            pool.Register(PoolKeys.EnemyBullet, ObjectFactory.CreateEnemyBullet, 70);

            // 敌人：四种各预热一批。陨石按同屏 8 颗算——它是环境障碍，会比敌机多一点
            EnemyConfig rusher = GameConfig.Rusher();
            EnemyConfig turret = GameConfig.Turret();
            EnemyConfig tracker = GameConfig.Tracker();
            EnemyConfig meteor = GameConfig.Meteor();

            pool.Register(PoolKeys.EnemyRusher, delegate { return ObjectFactory.CreateEnemy(EnemyKind.Rusher, rusher); }, 10);
            pool.Register(PoolKeys.EnemyTurret, delegate { return ObjectFactory.CreateEnemy(EnemyKind.Turret, turret); }, 8);
            pool.Register(PoolKeys.EnemyTracker, delegate { return ObjectFactory.CreateEnemy(EnemyKind.Tracker, tracker); }, 8);
            pool.Register(PoolKeys.EnemyMeteor, delegate { return ObjectFactory.CreateEnemy(EnemyKind.Meteor, meteor); }, 8);

            // Boss 只可能同时存在一个
            bossConfig = GameConfig.Boss();
            pool.Register(PoolKeys.Boss, delegate { return ObjectFactory.CreateBoss(bossConfig); }, 1);
        }

        // ------------------------------------------------------------ 背景

        private void CreateEnvironment()
        {
            GameObject environment = new GameObject("Environment");
            Starfield starfield = environment.AddComponent<Starfield>();
            starfield.Build(starCount);

            Refs.Starfield = starfield;
        }

        // ------------------------------------------------------------ 界面

        private void CreateUI(out HUDPanel hud, out MenuPanel menu, out PausePanel pause,
            out ResultPanel result, out SettingsPanel settings)
        {
            Canvas canvas = UIFactory.CreateCanvas("UI", null);
            UIFactory.EnsureEventSystem(canvas.transform);

            // 暗角 / 扫描线放在最前面建：UGUI 同层级的先后就是绘制顺序，
            // 它是「压在画面上、但压在 HUD 下面」的一层，所以必须排在 HUD 之前。
            ScreenFrame screenFrame = CreatePanel<ScreenFrame>(canvas.transform, "ScreenFrame");
            screenFrame.Build();

            // 边界警戒带也在 HUD 之下：它是环境提示，不该盖住血量数字
            BoundaryWarnings boundary = CreatePanel<BoundaryWarnings>(canvas.transform, "BoundaryWarnings");
            boundary.Build();

            // 受击红闪同理：压在画面上、垫在 HUD 下面。
            // 罩在 HUD 之上会把血量数字染成一片红，而那正是玩家最需要读数的时候。
            HurtFlash hurtFlash = CreatePanel<HurtFlash>(canvas.transform, "HurtFlash");
            hurtFlash.Build();

            hud = CreatePanel<HUDPanel>(canvas.transform, "HUD");
            hud.Build();

            // 公告横幅压在 HUD 之上（它是战斗中的视觉焦点），
            // 但排在主菜单 / 结算面板之前 —— 回到菜单或结算时必须让它们盖住公告。
            Announcement announcement = CreatePanel<Announcement>(canvas.transform, "Announcement");
            announcement.Build();
            Refs.Announcement = announcement;

            menu = CreatePanel<MenuPanel>(canvas.transform, "MenuPanel");
            menu.Build(GameManager.Instance.HighScore);

            // 子面板都建在菜单之后：后建的盖在上面
            settings = CreatePanel<SettingsPanel>(canvas.transform, "SettingsPanel");
            settings.Build();

            // 说明面板和设置 / 机库同级，都盖在主菜单之上
            HelpPanel help = CreatePanel<HelpPanel>(canvas.transform, "HelpPanel");
            help.Build();

            // 敌人图鉴：主菜单第五个按钮打开的那一页，同样盖在主菜单之上
            EnemyCodexPanel codex = CreatePanel<EnemyCodexPanel>(canvas.transform, "EnemyCodexPanel");
            codex.Build();

            // 选关面板：进战斗的唯一入口，所以它也盖在主菜单之上，
            // 并且和主菜单共用同一份关卡配置（levelConfigs）
            LevelSelectPanel levelSelect = CreatePanel<LevelSelectPanel>(canvas.transform, "LevelSelectPanel");
            levelSelect.Build(levelConfigs, bossConfig);

            menu.BindSubPanels(settings, help, levelSelect, codex);

            pause = CreatePanel<PausePanel>(canvas.transform, "PausePanel");
            pause.Build();

            result = CreatePanel<ResultPanel>(canvas.transform, "ResultPanel");
            result.Build();

            // 标题屏压在主菜单之上（后建 = 画在上面）。
            // 它是开机第一屏：一张关键帧 + 一个「进入游戏」，玩家点进去才看到主菜单。
            TitleScreen title = CreatePanel<TitleScreen>(canvas.transform, "TitleScreen");
            title.Build();

            // 所有面板在 Build 里就是隐藏的，这里不用再关一次
            // （重复调用会让它们走一次多余的淡出，虽然看不见但没必要）

            // 开机流程分三段，每一段的"交接点"都藏在一次全覆盖里：
            //
            //   BootSplash（黑幕）──散尽──▶ 标题屏 ──跃迁白光──▶ 主菜单
            //
            // 1) 主菜单从第一帧起就"显示"了，但出场序列被压住不放（IntroDeferred）。
            //    不压的话这一秒的出场会在黑幕后面白播一遍，等黑幕撤掉只剩个终态。
            // 2) 标题屏这时候已经在黑幕后面了，但它的内容还是起点状态；
            //    等黑幕散尽再 PlayIntro，两段不重叠——重叠的话片头字标（y=62）
            //    会和标题屏的字标（y=232）隔着半透明的黑叠在一起。
            // 3) 点「进入游戏」后播跃迁，白光打到峰值那一帧才收标题屏、放主菜单。
            //    差一帧就会看到标题消失时的空洞。
            //
            // 输入全程锁着（InputLocked）：这一路玩家很可能在按回车，
            // 不锁的话黑幕或白光里那一下会直接开一局，散开看到的是已经在跑的关卡。
            // 菜单出场落定之后才解锁。
            //
            // menuRef 是必要的：menu 是方法的 out 参数，out 参数进不了闭包（CS1628）
            MenuPanel menuRef = menu;
            menuRef.IntroDeferred = true;
            GameManager.Instance.InputLocked = true;

            title.EnterRequested = delegate
            {
                WarpTransition.Play(canvas.transform,
                    delegate
                    {
                        title.Hide();
                        menuRef.ResumeIntro();
                    },
                    delegate
                    {
                        GameManager.Instance.InputLocked = false;
                    });
            };

            BootSplash.Create(canvas.transform, delegate { title.PlayIntro(); });
        }

        private static T CreatePanel<T>(Transform parent, string name) where T : Component
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Stretch(go.GetComponent<RectTransform>());
            return go.AddComponent<T>();
        }

        // ------------------------------------------------------------ 关卡

        private LevelManager CreateLevelManager(LevelConfig[] configs)
        {
            GameObject go = new GameObject("LevelManager");
            LevelManager level = go.AddComponent<LevelManager>();
            level.Setup(configs, bossConfig);
            return level;
        }
    }
}
