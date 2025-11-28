using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using System;

public class ChaserAgent : Agent
{
    [Header("Movement Settings")]
    [SerializeField] private float normalSpeed = 4.6f;
    [SerializeField] private float aggressiveSpeed = 5.5f;
    private float currentSpeed;

    [Header("Danger Zone")]
    [SerializeField] private float dangerDistance = 5f;

    [Header("Intercept Prediction")]
    [SerializeField] private float predictionHorizon = 2f;

    [Header("References")]
    private Transform targetTransform;
    private EnvironmentGenerator envGenerator;

    private Vector3 targetPosition;
    private bool isMoving = false;
    private Vector3 lastMoveDirection = Vector3.zero;

    private Vector3 lastTargetPosition;
    private Vector2 targetMoveDirection = Vector2.zero;
    private float episodeStartTime = 0f;

    private float lastNextStepDist = 0f;
    private Vector2 lastTargetDir2D = Vector2.zero;

    private float lastShortInterceptDist = 0f;
    private float lastLongInterceptDist = 0f;

    // 🔥 Target 转弯触发的持续减速
    private float turningSlowTimer = 0f;
    [SerializeField] private float turnSlowDuration = 2.0f;   // 减速持续时间
    [SerializeField] private float turnSlowScale = 0.1f;      // 减速比例

    public override void Initialize()
    {
        envGenerator = FindObjectOfType<EnvironmentGenerator>();
        GameObject target = GameObject.Find("Target");
        if (target != null)
        {
            targetTransform = target.transform;
            lastTargetPosition = targetTransform.position;
        }
        currentSpeed = normalSpeed;
    }

    public override void OnEpisodeBegin()
    {
        targetPosition = transform.position;
        isMoving = false;
        lastMoveDirection = Vector3.zero;
        currentSpeed = normalSpeed;
        episodeStartTime = Time.time;

        if (targetTransform != null)
        {
            lastTargetPosition = targetTransform.position;
            targetMoveDirection = Vector2.zero;

            Vector2Int my = ToCell(transform.position);
            Vector2Int tar = ToCell(targetTransform.position);
            lastNextStepDist = CalculateBfsDistance(my, tar);

            lastShortInterceptDist = Vector2.Distance(transform.position, targetTransform.position);
            lastLongInterceptDist = lastShortInterceptDist;

            lastTargetDir2D = Vector2.zero;
        }

        turningSlowTimer = 0f;
    }

