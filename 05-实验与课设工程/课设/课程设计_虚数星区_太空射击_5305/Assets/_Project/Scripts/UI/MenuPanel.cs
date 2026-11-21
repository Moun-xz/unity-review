using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 主菜单 —— 全屏构图，不是"一块大卡片摆在中间"。
    ///
    /// 版面分三块，靠留白和细线切分，不再是居中堆叠：
    ///   · 左上：品牌区。英文微标 → 美术字标 → 一条细横线 → 中文类型说明 → 英文类型说明 → 三行署名。
    ///   · 左下：当前战机展示位（图 + 呼号 + 船名），换船后立刻能看到。
    ///   · 右侧：纵向功能列表。每行 = 序号 + 名称 + 英文元信息 + 箭头，悬停整行铺底、左侧竖条变宽。
    ///
    /// 为什么不给主菜单套 Sheet 面板：这一屏背后是星云美术图，套一块半透明大板会把图压死，
    /// 而且和暂停 / 结算这些"真正浮在战斗之上的面板"混成同一档。全屏构图更大气，也更像游戏启动屏。
    ///
    /// 交互逻辑一个字都没改：五个入口 + 操作说明，全部走同一个方法；
    /// 退出仍是"点两次才真退"；回车 / ESC 的快捷路径保持不变。
    /// </summary>
    public class MenuPanel : UIPanelBase
    {
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        /// <summary>左缘装饰竖线的横坐标（画布坐标，从屏幕左缘算）</summary>
        private const float AccentX = 80f;

        /// <summary>左侧内容起点：装饰线右侧再让 24</summary>
        private const float ContentLeft = 104f;

        /// <summary>右侧功能列表：宽 620、右缘距屏幕 80</summary>
        private const float RowWidth = 620f;
        private const float RowHeight = 84f;
        private const float RowStep = 96f;
        private const float RowRight = 80f;
        private const float RowTopY = 228f;

        private Text codeText;
        private Text subtitleCn;
        private Text subtitleEn;
        private Image shipImage;
        private Image shipGlow;
        private Image shipPadCore;
        private Image shipPadWingL;
        private Image shipPadWingR;
        private Text shipName;
        private Text shipCall;
        private Text teamText;
        private Text bestLabel;
        private Text bestValue;
        private Text hintText;
        private Image accentLine;
        private Image frameLine;
        private RectTransform logoRect;
        private RectTransform ruleRect;
        private MenuIntro intro;

        private UIAction[] rows;

        // 退出确认：第一次点只是"上膛"，窗口内再点一次才真退。
        // 答辩演示时手滑点到退出、整个游戏直接关掉，这个险不值得冒。
        private Text quitLabel;
        private UIButtonFx quitFx;
        private float quitArmedUntil;
        private const float QuitArmWindow = 3.5f;
        private const string QuitLabelIdle = "退出游戏";
        private const string QuitLabelArmed = "再点一次确认退出";

        private SettingsPanel settings;
        private HelpPanel help;
        private LevelSelectPanel levelSelect;
        private EnemyCodexPanel codex;
        private bool settingsOpen;
        private bool helpOpen;
        private bool levelOpen;
        private bool codexOpen;
        private float pulse;

        /// <summary>主菜单被收起来的时刻，用来判断回来时要不要重播出场</summary>
        private float hiddenAt = -99f;
        private const float IntroReplayGap = 1.2f;

        /// <summary>设置 / 机库 / 说明这类子面板是不是开着。GameManager 判断回车要不要直接开局时要看这个</summary>
        public bool SubPanelOpen { get { return settingsOpen || helpOpen || levelOpen || codexOpen; } }

        /// <summary>
        /// 开机流程还没走到主菜单之前先别出场。
        ///
        /// 主菜单在启动时就已经"显示"了（面板层 alpha 是 1），只是被标题屏和黑幕盖着。
        /// 不压住的话这一秒的出场会在黑幕后面白播一遍，等真被看到时只剩个终态。
        /// 由 GameBootstrap 置 true，跃迁白光打到峰值那一帧回调 ResumeIntro 放行。
        /// </summary>
        public bool IntroDeferred { get; set; }

        public void ResumeIntro()
        {
            IntroDeferred = false;
            if (intro != null) intro.Play();
        }

        public void Build(int highScore)
        {
            InitGroup(true);

            BuildBackdrop();
            BuildFrame();
            BuildBrand();
            BuildShowcase();
            BuildMenu();
            BuildFooter();

            // 放在最后：出场序列要拿各元素的最终位置当"原位"，晚一个方法对不上号
            BuildIntro();

            UpdateBest(highScore);
            UpdateShip();
        }

        /// <summary>由 GameBootstrap 在创建子面板之后调用</summary>
        public void BindSubPanels(SettingsPanel settingsPanel, HelpPanel helpPanel,
                                  LevelSelectPanel levelSelectPanel, EnemyCodexPanel codexPanel)
        {
            settings = settingsPanel;
            if (settings != null) settings.Closed = CloseSettings;


            help = helpPanel;
            if (help != null) help.Closed = CloseHelp;

            levelSelect = levelSelectPanel;
            if (levelSelect != null)
            {
                levelSelect.Closed = CloseLevelSelect;
                levelSelect.Chosen = OnLevelChosen;
            }

            codex = codexPanel;
            if (codex != null) codex.Closed = CloseCodex;
        }

        // ------------------------------------------------------------ 背景与画面框

        /// <summary>
        /// 四层背景：压暗层 → 程序化天幕 → 远景行星 → 左右渐隐暗坡 → 极淡网格。
        ///
        /// 底图不再用下载的星云照片：照片是写实摄影调，和深色界面不是一套色彩语言，
        /// 而且必须靠两道暗坡硬压才读得清字。现在换成 BackdropArt 程序化生成的天幕，
        /// 色板和游戏内、界面完全一致，暗坡只用来做"中间留亮、两侧收暗"的构图。
        /// </summary>
        private void BuildBackdrop()
        {
            Image overlay = UIFactory.CreateImage(transform, "Overlay", UITheme.Overlay);
            UIFactory.Stretch(overlay.rectTransform);

            Sprite sky = BackdropArt.Sky(0);
            if (sky != null)
            {
                Image bg = UIFactory.CreateImage(transform, "Backdrop", Color.white);
                bg.sprite = sky;
                bg.type = Image.Type.Simple;
                bg.raycastTarget = false;
                UIFactory.Stretch(bg.rectTransform);
            }

            // 远景行星：放在右上角、只露出受光的那一侧，给画面一个明确的景深锚点
            Sprite planetArt = BackdropArt.Planet(0);
            if (planetArt != null)
            {
                Image planet = UIFactory.CreateImage(transform, "SkyPlanet", new Color(0.92f, 0.96f, 1f, 0.58f));
                planet.sprite = planetArt;
                planet.type = Image.Type.Simple;
                planet.raycastTarget = false;
                RectTransform pr = planet.rectTransform;
                pr.anchorMin = new Vector2(0.5f, 0.5f);
                pr.anchorMax = new Vector2(0.5f, 0.5f);
                pr.pivot = new Vector2(0.5f, 0.5f);
                pr.sizeDelta = new Vector2(760f, 760f);
                pr.anchoredPosition = new Vector2(706f, 268f);
            }

            // 左右两道暗坡：左边给品牌区、右边给功能列表，都把背后的星云压下去，字才读得清。
            // 用横向渐变 sprite（而不是把竖向渐变旋转 90°）：旋转会把渐变的有效宽度换成屏幕高，
            // 暗坡会在半路断开，留下一条竖直硬边。
            Image ramp = UIFactory.CreateImage(transform, "LeftRamp", UITheme.A(UITheme.SurfaceDeep, 0.94f));
            ramp.sprite = UISprites.GradientH();
            ramp.type = Image.Type.Simple;
            ramp.raycastTarget = false;
            UIFactory.Stretch(ramp.rectTransform);

            Image rampRight = UIFactory.CreateImage(transform, "RightRamp", UITheme.A(UITheme.SurfaceDeep, 0.80f));
            rampRight.sprite = UISprites.GradientH();
            rampRight.type = Image.Type.Simple;
            rampRight.raycastTarget = false;
            UIFactory.Stretch(rampRight.rectTransform);
            // 水平翻转：同一张"左浓右淡"的纹理，镜像过来就是"右浓左淡"
            rampRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            UIChrome.GridBackdrop(transform, "Grid", 0.03f);
        }

        /// <summary>
        /// 整屏取景框：四角细括号 + 上下两条极细标尺线。
        ///
        /// 主菜单不在 Playing/Paused，ScreenFrame 那一层不会亮，所以这里自己画一套。
        /// 只画角、不画整框：整框会把画面切成"窗口"，角标才像取景器。
        /// </summary>
        private void BuildFrame()
        {
            RectTransform frame = UIFactory.NewUI(transform, "Frame");
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;

            Color corner = UITheme.A(UITheme.Cyan, 0.30f);
            UIChrome.Bracket(frame, UICorner.TopLeft, corner, 54f, 2f, 26f);
            UIChrome.Bracket(frame, UICorner.TopRight, corner, 54f, 2f, 26f);
            UIChrome.Bracket(frame, UICorner.BottomLeft, corner, 54f, 2f, 26f);
            UIChrome.Bracket(frame, UICorner.BottomRight, corner, 54f, 2f, 26f);

            frameLine = UIChrome.Hairline(frame, "TopRule", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(160f, -54f), 1600f, UITheme.EdgeHair);
            UIChrome.Hairline(frame, "BottomRule", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(160f, 54f), 1600f, UITheme.EdgeHair);

            // 两端的小刻度：只有 3px 高，但整条线立刻从"装饰"变成"仪表"
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 160f : 1760f;
                UIChrome.VLine(frame, "Tick", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, -54f), 14f, UITheme.A(UITheme.Cyan, 0.45f));
            }
        }

        // ------------------------------------------------------------ 左侧品牌区

        private void BuildBrand()
        {
            // 竖线：从英文微标上方贯到署名下方，整块品牌区靠它"立"起来
            accentLine = UIChrome.VLine(transform, "AccentLine", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(AccentX, 190f), 430f, UITheme.A(UITheme.Cyan, 0.34f));

            codeText = UIChrome.Eyebrow(transform, "BrandCode", "IMAGINARY SECTOR · S07",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ContentLeft, 404f), new Vector2(720f, 22f), TextAnchor.MiddleLeft,
                UITheme.TextFaint);

            // 字标：ui_logo 是 1174×354 的美术字，压进 720×217 正好保持 3.32:1。
            // 取不到图才退回文字，素材丢了也不会空一块
            Sprite logo = ArtLibrary.Get("ui_logo");
            if (logo != null)
            {
                Image image = UIFactory.CreateImage(transform, "Logo", Color.white);
                image.sprite = logo;
                image.preserveAspect = true;
                UIFactory.Place(image.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(ContentLeft, 240f), new Vector2(720f, 217f));
                logoRect = image.rectTransform;
            }
            else
            {
                Text title = UIFactory.CreateText(transform, "Logo", "虚 数 星 区", UITheme.SizeDisplay,
                    TextAnchor.MiddleLeft, UITheme.TextMain, true);
                UIFactory.Place(title.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(ContentLeft, 240f), new Vector2(760f, 120f));
                UIFactory.AddShadow(title, UITheme.ShadowColor, new Vector2(0f, -6f));
                logoRect = title.rectTransform;
            }

            ruleRect = UIChrome.Rule(transform, Vector2.zero, 600f, UITheme.Edge);
            UIFactory.Place(ruleRect.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ContentLeft, 104f), Vector2.zero);

            subtitleCn = UIChrome.CreateLabel(transform, "SubtitleCn", "赛博朋克太空射击",
                UITheme.SizeH2, TextAnchor.MiddleLeft, UITheme.TextMain,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ContentLeft, 58f), new Vector2(660f, 46f), true);

            subtitleEn = UIChrome.Eyebrow(transform, "SubtitleEn", "CYBERPUNK SPACE SHOOTER",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ContentLeft, 20f), new Vector2(660f, 22f), TextAnchor.MiddleLeft,
                UITheme.A(UITheme.Cyan, 0.42f));

            // 署名：只留三个人，前面加 BY（学校/班级/课程信息不再出现）
            UIChrome.CreateLabel(transform, "Meta1", "BY　伊全智　陈玉华　李秋婉",
                UITheme.SizeLabel, TextAnchor.MiddleLeft, UITheme.TextDim,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ContentLeft, -60f), new Vector2(720f, 26f));
        }

        // ------------------------------------------------------------ 左下：战机展示

        /// <summary>
        /// 当前战机展示位。
        ///
        /// 不用场景里那架真战机：面板是 UI（canvas 坐标），真战机是世界坐标，
        /// 两者之间隔着 CanvasScaler 的缩放系数，屏幕宽高比一变就错位。
        /// 只有把战机也变成 UI，对齐才是确定的，不依赖窗口比例。
        /// </summary>
        private void BuildShowcase()
        {
            Vector2 anchor = new Vector2(0f, 0.5f);
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            // 停机位背光：深色机身压在深色星空上会糊掉，垫一层随船色走的柔光。
            // 高度只给 96：上一版是 214，整块下半屏都在发光，读起来像"背景漏了一块"，
            // 而不是"飞机停在台面上"。
            shipGlow = UIFactory.CreateImage(transform, "ShipShowcaseGlow", UITheme.A(UITheme.Cyan, 0.2f));
            shipGlow.sprite = SpriteFactory.Glow();
            shipGlow.type = Image.Type.Simple;
            UIFactory.Place(shipGlow.gameObject, anchor, pivot,
                new Vector2(264f, -392f), new Vector2(228f, 66f));

            // 展台：中间一段亮线 + 两侧引线，和机库卡片里的那套一样
            shipPadCore = UIChrome.Hairline(transform, "ShipPadCore", anchor, pivot,
                new Vector2(264f, -394f), 100f, UITheme.A(UITheme.Cyan, 0.55f), 2f);

            shipPadWingL = UIChrome.Hairline(transform, "ShipPadWingL", anchor, pivot,
                new Vector2(186f, -394f), 56f, UITheme.A(UITheme.Cyan, 0.18f));

            shipPadWingR = UIChrome.Hairline(transform, "ShipPadWingR", anchor, pivot,
                new Vector2(342f, -394f), 56f, UITheme.A(UITheme.Cyan, 0.18f));

            shipImage = UIFactory.CreateImage(transform, "ShipShowcase", Color.white);
            shipImage.preserveAspect = true;
            UIFactory.Place(shipImage.gameObject, anchor, pivot,
                new Vector2(264f, -286f), new Vector2(300f, 214f));

            // 区块标记：左侧一道短竖条，和面板里每个 eyebrow 的读法一致
            Image loadoutMark = UIFactory.CreateImage(transform, "LoadoutMark", UITheme.A(UITheme.Cyan, 0.7f));
            UIFactory.Place(loadoutMark.gameObject, anchor, pivot,
                new Vector2(417f, -258f), new Vector2(2f, 14f));

            UIChrome.Eyebrow(transform, "LoadoutCaption", "CURRENT LOADOUT",
                anchor, anchor, new Vector2(430f, -258f), new Vector2(420f, 22f), TextAnchor.MiddleLeft,
                UITheme.A(UITheme.Cyan, 0.38f));

            shipName = UIChrome.CreateLabel(transform, "ShipName", "疾风",
                UITheme.SizeH3, TextAnchor.MiddleLeft, UITheme.TextMain,
                anchor, anchor, new Vector2(430f, -298f), new Vector2(460f, 38f), true);

            // 呼号用等宽 + 拉字距：机库里同一个呼号也是这么排的，两处读法才对得上
            shipCall = UIChrome.CreateLabel(transform, "ShipCall", "F-01",
                16, TextAnchor.MiddleLeft, UITheme.TextFaint,
                anchor, anchor, new Vector2(430f, -332f), new Vector2(460f, 24f), false, true);
        }

        // ------------------------------------------------------------ 右侧功能列表

        private void BuildMenu()
        {
            // 栏目名：中文不用等宽（等宽字体没有汉字，会变方块），只把字距拉开
            UIChrome.CreateLabel(transform, "MenuCaption", "主　菜　单",
                UITheme.SizeLabel, TextAnchor.MiddleRight, UITheme.TextDim,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-RowRight, 404f), new Vector2(RowWidth, 26f));

            UIChrome.Hairline(transform, "MenuRule", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-RowRight, 372f), RowWidth, UITheme.EdgeSoft);

            string[] labels = { "开始游戏", "敌人图鉴", "操作说明", "设置", "退出游戏" };
            string[] metas = { "START", "CODEX", "BRIEF", "CONFIG", "EXIT" };
            UnityEngine.Events.UnityAction[] handlers =
            {
                OnStartClicked, OnCodexClicked, OnHelpClicked, OnSettingsClicked, OnQuitClicked
            };

            float rowX = 960f - RowRight - RowWidth * 0.5f;   // 画布中心坐标系下的行中心
            rows = new UIAction[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                float y = RowTopY - RowStep * i;
                UIAction row = UIChrome.Action(transform, "Btn" + labels[i], "0" + (i + 1),
                    labels[i], metas[i], new Vector2(RowWidth, RowHeight), new Vector2(rowX, y));
                row.Button.onClick.AddListener(handlers[i]);
                rows[i] = row;
            }

            // 这里刻意**不给「开始游戏」任何常驻提亮**。
            //
            // 曾经给首行单独加了 AccentWash 底 + 青色描边 + 白字加粗，本意是"标出主行动"，
            // 结果整列里只有它一直亮着一块底：鼠标还没动，它看起来就已经被选中了，
            // 于是真正的悬停反馈（fill 铺底、竖条变宽、箭头右移）反而读不出来，
            // 玩家分不清"现在选中的是哪一行"。主行动靠位置表达就够了——
            // 它在第一行、序号是 01，不需要再用一块常亮的底去喊。

            // 退出前面拉一条线：列尾的"危险出口"和上面五个人分开
            UIChrome.Hairline(transform, "QuitRule", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-RowRight, -204f), RowWidth, UITheme.EdgeHair);

            quitLabel = rows[4].Label;
            quitFx = rows[4].Fx;

            // 页脚只写真正存在的操作：主菜单没有方向键焦点（那是 LevelSelectPanel 才有的），
            // 写上「↑↓ 选择」等于承诺了一个按下去没反应的键
            hintText = UIChrome.CreateLabel(transform, "MenuHint", "点击选择 · 回车确认",
                UITheme.SizeCaption, TextAnchor.MiddleRight, UITheme.TextFaint,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-RowRight, -252f), new Vector2(RowWidth, 24f));
        }

        // ------------------------------------------------------------ 页脚

        private void BuildFooter()
        {
            UIChrome.Hairline(transform, "FooterRule", Center, Center,
                new Vector2(0f, -428f), 1760f, UITheme.EdgeHair);

            teamText = UIChrome.CreateLabel(transform, "Team", "虚数星区 · 出品",
                UITheme.SizeCaption, TextAnchor.MiddleLeft, UITheme.TextFaint,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ContentLeft, -458f), new Vector2(760f, 24f));

            // 中文标签 + 等宽数字分开两段：「历史最高」不能用等宽字体渲染（Consolas 没有汉字）
            bestLabel = UIChrome.CreateLabel(transform, "BestLabel", "历史最高",
                UITheme.SizeCaption, TextAnchor.MiddleRight, UITheme.TextFaint,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-330f, -458f), new Vector2(260f, 24f));

            bestValue = UIFactory.CreateTextMono(transform, "BestValue", "000000",
                UITheme.SizeLabel, TextAnchor.MiddleRight, UITheme.TextMain);
            UIFactory.Place(bestValue.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-RowRight, -458f), new Vector2(240f, 28f));
        }

        // ------------------------------------------------------------ 出场序列

        /// <summary>
        /// 时间表：从上往下、从左往右。
        /// 字标先落定，副标和细线跟上，右侧六行一条条抬起来，最后收战机展示位和页脚。
        /// </summary>
        private void BuildIntro()
        {
            intro = gameObject.AddComponent<MenuIntro>();

            intro.Add(codeText.rectTransform, 0.04f, 0.28f, new Vector2(0f, -8f));
            intro.Add(logoRect, 0f, 0.62f, new Vector2(0f, -26f), 1.04f, 1f);
            intro.Add(ruleRect, 0.34f, 0.24f, Vector2.zero);
            intro.Add(subtitleCn.rectTransform, 0.24f, 0.30f, new Vector2(0f, -12f));
            intro.Add(subtitleEn.rectTransform, 0.29f, 0.30f, new Vector2(0f, -12f));

            for (int i = 0; i < rows.Length; i++)
            {
                // 行与行之间 45ms：比按钮矮一点，读起来是"一列在展开"而不是"六个独立地弹"
                intro.Add(rows[i].Root, 0.44f + i * 0.045f, 0.26f, new Vector2(18f, 0f));
            }

            intro.Add(hintText.rectTransform, 0.74f, 0.24f, new Vector2(0f, -8f));
            intro.Add(shipImage.rectTransform, 0.56f, 0.32f, new Vector2(0f, -16f), 0.94f);
            intro.Add(shipName.rectTransform, 0.62f, 0.28f, new Vector2(0f, -10f));
            intro.Add(shipCall.rectTransform, 0.65f, 0.28f, new Vector2(0f, -10f));
            intro.Add(teamText.rectTransform, 0.80f, 0.24f, new Vector2(0f, -8f));
            intro.Add(bestLabel.rectTransform, 0.80f, 0.24f, new Vector2(0f, -8f));
            intro.Add(bestValue.rectTransform, 0.82f, 0.24f, new Vector2(0f, -8f));
        }

        // ------------------------------------------------------------ 按钮回调

        /// <summary>由 GameManager 的回车处理调用：和点按钮走同一条路</summary>
        public void RequestStart()
        {
            OnStartClicked();
        }

        /// <summary>
        /// 「开始游戏」不直接开打，先过一层选关。
        /// 所有"进战斗"的路径（这个入口、机库的「出击」、键盘回车）都汇到选关面板，
        /// 只留一个入口，就不会出现"鼠标要点两下、键盘一下就进星区 01"的不一致。
        /// </summary>
        private void OnStartClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            OpenLevelSelect();
        }

        private void OpenLevelSelect()
        {
            if (levelSelect == null)
            {
                if (GameManager.Instance != null) GameManager.Instance.StartGame();
                return;
            }

            // 先收菜单再开面板（反了会被自己的 SetVisible 立刻关掉）
            SetVisible(false);
            levelSelect.Open();
            levelOpen = true;
        }

        private void CloseLevelSelect()
        {
            levelOpen = false;
            if (levelSelect != null) levelSelect.SetVisible(false);
            SetVisible(true);
        }

        private void OnLevelChosen(int levelIndex, bool bossOnly)
        {
            levelOpen = false;
            if (levelSelect != null) levelSelect.SetVisible(false);

            GameManager game = GameManager.Instance;
            if (game == null) return;

            if (bossOnly) game.StartBossRush();
            else game.StartGameAt(levelIndex);
        }


        private void OnSettingsClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (settings == null) return;

            SetVisible(false);
            settings.Open();
            settingsOpen = true;
        }

        private void OnHelpClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (help == null) return;

            SetVisible(false);
            help.Open();
            helpOpen = true;
        }

        private void CloseHelp()
        {
            helpOpen = false;
            if (help != null) help.SetVisible(false);
            SetVisible(true);
        }

        private void OnCodexClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (codex == null) return;

            SetVisible(false);
            codex.Open();
            codexOpen = true;
        }

        private void CloseCodex()
        {
            codexOpen = false;
            if (codex != null) codex.SetVisible(false);
            SetVisible(true);
        }

        private void OnQuitClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            if (Time.unscaledTime > quitArmedUntil)
            {
                quitArmedUntil = Time.unscaledTime + QuitArmWindow;
                SetQuitArmed(true);
                return;
            }

            if (GameManager.Instance != null) GameManager.Instance.QuitGame();
        }

        private void SetQuitArmed(bool armed)
        {
            if (!armed) quitArmedUntil = 0f;
            if (quitLabel != null) quitLabel.text = armed ? QuitLabelArmed : QuitLabelIdle;
            if (quitFx != null) quitFx.SetAlert(armed);
        }


        private void CloseSettings()
        {
            settingsOpen = false;
            SetVisible(true);
        }

        // ------------------------------------------------------------ 显隐

        public override void SetVisible(bool visible)
        {
            base.SetVisible(visible);

            if (visible)
            {
                UpdateShip();
                PlayIntro();
                return;
            }

            hiddenAt = Time.unscaledTime;

            settingsOpen = false;
            helpOpen = false;
            levelOpen = false;
            codexOpen = false;
            if (settings != null) settings.SetVisible(false);
            if (help != null) help.SetVisible(false);
            if (levelSelect != null) levelSelect.SetVisible(false);
            if (codex != null) codex.SetVisible(false);
            SetQuitArmed(false);
        }

        /// <summary>
        /// 出场序列要不要重播：离开不到 1.2 秒就回来的（反复开关设置、进机库看一眼就退）直接落终态，
        /// 一秒的排队动作重复三遍就成了噪音。
        /// </summary>
        private void PlayIntro()
        {
            if (intro == null) return;

            if (IntroDeferred)
            {
                intro.Hide();
                return;
            }

            if (Time.unscaledTime - hiddenAt > IntroReplayGap) intro.Play();
            else intro.Finish();
        }

        public void UpdateBest(int highScore)
        {
            if (bestValue != null) bestValue.text = highScore.ToString("D6");
        }

        public void UpdateShip()
        {
            ShipConfig ship = PlayerLoadout.Selected;

            if (shipImage != null)
            {
                shipImage.sprite = ship != null
                    ? ShipArt.Get(ship)
                    : SpriteFactory.PlayerShip();
            }

            if (ship != null)
            {
                if (shipGlow != null) shipGlow.color = UITheme.A(ship.accent, 0.17f);
                if (shipPadCore != null) shipPadCore.color = UITheme.A(ship.accent, 0.55f);
                if (shipPadWingL != null) shipPadWingL.color = UITheme.A(ship.accent, 0.18f);
                if (shipPadWingR != null) shipPadWingR.color = UITheme.A(ship.accent, 0.18f);
            }

            if (shipName != null)
            {
                shipName.text = ship != null ? ship.displayName : "—";
            }

            if (shipCall != null)
            {
                shipCall.text = ship != null ? UIFactory.SpaceOut(ship.callsign) : "";
            }
        }

        protected override void Update()
        {
            base.Update();

            if (group == null || group.alpha <= 0.01f) return;

            // 退出确认的超时复原：过了窗口还没点第二下，就当作放弃
            if (quitArmedUntil > 0f && Time.unscaledTime > quitArmedUntil) SetQuitArmed(false);

            pulse += Time.unscaledDeltaTime;

            // 字标极轻微地上下浮动，画面就不会完全死掉。
            // 位置不在这里写：出场动画也在动同一批 anchoredPosition，
            // 这里只把偏移量交给 MenuIntro，由它统一写
            if (intro != null) intro.Bob = new Vector2(0f, Mathf.Sin(pulse * 1.7f) * 4f);

            // 左侧强调竖线呼吸：只有亮度在变，位置不动，和字标浮动错开节奏
            if (accentLine != null)
            {
                accentLine.color = UITheme.A(UITheme.Cyan, 0.22f + Mathf.Sin(pulse * 1.1f) * 0.10f);
            }

            // 顶部标尺线随呼吸微微提亮：整屏"活着"的感觉来自这些看不见的细节
            if (frameLine != null)
            {
                frameLine.color = UITheme.A(UITheme.Cyan, 0.06f + Mathf.Sin(pulse * 0.8f) * 0.02f);
            }
        }
    }
}
