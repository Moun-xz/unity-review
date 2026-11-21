using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hole : MonoBehaviour
{
    void OnCollisionEnter(Collision other)
    {
        if (other.collider.tag.Equals("TargetBall") || other.collider.tag.Equals("Player"))
            GetComponent<AudioSource>().Play();
    }
}
