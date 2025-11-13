using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class TargetAgent : Agent
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("References")]
    private Transform chaserTransform;
    private EnvironmentGenerator envGenerator;

    private Vector3 targetPosition;
    private bool isMoving = false;

    private float lastDistance = 0f;
    private float episodeStartTime = 0f;
    private float totalDistanceFromChaser = 0f;
    private int distanceSampleCount = 0;

    public override void Initialize()
    {
        targetPosition = transform.position;
        envGenerator = FindObjectOfType<EnvironmentGenerator>();

        GameObject chaser = GameObject.Find("Chaser");
        if (chaser != null)
            chaserTransform = chaser.transform;
    }

    public override void OnEpisodeBegin()
    {
        targetPosition = transform.position;
        isMoving = false;
        episodeStartTime = Time.time;
        totalDistanceFromChaser = 0f;
        distanceSampleCount = 0;

        if (chaserTransform != null)
            lastDistance = Vector2.Distance(transform.position, chaserTransform.position);
    }

    void FixedUpdate()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }

        if (chaserTransform != null)
        {
            float distance = Vector2.Distance(transform.position, chaserTransform.position);
            totalDistanceFromChaser += distance;
            distanceSampleCount++;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position.x / 21f);
        sensor.AddObservation(transform.position.y / 21f);

        if (chaserTransform != null)
        {
            Vector2 toChaserDirection = (chaserTransform.position - transform.position).normalized;
            sensor.AddObservation(toChaserDirection.x);
            sensor.AddObservation(toChaserDirection.y);
            float distance = Vector2.Distance(transform.position, chaserTransform.position);
            sensor.AddObservation(distance / 30f);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        int[,] maze = envGenerator.GetMaze();
        int px = Mathf.RoundToInt(transform.position.x);
        int py = Mathf.RoundToInt(transform.position.y);
        int visionRange = 3;

        for (int dy = visionRange; dy >= -visionRange; dy--)
        {
            for (int dx = -visionRange; dx <= visionRange; dx++)
            {
                int x = px + dx;
                int y = py + dy;
                bool isWall = (x < 0 || y < 0 || x >= maze.GetLength(0) || y >= maze.GetLength(1)) || maze[x, y] == 1;
                sensor.AddObservation(isWall ? 1f : 0f);
            }
        }

        sensor.AddObservation(isMoving ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isMoving)
        {
            int action = actions.DiscreteActions[0];
            Vector3 direction = GetDirection(action);

            if (action == 0)
            {
                AddReward(-0.015f);
            }
            else if (CheckWall(direction))
            {
                AddReward(-0.02f);
            }
            else
            {
                targetPosition = transform.position + direction;
                isMoving = true;
            }
        }

        if (chaserTransform != null)
        {
            float distNow = Vector2.Distance(transform.position, chaserTransform.position);

            AddReward(0.05f * (float)System.Math.Tanh(distNow - lastDistance));
            lastDistance = distNow;

            float safeZone = 8f;
            float shaping = 0.01f * (float)System.Math.Tanh((distNow - safeZone) / 3f);
            float linear = 0.001f * (distNow - safeZone);
            AddReward(shaping + linear);
        }

        AddReward(0.004f);
    }

    private Vector3 GetDirection(int action)
    {
        switch (action)
        {
            case 0: return Vector3.zero;
            case 1: return Vector3.up;
            case 2: return Vector3.down;
            case 3: return Vector3.left;
            case 4: return Vector3.right;
            default: return Vector3.zero;
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (CheckWall(Vector3.up)) actionMask.SetActionEnabled(0, 1, false);
        if (CheckWall(Vector3.down)) actionMask.SetActionEnabled(0, 2, false);
        if (CheckWall(Vector3.left)) actionMask.SetActionEnabled(0, 3, false);
        if (CheckWall(Vector3.right)) actionMask.SetActionEnabled(0, 4, false);
    }

    private bool CheckWall(Vector3 direction)
    {
        Vector3 checkPosition = transform.position + direction;
        int[,] maze = envGenerator.GetMaze();
        int x = Mathf.RoundToInt(checkPosition.x);
        int y = Mathf.RoundToInt(checkPosition.y);
        if (x < 0 || x >= maze.GetLength(0) || y < 0 || y >= maze.GetLength(1))
            return true;
        return maze[x, y] == 1;
    }

    private void RecordGlobalStats(string reason)
    {
        var stats = Academy.Instance.StatsRecorder;
        float survivalTime = Time.time - episodeStartTime;
        float avgDistance = totalDistanceFromChaser / Mathf.Max(1, distanceSampleCount);
        float totalReward = GetCumulativeReward();

        stats.Add("Target/SurvivalTime", survivalTime);
        stats.Add("Target/AvgDistance", avgDistance);
        stats.Add("Target/TotalReward", totalReward);
        stats.Add("Target/Caught", reason == "caught" ? 1 : 0);
        stats.Add("Target/Timeout", reason == "timeout" ? 1 : 0);
    }

    public void OnCaught()
    {
        float survivalTime = Time.time - episodeStartTime;
        float t = Mathf.Clamp01((survivalTime - 5f) / 15f);
        float deathPenalty = Mathf.Lerp(-5f, -0.2f, t);
        AddReward(deathPenalty);

        RecordGlobalStats("caught");
        EndEpisode();
    }

    public void EndByTimeout()
    {
        RecordGlobalStats("timeout");
        EndEpisode();
    }

    public void OnTimeReward(float reward)
    {
        AddReward(reward);
    }

    public void SyncAfterReset()
    {
        targetPosition = transform.position;
        isMoving = false;

        if (chaserTransform != null)
            lastDistance = Vector2.Distance(transform.position, chaserTransform.position);
        else
            lastDistance = 0f;

        totalDistanceFromChaser = 0f;
        distanceSampleCount = 0;
    }
}
