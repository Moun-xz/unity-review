using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGravity
{
    /// <summary>
    /// 敌人图鉴（v2 战术终端）：一块 1180×880 的 Sheet，里面是 2×2 四张资料卡。
    ///
    /// 每一格回答四个问题：长什么样、有多硬、值多少分、怎么对付。
    /// 版面按"左侧立绘 + 右侧指标"排，四张卡尺寸一致，扫视时四组数字在同一列上对齐。
    ///
    /// 立绘必须和游戏里长得一样：
    ///   陨石用 `SpriteFactory.Meteor(seed)` 程序化生成（和关卡里随机的陨石同一套生成器），
    ///   其它三种用 `ArtLibrary` 里的贴图 + `EnemyBase.HostileTint` 染色。
    ///   染色只要和 `EnemyBase.OnSpawn` 里差一点，玩家就会觉得"图鉴里是另一种飞机"。
    ///
    /// 编号：主菜单的入口顺序是 "1 开始 / "2 机库 / "3 图鉴 / "4 说明 / "5 设置 / "6 退出，
    /// 每个面板 Header 上的序号跟这个顺序一致（本页是 02）。
    /// </summary>
    public class EnemyCodexPanel : UIPanelBase
    {
        /// <summary>关闭时回调给打开它的 MenuPanel</summary>
        public Action Closed;

        private const float SheetW = 1180f;
        private const float SheetH = 880f;

        private const float CardW = 528f;
        private const float CardH = 292f;
        private const float CardX = 276f;       // 左右两列的中心偏移
        private const float CardTopY = 162f;    // 上排中心
        private const float CardBottomY = -140f; // 下排中心

        private const float IconScale = 96f;    // 立绘基准尺寸：1 个世界单位 = 88px
        private const float IconX = -158f;      // 立绘中心相对卡片
        private const float TextX = -40f;       // 文字列左缘相对卡片

        /// <summary>
        /// 文字列可用宽 = 卡片半宽 - 右侧内缩 - 左缘。
        /// 原先写成 CardW - 22 - TextX（整个卡宽），正文和分隔线都会压出卡片右缘。
        /// </summary>
        private const float TextW = CardW * 0.5f - 22f - TextX;

        private bool built;

        // ================================================================== 构建

        public void Build()
        {
            if (built) return;

            InitGroup(false);
            built = true;

            UIChrome.Scrim(transform);
            UIChrome.GridBackdrop(transform, "Grid", 0.04f);

            UISheet sheet = UIChrome.Sheet(transform, "Sheet", new Vector2(SheetW, SheetH), Vector2.zero);
            UIChrome.Header(sheet, "02", "敌人图鉴", "CODEX · HOSTILES");
            UIChrome.Footer(sheet, "ESC 返回主菜单", "04 HOSTILES · 02 SECTORS");

            RectTransform content = sheet.Content;

            UIChrome.Eyebrow(content, "CodexHead", "敌人档案",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -2f),
                new Vector2(460f, 20f), TextAnchor.MiddleLeft);

            UIChrome.Eyebrow(content, "CodexCount", "共 4 种敌机",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f),
                new Vector2(360f, 20f), TextAnchor.MiddleRight, UITheme.TextFaint);

            UIChrome.Hairline(content, "CodexRule", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -30f), SheetW - UIChrome.PadSide * 2f, UITheme.EdgeSoft);

            // 顺序 = 玩家第一次遇到它们的顺序，不是 Enum 顺序：
            // 第一波就是突进者，炮台第二波才出，追踪导弹更晚，陨石是环境物
            Card(content, new Vector2(-CardX, CardTopY), EnemyKind.Rusher, 0,
                "突进者", "RUSHER", UITheme.Danger,
                "直线冲锋，撞上掉 20 点船体",
                "提前横向拉开，别正对着它倒退");

            Card(content, new Vector2(CardX, CardTopY), EnemyKind.Turret, 0,
                "哨戒炮台", "TURRET", UITheme.Amber,
                "原地不动，每 1.2 秒扇形齐射",
                "优先拆掉，别在它正面久留");

            // 追踪导弹的标记色跟着机型换：弹体改成金属灰之后，卡片再挂品红就自相矛盾了。
            // 四张卡的标记色现在按威胁度排：红（撞过来）> 琥珀（远程）> 银灰（追踪弹）> 深灰（环境物）。
            Card(content, new Vector2(-CardX, CardBottomY), EnemyKind.Tracker, 0,
                "追踪导弹", "TRACKER", UITheme.TextDim,
                "0.5 秒锁定，末段 1.9 倍速",
                "一梭子打爆，或横拉甩脱");

            Card(content, new Vector2(CardX, CardBottomY), EnemyKind.Meteor, 1,
                "陨石", "METEOR", UITheme.TextMuted,
                "不瞄准也不射击，靠体积占位",
                "从边缘绕开，大的能打碎");

            // 底部一条：左提示、右「返回」。两张卡整体上移 12px 把这条让出来
            // 436×34 = 文字 380 + 左右各 28、字号 20 + 上下各 7（原来的 520 宽留了 70px 空边，
            // 和别的 chip 不是同一套口径）
            UIChrome.Tag(content, "CodexTip", "被撞与中弹都会掉船体，血量见左下角血条",
                new Vector2(436f, 34f), new Vector2(0f, -330f), UITheme.Cyan);

            // 右下角「返回」：鼠标点得到，不必非得记住 ESC（ESC 依然有效，走同一个 Close）
            UIAction back = UIChrome.Action(content, "BtnBack", "", "返回", "ESC",
                new Vector2(260f, 60f), new Vector2(426f, -330f), UIActionStyle.Ghost, UITheme.Cyan);
            back.Button.onClick.AddListener(Close);
        }

        // ================================================================== 卡片

        /// <summary>
        /// 出没关卡：直接从 GameConfig.Levels() 的波次表里数出来，
        /// 以后改了波次组成，图鉴会自动跟着变，不会出现"文案说只在前两波、实际第六波也在"的谎。
        /// </summary>
        private static LevelConfig[] levelsCache;

        private static string[] Habitat(EnemyKind kind)
        {
            if (levelsCache == null) levelsCache = GameConfig.Levels();

            string[] result = new string[2];
            for (int i = 0; i < result.Length && i < levelsCache.Length; i++)
            {
                WaveDefinition[] waves = levelsCache[i].waves;
                StringBuilder sb = new StringBuilder();
                sb.Append(i == 0 ? "星区 01 · 波 " : "星区 02 · 波 ");

                bool any = false;
                for (int w = 0; w < waves.Length; w++)
                {
                    if (!WaveHas(waves[w], kind)) continue;
                    if (any) sb.Append('·');
                    sb.Append(w + 1);
                    any = true;
                }

                if (!any) sb.Append('—');
                result[i] = sb.ToString();
            }
            return result;
        }

        private static bool WaveHas(WaveDefinition wave, EnemyKind kind)
        {
            if (wave.kinds != null && wave.kinds.Length > 0)
            {
                for (int i = 0; i < wave.kinds.Length; i++)
                    if (wave.kinds[i] == kind) return true;
                return false;
            }
            return wave.kind == kind;
        }

        /// <summary>
        /// 一张敌人资料卡：切角底板 + 立绘 + 四项指标（血量 / 分值 / 行为 / 应对）。
        ///
        /// 只有 HP / KILL 这类三位数走等宽（保证四张卡上下对齐）；
        /// 行为、应对、出没关卡都是中文，一律走黑体粗体，不再用等宽拉开的小字。
        /// </summary>
        private void Card(RectTransform parent, Vector2 position, EnemyKind kind, int meteorSeed,
                          string displayName, string code, Color accent, string behavior, string counter)
        {
            GameObject go = new GameObject(code + "Card", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(CardW, CardH));
            RectTransform root = go.GetComponent<RectTransform>();

            // 底板：很暗的一层卡面，比主面板表面亮一档，卡片才浮得出来
            Image body = UIFactory.CreateImage(go.transform, "Body", UITheme.A(UITheme.SurfaceRaised, 0.96f));
            body.sprite = UISprites.Sheet(UITheme.CutCard);
            body.type = Image.Type.Sliced;
            UIFactory.Stretch(body.rectTransform);

            Image edge = UIFactory.CreateImage(go.transform, "Edge", UITheme.A(accent, 0.20f));
            edge.sprite = UISprites.SheetEdge(UITheme.CutCard, 1f);
            edge.type = Image.Type.Sliced;
            UIFactory.Stretch(edge.rectTransform);

            // 左上角一小段彩色强调线：四张卡靠这个颜色区分"危险等级"
            Image accentLine = UIFactory.CreateImage(go.transform, "AccentLine", UITheme.A(accent, 0.65f));
            UIFactory.Place(accentLine.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(UITheme.CutCard + 6f, -1f), new Vector2(64f, 2f));

            BuildIcon(go.transform, kind, meteorSeed, accent);

            // ID 行：中文名 + 代号（代号在右侧，等宽小字）
            UIChrome.CreateLabel(go.transform, "Name", displayName, UITheme.SizeH3,
                TextAnchor.MiddleLeft, UITheme.TextMain,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, 100f), new Vector2(200f, 38f));

            UIChrome.CreateLabel(go.transform, "Code", code, UITheme.SizeCaption,
                TextAnchor.MiddleRight, accent,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-22f, 100f), new Vector2(140f, 24f), true, true);

            // 指标行：血量 + 击毁分值
            EnemyConfig config = GameConfig.Enemy(kind);
            string hpText = "HP --";
            string scoreText = "KILL --";

            if (config != null)
            {
                if (kind == EnemyKind.Meteor)
                {
                    // 陨石的血量是一个区间：小的打两下就碎，大的要打一阵
                    int lo = Mathf.RoundToInt(config.maxHp * Meteor.HpScaleSmall);
                    int hi = Mathf.RoundToInt(config.maxHp * Meteor.HpScaleLarge);
                    hpText = "HP " + lo.ToString("000") + "~" + hi.ToString("000");
                }
                else
                {
                    hpText = "HP " + Mathf.RoundToInt(config.maxHp).ToString("000");
                }

                scoreText = "KILL +" + Mathf.RoundToInt(config.score);
            }

            UIChrome.CreateLabel(go.transform, "Stat", hpText + "   " + scoreText, UITheme.SizeLabel,
                TextAnchor.MiddleLeft, UITheme.A(accent, 0.95f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, 66f), new Vector2(300f, 26f), true, true);

            UIChrome.Hairline(go.transform, "CardRule", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, 44f), TextW, UITheme.EdgeHair);

            // 行为 / 应对：中文短标签 + 一句话正文。标签用 TextDim 粗体，
            // 之间是 4~8px 的固定呼吸，四张卡对齐同一套坐标。
            UIChrome.Eyebrow(go.transform, "BehaviorLabel", "行为",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(TextX, 28f),
                new Vector2(100f, 20f), TextAnchor.MiddleLeft, UITheme.TextDim, false);

            UIChrome.CreateLabel(go.transform, "Behavior", behavior, UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, 0f), new Vector2(TextW, 28f));

            UIChrome.Eyebrow(go.transform, "CounterLabel", "应对",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(TextX, -32f),
                new Vector2(100f, 20f), TextAnchor.MiddleLeft, UITheme.TextDim, false);

            UIChrome.CreateLabel(go.transform, "Counter", counter, UITheme.SizeCaption,
                TextAnchor.MiddleLeft, UITheme.TextMain,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, -60f), new Vector2(TextW, 28f));

            // 卡片下半部分：出没关卡（按 GameConfig 的实际波次表推出来，不写死）
            string[] habitat = Habitat(kind);

            UIChrome.Hairline(go.transform, "HabitatRule", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, -86f), TextW, UITheme.EdgeHair);

            UIChrome.CreateLabel(go.transform, "Habitat1", habitat[0], UITheme.SizeMicro,
                TextAnchor.MiddleLeft, UITheme.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, -102f), new Vector2(TextW, 24f));

            UIChrome.CreateLabel(go.transform, "Habitat2", habitat[1], UITheme.SizeMicro,
                TextAnchor.MiddleLeft, UITheme.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, -126f), new Vector2(TextW, 24f));
        }

        /// <summary>
        /// 立绘：底下一层冷色光晕，上面按 `bodyScale` 缩放到同一"目视大小"。
        ///
        /// 为什么要乘 bodyScale：`Meteor` 的贴图本身就是按体积缩放后在场景里用的，
        /// 不乘的话图鉴里那颗陨石会比游戏里小一圈，玩家对不上号。
        /// </summary>
        private void BuildIcon(Transform parent, EnemyKind kind, int meteorSeed, Color accent)
        {
            Sprite icon = kind == EnemyKind.Meteor
                ? SpriteFactory.Meteor(meteorSeed)
                : ArtLibrary.Get(ArtLibrary.EnemySpriteName(kind));

            if (icon == null) return;

            Image halo = UIFactory.CreateImage(parent, "IconHalo", UITheme.A(accent, 0.16f));
            halo.sprite = SpriteFactory.Glow();
            UIFactory.Place(halo.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(IconX, 6f), new Vector2(184f, 184f));

            EnemyConfig config = GameConfig.Enemy(kind);
            float body = config != null ? config.bodyScale : 1f;

            float unitW = icon.rect.width / icon.pixelsPerUnit * body * IconScale;
            float unitH = icon.rect.height / icon.pixelsPerUnit * body * IconScale;

            // 陨石可能被 bodyScale 放大到超出卡片，这里夹一次，宁可略小也别压到文字
            float maxW = 168f;
            float maxH = 176f;
            float clamp = Mathf.Min(1f, Mathf.Min(maxW / Mathf.Max(1f, unitW), maxH / Mathf.Max(1f, unitH)));

            // 图鉴立绘和战斗里用同一份双色调贴图，避免"图鉴一个色、打起来另一个色"
            icon = EnemyArt.Get(kind, icon);
            Image image = UIFactory.CreateImage(parent, "Icon", Color.white);
            image.sprite = icon;
            image.preserveAspect = true;
            UIFactory.Place(image.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(IconX, 6f), new Vector2(unitW * clamp, unitH * clamp));
        }

        // ================================================================== 交互

        public void Open()
        {
            SetVisible(true);
        }

        public void Close()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            // 先关面板再回调，否则菜单会在遮罩还亮着的时候被重新唤醒
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
