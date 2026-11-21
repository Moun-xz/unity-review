using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 翼尖航行灯：一圈一段的慢闪，左右不同相、不同色。
    ///
    /// 要点是"不同相"——三盏灯如果同频同相，看起来像整体在呼吸，
    /// 玩家的眼睛会把它当成 HUD 的一部分而不是机体的灯。
    /// </summary>
    public class NavLight : MonoBehaviour
    {
        private SpriteRenderer sr;
        private Color tint;
        private float period = 2.4f;
        private float phase;
        private float baseSize = 0.085f;
        private float keep = 0.22f;     // 暗着的时候还剩多少亮度
        private float spike = 2.6f;     // 闪的那一下有多尖

        /// <summary>建一盏航行灯。period 秒闪一次，phase 用来错开相位。</summary>
        public static NavLight Create(Transform parent, string name, Vector2 localPos, Color color,
                                      float period, float phase, float size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0.12f);
            go.transform.localScale = Vector3.one * size;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFactory.Star();
            renderer.sortingOrder = 11;

            NavLight light = go.AddComponent<NavLight>();
            light.sr = renderer;
            light.tint = color;
            light.period = Mathf.Max(0.4f, period);
            light.phase = phase;
            light.baseSize = size;
            return light;
        }

        private void Update()
        {
            if (sr == null) return;

            // 用 Time.time 而不是 unscaled：暂停时灯也该停，别让暂停界面看起来还在飞
            float t = Mathf.Repeat(Time.time / period + phase, 1f);
            // 只在每周期最后 18% 亮起：短促、有节奏
            float lit = t > 0.82f ? Mathf.Sin((t - 0.82f) / 0.18f * Mathf.PI) : 0f;
            float glow = keep + Mathf.Pow(lit, spike) * (1f - keep);

            sr.color = new Color(tint.r, tint.g, tint.b, glow);
            transform.localScale = Vector3.one * baseSize * (0.92f + 0.16f * lit);
        }
    }
}
