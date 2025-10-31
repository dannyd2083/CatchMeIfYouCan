using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetController : MonoBehaviour
{

    [Header("Moving setting")]
    [SerializeField] private float moveSpeed = 3f;           
    [SerializeField] private float changeDirectionTime = 80f; 



    private Rigidbody2D rb;
    private Vector2 moveDirection;
    private float timer;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        ChooseRandomDirection();
    }

    // Update is called once per frame
    void Update()
    {
        rb.velocity = moveDirection * moveSpeed;
        timer += Time.fixedDeltaTime;

        if(timer >= changeDirectionTime)
        {
            ChooseRandomDirection();
            timer = 0f;
        }
        
    }



    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall") ||
            collision.gameObject.CompareTag("Obstacle"))
        {
     
            Vector2 normal = collision.contacts[0].normal;
            moveDirection = Vector2.Reflect(moveDirection, normal);

            // ChooseRandomDirection();
        }
    }

    void ChooseRandomDirection()
    {
        float randomAngle = Random.Range(0f, 360f);
        moveDirection = new Vector2(
            Mathf.Cos(randomAngle * Mathf.Deg2Rad),
            Mathf.Sin(randomAngle * Mathf.Deg2Rad)
        ).normalized;
    }
}
