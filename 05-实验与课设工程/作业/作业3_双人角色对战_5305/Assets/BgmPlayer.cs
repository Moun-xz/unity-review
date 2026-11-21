using System.Collections;
using UnityEngine;

/// <summary>
/// 背景音乐播放器
///
/// </summary>
public class BgmPlayer : MonoBehaviour
{
    /// <summary>背景音乐音量（0~1）。超过 0.5 容易盖过打击音效。</summary>
    public static float BgmVolume = 0.3f;

    /// <summary>开头淡入时长（秒）。设为 0 则立即出声。</summary>
    public static float FadeInSeconds = 1.5f;

    private static BgmPlayer _instance;

    // 进入 Play 时自动创建，无需场景里存在这个物体
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("BgmPlayer");
        go.AddComponent<BgmPlayer>();
    }

    private void Awake()
    {
        // 单例保护
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        AudioClip clip = Resources.Load<AudioClip>("bgm");
        if (clip == null)
        {
            Debug.LogWarning("BgmPlayer：没找到 Resources/bgm，背景音乐不会播放");
            return;
        }

        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;                            // 循环播放
        src.playOnAwake = false;
        src.spatialBlend = 0f;                      // 2D：任何位置都听得见
        src.volume = FadeInSeconds > 0f ? 0f : BgmVolume;
        src.Play();

        if (FadeInSeconds > 0f)
            StartCoroutine(FadeIn(src));

        Debug.Log("BgmPlayer：背景音乐开始播放 -> " + clip.name
                  + "（时长 " + clip.length.ToString("F1") + " 秒，循环）");
    }

    
    private IEnumerator FadeIn(AudioSource src)
    {
        float t = 0f;
        while (t < FadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(0f, BgmVolume, t / FadeInSeconds);
            yield return null;
        }
        src.volume = BgmVolume;
    }
}
