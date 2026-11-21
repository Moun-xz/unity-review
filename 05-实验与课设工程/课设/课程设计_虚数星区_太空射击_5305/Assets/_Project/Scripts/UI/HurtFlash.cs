using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 受击红屏：只在屏幕四边留一圈很窄的红色渐晕，掉血时闪一下、快速衰减。
    ///
    /// v2 的克制之处：
    ///   不再盖整块红。DamageVignette 本身就是"外侧 10% 环带"的蒙版，
    ///   峰值压到 0.30（一次掉 3 点血才到 0.44），衰减 3.6/s——眨眼之间就没了。
    ///   玩家能从余光里感到"被打了"，但不会因为红屏看不清弹幕。
    ///
    /// 只在血量真的下降时闪：治疗、初始化、满血复活都不该触发。
    /// </summary>
    public class HurtFlash : MonoBehaviour
    {
        private const float PeakAlpha = 0.30f;
        private const float MaxAlpha = 0.62f;
        private const float DecaySpeed = 3.6f;

        /// <summary>每多一点掉血，峰值增加 15%（在 PeakAlpha 之上叠加）。</summary>
        private const float PerPointBonus = 0.15f;

        private Image flash;
        private float alpha;
        private int lastHp = -1;
        private bool built;

        public void Build()
        {
            flash = UIFactory.CreateImage(transform, "Flash", UITheme.A(UITheme.Danger, 0f));
            flash.sprite = SpriteFactory.DamageVignette();
            flash.type = Image.Type.Simple;
            flash.enabled = false;
            UIFactory.Stretch(flash.rectTransform);

            built = true;
        }

        private void OnEnable()
        {
            EventBus.PlayerHpChanged += OnPlayerHpChanged;
        }

        private void OnDisable()
        {
            EventBus.PlayerHpChanged -= OnPlayerHpChanged;
        }

        private void OnPlayerHpChanged(int current, int max)
        {
            // 第一次报告只用来建立基线：开局满血那次不能闪
            if (lastHp < 0)
            {
                lastHp = current;
                return;
            }

            if (current < lastHp)
            {
                int lost = lastHp - current;
                float peak = Mathf.Min(PeakAlpha * (1f + lost * PerPointBonus), MaxAlpha);
                alpha = Mathf.Max(alpha, peak);
            }

            lastHp = current;
        }

        private void Update()
        {
            if (!built || flash == null) return;

            if (alpha <= 0.001f)
            {
                if (flash.enabled)
                {
                    flash.enabled = false;
                    flash.color = UITheme.A(UITheme.Danger, 0f);
                }
                return;
            }

            alpha = Mathf.Max(0f, alpha - DecaySpeed * Time.unscaledDeltaTime);
            flash.enabled = true;
            flash.color = UITheme.A(UITheme.Danger, alpha);
        }
    }
}
