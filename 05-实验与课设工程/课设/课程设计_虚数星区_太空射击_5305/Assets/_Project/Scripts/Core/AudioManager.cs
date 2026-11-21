using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 音频管理器（全局四单例之一）。
    /// 职责：持有 BGM 与 SFX 两个 AudioSource，统一音量，统一播放接口。
    ///
    /// BGM 是**场景里挂好的**音乐文件（`BGM` 节点上的 AudioSource，AudioClip 直接拖的 ogg），
    /// 不是代码合成的——这样换曲子只要在 Inspector 里换一个 ogg，不用改脚本。
    /// 音效仍然是运行时由 SfxSynth 合成的（那些是几毫秒的短音，没有必要做成文件）。
    ///
    /// 为什么 SFX 用 PlayOneShot：射击和爆炸会同时触发，PlayOneShot 允许叠加，
    /// 而直接给 clip 赋值再 Play 会打断上一个还没播完的音效。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        /// <summary>场景里挂 BGM 音源的节点名，改名要同步这里</summary>
        private const string BgmObjectName = "BGM";

        [Range(0f, 1f)] public float bgmVolume = 0.26f;
        [Range(0f, 1f)] public float sfxVolume = 0.7f;

        private AudioSource bgmSource;
        private AudioSource sfxSource;

        private AudioClip laserClip;
        private AudioClip spreadClip;
        private AudioClip explosionClip;
        private AudioClip hitClip;
        private AudioClip hurtClip;
        private AudioClip uiClip;
        private AudioClip bossWarnClip;

        private const string BgmVolumeKey = "pg_bgm_volume";
        private const string SfxVolumeKey = "pg_sfx_volume";

        private void Awake()
        {
            Instance = this;

            // 先读存档再建 AudioSource：反过来的话source会以脚本上的默认值起播，
            // 玩家上次调好的音量要等到下一次改动才生效。
            bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, bgmVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);

            // BGM 音源优先用场景里挂好的那个；场景里没有（比如有人删了 BGM 节点）
            // 才退化成自建一个空音源，免得后面每个调用点都要判空。
            bgmSource = FindBgmSource();
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.spatialBlend = 0f;
            }
            // 场景里那个音源的音量先在 Inspector 里放着，这里统一按存档值覆盖
            bgmSource.volume = bgmVolume;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.volume = sfxVolume;
            sfxSource.spatialBlend = 0f;

            BuildClips();

            // 进场景就有音乐（主菜单也听得见）。放在设完音量之后，避免先以满音量炸一下。
            PlayBGM();
        }

        /// <summary>取场景里 BGM 节点上的 AudioSource，没有就返回 null</summary>
        private static AudioSource FindBgmSource()
        {
            GameObject bgm = GameObject.Find(BgmObjectName);
            if (bgm == null) return null;
            return bgm.GetComponent<AudioSource>();
        }

        private void BuildClips()
        {
            laserClip = SfxSynth.Laser();
            spreadClip = SfxSynth.Spread();
            explosionClip = SfxSynth.Explosion();
            hitClip = SfxSynth.Hit();
            hurtClip = SfxSynth.Hurt();
            uiClip = SfxSynth.UiClick();
            bossWarnClip = SfxSynth.BossWarn();
        }

        private void Play(AudioClip clip, float volumeScale)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, volumeScale);
        }

        public void PlayLaser() { Play(laserClip, 0.55f); }
        public void PlaySpread() { Play(spreadClip, 0.5f); }
        public void PlayExplosion() { Play(explosionClip, 0.85f); }
        public void PlayHit(float scale = 1f) { Play(hitClip, 0.4f * Mathf.Clamp(scale, 0.2f, 2f)); }
        public void PlayHurt() { Play(hurtClip, 0.9f); }
        public void PlayUi() { Play(uiClip, 0.6f); }
        public void PlayBossWarn() { Play(bossWarnClip, 1f); }

        /// <summary>播放 BGM。clip 已经在 Inspector 里挂好，这里只管让它转起来</summary>
        public void PlayBGM()
        {
            if (bgmSource == null || bgmSource.clip == null) return;
            if (!bgmSource.isPlaying) bgmSource.Play();
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        public void PauseBGM(bool paused)
        {
            if (bgmSource == null) return;
            if (paused) bgmSource.Pause();
            else if (bgmSource.clip != null) bgmSource.UnPause();
        }

        // ------------------------------------------------------------ 音量（设置面板用）

        /// <summary>直接改音源的 volume。音效走 PlayOneShot 的 volumeScale，会和这里相乘，所以两层都生效</summary>
        public void SetBgmVolume(float value)
        {
            bgmVolume = Mathf.Clamp01(value);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
            SaveVolume(BgmVolumeKey, bgmVolume);
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            if (sfxSource != null) sfxSource.volume = sfxVolume;
            SaveVolume(SfxVolumeKey, sfxVolume);
        }

        private static void SaveVolume(string key, float value)
        {
            // 拖动滑条会连续触发，PlayerPrefs.Save 每帧写盘开销不必要，
            // 只 SetFloat 留在内存里，真正落盘交给退出时的 PlayerPrefs 自动保存
            PlayerPrefs.SetFloat(key, value);
        }
    }
}
