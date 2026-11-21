# 虚数星区 · UI 视觉规范（v2「战术终端」）

> 本文件是本次界面重做的唯一标准。所有 UI 代码必须照此执行，不要凭感觉另起一套。

## 0. 一句话目标

把「一堆半透明方块 + 四条细边框」做成**精密战术终端**：深色玻璃面板、45° 切角、
等比字号阶梯、等宽数字、克制的青色强调、不对称取景框。

## 1. 硬性纪律（违反即返工）

1. **只通过 `UIChrome` / `UISprites` / `UIWidgets` / `UIFactory` 造界面。** 不要再手写
   `AddFrame`、`CreateCornerMarks`、`PanelBg` 那套旧装饰；旧 API 还能编译，但新界面不许用。
2. **颜色只能从 `UITheme` 取。** 禁止 `new Color(...)` 写死颜色（唯一例外：给 `UITheme.A()` 传 alpha）。
3. **字号只能从 `UITheme` 取。** 禁止生造 31、27 这种字号；阶梯见 §3。
4. **每个面板 = `UIChrome.Sheet` + `Header` + `Footer`。** 标题不许再裸放在正中，一律进页眉。
5. **不要改别人的文件。** 共享代码（UIChrome 等）有 bug 就报给 Lead，不要自己改。
6. 面板类必须继续继承 `UIPanelBase`，保持 `InitGroup(false)` / `SetVisible` / `Build()` 的语义，
   事件订阅/退订、键盘导航、回调 `Action` 全部保持不变——**改的是画法，不是逻辑**。
7. 中文正文用默认字体；**数字、英文小标签用等宽**（`UIFactory.CreateTextMono`）。
8. 拉丁小标签必须拉开字距：`UIFactory.SpaceOut("SECTOR 02")` → `S E C T O R  0 2`。

## 2. 形状语言

| 元素 | 切角 | 用法 |
|---|---|---|
| 大面板 | `UITheme.CutLarge` (20) | 每个全屏面板的外框 |
| 卡片 / 模块 | `UITheme.CutCard` (12) | 选关行、战机卡、HUD 模块 |
| 小徽标 / 键帽 | `UITheme.CutChip` (7) | 序号徽章、标签、键帽 |

切角面板 = 四层叠出来（`UIChrome.Sheet` 已经封好）：
外发光 → 投影 → 主体（含顶部渐变）→ 描边环 → 顶边两段强调线（青 + 品红）→ 对角取景框。

## 3. 字号阶梯（只用这些）

| token | 值 | 用途 |
|---|---|---|
| `SizeDisplay` | 84 | 标题屏主标题（每屏最多一个） |
| `SizeHero` | 64 | 结算大分数 |
| `SizeH1` | 48 | 面板主标题（一般交给 `Header`，不用自己写） |
| `SizeH2` | 36 | 区块标题 / 主按钮 |
| `SizeH3` | 30 | 列表行标题 |
| `SizeValue` | 34 | 大号数值 |
| `SizeBody` | 26 | 正文 |
| `SizeLabel` | 22 | 次级标签 |
| `SizeCaption` | 20 | 说明文字 |
| `SizeMicro` | 18 | 徽标内文字 / 键帽 |
| `SizeNano` | 16 | 全大写拉丁微标签（短标识配 `SpaceOut`；长分区名见 §20，用 `SizeMicro` 粗体） |

文字颜色阶梯：`TextMain`（主）→ `TextDim`（次）→ `TextFaint`（说明）→ `TextMuted`（装饰/版本号）。
具体值见 §18：**不允许对这四档再乘一层 `A(..., alpha)`** —— 那会把文字压回"水印"状态。

## 4. 面板骨架（照抄这段结构）

```csharp
private UISheet sheet;

protected override void Build()
{
    InitGroup(false);

    // 1) 整屏压暗（子面板必须：底下的主菜单要沉下去）
    UIChrome.Scrim(transform, "Scrim", new Color(0.006f, 0.011f, 0.022f, 0.55f));

    // 2) 面板本体：中心锚点，(宽, 高)，位置
    sheet = UIChrome.Sheet(transform, "Sheet", new Vector2(1000f, 760f), Vector2.zero);

    // 3) 页眉：序号 + 标题 + 右侧元信息（英文大写，会自动拉开字距）
    UIChrome.Header(sheet, "02", "选择星区", "SECTOR SELECT");

    // 4) 页脚：左侧操作说明、右侧版本；会把内容区底部让开 66px
    UIChrome.Footer(sheet, "方向键选择 · 回车确认 · ESC 返回", "AUST·2024305305");

    // 5) 正文一律摆进 sheet.Content（它已经扣掉页眉和四周内边距）
    //    内容区坐标原点 = Content 中心
}
```

> 内容区尺寸 = 面板尺寸 − 左右各 `PadSide`(34) − 顶部 `HeaderHeight+6`(74) − 底部 `PadBottom`(30)；
> 调用过 `Footer` 后底部让开 66。**建议所有纵向坐标按"内容区高度的一半"来算**，不要写死屏幕坐标。

## 5. 列表行 / 按钮

列表行统一 `UIChrome.Action(..., UIActionStyle.Row)`：

```csharp
UIAction row = UIChrome.Action(sheet.Content, "Row01", "01", "星区 01 · 掠影带", "6 WAVES",
                               new Vector2(rowWidth, 84f), new Vector2(0f, y));
row.Button.onClick.AddListener(...);
```

