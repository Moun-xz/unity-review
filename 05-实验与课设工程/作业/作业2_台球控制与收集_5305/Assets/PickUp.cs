using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUp : MonoBehaviour
{
    public int scoreValue = 1;  //吃一个方块得几分
    private bool isPicked = false;

    void OnCollisionEnter(Collision collision)
    {
        //已经吃过就不再重复加分数
        if (isPicked) return;

        //只有球（Tag 设成 Player）撞过来才算拾取
        if (!collision.gameObject.CompareTag("Player")) return;

        isPicked = true;

        if (GameManager.instance != null)
        {
            GameManager.instance.AddScore(scoreValue);
        }

        Destroy(gameObject);
    }
}
