using UnityEngine;

public class ChaserController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    
    [Header("Detection")]
    [SerializeField] private float catchRadius = 0.5f;

    private Rigidbody2D rb;
    private Transform targetTransform;
    private bool isMoving = false;
    private Vector3 targetPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        targetPosition = transform.position;
        
        GameObject target = GameObject.Find("Target");
        if (target != null)
        {
            targetTransform = target.transform;
        }
    }

    void Update()
    {
        if (!isMoving)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                TryMove(Vector3.up);
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                TryMove(Vector3.down);
            }
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                TryMove(Vector3.left);
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                TryMove(Vector3.right);
            }
        }

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }

        CheckCatch();
    }

    void TryMove(Vector3 direction)
    {
        Vector3 newPosition = transform.position + direction;
        
        Collider2D hit = Physics2D.OverlapPoint(newPosition, LayerMask.GetMask("Default"));
        
        if (hit == null || !hit.CompareTag("Wall"))
        {
            targetPosition = newPosition;
            isMoving = true;
        }
    }

    void CheckCatch()
    {
        if (targetTransform == null) return;

        float distance = Vector2.Distance(transform.position, targetTransform.position);
        
        if (distance <= catchRadius)
        {
            Time.timeScale = 0;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, catchRadius);
    }
}
