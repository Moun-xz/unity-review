using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 点击标题屏之后的跃迁：细亮线从中心炸开，白光在 0.45s 之后堆满，0.62s 处切场景，
    /// 随后白光褪去。是一个纯 overlay，没有底、没有面板——所有东西都只是光。
    ///
    /// 为什么保留 onReveal / onDone 两个回调：
    ///   onReveal 是"白光最亮、玩家什么都看不见"的那一帧，GameBootstrap 在这时切 UI、放开输入锁；
    ///   onDone 是白光彻底散尽，此时才能销毁自己。
    ///   两个时机分开，切场就不会被看见，也不会留下"白屏之后还在等"的空窗。
    /// </summary>
    public class WarpTransition : MonoBehaviour
    {
        private const float Duration = 1.30f;
        private const float RevealAt = 0.62f;
        private const float FlashUpAt = 0.45f;
        private const float FlashDownAt = 1.00f;
        private const float FlashPeak = 0.97f;
        private const int StreakCount = 96;

        private sealed class Streak
        {
            public RectTransform rect;
            public Image image;
            public float angle;
            public float radius;
            public float growth;
            public float width;
            public float alpha;
        }

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private readonly List<Streak> streaks = new List<Streak>(StreakCount);

        private CanvasGroup group;
        private Image flash;
        private Image core;

        private Action onReveal;
        private Action onDone;
        private float clock;
        private bool revealed;

        public static WarpTransition Play(Transform parent, Action onReveal, Action onDone)
        {
            GameObject go = new GameObject("WarpTransition", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Stretch((RectTransform)go.transform);

            WarpTransition warp = go.AddComponent<WarpTransition>();
            warp.onReveal = onReveal;
            warp.onDone = onDone;
            warp.Build();
            return warp;
        }

        private void Build()
        {
            group = UIFactory.AddCanvasGroup(gameObject);
            group.blocksRaycasts = false;
            group.interactable = false;

            BuildStreaks();

            // 中缝亮线：在爆闪前从 0 拉满整屏，是"跃迁通道打开"的那一下
            core = UIChrome.Hairline(transform, "Core", Center, Center,
                Vector2.zero, 0f, UITheme.A(UITheme.TextMain, 0f), 2f);

            // 白光必须最后建：它在层级最上面，才会盖住所有亮线
            flash = UIFactory.CreateImage(transform, "Flash", UITheme.A(UITheme.TextMain, 0f));
            UIFactory.Stretch(flash.rectTransform);
        }

        /// <summary>
        /// 96 根细线，从中心向外辐射。每根有自己的角度、起始半径、生长距离与亮度，
        /// 这样爆开时是一团有层次的线，而不是一把整齐的扇子。
        /// </summary>
        private void BuildStreaks()
        {
            for (int i = 0; i < StreakCount; i++)
            {
                float angle = i * (360f / StreakCount) + UnityEngine.Random.Range(-1.6f, 1.6f);
                float width = UnityEngine.Random.Range(1f, 2.4f);

                Image image = UIFactory.CreateImage(transform, "Streak" + i,
                    UITheme.A(UITheme.CyanBright, 0f));
                image.sprite = SpriteFactory.Streak();
                image.type = Image.Type.Simple;

                UIFactory.Place(image.gameObject, Center, Center, Vector2.zero,
                    new Vector2(width, width * 2f));

                Streak streak = new Streak();
                streak.rect = image.rectTransform;
                streak.image = image;
                streak.angle = angle;
                streak.radius = UnityEngine.Random.Range(30f, 220f);
                streak.growth = UnityEngine.Random.Range(420f, 980f);
                streak.width = width;
                streak.alpha = UnityEngine.Random.Range(0.30f, 0.85f);
                streak.rect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);

                streaks.Add(streak);
            }
        }

        private void Update()
        {
            clock += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(clock / Duration);

            UpdateStreaks(t);
            UpdateFlash(t);

            if (!revealed && clock >= RevealAt)
            {
                revealed = true;
                Action callback = onReveal;
                onReveal = null;
                if (callback != null) callback();
            }

            if (clock >= Duration)
            {
                Action callback = onDone;
                onDone = null;
                if (callback != null) callback();
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 半径按 t^2.2 加速：前 0.3s 几乎不动（还在"充能"），后段猛地冲出去。
        /// 线长跟着半径一起长，越长越细的观感靠宽度保持恒定、长度拉开。
        /// </summary>
        private void UpdateStreaks(float t)
        {
            float global = Mathf.Pow(t, 2.2f);

            for (int i = 0; i < streaks.Count; i++)
            {
                Streak streak = streaks[i];
                if (streak.rect == null) continue;

                float radius = streak.radius + streak.growth * global;
                float rad = streak.angle * Mathf.Deg2Rad;

                streak.rect.anchoredPosition = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
                streak.rect.sizeDelta = new Vector2(streak.width, Mathf.Max(streak.width * 2f, radius * 0.38f));

                // 前半程淡入（线要"亮起来"），0.65 之后随白光一起收掉
                float fadeIn = Mathf.Clamp01(t / 0.10f);
                float fadeOut = 1f - 0.85f * Mathf.Clamp01((t - 0.65f) / 0.35f);

                streak.image.color = UITheme.A(UITheme.CyanBright, streak.alpha * fadeIn * fadeOut);
            }
        }

        /// <summary>
        /// 白光两段：0.45→0.62 用三次方堆到 0.97（切场就藏在这一段最亮处），
        /// 0.62→1.00 用二次方退干净。峰值不能是 1.0，纯白会闪瞎眼，0.97 已经足够。
        /// </summary>
        private void UpdateFlash(float t)
        {
            float alpha = 0f;

            if (t >= FlashUpAt && t < RevealAt)
            {
                float k = (t - FlashUpAt) / (RevealAt - FlashUpAt);
                alpha = Mathf.Pow(Mathf.Clamp01(k), 3f) * FlashPeak;
            }
            else if (t >= RevealAt && t < FlashDownAt)
            {
                float k = (t - RevealAt) / (FlashDownAt - RevealAt);
                alpha = Mathf.Lerp(FlashPeak, 0f, k * k);
            }

            if (flash != null) flash.color = UITheme.A(UITheme.TextMain, alpha);

            if (core != null)
            {
                float width = 0f;
                if (t >= FlashUpAt && t < RevealAt)
                {
                    float k = (t - FlashUpAt) / (RevealAt - FlashUpAt);
                    width = Mathf.Lerp(0f, 1980f, Mathf.Pow(Mathf.Clamp01(k), 2f));
                }
                else if (t >= RevealAt && t < FlashDownAt)
                {
                    width = 1980f * (1f - Mathf.Clamp01((t - RevealAt) / (FlashDownAt - RevealAt)));
                }

                core.rectTransform.sizeDelta = new Vector2(width, 2f);
                core.color = UITheme.A(UITheme.TextMain, alpha);
            }
        }
    }
}
