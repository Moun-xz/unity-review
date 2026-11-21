using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : MonoBehaviour
{
    public int m_BallID = 0;
    public AudioSource m_audiosource;
    public AudioClip m_audioclip;
    void Start()
    {
        m_audiosource = GetComponent<AudioSource>();
    }
    void OnCollisionEnter(Collision other)
    {
        if (other.collider.tag.Equals("Hole"))
        {
            Destroy(this.gameObject);
        }
        if (other.collider.tag.Equals("TargetBall")
            || other.collider.tag.Equals("Player"))
        {
            m_audioclip = Resources.Load("Sound/hit") as AudioClip;
            if (m_audiosource)
                m_audiosource.PlayOneShot(m_audioclip);
        }
    }
}
