using UnityEngine;
using System.Collections.Generic;

public class ChaserAI : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2.5f;
    
    [Header("Detection Settings")]
    [SerializeField] private float catchRadius = 0.5f;
    
    private Transform targetTransform;
    private EnvironmentGenerator environmentGenerator;
    private Vector3 targetPosition;
    private bool isMoving = false;
    
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int currentPathIndex = 0;
    [SerializeField] private float pathUpdateInterval = 0.5f;
    private float lastPathUpdateTime = 0f;
    
    void Start()
    {
        targetPosition = transform.position;
        
        GameObject target = GameObject.Find("Target");
        if (target != null)
        {
            targetTransform = target.transform;
        }
        
        environmentGenerator = FindObjectOfType<EnvironmentGenerator>();
    }
    
    void FixedUpdate()
    {
        if (targetTransform == null || environmentGenerator == null) return;
        
        if (Time.time - lastPathUpdateTime > pathUpdateInterval)
        {
            UpdatePath();
            lastPathUpdateTime = Time.time;
        }
        
        if (!isMoving && currentPath != null && currentPath.Count > 0)
        {
            MoveAlongPath();
        }
        
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
            
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
        
        CheckCatch();
    }
    
    void UpdatePath()
    {
        Vector2Int chaserPos = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));
        Vector2Int targetPos = new Vector2Int(Mathf.RoundToInt(targetTransform.position.x), Mathf.RoundToInt(targetTransform.position.y));
        
        currentPath = FindPathBFS(chaserPos, targetPos);
        currentPathIndex = 0;
    }
    
    void MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0) return;
        
        while (currentPathIndex < currentPath.Count)
        {
            Vector2Int nextPos = currentPath[currentPathIndex];
            Vector3 nextWorldPos = new Vector3(nextPos.x, nextPos.y, 0);
            
            if (Vector3.Distance(transform.position, nextWorldPos) < 0.1f)
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
            targetPosition = new Vector3(nextPos.x, nextPos.y, 0);
            isMoving = true;
        }
    }
    
    List<Vector2Int> FindPathBFS(Vector2Int start, Vector2Int goal)
    {
        int[,] maze = environmentGenerator.GetMaze();
        if (maze == null) return new List<Vector2Int>();
        
        int width = maze.GetLength(0);
        int height = maze.GetLength(1);
        
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        
        queue.Enqueue(start);
        visited.Add(start);
        cameFrom[start] = start;
        
        Vector2Int[] directions = { 
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
            
            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;
                
                if (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height)
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
        
        if (!foundPath)
        {
            return new List<Vector2Int>();
        }
        
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
    
    void CheckCatch()
    {
        if (targetTransform == null) return;
        
        float distance = Vector2.Distance(transform.position, targetTransform.position);
        
        if (distance <= catchRadius)
        {
            TrainingEnvironment env = FindObjectOfType<TrainingEnvironment>();
            if (env != null)
            {
                env.OnTargetCaught();
            }
        }
    }
    
    public void ResetAI(Vector3 position)
    {
        transform.position = position;
        targetPosition = position;
        isMoving = false;
        currentPath = null;
        currentPathIndex = 0;
        lastPathUpdateTime = 0f;
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, catchRadius);
        
        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 from = new Vector3(currentPath[i].x, currentPath[i].y, 0);
                Vector3 to = new Vector3(currentPath[i + 1].x, currentPath[i + 1].y, 0);
                Gizmos.DrawLine(from, to);
            }
        }
    }
}

