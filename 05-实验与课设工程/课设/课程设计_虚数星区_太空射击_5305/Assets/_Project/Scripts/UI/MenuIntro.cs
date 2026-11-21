using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 主菜单的出场序列。
    ///
    /// 主菜单是玩家每次回到游戏看到的第一屏，元素一次性刷出来会显得"这界面是静态的"。
    /// 这里把元素排成一条时间表，从上往下依次浮现：字标先聚拢落定，副标和分割线跟上，
    /// 四个按钮一条一条抬起来，最后是战机展示位、说明徽章和页脚。
    /// 顺序就是眼睛从上往下扫的顺序，玩家不用找，视线自然被带到「开始游戏」。
    ///
    /// 四个实现上的决定：
    ///
    /// 1) 每个元素单独挂一个 CanvasGroup 控制透明度，而不是去改 Image / Text 的 color。
    ///    副标的 0.72、说明标签的 0.55 这些基准透明度都写在 color 里，
    ///    拿 color 做淡入就得先缓存、再覆写、结束时还原，一漏就是"回来之后副标变实了"。
    ///    CanvasGroup.alpha 是乘在子元素之上的，和元素自己的颜色互不干扰。
    ///
    /// 2) 只写位置 / 缩放 / 透明度，而且 fromScale 为 1 的元素一次都不碰 localScale。
    ///    按钮的 localScale 归 UIButtonFx 管（悬停放大 1.8%、按下缩回 3.5%），
    ///    两个组件同时写会按 Update 顺序互相覆盖、抖成一片。
    ///
    /// 3) 整个序列走 unscaledDeltaTime。
    ///    主菜单本身不在 timeScale = 0 的状态里，但从暂停菜单点「返回主菜单」时
    ///    timeScale 可能还没恢复，用 deltaTime 的话出场会卡在某一帧不动。
    ///
    /// 4) 位移只能有一个写入方。
    ///    字标本来在 MenuPanel.Update 里做 ±4px 的呼吸浮动，而浮动的也是 anchoredPosition。
    ///    所以呼吸位移改成由 MenuPanel 算好、通过 Bob 交进来，由这里统一写：
    ///    两边各写各的话，最终位置取决于两个组件谁后 Update，会一帧一跳。
    /// </summary>
    public class MenuIntro : MonoBehaviour
    {
        /// <summary>一个元素的出场参数</summary>
        private sealed class Act
        {
            public CanvasGroup group;
            public RectTransform rect;
            public Vector2 home;         // 原位（Build 结束时的位置）
            public Vector2 fromOffset;   // 起始位移，收敛到零
            public float fromScale;      // 起始缩放，1 表示不做缩放动画
            public float bobWeight;      // 呼吸位移的跟随比例
            public float delay;
            public float duration;
        }

        private readonly List<Act> acts = new List<Act>();

        private Vector2 bob;
        private float clock;
        private bool playing;
        private bool hidden;

        /// <summary>字标的呼吸位移，由 MenuPanel 每帧写进来（见类注释第 4 点）</summary>
        public Vector2 Bob
        {
            get { return bob; }
            set { bob = value; }
        }

        /// <summary>
        /// 登记一个元素，必须在它被摆到最终位置之后调用——原位是这时候取的。
        /// delay / duration 单位是秒；fromOffset 是起始位移；bobWeight 是呼吸跟随比例。
        /// 登记后立刻压到起始状态，这样从 Build 完到 Play 之间不会闪一帧全显。
        /// </summary>
        public void Add(RectTransform target, float delay, float duration,
                        Vector2 fromOffset, float fromScale = 1f, float bobWeight = 0f)
        {
            if (target == null) return;

            Act act = new Act();
            act.group = UIFactory.AddCanvasGroup(target.gameObject);
            act.rect = target;
            act.home = target.anchoredPosition;
            act.fromOffset = fromOffset;
            act.fromScale = fromScale;
            act.bobWeight = bobWeight;
            act.delay = Mathf.Max(0f, delay);
            act.duration = Mathf.Max(0.01f, duration);

            acts.Add(act);
            Apply(act, 0f);
        }

        /// <summary>
        /// 从头播一遍。每次回到主菜单都重播——这一屏本来就是"开机"，
        /// 只播一次的话第二次进来会显得潦草。
        /// </summary>
        public void Play()
        {
            hidden = false;
            clock = 0f;
            playing = acts.Count > 0;

            for (int i = 0; i < acts.Count; i++) Apply(acts[i], 0f);
        }

        /// <summary>
        /// 压回起点并停住，不推进。
        ///
        /// 开机画面盖在上面的时候用：菜单此时已经"显示"了（面板层的 CanvasGroup 是 1），
        /// 直接 Play 的话这一秒的出场会在黑幕后面白播一遍，等黑幕撤掉只剩个终态。
        /// </summary>
        public void Hide()
        {
            hidden = true;
            playing = false;

            for (int i = 0; i < acts.Count; i++) Apply(acts[i], 0f);
        }

        /// <summary>
        /// 不播，直接落到终态。
        ///
        /// 给"刚关掉又马上回来"的情况用：在设置面板里反复开关全屏、进机库看一眼就退，
        /// 每次都等一秒出场会变成噪音。判据在主调方（MenuPanel 记了隐藏时长）。
        /// </summary>
        public void Finish()
        {
            hidden = false;
            playing = false;

            for (int i = 0; i < acts.Count; i++) Apply(acts[i], 1f);
        }

        private void Update()
        {
            // 压住的期间不能走"落定之后只剩呼吸"那段：
            // 它会按 t = 1 把 alpha 写回 1，hide 就白做了
            if (hidden || acts.Count == 0) return;

            if (playing)
            {
                clock += Time.unscaledDeltaTime;

                bool allDone = true;
                for (int i = 0; i < acts.Count; i++)
                {
                    Act act = acts[i];
                    float t = Mathf.Clamp01((clock - act.delay) / act.duration);
                    if (t < 1f) allDone = false;
                    Apply(act, t);
                }

                playing = !allDone;
                return;
            }

            // 落定之后只剩呼吸：只有跟着字标浮动的元素还需要每帧重写位置，
            // 其余元素停在终态，一帧都不用再处理。
            for (int i = 0; i < acts.Count; i++)
            {
                if (acts[i].bobWeight > 0f) Apply(acts[i], 1f);
            }
        }

        private void Apply(Act act, float t)
        {
            if (act.group != null)
            {
                act.group.alpha = Fade(t);

                // 还没显影完的元素不接射线。
                // 「开始游戏」正好在屏幕正中（canvas y = -6），启动时鼠标十有八九就停在它上面：
                // 只淡不说的话，玩家会看见一个还没出现就被点亮的 hover 光晕，
                // 甚至直接点进游戏。显影完（t = 1）才放开。
                act.group.blocksRaycasts = t >= 1f;
            }

            if (act.rect == null) return;

            // 位移和缩放走 easeOutCubic：起手快、末尾慢慢贴住，读起来是"被放到位置上"。
            // 匀速的话最后一刻会硬停，出场动画最容易被看出廉价感的正是这一下。
            float k = EaseOut(t);

            Vector2 pos = act.home + Vector2.LerpUnclamped(act.fromOffset, Vector2.zero, k);
            if (act.bobWeight > 0f) pos += bob * act.bobWeight;
            act.rect.anchoredPosition = pos;

            if (!Mathf.Approximately(act.fromScale, 1f))
            {
                float s = Mathf.LerpUnclamped(act.fromScale, 1f, k);
                act.rect.localScale = new Vector3(s, s, 1f);
            }
        }

        /// <summary>透明度比位移更早到位（走到 55% 就满了）：元素先"显影"，再"落位"</summary>
        private static float Fade(float t)
        {
            float x = Mathf.Clamp01(t / 0.55f);
            return x * x * (3f - 2f * x);
        }

        private static float EaseOut(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
