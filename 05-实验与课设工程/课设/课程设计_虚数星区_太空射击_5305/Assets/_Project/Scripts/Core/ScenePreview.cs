using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 编辑模式预览层（挂在场景根节点 _Preview 上）。
    ///
    /// 场景里这一层摆的是**开屏那一版的构图**。它的唯一用途是：
    /// 不运行游戏、只是在 Unity 里打开 Game.unity 时，Game 视图里看到的就是
    /// 玩家启动后第一眼会看到的画面，而不是一片空黑色。
    ///
    /// 现在摆的是标题屏（TitleScreen）那一套：
    ///   backdrop   ── bg_title.png 关键帧（战机 + 紫行星），缩放 1.037 刚好铺满正交相机
    ///   logo       ── ui_logo.png，挪到左上 (-5.807, 3.235)、缩到 680 UI 宽
    ///   overlay    ── ui_title_overlay.png，把副标两行 / 「进入游戏」/ 提示 / 页脚
    ///                 这些 UGUI 文字和按钮烤成一张 1920×1080 透明图
    ///
    /// 为什么文字要走贴图：UGUI 在编辑模式下不渲染（Canvas 是运行时建的），
    /// 而世界空间里手摆 TextMesh 又对不齐 UI 常量。烤一张按 1920×1080 UI 坐标
    /// 排好的透明图，缩放 1.037 之后 1 像素对 1 像素，和运行时完全重合。
    /// 代价是这张图不会跟着 TitleScreen 的常量自动变——改版式要重跑
    /// `_tools/gen_preview_overlay.py`。
    ///
    /// 星球 / 56 颗星尘 / 小战机都关掉了：关键帧里本来就有，再叠一层会脏。
    /// （它们留在场景里没删，改回星云背景时直接打开即可。）
    ///
    /// 真正可玩的内容全部由 GameBootstrap 在运行时用代码搭出来，所以一进入 Play
    /// 这一层必须立刻销毁，否则会和运行时生成的背景、战机叠成两套。
    /// </summary>
    public class ScenePreview : MonoBehaviour
    {
        private void Awake()
        {
            // 先关掉再销毁：Destroy 要到这一帧末尾才真正生效，
            // 不先 SetActive(false) 的话 Play 的第一帧会和运行时画面叠在一起闪一下。
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
