using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player : MonoBehaviour
{
    public float m_Force = 20;
    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");
        this.gameObject.GetComponent<Rigidbody>().AddForce(new Vector3(x, 0, y) * m_Force);
    }
    void OnCollisionEnter(Collision other)
    {
        if (other.collider.tag.Equals("Hole"))
        {
            Rigidbody rg = gameObject.GetComponent<Rigidbody>();
            rg.Sleep();
            gameObject.transform.position = new Vector3(0, 0.5f, 0);
        }
    }
}
