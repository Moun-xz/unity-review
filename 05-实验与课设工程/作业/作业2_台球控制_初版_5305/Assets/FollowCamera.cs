using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 4, -6);
    [Range(0.01f, 0.3f)] public float smoothSpeed = 0.1f;

    void LateUpdate() // 必须LateUpdate！！
    {
        Vector3 desiredPos = target.position + offset;
        Vector3 smoothPos = Vector3.Lerp(transform.position, desiredPos, smoothSpeed);
        transform.position = smoothPos;
        transform.LookAt(target);
    }
}
