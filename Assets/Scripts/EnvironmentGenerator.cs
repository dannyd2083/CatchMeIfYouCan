using System.Collections.Generic;
using UnityEngine;

public class EnvironmentGenerator : MonoBehaviour
{
    [Header("Maze Settings")]
    public int width = 21;
    public int height = 21;

    [Header("Extra Passages")]
    [Range(0f, 1.0f)]
    public float extraPassages = 0.2f;

    [Header("Visual Settings")]
    public Color wallColor = Color.black;
    public Color floorColor = new Color(0.85f, 0.85f, 0.85f);

    [Header("Multi-Map Training")]
    public int currentMapIndex = 0;
    private static readonly int[] MAP_SEEDS = new int[] {
        12345, 23456, 34567, 45678, 56789,
        67890, 78901, 89012, 90123, 11234,
        22345, 33456, 44567, 55678, 66789,
        77890, 88901, 99012, 10123, 21234,
        99999, 88888, 77777, 66666, 55555,
        44444, 33333, 22222, 11111, 10001
    };

    [Header("Random Spawn Settings")]
    public int minSpawnDistance = 15;

    private Transform mapParent;
    private int[,] maze;
    private static int resetCounter = 0;

    void Start()
    {
        GenerateMaze();
        AddExtraPassages();
        EnsureCornerSpaces();
        
        resetCounter++;
        Random.InitState((int)(System.DateTime.Now.Ticks + resetCounter * 1000));
        
        RenderMaze();
        SetupPlayerPositions();
    }

    public int[,] GetMaze()
    {
        return maze;
    }

    public void SwitchToMap(int mapIndex)
    {
        currentMapIndex = mapIndex;
        RegenerateMaze();
    }

    public void RegenerateMaze()
    {
        if (mapParent != null)
        {
            foreach (Transform child in mapParent)
            {
                Destroy(child.gameObject);
            }
        }

        GenerateMaze();
        AddExtraPassages();
        EnsureCornerSpaces();
        
        resetCounter++;
        Random.InitState((int)(System.DateTime.Now.Ticks + resetCounter * 1000));
        
        RenderMaze();
        SetupPlayerPositions();
    }

    public void ResetPlayerPositions()
    {
        resetCounter++;
        Random.InitState((int)(System.DateTime.Now.Ticks + resetCounter * 1000));
        
        SetupPlayerPositions();
    }

    void GenerateMaze()
    {
        int seed = MAP_SEEDS[currentMapIndex % MAP_SEEDS.Length];
        Random.InitState(seed);
        maze = new int[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                maze[x, y] = 1;

        int startX = 1;
        int startY = 1;
        maze[startX, startY] = 0;

        List<Vector2Int> frontiers = new List<Vector2Int>();
        AddFrontiers(startX, startY, frontiers);

        while (frontiers.Count > 0)
        {
            int i = Random.Range(0, frontiers.Count);
            Vector2Int frontier = frontiers[i];
            frontiers.RemoveAt(i);

            List<Vector2Int> neighbors = GetCarvedNeighbors(frontier.x, frontier.y);

            if (neighbors.Count > 0)
            {
                Vector2Int neighbor = neighbors[Random.Range(0, neighbors.Count)];

                maze[frontier.x, frontier.y] = 0;

                int wallX = (frontier.x + neighbor.x) / 2;
                int wallY = (frontier.y + neighbor.y) / 2;
                maze[wallX, wallY] = 0;

                AddFrontiers(frontier.x, frontier.y, frontiers);
            }
        }

        for (int x = 0; x < width; x++)
        {
            maze[x, 0] = 1;
            maze[x, height - 1] = 1;
        }
        for (int y = 0; y < height; y++)
        {
            maze[0, y] = 1;
            maze[width - 1, y] = 1;
        }
    }

    void AddFrontiers(int x, int y, List<Vector2Int> frontiers)
    {
        Vector2Int[] dirs = {
            new Vector2Int(0, 2),
            new Vector2Int(0, -2),
            new Vector2Int(-2, 0),
            new Vector2Int(2, 0)
        };

        foreach (var d in dirs)
        {
            int nx = x + d.x;
            int ny = y + d.y;

            if (nx > 0 && ny > 0 && nx < width && ny < height && maze[nx, ny] == 1)
            {
                if (!frontiers.Contains(new Vector2Int(nx, ny)))
                {
                    frontiers.Add(new Vector2Int(nx, ny));
                }
            }
        }
    }

    List<Vector2Int> GetCarvedNeighbors(int x, int y)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        Vector2Int[] dirs = {
            new Vector2Int(0, 2),
            new Vector2Int(0, -2),
            new Vector2Int(-2, 0),
            new Vector2Int(2, 0)
        };

        foreach (var d in dirs)
        {
            int nx = x + d.x;
            int ny = y + d.y;

            if (nx > 0 && ny > 0 && nx < width && ny < height && maze[nx, ny] == 0)
            {
                list.Add(new Vector2Int(nx, ny));
            }
        }
        return list;
    }

