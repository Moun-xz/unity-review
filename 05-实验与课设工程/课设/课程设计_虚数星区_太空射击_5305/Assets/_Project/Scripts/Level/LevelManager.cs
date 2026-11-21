using System.Collections;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 关卡与波次管理器。
    ///
    /// 一关 = 若干波敌人 + （可选）一个 Boss。
    /// 清场判定靠 aliveCount：每生成一只 +1，敌人离场（被打死或飞出屏幕）时 -1。
    /// 之所以不靠「当前场上对象数量」来判断，是因为对象池里躺着的对象也是 EnemyBase，
    /// 数对象会把池里的空闲实例一起算进去。
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        private LevelConfig[] levels;
        private BossConfig bossConfig;
        private int aliveCount;
        private BossController boss;

        public int CurrentLevelIndex { get; private set; }
        public int AliveCount { get { return aliveCount; } }

        private void Awake()
        {
            Instance = this;
        }

        public void Setup(LevelConfig[] configs, BossConfig boss)
        {
            levels = configs;
            bossConfig = boss;
        }

        private void OnEnable()
        {
            EventBus.EnemyKilled += OnEnemyLeftField;
        }

        private void OnDisable()
        {
            EventBus.EnemyKilled -= OnEnemyLeftField;
        }

        private void OnEnemyLeftField(EnemyBase enemy)
        {
            if (aliveCount > 0) aliveCount--;
        }

        /// <summary>Boss 召唤的小怪也要计入存活数，否则波次会在小怪还活着时就判定清空</summary>
        public void RegisterSummonedEnemy()
        {
            aliveCount++;
        }

        // ------------------------------------------------------------ 流程

        /// <summary>当前这条关卡流水线。只认这一个句柄，不再用 StopAllCoroutines 一把扫。</summary>
        private Coroutine gameLoop;

        /// <summary>
        /// 停掉当前流水线。
        ///
        /// 之前用的是 StopAllCoroutines：它把这个组件上**所有**协程一起杀掉。
        /// 现在 LevelManager 上只有关卡协程，所以没出过问题；
        /// 但以后只要加一个倒计时、环境音之类的协程，就会在这里被静默误杀，
        /// 而且没有任何报错，查起来很费劲。
        /// </summary>
        private void StopLoop()
        {
            if (gameLoop == null) return;
            StopCoroutine(gameLoop);
            gameLoop = null;
        }

        /// <summary>从第一关开始，跑完整条线（正常流程）</summary>
        public void StartGame()
        {
            StartGame(0, false);
        }

        /// <summary>
        /// 从指定的关卡开始。
        ///
        /// fromLevel 是关卡下标；bossOnly 为 true 时跳过这一关的全部波次、直接进 Boss。
        /// 选关面板就是靠这两个参数把"从哪一关进"交出去的——
        /// 不这么做的话，想演示 Boss 的三阶段演出就得先把前面十几波打完。
        ///
        /// 注意只改了**起点**，后面的关卡照常往下串：
        /// 从星区 01 进还是打两关 + Boss，从星区 02 进就是「星区 02 + Boss」。
        /// </summary>
        public void StartGame(int fromLevel, bool bossOnly)
        {
            StopLoop();
            ClearField();
            if (Refs.Announcement != null) Refs.Announcement.Clear();

            aliveCount = 0;
            boss = null;

            int lastIndex = Mathf.Max(0, (levels != null ? levels.Length : 0) - 1);
            CurrentLevelIndex = Mathf.Clamp(fromLevel, 0, lastIndex);

            // 归零进度条：不然第二局开局会先显示上一局的满格
            EventBus.RaiseLevelProgress(0, 0);

            if (bossOnly) gameLoop = StartCoroutine(RunBossOnly());
            else gameLoop = StartCoroutine(RunAllLevels(CurrentLevelIndex));
        }

        public void StopGame()
        {
            StopLoop();
            ClearField();
            aliveCount = 0;

            // 公告是独立于 GameState 的，返回菜单 / 重开时必须手动收，
            // 否则「WARNING」会跟着回到主菜单
            if (Refs.Announcement != null) Refs.Announcement.Clear();
        }

        private IEnumerator RunAllLevels(int from)
        {
            for (int i = from; i < levels.Length; i++)
            {
                CurrentLevelIndex = i;
                LevelConfig config = levels[i];

                EventBus.RaiseLevel(config.displayName);

                // 换关同时换星区底图（交叉淡入，不会闪黑）
                if (Refs.Starfield != null) Refs.Starfield.SetSector(i);

                // 开场横幅：淡入 0.26 + 保持 1.15 + 淡出 0.42 ≈ 1.83 秒，
                // 这里等 2.0 秒留 0.2 秒空档，免得第一波敌人和字一起冒出来
                if (Refs.Announcement != null)
                    Refs.Announcement.ShowLevel(config.displayName, config.subtitle);

                yield return new WaitForSeconds(2.0f);

                for (int w = 0; w < config.waves.Length; w++)
                {
                    // 波次一开始就报「正在打第 w+1 波」，玩家看到数字往前走就知道离换星区还有多远。
                    // 不报「已完成 w」是因为第一波会显示 0/6，看着像还没开始。
                    EventBus.RaiseLevelProgress(w + 1, config.waves.Length);

                    // 直接 yield 子协程，不用 StartCoroutine 包一层：
                    // 包了之后子协程是独立对象，父协程被 StopCoroutine 停掉时它不会跟着停，
                    // 人已经回主菜单了，出兵协程还在后台一波波刷。
                    yield return RunWave(config.waves[w]);
                }

                while (aliveCount > 0) yield return null;

                if (config.hasBoss)
                {
                    // 波次清完 = 星区这条进度走满，接下来是 Boss。
                    // 满格和 Boss 血条同时出现，玩家能读出「小怪打完了，剩最后一个」
                    EventBus.RaiseLevelProgress(config.waves.Length, config.waves.Length);
                    yield return RunBoss();
                }

                // 只有不是最后一关才需要过渡停顿
                if (i < levels.Length - 1) yield return new WaitForSeconds(2.4f);
            }

            if (GameManager.Instance != null) GameManager.Instance.Victory();
        }

        /// <summary>
        /// 直接进 Boss 战：跳过波次，只演 Boss 和它的三阶段。
        ///
        /// 用的是最后一关的场景（`RunBoss` 自己会把星区底图换成 Boss 那一套），
        /// 所以 HUD 上的星区名沿用最后一关，进度条直接给满格——
        /// 这一局本来就只有一场仗，进度条空着反而像"还有七波没打"。
        /// 开场横幅不报星区名，直接由 `RunBoss` 报 Boss 的警告，
        /// 少一次"星区 02"的横幅，节奏就对了。
        /// </summary>
        private IEnumerator RunBossOnly()
        {
            LevelConfig last = levels != null && levels.Length > 0 ? levels[levels.Length - 1] : null;

            CurrentLevelIndex = levels != null ? Mathf.Max(0, levels.Length - 1) : 0;
            EventBus.RaiseLevel(last != null ? last.displayName : "最终星区");
            EventBus.RaiseLevelProgress(1, 1);

            yield return RunBoss();

            if (GameManager.Instance != null) GameManager.Instance.Victory();
        }

        private IEnumerator RunWave(WaveDefinition wave)
        {
            yield return new WaitForSeconds(wave.startDelay);

            for (int i = 0; i < wave.count; i++)
            {
                // 混编波次按 kinds 轮转出兵：同一波里冲锋、炮台、导弹交替出现，
                // 屏幕上不会出现"一整排一模一样的敌机",也因为威胁性质不同而必须换打法。
                SpawnEnemy(wave.KindAt(i));
                if (i < wave.count - 1) yield return new WaitForSeconds(wave.interval);
            }

            while (aliveCount > 0) yield return null;
        }

        private IEnumerator RunBoss()
        {
            // 进 Boss 前 aliveCount 已经归零（RunAllLevels 里等过），
            // 所以演出期间场上没有小怪，慢镜只影响观感、不影响公平性。
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBossWarn();

            // Boss 战换一套更凶的底图，配合警报声把气氛压上去
            if (Refs.Starfield != null) Refs.Starfield.SetSector(2, 0.65f);

            // 警报响起的那一下世界慢下来 + 镜头震一下。
            // 这两件事一起做才有「有东西正在压过来」的重量感，少了任何一样都只是屏幕变红而已。
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.5f, 0.85f);
            if (HitStop.Instance != null) HitStop.Instance.Slow(0.42f, 0.5f);

            if (Refs.Announcement != null)
                Refs.Announcement.ShowBossWarning(bossConfig != null ? bossConfig.displayName : null);

            // 慢镜段必须用 realtime：此刻 timeScale 是 0.42，
            // WaitForSeconds 会被拖成一倍多的时长，警告还没闪完 Boss 就上来了
            yield return new WaitForSecondsRealtime(0.5f);

            // 等慢镜真的恢复再开始按常规时间计时（HitStop 的协程可能比这里晚一帧）。
            // 问的是 TimeFlow「还有没有慢镜」，不再直接读 timeScale ——
            // 暂停时 timeScale 也是 0，直接读会把「玩家暂停」误判成「慢镜还没结束」。
            while (TimeFlow.IsSlowed
                   && GameManager.Instance != null
                   && GameManager.Instance.State == GameState.Playing)
            {
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);

            Vector3 spawnPosition = new Vector3(0f, GameArea.HalfHeight + 2.4f, 0f);
            GameObject go = PoolManager.Instance.Spawn(PoolKeys.Boss, spawnPosition, Quaternion.identity);
            boss = go != null ? go.GetComponent<BossController>() : null;

            while (boss != null && boss.IsAlive) yield return null;

            boss = null;
            yield return new WaitForSeconds(1.6f);
        }

        // ------------------------------------------------------------ 工具

        private void SpawnEnemy(EnemyKind kind)
        {
            if (PoolManager.Instance == null) return;

            Vector3 position = GameArea.RandomSpawnPoint(1.3f);
            PoolManager.Instance.Spawn(PoolKeys.EnemyOf(kind), position, Quaternion.identity);
            aliveCount++;
        }

        /// <summary>重开一局时把场上所有敌人收回池，避免旧敌人残留</summary>
        private void ClearField()
        {
            EnemyBase[] enemies = FindObjectsOfType<EnemyBase>();
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null) enemies[i].DespawnImmediately();
            }

            if (boss != null && boss.IsAlive)
            {
                PoolManager.Instance.Despawn(PoolKeys.Boss, boss.gameObject);
            }
            boss = null;
        }
    }
}
