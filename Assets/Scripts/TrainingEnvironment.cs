using UnityEngine;
using Unity.MLAgents;

public class TrainingEnvironment : MonoBehaviour
{
    [Header("Training Settings")]
    [SerializeField] private float maxEpisodeTime = 30f;
    [SerializeField] private float timeRewardInterval = 10f;
    [SerializeField] private float timeRewardDecay = 0.05f;

    [Header("Multi-Map Training")]
    [SerializeField] private int stepsPerMap = 2000000;
    [SerializeField] private int stepsBeforeSwitch = 100000;
    private const int TOTAL_MAPS = 20;
    private int[] mapSteps = new int[TOTAL_MAPS];
    private int currentStepCount = 0;
    private bool shouldSwitchMap = false;

    [Header("References")]
    [SerializeField] private TargetAgent targetAgent;
    [SerializeField] private ChaserAI chaserAI;
    [SerializeField] private EnvironmentGenerator environmentGenerator;

    private float episodeTimer = 0f;
    private int timeRewardMultiplier = 1;
    private bool episodeEnded = false;

    void Start()
    {
        if (targetAgent == null)
        {
            GameObject target = GameObject.Find("Target");
            if (target != null)
            {
                targetAgent = target.GetComponent<TargetAgent>();
                if (targetAgent == null)
                    targetAgent = target.AddComponent<TargetAgent>();
            }
        }

        if (chaserAI == null)
        {
            GameObject chaser = GameObject.Find("Chaser");
            if (chaser != null)
            {
                chaserAI = chaser.GetComponent<ChaserAI>();
                if (chaserAI == null)
                    chaserAI = chaser.AddComponent<ChaserAI>();
            }
        }

        if (environmentGenerator == null)
        {
            environmentGenerator = FindObjectOfType<EnvironmentGenerator>();
        }

        for (int i = 0; i < TOTAL_MAPS; i++)
        {
            mapSteps[i] = 0;
        }
    }

    void FixedUpdate()
    {
        if (episodeEnded) return;

        currentStepCount++;
        CheckMapSwitch();

        episodeTimer += Time.fixedDeltaTime;

        float timeThreshold = timeRewardInterval * timeRewardMultiplier;
        if (episodeTimer >= timeThreshold)
        {
            float reward = 0.8f * Mathf.Exp(-timeRewardDecay * timeRewardMultiplier);

            if (targetAgent != null)
                targetAgent.OnTimeReward(reward);

            timeRewardMultiplier++;

            if (Application.isEditor)
                Debug.Log($"Time reward! Survived {episodeTimer:F1}s, gained {reward:F3}");
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            OnEpisodeTimeout();
        }
    }

    void CheckMapSwitch()
    {
        if (environmentGenerator == null) return;

        int currentMapIndex = environmentGenerator.currentMapIndex;
        mapSteps[currentMapIndex]++;

        if (currentStepCount >= stepsBeforeSwitch)
        {
            shouldSwitchMap = true;
            currentStepCount = 0;
        }

        bool allCompleted = true;
        for (int i = 0; i < TOTAL_MAPS; i++)
        {
            if (mapSteps[i] < stepsPerMap)
            {
                allCompleted = false;
                break;
            }
        }
        if (allCompleted)
        {
            Debug.Log("All maps completed training!");
        }
    }

    public void OnTargetCaught()
    {
        if (episodeEnded) return;

        episodeEnded = true;
        Debug.Log($"Target caught! Survival time: {episodeTimer:F1} seconds");

        if (targetAgent != null)
        {
            targetAgent.OnCaught();
        }

        Invoke(nameof(ResetEnvironment), 0.5f);
    }

    void OnEpisodeTimeout()
    {
        if (episodeEnded) return;

        episodeEnded = true;
        Debug.Log($"Timeout! Target survived full {maxEpisodeTime} seconds!");

        if (targetAgent != null)
        {
            targetAgent.AddReward(1.0f);
            targetAgent.EndByTimeout();
        }

        Invoke(nameof(ResetEnvironment), 0.5f);
    }

    public void ResetEnvironment()
    {
        episodeTimer = 0f;
        timeRewardMultiplier = 1;
        episodeEnded = false;

        if (shouldSwitchMap && environmentGenerator != null)
        {
            int currentMapIndex = environmentGenerator.currentMapIndex;
            int nextMapIndex = (currentMapIndex + 1) % TOTAL_MAPS;
            Debug.Log($"Map {currentMapIndex} completed cycle ({mapSteps[currentMapIndex]} steps total). Switching to Map {nextMapIndex}");
            environmentGenerator.SwitchToMap(nextMapIndex);
            shouldSwitchMap = false;
        }
        else if (environmentGenerator != null)
        {
            environmentGenerator.ResetPlayerPositions();
        }

        if (targetAgent != null)
            targetAgent.SyncAfterReset();

        if (chaserAI != null)
        {
            chaserAI.ResetAI(chaserAI.transform.position);
        }

        if (environmentGenerator == null)
        {
            if (targetAgent != null)
                targetAgent.transform.position = new Vector3(19, 19, 0);

            if (chaserAI != null)
                chaserAI.ResetAI(new Vector3(1, 1, 0));
        }
    }

    void OnGUI()
    {
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        float y = 10;

        if (environmentGenerator != null)
        {
            int currentMap = environmentGenerator.currentMapIndex;
            int steps = mapSteps[currentMap];
            float progress = (float)steps / stepsPerMap * 100f;
            GUI.Label(new Rect(10, y, 400, 30), $"Map {currentMap}: {steps}/{stepsPerMap} ({progress:F1}%)", style);
            y += 30;
        }

        GUI.Label(new Rect(10, y, 300, 30), $"Survival Time: {episodeTimer:F1}s", style);
        y += 30;

        float remaining = maxEpisodeTime - episodeTimer;
        GUI.Label(new Rect(10, y, 300, 30), $"Remaining: {remaining:F1}s", style);
        y += 30;

        float nextReward = (timeRewardInterval * timeRewardMultiplier) - episodeTimer;
        if (nextReward > 0)
        {
            GUI.Label(new Rect(10, y, 300, 30), $"Next Reward: {nextReward:F1}s", style);
        }

        if (targetAgent != null && chaserAI != null)
        {
            float distance = Vector2.Distance(targetAgent.transform.position, chaserAI.transform.position);
            y += 30;
            GUI.Label(new Rect(10, y, 300, 30), $"Distance: {distance:F1}", style);
        }
    }
}