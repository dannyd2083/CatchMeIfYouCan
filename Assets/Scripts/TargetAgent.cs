using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class TargetAgent : Agent
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("References")]
    private Transform chaserTransform;
    private TrainingEnvironment trainingEnv;
    
    private Vector3 targetPosition;
    private bool isMoving = false;
    private Vector3 lastPosition;
    private int stuckFrames = 0;
    private const int MAX_STUCK_FRAMES = 10;
    
    private int consecutiveWallHits = 0;
    private Vector3 lastMoveDir = Vector3.zero;
    private float lastDistance = 0f;
    
    public override void Initialize()
    {
        targetPosition = transform.position;
        lastPosition = transform.position;
        
        trainingEnv = GetComponentInParent<TrainingEnvironment>();
        
        GameObject chaser = GameObject.Find("Chaser");
        if (chaser != null)
        {
            chaserTransform = chaser.transform;
        }
    }
    
    public override void OnEpisodeBegin()
    {
        targetPosition = transform.position;
        isMoving = false;
        lastPosition = transform.position;
        stuckFrames = 0;
        consecutiveWallHits = 0;
        
        if (chaserTransform != null)
        {
            lastDistance = Vector2.Distance(transform.position, chaserTransform.position);
        }
        else
        {
            lastDistance = 0f;
        }
    }
    
    void FixedUpdate()
    {
        if (isMoving)
        {
            Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
            Vector3 dir = (targetPosition - transform.position).normalized;
            float dist = Vector3.Distance(transform.position, nextPos);

            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, dist + 0.1f, LayerMask.GetMask("WallLayer"));
            if (hit.collider != null && hit.collider.CompareTag("Wall"))
            {
                isMoving = false;
                AddReward(-0.05f);
                return;
            }
            
            Collider2D targetCheck = Physics2D.OverlapPoint(targetPosition, LayerMask.GetMask("WallLayer"));
            if (targetCheck != null && targetCheck.CompareTag("Wall"))
            {
                isMoving = false;
                AddReward(-0.05f);
                return;
            }

            transform.position = nextPos;
            
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
                consecutiveWallHits = 0;
            }
        }
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position.x);
        sensor.AddObservation(transform.position.y);
        
        if (chaserTransform != null)
        {
            sensor.AddObservation(chaserTransform.position.x);
            sensor.AddObservation(chaserTransform.position.y);
            
            Vector2 directionToChaser = (chaserTransform.position - transform.position).normalized;
            float distanceToChaser = Vector2.Distance(transform.position, chaserTransform.position);
            sensor.AddObservation(directionToChaser.x);
            sensor.AddObservation(directionToChaser.y);
            sensor.AddObservation(distanceToChaser);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
        
        sensor.AddObservation(isMoving ? 1f : 0f);
        
        sensor.AddObservation(CheckWall(Vector3.up) ? 1f : 0f);
        sensor.AddObservation(CheckWall(Vector3.down) ? 1f : 0f);
        sensor.AddObservation(CheckWall(Vector3.left) ? 1f : 0f);
        sensor.AddObservation(CheckWall(Vector3.right) ? 1f : 0f);
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isMoving)
        {
            int action = actions.DiscreteActions[0];
            
            Vector3 direction = Vector3.zero;
            switch (action)
            {
                case 0:
                    AddReward(-0.01f);
                    break;
                case 1:
                    direction = Vector3.up;
                    break;
                case 2:
                    direction = Vector3.down;
                    break;
                case 3:
                    direction = Vector3.left;
                    break;
                case 4:
                    direction = Vector3.right;
                    break;
            }
            
            if (direction != Vector3.zero)
            {
                TryMove(direction);
            }
        }
        
        if (Vector3.Distance(transform.position, lastPosition) < 0.01f)
        {
            stuckFrames++;
            if (stuckFrames == MAX_STUCK_FRAMES)
            {
                AddReward(-0.1f);
            }
        }
        else
        {
            stuckFrames = 0;
        }
        lastPosition = transform.position;
        
        AddReward(0.002f);
        
        // 🔧 调整后的奖励系统
        if (chaserTransform != null)
        {
            float currentDistance = Vector2.Distance(transform.position, chaserTransform.position);
            
            if (lastDistance > 0)
            {
                float delta = currentDistance - lastDistance;
                
                if (delta > 0)
                {
                    AddReward(delta * 0.05f);  // 🔧 提高到 0.05
                }
            }
            
            lastDistance = currentDistance;
            
            if (currentDistance < 3.0f)
            {
                AddReward(-0.01f);  // 🔧 降低到 -0.01
            }
        }
    }
    
    private void TryMove(Vector3 direction)
    {
        Vector3 newPosition = transform.position + direction;
        
        if (!CheckWall(direction))
        {
            bool dangerAhead = false;
            if (chaserTransform != null)
            {
                Vector3 toChaser = (chaserTransform.position - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, chaserTransform.position);
                if (Vector3.Dot(toChaser, direction) > 0.7f && distance < 1.5f)
                    dangerAhead = true;
            }

            if (!dangerAhead && Vector3.Dot(direction, lastMoveDir) < 0)
            {
                AddReward(-0.05f);
            }

            targetPosition = newPosition;
            isMoving = true;
            consecutiveWallHits = 0;
            
            lastMoveDir = direction;
        }
        else
        {
            consecutiveWallHits++;
            
            if (consecutiveWallHits >= 3)
            {
                AddReward(-0.1f);
                consecutiveWallHits = 0;
            }
            else
            {
                AddReward(-0.02f);
            }
        }
    }
    
    private bool CheckWall(Vector3 direction)
    {
        Vector3 checkPosition = transform.position + direction;
        Collider2D hit = Physics2D.OverlapPoint(checkPosition, LayerMask.GetMask("WallLayer"));
        return hit != null && hit.CompareTag("Wall");
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;
        
        if (Input.GetKey(KeyCode.UpArrow))
            discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.DownArrow))
            discreteActions[0] = 2;
        else if (Input.GetKey(KeyCode.LeftArrow))
            discreteActions[0] = 3;
        else if (Input.GetKey(KeyCode.RightArrow))
            discreteActions[0] = 4;
    }
    
    public void OnCaught()
    {
        AddReward(-0.5f);
        EndEpisode();
    }
    
    public void OnTimeReward(float reward)
    {
        AddReward(reward);
    }
}