- 行高 84，行距（步进）96；`Index` 左 18、`Label` 左 60、`Meta` 右 −46、箭头右 −14，已封好。
- 悬停自动铺底 + 竖条变宽 + 序号变亮 + 箭头右移（`UIButtonFx` 负责，别自己写 hover）。
- 选中态：把 `row.Body.color` 改成 `UITheme.AccentWashStrong`、`row.Edge.color` 改成 `UITheme.A(accent, 0.5f)`、
  `row.Label.color` 改成 `UITheme.TextMain`。

主按钮 `UIActionStyle.Primary`（宽 ≥ 380，高 84 左右），次按钮 `UIActionStyle.Ghost`（宽 260，高 60）。

## 6. 其它构件

```csharp
// 小标签
UITag tag = UIChrome.Tag(parent, "Tag", "已装备", new Vector2(96f, 26f), new Vector2(180f, 0f));

// 键帽提示（锚点左中）
UIChrome.KeyHint(parent, "ESC", "返回", new Vector2(0f, -40f));

// 细线 / 竖线 / 带菱形端点的分隔线
UIChrome.Hairline(parent, "Line", new Vector2(0f,1f), new Vector2(0f,1f), new Vector2(0,-20), 400f, UITheme.EdgeSoft);
UIChrome.VLine(parent, "V", new Vector2(0f,0.5f), new Vector2(0f,0.5f), new Vector2(0,0), 64f, UITheme.Edge);
UIChrome.Rule(parent, new Vector2(0f, 40f), 520f);

// 等宽大数字
Text value = UIFactory.CreateTextMono(parent, "Score", "001234", UITheme.SizeHero, TextAnchor.MiddleRight, UITheme.Cyan);

// 拉丁微标签（自动拉开字距）
UIChrome.Eyebrow(parent, "En", "IMAGINARY SECTOR", new Vector2(0f,0.5f), new Vector2(0f,0.5f),
                 new Vector2(0f, 0f), new Vector2(600f, 22f), TextAnchor.MiddleLeft);

// 分段进度条（自带组件，放在任意 RectTransform 下）
UISegBar bar = UISegBar.Create(parent, "Hp", new Vector2(360f, 14f), Vector2.zero, 20, UITheme.Cyan, UITheme.A(UITheme.Cyan, 0.12f));
bar.Set(0.7f);

// 细长数值条（带刻度）
UIMeter meter = UIMeter.Create(parent, "Stat", new Vector2(212f, 8f), Vector2.zero, 5, UITheme.Cyan);
meter.Set(rating);

// 扫光（只给主菜单 / 标题屏用，周期长、别抢内容）
UISweep.Attach(sheet.Root, UITheme.Cyan, 100f, 180f, 0.35f);
```

## 7. 各面板要点

- **主菜单**：左侧品牌区（logo + 中文标题 + 英文微标）／右侧纵向菜单列表（序号 + 行 + 箭头）；
  底部一条通栏细线；右下角历史最高（等宽大数字）。不要外框大卡片。
- **标题屏**：全屏构图，左下角 logo 与标题，右下角「按任意键」，中缝斜线/取景框。
- **HUD**：只在四角与上下边缘出现，中间绝不能挡画面。血条分段、分数等宽、武器芯片切角标签。
- **选关 / 机库 / 图鉴 / 设置 / 帮助**：面板 + 页眉 + 行或卡片 + 页脚，行/卡统一用 `Action` 或 `Sheet`(CutCard)。
- **暂停 / 结算**：面板更小（宽 ≤ 860），标题进页眉，主按钮 Primary，次按钮 Ghost。

## 8. 验收标准

1. `dotnet build Temp\verify\Verify.csproj -v q` → **0 个错误**（必须先过这关）。
2. 全部界面的字号、颜色、切角都来自 `UITheme`；`grep "new Color(" 你的文件` 只允许出现在 `UITheme.A(...)` 里。
3. 键盘导航、回调、`SetVisible` 行为与改前一致。
4. 汇报时给出：改了哪些文件、每个文件的版面结构一句话、编译结果。

## 9. 修订记录（2026-11-10 第二轮）

第一轮把十来个面板全部按本规范重做，跑完离屏截图逐张复查，补了三条硬规则：

1. **锚点必须成对思考。** 想写"相对中心往左 40"时，`anchor` 与 `pivot` 都要给
   `(0.5, 0.5)` / `(0, 0.5)`；只要 `anchor` 写成 `(0, 0.5)`（父级左边缘），
   那个 `-40` 就变成"再往左 40"，整块内容会画到面板外面。本轮图鉴与帮助页就是这样翻车的。
2. **选中态不能用"半透明底色"。** `UISheet` 的层次是 Glow → Shadow → Body → Sheen → Edge，
   底板一旦半透明，背后同色的 Glow 就透成一块实心色块（机库选中卡曾经整张泛琥珀）。
   选中信号只交给：描边 `Edge`、外辉光 `Glow`、名字颜色、顶部强调条。
3. **渐变按方向生成，别旋转复用。** 竖向渐变旋转 90° 后有效宽度变成屏幕高度，
   暗坡会在中途断掉并留下一条硬竖边。横向渐变用 `UISprites.GradientH()`。

另外两处清理：

- 删掉 `UIChrome.Header` 里右下角的 `// 0X` 装饰字——它挂在页眉下缘之外，
  与每个面板顶部的 eyebrow 撞成乱码；序号已经由左侧徽章表达，不需要重复。
