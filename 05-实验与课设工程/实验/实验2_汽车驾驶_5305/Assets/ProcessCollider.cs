using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcessCollider : MonoBehaviour
{
    void OnCollisionEnter(Collision other)
    {
        // 不再按名字找、也不再直接改字段
        CarLife car = other.gameObject.GetComponent<CarLife>();
        if (car != null) car.TakeDamage(3);
    }
}

