using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 开机第一屏：一张关键帧 + 左侧品牌块 + 右下角「按任意键」。
    ///
    /// v2「战术终端」版式：
    ///   左边一列是信息块——字标、英文微标、中文主标题、一行副标、一条强调细线；
    ///   右下角是唯一的动作提示（键盘帽 + 呼吸文案），中间大片区域留给背景里的飞船与行星。
    ///   四角取景框 + 中缝一条极淡斜线，把整屏读成一块仪表板，而不是一张海报上贴了几个按钮。
    ///
    /// 和 BootSplash 的分工不变：片头是纯色上的字标，这一屏才开始讲故事，并且等玩家操作。
    ///
    /// 为什么它不继承 UIPanelBase：
    ///   UIPanelBase 是给「会被反复开关的面板」用的（淡入淡出 + 24px 上滑），
    ///   这一屏一辈子只出现一次，出场节奏要自己控（MenuIntro 时间表）。
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        // ---- 版式常量：左列一个信息块，右下角一个动作区 ----

        private const float LogoWidth = 700f;
        private const float LogoHeight = 210f;
        private const float LogoX = -570f;
        private const float LogoY = 300f;

        /// <summary>左列文字的实体左缘。都挂 (0,0.5) 的 pivot，x 给的就是左缘。</summary>
        private const float Left = -884f;
        private const float EyebrowY = 166f;
        // 大标题已删：logo 本身就是标题，再排一遍「虚数星区」是重复信息
        private const float SubtitleY = 112f;
        private const float AccentY = 58f;

        /// <summary>右下角动作区统一以屏幕右缘为基准（pivot (1,0.5)）。</summary>
        private const float Right = 880f;
        private const float PromptY = -344f;
        private const float KeyCapY = -404f;
        private const float FooterY = -492f;

        /// <summary>出场全部落定的时刻。落定之后才接键盘，避免"还没看清就被按进去"</summary>
        private const float IntroDuration = 0.92f;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private CanvasGroup group;
        private Image backdrop;
        private Text hint;
        private MenuIntro intro;

        private float clock;
        private float drift;
        private bool playing;
        private bool accepted;

        /// <summary>按任意键（或点击）。由 GameBootstrap 接过去播跃迁</summary>
        public Action EnterRequested;

        public void Build()
        {
            group = UIFactory.AddCanvasGroup(gameObject);

            BuildBackdrop();
            BuildBrandBlock();
            BuildScreenFrame();
            BuildPressHint();
            BuildFooter();
            BuildIntro();

            // 刚建出来时背景已经在，但内容压在起点：开机画面还盖在上面，
            // 要等它散尽（GameBootstrap 调 PlayIntro）这一屏才开始出场。
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            playing = false;
        }

        // ------------------------------------------------------------ 各块

        /// <summary>
        /// 关键帧背景。tint 必须是白的：Image.color 是乘在贴图上的，
        /// 写成深色会把 1920×1080 的剧照压到几乎全黑。
        /// 深色只在素材丢失、退回程序化贴图时当底色用。
        /// </summary>
        private void BuildBackdrop()
        {
            Sprite art = ArtLibrary.Get("bg_title");

            if (art != null)
            {
                backdrop = UIFactory.CreateImage(transform, "Backdrop", UITheme.TextMain);
                backdrop.sprite = art;
            }
            else
            {
                backdrop = UIFactory.CreateImage(transform, "Backdrop",
                    UITheme.A(UITheme.SurfaceDeep, 1f));
                backdrop.sprite = SpriteFactory.Backdrop();
            }

            backdrop.raycastTarget = false;
            UIFactory.Stretch(backdrop.rectTransform);
        }

        /// <summary>
        /// 左侧品牌块：字标图 → 英文微标 → 中文主标题 → 副标 → 强调细线。
        ///
        /// 主标题用 SizeDisplay（84）是全屏唯一一个这个字号，避免"两个大字互相打架"。
        /// 英文品牌微标用 Eyebrow（等宽粗体、紧凑不拉字距，见 UI_STYLE §21）：
        /// 读起来像设备型号，不像第二标题；中文功能标签不走这里，走黑体粗体。
        /// </summary>
        private void BuildBrandBlock()
        {
            Sprite logo = ArtLibrary.Get("ui_logo");

            if (logo != null)
            {
                Image image = UIFactory.CreateImage(transform, "Logo", UITheme.TextMain);
                image.sprite = logo;
                image.preserveAspect = true;
                image.raycastTarget = false;
                UIFactory.Place(image.gameObject, Center, Center,
                    new Vector2(LogoX, LogoY), new Vector2(LogoWidth, LogoHeight));
            }
            else
            {
                // 素材丢了也不能空一块：退回字标文字
                Text fallback = UIFactory.CreateText(transform, "Logo", "虚 数 星 区", UITheme.SizeH1,
                    TextAnchor.MiddleCenter, UITheme.TextMain, true);
                UIFactory.Place(fallback.gameObject, Center, Center,
                    new Vector2(LogoX, LogoY), new Vector2(LogoWidth, LogoHeight));
                UIFactory.AddOutline(fallback, UITheme.A(UITheme.Cyan, 0.35f), new Vector2(2f, -2f));
            }

            UIChrome.Eyebrow(transform, "Eyebrow", "IMAGINARY SECTOR / S07",
                Center, new Vector2(0f, 0.5f), new Vector2(Left, EyebrowY),
                new Vector2(760f, 22f), TextAnchor.MiddleLeft, UITheme.A(UITheme.Cyan, 0.78f));


            UIChrome.CreateLabel(transform, "Subtitle", "赛博朋克 · 太空射击", UITheme.SizeBody,
                TextAnchor.MiddleLeft, UITheme.TextDim, Center, new Vector2(0f, 0.5f),
                new Vector2(Left, SubtitleY), new Vector2(760f, 36f));

            // 强调细线：从标题左缘伸出来，长度只到标题宽度的一半，不横穿整屏
            UIChrome.Hairline(transform, "Accent", Center, new Vector2(0f, 0.5f),
                new Vector2(Left, AccentY), 420f, UITheme.A(UITheme.Cyan, 0.55f), 2f);
        }

        /// <summary>
        /// 全屏取景框：四角短括号 + 一条中缝斜线 + 一条极淡的竖中缝。
        /// 都画在内容之前，保证不盖住字标与提示。
        /// </summary>
        private void BuildScreenFrame()
        {
            RectTransform root = (RectTransform)transform;

            UIChrome.CornerBrackets(root, UICorner.TopLeft, UICorner.BottomRight,
                UITheme.A(UITheme.Cyan, 0.30f), 46f, 2f, 40f);
            UIChrome.CornerBrackets(root, UICorner.TopRight, UICorner.BottomLeft,
                UITheme.A(UITheme.Cyan, 0.16f), 28f, 1f, 40f);

            // 中缝：一条 24° 的极淡斜线 + 一条竖线，把画面切成"取景器"
            Image diagonal = UIFactory.CreateImage(transform, "Diagonal",
                UITheme.A(UITheme.Cyan, 0.07f));
            UIFactory.Place(diagonal.gameObject, Center, Center,
                new Vector2(140f, 0f), new Vector2(1f, 980f));
            diagonal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);


            // 右上角一行系统标识，和左下品牌块形成对角
            UIChrome.Eyebrow(transform, "SysTag", "SYS / ONLINE", Center,
                new Vector2(1f, 0.5f), new Vector2(Right, 496f),
                new Vector2(320f, 20f), TextAnchor.MiddleRight, UITheme.TextFaint);
        }

        /// <summary>
        /// 右下角：键帽 + 呼吸提示。
        ///
        /// 这一屏没有按钮——整块屏都是按钮，靠「按任意键」进入。
        /// 键帽给键盘玩家一个明确的按键，呼吸文案给"这里还能继续"的生命感，
        /// 两者都用 UIBreathe / 静态细边，不参与 MenuIntro 的位移写入。
        /// </summary>
        private void BuildPressHint()
        {
            Image cap = UIFactory.CreateImage(transform, "EnterCap",
                UITheme.A(UITheme.SurfaceRaised, 0.96f));
            cap.sprite = UISprites.Sheet(UITheme.CutChip);
            cap.type = Image.Type.Sliced;
            UIFactory.Place(cap.gameObject, Center, new Vector2(1f, 0.5f),
                new Vector2(Right, KeyCapY), new Vector2(150f, 44f));

            Image capEdge = UIFactory.CreateImage(cap.transform, "Edge",
                UITheme.A(UITheme.Cyan, 0.42f));
            capEdge.sprite = UISprites.SheetEdge(UITheme.CutChip, 1.2f);
            capEdge.type = Image.Type.Sliced;
            UIFactory.Stretch(capEdge.rectTransform);

            UIChrome.CreateLabel(cap.transform, "Key", "ENTER", UITheme.SizeLabel,
                TextAnchor.MiddleCenter, UITheme.CyanBright, Center, Center,
                Vector2.zero, new Vector2(136f, 30f), true, true);

            // 键帽左边的辅助说明：键盘玩家看键帽，鼠标玩家看这句
            UIChrome.CreateLabel(transform, "ClickTip", "或 点 击 屏 幕",
                UITheme.SizeCaption, TextAnchor.MiddleRight, UITheme.TextFaint,
                Center, new Vector2(1f, 0.5f),
                new Vector2(Right - 168f, KeyCapY), new Vector2(360f, 26f));

            // 主提示：这一屏唯一要让人看见的动作用语，字号跟标题屏其它主文案同级
            hint = UIChrome.CreateLabel(transform, "Hint", "按 任 意 键 进 入", UITheme.SizeH2,
                TextAnchor.MiddleRight, UITheme.TextMain, Center, new Vector2(1f, 0.5f),
                new Vector2(Right, PromptY), new Vector2(640f, 48f), true);

            // 呼吸：alpha 在 0.55~1 之间走，比整块文字闪更有"待机"的味道
            UIBreathe breathe = hint.gameObject.AddComponent<UIBreathe>();
            breathe.target = hint;
            breathe.speed = 1.6f;
            breathe.min = 0.55f;
            breathe.max = 1f;

            // 提示下面压一条很淡的动势线，把"待进入"的感觉往画面右侧带
            UIChrome.Hairline(transform, "HintRule", Center, new Vector2(1f, 0.5f),
                new Vector2(Right, PromptY - 34f), 300f, UITheme.A(UITheme.Cyan, 0.22f));
        }

        private void BuildFooter()
        {
            // 页脚有中文，必须走普通字体：MonoFont 是 Consolas，没有 CJK 字形。
            UIChrome.CreateLabel(transform, "Footer", "BY　伊全智　陈玉华　李秋婉",
                UITheme.SizeLabel, TextAnchor.MiddleLeft, UITheme.TextDim,
                Center, new Vector2(0f, 0.5f), new Vector2(Left, FooterY),
                new Vector2(760f, 26f));
        }

        // ------------------------------------------------------------ 出场

        /// <summary>
        /// 时间表：字标先从 105% 缩回，英文微标与主标题跟着落位，
        /// 副标和细线补上，最后才是右下角的动作区。顺序就是眼睛从上往下的顺序。
        /// </summary>
        private void BuildIntro()
        {
            intro = gameObject.AddComponent<MenuIntro>();

            intro.Add(Find("Logo"), 0f, 0.62f, new Vector2(0f, -26f), 1.05f, 1f);
            intro.Add(Find("Eyebrow"), 0.20f, 0.30f, new Vector2(0f, -12f), 1f, 1f);
            intro.Add(Find("Subtitle"), 0.26f, 0.30f, new Vector2(0f, -10f));
            intro.Add(Find("Accent"), 0.38f, 0.26f, new Vector2(0f, -8f));
            intro.Add(Find("EnterCap"), 0.50f, 0.28f, new Vector2(0f, -10f));
            intro.Add(Find("Hint"), 0.52f, 0.28f, new Vector2(0f, -10f));
            intro.Add(Find("Footer"), 0.62f, 0.26f, new Vector2(0f, -6f));
        }

        private RectTransform Find(string name)
        {
            Transform child = transform.Find(name);
            return child == null ? null : child as RectTransform;
        }

        /// <summary>开机画面散尽之后调用</summary>
        public void PlayIntro()
        {
            playing = true;
            clock = 0f;
            group.blocksRaycasts = true;
            group.interactable = true;
            if (intro != null) intro.Play();
        }

        /// <summary>
        /// 跃迁开始时收掉这一屏。
        /// 不做淡出：白光已经打到全屏，硬切看不出来，反而省掉一次"半透明还在接点击"的隐患。
        /// </summary>
        public void Hide()
        {
            accepted = true;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            enabled = false;
        }

        // ------------------------------------------------------------ 输入

        private void Accept()
        {
            if (accepted || !playing) return;
            accepted = true;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (EnterRequested != null) EnterRequested();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // 极慢的推镜：背景是一张静态剧照，完全不动会像贴了张壁纸。
            // 3% 的缩放 + 十几像素的漂移，30 秒一个来回——能感觉到"在动"，但不会分神。
            drift += dt;
            if (backdrop != null)
            {
                float s = 1.030f + Mathf.Sin(drift * 0.088f) * 0.012f;
                backdrop.rectTransform.localScale = new Vector3(s, s, 1f);
                backdrop.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Sin(drift * 0.107f) * 12f,
                    Mathf.Cos(drift * 0.081f) * 9f);
            }

            if (playing)
            {
                clock += dt;

                // 字标跟着呼吸，副标一起走——只动字标的话两者会逐渐错开
                if (intro != null)
                {
                    intro.Bob = new Vector2(0f, Mathf.Sin(clock * 1.9f) * 3f);
                }
            }

            if (!playing || accepted) return;

            // 出场没落定之前不接键盘：玩家很可能一边看动画一边敲回车，
            // 那一下就跳过了整个开场，等于白做。
            if (clock < IntroDuration) return;

            // 「按任意键」就真的是任意键（Input.anyKeyDown 也包含鼠标按键），
            // 不需要再单独给一个按钮区域。
            if (Input.anyKeyDown) Accept();
        }
    }
}
