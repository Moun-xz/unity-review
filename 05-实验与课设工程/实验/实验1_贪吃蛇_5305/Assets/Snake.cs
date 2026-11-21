using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class Snake : MonoBehaviour
{
    Vector3 dir = Vector3.right;
    List<Transform> tail = new List<Transform>();
    bool ate = false;
    public GameObject tailPrefab; // Tail Prefab
    public bool work = false;
    void Start()
    {
        InvokeRepeating("Move", 1f, 0.3f);
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag.Equals("food"))
        {
            ate = true;
            Destroy(other.gameObject);
        }
        else
        {
            work = false;
            Game game = GameObject.Find("Game").GetComponent<Game>() as Game;
            game.StopGame();
        }
    }
    void Update()
    {
        if (Input.GetKey(KeyCode.RightArrow)) dir = Vector3.right;
        else if (Input.GetKey(KeyCode.DownArrow)) dir = Vector3.down;
        else if (Input.GetKey(KeyCode.LeftArrow)) dir = Vector3.left;
        else if (Input.GetKey(KeyCode.UpArrow)) dir = Vector3.up;
    }
    void Move()
    {
        if (work)
        {
            Vector3 v = transform.position;
            transform.Translate(dir);
            if (ate)
            {
                GameObject g = (GameObject)Instantiate(tailPrefab, v,
                                            Quaternion.identity);
                tail.Insert(0, g.transform);
                ate = false;
            }
            else if (tail.Count > 0)
            {
                tail.Last().position = v;
                tail.Insert(0, tail.Last());
                tail.RemoveAt(tail.Count - 1);
            }
        }
    }
}
