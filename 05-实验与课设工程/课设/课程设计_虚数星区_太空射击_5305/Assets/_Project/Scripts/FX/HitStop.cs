using System.Collections;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 命中停顿（Hit Stop）：受击瞬间把时间压缩到 5%，持续几十毫秒再恢复。
    /// 这是动作游戏里最便宜也最有效的"打击感"技巧——玩家会感觉攻击"实"了。
    ///
    /// 时间缩放不在这里直接写，统一交给 TimeFlow：慢镜和暂停是两个独立状态，
    /// 「暂停优先」由 TimeFlow 算，这里只管进慢镜和退慢镜。
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        [Range(0.01f, 0.5f)] public float slowScale = 0.05f;

        private Coroutine routine;

        private void Awake()
        {
            Instance = this;
        }

        public void Freeze(float duration)
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FreezeRoutine(duration));
        }

        /// <summary>
        /// 慢镜（不是冻结）：把时间压到 scale 倍速维持 duration 秒。
        /// 和 Freeze 的区别是幅度——Freeze 是 0.05（几乎静止，做打击感），
        /// Slow 是 0.4 左右（世界还能动，但明显被拖慢，做演出压迫感）。
        /// Boss 出场用它：警报响起的那一下世界慢下来，比直接刷怪有分量得多。
        /// </summary>
        public void Slow(float scale, float duration)
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(SlowRoutine(scale, duration));
        }

        private IEnumerator FreezeRoutine(float duration)
        {
            TimeFlow.SetSlow(slowScale);
            yield return new WaitForSecondsRealtime(duration);

            // 到期只管摘掉慢镜，不用判断当前是不是暂停：
            // 暂停期间 ClearSlow 的结果仍然是 timeScale = 0（暂停优先）
            TimeFlow.ClearSlow();
            routine = null;
        }

        private IEnumerator SlowRoutine(float scale, float duration)
        {
            TimeFlow.SetSlow(scale);
            yield return new WaitForSecondsRealtime(duration);

            TimeFlow.ClearSlow();
            routine = null;
        }
    }
}
