using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallShoot : MonoBehaviour
{
    public Rigidbody rb;
    public float moveForce = 5f; //移动推力，按需调大小
    public float drag = 0.8f;    //阻力，模拟台球减速停下

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.drag = drag; //设置阻力，球会慢慢减速，更像台球
    }

    void Update()
    {
        // 获取水平、垂直输入（W/A/S/D 和 方向箭头都能触发）
        float h = Input.GetAxis("Horizontal");   // A/D / ← →
        float v = Input.GetAxis("Vertical");     // W/S / ↑ ↓

        // 构建水平移动方向，y=0，只在桌面平面移动
        Vector3 moveDir = new Vector3(h, 0, v).normalized;

        // 给刚体施加力
        rb.AddForce(moveDir * moveForce, ForceMode.Acceleration);
    }
}
