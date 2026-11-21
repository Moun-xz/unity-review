using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PixelGravity;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 截图流程的执行体。跑在编辑器进程里（EditorApplication.update 驱动），不是 MonoBehaviour——
/// 因为 Unity 拒绝把 Assets/Editor 下的 MonoBehaviour 挂到 GameObject 上
/// （"Can't add script behaviour ... because it is an editor script"）。
///
/// 自己实现了一个极小的协程引擎：IEnumerator 支持 yield return null / yield return WaitSec / 嵌套 IEnumerator，
/// 这样流程代码还能写成顺序的 yield，等待用 realtime 计时（暂停时 Time.timeScale=0 也不影响）。
///
/// 画面来源：一个复制主相机的离屏相机 + 1920×1080 RenderTexture，并把 UI Canvas 临时切成
/// ScreenSpaceCamera 指向它。这样每一帧引擎都会把「世界 + UI」正常渲进 RT，
/// 编辑器是不是在最前台、Game 窗口是什么分辨率都不影响结果；截图 = 读回 RT 存 PNG。
/// </summary>
public static class UIShotFlow
{
    private const float HardTimeout = 150f;

    /// 最小验证模式：只截 title 一张就退出 Play，用来证明「能稳定进出 Play + 截图非黑」
    private const bool QuickOnly = false;

    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static float waitUntil;
    private static float startedAt;
    private static bool hooked;

    private static Canvas canvas;
    private static RenderTexture rt;
    private static GameObject camGo;
    private static Camera shotCam;
    private static RenderMode savedMode;
    private static Camera savedWorldCam;
    private static float savedPlane;

    private class WaitSec
    {
        public readonly float Seconds;
        public WaitSec(float s) { Seconds = s; }
    }

    // ------------------------------------------------------------ 生命周期

    public static bool IsRunning
    {
        get { return hooked; }
    }

    public static void Begin()
    {
        if (hooked)
        {
            UIShotCapture.Log("flow already running, ignore Begin");
            return;
        }
        hooked = true;
        startedAt = Time.realtimeSinceStartup;
        stack.Clear();
        stack.Push(Run());
        waitUntil = 0f;
        EditorApplication.update += Tick;
        UIShotCapture.Log("flow begin");
    }

    public static void Tick()
    {
        if (stack.Count == 0)
        {
            Detach("ended");
            return;
        }

        if (EditorApplication.isPaused) EditorApplication.isPaused = false;

        if (Time.realtimeSinceStartup - startedAt > HardTimeout)
        {
            UIShotCapture.Log("flow HARD TIMEOUT (" + HardTimeout + "s)");
            TearDownOffscreen();
            Detach("timeout");
            UIShotCapture.MarkFinishedAndExit("timeout");
            return;
        }

        if (Time.realtimeSinceStartup < waitUntil) return;
        waitUntil = 0f;

        try
        {
            IEnumerator cur = stack.Peek();
            if (!cur.MoveNext())
            {
                stack.Pop();
                return;
            }
            object y = cur.Current;
            if (y is IEnumerator)
            {
                stack.Push((IEnumerator)y);
                return;
            }
            if (y is WaitSec)
            {
                waitUntil = Time.realtimeSinceStartup + ((WaitSec)y).Seconds;
            }
            // y == null：下一 tick 继续
        }
        catch (Exception e)
        {
            UIShotCapture.Log("flow EXCEPTION: " + e);
            TearDownOffscreen();
            Detach("exception");
            UIShotCapture.MarkFinishedAndExit("exception");
        }
    }

    private static void Detach(string why)
    {
        if (!hooked) return;
        hooked = false;
        EditorApplication.update -= Tick;
        UIShotCapture.Log("flow detached: " + why);
    }

    private static WaitSec Wait(float s)
    {
        return new WaitSec(s);
    }

    // ------------------------------------------------------------ 主流程

