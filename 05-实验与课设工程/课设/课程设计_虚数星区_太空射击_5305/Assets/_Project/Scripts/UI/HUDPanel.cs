using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 战斗 HUD（v2「战术终端」）。
    ///
    /// 版面只占四角与上下边缘，中间整块留给弹幕：
    ///   左上 船体仪表（左栏 机型铭牌 + 大号当前值 / 小号上限，右栏 标题 / 状态词 / 20 格分段 + 三档刻度）
    ///   右上 得分（SizeHero 等宽）与连击（脉冲缩放，从右边缘向左生长）
    ///   左下 两枚带键帽的切角武器芯片
    ///   右下 星区名 + WAVE 微标 + 波次刻度条
    ///   顶部 Boss：整宽切角分段条（出现时左右上角模块整体下移让位，避免叠压）
    ///
    /// 它是纯粹的「订阅者」：所有数据都从 EventBus 来，从不主动去问玩家或关卡，
    /// 因此改玩法逻辑不会波及界面，改界面也不会碰玩法。
    /// 显隐交给 UIPanelBase 统一处理（用 CanvasGroup.alpha 而不是 SetActive）。
    /// </summary>
    public class HUDPanel : UIPanelBase
    {
        // 顶角模块的玻璃底比标准面板更透：HUD 底下是活的星空和弹幕，压太狠会挡画面。
        // 但不能再低 —— 0.62 的时候顶层那条取景标尺线会从模块里透出来，像一道划痕。
        private const float ModuleBodyAlpha = 0.78f;
        // Boss 条占掉顶部多少：条出现时顶角模块整组下移这么多
        private const float BossCornerDrop = 52f;
        // 下移 / 回落速度：0.15 秒左右，够顺滑又不拖沓
        private const float CornerDropSpeed = 360f;

        // ------------------------------------------------------------ 左上：船体

        private UISegBar hpBar;
        private Text hpLabel;       // 当前值：大号
        private Text hpMaxText;     // 上限「/ 150」：小一号，缩在数值右下，像刻度内的副读数
        private Text hpStateText;
        private Text hpPlate;       // 机型铭牌「疾风 · GALE」

        // 受击时抖的是这条分段条（不是整块模块，模块跟着抖会把标题也甩动）
        private RectTransform hpBarRect;
        private Vector2 hpBarHome;
        private float hpShake;          // 1 → 0 衰减，驱动左右抖动
        private float hpRatio = 1f;
        private int lastHp = -1;

        // 顶角模块整体位移：Boss 条是全宽的，顶角模块必须让开
        private RectTransform hullCluster;
        private RectTransform scoreCluster;
        private Vector2 hullHome;
        private Vector2 scoreHome;
        private float cornerDrop;
        private float cornerDropTarget;
        private float appliedCornerDrop = -1f;

        // ------------------------------------------------------------ 右上：得分 / 连击

        private Text scoreText;
        private Text comboText;
        private float displayScore;
        private int targetScore;
        private float comboPunch;

        // ------------------------------------------------------------ 左下：武器

        private Text weaponText;
        private Image[] chipBg;
        private Image[] chipEdge;
        private Text[] chipLabel;
        private Text[] chipKey;         // 键帽上的 1 / 2
        private Image[] chipKeyBg;
        // 名字和武器配置对齐（离子光束 / 双轨机炮）：芯片上写短名会和操作说明、图鉴对不上。
        // 序号已经交给左边的键帽，这里不再重复写「1 ·」。
        private static readonly string[] ChipFallback = { "离子光束", "双轨机炮" };

        // ------------------------------------------------------------ 右下：星区 / 波次

        private Text levelText;
        private Text waveText;
        private UIMeter waveMeter;
        private string levelName = "星区 01";
        private int progressCurrent;
        private int progressTotal;

        // ------------------------------------------------------------ 顶部：Boss

        private GameObject bossRoot;
        private UISegBar bossBar;
        private Text bossNameText;
        private Text bossPhaseText;
        private int bossPhase = -1;

        // 常驻微标签的字距版本：SpaceOut 每次调用都要拼字符串，固定的几个先拼好
        private static readonly string LabelStable = UIFactory.SpaceOut("STABLE");
        private static readonly string LabelCritical = UIFactory.SpaceOut("CRITICAL");
        private static readonly string LabelCombo = UIFactory.SpaceOut("COMBO");

        // ------------------------------------------------------------ 构建

        public void Build()
        {
            InitGroup(false);

            BuildHealthBar();
            BuildScore();
            BuildCombo();
            BuildFooter();
            BuildBossBar();
        }

        private void BuildHealthBar()
        {
            // 模块整组放进一个空容器：Boss 条出现时移动容器，而不是逐个挪子物体
            hullCluster = UIFactory.NewUI(transform, "HullCluster");
            UIFactory.Place(hullCluster.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -26f), new Vector2(456f, 100f));
            hullHome = hullCluster.anchoredPosition;

            UISheet sheet = BuildGlassModule(hullCluster, "HullModule", new Vector2(456f, 100f), UITheme.Cyan);
            RectTransform root = sheet.Root;

            // 模块拆成左右两栏：左栏是「读数」，右栏是「量表」，中间一条竖线分开。
            // 上一版全横排 —— 大数值右对齐、分段条左对齐，两个都够宽，结果数字左缘压到条的末格上。
            //
            // 标题行只留两块：机型铭牌（左）和状态词（右）。
            // 原来这里还写着「HULL INTEGRITY」，拉开字距后有 239px，和右对齐的 STABLE 直接叠成
            // 「HULL INTEGRITYSTABLE」；角落模块里的"20 格血条 + 大号数字"已经说明了一切，
            // 与其把长标题压到 10px 硬塞，不如让位给真正有信息量的铭牌。
            Vector2 tl = new Vector2(0f, 1f);
            Vector2 tr = new Vector2(1f, 1f);

            // ---- 左栏：机型铭牌 / 当前值 + 上限
            TypedPlate(root, tl);

            hpLabel = UIFactory.CreateTextMono(root, "HullValue", "100", UITheme.SizeValue,
                TextAnchor.MiddleLeft, UITheme.Cyan, true);
            UIFactory.Place(hpLabel.gameObject, tl, tl, new Vector2(18f, -38f), new Vector2(64f, 42f));

            hpMaxText = UIFactory.CreateTextMono(root, "HullMax", "/ 100", UITheme.SizeMicro,
                TextAnchor.MiddleLeft, UITheme.A(UITheme.Cyan, 0.72f));
            UIFactory.Place(hpMaxText.gameObject, tl, tl, new Vector2(82f, -46f), new Vector2(72f, 20f));

            UIChrome.VLine(root, "HullDivider", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(152f, 0f), 70f, UITheme.A(UITheme.Cyan, 0.16f));

            // ---- 右栏：状态词 / 20 格分段条 / 三档刻度数字
            // 右上状态词：STABLE / CRITICAL，掉到 35% 以下换红色（和血条同步呼吸）
            hpStateText = UIFactory.CreateTextMono(root, "HullState", LabelStable,
                UITheme.SizeNano, TextAnchor.MiddleRight, UITheme.TextFaint);
            UIFactory.Place(hpStateText.gameObject, tr, tr, new Vector2(-24f, -14f), new Vector2(112f, 18f));

            // 一格 = 5%，掉血是整格熄灭，比连续条更容易一眼读出剩余
            hpBar = UISegBar.Create(root, "HullSegments", new Vector2(256f, 14f), Vector2.zero,
                20, UITheme.Cyan, UITheme.A(UITheme.Cyan, 0.10f));
            UIFactory.Place(hpBar.gameObject, tl, tl, new Vector2(168f, -50f), new Vector2(256f, 14f));
            hpBar.Set(1f);

            hpBarRect = hpBar.GetComponent<RectTransform>();
            hpBarHome = hpBarRect.anchoredPosition;

            // 刻度数字比刻度线好读：1px 的线在 14px 高的条上等于没画，数字还能顺带交代"条有多长"
            for (int i = 1; i <= 3; i++)
            {
                Text tick = UIFactory.CreateTextMono(root, "HullScale" + i, (i * 25).ToString(),
                    UITheme.SizeNano, TextAnchor.MiddleCenter, UITheme.A(UITheme.Cyan, 0.62f));
                UIFactory.Place(tick.gameObject, tl, tl,
                    new Vector2(168f + 256f * (i * 0.25f) - 22f, -72f), new Vector2(44f, 20f));
            }
        }

        /// <summary>
        /// 机型铭牌：局内也得知道自己开的是哪艘船。机库里四艘船的差别写在卡片上，
        /// 进了战斗就只剩血量和射速的体感差异 —— 补一块铭牌，两处读法才对得上。
        /// </summary>
        private void TypedPlate(RectTransform parent, Vector2 anchor)
        {
            hpPlate = UIFactory.CreateText(parent, "HullPlate", "", UITheme.SizeMicro,
                TextAnchor.MiddleLeft, UITheme.TextDim);
            UIFactory.Place(hpPlate.gameObject, anchor, anchor, new Vector2(18f, -13f), new Vector2(190f, 18f));

            RefreshShipPlate();
        }

        private void RefreshShipPlate()
        {
            if (hpPlate == null) return;

            ShipConfig ship = PlayerLoadout.Selected;
            hpPlate.text = ship != null ? ship.displayName + " · " + ship.callsign : "";
        }

        private void BuildScore()
        {
            scoreCluster = UIFactory.NewUI(transform, "ScoreCluster");
            UIFactory.Place(scoreCluster.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -26f), new Vector2(404f, 92f));
            scoreHome = scoreCluster.anchoredPosition;

            UISheet sheet = BuildGlassModule(scoreCluster, "ScoreModule", new Vector2(404f, 92f), UITheme.Cyan);

            // 标题挪到左上、配一道强调竖条：上一版标题右对齐、数字也右对齐，
            // 结果是"框的左上角整块空着，数字挤在右下角"——重心全歪在一角。
            Image mark = UIFactory.CreateImage(sheet.Root, "ScoreMark", UITheme.A(UITheme.Cyan, 0.7f));
            UIFactory.Place(mark.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -18f), new Vector2(2f, 10f));

            UIChrome.Eyebrow(sheet.Root, "ScoreTitle", "SCORE",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -15f), new Vector2(200f, 16f), TextAnchor.MiddleLeft,
                UITheme.A(UITheme.Cyan, 0.55f));

            // 等宽大数字：滚动时每一位等宽，整行不会左右抖。
            // 字号收到 58：SizeHero(64) 在 104 高的框里顶到右边框只剩 6px，读起来像"溢出"而不是"居中"
            scoreText = UIFactory.CreateTextMono(sheet.Root, "Score", "000000", UITheme.SizeScore,
                TextAnchor.MiddleRight, UITheme.TextMain, true);
            UIFactory.Place(scoreText.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -28f), new Vector2(348f, 54f));

            // 读数底线：让大数字像刻在仪表上，而不是浮在空中
            UIChrome.Hairline(sheet.Root, "ScoreRule", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 8f), 348f, UITheme.EdgeHair);
        }

        private void BuildCombo()
        {
            // 连击贴着得分模块下沿、同样右对齐：整块都在右边缘，不占画面中间
            comboText = UIFactory.CreateTextMono(scoreCluster, "Combo", "", UITheme.SizeH3,
                TextAnchor.MiddleRight, UITheme.CyanBright, true);
            UIFactory.Place(comboText.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -126f), new Vector2(540f, 40f));
        }

        private void BuildFooter()
        {
            // 武器区标题交给 Eyebrow，旧版这行写的是「武 器」，中文字距拉开反而更难看
            weaponText = UIChrome.Eyebrow(transform, "WeaponTitle", "WEAPON",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 126f), new Vector2(180f, 16f), TextAnchor.MiddleLeft, UITheme.TextFaint);

            BuildWeaponChips();

            // 星区名是中文，走默认字体；数字和 WAVE 微标才用等宽
            levelText = UIFactory.CreateText(transform, "Level", levelName, UITheme.SizeH2,
                TextAnchor.MiddleRight, UITheme.TextMain);
            UIFactory.Place(levelText.gameObject, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 138f), new Vector2(460f, 46f));

            BuildProgress();
        }

        /// <summary>
        /// 两枚切角武器芯片，左边嵌一个键帽。
        ///
        /// 上一版是把「1 · 离子光束」整串写在芯片里、铺满整块洗底色 —— 选中的那枚
        /// 读起来像一颗实心色块，序号和名字挤在一起分不出层次。现在把键位单独提出来做键帽，
        /// 名字退到右边左对齐，选中态只加一层很淡的洗底 + 描边，靠键帽亮起来表达"当前"。
        /// </summary>
        private void BuildWeaponChips()
        {
            const float chipW = 196f;
            const float chipH = 42f;
            const float gap = 12f;

            RectTransform row = UIFactory.NewUI(transform, "WeaponRow");
            UIFactory.Place(row.gameObject, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 76f), new Vector2(chipW * 2f + gap, chipH));

            chipBg = new Image[ChipFallback.Length];
            chipEdge = new Image[ChipFallback.Length];
            chipLabel = new Text[ChipFallback.Length];
            chipKey = new Text[ChipFallback.Length];
            chipKeyBg = new Image[ChipFallback.Length];

            for (int i = 0; i < ChipFallback.Length; i++)
            {
                Color accent = i == 0 ? UITheme.Cyan : UITheme.Magenta;

                // 芯片中心相对 row 中心：两枚并排、等距
                float x = -(chipW + gap) * 0.5f + i * (chipW + gap);

                Image body = UIFactory.CreateImage(row, "WeaponChip" + i, UITheme.A(UITheme.Surface, 0.42f));
                body.sprite = UISprites.Sheet(UITheme.CutChip);
                body.type = Image.Type.Sliced;
                UIFactory.Place(body.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(chipW, chipH));

                Image edge = UIFactory.CreateImage(body.transform, "Edge", UITheme.EdgeHair);
                edge.sprite = UISprites.SheetEdge(UITheme.CutChip, 1f);
                edge.type = Image.Type.Sliced;
                UIFactory.Stretch(edge.rectTransform);

                // 键帽：1 / 2，和键盘上的数字键一一对应
                Image keyBg = UIFactory.CreateImage(body.transform, "Key", UITheme.A(UITheme.SurfaceDeep, 0.7f));
                keyBg.sprite = UISprites.Sheet(3f);
                keyBg.type = Image.Type.Sliced;
                UIFactory.Place(keyBg.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(10f, 0f), new Vector2(24f, 24f));

                Text key = UIFactory.CreateTextMono(keyBg.transform, "KeyLabel", (i + 1).ToString(),
                    UITheme.SizeMicro, TextAnchor.MiddleCenter, UITheme.TextDim, true);
                UIFactory.Stretch(key.rectTransform);

                Text label = UIFactory.CreateText(body.transform, "Label", ChipFallback[i],
                    UITheme.SizeCaption, TextAnchor.MiddleLeft, UITheme.TextDim);
                UIFactory.Place(label.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(44f, 0f), new Vector2(148f, 26f));

                chipBg[i] = body;
                chipEdge[i] = edge;
                chipLabel[i] = label;
                chipKey[i] = key;
                chipKeyBg[i] = keyBg;
            }

            PaintChips(0, null);
        }

        /// <summary>重画芯片状态。displayName 只在切到该武器时才知道，没给就沿用原来的字</summary>
        private void PaintChips(int active, string displayName)
        {
            if (chipBg == null) return;

            for (int i = 0; i < chipBg.Length; i++)
            {
                bool on = i == active;
                Color accent = i == 0 ? UITheme.Cyan : UITheme.Magenta;

                chipBg[i].color = on ? UITheme.A(accent, 0.03f) : UITheme.A(UITheme.Surface, 0.42f);

                if (chipEdge != null && chipEdge[i] != null)
                    chipEdge[i].color = on ? UITheme.A(accent, 0.62f) : UITheme.EdgeHair;

                if (chipKeyBg[i] != null)
                    chipKeyBg[i].color = on ? UITheme.A(accent, 0.9f) : UITheme.A(UITheme.SurfaceDeep, 0.7f);

                if (chipKey[i] != null)
                    chipKey[i].color = on ? UITheme.SurfaceDeep : UITheme.TextDim;

                if (chipLabel[i] != null)
                {
                    chipLabel[i].color = on ? accent : UITheme.TextDim;
                    chipLabel[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;

                    if (on && !string.IsNullOrEmpty(displayName))
                        chipLabel[i].text = displayName;
                }
            }
        }

        /// <summary>
        /// 波次推进：WAVE 微标 + 一条带刻度的细条。
        ///
        /// 旧版把「星区 01 · 波次 2 / 6」塞进同一个 Text，字体只能取其一；
        /// 现在中文星区名走默认字体，WAVE 微标走等宽，两者各司其职。
        /// </summary>
        private void BuildProgress()
        {
            waveMeter = UIMeter.Create(transform, "WaveMeter", new Vector2(320f, 6f), Vector2.zero,
                6, UITheme.Cyan);
            waveMeter.SetTrackColor(UITheme.A(UITheme.Cyan, 0.12f));
            UIFactory.Place(waveMeter.gameObject, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 84f), new Vector2(320f, 6f));

            waveText = UIChrome.Eyebrow(transform, "WaveLabel", "WAVE -- / --",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 108f), new Vector2(420f, 16f), TextAnchor.MiddleRight, UITheme.TextFaint);
        }

        /// <summary>星区名和波次来自不同事件，各自记下来再拼</summary>
        private void RefreshLevelLine()
        {
            if (levelText != null) levelText.text = levelName;

            if (waveText != null)
            {
                waveText.text = progressTotal > 0
                    ? UIFactory.SpaceOut("WAVE " + progressCurrent.ToString("D2") + " / " + progressTotal.ToString("D2"))
                    : UIFactory.SpaceOut("WAVE -- / --");
            }
        }

        private void BuildBossBar()
        {
            GameObject root = new GameObject("BossBar", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            UIFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(1840f, 70f));

            // 名字（左）/ 阶段（右）
            bossNameText = UIFactory.CreateTextMono(root.transform, "BossName", "",
                UITheme.SizeMicro, TextAnchor.MiddleLeft, UITheme.Magenta, true);
            UIFactory.Place(bossNameText.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -2f), new Vector2(720f, 20f));

            bossPhaseText = UIFactory.CreateTextMono(root.transform, "BossPhase", "",
                UITheme.SizeNano, TextAnchor.MiddleRight, UITheme.A(UITheme.Magenta, 0.85f));
            UIFactory.Place(bossPhaseText.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -2f), new Vector2(320f, 20f));

            // 整宽切角条：底 + 品红描边，比旧版 1000 宽的居中条更有「Boss 压境」的分量
            Image body = UIFactory.CreateImage(root.transform, "BossBody", UITheme.A(UITheme.SurfaceDeep, 0.82f));
            body.sprite = UISprites.Sheet(UITheme.CutChip);
            body.type = Image.Type.Sliced;
            UIFactory.Place(body.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(1840f, 34f));

            Image edge = UIFactory.CreateImage(body.transform, "BossEdge", UITheme.A(UITheme.Magenta, 0.38f));
            edge.sprite = UISprites.SheetEdge(UITheme.CutChip, 1.15f);
            edge.type = Image.Type.Sliced;
            UIFactory.Stretch(edge.rectTransform);

            // 32 格分段：Boss 掉血是一格一格熄，比连续条更有压迫感
            bossBar = UISegBar.Create(body.transform, "BossSegments", new Vector2(1824f, 22f),
                Vector2.zero, 32, UITheme.Magenta, UITheme.A(UITheme.Magenta, 0.12f));
            UIFactory.Place(bossBar.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1824f, 22f));
            bossBar.Set(1f);

            // 阶段阈值（66% / 33%）：三段攻击的分界，画在条上沿
            for (int i = 1; i <= 2; i++)
            {
                Image tick = UIFactory.CreateImage(bossBar.transform, "BossPhaseTick" + i,
                    UITheme.A(UITheme.Magenta, 0.55f));
                UIFactory.Place(tick.gameObject, new Vector2(i / 3f, 1f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 2f), new Vector2(1f, 5f));
            }

            bossRoot = root;
            bossRoot.SetActive(false);
        }

        // ------------------------------------------------------------ 共用构件

        /// <summary>
        /// HUD 模块统一的玻璃底：CutCard 切角、无外发光（别把星空染亮）。
        /// 大面板那套装饰在这里要收掉一半：顶边的品红短线是为宽面板准备的，
        /// 压到 456 宽的角落模块上就是一道莫名其妙的粉杠。
        /// </summary>
        private UISheet BuildGlassModule(Transform parent, string name, Vector2 size, Color accent)
        {
            UISheet sheet = UIChrome.Sheet(parent, name, size, Vector2.zero, accent, UITheme.CutCard, false);
            sheet.Body.color = UITheme.A(UITheme.Surface, ModuleBodyAlpha);
            sheet.Edge.color = UITheme.A(accent, 0.18f);

            if (sheet.TopLine2 != null) sheet.TopLine2.gameObject.SetActive(false);
            if (sheet.TopLine != null) sheet.TopLine.color = UITheme.A(accent, 0.55f);

            return sheet;
        }

        /// <summary>Boss 名字默认是拉丁（WARDEN-01），但配置允许写中文：等宽字体没有汉字字形，得让位</summary>
        private void SetBossName(string displayName)
        {
            if (bossNameText == null || string.IsNullOrEmpty(displayName)) return;

            bool cjk = ContainsCjk(displayName);
            bossNameText.font = cjk ? UIFactory.DefaultFont : UIFactory.MonoFont;
            bossNameText.text = cjk ? displayName : UIFactory.SpaceOut(displayName.ToUpperInvariant());
        }

        private static bool ContainsCjk(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= 0x2E80) return true;
            }

            return false;
        }

        // ------------------------------------------------------------ 订阅

        private void OnEnable()
        {
            EventBus.PlayerHpChanged += OnPlayerHpChanged;
            EventBus.ScoreChanged += OnScoreChanged;
            EventBus.ComboChanged += OnComboChanged;
            EventBus.WeaponChanged += OnWeaponChanged;
            EventBus.LevelChanged += OnLevelChanged;
            EventBus.LevelProgressChanged += OnLevelProgress;
            EventBus.BossHpChanged += OnBossHpChanged;
            EventBus.BossBarVisible += OnBossBarVisible;
            EventBus.LoadoutChanged += RefreshShipPlate;
        }

        private void OnDisable()
        {
            EventBus.PlayerHpChanged -= OnPlayerHpChanged;
            EventBus.ScoreChanged -= OnScoreChanged;
            EventBus.ComboChanged -= OnComboChanged;
            EventBus.WeaponChanged -= OnWeaponChanged;
            EventBus.LevelChanged -= OnLevelChanged;
            EventBus.LevelProgressChanged -= OnLevelProgress;
            EventBus.BossHpChanged -= OnBossHpChanged;
            EventBus.BossBarVisible -= OnBossBarVisible;
            EventBus.LoadoutChanged -= RefreshShipPlate;
        }

        // ------------------------------------------------------------ 事件响应

        private void OnLevelProgress(int current, int total)
        {
            progressCurrent = current;
            progressTotal = total;

            if (waveMeter != null)
                waveMeter.Set(total > 0 ? Mathf.Clamp01(current / (float)total) : 0f);

            RefreshLevelLine();
        }

        private void OnPlayerHpChanged(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
            hpRatio = ratio;

            // 掉血才抖：重置满血、换船拉高上限、加血都不是挨打
            if (lastHp >= 0 && current < lastHp) hpShake = 1f;
            lastHp = current;

            bool danger = ratio <= 0.35f;

            if (hpBar != null)
            {
                hpBar.Set(ratio);
                // 低血量切 Danger：条和熄灭格一起染红，剩下的格子才不会看着像"还有余量"
                hpBar.SetOnColor(danger ? UITheme.Danger : UITheme.Cyan);
                hpBar.SetOffColor(danger ? UITheme.A(UITheme.Danger, 0.12f) : UITheme.A(UITheme.Cyan, 0.10f));
            }

            if (hpLabel != null)
            {
                hpLabel.text = current.ToString();
                hpLabel.color = danger ? UITheme.Danger : UITheme.Cyan;
            }

            if (hpMaxText != null)
            {
                hpMaxText.text = "/ " + max;
                hpMaxText.color = danger
                    ? UITheme.A(UITheme.Danger, 0.55f)
                    : UITheme.A(UITheme.Cyan, 0.48f);
            }

            if (hpStateText != null)
            {
                hpStateText.text = danger ? LabelCritical : LabelStable;
                hpStateText.color = danger ? UITheme.Danger : UITheme.TextFaint;
            }
        }

        private void OnScoreChanged(int score)
        {
            targetScore = score;

            // 归零（重开一局）直接归位，不要从上一局的高分往回滚
            if (score < displayScore) displayScore = score;

            if (scoreText != null) scoreText.text = Mathf.RoundToInt(displayScore).ToString("D6");
        }

        private void OnComboChanged(int combo)
        {
            if (comboText == null) return;

            if (combo <= 1)
            {
                comboText.text = "";
                return;
            }

            float multiplier = 1f + Mathf.Min(combo, 20) * 0.1f;
            string amber = ColorUtility.ToHtmlStringRGB(UITheme.Amber);

            // 等宽字体没有汉字字形，这里只能用拉丁微标；分数乘率用主题琥珀色，不写死颜色
            comboText.text = LabelCombo + "  ×" + combo
                + "   <color=#" + amber + ">MULT " + multiplier.ToString("0.0") + "</color>";
            comboPunch = 1f;        // 每连一次弹一下，连得越快弹得越密
        }

        private void OnWeaponChanged(int index, string displayName)
        {
            // 标题带上槽位号，和芯片序号对得上；换武器时这里是最先被看到的反馈
            if (weaponText != null)
                weaponText.text = UIFactory.SpaceOut("WEAPON " + (index + 1).ToString("D2"));

            PaintChips(index, displayName);
        }

        private void OnLevelChanged(string name)
        {
            levelName = name;
            RefreshLevelLine();
        }

        private void OnBossHpChanged(float normalized)
        {
            float ratio = Mathf.Clamp01(normalized);

            if (bossBar != null) bossBar.Set(ratio);

            if (bossPhaseText != null)
            {
                // 和 BossController 的 66% / 33% 阶段阈值保持一致
                int phase = ratio > 0.66f ? 1 : (ratio > 0.33f ? 2 : 3);

                if (phase != bossPhase)
                {
                    bossPhase = phase;
                    bossPhaseText.text = UIFactory.SpaceOut("PHASE " + phase + " / 3");
                }
            }
        }

        private void OnBossBarVisible(bool visible, string displayName)
        {
            if (!string.IsNullOrEmpty(displayName)) SetBossName(displayName);

            if (bossRoot != null) bossRoot.SetActive(visible);

            // 整宽 Boss 条会盖住顶角模块：整组下移让位，Boss 倒了再落回来
            cornerDropTarget = visible ? BossCornerDrop : 0f;
        }

        // ------------------------------------------------------------ 每帧动效

        /// <summary>
        /// 四个小动效都走 unscaledDeltaTime：暂停时它们要停住，但结算面板弹出那一下
        /// 分数还在滚，用 scaled 的话 timeScale=0 会让数字永远停在半路。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            UpdateScoreRoll();
            UpdateHealthFeedback();
            UpdateComboPunch();
            UpdateCornerDrop();
        }

        /// <summary>分数滚动：从旧值滚到新值，差得多滚得快，差得少也不磨蹭</summary>
        private void UpdateScoreRoll()
        {
            if (Mathf.Approximately(displayScore, targetScore)) return;

            float diff = Mathf.Abs(targetScore - displayScore);
            float speed = Mathf.Max(900f, diff * 5f);
            displayScore = Mathf.MoveTowards(displayScore, targetScore, speed * Time.unscaledDeltaTime);

            if (scoreText != null) scoreText.text = Mathf.RoundToInt(displayScore).ToString("D6");
        }

        /// <summary>受击抖动 + 低血脉动</summary>
        private void UpdateHealthFeedback()
        {
            if (hpShake > 0f)
            {
                hpShake = Mathf.Max(0f, hpShake - Time.unscaledDeltaTime * 4.5f);

                // 抖 5px、78rad/s：够看出来，又不至于把数字甩出屏幕
                float dx = Mathf.Sin(Time.unscaledTime * 78f) * 5f * hpShake;
                if (hpBarRect != null) hpBarRect.anchoredPosition = hpBarHome + new Vector2(dx, 0f);
            }
            else if (hpBarRect != null && hpBarRect.anchoredPosition != hpBarHome)
            {
                hpBarRect.anchoredPosition = hpBarHome;
            }

            // 低血量：整条呼吸。颜色在事件里已经切成 Danger，这里只动透明度（和状态词一起）
            if (hpBar != null && hpRatio <= 0.35f)
            {
                float pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 7f);
                hpBar.SetOnColor(UITheme.A(UITheme.Danger, pulse));

                if (hpStateText != null)
                    hpStateText.color = UITheme.A(UITheme.Danger, Mathf.Lerp(0.55f, 1f, pulse));
            }
        }

        /// <summary>连击数字弹一下再回落。轴心在右上角，所以是向左下生长，不挤中间画面</summary>
        private void UpdateComboPunch()
        {
            if (comboPunch <= 0f) return;

            comboPunch = Mathf.Max(0f, comboPunch - Time.unscaledDeltaTime * 3.6f);

            if (comboText != null)
            {
                float s = 1f + 0.14f * comboPunch;
                comboText.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        /// <summary>Boss 条出现 / 消失时，顶角模块整组上下让位</summary>
        private void UpdateCornerDrop()
        {
            if (!Mathf.Approximately(cornerDrop, cornerDropTarget))
                cornerDrop = Mathf.MoveTowards(cornerDrop, cornerDropTarget, CornerDropSpeed * Time.unscaledDeltaTime);

            if (Mathf.Approximately(cornerDrop, appliedCornerDrop)) return;
            appliedCornerDrop = cornerDrop;

            if (hullCluster != null)
                hullCluster.anchoredPosition = new Vector2(hullHome.x, hullHome.y - cornerDrop);

            if (scoreCluster != null)
                scoreCluster.anchoredPosition = new Vector2(scoreHome.x, scoreHome.y - cornerDrop);
        }
    }
}
