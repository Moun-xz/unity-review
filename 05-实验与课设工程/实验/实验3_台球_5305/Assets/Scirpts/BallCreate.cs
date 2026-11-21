using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallCreate : MonoBehaviour
{
    public Transform m_ballPrefab;//预制小球，拖拽小球预制到变量中
    //材质名称
    private string[] m_mid = new string[] { "ball1", "ball2", "ball3", "ball4", "ball5", "ball6" };
    private Vector3[] m_pos = new Vector3[] {
        new Vector3(0,    0.5f, 5),  new Vector3(0.5f, 0.5f, 6),
        new Vector3(-0.5f,0.5f, 6), new Vector3(1,    0.5f, 7),
        new Vector3(0,    0.5f, 7),  new Vector3(-1,   0.5f, 7)};
    void Start()
    {
        //产生一组小球
        int i;
        for (i = 0; i < 6; i++)
        {
            Transform go = Instantiate(m_ballPrefab);
            go.transform.parent = this.gameObject.transform;
            //go.position = transform.position;
            //设置位置
            go.position = m_pos[i];
            go.rotation = transform.rotation;
        }
        //获取小球组
        GameObject[] balls = GameObject.FindGameObjectsWithTag("TargetBall");
        Material[] mats = new Material[balls.Length];
        for (i = 0; i < balls.Length; i++)
        {
            //材质获取
            mats[i] = Resources.Load("Materials/" + m_mid[i]) as Material;
            //设置小球ID和材质
            Ball ball = balls[i].GetComponent<Ball>();
            ball.m_BallID = i + 1;
            ball.GetComponent<MeshRenderer>().material = mats[i];
        }
    }
}