    private static IEnumerator Run()
    {
        UIShotCapture.Log("run start: frame=" + Time.frameCount + " screen=" + Screen.width + "x" + Screen.height);
        Application.runInBackground = true;
        Time.timeScale = 1f;

        // 1) 等 GameBootstrap.Awake 把 UI 全部建出来（TitleScreen 是 CreateUI 里最后一个建的）
        float waited = 0f;
        while (waited < 30f)
        {
            canvas = FindCanvas();
            if (canvas != null && GameObject.Find("TitleScreen") != null && GameObject.Find("MenuPanel") != null) break;
            yield return Wait(0.1f);
            waited += 0.1f;
        }
        if (canvas == null)
        {
            UIShotCapture.Log("FATAL: UI canvas not found");
            Finish("no canvas");
            yield break;
        }
        UIShotCapture.Log("canvas ready after ~" + waited.ToString("F1") + "s, children=" + canvas.transform.childCount);
        {
            string names = "";
            for (int ci = 0; ci < canvas.transform.childCount; ci++)
            {
                Transform ch = canvas.transform.GetChild(ci);
                names += ch.name + (ch.gameObject.activeSelf ? "" : "(inactive)") + " ";
            }
            UIShotCapture.Log("canvas children: " + names);
        }

        yield return Wait(0.3f);
        if (!SetUpOffscreen())
        {
            UIShotCapture.Log("FATAL: offscreen capture setup failed");
            Finish("offscreen setup failed");
            yield break;
        }
        yield return Wait(0.4f);

        // 2) 等开机黑幕散尽 + 标题屏出场（TitleScreen.IntroDuration = 0.92）
        float t = 0f;
        while (GameObject.Find("BootSplash") != null && t < 12f)
        {
            yield return Wait(0.08f);
            t += 0.08f;
        }
        UIShotCapture.Log("boot splash gone after ~" + t.ToString("F1") + "s");
        yield return Wait(2.60f);
        yield return Shot("01_title.png");

        if (QuickOnly)
        {
            UIShotCapture.Log("QUICK MODE: single title shot done, exiting play mode");
            Finish("quick ok");
            yield break;
        }

        // 3) 主菜单
        TitleScreen title = FindComp<TitleScreen>("TitleScreen");
        MenuPanel menu = FindComp<MenuPanel>("MenuPanel");
        if (title != null) title.Hide();
        if (menu != null)
        {
            menu.SetVisible(true);
            menu.ResumeIntro();
        }
        yield return Wait(3.20f);
        yield return Shot("02_menu.png");
        yield return Wait(1.20f);
        yield return Shot("02b_menu_late.png");

        // 4) 五个子面板（机库已随"唯一战机"改造移除）
        LevelSelectPanel levelSelect = FindComp<LevelSelectPanel>("LevelSelectPanel");
        EnemyCodexPanel codex = FindComp<EnemyCodexPanel>("EnemyCodexPanel");
        SettingsPanel settings = FindComp<SettingsPanel>("SettingsPanel");
        HelpPanel help = FindComp<HelpPanel>("HelpPanel");

        yield return SubShot(menu, levelSelect, "03_levelselect.png");
        yield return SubShot(menu, codex, "05_codex.png");
        yield return SubShot(menu, settings, "06_settings.png");
        yield return SubShot(menu, help, "07_help.png");

        if (menu != null) menu.SetVisible(false);
        yield return Wait(0.35f);

        // 5) 战斗 HUD
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            UIShotCapture.Log("FATAL: GameManager.Instance is null");
            Finish("no GameManager");
            yield break;
        }

        gm.StartGameAt(0);
        UIShotCapture.Log("StartGameAt(0) -> state=" + gm.State);
        yield return Wait(2.60f);

        // 编辑器失焦会自动暂停（GameManager.OnApplicationFocus），截 HUD 前按回 Playing
        if (gm.State != GameState.Playing) gm.ChangeState(GameState.Playing);
        if (gm.PauseMenu != null) gm.PauseMenu.SetVisible(false);
        Time.timeScale = 1f;
        yield return Wait(0.15f);

