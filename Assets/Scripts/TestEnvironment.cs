using UnityEngine;
using Unity.MLAgents;
using System.Collections.Generic;

public enum TestMode
{
    TestTarget,   // Target(RL) vs ChaserAI(script)
    TestChaser    // Target(RL) vs ChaserAgent(RL)
}

public class TestEnvironment : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private int[] testMapIndices = new int[] {50, 51, 52, 53, 54, 55, 56, 57, 58, 59};
    [SerializeField] private int episodesPerMap = 100;
    [SerializeField] private float maxEpisodeTime = 30f;

    [Header("Test Mode")]
    [SerializeField] private TestMode testMode = TestMode.TestTarget;

    [Header("References")]
    [SerializeField] private TargetAgent targetAgent;
    [SerializeField] private ChaserAI chaserAI;         // 用于 TestTarget
    [SerializeField] private ChaserAgent chaserAgent;   // 用于 TestChaser
    [SerializeField] private EnvironmentGenerator environmentGenerator;

    private int currentTestMapIdx = 0;
    private int currentEpisodeCount = 0;
    private Dictionary<int, TestResults> results = new Dictionary<int, TestResults>();
    
    private float episodeTimer = 0f;
    private bool episodeEnded = false;
    private bool testingComplete = false;

    private float episodeStartTime = 0f;
    private float totalDistanceThisEpisode = 0f;
    private int distanceSampleCount = 0;

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

        GameObject chaserObj = GameObject.Find("Chaser");

        if (testMode == TestMode.TestTarget)
        {
            // 原模式：Target RL + Chaser 脚本 AI
            if (chaserAI == null && chaserObj != null)
                chaserAI = chaserObj.GetComponent<ChaserAI>();
            chaserAgent = null;
        }
        else // TestChaser
        {
            // 新模式：Target RL + Chaser RL
            if (chaserAgent == null && chaserObj != null)
                chaserAgent = chaserObj.GetComponent<ChaserAgent>();
            chaserAI = null;
        }

        if (environmentGenerator == null)
        {
            environmentGenerator = FindObjectOfType<EnvironmentGenerator>();
        }

        foreach (int mapIdx in testMapIndices)
        {
            results[mapIdx] = new TestResults();
        }

        StartTestingMap(testMapIndices[0]);
    }

    void FixedUpdate()
    {
        if (testingComplete || episodeEnded) return;

        episodeTimer += Time.fixedDeltaTime;

        // 根据当前模式选择使用哪个 chaser 的位置
        if (targetAgent != null)
        {
            Transform chaserTf = null;
            if (testMode == TestMode.TestTarget && chaserAI != null)
                chaserTf = chaserAI.transform;
            else if (testMode == TestMode.TestChaser && chaserAgent != null)
                chaserTf = chaserAgent.transform;

            if (chaserTf != null)
            {
                float distance = Vector2.Distance(targetAgent.transform.position, chaserTf.position);
                totalDistanceThisEpisode += distance;
                distanceSampleCount++;
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            OnEpisodeTimeout();
        }
    }

    void StartTestingMap(int mapIndex)
    {
        Debug.Log($"========== Starting test on Map {mapIndex} ==========");
        
        if (environmentGenerator != null)
        {
            environmentGenerator.SwitchToMap(mapIndex);
        }

        currentEpisodeCount = 0;
        ResetEpisode();
    }

    public void OnTargetCaught()
    {
        if (episodeEnded || testingComplete) return;

        episodeEnded = true;
        float survivalTime = episodeTimer;

        int currentMapIndex = testMapIndices[currentTestMapIdx];
        results[currentMapIndex].caught++;
        results[currentMapIndex].totalSurvivalTime += survivalTime;
        
        float avgDist = distanceSampleCount > 0 ? totalDistanceThisEpisode / distanceSampleCount : 0;
        results[currentMapIndex].totalAvgDistance += avgDist;

        RecordEpisodeStats(survivalTime, avgDist, wasCaught: true);

        Debug.Log($"Map {currentMapIndex} - Episode {currentEpisodeCount + 1}/{episodesPerMap}: CAUGHT at {survivalTime:F2}s, AvgDist: {avgDist:F2}");

        // 如果在测试 Chaser 模式，给 ChaserAgent 奖励
        if (testMode == TestMode.TestChaser && chaserAgent != null)
        {
            chaserAgent.OnCatchTarget();
        }

        if (targetAgent != null)
        {
            targetAgent.EndEpisode();
        }

        Invoke(nameof(OnEpisodeComplete), 0.5f);
    }

    void OnEpisodeTimeout()
    {
        if (episodeEnded || testingComplete) return;

        episodeEnded = true;
        float survivalTime = episodeTimer;

        int currentMapIndex = testMapIndices[currentTestMapIdx];
        results[currentMapIndex].timeouts++;
        results[currentMapIndex].totalSurvivalTime += survivalTime;
        
        float avgDist = distanceSampleCount > 0 ? totalDistanceThisEpisode / distanceSampleCount : 0;
        results[currentMapIndex].totalAvgDistance += avgDist;

        RecordEpisodeStats(survivalTime, avgDist, wasCaught: false);

        Debug.Log($"Map {currentMapIndex} - Episode {currentEpisodeCount + 1}/{episodesPerMap}: TIMEOUT at {survivalTime:F2}s, AvgDist: {avgDist:F2}");

        // 测试 Chaser 模式时给 ChaserAgent 惩罚
        if (testMode == TestMode.TestChaser && chaserAgent != null)
        {
            chaserAgent.OnTimeout();
        }

        if (targetAgent != null)
        {
            targetAgent.EndEpisode();
        }

        Invoke(nameof(OnEpisodeComplete), 0.5f);
    }

    void OnEpisodeComplete()
    {
        currentEpisodeCount++;

        if (currentEpisodeCount >= episodesPerMap)
        {
            PrintMapResults(testMapIndices[currentTestMapIdx]);

            currentTestMapIdx++;

            if (currentTestMapIdx >= testMapIndices.Length)
            {
                OnAllTestsComplete();
                return;
            }
            else
            {
                StartTestingMap(testMapIndices[currentTestMapIdx]);
            }
        }
        else
        {
            ResetEpisode();
        }
    }

    void ResetEpisode()
    {
        episodeTimer = 0f;
        episodeEnded = false;
        episodeStartTime = Time.time;
        totalDistanceThisEpisode = 0f;
        distanceSampleCount = 0;

        if (environmentGenerator != null)
        {
            environmentGenerator.ResetPlayerPositions();

            if (targetAgent != null)
                targetAgent.SyncAfterReset();

            // 原来的脚本 Chaser 重置逻辑（仅 TestTarget 时使用）
            if (testMode == TestMode.TestTarget && chaserAI != null)
            {
                chaserAI.ResetAI(chaserAI.transform.position);
            }

            // 新增：测试 Chaser 时使用 RL Chaser 的同步
            if (testMode == TestMode.TestChaser && chaserAgent != null)
            {
                chaserAgent.SyncAfterReset();
            }
        }
    }
    void PrintMapResults(int mapIndex)
    {
        TestResults res = results[mapIndex];
        int totalEpisodes = res.timeouts + res.caught;
        float timeoutRate = (float)res.timeouts / totalEpisodes * 100f;
        float caughtRate = (float)res.caught / totalEpisodes * 100f;
        float avgSurvival = res.totalSurvivalTime / totalEpisodes;
        float avgDistance = res.totalAvgDistance / totalEpisodes;

        Debug.Log($"\n========== Map {mapIndex} Results ==========");
        Debug.Log($"Total Episodes: {totalEpisodes}");
        Debug.Log($"Timeouts: {res.timeouts} ({timeoutRate:F2}%)");
        Debug.Log($"Caught: {res.caught} ({caughtRate:F2}%)");
        Debug.Log($"Avg Survival Time: {avgSurvival:F2}s");
        Debug.Log($"Avg Distance: {avgDistance:F2}");
        Debug.Log($"==========================================\n");
    }

    private void RecordEpisodeStats(float survivalTime, float avgDistance, bool wasCaught)
    {
        var stats = Academy.Instance.StatsRecorder;

        // 这里仍然记录到 Target 下（保持和你原来的逻辑一致）
        // 如果你之后想拆成 Chaser/* 和 Target/* 可以再调。
        stats.Add("Target/SurvivalTime", survivalTime);
        stats.Add("Target/AvgDistance", avgDistance);
        stats.Add("Target/Timeout", wasCaught ? 0f : 1f);
        stats.Add("Target/Caught", wasCaught ? 1f : 0f);
    }

    void OnAllTestsComplete()
    {
        testingComplete = true;

        Debug.Log("\n\n========== ALL TESTS COMPLETE ==========");
        
        int totalTimeouts = 0;
        int totalCaught = 0;
        float totalAvgSurvival = 0f;
        float totalAvgDistance = 0f;

        foreach (int mapIdx in testMapIndices)
        {
            TestResults res = results[mapIdx];
            totalTimeouts += res.timeouts;
            totalCaught += res.caught;
            
            int episodes = res.timeouts + res.caught;
            totalAvgSurvival += res.totalSurvivalTime / episodes;
            totalAvgDistance += res.totalAvgDistance / episodes;
        }

        int grandTotal = totalTimeouts + totalCaught;
        float overallTimeoutRate = (float)totalTimeouts / grandTotal * 100f;
        float overallCaughtRate = (float)totalCaught / grandTotal * 100f;
        float overallAvgSurvival = totalAvgSurvival / testMapIndices.Length;
        float overallAvgDistance = totalAvgDistance / testMapIndices.Length;

        Debug.Log($"========== OVERALL RESULTS ==========");
        Debug.Log($"Total Episodes: {grandTotal}");
        Debug.Log($"Overall Timeout Rate: {overallTimeoutRate:F2}%");
        Debug.Log($"Overall Caught Rate: {overallCaughtRate:F2}%");
        Debug.Log($"Overall Avg Survival: {overallAvgSurvival:F2}s");
        Debug.Log($"Overall Avg Distance: {overallAvgDistance:F2}");
        Debug.Log($"=====================================\n");

        SaveResultsToFile();

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    void SaveResultsToFile()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"TestResults_{timestamp}.txt";
        string path = System.IO.Path.Combine(Application.dataPath, "..", "TestResults", filename);
        
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "..", "TestResults"));

        using (System.IO.StreamWriter writer = new System.IO.StreamWriter(path))
        {
            writer.WriteLine("========== TEST RESULTS ==========");
            writer.WriteLine($"Test Date: {System.DateTime.Now}");
            writer.WriteLine($"Episodes per map: {episodesPerMap}");
            writer.WriteLine($"Test maps: {string.Join(", ", testMapIndices)}");
            writer.WriteLine();

            foreach (int mapIdx in testMapIndices)
            {
                TestResults res = results[mapIdx];
                int total = res.timeouts + res.caught;
                float timeoutRate = (float)res.timeouts / total * 100f;
                float avgSurvival = res.totalSurvivalTime / total;
                float avgDistance = res.totalAvgDistance / total;

                writer.WriteLine($"Map {mapIdx}:");
                writer.WriteLine($"  Timeout: {res.timeouts}/{total} ({timeoutRate:F2}%)");
                writer.WriteLine($"  Caught: {res.caught}/{total} ({(100 - timeoutRate):F2}%)");
                writer.WriteLine($"  Avg Survival: {avgSurvival:F2}s");
                writer.WriteLine($"  Avg Distance: {avgDistance:F2}");
                writer.WriteLine();
            }

            int totalTimeouts = 0;
            int totalCaught = 0;
            float totalAvgSurvival = 0f;
            float totalAvgDistance = 0f;

            foreach (int mapIdx in testMapIndices)
            {
                TestResults res = results[mapIdx];
                totalTimeouts += res.timeouts;
                totalCaught += res.caught;
                int episodes = res.timeouts + res.caught;
                totalAvgSurvival += res.totalSurvivalTime / episodes;
                totalAvgDistance += res.totalAvgDistance / episodes;
            }

            int grandTotal = totalTimeouts + totalCaught;
            writer.WriteLine("========== OVERALL ==========");
            writer.WriteLine($"Total Episodes: {grandTotal}");
            writer.WriteLine($"Overall Timeout Rate: {(float)totalTimeouts / grandTotal * 100f:F2}%");
            writer.WriteLine($"Overall Avg Survival: {totalAvgSurvival / testMapIndices.Length:F2}s");
            writer.WriteLine($"Overall Avg Distance: {totalAvgDistance / testMapIndices.Length:F2}");
        }

        Debug.Log($"Results saved to: {path}");
    }

    void OnGUI()
    {
        if (testingComplete) return;

        GUI.color = Color.white;
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        float y = 10;

        int currentMapIndex = testMapIndices[currentTestMapIdx];
        GUI.Label(new Rect(10, y, 400, 30), $"Test Mode: {testMode}", style);
        y += 30;

        GUI.Label(new Rect(10, y, 400, 30), $"Testing Map {currentMapIndex}", style);
        y += 30;

        GUI.Label(new Rect(10, y, 400, 30), $"Episode: {currentEpisodeCount}/{episodesPerMap}", style);
        y += 30;

        GUI.Label(new Rect(10, y, 300, 30), $"Survival Time: {episodeTimer:F1}s", style);
        y += 30;

        Transform chaserTf = null;
        if (testMode == TestMode.TestTarget && chaserAI != null)
            chaserTf = chaserAI.transform;
        else if (testMode == TestMode.TestChaser && chaserAgent != null)
            chaserTf = chaserAgent.transform;

        if (targetAgent != null && chaserTf != null)
        {
            float distance = Vector2.Distance(targetAgent.transform.position, chaserTf.position);
            GUI.Label(new Rect(10, y, 300, 30), $"Distance: {distance:F1}", style);
        }
    }
}

public class TestResults
{
    public int timeouts = 0;
    public int caught = 0;
    public float totalSurvivalTime = 0f;
    public float totalAvgDistance = 0f;
}
