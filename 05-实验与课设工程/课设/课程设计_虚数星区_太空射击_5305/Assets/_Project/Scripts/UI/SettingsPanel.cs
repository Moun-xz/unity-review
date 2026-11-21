using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 设置面板（v2 战术终端）：一块 780×540 的 Sheet，正文只有两行混音推子。
    ///
    /// 为什么不再放「分辨率 / 全屏 / 画质」：
    ///   整套界面是按 1920×1080 的绝对坐标排的（Sheet 尺寸、取景框、列表行位置），
    ///   分辨率一换，所有版面就要重新按比例算一遍。这门课要交的是一个"稳定的画面"，
    ///   不是一个能跑的设置中心——多一个开关就多一条答辩时会被追问的状态。
    ///
    /// 版式：Sheet 三段（Header / Content / Footer）。每行 = 左标签 + 中推子 + 右等宽百分比。
    /// 推子不用 UIFactory.CreateSlider 的默认皮肤（那条是上一版的圆角轨道），
    /// 这里用切角蒙版重画，读数用等宽字体——整套界面里所有数字都是等宽的。
    /// </summary>
    public class SettingsPanel : UIPanelBase
    {
        /// <summary>关闭时回调给打开它的 MenuPanel</summary>
        public Action Closed;

        private const float SheetW = 780f;
        private const float SheetH = 540f;

        /// <summary>推子轨道宽。三列（标签 17" + 轨道 36" + 读数 90）加起来正好落在内容区里</summary>
        private const float TrackW = 360f;

        private Text bgmPercent;
        private Text sfxPercent;
        private Slider bgmSlider;
        private Slider sfxSlider;

        // ================================================================== 构建

        public void Build()
        {
            if (bgmSlider != null) return;

            InitGroup(false);

            UIChrome.Scrim(transform);
            UIChrome.GridBackdrop(transform, "Grid", 0.04f);

            UISheet sheet = UIChrome.Sheet(transform, "Sheet", new Vector2(SheetW, SheetH), Vector2.zero);
            UIChrome.Header(sheet, "04", "设置", "CONFIG");
            UIChrome.Footer(sheet, "拖动滑条调节 · ESC 返回主菜单", "IMAGINARY SECTOR");

            RectTransform content = sheet.Content;

            UIChrome.Eyebrow(content, "Mixer", "音量设置",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -2f),
                new Vector2(420f, 20f), TextAnchor.MiddleLeft);

            UIChrome.Eyebrow(content, "MixerCount", "2 路通道",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f),
                new Vector2(320f, 20f), TextAnchor.MiddleRight, UITheme.TextFaint);

            UIChrome.Hairline(content, "TopRule", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -30f), SheetW - UIChrome.PadSide * 2f, UITheme.EdgeSoft);

            AudioManager audio = AudioManager.Instance;
            float bgm = audio != null ? audio.bgmVolume : 0.26f;
            float sfx = audio != null ? audio.sfxVolume : 0.7f;

            SliderRow(content, "背景音乐", "BGM", 66f, bgm, out bgmSlider, out bgmPercent);
            SliderRow(content, "音效", "SFX", -34f, sfx, out sfxSlider, out sfxPercent);

            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);

            RefreshLabels(bgm, sfx);

            // 说明：一句话交代"改了就存"，省得玩家不确定要不要按确认
            // 236×34 = 文字 180 + 左右各 28、字号 20 + 上下各 7
            UIChrome.Tag(content, "SaveHint", "调整立即生效并保存", new Vector2(236f, 34f),
                new Vector2(0f, -108f), UITheme.Cyan);

            // 说明行靠左：右下角这一格要让给「返回」按钮，两者不能叠在一起。
            // 中间提示条已经说了「立即生效并保存」，这里只补「存到哪」，不重复同一句。
            UIChrome.Eyebrow(content, "HintLine", "音量设置随存档保留",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 22f),
                new Vector2(430f, 22f), TextAnchor.MiddleLeft, UITheme.TextFaint);

            // 右下角「返回」：鼠标点得到，不必非得记住 ESC（ESC 依然有效，走同一个 Close）
            UIAction back = UIChrome.Action(content, "BtnBack", "", "返回", "ESC",
                new Vector2(260f, 60f), new Vector2(226f, -160f), UIActionStyle.Ghost, UITheme.Cyan);
            back.Button.onClick.AddListener(Close);
        }

        /// <summary>
        /// 一行混音：左标签（中文，SizeH3）+ 中推子 + 右等宽百分比。
        ///
        /// 推子是自己拼的，不是 UIFactory.CreateSlider：
        ///   轨道 / 填充 / 手柄三张图都换成切角蒙版（Sheet 2 与 CutChip），
        ///   这样它和界面里其他"切角构件"是同一套形状语言，不会在同一屏里出现圆角。
        /// </summary>
        private void SliderRow(Transform parent, string label, string code, float y, float value,
                               out Slider slider, out Text percent)
        {
            UIChrome.CreateLabel(parent, code + "Label", label, UITheme.SizeH3,
                TextAnchor.MiddleLeft, UITheme.TextMain,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(-300f, y + 12f), new Vector2(180f, 36f));

            UIChrome.CreateLabel(parent, code + "Tag", code, UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(-300f, y - 22f), new Vector2(180f, 24f), true, true);

            GameObject go = new GameObject(code + "Slider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(4f, y), new Vector2(TrackW, 34f));

            slider = go.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            Image track = UIFactory.CreateImage(go.transform, "Track", UITheme.SurfaceSunken);
            track.sprite = UISprites.Sheet(3f);
            track.type = Image.Type.Sliced;
            track.raycastTarget = true;             // 允许直接点轨道跳值
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.anchoredPosition = Vector2.zero;
            trackRect.sizeDelta = new Vector2(0f, 10f);

            // 刻度：每 10% 一根极细的暗线，压在填充之上，读起来像调音台的刻度槽
            for (int i = 1; i < 10; i++)
            {
                Image tick = UIFactory.CreateImage(go.transform, "Tick" + i, UITheme.A(UITheme.SurfaceDeep, 0.9f));
                RectTransform r = tick.rectTransform;
                r.anchorMin = new Vector2(i / 10f, 0.5f);
                r.anchorMax = new Vector2(i / 10f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(1.5f, 14f);
            }

            RectTransform fillArea = UIFactory.NewUI(go.transform, "FillArea");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.anchoredPosition = Vector2.zero;
            fillArea.sizeDelta = new Vector2(-14f, 10f);

            Image fill = UIFactory.CreateImage(fillArea, "Fill", UITheme.Cyan);
            fill.sprite = UISprites.Sheet(3f);
            fill.type = Image.Type.Sliced;
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            RectTransform handleArea = UIFactory.NewUI(go.transform, "HandleArea");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.anchoredPosition = Vector2.zero;
            handleArea.sizeDelta = new Vector2(-14f, 0f);

            Image handle = UIFactory.CreateImage(handleArea, "Handle", UITheme.CyanBright);
            handle.sprite = UISprites.Sheet(UITheme.CutChip);
            handle.type = Image.Type.Sliced;
            handle.raycastTarget = true;            // 拖拽靠它接收射线
            RectTransform handleRect = handle.rectTransform;
            handleRect.sizeDelta = new Vector2(14f, 26f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.value = value;

            percent = UIFactory.CreateTextMono(parent, code + "Percent", "000%", UITheme.SizeValue,
                TextAnchor.MiddleRight, UITheme.Cyan, true);
            UIFactory.Place(percent.gameObject, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(300f, y), new Vector2(112f, 40f));
        }

        private void RefreshLabels(float bgm, float sfx)
        {
            if (bgmPercent != null) bgmPercent.text = PadPercent(bgm);
            if (sfxPercent != null) sfxPercent.text = PadPercent(sfx);
        }

        /// <summary>等宽字体下三位数宽度一致，从 0% 跳到 100% 时读数不会左右抖</summary>
        private static string PadPercent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString("000") + "%";
        }

        private void OnBgmChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetBgmVolume(value);
            if (bgmPercent != null) bgmPercent.text = PadPercent(value);
        }

        private void OnSfxChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(value);
            if (sfxPercent != null) sfxPercent.text = PadPercent(value);
        }

        // ================================================================== 交互

        /// <summary>打开时把外部改过（比如读档）的音量同步回滑条</summary>
        public void Open()
        {
            if (AudioManager.Instance != null)
            {
                if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(AudioManager.Instance.bgmVolume);
                if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioManager.Instance.sfxVolume);
                RefreshLabels(AudioManager.Instance.bgmVolume, AudioManager.Instance.sfxVolume);
            }

            SetVisible(true);
        }

        public void Close()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            SetVisible(false);
            if (Closed != null) Closed();
        }

        protected override void Update()
        {
            base.Update();

            if (group == null || group.alpha <= 0.01f) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }
    }
}