    void FixedUpdate()
    {
        if (targetTransform == null) return;

        UpdateTargetMoveDirection();

        float dist = Vector2.Distance(transform.position, targetTransform.position);
        currentSpeed = (dist < dangerDistance) ? aggressiveSpeed : normalSpeed;

        if (isMoving)
        {
            // =============================
            // 🔥 Target 是否在转弯（方向变化）
            // =============================
            float targetTurnDot = 1f;
            if (lastTargetDir2D != Vector2.zero && targetMoveDirection != Vector2.zero)
                targetTurnDot = Vector2.Dot(lastTargetDir2D.normalized, targetMoveDirection.normalized);

            bool targetIsTurning = targetTurnDot < 0.75f;

            // =============================
            // 🔥 Chaser 是否在 target 的正后方
            // =============================
            float rearDot = 0f;
            if (lastMoveDirection != Vector3.zero)
            {
                Vector2 toTarget = (targetTransform.position - transform.position).normalized;
                Vector2 lm = new Vector2(lastMoveDirection.x, lastMoveDirection.y).normalized;
                rearDot = Vector2.Dot(lm, toTarget);
            }
            bool isRearTracking = rearDot > 0.6f;   // 不误伤侧后截击

            // =============================
            // 🔥 距离是否近
            // =============================
            float chaseDistance = Vector2.Distance(transform.position, targetTransform.position);
            bool isClose = chaseDistance < 2.0f;

            // =============================
            // 🔥 满足条件 → 触发 1.5 秒的强减速
            // =============================
            if (targetIsTurning && isRearTracking && isClose)
            {
                turningSlowTimer = turnSlowDuration;
            }

            // 计时器递减
            if (turningSlowTimer > 0f)
                turningSlowTimer -= Time.fixedDeltaTime;

            // 应用减速（20% 速度）
            float turnSpeedScale = (turningSlowTimer > 0f) ? turnSlowScale : 1f;
            float finalSpeed = currentSpeed * turnSpeedScale;

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                finalSpeed * Time.fixedDeltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }

            lastTargetDir2D = targetMoveDirection;
        }
    }
    void UpdateTargetMoveDirection()
    {
        Vector3 now = targetTransform.position;
        Vector3 delta = now - lastTargetPosition;
        if (delta.magnitude > 0.01f)
            targetMoveDirection = new Vector2(delta.x, delta.y).normalized;
        lastTargetPosition = now;
    }

    Vector2Int ToCell(Vector3 pos)
    {
        return new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
    }

    bool InMaze(int x, int y, int[,] maze)
    {
        return (x >= 0 && y >= 0 && x < maze.GetLength(0) && y < maze.GetLength(1));
    }

    int CalculateBfsDistance(Vector2Int start, Vector2Int goal)
    {
        int[,] maze = envGenerator.GetMaze();
        if (start == goal) return 0;
        if (!InMaze(start.x, start.y, maze) || !InMaze(goal.x, goal.y, maze)) return 999;
        if (maze[start.x, start.y] == 1 || maze[goal.x, goal.y] == 1) return 999;

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int>();

        q.Enqueue(start);
        dist[start] = 0;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            int cd = dist[cur];

            foreach (var d in dirs)
            {
                Vector2Int nxt = cur + d;
                if (!InMaze(nxt.x, nxt.y, maze)) continue;
                if (maze[nxt.x, nxt.y] == 1) continue;
                if (dist.ContainsKey(nxt)) continue;

                dist[nxt] = cd + 1;
                if (nxt == goal) return cd + 1;
                q.Enqueue(nxt);
            }
        }
        return 999;
    }

    Vector2Int PredictTargetNextStep()
    {
        int[,] maze = envGenerator.GetMaze();
        Vector2Int tar = ToCell(targetTransform.position);
        Vector2Int cha = ToCell(transform.position);

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();

        q.Enqueue(tar);
        parent[tar] = tar;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        bool found = false;

        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            if (cur == cha) { found = true; break; }

            foreach (var d in dirs)
            {
                Vector2Int nxt = cur + d;
                if (!parent.ContainsKey(nxt) && InMaze(nxt.x, nxt.y, maze) && maze[nxt.x, nxt.y] == 0)
                {
                    parent[nxt] = cur;
                    q.Enqueue(nxt);
                }
            }
        }

        if (!found) return tar;

        Vector2Int p = cha;
        while (parent[p] != tar)
            p = parent[p];

        return p;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        int[,] maze = envGenerator.GetMaze();

        sensor.AddObservation(transform.position.x / 21f);
        sensor.AddObservation(transform.position.y / 21f);
        sensor.AddObservation(targetTransform.position.x / 21f);
        sensor.AddObservation(targetTransform.position.y / 21f);

        Vector2 toTarget = (targetTransform.position - transform.position);
        toTarget.Normalize();
        sensor.AddObservation(toTarget);

        float dist = Vector2.Distance(transform.position, targetTransform.position);
        sensor.AddObservation(dist / 30f);

        sensor.AddObservation(targetMoveDirection);

        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);

        Vector2Int nextStep = PredictTargetNextStep();
        Vector2Int me = ToCell(transform.position);

        int dx = nextStep.x - me.x;
        int dy = nextStep.y - me.y;

        sensor.AddObservation(dy > 0 ? 1f : 0f);
        sensor.AddObservation(dy < 0 ? 1f : 0f);
        sensor.AddObservation(dx < 0 ? 1f : 0f);
        sensor.AddObservation(dx > 0 ? 1f : 0f);

        Vector2Int tar = ToCell(targetTransform.position);
        sensor.AddObservation(CountLocalDegree(tar.x, tar.y, maze) / 4f);

        int px = me.x;
        int py = me.y;
        int vr = 4;

        for (int dy2 = vr; dy2 >= -vr; dy2--)
            for (int dx2 = -vr; dx2 <= vr; dx2++)
            {
                int x = px + dx2;
                int y = py + dy2;
                bool isWall = !InMaze(x, y, maze) || maze[x, y] == 1;
                sensor.AddObservation(isWall ? 1f : 0f);
            }

        sensor.AddObservation(isMoving ? 1f : 0f);
        sensor.AddObservation(lastMoveDirection.x);
        sensor.AddObservation(lastMoveDirection.y);
        sensor.AddObservation(CountLocalDegree(px, py, maze) / 4f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];
        Vector3 dir = GetDirection(action);

        if (!isMoving && dir != Vector3.zero && !CheckWall(dir))
        {
            targetPosition = transform.position + dir;
            isMoving = true;
            lastMoveDirection = dir;
        }

        Vector2Int me = ToCell(transform.position);
        Vector2Int next = PredictTargetNextStep();

        float nowDist = CalculateBfsDistance(me, next);
        float delta = lastNextStepDist - nowDist;
        AddReward(0.04f * (float)System.Math.Tanh(delta));
        lastNextStepDist = nowDist;

        if (nowDist == 0)
            AddReward(0.3f);

        float turnDot = Vector2.Dot(lastTargetDir2D, targetMoveDirection);
        if (turnDot < 0.5f)
            AddReward(0.02f);

        lastTargetDir2D = targetMoveDirection;

        // ========= 🔥 Target 转弯惩罚 =========
        float targetTurnDotReward = 1f;
        if (lastTargetDir2D != Vector2.zero && targetMoveDirection != Vector2.zero)
            targetTurnDotReward = Vector2.Dot(lastTargetDir2D.normalized, targetMoveDirection.normalized);
        bool targetTurningReward = targetTurnDotReward < 0.75f;

        float rearDotReward = 0f;
        if (lastMoveDirection != Vector3.zero)
        {
            Vector2 toTargetReward = (targetTransform.position - transform.position).normalized;
            Vector2 lmReward = new Vector2(lastMoveDirection.x, lastMoveDirection.y).normalized;
            rearDotReward = Vector2.Dot(lmReward, toTargetReward);
        }
        bool isRearTrackingReward = rearDotReward > 0.6f;

        float chaseDistReward = Vector2.Distance(transform.position, targetTransform.position);
        bool isCloseReward = chaseDistReward < 2.0f;

        if (targetTurningReward && isRearTrackingReward && isCloseReward)
            AddReward(-0.01f);

        AddReward(-0.0001f);
    }
    private Vector3 GetDirection(int a)
    {
        switch (a)
        {
            case 1: return Vector3.up;
            case 2: return Vector3.down;
            case 3: return Vector3.left;
            case 4: return Vector3.right;
        }
        return Vector3.zero;
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask mask)
    {
        if (CheckWall(Vector3.up)) mask.SetActionEnabled(0, 1, false);
        if (CheckWall(Vector3.down)) mask.SetActionEnabled(0, 2, false);
        if (CheckWall(Vector3.left)) mask.SetActionEnabled(0, 3, false);
        if (CheckWall(Vector3.right)) mask.SetActionEnabled(0, 4, false);
    }

    private bool CheckWall(Vector3 direction)
    {
        Vector3 p = transform.position + direction;
        int[,] maze = envGenerator.GetMaze();
        int x = Mathf.RoundToInt(p.x);
        int y = Mathf.RoundToInt(p.y);
        if (!InMaze(x, y, maze)) return true;
        return maze[x, y] == 1;
    }

    private int CountLocalDegree(int x, int y, int[,] maze)
    {
        int c = 0;
        if (InMaze(x, y + 1, maze) && maze[x, y + 1] == 0) c++;
        if (InMaze(x, y - 1, maze) && maze[x, y - 1] == 0) c++;
        if (InMaze(x + 1, y, maze) && maze[x + 1, y] == 0) c++;
        if (InMaze(x - 1, y, maze) && maze[x - 1, y] == 0) c++;
        return c;
    }

    // ============================================
    // 🔥 Required functions (must NOT remove)
    // ============================================
    public void OnCatchTarget()
    {
        float catchTime = Time.time - episodeStartTime;
        float timeBonus = Mathf.Clamp01(1f - catchTime / 30f) * 5f;
        AddReward(20f + timeBonus);
        RecordStats("catch");
        EndEpisode();
    }

    public void OnTimeout()
    {
        AddReward(-0.5f);
        RecordStats("timeout");
        EndEpisode();
    }

    private void RecordStats(string reason)
    {
        var stats = Academy.Instance.StatsRecorder;
        float chaseTime = Time.time - episodeStartTime;

        float finalDistance = targetTransform != null
            ? Vector2.Distance(transform.position, targetTransform.position)
            : 0f;

        stats.Add("Chaser/ChaseTime", chaseTime);
        stats.Add("Chaser/FinalDistance", finalDistance);
        stats.Add("Chaser/TotalReward", GetCumulativeReward());
        stats.Add("Chaser/Caught", reason == "catch" ? 1 : 0);
        stats.Add("Chaser/Timeout", reason == "timeout" ? 1 : 0);
    }

    public void SyncAfterReset()
    {
        targetPosition = transform.position;
        isMoving = false;
        lastMoveDirection = Vector3.zero;
        currentSpeed = normalSpeed;

        if (targetTransform != null)
        {
            lastTargetPosition = targetTransform.position;
            targetMoveDirection = Vector2.zero;
        }

        turningSlowTimer = 0f;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discrete = actionsOut.DiscreteActions;
        int action = 0;
        if (Input.GetKey(KeyCode.I)) action = 1;
        else if (Input.GetKey(KeyCode.K)) action = 2;
        else if (Input.GetKey(KeyCode.J)) action = 3;
        else if (Input.GetKey(KeyCode.L)) action = 4;
        discrete[0] = action;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, dangerDistance);

        if (targetTransform != null && targetMoveDirection != Vector2.zero)
        {
            Vector3 future = targetTransform.position + (Vector3)(targetMoveDirection * predictionHorizon);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(future, 0.3f);
            Gizmos.DrawLine(targetTransform.position, future);
        }
    }
}
