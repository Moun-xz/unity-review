using UnityEngine;

/// <summary>
/// 打击反馈管理器：负责播放命中音效 + 挥拳破空声 + 命中火花粒子。
///
/// 使用方式（静态调用，无需手动在场景里挂载）：
///     HitFeedback.PlayHit(point);   // 命中：音效 + 火花
///     HitFeedback.PlayWhoosh();     // 出拳：破空声
///
/// 第一次被调用时会自动创建一个名为 "HitFeedback" 的常驻对象。
/// </summary>
public class HitFeedback : MonoBehaviour
{
    private static HitFeedback _instance;

    private AudioSource audioSource;
    private AudioClip impactClip;
    private AudioClip whooshClip;
    private Material sparkMaterial;

    // 外部特效预制体：把自己的特效放到 Assets/Resources/hit_sparks.prefab 就会自动启用
    private GameObject hitEffectPrefab;
    private bool hitEffectChecked;

    private static HitFeedback Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("HitFeedback");
                _instance = go.AddComponent<HitFeedback>();
            }
            return _instance;
        }
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

        // 从 Resources 目录加载音频（文件位于 Assets/Resources/*.wav）
        impactClip = Resources.Load<AudioClip>("hit_impact");
        whooshClip = Resources.Load<AudioClip>("hit_whoosh");

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;   // 2D 音效，保证任何时候都听得见
        audioSource.volume = 1f;

        // 火花材质：内置渲染管线优先用 Particles/Standard Unlit
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        sparkMaterial = new Material(shader);
        sparkMaterial.name = "HitSparkMaterial";

        if (impactClip == null)
            Debug.LogWarning("HitFeedback：未找到 Resources/hit_impact，命中音效将静音");
        if (whooshClip == null)
            Debug.LogWarning("HitFeedback：未找到 Resources/hit_whoosh，出拳音效将静音");
    }

    /// <summary>在指定世界坐标播放命中反馈：打击音 + 火花四溅。</summary>
    public static void PlayHit(Vector3 point)
    {
        Instance.DoPlayHit(point);
    }

    /// <summary>播放挥拳破空声（无火花）。</summary>
    public static void PlayWhoosh()
    {
        Instance.DoPlayWhoosh();
    }

    private void DoPlayHit(Vector3 point)
    {
        if (impactClip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.12f);  // 轻微变调，避免听感重复
            audioSource.PlayOneShot(impactClip, 1f);
        }
        SpawnSparks(point);
    }

    private void DoPlayWhoosh()
    {
        if (whooshClip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.08f);
            audioSource.PlayOneShot(whooshClip, 0.55f);
        }
    }

    /// <summary>在命中点生成一次性火花爆发粒子。</summary>
    private void SpawnSparks(Vector3 point)
    {
        // ===== 优先使用自己放入 Resources 的特效预制体 =====
        if (!hitEffectChecked)
        {
            hitEffectChecked = true;
            hitEffectPrefab = Resources.Load<GameObject>("hit_sparks");
            if (hitEffectPrefab != null)
                Debug.Log("HitFeedback：命中特效已切换为外部预制体 Resources/hit_sparks");
        }
        if (hitEffectPrefab != null)
        {
            GameObject fx = Instantiate(hitEffectPrefab, point, Quaternion.identity);
            // 有些预制体的粒子是 playOnAwake=false，这里强制播一遍
            foreach (ParticleSystem child in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                child.Clear();
                child.Play();
            }
            Destroy(fx, 3f);
            return;
        }

        // ===== 兜底：程序化生成火花（没放外部预制体时用这个）=====
        GameObject go = new GameObject("HitSparks");
        // 关键：先失活。ParticleSystem 被激活后会立刻开始播放，
        // 此时再设置 duration 会被 Unity 拒绝并刷警告。
        go.SetActive(false);
        go.transform.position = point;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // ---- 主模块 ----
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.45f),
            new Color(1f, 0.45f, 0.05f));
        main.gravityModifier = 0.7f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        // ---- 发射模块：一次性爆发 ----
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 22)
        });

        // ---- 形状模块：从命中点向外喷 ----
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;

        // ---- 生命周期内颜色：亮黄 -> 橙红 -> 暗红 ----
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.75f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.05f), 0.5f),
                new GradientColorKey(new Color(0.45f, 0.05f, 0.05f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // ---- 尺寸随时间收缩 ----
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        // ---- 渲染模块 ----
        ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.material = sparkMaterial;
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        // 配置全部完成后再激活并播放
        go.SetActive(true);
        ps.Play();

        // 兜底销毁（正常由 stopAction 负责）
        Destroy(go, 2f);
    }
}
