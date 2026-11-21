using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 面板基类：统一管理显隐、淡入淡出，以及「进来的那一下」位移。
    ///
    /// 三个关键决定：
    /// 1) 用 CanvasGroup.alpha 而不是 SetActive(false)。
    ///    SetActive(false) 会触发 OnDisable → 面板退订 EventBus，
    ///    再次显示时就接不到期间发生的事件（血条会停在旧数值上）。
    /// 2) 淡入用 unscaledDeltaTime 推进。
    ///    暂停面板打开时 Time.timeScale = 0，用 deltaTime 的话它就永远淡不进来。
    /// 3) 位移也用 unscaledDeltaTime，理由同上；而且位移距离只有 24px，
    ///    再大就成了"从屏幕外飞进来"，在暂停/结算这种频繁开关的面板上会烦。
    /// </summary>
    public abstract class UIPanelBase : MonoBehaviour
    {
        protected CanvasGroup group;

        private float targetAlpha;
        private const float FadeSpeed = 9f;

        private RectTransform rect;
        private Vector2 home;
        private float slide;
        private float slideTarget;

        private const float SlideDistance = 24f;
        private const float SlideSpeed = 420f;      // 单位/秒，24px 约 0.06 秒走完

        /// <summary>子类在 Build 里调用一次，创建并初始化 CanvasGroup</summary>
        protected void InitGroup(bool startVisible)
        {
            group = UIFactory.AddCanvasGroup(gameObject);
            group.alpha = startVisible ? 1f : 0f;
            targetAlpha = group.alpha;
            group.blocksRaycasts = startVisible;
            group.interactable = startVisible;

            rect = transform as RectTransform;
            if (rect != null) home = rect.anchoredPosition;
            slide = startVisible ? 0f : -SlideDistance;
            slideTarget = slide;
            ApplySlide();
        }

        public virtual void SetVisible(bool visible)
        {
            targetAlpha = visible ? 1f : 0f;

            if (group == null) return;
            group.blocksRaycasts = visible;
            group.interactable = visible;

            slideTarget = visible ? 0f : -SlideDistance;

            // 已经淡到看不见了才把起点拉回下方，否则半透明时重开会「跳」一下
            if (visible && group.alpha <= 0.02f) slide = -SlideDistance;
            ApplySlide();
        }

        protected virtual void Update()
        {
            if (group == null) return;

            if (!Mathf.Approximately(slide, slideTarget))
            {
                slide = Mathf.MoveTowards(slide, slideTarget, Time.unscaledDeltaTime * SlideSpeed);
                ApplySlide();
            }

            if (Mathf.Approximately(group.alpha, targetAlpha)) return;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * FadeSpeed);
        }

        private void ApplySlide()
        {
            if (rect == null) return;
            rect.anchoredPosition = home + new Vector2(0f, slide);
        }
    }
}
