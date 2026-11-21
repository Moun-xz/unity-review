using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 全局游戏管理器（全局四单例之一）。
    ///
    /// 职责：维护 GameState 状态机、得分与连击、最高分存档，并在状态切换时
    /// 统一决定「哪些界面该显示」。
    /// 所有面板都不自己决定显隐，避免出现「暂停面板和结算面板同时亮着」这类问题。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int HighScore { get; private set; }

        // 由 GameBootstrap 在构建完场景后注入，避免各处 FindObjectOfType
        public HUDPanel Hud;
        public MenuPanel Menu;
        public PausePanel PauseMenu;
        public ResultPanel Result;
        public PlayerHealth PlayerHealthRef;
        public PlayerShooter PlayerShooterRef;
        public PlayerRig PlayerRigRef;
        public LevelManager Level;

        private const float ComboWindow = 3f;
        private const string HighScoreKey = "dss_highscore";

        private float comboTimer;

        /// <summary>
        /// 开机流程还没走完的时候先不收键盘。
        ///
        /// 从启动到主菜单出场落定，要经过黑幕（BootSplash）→ 标题屏 → 跃迁白光
        /// → 主菜单出场这四段。这期间玩家很可能在不停敲回车：
        /// 敲黑幕上是"跳过"，敲标题屏上是"进入游戏"，敲到主菜单上才是"开局"。
        /// 不挡的话，前两下里的任意一下都会顺手开一局，散场时看到的是已经在跑的关卡。
        ///
        /// 由 GameBootstrap 在启动时置 true，WarpTransition 播完后置 false。
        /// </summary>
        public bool InputLocked { get; set; }

        private void Awake()
        {
            Instance = this;
            State = GameState.Boot;
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        }

        public void Initialize(HUDPanel hud, MenuPanel menu, PausePanel pauseMenu, ResultPanel result,
                               PlayerHealth playerHealth, PlayerShooter playerShooter, LevelManager level)
        {
            Hud = hud;
            Menu = menu;
            PauseMenu = pauseMenu;
            Result = result;
            PlayerHealthRef = playerHealth;
            PlayerShooterRef = playerShooter;
            PlayerRigRef = playerHealth != null ? playerHealth.GetComponent<PlayerRig>() : null;
            Level = level;

            ChangeState(GameState.MainMenu);
        }

        private void Update()
        {
            TickCombo();

            switch (State)
            {
                case GameState.MainMenu:
                    // 开机流程还没走完：这一下按键是"跳过"或"进入游戏"，不是"开局"
                    if (InputLocked) break;
                    // 设置 / 机库 / 选关 / 说明开着的时候回车不该直接开局，
                    // 否则会出现「面板还挂在屏幕上、游戏已经在跑了」的错位
                    if (Menu != null && Menu.SubPanelOpen) break;
                    // 回车和点按钮走同一条路：现在进战斗要先过选关面板，
                    // 这里如果还直接 StartGame()，就会出现"用鼠标要点两下、用键盘一下就到星区 01"
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        if (Menu != null) Menu.RequestStart();
                    }
                    break;

                case GameState.Playing:
                    if (Input.GetKeyDown(KeyCode.Escape)) ChangeState(GameState.Paused);
                    break;

                case GameState.Paused:
                    if (Input.GetKeyDown(KeyCode.Escape)) ChangeState(GameState.Playing);
                    break;

                case GameState.GameOver:
                case GameState.Victory:
                    if (Input.GetKeyDown(KeyCode.R)) StartGame();
                    else if (Input.GetKeyDown(KeyCode.Escape)) ReturnToMenu();
                    break;
            }
        }

        /// <summary>
        /// 切出窗口（Alt+Tab、点浏览器、点编辑器 Inspector）时自动暂停。
        /// 射击游戏里最扫兴的事就是回来发现自己已经被撞死了。
        ///
        /// 只在 Playing 时处理：菜单里切来切去不该弹暂停面板，
        /// 暂停中再失焦也不用重复切（已经在暂停了）。
        /// </summary>
        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            if (State == GameState.Playing) ChangeState(GameState.Paused);
        }

        // ------------------------------------------------------------ 连击与得分

        private void TickCombo()
        {
            if (comboTimer <= 0f) return;

            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f && Combo > 0)
            {
                Combo = 0;
                EventBus.RaiseCombo(0);
            }
        }

        /// <summary>
        /// 击杀结算。连击每层 +10% 分数，上限 20 层（3 倍）。
        /// 3 秒内没有再击杀就断连——这个窗口既是压力也是爽点。
        /// </summary>
        public void RegisterKill(int baseScore)
        {
            Combo++;
            comboTimer = ComboWindow;

            float multiplier = 1f + Mathf.Min(Combo, 20) * 0.1f;
            Score += Mathf.RoundToInt(baseScore * multiplier);

            EventBus.RaiseScore(Score);
            EventBus.RaiseCombo(Combo);
        }

        // ------------------------------------------------------------ 状态切换

        public void ChangeState(GameState next)
        {
            State = next;

            if (Hud != null) Hud.SetVisible(next == GameState.Playing || next == GameState.Paused);
            if (Menu != null) Menu.SetVisible(next == GameState.MainMenu);
            if (PauseMenu != null) PauseMenu.SetVisible(next == GameState.Paused);
            if (Result != null) Result.SetVisible(next == GameState.GameOver || next == GameState.Victory);

            // 主菜单里的战机是 UI 展示位（MenuPanel 的 ShipShowcase）：
            // 面板在 canvas 坐标系、战机在世界坐标系，中间隔着 CanvasScaler 的缩放，
            // 屏幕比例一变两者就对不齐。所以菜单里把世界里的这架收起来，
            // 展示交给 UI，位置和文字的对齐才是确定的。
            if (next == GameState.MainMenu && PlayerHealthRef != null)
            {
                PlayerHealthRef.gameObject.SetActive(false);
            }

            // 结算和回菜单要把慢镜一起清掉：慢镜可能还挂着没到期，
            // 只清暂停的话结算面板会带着 0.42 倍速淡入，看着像卡了。
            if (next == GameState.GameOver || next == GameState.Victory || next == GameState.MainMenu)
            {
                TimeFlow.Reset();
            }
            else
            {
                TimeFlow.SetPaused(next == GameState.Paused);
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PauseBGM(next == GameState.Paused);
            EventBus.RaiseState(next);
        }

        /// <summary>
        /// 这一局是从哪一关进来的。
        ///
        /// 「再来一局」和 R 键要重开**同一个入口**：从星区 02 打死的玩家，
        /// 重开之后应该还在星区 02，而不是被扔回星区 01 重新打一遍。
        /// </summary>
        private int entryLevel;
        private bool entryBossOnly;

        public void StartGame()
        {
            StartRun(entryLevel, entryBossOnly);
        }

        /// <summary>从选关面板指定的关卡进入</summary>
        public void StartGameAt(int fromLevel)
        {
            StartRun(fromLevel, false);
        }

        /// <summary>直接进 Boss 战（选关面板最下面那一条）</summary>
        public void StartBossRush()
        {
            StartRun(0, true);
        }

        private void StartRun(int fromLevel, bool bossOnly)
        {
            entryLevel = Mathf.Max(0, fromLevel);
            entryBossOnly = bossOnly;

            Score = 0;
            Combo = 0;
            comboTimer = 0f;
            EventBus.RaiseScore(0);
            EventBus.RaiseCombo(0);

            if (PlayerHealthRef != null)
            {
                PlayerHealthRef.gameObject.SetActive(true);
                PlayerHealthRef.transform.position = new Vector3(0f, -GameArea.HalfHeight * 0.55f, 0f);
                PlayerHealthRef.transform.rotation = Quaternion.identity;
            }

            // 玩家可能刚在机库里换了船，开局这一下才真正换上：改图、改手感、改血上限。
            // 必须在 ResetHealth 之前——SetMaxHp 会把血量直接拉满。
            if (PlayerRigRef != null) PlayerRigRef.Apply(PlayerLoadout.Selected);

            if (PlayerHealthRef != null) PlayerHealthRef.ResetHealth();
            if (PlayerShooterRef != null) PlayerShooterRef.ResetForGame();

            ChangeState(GameState.Playing);

            if (Level != null) Level.StartGame(entryLevel, entryBossOnly);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM();
                AudioManager.Instance.PlayUi();
            }
        }

        public void PlayerDied()
        {
            if (State != GameState.Playing) return;

            if (Level != null) Level.StopGame();
            LogPoolStats();

            bool record = SaveHighScore();
            ChangeState(GameState.GameOver);
            if (Result != null) Result.Show(false, Score, HighScore, record);
        }

        public void BossDefeated()
        {
            Victory();
        }

        public void Victory()
        {
            if (State == GameState.Victory || State == GameState.GameOver) return;

            if (Level != null) Level.StopGame();
            LogPoolStats();

            bool record = SaveHighScore();
            ChangeState(GameState.Victory);
            if (Result != null) Result.Show(true, Score, HighScore, record);
        }

        public void ReturnToMenu()
        {
            // 慢镜可能还挂着没到期，只清暂停会带着 0.42 进主菜单
            TimeFlow.Reset();

            if (Level != null) Level.StopGame();
            if (PlayerHealthRef != null) PlayerHealthRef.gameObject.SetActive(true);

            // 这一局可能刷了纪录，回到主菜单要显示新值，否则会看到旧的最高分
            if (Menu != null) Menu.UpdateBest(HighScore);

            ChangeState(GameState.MainMenu);
        }

        /// <summary>
        /// 结算时报一次对象池的累计创建数。
        ///
        /// 这个数就是「一局总共 new 了多少个对象」——它是对象池收益的直接证据：
        /// 池里预热了 215 个，跑完一整局如果这个数还是 215，说明全程零额外分配；
        /// 高出来的部分就是池子不够大、中途扩容的次数。
        ///
        /// 之所以放在结算而不是每帧统计，是因为控制台刷屏会拖慢调试节奏，
        /// 一局看一条就够。
        /// </summary>
        private void LogPoolStats()
        {
            if (PoolManager.Instance == null) return;
            Debug.Log("[Pool] 本局累计新建 " + PoolManager.Instance.CreatedCount + " 个对象（预热 215）");
        }

        private bool SaveHighScore()
        {
            if (Score <= HighScore) return false;

            HighScore = Score;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            PlayerPrefs.Save();
            EventBus.RaiseHighScore(HighScore);
            return true;
        }

        /// <summary>
        /// 退出游戏。编辑器里 Application.Quit() 是空操作（只有打包出来的 exe 才生效），
        /// 所以编辑器下改为停止播放，测试「退出游戏」按钮时才不会以为它坏了。
        /// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
