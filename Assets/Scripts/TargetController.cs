using UnityEngine;

public class TargetController : MonoBehaviour
{
    [Header("Moving setting")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float changeDirectionTime = 80f;

    private Rigidbody2D rb;
    private Vector2 moveDirection;
    private float timer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        ChooseRandomDirection();
    }

    void FixedUpdate()
    {
        rb.velocity = moveDirection * moveSpeed;
        timer += Time.fixedDeltaTime;

        if (timer >= changeDirectionTime)
        {
            ChooseRandomDirection();
            timer = 0f;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            ChooseRandomDirection();
            timer = 0f;
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
