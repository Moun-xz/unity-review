using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarLife : MonoBehaviour
{
    public int life = 10;

    public void TakeDamage(int amount)
    {
        life -= amount;
        Debug.Log("车剩余生命：" + life);

        if (life <= 0)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cam.transform.SetParent(null);   // 方案二：先摘相机
            Destroy(gameObject);                              // 再销毁车
        }
    }
}
