using UnityEngine;
using System.Collections.Generic;

public class ChaserAI : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float normalSpeed = 4.6f;
    [SerializeField] private float aggressiveSpeed = 5.5f;
    [SerializeField] private float normalUpdateInterval = 0.8f;
    [SerializeField] private float aggressiveUpdateInterval = 0.3f;

    [Header("Turn Detection")]
    [SerializeField] private float dangerDistance = 5f;
    [SerializeField] private float turnDelay = 2.5f;
    [SerializeField] private float turnAngleThreshold = 45f;
    [SerializeField] private float turnDetectionDistance = 0.5f;

    [Header("Detection Settings")]
    [SerializeField] private float catchRadius = 1f;

    private Transform targetTransform;
    private EnvironmentGenerator environmentGenerator;

    private Vector3 targetPosition;
    private bool isMoving = false;
    private float currentSpeed;

    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int currentPathIndex = 0;
    private float lastPathUpdateTime = 0f;

    private Vector3 lastTargetPosition;
    private Vector2 lastTargetDirection = Vector2.zero;
    private float lastTurnTime = -999f;

    void Start()
    {
        targetPosition = transform.position;
        currentSpeed = normalSpeed;

        GameObject target = GameObject.Find("Target");
        if (target != null)
        {
            targetTransform = target.transform;
            lastTargetPosition = targetTransform.position;
        }

        environmentGenerator = FindObjectOfType<EnvironmentGenerator>();
    }

    void FixedUpdate()
    {
        if (targetTransform == null || environmentGenerator == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);

        if (distanceToTarget < dangerDistance)
        {
            currentSpeed = aggressiveSpeed;
            DetectTargetTurnRealtime();
        }
        else
        {
            currentSpeed = normalSpeed;
            lastTurnTime = -999f;
            lastTargetDirection = Vector2.zero;
            lastTargetPosition = targetTransform.position;
        }

        if (ShouldUpdatePath(distanceToTarget))
        {
            UpdatePath();
            lastPathUpdateTime = Time.time;
        }

        ProcessMovement();
        
        CheckCatch();
    }

    void DetectTargetTurnRealtime()
    {
        Vector3 currentPos = targetTransform.position;
        Vector2 currentDirection = (currentPos - lastTargetPosition).normalized;
        
        float movementDistance = Vector3.Distance(currentPos, lastTargetPosition);
        
        if (movementDistance > turnDetectionDistance)
        {
            if (lastTargetDirection != Vector2.zero)
            {
                float angle = Vector2.Angle(currentDirection, lastTargetDirection);
                
                if (angle > turnAngleThreshold && angle < 135f)
                {
                    lastTurnTime = Time.time;
                }
            }
            
            lastTargetPosition = currentPos;
            lastTargetDirection = currentDirection;
        }
    }

    bool ShouldUpdatePath(float distanceToTarget)
    {
        float currentUpdateInterval = (distanceToTarget < dangerDistance) 
            ? aggressiveUpdateInterval 
            : normalUpdateInterval;

        if (Time.time - lastPathUpdateTime < currentUpdateInterval)
        {
            return false;
        }

        if (distanceToTarget < dangerDistance)
        {
            float timeSinceTurn = Time.time - lastTurnTime;
            
            if (timeSinceTurn < turnDelay)
            {
                return false;
            }
        }

        return true;
    }

    void ProcessMovement()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                currentSpeed * Time.fixedDeltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = new Vector3(
                    Mathf.Round(transform.position.x),
                    Mathf.Round(transform.position.y),
                    0
                );
                
                TrySelectNextWaypoint();
            }
        }
        else
        {
            TrySelectNextWaypoint();
        }
    }

    void TrySelectNextWaypoint()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            isMoving = false;
            return;
        }

        while (currentPathIndex < currentPath.Count)
        {
            Vector3 waypoint = new Vector3(
                currentPath[currentPathIndex].x,
                currentPath[currentPathIndex].y,
                0
            );
            
            if (Vector3.Distance(transform.position, waypoint) < 0.4f)
            {
                currentPathIndex++;
            }
            else
            {
                break;
            }
        }

        if (currentPathIndex < currentPath.Count)
        {
            Vector2Int nextPos = currentPath[currentPathIndex];

            if (IsWall(nextPos) || IsBlockedByWalls(nextPos))
            {
                isMoving = false;
                return;
            }

            targetPosition = new Vector3(nextPos.x, nextPos.y, 0);
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }
    }

    void UpdatePath()
    {
        Vector2Int chaserPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y)
        );
        Vector2Int targetPos = new Vector2Int(
            Mathf.RoundToInt(targetTransform.position.x),
            Mathf.RoundToInt(targetTransform.position.y)
        );

        currentPath = FindPathBFS(chaserPos, targetPos);

        if (currentPath == null || currentPath.Count == 0)
        {
            currentPath = new List<Vector2Int>();
            isMoving = false;
            targetPosition = transform.position;
            return;
        }

        currentPathIndex = 0;
    }

    List<Vector2Int> FindPathBFS(Vector2Int start, Vector2Int goal)
    {
        int[,] maze = environmentGenerator.GetMaze();
        if (maze == null) return new List<Vector2Int>();

        if (maze[start.x, start.y] == 1 || maze[goal.x, goal.y] == 1)
            return new List<Vector2Int>();

        int width = maze.GetLength(0);
        int height = maze.GetLength(1);

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);
        cameFrom[start] = start;

        Vector2Int[] dirs = {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0)
        };

        bool foundPath = false;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == goal)
            {
                foundPath = true;
                break;
            }

            foreach (var dir in dirs)
            {
                Vector2Int neighbor = current + dir;
                if (neighbor.x < 0 || neighbor.x >= width ||
                    neighbor.y < 0 || neighbor.y >= height)
                    continue;
                if (visited.Contains(neighbor))
                    continue;
                if (maze[neighbor.x, neighbor.y] == 1)
                    continue;

                queue.Enqueue(neighbor);
                visited.Add(neighbor);
                cameFrom[neighbor] = current;
            }
        }

        if (!foundPath) return new List<Vector2Int>();

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int step = goal;
        while (step != start)
        {
            path.Add(step);
            step = cameFrom[step];
        }

        path.Reverse();
        return path;
    }

    bool IsWall(Vector2Int pos)
    {
        if (!Application.isPlaying)
            return false;
        if (environmentGenerator == null) 
            return false;
        int[,] maze = environmentGenerator.GetMaze();
        int width = maze.GetLength(0);
        int height = maze.GetLength(1);
        if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height)
            return true;
        return maze[pos.x, pos.y] == 1;
    }

    bool IsBlockedByWalls(Vector2Int pos)
    {
        if (environmentGenerator == null) return false;
        int[,] maze = environmentGenerator.GetMaze();
        int width = maze.GetLength(0);
        int height = maze.GetLength(1);
        int wallCount = 0;

        Vector2Int[] dirs = {
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
            new Vector2Int(1,0),
            new Vector2Int(-1,0)
        };

        foreach (var d in dirs)
        {
            int nx = pos.x + d.x;
            int ny = pos.y + d.y;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height || maze[nx, ny] == 1)
                wallCount++;
        }
        return wallCount >= 3;
    }

    void CheckCatch()
    {
        if (targetTransform == null) return;

        float distance = Vector2.Distance(transform.position, targetTransform.position);
        if (distance <= catchRadius)
        {
            TestEnvironment testEnv = FindObjectOfType<TestEnvironment>();
            if (testEnv != null)
            {
                testEnv.OnTargetCaught();
                return;
            }
            
            TrainingEnvironment env = FindObjectOfType<TrainingEnvironment>();
            if (env != null)
                env.OnTargetCaught();
        }
    }

    public void ResetAI(Vector3 position)
    {
        isMoving = false;
        
        transform.position = new Vector3(
            Mathf.Round(position.x),
            Mathf.Round(position.y),
            0
        );
        
        targetPosition = transform.position;
        currentSpeed = normalSpeed;
        
        currentPath = new List<Vector2Int>();
        currentPathIndex = 0;
        lastPathUpdateTime = 0f;
        
        if (targetTransform != null)
        {
            lastTargetPosition = targetTransform.position;
            lastTargetDirection = Vector2.zero;
            lastTurnTime = -999f;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, catchRadius);

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, dangerDistance);

        if (currentPath != null && currentPath.Count > 0)
        {
            float distanceToTarget = targetTransform != null ? 
                Vector3.Distance(transform.position, targetTransform.position) : 999f;
            
            bool inDangerZone = distanceToTarget < dangerDistance;
            float timeSinceTurn = Time.time - lastTurnTime;
            bool inTurnDelay = timeSinceTurn < turnDelay;
            
            if (inDangerZone && inTurnDelay)
                Gizmos.color = Color.red;
            else if (inDangerZone)
                Gizmos.color = new Color(1f, 0.5f, 0f);
            else
                Gizmos.color = Color.green;
            
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 from = new Vector3(currentPath[i].x, currentPath[i].y, 0);
                Vector3 to = new Vector3(currentPath[i + 1].x, currentPath[i + 1].y, 0);
                Gizmos.DrawLine(from, to);
            }
        }

        if (Application.isPlaying && targetTransform != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);
            if (distanceToTarget < dangerDistance)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(lastTargetPosition, 0.3f);
                
                if (lastTargetDirection != Vector2.zero)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(lastTargetPosition, lastTargetPosition + (Vector3)lastTargetDirection);
                }
            }
        }

        if (Application.isPlaying)
        {
            Gizmos.color = IsWall(Vector2Int.RoundToInt(targetPosition)) ? Color.magenta : Color.cyan;
        }
        else
        {
            Gizmos.color = Color.cyan;
        }
        Gizmos.DrawSphere(targetPosition, 0.15f);
    }
}