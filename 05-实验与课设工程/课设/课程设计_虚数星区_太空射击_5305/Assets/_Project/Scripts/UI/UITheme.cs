using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 界面主题：所有颜色、字号、间距、切角尺寸集中在这一个文件里。
    ///
    /// 上一版只有 5 个颜色 + 4 个字号，界面看起来"平"就是因为没有中间层：
    /// 面板底色只有一档、描边只有一档、文字只有亮/暗两档，堆不出层次。
    /// 这一版补了成套的刻度：
    ///   Surface 系列 —— 从最深到最亮四档底色，用深浅表达层级（越靠前越亮）
    ///   Edge 系列    —— 四档描边透明度
    ///   TextMain/Dim/Faint/Muted —— 四档文字
    ///   字号 11 档、间距 8 档、切角 3 档
    ///
    /// 想整体换色调，只改这个文件。
    /// </summary>
    public static class UITheme
    {
        // ---------------------------------------------------------- 主色

        public static readonly Color Cyan = new Color(0.35f, 0.95f, 1f, 1f);
        public static readonly Color CyanBright = new Color(0.62f, 1f, 1f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.42f, 0.86f, 1f);
        public static readonly Color Amber = new Color(1f, 0.74f, 0.28f, 1f);
        public static readonly Color Danger = new Color(1f, 0.32f, 0.38f, 1f);
        public static readonly Color Mint = new Color(0.42f, 1f, 0.74f, 1f);

        // ---------------------------------------------------------- 文字
        //
        // 2026-11-10 可读性修订：Linear 色彩空间下，深色面板上的低亮度文字会比设计值更暗，
        // 原来 Faint 0.34 / Muted 0.23 那一档在 1080p 实机截图里已经接近"水印"，
        // 14px 的小标签更是连字形都读不出来。这里把整条阶梯抬一档，但保持四档有序：
        //   Main 0.93 > Dim 0.70 > Faint 0.56 > Muted 0.42
        // 原来的 TextDim 现在正好落在新 Faint 的位置，所以层级关系没有变，只是整体变亮。
        // 注意：不要再对这些颜色二次乘 alpha（A(TextDim, 0.55) 之类），
        // 那等于把已经压到下限的文字又砍一刀，是这次"看不清"的另一个根因。

        public static readonly Color TextMain = new Color(0.93f, 0.97f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.70f, 0.78f, 0.88f, 1f);
        public static readonly Color TextFaint = new Color(0.56f, 0.64f, 0.76f, 1f);
        public static readonly Color TextMuted = new Color(0.42f, 0.49f, 0.60f, 1f);

        // ---------------------------------------------------------- 面板与线条（旧 token，保留兼容）

        public static readonly Color PanelBg = new Color(0.030f, 0.050f, 0.100f, 0.88f);
        public static readonly Color PanelBgSoft = new Color(0.040f, 0.060f, 0.120f, 0.55f);
        public static readonly Color BtnBg = new Color(0.060f, 0.098f, 0.195f, 0.80f);
        public static readonly Color SlotBg = new Color(0.10f, 0.16f, 0.26f, 0.90f);
        public static readonly Color CardBg = new Color(0.030f, 0.050f, 0.100f, 0.74f);
        public static readonly Color CardBgPicked = new Color(0.055f, 0.105f, 0.205f, 0.88f);
        public static readonly Color Line = new Color(0.30f, 0.76f, 0.95f, 0.45f);
        public static readonly Color LineSoft = new Color(0.30f, 0.76f, 0.95f, 0.16f);
        public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.85f);

        /// <summary>主菜单背后压的那层暗色。不能太黑：现在背景是真的星空，压太狠就看不见了</summary>
        public static readonly Color Overlay = new Color(0.012f, 0.018f, 0.038f, 0.45f);

        /// <summary>
        /// 图鉴里图标底下那层冷光。深色贴图（陨石 #121520）放在深色卡片上会糊成一团，
        /// 加一层淡到几乎看不见的底光，图标的暗部就沉回背景之下，重新变成剪影。
        /// 亮度只到 #1A3357 上下：再亮就变成"每个图标下面扣了个发光圆盘"。
        /// </summary>
        public static readonly Color IconHalo = new Color(0.055f, 0.150f, 0.320f, 0.72f);

        // ---------------------------------------------------------- 表面（四档，越靠前越亮）

        /// <summary>最深的底：整屏遮罩、菜单右侧空白区</summary>
        public static readonly Color SurfaceDeep = new Color(0.012f, 0.020f, 0.036f, 0.94f);

        /// <summary>标准面板底</summary>
        public static readonly Color Surface = new Color(0.020f, 0.032f, 0.058f, 0.94f);

        /// <summary>浮起一层：卡片、列表行、HUD 模块</summary>
        public static readonly Color SurfaceRaised = new Color(0.032f, 0.050f, 0.086f, 0.92f);

        /// <summary>悬停 / 选中</summary>
        public static readonly Color SurfaceHover = new Color(0.055f, 0.090f, 0.150f, 0.94f);

        public static readonly Color SurfaceSunken = new Color(0.008f, 0.014f, 0.026f, 0.85f);

        // ---------------------------------------------------------- 描边（四档）

        public static readonly Color EdgeStrong = new Color(0.42f, 0.92f, 1f, 0.52f);
        public static readonly Color Edge = new Color(0.36f, 0.82f, 0.99f, 0.26f);
        public static readonly Color EdgeSoft = new Color(0.36f, 0.82f, 0.99f, 0.12f);
        public static readonly Color EdgeHair = new Color(0.36f, 0.82f, 0.99f, 0.055f);

        /// <summary>
        /// 主按钮的实心底。
        ///
        /// 之前主按钮用的是 14% 青的"淡洗"，叠在同样偏青的面板底上——色相同、明度几乎没变，
        /// 结果就是"一圈亮边包着一块和背景差不多的空"。主操作没有重量感，看着像对话框。
        /// 这里换成不透明的深青灰：明度约为面板底的 4 倍，仍然是冷色，但能明确读成"一块抬起来的板"。
        /// </summary>
        public static readonly Color BtnPrimaryFill = new Color(0.094f, 0.168f, 0.208f, 0.98f);

        /// <summary>键帽底色：比按钮更深，做出"凹进去的键"</summary>
        public static readonly Color BtnKeyCap = new Color(0.004f, 0.010f, 0.022f, 0.62f);

        /// <summary>强调色的"淡洗"：选中项底色、悬停铺底</summary>
        public static readonly Color AccentWash = new Color(0.35f, 0.95f, 1f, 0.075f);
        public static readonly Color AccentWashStrong = new Color(0.35f, 0.95f, 1f, 0.14f);
        public static readonly Color DangerWash = new Color(1f, 0.32f, 0.38f, 0.12f);

        // ---------------------------------------------------------- 字号（新刻度）
        //
        // 2026-11-10 可读性修订：下面四档整体上抬一档。
        // 1920×1080 实机上，14px 的全大写微标签再经 SpaceOut 拉开字距，
        // 每个字母只占十来个像素，隔着一臂距离就是一排灰点；16 是能读清的下限。
        // 阶梯仍然有序（22 > 20 > 18 > 16），没有新增"生造字号"。

        public const int SizeDisplay = 84;   // 标题屏主标题
        public const int SizeHero = 64;      // 结算大分数
        public const int SizeH1 = 48;        // 面板主标题
        public const int SizeH2 = 36;        // 区块标题
        public const int SizeH3 = 30;        // 列表行标题 / 按钮
        public const int SizeValue = 34;     // 大号数值
        public const int SizeBody = 26;
        public const int SizeLabel = 22;
        public const int SizeCaption = 20;
        public const int SizeMicro = 18;     // 徽标内文字 / 键帽
        public const int SizeNano = 16;      // 短标识：键帽 / HUD 状态词 / 刻度（长微标见 §20，用 SizeMicro 粗体）
        public const int SizeScore = 58;     // 战斗 HUD 右上角得分（比 Hero 略小，好塞进角落模块）

        // 旧字号（保留兼容）
        public const int SizeTitle = 96;
        public const int SizeSmall = 22;
        public const int SizeTiny = 20;

        // ---------------------------------------------------------- 间距

        public const float Gap1 = 4f;
        public const float Gap2 = 8f;
        public const float Gap3 = 12f;
        public const float Gap4 = 16f;
        public const float Gap5 = 24f;
        public const float Gap6 = 32f;
        public const float Gap7 = 48f;
        public const float Gap8 = 64f;

        // ---------------------------------------------------------- 切角

        public const float CutLarge = 20f;   // 大面板
        public const float CutCard = 12f;    // 卡片 / 模块
        public const float CutChip = 7f;     // 小徽标 / 键帽

        // ---------------------------------------------------------- 小标签内边距
        //
        // 横向 28 差不多是字号的 1.4 倍，是切角 7 的四倍：chip 的四个角被切掉一块，
        // 文字离框只有十几像素时，首尾两个字正好落在斜切边上，读起来像"字把框撑破了"
        // （「新纪录」原来左右各 7px，暂停那枚上下只剩 3px）。按 28 排，字占框宽的七成左右，
        // 一眼就是"框包着字"，而不是"字塞在框里"。
        // 尺寸仍写在各调用点（静态检查要读得到字面量），这两个值只负责"该留多少"的口径。

        public const float TagPadX = 28f;
        public const float TagPadY = 7f;

        /// <summary>小标签的标准高度：字号 + 上下内边距</summary>
        public const float TagHeight = SizeCaption + TagPadY * 2f;

        // ---------------------------------------------------------- 半透明辅助

        public static Color A(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