        for (int i = 0; i < 6; i++) gm.RegisterKill(150);   // 让 HUD 上有分数/连击
        yield return Wait(0.40f);
        yield return Shot("08_hud.png");

        // 6) 暂停
        gm.ChangeState(GameState.Paused);
        yield return Wait(0.90f);
        yield return Shot("09_pause.png");

        // 7) 结算（通关）
        int savedHighScore = PlayerPrefs.GetInt("dss_highscore", 0);
        gm.ChangeState(GameState.Playing);
        yield return Wait(0.25f);
        gm.Victory();
        yield return Wait(1.70f);
        yield return Shot("10_result.png");

        PlayerPrefs.SetInt("dss_highscore", savedHighScore);
        PlayerPrefs.Save();
        UIShotCapture.Log("restored dss_highscore=" + savedHighScore);

        Finish("done");
    }

    private static void Finish(string reason)
    {
        UIShotCapture.Log("flow finished: " + reason);
        Time.timeScale = 1f;
        TearDownOffscreen();
        Detach(reason);
        UIShotCapture.MarkFinishedAndExit(reason);
    }

    // ------------------------------------------------------------ 子面板

    private static IEnumerator SubShot(MenuPanel menu, UIPanelBase panel, string file)
    {
        if (panel == null)
        {
            UIShotCapture.Log("SKIP " + file + " (panel not found)");
            yield break;
        }

        // 真实交互里都是「先收主菜单、再开子面板」
        if (menu != null) menu.SetVisible(false);
        yield return Wait(0.12f);

        MethodInfo open = panel.GetType().GetMethod("Open", BindingFlags.Public | BindingFlags.Instance);
        if (open != null && open.GetParameters().Length == 0)
        {
            open.Invoke(panel, null);
        }
        else
        {
            UIShotCapture.Log("WARN no usable public Open() on " + panel.GetType().Name);
        }

        yield return Wait(1.40f);       // 淡入 9/s + 24px 上滑
        yield return Shot(file);

        panel.SetVisible(false);
        yield return Wait(0.20f);
    }

    // ------------------------------------------------------------ 离屏相机

    private static bool SetUpOffscreen()
    {
        Camera main = Camera.main;
        if (main == null)
        {
            UIShotCapture.Log("no Camera.main");
            return false;
        }

        rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        rt.name = "__UIShotRT";
        rt.antiAliasing = 1;
        rt.Create();

        camGo = new GameObject("__UIShotCam");
        UnityEngine.Object.DontDestroyOnLoad(camGo);
        shotCam = camGo.AddComponent<Camera>();
        shotCam.CopyFrom(main);
        shotCam.transform.position = main.transform.position;
        shotCam.transform.rotation = main.transform.rotation;
        shotCam.targetTexture = rt;
        shotCam.enabled = true;

        savedMode = canvas.renderMode;
        savedWorldCam = canvas.worldCamera;
        savedPlane = canvas.planeDistance;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = shotCam;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();

        UIShotCapture.Log("offscreen ready: cam='" + main.name + "' ortho=" + main.orthographic
            + " size=" + main.orthographicSize + " bg=" + main.backgroundColor
            + " canvasMode=" + savedMode + "->" + canvas.renderMode);
        return true;
    }

    private static void TearDownOffscreen()
    {
        try
        {
            if (canvas != null && canvasActiveByUs())
            {
                canvas.renderMode = savedMode;
                canvas.worldCamera = savedWorldCam;
                canvas.planeDistance = savedPlane;
                Canvas.ForceUpdateCanvases();
            }
        }
        catch (Exception e) { UIShotCapture.Log("teardown canvas ex: " + e.Message); }

        try
        {
            if (camGo != null) UnityEngine.Object.Destroy(camGo);
            if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
        }
        catch (Exception e) { UIShotCapture.Log("teardown rt ex: " + e.Message); }

        camGo = null;
        shotCam = null;
        rt = null;
    }

    private static bool canvasActiveByUs()
    {
        return canvas != null && shotCam != null && canvas.worldCamera == shotCam;
    }

    // ------------------------------------------------------------ 截图

    private static IEnumerator Shot(string file)
    {
        if (rt == null)
        {
            UIShotCapture.Log("FAIL " + file + " (no render texture)");
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        yield return Wait(0.14f);          // 让引擎把这一帧正常渲进 RT

        Texture2D full = ReadBack(rt);

        // 对照组：关掉 Canvas 再渲一帧，差分占比 = UI 真的画进去了多少
        canvas.enabled = false;
        Canvas.ForceUpdateCanvases();
        yield return Wait(0.14f);
        Texture2D worldOnly = ReadBack(rt);
        canvas.enabled = true;
        Canvas.ForceUpdateCanvases();

        float meanLuma = MeanLuma(full);
        float uiCoverage = DiffFraction(worldOnly, full);

        string path = Path.Combine(UIShotCapture.OutDir, file);
        bool ok = false;
        if (meanLuma < 0.002f)
        {
            UIShotCapture.Log("FAIL " + file + " black frame (meanLuma=" + meanLuma.ToString("F4") + ")");
        }
        else if (uiCoverage < 0.01f)
        {
            UIShotCapture.Log("WARN " + file + " uiCoverage=" + uiCoverage.ToString("F3") + " (UI may be missing)");
        }
        else
        {
            byte[] png = full.EncodeToPNG();
            if (png != null && png.Length > 2000)
            {
                Directory.CreateDirectory(UIShotCapture.OutDir);
                File.WriteAllBytes(path, png);
                ok = true;
            }
        }

        UIShotCapture.Log((ok ? "OK   " : "FAIL ") + file
            + " 1920x1080 meanLuma=" + meanLuma.ToString("F4")
            + " uiCoverage=" + uiCoverage.ToString("F3")
            + (ok ? " " + new FileInfo(path).Length + "B" : ""));

        UnityEngine.Object.Destroy(full);
        UnityEngine.Object.Destroy(worldOnly);
        yield return Wait(0.12f);
    }

    private static Texture2D ReadBack(RenderTexture src)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = src;
        Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0, false);
        tex.Apply();
        RenderTexture.active = prev;
        return tex;
    }

    private static float MeanLuma(Texture2D tex)
    {
        Color32[] px = tex.GetPixels32();
        int step = Mathf.Max(1, px.Length / 50000);
        long sum = 0;
        int n = 0;
        for (int i = 0; i < px.Length; i += step)
        {
            Color32 c = px[i];
            sum += (c.r * 30 + c.g * 59 + c.b * 11) / 100;
            n++;
        }
        return n == 0 ? 0f : sum / (float)n / 255f;
    }

    private static float DiffFraction(Texture2D a, Texture2D b)
    {
        Color32[] pa = a.GetPixels32();
        Color32[] pb = b.GetPixels32();
        int step = Mathf.Max(1, pa.Length / 50000);
        int diff = 0;
        int n = 0;
        int len = Mathf.Min(pa.Length, pb.Length);
        for (int i = 0; i < len; i += step)
        {
            int la = (pa[i].r * 30 + pa[i].g * 59 + pa[i].b * 11) / 100;
            int lb = (pb[i].r * 30 + pb[i].g * 59 + pb[i].b * 11) / 100;
            if (Mathf.Abs(la - lb) > 10) diff++;
            n++;
        }
        return n == 0 ? 0f : diff / (float)n;
    }

    // ------------------------------------------------------------ 工具

    private static Canvas FindCanvas()
    {
        GameObject go = GameObject.Find("UI");
        return go == null ? null : go.GetComponent<Canvas>();
    }

    private static T FindComp<T>(string name) where T : Component
    {
        // 名字查找只找激活对象，且受 Canvas 层级影响；这里退到类型查找（含未激活），更稳。
        T[] found = UnityEngine.Object.FindObjectsOfType<T>(true);
        if (found != null && found.Length > 0) return found[0];
        GameObject go = GameObject.Find(name);
        return go == null ? null : go.GetComponent<T>();
    }
}
