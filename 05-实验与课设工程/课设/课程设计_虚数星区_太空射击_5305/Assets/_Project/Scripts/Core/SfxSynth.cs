using System;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 程序化音效合成。
    ///
    /// 七个音效全部用振荡器 + 噪声在运行时合成，工程里没有任何 wav/mp3，
    /// 也就不存在音效版权问题。每个方法返回一个 AudioClip，由 AudioManager 统一播放。
    ///
    /// BGM 不在这里——背景音乐是 `Assets/_Project/Art/Audio/bgm_main.ogg`，
    /// 挂在场景 `BGM` 节点的 AudioSource 上。理由：音效是几十毫秒的短音，
    /// 合成出来比做成文件更省事；而音乐要能随时替换、需要流式加载，
    /// 做成资源文件、在 Inspector 里拖到 AudioClip 才是正常做法。
    /// </summary>
    public static class SfxSynth
    {
        private const int SampleRate = 22050;
        private const float TwoPi = Mathf.PI * 2f;

        // ------------------------------------------------------------ 合成基础

        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
            return 1f - m / 1073741824f;
        }

        private static AudioClip Make(string name, float seconds, Func<float, int, float> generator, bool fadeEdges)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            float[] data = new float[count];
            int fade = Mathf.Min(SampleRate / 200, count / 6); // 约 5ms 淡入淡出，消除爆音

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float s = generator(t, i);
                if (fadeEdges && fade > 0)
                {
                    if (i < fade) s *= i / (float)fade;
                    if (i > count - fade) s *= (count - i) / (float)fade;
                }
                data[i] = Mathf.Clamp(s, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------ 音效

        /// <summary>激光：高频下滑的方波，短促</summary>
        public static AudioClip Laser()
        {
            return Make("sfx_laser", 0.14f, delegate(float t, int i)
            {
                float f = Mathf.Lerp(1500f, 480f, Mathf.Clamp01(t / 0.14f));
                float s = Mathf.Sign(Mathf.Sin(TwoPi * f * t)) * 0.45f;
                s += Mathf.Sin(TwoPi * f * 2f * t) * 0.18f;
                return s * Mathf.Exp(-t * 20f) * 0.55f;
            }, true);
        }

        /// <summary>
        /// 机炮：一记短促的"咔"。
        ///
        /// 原来是 0.22 秒的噪声 + 低频推挤（一发两响的"砰——"），够用是因为那会儿
        /// 0.45 秒才打一发。改成连射后一秒 18 发，长音会叠成一片"嘶——"的白噪声，
        /// 反而听不出节奏。所以把它压到 75 毫秒、衰减加快：每发都是独立的一下。
        /// </summary>
        public static AudioClip Spread()
        {
            return Make("sfx_spread", 0.075f, delegate(float t, int i)
            {
                float noise = Hash(i * 7 + 13) * 0.30f;
                float body = Mathf.Sin(TwoPi * Mathf.Lerp(240f, 90f, Mathf.Clamp01(t / 0.075f)) * t) * 0.55f;
                return (noise + body) * Mathf.Exp(-t * 42f) * 0.70f;
            }, true);
        }

        /// <summary>爆炸：白噪声 + 低频轰鸣，指数衰减</summary>
        public static AudioClip Explosion()
        {
            return Make("sfx_explosion", 0.62f, delegate(float t, int i)
            {
                float noise = Hash(i * 31 + 7) * 0.9f;
                float rumble = Mathf.Sin(TwoPi * Mathf.Lerp(120f, 45f, t / 0.62f) * t) * 0.55f;
                return (noise * 0.7f + rumble) * Mathf.Exp(-t * 7f) * 0.85f;
            }, true);
        }

        /// <summary>命中：极短的高频点击</summary>
        public static AudioClip Hit()
        {
            return Make("sfx_hit", 0.07f, delegate(float t, int i)
            {
                float s = Mathf.Sin(TwoPi * 1300f * t) * 0.6f + Hash(i * 3) * 0.3f;
                return s * Mathf.Exp(-t * 70f) * 0.5f;
            }, true);
        }

        /// <summary>玩家受伤：低频锯齿下坠</summary>
        public static AudioClip Hurt()
        {
            return Make("sfx_hurt", 0.32f, delegate(float t, int i)
            {
                float f = Mathf.Lerp(300f, 80f, Mathf.Clamp01(t / 0.32f));
                float phase = Mathf.Repeat(f * t, 1f);
                float saw = phase * 2f - 1f;
                return saw * Mathf.Exp(-t * 7f) * 0.55f;
            }, true);
        }

        /// <summary>UI 提示音</summary>
        public static AudioClip UiClick()
        {
            return Make("sfx_ui", 0.09f, delegate(float t, int i)
            {
                return Mathf.Sin(TwoPi * 880f * t) * Mathf.Exp(-t * 30f) * 0.4f;
            }, true);
        }

        /// <summary>Boss 出场警告</summary>
        public static AudioClip BossWarn()
        {
            return Make("sfx_bosswarn", 1.0f, delegate(float t, int i)
            {
                float f = Mathf.Lerp(160f, 760f, t / 1.0f);
                float tremolo = 0.6f + 0.4f * Mathf.Sin(TwoPi * 9f * t);
                return Mathf.Sin(TwoPi * f * t) * tremolo * Mathf.Exp(-t * 2.2f) * 0.45f;
            }, true);
        }
    }
}
