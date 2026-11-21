using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class CarMove : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float turnSpeed = 90f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.drag = 0f;
        rb.angularDrag = 0.5f;
        // 只让 Y 轴能转，撞墙不会被掀翻
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        // 关键：高速下用连续检测，否则 10 米/秒也能一帧穿过薄墙
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void FixedUpdate()   // 物理相关一律 FixedUpdate
    {
        float move = Input.GetAxis("Vertical");
        float turn = Input.GetAxis("Horizontal");

        // 给刚体设速度，而不是硬写 transform 位置
        Vector3 v = transform.forward * move * moveSpeed;
        v.y = rb.velocity.y;          // Y 轴留给重力
        rb.velocity = v;

        rb.angularVelocity = Vector3.up * turn * turnSpeed * Mathf.Deg2Rad;
    }
}


