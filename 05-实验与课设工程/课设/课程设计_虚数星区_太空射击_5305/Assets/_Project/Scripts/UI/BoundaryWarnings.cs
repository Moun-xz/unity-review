using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 左右边界预警：玩家靠近可活动区边缘时，屏幕两侧的红色渐变带逐渐亮起来。
    ///
    /// v2 的克制之处：
    ///   不再用两条 76px 的红块。改成 48px 宽的 EdgeGradient——靠屏幕边缘最浓、向内衰减到 0，
    ///   再加一条 1px 的亮边。正常情况下几乎看不见（8%），只有真的贴到墙根才推到 30%。
    ///   玩家应该感觉到"边界在推我"，而不是看到"一块红屏"。
    ///
    /// 数据来源：Refs.PlayerPosition.x 对 GameArea.PlayerLimitX 的比例，从 70% 开始预警。
    /// 只在 Playing/Paused 显示：菜单和结算里屏幕上没有可撞的边界。
    /// </summary>
    public class BoundaryWarnings : UIPanelBase
    {
        private const float BandWidth = 48f;
        private const float BandHeight = 1100f;
        private const float IdleAlpha = 0.08f;
        private const float HotAlpha = 0.30f;
        private const float EdgeIdleAlpha = 0.10f;
        private const float EdgeHotAlpha = 0.42f;

        /// <summary>从活动区宽度的 70% 处开始预警，最后 30% 才从无到有。</summary>
        private const float WarnStart = 0.70f;

        private const float SmoothSpeed = 8f;

        private Image leftBand;
        private Image rightBand;
        private Image leftEdge;
        private Image rightEdge;

        private float leftHot;
        private float rightHot;

        public void Build()
        {
            InitGroup(false);

            Sprite gradient = SpriteFactory.EdgeGradient();

            // 左带：锚左、pivot 在左缘，亮部贴屏幕边、向内衰减
            leftBand = UIFactory.CreateImage(transform, "BandLeft", UITheme.A(UITheme.Danger, IdleAlpha));
            leftBand.sprite = gradient;
            leftBand.type = Image.Type.Simple;
            UIFactory.Place(leftBand.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(BandWidth, BandHeight));

            // 右带：锚右、pivot 在右缘，再 localScale.x = -1 把贴图镜像过来
            rightBand = UIFactory.CreateImage(transform, "BandRight", UITheme.A(UITheme.Danger, IdleAlpha));
            rightBand.sprite = gradient;
            rightBand.type = Image.Type.Simple;
            UIFactory.Place(rightBand.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(BandWidth, BandHeight));
            rightBand.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            // 1px 亮边：渐变带是"面"，亮边负责在贴墙那一下给出清楚的几何边界
            leftEdge = UIChrome.VLine(transform, "EdgeLeft",
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(1.5f, 0f), 1080f, UITheme.A(UITheme.Danger, EdgeIdleAlpha), 1f);

            rightEdge = UIChrome.VLine(transform, "EdgeRight",
                new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-1.5f, 0f), 1080f, UITheme.A(UITheme.Danger, EdgeIdleAlpha), 1f);
        }

        // ------------------------------------------------------------ 状态

        private void OnEnable()
        {
            EventBus.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            EventBus.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            // OnEnable 可能在 Build 之前就跑（AddComponent 立刻触发），group 还没建好
            if (group == null) return;
            SetVisible(state == GameState.Playing || state == GameState.Paused);
        }

        // ------------------------------------------------------------ 每帧

        protected override void Update()
        {
            base.Update();

            float left = 0f;
            float right = 0f;

            if (Refs.Player != null)
            {
                float limit = GameArea.PlayerLimitX;
                if (limit > 0.001f)
                {
                    float normalized = Refs.PlayerPosition.x / limit;
                    left = Mathf.Clamp01((-normalized - WarnStart) / (1f - WarnStart));
                    right = Mathf.Clamp01((normalized - WarnStart) / (1f - WarnStart));
                }
            }

            // 平滑一下：撞墙时的位移是突变的，直接跟会闪；8/s 的指数趋近刚好够快又不刺眼
            float k = 1f - Mathf.Exp(-SmoothSpeed * Time.unscaledDeltaTime);
            leftHot = Mathf.Lerp(leftHot, left, k);
            rightHot = Mathf.Lerp(rightHot, right, k);

            Apply(leftHot, rightHot);
        }

        private void Apply(float left, float right)
        {
            if (leftBand != null)
            {
                leftBand.color = UITheme.A(UITheme.Danger, Mathf.Lerp(IdleAlpha, HotAlpha, left));
            }

            if (rightBand != null)
            {
                rightBand.color = UITheme.A(UITheme.Danger, Mathf.Lerp(IdleAlpha, HotAlpha, right));
            }

            if (leftEdge != null)
            {
                leftEdge.color = UITheme.A(UITheme.Danger, Mathf.Lerp(EdgeIdleAlpha, EdgeHotAlpha, left));
            }

            if (rightEdge != null)
            {
                rightEdge.color = UITheme.A(UITheme.Danger, Mathf.Lerp(EdgeIdleAlpha, EdgeHotAlpha, right));
            }
        }
    }
}
