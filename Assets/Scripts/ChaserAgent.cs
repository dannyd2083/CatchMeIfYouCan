using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class ChaserAgent : Agent
{
    // ==================== Component References ====================
    [Header("Component References")]
    [SerializeField] private Transform targetTransform;  // Reference to Target
    private Rigidbody2D rb;                              // Rigidbody component

    // ==================== Movement Parameters ====================
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;       // Movement speed

    // ==================== Training Parameters ====================
    [Header("Training Settings")]
    [SerializeField] private float maxStepsPerEpisode = 5000f;  // Max steps per episode

    // ==================== Environment Info ====================
    [Header("Environment Info")]
    [SerializeField] private float arenaSize = 21f;

    // ==================== Lifecycle Methods ====================

    /// Initialize - Called once when the game starts

    public override void Initialize()
    {
        // Get components
        rb = GetComponent<Rigidbody2D>();

        // Auto-find Target if not manually assigned
        if (targetTransform == null)
        {
            GameObject targetObj = GameObject.Find("Target");
            if (targetObj != null)
            {
                targetTransform = targetObj.transform;
            }
            else
            {
                Debug.LogError("Target object not found! Make sure there's an object named 'Target' in the scene");
            }
        }

        Debug.Log("ChaserAgent initialized!");
    }


    /// Episode Begin - Reset environment at the start of each episode

    public override void OnEpisodeBegin()
    {
        Debug.Log("--- New Episode Started ---");

        // Reset Chaser position and velocity
        transform.position = new Vector3(1f, 1f, 0f);
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // Reset Target position (simple random)
        //if (targetTransform != null)
        //{
        //    float randomX = Random.Range(-3f, 3f);
        //    float randomY = Random.Range(-3f, 3f);
        //    targetTransform.localPosition = new Vector3(randomX, randomY, 0f);
        //}
    }


    /// Collect Observations - The agent's "eyes"

    public override void CollectObservations(VectorSensor sensor)
    {
        // Observation 1-2: Relative position (Target relative to Chaser)
        Vector2 relativePosition = targetTransform.localPosition - transform.localPosition;
        sensor.AddObservation(relativePosition.x / arenaSize);
        sensor.AddObservation(relativePosition.y / arenaSize);

        // Observation 3-4: Chaser's velocity
        sensor.AddObservation(rb.velocity.x / moveSpeed);
        sensor.AddObservation(rb.velocity.y / moveSpeed);

        Debug.Log($"Observations: RelPos=({relativePosition.x:F2}, {relativePosition.y:F2}), Vel=({rb.velocity.x:F2}, {rb.velocity.y:F2})");
    }

    
    /// Execute Actions
    public override void OnActionReceived(ActionBuffers actions)
    {
        Debug.Log($"OnActionReceived called");
        // Get AI's action decision (continuous actions: X and Y direction)
        float moveX = actions.ContinuousActions[0];  // -1 to 1
        float moveY = actions.ContinuousActions[1];  // -1 to 1

        // Execute movement
        Vector2 movement = new Vector2(moveX, moveY) * moveSpeed;
        rb.velocity = movement;

        // Check if caught the Target
        float distanceToTarget = Vector2.Distance(transform.localPosition, targetTransform.localPosition);

        if (distanceToTarget < 0.8f)  // Caught if distance < 0.8
        {
            Debug.Log("Target Caught!");
            AddReward(1.0f);   // Give reward
            EndEpisode();      // End episode
        }

        // Small penalty each step (encourage efficiency)
        AddReward(-1f / maxStepsPerEpisode);

        // Force end if exceeded max steps
        if (StepCount >= maxStepsPerEpisode)
        {
            Debug.Log("Timeout! Episode ended");
            EndEpisode();
        }
    }

   
    /// Heuristic Mode - For testing (keyboard control)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;

        // WASD or Arrow keys control

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        continuousActions[0] = horizontal;  // A/D  
        continuousActions[1] = vertical;    // W/S 

        Debug.Log("Heuristic control mode");
    }

    /// <summary>
    /// Collision Detection - Wall penalty
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            Debug.Log("Hit wall!");
            AddReward(-0.1f);  // Small penalty
        }
    }
}