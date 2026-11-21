using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 时间缩放的唯一写入点。
    ///
    /// 之前 Time.timeScale 散落在五处：GameManager 管暂停、HitStop 管慢镜、
    /// PausePanel / ResultPanel 各自在回主菜单时手动写回 1。
    /// 这套写法能跑，但靠的是一条看不见的约定——每个写 timeScale 的地方
    /// 都得自己记得「现在是不是暂停中」。任何一处漏判，就会出现
    /// 「暂停界面被慢镜顶回 1 倍速」或者「退回菜单后整个世界卡在 0.42」。
    ///
    /// 收敛到这里的规则只有一条：**暂停优先于慢镜**。
    /// 两个状态分开记，最后统一算一次 timeScale，谁也不用关心对方的死活。
    /// </summary>
    public static class TimeFlow
    {
        /// <summary>是否停在时间上（暂停 / 结算）</summary>
        private static bool paused;

        /// <summary>当前慢镜倍率，没有慢镜时是 1</summary>
        private static float slow = 1f;

        /// <summary>当前是否处在慢镜里（暂停不算——暂停时慢镜可能已经到期了）。</summary>
        public static bool IsSlowed
        {
            get { return slow < 0.99f; }
        }

        /// <summary>暂停开关。暂停时无论慢镜说什么，时间都是 0。</summary>
        public static void SetPaused(bool value)
        {
            paused = value;
            Apply();
        }

        /// <summary>进入慢镜。倍率会被夹到 0.01~1，0 会真的把时间冻死。</summary>
        public static void SetSlow(float scale)
        {
            slow = Mathf.Clamp(scale, 0.01f, 1f);
            Apply();
        }

        /// <summary>退出慢镜，回到该有的速度（暂停中仍然保持 0）。</summary>
        public static void ClearSlow()
        {
            slow = 1f;
            Apply();
        }

        /// <summary>
        /// 彻底复位。回主菜单、重开一局都走这里：
        /// 慢镜可能还挂着没到期，只清暂停的话会带着 0.42 进下一局。
        /// </summary>
        public static void Reset()
        {
            paused = false;
            slow = 1f;
            Time.timeScale = 1f;
        }

        private static void Apply()
        {
            Time.timeScale = paused ? 0f : slow;
        }

        /// <summary>
        /// 编辑器关掉 Domain Reload 时静态字段不会复位，
        /// 第二次 Play 会带着上一次的 paused / slow 进来。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Reset();
        }
    }
}
