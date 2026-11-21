using UnityEngine;

public class Blendtree : MonoBehaviour
{
    [Header("移动")]
    public float walkSpeed = 1.2f;     // 走路速度（米/秒）
    public float runSpeed = 3f;        // 跑步速度（米/秒）

    [Header("转向")]
    public float rotateSpeed = 120f;   // 转向速度（度/秒）

    private Animator anim;

    void Start() { anim = GetComponent<Animator>(); }

    void Update()
    {
        // ---- 转向 ----
        // 乘 Time.deltaTime：rotateSpeed 表示"每秒转多少度"，不随帧率变化
        float h = Input.GetAxis("Horizontal");
        transform.Rotate(0f, h * rotateSpeed * Time.deltaTime, 0f);

        // Direction 驱动融合树里"左走 / 走 / 右走"三个片段的混合权重
        anim.SetFloat("Direction", h);

        // ---- 前进 ----
        // Speed 参数的取值必须落在融合树的阈值区间里：
        // 融合树里 walking 在 0、running 在 0.8，所以传 0.5 会变成"62% 跑步动画"。
        // 取 0.2（≈25% 跑）和 0.9（超过 0.8，纯跑）才和课件一致。
        if (Input.GetKey(KeyCode.UpArrow))
        {
            bool running = Input.GetKey(KeyCode.Z);
            anim.SetFloat("Speed", running ? 0.9f : 0.2f);
            transform.Translate(Vector3.forward * (running ? runSpeed : walkSpeed) * Time.deltaTime);
        }
        else
        {
            anim.SetFloat("Speed", 0f);   // Speed < 0.1 → 回 Standing
        }
    }
}