    void AddExtraPassages()
    {
        List<Vector2Int> breakableWalls = new List<Vector2Int>();

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (maze[x, y] == 1 && CanBreakWall(x, y))
                {
                    breakableWalls.Add(new Vector2Int(x, y));
                }
            }
        }

        int wallsToBreak = Mathf.RoundToInt(breakableWalls.Count * extraPassages);

        for (int i = 0; i < wallsToBreak && breakableWalls.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, breakableWalls.Count);
            Vector2Int wall = breakableWalls[randomIndex];
            maze[wall.x, wall.y] = 0;
            breakableWalls.RemoveAt(randomIndex);
        }
    }

    bool CanBreakWall(int x, int y)
    {
        bool horizontal = (maze[x - 1, y] == 0 && maze[x + 1, y] == 0);
        bool vertical = (maze[x, y - 1] == 0 && maze[x, y + 1] == 0);
        return horizontal || vertical;
    }

    void EnsureCornerSpaces()
    {
        maze[1, 1] = 0;
        maze[width - 2, height - 2] = 0;
    }

    void SetupPlayerPositions()
    {
        GameObject chaser = GameObject.Find("Chaser");
        GameObject target = GameObject.Find("Target");

        GetRandomSpawnPositions(out Vector2Int targetPos, out Vector2Int chaserPos);
        
        Vector3 targetStartPos = new Vector3(targetPos.x, targetPos.y, 0);
        Vector3 chaserStartPos = new Vector3(chaserPos.x, chaserPos.y, 0);

        if (target != null)
        {
            target.transform.position = targetStartPos;
            SetupTarget(target);
        }

        if (chaser != null)
        {
            chaser.transform.position = chaserStartPos;
            SetupChaser(chaser);
            
            ChaserAI chaserAI = chaser.GetComponent<ChaserAI>();
            if (chaserAI != null)
            {
                chaserAI.ResetAI(chaserStartPos);
            }
        }
    }

    void GetRandomSpawnPositions(out Vector2Int targetPos, out Vector2Int chaserPos)
    {
        List<Vector2Int> allPositions = new List<Vector2Int>();

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (maze[x, y] == 0)
                {
                    allPositions.Add(new Vector2Int(x, y));
                }
            }
        }

        if (allPositions.Count < 2)
        {
            targetPos = new Vector2Int(1, 1);
            chaserPos = new Vector2Int(width - 2, height - 2);
            return;
        }

        targetPos = allPositions[Random.Range(0, allPositions.Count)];

        List<Vector2Int> validChaserPositions = new List<Vector2Int>();
        foreach (var pos in allPositions)
        {
            int dist = Mathf.Abs(pos.x - targetPos.x) + Mathf.Abs(pos.y - targetPos.y);
            if (dist >= minSpawnDistance)
            {
                validChaserPositions.Add(pos);
            }
        }

        if (validChaserPositions.Count == 0)
        {
            float maxDist = 0;
            chaserPos = allPositions[0];
            foreach (var pos in allPositions)
            {
                float dist = Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(targetPos.x, targetPos.y));
                if (dist > maxDist)
                {
                    maxDist = dist;
                    chaserPos = pos;
                }
            }
        }
        else
        {
            chaserPos = validChaserPositions[Random.Range(0, validChaserPositions.Count)];
        }
    }


    void SetupChaser(GameObject chaser)
    {
        ChaserAI aiController = chaser.GetComponent<ChaserAI>();
        if (aiController == null)
        {
            aiController = chaser.AddComponent<ChaserAI>();
        }

        Rigidbody2D rb = chaser.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            DestroyImmediate(rb);
        }

        BoxCollider2D collider = chaser.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = chaser.AddComponent<BoxCollider2D>();
        }
        collider.size = Vector2.one * 0.8f;

        SpriteRenderer spriteRenderer = chaser.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 1;
        }
    }

    void SetupTarget(GameObject target)
    {
        TargetAgent agent = target.GetComponent<TargetAgent>();
        if (agent == null)
        {
            agent = target.AddComponent<TargetAgent>();
        }

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            DestroyImmediate(rb);
        }

        BoxCollider2D collider = target.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = target.AddComponent<BoxCollider2D>();
        }
        collider.size = Vector2.one * 0.8f;

        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 1;
        }
    }

    void RenderMaze()
    {
        if (mapParent == null)
            mapParent = new GameObject("Map").transform;

        foreach (Transform child in mapParent)
            Destroy(child.gameObject);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x, y, 0);

                if (maze[x, y] == 1)
                {
                    GameObject wall = CreateSprite("Wall", pos, wallColor);
                    wall.tag = "Wall";
                    wall.layer = LayerMask.NameToLayer("WallLayer");

                    BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
                    collider.size = Vector2.one;

                    SpriteRenderer wallRenderer = wall.GetComponent<SpriteRenderer>();
                    if (wallRenderer != null)
                    {
                        wallRenderer.sortingOrder = 2;
                    }

                    wall.transform.SetParent(mapParent);
                }
                else
                {
                    GameObject floor = CreateSprite("Floor", pos, floorColor);
                    floor.transform.SetParent(mapParent);
                }
            }
        }

        AdjustCamera();
    }

    void AdjustCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(width / 2f, height / 2f, -10);

            float aspectRatio = (float)Screen.width / Screen.height;
            float verticalSize = height / 2f + 1;
            float horizontalSize = width / (2f * aspectRatio) + 1;
            mainCam.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
        }
    }

    GameObject CreateSprite(string name, Vector3 position, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = position;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite();
        renderer.color = color;
        renderer.sortingOrder = 0;

        return obj;
    }

    Sprite CreateSquareSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }
}