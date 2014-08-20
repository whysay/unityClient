using UnityEngine;
using System.Collections;

public class move : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
    private Vector2 speed = new Vector2(3, 0);
	// Update is called once per frame
	void Update () {
        if (5.0f < rigidbody2D.position.x)
        {
            speed.x = Random.Range(-3.0f, -1.0f);
            speed.y = Random.Range(-3.0f, 3.0f);
        }
        if (rigidbody2D.position.x < -5.0f)
        {
            speed.x = Random.Range(1.0f, 3.0f);
            speed.y = Random.Range(-3.0f, 3.0f);
        }
            

        if (5.0f < rigidbody2D.position.y)
        {
            speed.y = Random.Range(-3.0f, -1.0f);
            speed.x = Random.Range(-3.0f, 3.0f);
        }
        if (rigidbody2D.position.y < -5.0f)
        {
            speed.y = Random.Range(1.0f, 3.0f);
            speed.x = Random.Range(-3.0f, 3.0f);
        }
            
        rigidbody2D.MovePosition(rigidbody2D.position + speed * Time.deltaTime);
	}
}
