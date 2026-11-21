using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{
    public FoodSpawner fs;
    public Snake snake;
    public GameObject textLose;
    public bool status = false;

    // 「You Lose」的显示位置：棋盘正上方那条空白带。
    // 棋盘顶墙在 y≈9.8，相机上边缘在 y≈11.7，中间这条横带是空的，正好当横幅。
    // 在 Inspector 里改这个值也能实时生效，不用动代码。
    public Vector3 loseTextPos = new Vector3(0f, 10.7f, -1f);

    void Start()
    {
        // TextMesh 默认是「左上角锚点」，坐标指的是文字左上角而不是中心，很难对。
        // 改成居中对齐后，坐标就等于文字中心，摆位上下一目了然。
        TextMesh tm = textLose.GetComponent<TextMesh>();
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;

        // 位置统一在这里定，开局先把它藏起来。
        textLose.transform.position = loseTextPos;
        textLose.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            status = true;
            textLose.SetActive(false);
            fs.gameObject.SetActive(true);
            fs.Continuous = true;
            snake.work = true;
            snake.enabled = true;
            snake.gameObject.SetActive(true);
        }
    }

    public void StopGame()
    {
        status = false;
        textLose.SetActive(true);
        fs.Continuous = false;
        fs.gameObject.SetActive(false);
        snake.work = false;
        snake.gameObject.SetActive(false);
    }
}
