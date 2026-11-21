using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 结算面板：900×720，左对齐标题块 + 两张并排数据卡。
    ///
    /// 为什么不再是一列居中（旧版 860×760 全是上下堆叠）：
    ///   旧版八行元素全压在中轴上，最宽的分数 720 而其余行只有 520 / 280，
    ///   左右各空 300 多，读起来像一张表单；两个数字上下排还逼着视线来回跳，
    ///   看不出"这局比上次好多少"。改成并排双卡后，比较关系是一眼就能读的横向关系。
    ///
    /// 标题块为什么左对齐：
    ///   居中标题会让"任务完成/失败"变成一枚装饰徽章；左对齐 + 左侧 4px 状态色条，
    ///   读起来是"结论 + 适用条件（哪个星区、哪架机）"，和页眉的编号形成同一条左基准线。
    ///
    /// 分数仍然等宽：滚动计数器每帧改文本长度，比例字体下整块数字会左右抖。
    /// </summary>
    public class ResultPanel : UIPanelBase
    {
        private const float PanelWidth = 900f;
        private const float PanelHeight = 720f;

        /// <summary>分数滚动时长。0.62s 刚好能被眼睛跟上最后三位，再长就变成等动画。</summary>
        private const float ScoreRollDuration = 0.62f;

        /// <summary>新纪录徽标的脉冲频率（弧度/秒）。</summary>
        private const float RecordPulseSpeed = 3.2f;

        // Content 区 = 900-68 × 720-104 = 832 × 616，坐标原点在中心，
        // 因此 x ∈ [-416, 416]、y ∈ [-308, 308]。锚点统一给 Center，
        // 不能用左边缘锚点（x=-416 会变成"左边缘再往左 416"，标题整块跑到面板外）。
        private static readonly Vector2 CardSize = new Vector2(388f, 170f);
        private static readonly Vector2 CardLeft = new Vector2(-222f, 56f);
        private static readonly Vector2 CardRight = new Vector2(222f, 56f);

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private Text titleText;
        private Text statusText;
        private Image accentBar;
        private Text subText;
        private Text scoreValue;
        private Text bestValue;
        private UITag recordTag;

        private string levelName = "";

        private int scoreTarget;
        private float scoreRoll;
        private bool scoreRolling;
        private float recordPhase;

        public void Build()
        {
            InitGroup(false);

            UIChrome.Scrim(transform, "Scrim", UITheme.A(UITheme.SurfaceDeep, 0.86f));

            UISheet sheet = UIChrome.Sheet(transform, "Sheet",
                new Vector2(PanelWidth, PanelHeight), Vector2.zero, UITheme.Cyan, UITheme.CutLarge);

            UIChrome.Header(sheet, "06", "任务结算", "DEBRIEF");
            UIChrome.Footer(sheet, "R 再来一局　·　ESC 返回主菜单", "RESULT");

            Transform content = sheet.Content;

            // 内屏光：面板左上微微发亮，右下自然沉下去
            UIChrome.InnerGlow(content, "InnerGlow", UITheme.Cyan,
                new Vector2(470f, 320f), new Vector2(-110f, 195f), 0.055f);

            // ---- 标题块：色条 + 结论 + 适用条件 ----
            // 色条颜色跟着结论走：原来失败态是红条、胜利态还是红条，标题绿了条还是红的
            accentBar = UIFactory.CreateImage(content, "AccentBar", UITheme.Danger);
            UIFactory.Place(accentBar.gameObject, Center, new Vector2(0f, 0.5f),
                new Vector2(-416f, 232f), new Vector2(4f, 48f));

            titleText = UIChrome.CreateLabel(content, "Result", "任务失败", UITheme.SizeH1,
                TextAnchor.MiddleLeft, UITheme.Danger,
                Center, new Vector2(0f, 0.5f),
                new Vector2(-394f, 232f), new Vector2(460f, 56f), true);
            // 48px 标题和 60px 分数都压在深底上，一层 2px 下投影让文字"立"在面板表面
            UIFactory.AddShadow(titleText, new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -2f));

            // 标题行右端的状态码：把"结论"这一行拉成左右对位的一条。
            // 中文标题说"是什么结果"，等宽状态码说"系统怎么记录它"，两栏都占满，标题区才不偏左。
            statusText = UIChrome.CreateLabel(content, "Status", "MISSION FAILED", UITheme.SizeMicro,
                TextAnchor.MiddleRight, UITheme.A(UITheme.Danger, 0.42f),
                Center, new Vector2(1f, 0.5f),
                new Vector2(416f, 232f), new Vector2(320f, 24f), true, true);

            subText = UIChrome.CreateLabel(content, "Sub", "", UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextMuted,
                Center, new Vector2(0f, 0.5f),
                new Vector2(-394f, 182f), new Vector2(640f, 24f), false);

            // ---- 双数据卡 ----
            // 卡片位置由调用处决定（而不是 BuildCard 内部传参），这样版式的所有坐标都写在
            // 同一个平面上，改动时一眼能看到两张卡的间距，不会被藏进函数参数里。
            GameObject cardScore = BuildCard(content, "CardScore", "本次得分", "01");
            UIFactory.Place(cardScore, Center, Center, CardLeft, CardSize);
            scoreValue = AddCardValue(cardScore, 60, UITheme.CyanBright);

            GameObject cardBest = BuildCard(content, "CardBest", "历史最高", "02");
            UIFactory.Place(cardBest, Center, Center, CardRight, CardSize);
            bestValue = AddCardValue(cardBest, 48, new Color(0.78f, 0.85f, 0.93f));

            // 新纪录徽标贴在右卡的右上角。UITag 内部固定用居中锚点，
            // 所以这里给的是内容区绝对坐标，而不是相对卡片的偏移。
            // 116×34 = 文字 60 + 左右各 28、字号 20 + 上下各 7。原来是 74×28：
            // 左右只剩 7px，正好等于切角，两个字都压在斜边上。加宽后右缘退到 404，
            // 离卡片右缘（416）留 12px，和卡片的圆角切边不打架。
            recordTag = UIChrome.Tag(content, "Record", "新纪录",
                new Vector2(116f, 34f), new Vector2(344f, 114f), UITheme.Amber, true);
            recordTag.Root.gameObject.SetActive(false);

            // 卡片与动作之间不画分隔线：两侧都是空白，一条横线只会把下半屏切成"没写完的一段"。
            // 分区靠留白（64px）表达就够了。

            // ---- 动作 ----
            // 主次同宽：两个不同宽度的框摞在一起，看起来像"随手放的两块"；
            // 同宽之后它们共享一条左右边界，右列读成一个动作块。
            UIAction again = UIChrome.Action(content, "BtnAgain", "", "再来一局", "R",
                new Vector2(340f, 62f), new Vector2(0f, -132f), UIActionStyle.Primary);
            again.Button.onClick.AddListener(OnAgainClicked);

            UIAction menu = UIChrome.Action(content, "BtnMenu", "", "返回主菜单", "",
                new Vector2(340f, 46f), new Vector2(0f, -212f), UIActionStyle.Ghost);
            menu.Button.onClick.AddListener(OnMenuClicked);
        }

        /// <summary>
        /// 卡片外壳：底板 + 描边 + 上下沿厚度 + 左侧短色条 + 左上标题 + 右上编号 + 中间分界线。
        /// 位置由调用方 Place。数值单独用 AddCardValue 加，因为两张卡的字号不一样。
        /// </summary>
        private static GameObject BuildCard(Transform parent, string name, string caption, string code)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image body = go.AddComponent<Image>();
            body.sprite = UISprites.Sheet(UITheme.CutCard);
            body.type = Image.Type.Sliced;
            body.color = UITheme.CardBg;
            body.raycastTarget = false;

            // 上沿受光、下沿积影：卡片因此有厚度，而不是一块被填色的矩形
            UIChrome.CardShade(go.GetComponent<RectTransform>());

            Image edge = UIFactory.CreateImage(go.transform, "Edge", UITheme.EdgeSoft);
            edge.sprite = UISprites.SheetEdge(UITheme.CutCard, 1f);
            edge.type = Image.Type.Sliced;
            UIFactory.Stretch(edge.rectTransform);

            Image mark = UIFactory.CreateImage(go.transform, "Mark", UITheme.A(UITheme.Cyan, 0.55f));
            UIFactory.Place(mark.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(4f, 41f), new Vector2(2f, 26f));

            // 编号放在名目前面（"01 本次得分"），而不是右上角：
            // 右上角是「新纪录」徽标的位置，编号挤在那里会跟动态标签打架。
            UIChrome.CreateLabel(go.transform, "Code", code, UITheme.SizeNano,
                TextAnchor.MiddleLeft, UITheme.A(UITheme.TextMuted, 0.9f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, 41f), new Vector2(24f, 22f), false, true);

            UIChrome.CreateLabel(go.transform, "Caption", caption, UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextFaint,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(48f, 41f), new Vector2(200f, 22f), false);

            // 名目与读数之间一条 1px 分界：卡片被读成"标签栏 / 读数窗"两段
            Image split = UIFactory.CreateImage(go.transform, "Split", UITheme.A(UITheme.Cyan, 0.10f));
            UIFactory.Place(split.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 16f), new Vector2(CardSize.x - 36f, 1f));

            return go;
        }

        /// <summary>卡片主数值：等宽 6 位，左下角对齐。左卡 60 号是主视觉，右卡 48 号做陪衬。</summary>
        private static Text AddCardValue(GameObject card, int valueSize, Color valueColor)
        {
            // 读数窗的光：一层左亮右暗的极淡渐变，数字像是从窗口左侧亮起来的
            Image wash = UIFactory.CreateImage(card.transform, "ValueWash", UITheme.A(valueColor, 0.05f));
            wash.sprite = UISprites.GradientH();
            wash.type = Image.Type.Simple;
            UIFactory.Place(wash.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, -40f), new Vector2(250f, 60f));

            Text value = UIFactory.CreateTextMono(card.transform, "Value", "000000",
                valueSize, TextAnchor.MiddleLeft, valueColor, true);
            UIFactory.Place(value.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, -40f), new Vector2(CardSize.x - 36f, 64f));
            UIFactory.AddShadow(value, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -2f));
            return value;
        }

        // ------------------------------------------------------------ 展示

        /// <summary>由 GameManager.PlayerDied / Victory 调用。签名保持不变。</summary>
        public void Show(bool victory, int score, int best, bool newRecord)
        {
            if (titleText != null)
            {
                titleText.text = victory ? "任务完成" : "任务失败";
                titleText.color = victory ? UITheme.Mint : UITheme.Danger;
            }

            if (statusText != null)
            {
                statusText.text = victory ? "MISSION CLEARED" : "MISSION FAILED";
                statusText.color = UITheme.A(victory ? UITheme.Mint : UITheme.Danger, 0.42f);
            }

            if (accentBar != null)
            {
                accentBar.color = victory ? UITheme.Mint : UITheme.Danger;
            }

            if (subText != null)
            {
                string ship = PlayerLoadout.Selected != null ? PlayerLoadout.Selected.displayName : "";
                string level = string.IsNullOrEmpty(levelName) ? "" : levelName;
                subText.text = string.IsNullOrEmpty(level) ? ship : level + "　·　" + ship;
            }

            scoreTarget = Mathf.Max(0, score);
            scoreRoll = 0f;
            scoreRolling = true;
            if (scoreValue != null) scoreValue.text = "000000";

            if (bestValue != null) bestValue.text = Mathf.Max(0, best).ToString("D6");

            if (recordTag != null)
            {
                recordTag.Root.gameObject.SetActive(newRecord);
                recordTag.Root.localScale = Vector3.one;
                recordPhase = 0f;
                if (newRecord) recordTag.Text.color = UITheme.Amber;
            }

            SetVisible(true);
        }

        public override void SetVisible(bool visible)
        {
            base.SetVisible(visible);

            // 面板收起时别再让计数器跑：下次 Show 会从 0 重新滚，
            // 否则隐藏期间分数会继续爬到上一次的目标值。
            if (!visible) scoreRolling = false;
        }

        private void OnEnable()
        {
            EventBus.LevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            EventBus.LevelChanged -= OnLevelChanged;
        }

        private void OnLevelChanged(string name)
        {
            levelName = name;
        }

        // ------------------------------------------------------------ 动效

        protected override void Update()
        {
            base.Update();

            if (scoreRolling) UpdateScoreRoll();
            if (recordTag != null && recordTag.Root.gameObject.activeSelf) UpdateRecordPulse();
        }

        private void UpdateScoreRoll()
        {
            if (scoreValue == null)
            {
                scoreRolling = false;
                return;
            }

            scoreRoll += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(scoreRoll / ScoreRollDuration);

            // easeOutCubic：数字先冲出去、最后三位慢下来，读起来像机械计数器停表
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            int value = Mathf.RoundToInt(Mathf.Lerp(0f, scoreTarget, eased));
            scoreValue.text = value.ToString("D6");

            if (t >= 1f)
            {
                scoreRolling = false;
                scoreValue.text = scoreTarget.ToString("D6");
            }
        }

        private void UpdateRecordPulse()
        {
            recordPhase += Time.unscaledDeltaTime * RecordPulseSpeed;

            float pulse = (Mathf.Sin(recordPhase) + 1f) * 0.5f;
            float scale = 1f + 0.05f * pulse;
            recordTag.Root.localScale = new Vector3(scale, scale, 1f);
            recordTag.Text.color = UITheme.A(UITheme.Amber, Mathf.Lerp(0.78f, 1f, pulse));
        }

        // ------------------------------------------------------------ 回调

        private void OnAgainClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
        }

        private void OnMenuClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            TimeFlow.Reset();
            if (GameManager.Instance != null) GameManager.Instance.ReturnToMenu();
        }
    }
}
