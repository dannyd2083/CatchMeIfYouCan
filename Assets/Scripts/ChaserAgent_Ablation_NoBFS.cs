using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System;

public class ChaserAgent_Ablation_NoBFS : Agent
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
    private Vector2 smoothedTargetDir2D = Vector2.zero;
    private float episodeStartTime = 0f;

    private Vector2 lastTargetDir2D = Vector2.zero;

    private float turningSlowTimer = 0f;
    [SerializeField] private float turnSlowDuration = 2.0f;
    [SerializeField] private float turnSlowScale = 0.1f;

    [Header("Distance Shaping")]
    [SerializeField] private float minEngageDistance = 1.5f;
    [SerializeField] private float maxEngageDistance = 6f;
    [SerializeField] private float distRewardScale = 0.01f;
    [SerializeField] private float farPenalty = -0.003f;
    private float lastRawDist = 0f;


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
            smoothedTargetDir2D = Vector2.zero;
            lastTargetDir2D = Vector2.zero;
            lastRawDist = Vector2.Distance(transform.position, targetTransform.position);
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
            float targetTurnDot = 1f;
            if (lastTargetDir2D != Vector2.zero && targetMoveDirection != Vector2.zero)
                targetTurnDot = Vector2.Dot(lastTargetDir2D.normalized, targetMoveDirection.normalized);

            bool targetIsTurning = targetTurnDot < 0.75f;

            float rearDot = 0f;
            if (lastMoveDirection != Vector3.zero)
            {
                Vector2 toTarget = (targetTransform.position - transform.position).normalized;
                Vector2 lm = new Vector2(lastMoveDirection.x, lastMoveDirection.y).normalized;
                rearDot = Vector2.Dot(lm, toTarget);
            }
            bool isRearTracking = rearDot > 0.6f;
            bool isClose = (dist < 2.0f);

            if (targetIsTurning && isRearTracking && isClose)
                turningSlowTimer = turnSlowDuration;

            if (turningSlowTimer > 0f)
                turningSlowTimer -= Time.fixedDeltaTime;

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
                RequestDecision();
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
        else
            targetMoveDirection = Vector2.zero;

        if (targetMoveDirection != Vector2.zero)
            smoothedTargetDir2D = Vector2.Lerp(smoothedTargetDir2D, targetMoveDirection, 0.2f);
        else
            smoothedTargetDir2D = Vector2.Lerp(smoothedTargetDir2D, Vector2.zero, 0.1f);

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


    public override void CollectObservations(VectorSensor sensor)
    {
        int[,] maze = envGenerator.GetMaze();

        sensor.AddObservation(transform.position.x / 21f);
        sensor.AddObservation(transform.position.y / 21f);
        sensor.AddObservation(targetTransform.position.x / 21f);
        sensor.AddObservation(targetTransform.position.y / 21f);

        Vector2 toTarget = (targetTransform.position - transform.position).normalized;
        sensor.AddObservation(toTarget);

        float dist = Vector2.Distance(transform.position, targetTransform.position);
        sensor.AddObservation(dist / 30f);

        sensor.AddObservation(targetMoveDirection);
        sensor.AddObservation(smoothedTargetDir2D);

        sensor.AddObservation(toTarget.y > 0.5f ? 1f : 0f);
        sensor.AddObservation(toTarget.y < -0.5f ? 1f : 0f);
        sensor.AddObservation(toTarget.x < -0.5f ? 1f : 0f);
        sensor.AddObservation(toTarget.x > 0.5f ? 1f : 0f);

        Vector2Int tar = ToCell(targetTransform.position);
        sensor.AddObservation(CountLocalDegree(tar.x, tar.y, maze) / 4f);

        Vector2Int me = ToCell(transform.position);
        int px = me.x;
        int py = me.y;
        int vr = 4;

        for (int dy2 = vr; dy2 >= -vr; dy2--)
            for (int dx2 = -vr; dx2 <= vr; dx2++)
            {
                int xx = px + dx2;
                int yy = py + dy2;
                bool isWall = !InMaze(xx, yy, maze) || maze[xx, yy] == 1;
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

        if (!isMoving && dir != Vector3.zero && lastMoveDirection != Vector3.zero)
        {
            float turnDot = Vector3.Dot(lastMoveDirection.normalized, dir.normalized);
            if (turnDot < -0.1f)
                AddReward(-0.01f);
        }

        if (!isMoving && dir != Vector3.zero && !CheckWall(dir))
        {
            targetPosition = transform.position + dir;
            isMoving = true;
            lastMoveDirection = dir;
        }

        if (targetTransform != null)
        {
            float dist = Vector2.Distance(transform.position, targetTransform.position);
            float prevDist = lastRawDist;
            float deltaDist = prevDist - dist;

            if (prevDist > maxEngageDistance)
            {
                AddReward(farPenalty);

                if (deltaDist > 0f)
                {
                    float shaped = Mathf.Clamp(deltaDist, -1f, 1f);
                    AddReward(shaped * distRewardScale * 3f);
                }
            }
            else if (prevDist > minEngageDistance && prevDist <= maxEngageDistance)
            {
                if (deltaDist > 0f)
                {
                    float shaped = Mathf.Clamp(deltaDist, -1f, 1f);
                    AddReward(shaped * distRewardScale * 0.5f);
                }
            }

            lastRawDist = dist;

            if (dist < 1.8f)
                AddReward(0.2f);
        }

        float turnDot2 = Vector2.Dot(lastTargetDir2D, targetMoveDirection);
        if (turnDot2 < 0.5f) AddReward(0.02f);

        lastTargetDir2D = targetMoveDirection;


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
        if (InMaze(x + 1, y, maze) && maze[x, y] == 0) c++;
        if (InMaze(x - 1, y, maze) && maze[x, y - 1] == 0) c++;
        return c;
    }


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
            smoothedTargetDir2D = Vector2.zero;
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

        if (targetTransform != null)
        {
            Vector3 future = targetTransform.position;
            if (smoothedTargetDir2D.sqrMagnitude > 0.001f)
                future += (Vector3)(smoothedTargetDir2D.normalized * predictionHorizon);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(future, 0.3f);
            Gizmos.DrawLine(targetTransform.position, future);
        }
    }
}
