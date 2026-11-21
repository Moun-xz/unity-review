using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 尾焰：从两个喷口各喷出三层柔光（核心 / 主焰 / 外层晕），长度随速度拉伸。
    ///
    /// 为什么不在 ShipArt 里画死：
    /// 机尾余焰是"动态"信息——玩家靠它判断自己是不是在加速。画进贴图就永远不会变，
    /// 静止的飞机反而看起来像一直在推进。
    /// </summary>
    public class ThrusterFlame : MonoBehaviour
    {
        // 喷口位置在机身贴图坐标系里（ShipArt 的 HAMMER：±0.486, -0.804）。
        // 子物体会跟着 bodyScale 一起放大，所以这里直接用贴图单位。
        // ShipArt 的 u/v 是 [-1,1]，对应 sprite 的 ±0.5 世界单位，所以这里要乘 0.5
        private const float NozzleX = 0.243f;
        private const float NozzleY = -0.4225f;

        // 三层的基础长度与宽度（贴图单位）：由内到外越来越长、越来越淡
        private static readonly float[] BaseLen = { 0.20f, 0.38f, 0.56f };
        private static readonly float[] BaseWidth = { 0.050f, 0.092f, 0.145f };
        private static readonly float[] BaseAlpha = { 0.90f, 0.42f, 0.17f };

        private readonly List<SpriteRenderer> layers = new List<SpriteRenderer>();
        private Rigidbody2D body;
        private float pulse;

        /// <summary>在玩家根节点下建尾焰，返回可直接 SetAccent 的组件。</summary>
        public static ThrusterFlame Create(Transform parent, Color accentColor)
        {
            GameObject go = new GameObject("ThrusterFlame");
            go.transform.SetParent(parent, false);

            ThrusterFlame flame = go.AddComponent<ThrusterFlame>();
            flame.body = parent.GetComponent<Rigidbody2D>();
            flame.Build();
            flame.SetAccent(accentColor);
            return flame;
        }

        private void Build()
        {
            // 两个喷口各三层；sortingOrder 6/7/8 都压在机身(10) 之下，不会糊住机尾轮廓
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    GameObject layer = new GameObject("Flame" + (side < 0 ? "L" : "R") + i);
                    layer.transform.SetParent(transform, false);
                    layer.transform.localPosition = new Vector3(side * NozzleX, NozzleY, 0.05f);

                    SpriteRenderer sr = layer.AddComponent<SpriteRenderer>();
                    Sprite glow = SpriteFactory.Glow();
                    sr.sprite = glow;
                    sr.sortingOrder = 6 + i;
                    sr.color = new Color(1f, 0.85f, 0.6f, BaseAlpha[i]);

                    // 贴图是一整个 sprite（1 世界单位），这里按 bounds 反算缩放，
                    // 让 BaseLen / BaseWidth 真的等于"世界单位里多长、多宽"。
                    Vector2 raw = glow.bounds.size;
                    sr.transform.localScale = new Vector3(BaseWidth[i] / Mathf.Max(raw.x, 0.001f),
                                                           BaseLen[i] / Mathf.Max(raw.y, 0.001f), 1f);
                    layers.Add(sr);
                }
            }
        }

        public void SetAccent(Color color)
        {
            if (layers.Count == 0) return;

            // 核心偏白热、主焰取强调色、外层晕再压暗一档
            Color hot = Color.Lerp(color, Color.white, 0.55f);
            Color mid = color;
            Color far = new Color(color.r * 0.78f, color.g * 0.78f, color.b * 0.78f, 1f);

            for (int i = 0; i < layers.Count; i++)
            {
                Color c = (i % 3) switch
                {
                    0 => hot,
                    1 => mid,
                    _ => far,
                };
                c.a = BaseAlpha[i % 3];
                layers[i].color = c;
            }
        }

        private void Update()
        {
            if (layers.Count == 0) return;
            if (body == null) body = GetComponentInParent<Rigidbody2D>();

            float speed = body != null ? body.velocity.magnitude : 0f;
            // 0.55 是怠速长度：完全静止时也要留一截"点着了"的短焰
            float throttle = Mathf.Clamp01(speed / 7.5f);
            pulse += Time.deltaTime * (18f + throttle * 26f);
            float flicker = 0.92f + 0.08f * Mathf.Sin(pulse) + 0.04f * Mathf.Sin(pulse * 2.7f);

            float stretch = (0.55f + throttle * 1.15f) * flicker;
            float tighten = 1f - throttle * 0.16f;

            for (int i = 0; i < layers.Count; i++)
            {
                int tier = i % 3;
                float len = BaseLen[tier] * stretch * (1f + tier * 0.05f);
                float wid = BaseWidth[tier] * tighten;

                SpriteRenderer sr = layers[i];
                Transform tr = sr.transform;
                Vector2 raw = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
                tr.localScale = new Vector3(wid / Mathf.Max(raw.x, 0.001f), len / Mathf.Max(raw.y, 0.001f), 1f);
                // 焰心顶在喷口上：中心 = 喷口 - 半长
                tr.localPosition = new Vector3(tr.localPosition.x, NozzleY - len * 0.5f, 0.05f);

                Color c = sr.color;
                c.a = BaseAlpha[tier] * Mathf.Clamp01(0.55f + stretch * 0.42f);
                sr.color = c;
            }

            // 横向移动时尾焰朝反方向斜一点，像被气流吹偏
            float lean = body != null ? Mathf.Clamp(body.velocity.x * 1.6f, -12f, 12f) : 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, -lean);
        }
    }
}
