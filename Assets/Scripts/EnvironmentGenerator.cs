using System.Collections.Generic;
using UnityEngine;

public class EnvironmentGenerator : MonoBehaviour
{
    [Header("Maze Settings")]
    public int width = 21;
    public int height = 21;
    
    [Header("Extra Passages")]
    [Range(0f, 0.3f)]
    public float extraPassages = 0.2f;
    
    [Header("Visual Settings")]
    public Color wallColor = Color.black;
    public Color floorColor = new Color(0.85f, 0.85f, 0.85f);
    
    private Transform mapParent;
    private int[,] maze;

    void Start()
    {
        GenerateMaze();
        AddExtraPassages();
        EnsureCornerSpaces();
        RenderMaze();
        SetupPlayerPositions();
    }

    void GenerateMaze()
    {
        maze = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                maze[x, y] = 1;

        int startX = 1;
        int startY = 1;
        maze[startX, startY] = 0;

        List<Vector2Int> walls = new List<Vector2Int>();
        walls.AddRange(GetNeighborWalls(startX, startY));

        while (walls.Count > 0)
        {
            int i = Random.Range(0, walls.Count);
            Vector2Int wall = walls[i];
            walls.RemoveAt(i);

            if (CanCarve(wall.x, wall.y))
            {
                maze[wall.x, wall.y] = 0;
                foreach (var w in GetNeighborWalls(wall.x, wall.y))
                    if (maze[w.x, w.y] == 1)
                        walls.Add(w);
            }
        }
    }

    bool CanCarve(int x, int y)
    {
        int passages = 0;
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            int nx = x + d.x;
            int ny = y + d.y;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                continue;
            if (maze[nx, ny] == 0)
                passages++;
        }
        return passages == 1;
    }

    List<Vector2Int> GetNeighborWalls(int x, int y)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            int nx = x + d.x;
            int ny = y + d.y;
            if (nx > 0 && ny > 0 && nx < width - 1 && ny < height - 1)
                list.Add(new Vector2Int(nx, ny));
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

        if (chaser != null)
        {
            chaser.transform.position = new Vector3(1, 1, 0);
            SetupChaser(chaser);
        }

        if (target != null)
        {
            target.transform.position = new Vector3(width - 2, height - 2, 0);
            
            SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 1;
            }
            
            TargetController targetController = target.GetComponent<TargetController>();
            if (targetController != null)
            {
                targetController.enabled = false;
            }
        }
    }

    void SetupChaser(GameObject chaser)
    {
        Rigidbody2D rb = chaser.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = chaser.AddComponent<Rigidbody2D>();
        }
        
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.drag = 0;
        rb.angularDrag = 0;
        
        ChaserController controller = chaser.GetComponent<ChaserController>();
        if (controller == null)
        {
            controller = chaser.AddComponent<ChaserController>();
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
