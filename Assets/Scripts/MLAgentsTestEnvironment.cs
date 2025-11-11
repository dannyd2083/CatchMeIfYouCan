using UnityEngine;
using Unity.MLAgents;
using System.Collections.Generic;
using System.IO;

public class MLAgentsTestEnvironment : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private int startTestMapIndex = 100;
    [SerializeField] private int endTestMapIndex = 109;
    [SerializeField] private float maxEpisodeTime = 30f;

    [Header("References")]
    [SerializeField] private TargetAgent targetAgent;
    [SerializeField] private ChaserAI chaserAI;
    [SerializeField] private EnvironmentGenerator environmentGenerator;

    private System.Random testMapRng;
    private Dictionary<int, MLTestResults> results = new Dictionary<int, MLTestResults>();
    
    private float episodeTimer = 0f;
    private bool episodeEnded = false;

    private float totalDistanceThisEpisode = 0f;
    private int distanceSampleCount = 0;
    private int currentMapIndex = 100;

    private int totalEpisodes = 0;
    private const int SAVE_INTERVAL = 100;

    void Start()
    {
        if (targetAgent == null)
        {
            GameObject target = GameObject.Find("Target");
            if (target != null)
            {
                targetAgent = target.GetComponent<TargetAgent>();
            }
        }

        if (chaserAI == null)
        {
            GameObject chaser = GameObject.Find("Chaser");
            if (chaser != null)
            {
                chaserAI = chaser.GetComponent<ChaserAI>();
            }
        }

        if (environmentGenerator == null)
        {
            environmentGenerator = FindObjectOfType<EnvironmentGenerator>();
        }

        testMapRng = new System.Random(123);

        for (int i = startTestMapIndex; i <= endTestMapIndex; i++)
        {
            results[i] = new MLTestResults();
        }

        SelectRandomTestMap();
    }

    void FixedUpdate()
    {
        if (episodeEnded) return;

        episodeTimer += Time.fixedDeltaTime;

        if (targetAgent != null && chaserAI != null)
        {
            float distance = Vector2.Distance(targetAgent.transform.position, chaserAI.transform.position);
            totalDistanceThisEpisode += distance;
            distanceSampleCount++;
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            OnEpisodeTimeout();
        }
    }

    void SelectRandomTestMap()
    {
        currentMapIndex = testMapRng.Next(startTestMapIndex, endTestMapIndex + 1);
        
        if (environmentGenerator != null)
        {
            environmentGenerator.SwitchToMap(currentMapIndex);
        }
    }

    public void OnTargetCaught()
    {
        if (episodeEnded) return;

        episodeEnded = true;
        float survivalTime = episodeTimer;

        results[currentMapIndex].caught++;
        results[currentMapIndex].totalSurvivalTime += survivalTime;
        
        float avgDist = distanceSampleCount > 0 ? totalDistanceThisEpisode / distanceSampleCount : 0;
        results[currentMapIndex].totalAvgDistance += avgDist;

        RecordEpisodeStats(survivalTime, avgDist, wasCaught: true);

        totalEpisodes++;
        
        if (totalEpisodes % SAVE_INTERVAL == 0)
        {
            SaveIntermediateResults();
        }

        if (targetAgent != null)
        {
            targetAgent.EndEpisode();
        }

        ResetEnvironment();
    }

    void OnEpisodeTimeout()
    {
        if (episodeEnded) return;

        episodeEnded = true;
        float survivalTime = episodeTimer;

        results[currentMapIndex].timeouts++;
        results[currentMapIndex].totalSurvivalTime += survivalTime;
        
        float avgDist = distanceSampleCount > 0 ? totalDistanceThisEpisode / distanceSampleCount : 0;
        results[currentMapIndex].totalAvgDistance += avgDist;

        RecordEpisodeStats(survivalTime, avgDist, wasCaught: false);

        totalEpisodes++;
        
        if (totalEpisodes % SAVE_INTERVAL == 0)
        {
            SaveIntermediateResults();
        }

        if (targetAgent != null)
        {
            targetAgent.EndEpisode();
        }

        ResetEnvironment();
    }

    void ResetEnvironment()
    {
        episodeTimer = 0f;
        episodeEnded = false;
        totalDistanceThisEpisode = 0f;
        distanceSampleCount = 0;

        SelectRandomTestMap();

        if (targetAgent != null)
            targetAgent.SyncAfterReset();

        if (chaserAI != null)
        {
            chaserAI.ResetAI(chaserAI.transform.position);
        }
    }

    private void RecordEpisodeStats(float survivalTime, float avgDistance, bool wasCaught)
    {
        var stats = Academy.Instance.StatsRecorder;
        stats.Add("Test/SurvivalTime", survivalTime);
        stats.Add("Test/AvgDistance", avgDistance);
        stats.Add("Test/Timeout", wasCaught ? 0f : 1f);
        stats.Add("Test/Caught", wasCaught ? 1f : 0f);
    }

    void SaveIntermediateResults()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"TestResults_Intermediate_{totalEpisodes}eps_{timestamp}.txt";
        string path = Path.Combine(Application.dataPath, "..", "TestResults", filename);
        
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", "TestResults"));

        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("========== INTERMEDIATE TEST RESULTS ==========");
            writer.WriteLine($"Test Date: {System.DateTime.Now}");
            writer.WriteLine($"Total Episodes So Far: {totalEpisodes}");
            writer.WriteLine($"Test Map Range: {startTestMapIndex}-{endTestMapIndex}");
            writer.WriteLine();

            int totalTimeouts = 0;
            int totalCaught = 0;
            float totalAvgSurvival = 0f;
            float totalAvgDistance = 0f;
            int mapsWithData = 0;

            for (int mapIdx = startTestMapIndex; mapIdx <= endTestMapIndex; mapIdx++)
            {
                MLTestResults res = results[mapIdx];
                int total = res.timeouts + res.caught;
                
                if (total == 0) continue;
                
                mapsWithData++;
                totalTimeouts += res.timeouts;
                totalCaught += res.caught;
                
                float timeoutRate = (float)res.timeouts / total * 100f;
                float avgSurvival = res.totalSurvivalTime / total;
                float avgDistance = res.totalAvgDistance / total;
                
                totalAvgSurvival += avgSurvival;
                totalAvgDistance += avgDistance;

                writer.WriteLine($"Map {mapIdx}: {total} episodes");
                writer.WriteLine($"  Timeout: {res.timeouts} ({timeoutRate:F2}%)");
                writer.WriteLine($"  Caught: {res.caught} ({(100 - timeoutRate):F2}%)");
                writer.WriteLine($"  Avg Survival: {avgSurvival:F2}s");
                writer.WriteLine($"  Avg Distance: {avgDistance:F2}");
                writer.WriteLine();
            }

            int grandTotal = totalTimeouts + totalCaught;
            if (grandTotal > 0)
            {
                writer.WriteLine("========== OVERALL ==========");
                writer.WriteLine($"Total Episodes: {grandTotal}");
                writer.WriteLine($"Overall Timeout Rate: {(float)totalTimeouts / grandTotal * 100f:F2}%");
                writer.WriteLine($"Overall Caught Rate: {(float)totalCaught / grandTotal * 100f:F2}%");
                writer.WriteLine($"Overall Avg Survival: {totalAvgSurvival / mapsWithData:F2}s");
                writer.WriteLine($"Overall Avg Distance: {totalAvgDistance / mapsWithData:F2}");
            }
        }

        Debug.Log($"Intermediate results saved: {path}");
    }

    void OnGUI()
    {
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        float y = 10;

        GUI.Label(new Rect(10, y, 400, 30), $"Testing Map: {currentMapIndex}", style);
        y += 30;

        GUI.Label(new Rect(10, y, 400, 30), $"Total Episodes: {totalEpisodes}", style);
        y += 30;

        GUI.Label(new Rect(10, y, 300, 30), $"Survival Time: {episodeTimer:F1}s", style);
        y += 30;

        if (targetAgent != null && chaserAI != null)
        {
            float distance = Vector2.Distance(targetAgent.transform.position, chaserAI.transform.position);
            GUI.Label(new Rect(10, y, 300, 30), $"Distance: {distance:F1}", style);
        }
    }

    void OnApplicationQuit()
    {
        SaveIntermediateResults();
    }
}

public class MLTestResults
{
    public int timeouts = 0;
    public int caught = 0;
    public float totalSurvivalTime = 0f;
    public float totalAvgDistance = 0f;
}