using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 暂停面板：780×470 的左右分栏 Sheet，压在 80% 的 Scrim 上。
    ///
    /// 为什么改成分栏（旧版是 700×500 的居中小方块）：
    ///   旧版纵向只有"已暂停 / 冻结提示 / 两个按钮"三块，Content 区 360 高只用了 230，
    ///   中间还留 98 的空档，读完一句"已暂停"就没有任何信息可看。
    ///   暂停是玩家最想确认"我打到哪、多少分"的一刻，把左边的空白换成四行实时数据，
    ///   右边留给动作——两个按钮不再各占一整行纵向空间，面板还能更矮（500 → 470）。
    ///
    /// 四行数据全部是**只读**的：得分与连击取自 GameManager，关卡与武器来自 EventBus 缓存，
    /// 不新增任何玩法字段，也不改任何游戏状态。
    ///
    /// 标题用等宽 "PAUSED" 而不是再写一遍"已暂停"：页眉已经有中文标题，
    /// 内容区再来一个同名大字是重复信息；中英分工后，一个负责"这是什么屏"，一个负责"现在什么状态"。
    /// </summary>
    public class PausePanel : UIPanelBase
    {
        private const float PanelWidth = 780f;
        private const float PanelHeight = 470f;

        // Content 区 = 面板 - 左右 34 - 上 74 - 下 30 = 712 × 366，坐标原点在中心，
        // 因此 x ∈ [-356, 356]、y ∈ [-183, 183]。下面的所有坐标都是这个中心坐标系，
        // 锚点统一给 Center——**不能用左边缘锚点**，那样 x=-356 会变成"左边缘再往左 356"，
        // 整列文字会飞出面板（这是第一版真实踩过的坑）。
        private const float LeftEdge = -356f;   // 左列文字左缘 = 内容区左边界
        private const float ValueRight = 30f;   // 左列数值右缘
        private const float DividerX = 62f;     // 竖分隔线
        private const float RightCenterX = 212f;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private Text scoreValue;
        private Text comboValue;
        private Text levelValue;
        private Text weaponValue;

        private string levelName = "";
        private string weaponName = "";
        private int waveCurrent;
        private int waveTotal;

        public void Build()
        {
            InitGroup(false);

            UIChrome.Scrim(transform, "Scrim", UITheme.A(UITheme.SurfaceDeep, 0.80f));

            UISheet sheet = UIChrome.Sheet(transform, "Sheet",
                new Vector2(PanelWidth, PanelHeight), Vector2.zero);

            UIChrome.Header(sheet, "05", "已暂停", "SYSTEM HOLD");
            // M 键在 GameManager 里没有实现，这里不写进页脚——留一句做不到的快捷键比不写更糟
            UIChrome.Footer(sheet, "ESC 继续任务　·　或点下方按钮", "PAUSED");

            Transform content = sheet.Content;

            // 内屏那团几乎看不见的径向光：面板不再是均匀一块色，左上略亮
            UIChrome.InnerGlow(content, "InnerGlow", UITheme.Cyan,
                new Vector2(390f, 290f), new Vector2(-140f, 115f), 0.055f);

            BuildStatusBlock(content);
            BuildDataRows(content);
            BuildActions(content);
        }

        // ------------------------------------------------------------ 左列

        private void BuildStatusBlock(Transform content)
        {
            Image bar = UIFactory.CreateImage(content, "AccentBar", UITheme.Cyan);
            UIFactory.Place(bar.gameObject, Center, new Vector2(0f, 0.5f),
                new Vector2(LeftEdge, 132f), new Vector2(4f, 50f));

            Text wordmark = UIChrome.CreateLabel(content, "Wordmark", "PAUSED", 52,
                TextAnchor.MiddleLeft, UITheme.CyanBright,
                Center, new Vector2(0f, 0.5f),
                new Vector2(LeftEdge + 22f, 132f), new Vector2(320f, 56f), true, true);
            // 52px 大字压在深底上会显得"贴"在面板表面，一层 2px 下投影把它托起来
            UIFactory.AddShadow(wordmark, new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -2f));

            UIChrome.CreateLabel(content, "Sub", "战斗时间已冻结 · 随时可以继续", UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextMuted,
                Center, new Vector2(0f, 0.5f),
                new Vector2(LeftEdge + 22f, 82f), new Vector2(376f, 24f), false);

            Image rule = UIFactory.CreateImage(content, "Rule", UITheme.EdgeSoft);
            UIFactory.Place(rule.gameObject, Center, new Vector2(0f, 0.5f),
                new Vector2(LeftEdge, 54f), new Vector2(386f, 1f));
        }

        /// <summary>
        /// 四行"序号 + 标签 + 数值"。
        ///
        /// 这里刻意不抽成 DataRow(...) 方法：抽出去之后每行的 y 就变成方法参数，
        /// _tools/check_ui_layout.py 只能静态解析字面量坐标，参数化的行会被整段漏检
        /// （暂停面板上一版的错位正是这种"工具看不见"的位置）。内联之后每一行的
        /// 三个坐标都摆在同一平面上，改动和检查都是同一个视野里的事。
        /// </summary>
        private void BuildDataRows(Transform content)
        {
            // ---- 01 本局得分 ----
            UIChrome.CreateLabel(content, "ScoreIndex", "01", UITheme.SizeNano,
                TextAnchor.MiddleLeft, UITheme.A(UITheme.TextMuted, 0.85f),
                Center, new Vector2(0f, 0.5f), new Vector2(LeftEdge, 10f), new Vector2(22f, 22f), false, true);
            UIChrome.CreateLabel(content, "ScoreLabel", "本局得分", UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextFaint,
                Center, new Vector2(0f, 0.5f), new Vector2(LeftEdge + 30f, 10f), new Vector2(150f, 22f), false);
            scoreValue = UIChrome.CreateLabel(content, "ScoreValue", "000000", 26,
                TextAnchor.MiddleRight, UITheme.TextMain,
                Center, new Vector2(1f, 0.5f), new Vector2(ValueRight, 10f), new Vector2(200f, 32f), true, true);

            // ---- 02 当前连击 ----
            UIChrome.CreateLabel(content, "ComboIndex", "02", UITheme.SizeNano,
                TextAnchor.MiddleLeft, UITheme.A(UITheme.TextMuted, 0.85f),
                Center, new Vector2(0f, 0.5f), new Vector2(LeftEdge, -36f), new Vector2(22f, 22f), false, true);
            UIChrome.CreateLabel(content, "ComboLabel", "当前连击", UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextFaint,
                Center, new Vector2(0f, 0.5f), new Vector2(LeftEdge + 30f, -36f), new Vector2(150f, 22f), false);
            comboValue = UIChrome.CreateLabel(content, "ComboValue", "×0", 26,
                TextAnchor.MiddleRight, UITheme.TextMain,
                Center, new Vector2(1f, 0.5f), new Vector2(ValueRight, -36f), new Vector2(200f, 32f), true, true);

            // ---- 03 关卡进度 ----
            UIChrome.CreateLabel(content, "LevelIndex", "03", UITheme.SizeNano,
                TextAnchor.MiddleLeft, UITheme.A(UITheme.TextMuted, 0.85f),
                Center, new Vector2(0f, 0.5f), new Vector2(LeftEdge, -82f), new Vector2(22f, 22f), false, true);
            UIChrome.CreateLabel(content, "LevelLabel", "关卡进度", UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextFaint,
                Center, new Vector2(0f, 0.5f), new Vector2(LeftEdge + 30f, -82f), new Vector2(150f, 22f), false);
            levelValue = UIChrome.CreateLabel(content, "LevelValue", "—", 24,
                TextAnchor.MiddleRight, UITheme.TextDim,
                Center, new Vector2(1f, 0.5f), new Vector2(ValueRight, -82f), new Vector2(200f, 32f), true, false);

            // ---- 04 当前武器 ----
            UIChrome.CreateLabel(content, "WeaponIndex", "04", UITheme.SizeNano,
                TextAnchor.MiddleLeft, UITheme.A(UITheme.TextMuted, 0.85f),
                Center, new Vector2(0f, 0.5f), new Vector2(LeftEdge, -128f), new Vector2(22f, 22f), false, true);
            UIChrome.CreateLabel(content, "WeaponLabel", "当前武器", UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextFaint,
                Center, new Vector2(0f, 0.5f), new Vector2(LeftEdge + 30f, -128f), new Vector2(150f, 22f), false);
            weaponValue = UIChrome.CreateLabel(content, "WeaponValue", "—", 24,
                TextAnchor.MiddleRight, UITheme.TextDim,
                Center, new Vector2(1f, 0.5f), new Vector2(ValueRight, -128f), new Vector2(200f, 32f), true, false);

            // 行分隔线：四行原本是四组"左标签 + 右数值"的松散文字，
            // 三条极淡的线把它们读成一张表——线比留白更能表达"这是一列读数"。
            // 和上面一样内联，理由相同：y 一旦变成方法参数，工具就整段看不见。
            Image rowRule1 = UIFactory.CreateImage(content, "RowRule1", UITheme.A(UITheme.Cyan, 0.13f));
            UIFactory.Place(rowRule1.gameObject, Center, Center,
                new Vector2((LeftEdge + ValueRight) * 0.5f, -13f), new Vector2(ValueRight - LeftEdge, 1f));

            Image rowRule2 = UIFactory.CreateImage(content, "RowRule2", UITheme.A(UITheme.Cyan, 0.13f));
            UIFactory.Place(rowRule2.gameObject, Center, Center,
                new Vector2((LeftEdge + ValueRight) * 0.5f, -59f), new Vector2(ValueRight - LeftEdge, 1f));

            Image rowRule3 = UIFactory.CreateImage(content, "RowRule3", UITheme.A(UITheme.Cyan, 0.13f));
            UIFactory.Place(rowRule3.gameObject, Center, Center,
                new Vector2((LeftEdge + ValueRight) * 0.5f, -105f), new Vector2(ValueRight - LeftEdge, 1f));

            // 竖分隔也升级成带菱形端点的刻度线：裸线是"切开"，带端点是"仪器分栏"
            UIChrome.VRule(content, new Vector2(DividerX, 0f), 264f, UITheme.EdgeHair, 5f);
        }

        // ------------------------------------------------------------ 右列

        private void BuildActions(Transform content)
        {
            // 主次两个按钮同宽。宽度一样，右列才有一条共同的左右边界；
            // 高度差 12px 足够交代"谁更重"，不需要再靠线去分组——
            // 主按钮是实体块、次按钮是一行字，两者本来就不是同一种东西。
            UIAction resume = UIChrome.Action(content, "BtnResume", "", "继续任务", "ESC",
                new Vector2(264f, 58f), new Vector2(RightCenterX, 60f), UIActionStyle.Primary);
            resume.Button.onClick.AddListener(OnResumeClicked);

            UIAction menu = UIChrome.Action(content, "BtnMenu", "", "返回主菜单", "",
                new Vector2(264f, 46f), new Vector2(RightCenterX, -12f), UIActionStyle.Ghost);
            menu.Button.onClick.AddListener(OnMenuClicked);

            // 说明文字改成描边小标签：裸文字挂在按钮下方像"没写完的一行"，
            // 变成 chip 之后它是明确的状态注记，也不会跟按钮抢层级。
            // 196×34 = 文字 140 + 左右各 28、字号 20 + 上下各 7：原来的 176×26 太贴，
            // 字几乎顶到切角上（见 UITheme.TagPadX 的注）
            UIChrome.Tag(content, "Hint", "本局进度已保留",
                new Vector2(196f, 34f), new Vector2(RightCenterX, -68f), UITheme.A(UITheme.Cyan, 0.85f), false);
        }

        // ------------------------------------------------------------ 数据

        /// <summary>面板一直存在、只是 alpha 变化，所以 OnEnable 只会在创建时跑一次，订阅是常驻的。</summary>
        private void OnEnable()
        {
            EventBus.LevelChanged += OnLevelChanged;
            EventBus.LevelProgressChanged += OnLevelProgress;
            EventBus.WeaponChanged += OnWeaponChanged;
        }

        private void OnDisable()
        {
            EventBus.LevelChanged -= OnLevelChanged;
            EventBus.LevelProgressChanged -= OnLevelProgress;
            EventBus.WeaponChanged -= OnWeaponChanged;
        }

        private void OnLevelChanged(string name)
        {
            levelName = name;
        }

        private void OnLevelProgress(int current, int total)
        {
            waveCurrent = current;
            waveTotal = total;
        }

        private void OnWeaponChanged(int index, string name)
        {
            weaponName = name;
        }

        public override void SetVisible(bool visible)
        {
            base.SetVisible(visible);
            if (visible) Refresh();
        }

        /// <summary>
        /// 打开的那一帧才读数值：数据是"这一刻的快照"，不需要每帧刷新。
        /// 得分/连击直接问 GameManager，其余用事件缓存——暂停时场上没有事件，缓存就是定格值。
        /// </summary>
        private void Refresh()
        {
            GameManager gm = GameManager.Instance;

            if (scoreValue != null)
            {
                int score = gm != null ? gm.Score : 0;
                scoreValue.text = score.ToString("D6");
            }

            if (comboValue != null)
            {
                int combo = gm != null ? gm.Combo : 0;
                // 用乘号而不是字母 x：等宽字体里 x 和乘号高度不同，乘号更矮更居中
                comboValue.text = "×" + combo;
            }

            if (levelValue != null)
            {
                string text = string.IsNullOrEmpty(levelName) ? "—" : levelName;
                if (waveTotal > 0) text += "　" + waveCurrent + "/" + waveTotal;
                levelValue.text = text;
            }

            if (weaponValue != null)
            {
                weaponValue.text = string.IsNullOrEmpty(weaponName) ? "—" : weaponName;
            }
        }

        // ------------------------------------------------------------ 回调

        private void OnResumeClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.Playing);
            }
        }

        private void OnMenuClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            // 回主菜单前必须把时间放开：留着 timeScale 为 0 会让整个菜单冻住。
            // 用 Reset 而不是只清暂停——慢镜可能还挂着没到期，只清暂停会带着 0.42 回去。
            TimeFlow.Reset();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMenu();
            }
        }
    }
}
