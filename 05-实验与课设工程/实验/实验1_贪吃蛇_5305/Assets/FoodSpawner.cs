using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    public GameObject foodPrefab;
    //Borders
    public Transform borderTop;
    public Transform borderBottom;
    public Transform borderLeft;
    public Transform borderRight;
    public bool Continuous = false;
    void Start()
    {
        InvokeRepeating("Spawn", 1, 4); // 每4秒钟生成一个食物
    }
    // Spawn one food
    void Spawn()
    {
        if (Continuous)
        {
            int x = (int)Random.Range(borderLeft.position.x + 0.5f,
                                            borderRight.position.x - 0.5f);
            int y = (int)Random.Range(borderBottom.position.y + 0.5f,
                                            borderTop.position.y - 0.5f);
            // Instantiate the food at (x, y)
            Instantiate(foodPrefab, new Vector3(x, y, -0.5f),
                                            Quaternion.identity);
        }
    }
}
