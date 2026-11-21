using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller1 : MonoBehaviour
{
    public float FloWalkingSpeed = 0.02F;   //走路速度
    public float FloRatationSpeed = 0.2F;   //旋转速度
    private Animator AnimaObj;               //动画
    void Start() { AnimaObj = this.GetComponent<Animator>(); }
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                AnimaObj.SetBool("IsRun", true);
                this.transform.Translate(Vector3.forward * FloWalkingSpeed);
            }
            else
            {
                AnimaObj.SetBool("IsRun", false);
                AnimaObj.SetFloat("Speed", 1);
            }
            this.transform.Translate(Vector3.forward * FloWalkingSpeed);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            this.transform.Translate(Vector3.back * FloWalkingSpeed);
        }
        else
        {
            AnimaObj.SetFloat("Speed", 0);
            AnimaObj.SetBool("IsRun", false);
        }
        //转弯
        if (Input.GetKey(KeyCode.A))
        {
            this.transform.Rotate(Vector3.down * FloRatationSpeed);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            this.transform.Rotate(Vector3.up * FloRatationSpeed);
        }
    }
}