- `UIFactory.SpaceOut` 遇到原有空格时补足三格，否则 `IMAGINARY SECTOR` 会被拉成
  `IMAGINARYSECTOR`（逐字加字距把词间空隙一起吞掉了）。

验收仍然以 `Temp\uishots\before\` 下的 11 张离屏截图为据（标题 / 菜单 / 选关 / 机库 /
图鉴 / 设置 / 说明 / HUD / 暂停 / 结算），脚本见 `Assets/Editor/UIShotFlow.cs`。
## 10. 第二轮修订（背景 / 战机 / 字号 / 返回按钮）

1. **背景不再用外部照片**：游戏内与主菜单背景统一改由 `FX/BackdropArt.cs` 程序化生成（`Sky(sector)` 天幕 + `Planet(sector)` 行星），
   色板按星区切换；`Starfield` 删掉竖向汇聚网格线，横向透视线收到 7 条、alpha ≤0.055。
2. **战机改程序化**：`Core/ShipArt.cs` 逐像素生成四架剪影，走 `ShipArt.Get(ship)`；不要再引用 `ArtLibrary` 的战机 png。
   战斗里额外铺一层 `HullGlow`（同 `ship.accent`、alpha 0.16、sortingOrder 9），深色剪影才不会糊进星空。
3. **底板必须接近不透明**：`UIChrome.Sheet` 的 Body alpha 固定 0.985。0.94 会让背后的行星透上来，像隔着毛玻璃。
4. **字号阶梯上调一档**：Body 26 / Caption 19 / Micro 16 / Nano 14。改 token 即可，不要在面板里写死字号。
   （这一档后来又被 §18 上调一次，以 §18 / `UITheme.cs` 为准。）
5. **子面板必须有可点的返回**：`UIChrome.Action(...,UIActionStyle.Ghost)` + `Close()`，与 ESC 走同一条路径；
   底部 Footer 仍保留 "ESC 返回主菜单" 作为提示，但不再是唯一出口。
6. **文本框宽度按「面板宽 − 2×34 − 120」反推**，再加 10% 余量；`CreateText` 是 Overflow，不会自动换行也不会裁剪。
## 11. 第三轮修订（层次与可读性）

1. **背景要分层且会动**：`BackdropArt.Sky` 是天幕，`BackdropArt.Wisps` 是第二层薄雾，行星是第三层；
   `Starfield` 用 `planetBlend / nebulaBlend` 做交叉淡化，换星区不允许"瞬间换图"。
2. **行星只露一弧**：中心推到画外（`halfW*0.88`），tint alpha 0.80。整颗球摆在画面里会抢界面层级。
3. **战机 384×384**：`ShipArt` 的细节量以"机库卡片 176 见方仍能看清舱盖刻线"为准。
4. **选中态可以有动效，但必须克制**：`UISheen` 2.6s 扫过 / 4.2s 静止 / 峰值 alpha 0.11，
   并且必须 `RectMask2D` 裁在卡片内。扫光只给"当前选中"那一个。
5. **HUD 数字一律等宽**（`UIFactory.CreateTextMono`），中文字号与数字分开调：
   星区名 36、芯片文字 20、刻度 16，小标签 16 起，不允许再出现 15px 以下的可读文本。
6. **空的地方放"信息"而不是放装饰**：图鉴卡片下半部空 → 补「出没关卡」并让它从 `GameConfig.Levels()` 算出来，
   而不是写死字符串。
## 12. 阵营与机体配色

1. **战机**：`ShipArt.Finish()` 在中间调做 50% 机体色混合（`hue = accent * (0.60 + 0.85*lum)`），
   外描边（lum < 0.055）与倒角高光不染色——否则会从"金属"变成"塑料"。
2. **座舱玻璃**：`gHot = Mix(GlassHot, accent, 0.34)`、`gDeep = Mix(GlassDeep, accent*0.62, 0.52)`，四架的玻璃色因此不同。
3. **敌人**：颜色只有一个来源 `EnemyBase.TintFor(kind)`（突进者珊瑚红 / 炮台琥珀 / 追踪弹紫罗兰 / 陨石冷白）。
   `sr.color`、图鉴图标、爆炸与拖尾全部取它；原图是 Kenney 白模，靠乘法染色，所以颜色改这一处就全局生效。
4. **不新增发光层**：敌人同屏数量大，再加柔光叠层会糊成一片；阵营辨识交给"颜色 + 剪影"两条通道。
## 13. 标题屏：字标不重复

logo 素材本身就是「虚数星区」字标，下面**不要再排一遍同名字体大字**（SizeDisplay 级别的重复标题会变成两张标题）。
标题屏左列从上到下只有：logo → 英文微标 `IMAGINARY SECTOR / S07` → 中文副标「赛博朋克 · 太空射击」→ 420px 青色细线。
`TitleScreen.cs` 里 `TitleY` 常量已删除；`SubtitleY = 112f`、`AccentY = 58f`。
## 14. 双色调（战机与敌人）

单色滤镜是"刷了一层漆"，双色调才是"配色"。两处实现，思路一致：**一个色相管机身、另一个色相管外侧/暗部**。

1. **战机**（`ShipArt.Finish`）：按归一化横向坐标 `u` 渐变。`|u| < 0.30` 用 `accent`（机身），
   `|u| > 0.60` 完全切到 `accentAlt`（机翼外侧与翼尖），中间是平滑过渡。
   两色必须同族（冷配冷、暖配暖），否则机翼会像贴了另一架飞机。
   `ShipConfig.accentAlt` 是数据字段，四架分别是：疾风 深钴蓝 / 蜂刺 松石绿 / 重锤 铁锈红 / 赤影 紫罗兰。
2. **敌人**（`FX/EnemyArt.cs`）：按亮度做 duotone —— 暗部映射到 `Shadow`、亮部映射到 `Light`，
   中间再乘 `0.74 + 0.54*lum` 保住面板线，高光侧加 0.12 的过量光。
   注意 Kenney png 没开 Read/Write，必须 `Graphics.Blit` 到临时 RT 再 `ReadPixels`。
3. **敌人贴图重绘后不要再用 `sr.color` 染色**（会二次相乘变暗）：`OnSpawn` 把 `sr.color` 置白，
   颜色已经烘焙进贴图；受击灼烧改成朝 `BurnHot (2.1,1.9,1.75)` 推，靠 >1 的分量做过曝。
4. **池复用**：重绘前把 `sr.sprite` 存进 `sourceSprite`，每次出生都从原图重绘，避免在染色图上反复叠加。
5. **第二色相只给高光**：敌人 duotone 之上再加一个 `Accent` 色（和机身色相对撞：珊瑚红→冷青、琥珀→钢蓝、紫罗兰→暖琥珀），
   只作用于 `t > 0.90` 的最亮像素、强度 0.32。面积一大就变成两种颜色打架（第一版 0.55/0.78 阈值就翻车了）。
6. **战机同理**：`accentAlt` 只铺到 `|u| > 0.30` 的翼展外侧，机身中线保持主色——"机身一色、机翼一色"才是配色，
   整架渐变到另一个颜色反而像没上色。
## 15. 收敛到一架战机

1. **别用数量掩盖质量**：四个"过得去"的剪影，不如一个能近看的。做课程设计时先问"玩家真的会切换吗"，
   不会就砍掉入口（`ShipSelectPanel`、主菜单「选择战机」、`BindSubPanels` 的 hangar 参数一起删）。
2. **数据只剩一条也要留在 `GameConfig`**：`Ships()` 返回单元素数组，`PlayerLoadout` / HUD 铭牌 / 结算都走同一条链路，
   以后想加第二架不需要改界面代码。
3. **贴图上限跟着"最大显示尺寸"走**：展示位 214px，贴图给到 512（约 2.4×），DPI 与抗锯齿都留了余量；
   线宽是按像素写的，贴图变大 = 刻线更细，这是最省力的"精修"。
4. **细节要分层**：结构线（机身/翼/舱）→ 面板缝（装甲带、检修口）→ 微细节（铆钉、格栅、同心环）。
   微细节一多就会糊，所以只在大块留白处点，且只用 `StyleSeamFaint`（比主缝更弱一档）。

## 16. 字体与字号的最后一遍

1. **等宽字体优先 Cascadia Mono**（Windows 11 自带），字形方正、`0` 带点，读数字比 Consolas 更像仪表；
   机器上没有才退回 Consolas。改的是 `UIFactory.PreferredMono`，全局生效。
2. **字号只留一条阶梯**：Display 84 / Hero 64 / Score 58 / H1 48 / H2 36 / Value 34 / H3 30 / Body 26 /
   Label 22 / Caption 20 / Micro 18 / Nano 16。面板里出现裸数字（如旧的 `58`）一律补成 token。
3. **HUD 三处永远会撞，改版面时先检查**：
   - 左下「WEAPON 01」小标要在武器芯片**上方** 16px（芯片高 42 + 间隙），否则被芯片切掉半行；
   - 左上「STABLE / CRITICAL」右对齐要留 **24px** 内边距，否则压到模块的切角；
   - 右上「SCORE」标题离切角 **30px**、大数字右边距 **24px**，`SizeScore` 58 是能塞进 404×92 模块的上限。
4. **能一眼看出来的错位，都是"两个元素抢同一块像素"**：用裁切截图（Pillow 裁 1920×1080 的局部）比看整屏更容易发现。


## 17. 按参考图改造程序化剪影

1. **先拆层，再画线**：一架船拆成「主翼 / 机身 / 引擎舱 / 背脊 / 进气口」五个 Role，
   每层一个多边形，靠 `RolePriority` 决定压盖顺序。参考图的"层次感"来自层与层的遮挡，不是刻线数量。
2. **刻线只画"边界的内侧那条"**：装甲板的四条边里，靠外的那条通常就是剪影轮廓或翼前缘强调线，
   再画一遍会脏。板线一律用 `StyleSeam`（主缝）+ `StyleSeamFaint`（发丝缝）成对出现，才有板厚。
3. **逐机染色强度**：`Design.TintMix` 默认 0.50，深色金属机身要降到 0.30 ——
   否则中间调被 accent 洗满，机身会"泛金/泛紫"，看起来像塑料而不是金属。
4. **双色分工**：机翼用 `accentAlt`（冷色）、机身用 `accent`（暖色）、喷口用暖色高光。
   参考图那种"深蓝灰 + 暖尾焰"就是靠这个分工成立的；两边都用同色系会变成单色滤镜。
5. **展示位就是验收位**：战机在菜单里只有 214px，看不清的细节等于没画。
   改完先裁 menu 的展示框放大 3 倍看轮廓，再看战斗里 60px 的读法。


### 17.1 补：板面 Role 与整流罩（第二轮）

- **新增 `RolePlate = 7`**，优先级 30（`RolePriority` 数组末尾追加）：装甲板盖住机翼与引擎舱，又被机身压住。
  单独一个 Role 才能有独立的明暗曲线 —— 只靠刻线，板面和机翼永远是同一个反射率，"分块"读不出来。
- **板面配色** `PlateLit (0.455,0.520,0.600)` / `PlateDark (0.155,0.185,0.245)`：比机翼亮一档、偏冷。
  公式 `PlateLit + (PlateDark-PlateLit)*pow(k,1.15)`，`k` 是离板心的横向归一化距离；再乘 `0.95+0.09*vNorm` 让板面随前后方向轻微变化。
- **翼根整流罩**：机翼后缘与引擎舱前缘本来是硬接（一条斜线顶到圆柱），补一块六边形整流罩过渡，
  体积感来自"过渡件"，不是来自把接缝画细。
- **顺序经验**：先确 Role 与优先级（谁压谁），再填多边形，最后才补刻线和铆钉。
  反过来做，刻线会被后画的板面盖掉，白画。


### 17.2 补：明暗的方向感

- **板面渐变跟着后掠角走**：纯横向渐变（`k = |u|`）会让板面像"左右各刷一道"，与翼形无关。
  改成斜向合成 `k = 0.62*chord - 0.46*(v - By0) + 0.46`，明暗带就与翼前缘平行，读起来是"沿翼展方向的结构件"。
- **机身要有纵深**：机身压边高光如果亮度一致，整机像平面贴图。加一条随前后位置的乘子
  `c *= 0.90 + 0.16*SmoothStep(-0.95, 0.75, v)`：机鼻略提亮、机尾自然入影。
- 这两处都只改 `BaseColor` 的渐变，不动剪影，属于"低风险高收益"的调整；改完照例裁菜单展示位放大 3 倍看。


## 18. 动态装饰：尾焰与航行灯

剪影是静态资产，但玩家对"有没有在加速"的判断靠的是动态线索。两件低成本的事：

1. **尾焰**（`Player/ThrusterFlame.cs`）：两个喷口各三层径向柔光（核心 / 主焰 / 外层晕），
   `len = baseLen * (0.55 + throttle*1.15) * flicker`，`throttle = speed/7.5`。
   静止时留 55% 的"怠速焰"，完全不动会显得船在漂；横向移动时整体反向倾斜 `-vx*1.6°`。
2. **航行灯**（`Player/NavLight.cs`）：`t = Repeat(time/period + phase, 1)`，只在每周期最后 18% 亮起，
   `pow(lit, 2.6)` 让闪得短促。**左右灯必须不同相**（0.00 / 0.46），同相同频会被读成"整体呼吸"而不是灯。
3. **单位陷阱（踩过两次）**：`ShipArt` 的 `u/v` 是 `[-1,1]` 归一化坐标，对应 sprite 只有 `±0.5` 世界单位。
   把贴图坐标直接当世界坐标用，尾焰会甩到船尾下方半个机身外——所有"从贴图坐标换算到子物体位置"的地方都要乘 0.5。
4. **不要用 `SpriteFactory.EngineGlow()` 当可染色焰心**：它的贴图自带青蓝色梯度，乘琥珀色会变绿。
   要染色的光晕一律用白色径向的 `SpriteFactory.Glow()`，再按 `sprite.bounds.size` 反算缩放，
   这样 `width/height` 才是真正的世界单位。


---

## 19. 第四轮修订 · 小字可读性（2026-11-10）

起因：1080p 实机逐屏截图里，标题页脚、图鉴卡片标签、帮助页键位说明、
设置页 BGM/SFX、HUD 刻度与状态词**集体读不清**。裁 2 倍放大后确认：
字形边缘是锐利的，不是字体渲染糊，**根因是「字号太小 + 颜色太暗」两件事叠在一起**。

1. **文字四档整体上抬，但保持有序**（改 `UITheme.cs`，一处生效）：
   `TextMain 0.93` → `TextDim 0.70` → `TextFaint 0.56` → `TextMuted 0.42`。
   旧 `TextDim(0.56)` 正好落在新 `TextFaint` 的位置，所以层级关系没变、只是整体变亮。
   Linear 色彩空间下深色底会再吃一档亮度，设计值 0.34 / 0.23 到屏幕上已经是水印。
2. **小字四档上调一档**：`SizeLabel 22` / `SizeCaption 20` / `SizeMicro 18` / `SizeNano 16`。
   阶梯仍然有序，没有生造 31、27 这种裸字号。
3. **禁止二次乘 alpha**：把 `A(TextDim, 0.55)` / `A(TextDim, 0.62)` / `A(Cyan, 0.30)`
   这类写法收敛成 token 本身或强调色直给。选中/未选中的差别靠**颜色 + 粗细 + 描边**表达，
   不靠"把未选中的字压到看不见"。
4. **裸字号全部补成 token**：HUD 血条刻度原为 `11`（违反 §11.5），键帽原为 `14`，
   现分别用 `SizeNano` / `SizeMicro`。
5. **中文不要走等宽字体**：图鉴「出没关卡」两行原来 `mono: true`，
   Cascadia Mono / Consolas 没有中文字形，会回退成杂糅字形且行高不稳。改成默认中文字体。
6. **说明性文字用 `TextDim` 起步**：帮助页键位表右侧四行说明原先用最暗的 `TextFaint`，
   但那是要被读的内容，不是装饰。只有版本号、页脚署名这类才用 `TextMuted`。

`CreateText` 的 `horizontalOverflow` / `verticalOverflow` 都是 `Overflow`，
所以字号上调不会把字裁掉，但**会挤到相邻元素**；改完必须逐屏截图验收间距。


---

## 20. 第五轮修订 · 英文微标的字距与字重（2026-11-10）

起因：用户截了一张 586×537 的「操作说明」面板图，反馈「好多地方字体压根看不清」。
把四块小字裁 2 倍放大后确认，这一轮和 §19 不是同一个问题：

**§19 修的是「太小 + 太暗」，这一轮修的是「字形被拆散」。**
`SpaceOut` 会逐字插一个空格，`FLIGHT CONTROLS` 实际渲染成 `F L I G H T   C O N T R O L S`。
16px 等宽细体 + `TextFaint/TextMuted` 在深色底上，每个字母都成了一座孤岛，
扫一眼只看到一排点，拼不回单词 —— 这才是「压根看不清」的主因。

### 短标识与长微标分开处理

- **保留 `SpaceOut`（逐字拉开）**：键帽 `W A S D`、HUD 状态词 `STABLE / CRITICAL / COMBO`、
  `WAVE 01 / 02`、Boss 名、战机呼号。这些只有三五个字符，拉开反而一眼能认。
- **长分区微标改 `SpaceOutTight`**：字母之间不插空格，只在原有词间补一格。
  `FLIGHT  CONTROLS` / `FIELD  MANUAL  ·  V2` 仍是全大写等宽的终端味道，但字形连成了词。
  一处生效覆盖 `Eyebrow`、`Header.Meta`、`Footer.Right`、`UIAction.Meta`、`Announcement`。
- 这些微标同时升到 `SizeMicro(18)` + **等宽粗体**，颜色从 `Faint/Muted` 两档
  改用 `Dim/Faint` 两档。层级关系保留（主标亮、计数弱），但最暗的 `TextMuted` 不再给微标用。

### 顺带清掉的三处

1. `Tag` 提示条文字原来 `SizeMicro(18)` 且**没有加粗**（`filled` 被顺手当 bold 参数用，
   多数调用点传的是 false），细体青字在框里像一条发光虚线。改成 `SizeCaption(20)` + 固定粗体，
   五个调用点的框高 28 → 32 留出呼吸。
2. 键位表右侧说明 `SizeCaption(20)` 对左侧动作名 `SizeH3(30)` 差一整档，提到 `SizeLabel(22)`；
   任务规则左侧「目　标」这类全角空格拉开的两字标签改回紧凑写法。
3. `UIAction` 按钮底部副标（"ESC"）原来是 `A(a, 0.45f)` 半透明，提到 `0.90f`。

### 一个容易看错的坑

`UITheme.A(color, a)` 是**把 alpha 直接设成 a**，不是乘算。
而 `UIChrome.Tag` 内部对 body / edge / label 分别调用了 `A(a, 0.16/0.22/0.95)`，
每一处都会把 alpha **重新覆盖**，所以调用点写 `Tag(..., A(Cyan, 0.45f))` 和写
`Tag(..., Cyan)` 在视觉上完全等价 —— 提示条淡的原因是**字太细**，不是透明度。
这次把这几处调用改成直接传 `UITheme.Cyan`，免得后来的人误判。

### 验收口径

`CreateText` 的 `horizontalOverflow / verticalOverflow` 都是 `Overflow`，
字号和字重上调不会裁字，但**会挤到相邻元素**；`SpaceOutTight` 让文本变短，一般不会撑破，
反而要留意右对齐的计数微标别和左对齐的主标连成一片。必须逐屏截图复验。


### 收尾（v50 复验后补）

- **按钮角标会压主字**：`UIAction` 的 Ghost / Primary 分支里，主文字居中于按钮中心，
  而 `Meta`（"ESC"）挂在按钮底部内侧，两者原本就重叠；以前 `SizeNano + A(a, 0.45)`
  几乎看不见，这轮把角标加粗提亮后，"ESC" 直接横穿「返回」二字。
  修法是按「按钮底为 0」重新分带：带角标时主文字上抬 12px，角标降到 `y=3 / 高 20`，
  主字占 27~57、角标占 4~22，留 5px。`UIButtonFx` 只改 `label.color`，不会重置位置。
- `Announcement.Play()` 运行时会给 eyebrow **重新赋色**，只改 `Eyebrow` 的默认色不够，
  这一处必须同步改成 `TextDim`，否则公告微标还是淡的。
- 标题页署名原本 `SizeMicro + TextFaint`，比主菜单同名署名（`SizeLabel + TextDim`）弱一档，统一到后者。
- 帮助页 `MISSION RULES` 段整体下移 12px（eyebrow 两条 + 横线），
  把加粗后的分区名与上一行「暂停」拉开，横线到首行规则仍有 44px。


---

## 21. 第六轮修订 · 排版系统重设计（2026-11-10）

起因：用户第三次反馈字体问题，贴了图鉴 / 说明 / 设置三张截图，措辞是
「非常不合适、非常模糊、非常丑，请优化这个设计」。
**§19 抬字号亮度、§20 改字距字重都是参数级修补，这次问题在设计本身。**
三张截图分别是 754×562 / 589×538 / 496×331，而 UI 按 1920×1080 排版，
等于缩小到 0.26~0.39 倍观看 —— 这是实际使用场景，不能只在 1080p 下验收。

### 三个病根（裁 3 倍放大 + 读代码交叉确认）

1. **中文全是细体**。`UIChrome.CreateLabel` 的 `bold` 默认 `false`，敌人名、
   卡片正文、键位说明、任务规则、音量标签无一例外走 `FontStyle.Normal`。
   雅黑 Regular 在深色底 20~26px 只有 1px 笔画，缩小后糊成灰条。
   这才是「非常模糊」的主因，不是字体渲染问题。
2. **等宽全大写英文点阵泛滥**。图鉴一屏有 `HOSTILE PROFILES`、
   `THREAT INDEX 01-04`，加四张卡各两条 `BEHAVIOR` / `COUNTERMEASURE`，
   共 8 条 18px 等宽微标。中文界面里这些英文既无信息价值，又散成字母点阵。
   这是「丑 / 不合适」的主因。
3. **层级过密**。卡片 292px 高里塞了 30 / 20 / 18 / 20 / 16 五档小字，
   主次不分，字号再抬也只会互相挤。

### 设计决策

- **中文一律粗体**：`UIFactory.CreateText` / `CreateTextFont` / `CreateTextMono` /
  `CreateTextSpaced` 与 `UIChrome.CreateLabel` 的默认 `bold` 全部改成 `true`。
  一处生效、全工程一致，不会某个面板粗某个面板细。显式传 `false` 的少数调用保持。
- **区分「功能标签」与「品牌铭牌」**：`UIChrome.Eyebrow` 按内容自动分流 ——
  含 CJK 字符 → 黑体 `SizeLabel(22)` 粗体；纯拉丁 → 等宽 `SizeCaption(20)` 粗体，
  且**不再插任何空格**。功能性分区名全部中文化：
  `敌人档案 / 共 4 种敌机 / 行为 / 应对`（图鉴）、
  `飞行操作 / 4 组按键 / 任务规则 / 4 条说明`（说明）、
  `音量设置 / 2 路通道 / 调整后自动保存`（设置）。
  品牌铭牌保留英文终端味（`IMAGINARY SECTOR`、`SCORE`、`WAVE`、页眉 meta、页脚 right），
  但 `Header.Meta` / `Footer.Right` 去掉双空格、加大加粗、从 `TextFaint` 提到 `TextDim`。
- **`SpaceOutTight` 退役并删除**（§20 的方案）。`SpaceOut` 只保留给
  HUD 状态词这类三五字符的短标识；长串一律不拉字距。
  （键帽后来也改成紧凑排版、不再拉字距，见 §22。）
- **图鉴卡片重排**：10 个元素在半高 146 的卡片里重新分带，
  主次收敛成 30 / 22 / 20 / 18 四档全粗体；代号 16→20 且不再压暗；
  HP/KILL 20→22；出没关卡 16→18 并从 `-112 / -134` 上收到 `-102 / -126`，
  卡片底边留 8px。标签与正文之间保持 4~8px 固定呼吸，四张卡共用同一套坐标。

### 口径

- 文字层级现在靠**字号 + 字重 + 颜色**表达，不再靠「大写 + 拉字距 + 压暗」。
  暗到看不见不等于有层次。
- `horizontalOverflow / verticalOverflow` 都是 `Overflow`，加粗不会裁字，
  但中文粗体比细体略宽，卡片正文仍保持 `SizeCaption(20)` 不上提到 22，
  否则 `直线冲锋，撞上掉 20 点船体` 会顶出 `TextW(282)` 和卡片右缘。
- `Eyebrow` 的 `spaced` 参数保留兼容旧调用点，但已不生效。
  **`box.y` 必须照调用点原值**：Eyebrow 大多是顶部对齐（pivot.y=1），
  把 box 撑高会把文字中心一起往下推，v51 实测「任务规则」被分区横线穿过。
  `verticalOverflow = Overflow` 保证 22px 字装进 20px box 不会裁；
  紧跟标签的横线统一下移 4px 到 `-30`（选关页本来就是 -30），留 7px 呼吸。
- 设置页 `HintLine` 中文化后与提示条「调整立即生效并保存」语义重复，
  改写成「音量设置随存档保留」，只补充「存到哪」，不说第二遍「自动保存」。

### 验收口径（新增）

必须在**两个尺度**同时看：1920×1080 实机图看布局是否溢出，
再把同一张图缩到用户实际观看的 ~500~750 宽看笔画是否立得住。
只在 1080p 下「看着还行」不算通过。

---

## 22. 第七轮修订 · 键帽文字溢出（2026-11-10）

起因：用户贴了操作说明页键位表的局部截图（478×178），原话
「这个 space 都溢出来了，很不合理啊」。裁 6 倍放大后可以清楚看到：
`S P A C E` 里只有中间的 `P A C` 落在一枚约 55px 的深色圆角胶囊上，
首尾 `S` / `E` 整个飘在胶囊外的灰底上；`ESC` 也只有中间的 `S` 在框里。

### 病根：宽度按「原字符数」算，文字按「拉字距后」的字符数排

`HelpPanel.KeyRow()` 里两处对不上：

- 键面文字用 `UIFactory.SpaceOut(key)`：`SPACE` → `S P A C E`，5 字符变 9 字符；
  18px 粗体等宽约 11px/字，实际占宽 ~90px。
- 键帽宽度 `CapWidth()` 是 `14 + 8.2 × key.Length`：`SPACE` 只给 55px、
  `ESC` 只给 38.6px。这个系数是**字号还小、文字不拉字距**那一版留下的。
- 全局 `horizontalOverflow = Overflow`（`UIFactory.CreateText`），文字又 `Stretch`
  铺满键帽并 `MiddleCenter` → 装不下不换行、不裁字，直接以胶囊中心向两侧溢出。
  所以不是「字被压扁」，是**字根本没被容器约束**。

⚠️ 这类问题只靠 1080p 整屏图容易漏看：键帽在整屏里只有几十像素，
必须裁出来放大，才看得出「框」和「字」的边界对不上。

### 改法：键帽字不拉字距 + 宽度按实际字宽重算

- ✅ `KeyRow` 键面文字改成直接写 `key`，不再 `SpaceOut`。键帽是「实体按键的键面」，
  字母应当紧凑；拉字距既不像按键，也必然挤爆容器。对
  `W / A / S / D / 1 / Q / 2 / E` 这些单字符**没有任何视觉变化**
  （`SpaceOut("W") == "W"`），只修 `SPACE` / `ESC` 两枚。
- ✅ `CapWidth()` 改成 `Max(32, 22 + 11.5 × len)`：18px 粗体等宽按 11px/字留余量，
  左右各 11px 内边距。单字符 33.5px（接近原来的 30），`ESC` 56.5px，
  `SPACE` 79.5px —— 空格键自然成为一枚**横向长键帽**，和真实键盘一致，
  比原来「五个字母硬塞进单字宽」合理。
- ✅ 边界复核：键帽列从 `KeyX = -180` 起，最宽的 `SPACE` 右缘 -100.5，
  说明列从 `DescX = 76` 起，中间仍有 176px；四行都撞不到说明列。

### 口径收窄

§21 原写「`SpaceOut` 只保留给键帽 `W A S D`、HUD 状态词这类短标识」，
本轮把**键帽也移出适用范围**：`SpaceOut` 现在只用于没有硬边界的短标识
（`STABLE / CRITICAL / COMBO / WAVE / WEAPON / PHASE` 等）与少数长标题铭牌。
**凡是带背景框、有固定宽度的徽标 / 键帽 / 标签，一律不拉字距** ——
字距是给「自由排版」用的，不是给「定宽容器」用的。

### 验收

v53 一轮 10 张 1920×1080 实机截图；键位表裁 2 倍做前后对照，
再按用户截图的 0.62 尺度（478×178）做同尺寸对照，
`SPACE` / `ESC` 都完整收在胶囊内、四行对齐、小尺寸下笔画清晰。
编译 75 文件 / 0 错误，`AutoRun = false`（`Build = "v53"`）。
改动仅 `HelpPanel.cs` + 本文档，备份 `_tools/_backup_space_20261110/`。

---

## 23. 第八轮修订 · 主菜单页脚压线（2026-11-10）

起因：用户贴主菜单右下角 268×55 局部截图：「还有这里的历史最高分数都压在横线上面了」。
放大 6 倍确认：数字 `005073` 的下缘正好与右下角取景器角标的亮青色水平段重合，
看着像"骑"在线上；上方两条浅横线也只隔了几像素。

### 病根：页脚文字被塞进取景框的装饰线带里

主菜单底部有两套各自独立、坐标体系却不同的元素。统一换算成
「距屏幕底缘多少像素」才看得清冲突：

| 元素 | 距底 | 来源 |
|---|---|---|
| `FooterRule`（页脚上分隔线） | 66 | `BuildFooter`，画布 y = -474 |
| `BottomRule`（取景框下标尺线） | 54 | `BuildFrame`，anchor(0,0) + (160, 54) |
| 页脚文字中心 | 34 | `BuildFooter`，画布 y = -506 |
| 右下角 `Bracket` 水平段 | 26 | `BuildFrame`，`Bracket(... inset 26)` |

24~28px 高的文字中心放在 34px，下缘约 20~22px —— **26px 的角标亮线正好穿过字脚**；
上缘约 46~48px，离 54px 的标尺线只剩 6~8px。这条缝一共 28px 高，
根本放不下一行字，调字号、颜色、亮度都救不了。

⚠️ **坐标口径易错**：`BuildFooter` 用 Center 锚点（画布 y 为负），
`BuildFrame` 用屏幕角锚点（距底为正）。不换算就会误以为
「-506 到 -474 有 32px，够宽」—— 那 32px 是**同一条带内部**，
并不是与角标、标尺线分开的空间。

### 改法：整条页脚带上抬 48px，让文字落在两条浅线之间

`MenuPanel.BuildFooter()` 只改 4 个数值：

- `FooterRule` `-474 → -428`（距底 66 → 112）
- `teamText` / `bestLabel` / `bestValue` `-506 → -458`（距底 34 → 82）

改完文字上缘 96、下缘 68：上方 `FooterRule` 112 留 16px、
下方 `BottomRule` 54 留 14px；右下角标 26 退到文字下方 42px，
`005073` 不再与任何线相交。页脚仍贴底（中心距底 82px），
离上方 `hintText`（-252 / 距底 288）还有 176px，不挤菜单。
这次**没有加任何注释或新构件**，改动就是四个常量，避免动到出场动画取的位置基准。

### 验收

v54 一轮 10 张 1920×1080 实机截图；右下角页脚 3 倍前后对照 +
按用户截图 0.64 尺度（268×55）三栏同区域对照，`005073` 与亮线完全分开；
整条页脚缩到 960×75 复核左右两端无重叠、与上方按钮不打架。
结算页的「历史最高」是 Sheet 内上下居中排版
（`ResultPanel` 的 `Rule` 在 y=46、文字在 -6 / -50），间距充足，未改。
编译 75 文件 / 0 错误，`AutoRun = false`（`Build = "v54"`）。
改动仅 `MenuPanel.cs` 的 4 个坐标，备份 `_tools/_backup_footer_20261110/`。
