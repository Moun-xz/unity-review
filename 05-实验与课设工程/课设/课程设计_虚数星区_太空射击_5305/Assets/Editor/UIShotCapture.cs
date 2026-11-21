using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 截图流水线的"遥控器"：负责三件事——进 Play、把画面交给 UIShotFlow、跑完退出。
///
/// 为什么要这么绕：截图必须在 Play 模式里跑（GameBootstrap.Awake 才会把 UI 建出来），
/// 而进 Play 会触发一次域重载，静态变量全清空。所以运行状态只能放在 SessionState 里，
/// [InitializeOnLoad] 在每次域重载后重新读它，判断"该开始 / 该接着跑 / 已经跑完"。
///
/// 判断"这次域重载是不是进 Play"不能用 isPlaying：
///   进 Play 的那次重载里 EditorApplication.isPlaying 还是 false，只有
///   isPlayingOrWillChangePlaymode 已经是 true。用 isPlaying 会把"正在进"误判成"没在玩"。
///
/// 重跑方式：把 Build 字符串改一下（一次重编译 = 一次域重载 = 一次新跑）。
/// 输出：Temp/uishots/before/*.png，日志：Temp/uishots/capture.log
/// </summary>
[InitializeOnLoad]
public static class UIShotCapture
{
    /// <summary>改成 v8、v9…… 就能在同一台机器上再跑一轮</summary>
    public const string Build = "v54";

    /// <summary>
    /// 交付版本默认关闭：打开工程时不要自动进 Play 截图。
    /// 需要重跑一轮时把这里改成 true、同时把 Build 改个新值即可。
    /// </summary>
    public const bool AutoRun = false;

    private const string KActive = "uishot.active";     // 本轮版本号
    private const string KFinished = "uishot.finished"; // 本轮已结束（防止退出 Play 时又开一轮）
    private const string KStarted = "uishot.started";   // 开始时间，用于 watchdog

    private const float LaunchDelay = 3.0f;             // 进 Play 后等 GameBootstrap 起来
    private const float HardTimeout = 200f;             // 兜底：整轮超过这个时间强制收工

    private static double launchAt;
    private static double nextAttempt;
    private static int attempts;
    private static double nextBeat;
    private static bool requestedPlay;

    public static string OutDir
    {
        get { return Path.Combine(Directory.GetCurrentDirectory(), "Temp/uishots/before"); }
    }

    private static string LogPath
    {
        get { return Path.Combine(Directory.GetCurrentDirectory(), "Temp/uishots/capture.log"); }
    }

    static UIShotCapture()
    {
        // 0) 交付版本：流水线关闭，打开工程不会自己进 Play
        if (!AutoRun) return;

        // 1) 已经在跑（出现异常/卡住后重新编译）：只接上 watchdog
        if (SessionState.GetString(KActive, "") == Build &&
            SessionState.GetString(KFinished, "") != Build)
        {
            Log("reload while active: playing=" + EditorApplication.isPlaying
                + " willChange=" + EditorApplication.isPlayingOrWillChangePlaymode);
            Hook();
            return;
        }

        // 2) 本轮已跑完：什么都不做（否则退出 Play 的重载会再开一轮）
        if (SessionState.GetString(KFinished, "") == Build) return;

        // 3) 正常启动：标记本轮 + 进 Play
        SessionState.SetString(KActive, Build);
        SessionState.SetString(KStarted, EditorApplication.timeSinceStartup.ToString("F1"));
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Temp/uishots"));

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // 已经处在"正在进 Play"的状态：等引擎真正跑起来再交给 flow
            launchAt = EditorApplication.timeSinceStartup + LaunchDelay;
            Log("enter playmode detected, launching flow in " + LaunchDelay.ToString("F1") + "s");
            Hook();
        }
        else
        {
            Log("begin run " + Build + " -> EnterPlaymode");
            requestedPlay = true;
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
            Hook();
        }
    }

    // ------------------------------------------------------------ 驱动

    private static void Hook()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (SessionState.GetString(KFinished, "") == Build)
        {
            EditorApplication.update -= Tick;
            return;
        }

        double started = 0;
        double.TryParse(SessionState.GetString(KStarted, "0"), out started);
        if (started > 0 && EditorApplication.timeSinceStartup - started > HardTimeout)
        {
            Log("HARD TIMEOUT, aborting play mode");
            MarkFinishedAndExit("timeout");
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            // EnterPlaymode 会被"正在编译""有弹窗"静默吞掉，所以这里重试 + 兜底走菜单项。
            // 电源开关一样的操作重复几次不会进两个 Play 模式：进了 Play 后这段就再也不会走到。
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorApplication.timeSinceStartup >= nextAttempt && attempts < 12)
            {
                attempts++;
                nextAttempt = EditorApplication.timeSinceStartup + 2.0;
                Log("request play mode #" + attempts + (requestedPlay && attempts > 1 ? " (retry)" : ""));

                if (attempts % 2 == 1)
                {
                    EditorApplication.EnterPlaymode();
                }
                else
                {
                    // 兜底：等价于点工具栏的 Play 按钮
                    EditorApplication.ExecuteMenuItem("Edit/Play");
                }
            }

            if (EditorApplication.timeSinceStartup >= nextBeat)
            {
                nextBeat = EditorApplication.timeSinceStartup + 5.0;
                Log("waiting: playing=" + EditorApplication.isPlaying
                    + " willChange=" + EditorApplication.isPlayingOrWillChangePlaymode
                    + " compiling=" + EditorApplication.isCompiling
                    + " updating=" + EditorApplication.isUpdating);
            }

            return;
        }
        if (UIShotFlow.IsRunning) return;
        if (EditorApplication.timeSinceStartup < launchAt) return;

        // 进 Play 后静态构造可能比 GameBootstrap 早，flow 内部自己会等 canvas，
        // 所以这里只保证"真的进 Play 了"就交棒
        Log("handing over to UIShotFlow");
        UIShotFlow.Begin();
    }

    // ------------------------------------------------------------ 收尾

    public static void MarkFinishedAndExit(string reason)
    {
        Log("RUN FINISHED (" + reason + "), exiting play mode");
        SessionState.SetString(KFinished, Build);
        EditorApplication.update -= Tick;

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += () => EditorApplication.ExitPlaymode();
        }
    }

    // ------------------------------------------------------------ 日志

    public static void Log(string message)
    {
        string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + message;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // 日志写不进去不能影响截图本身
        }

        Debug.Log("[UIShot] " + message);
    }
}
