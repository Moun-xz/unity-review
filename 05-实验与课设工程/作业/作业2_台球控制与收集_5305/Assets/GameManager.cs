using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public Text scoreText;        //右上角显示分数的 Text
    public int totalPickups = 5;  //场景里一共有几个方块
    private int score = 0;
    void Awake()
    {
        instance = this;
    }
    void Start()
    {
        RefreshUI();
    }
    public void AddScore(int value)
    {
        score += value;
        RefreshUI();
    }

    void RefreshUI()
    {
        if (scoreText == null) return;

        if (score >= totalPickups)
        {
            scoreText.text = "CLEAR!";
        }
        else
        {
            scoreText.text = score + " / " + totalPickups;
        }
    }
}
