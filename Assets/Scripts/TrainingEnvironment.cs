using UnityEngine;
using Unity.MLAgents;

public enum TrainingMode
{
    TrainTarget,    // 训练Target，Chaser用脚本AI
    TrainChaser     // 训练Chaser，Target用训练好的模型
}

public class TrainingEnvironment : MonoBehaviour
{
    [Header("Training Mode")]
    [SerializeField] private TrainingMode trainingMode = TrainingMode.TrainTarget;

    [Header("Training Settings")]
    [SerializeField] private float maxEpisodeTime = 30f;
    [SerializeField] private float timeRewardInterval = 10f;
    [SerializeField] private float timeRewardDecay = 0.05f;

    [Header("Multi-Map Training")]
    [SerializeField] private int totalTrainingMaps = 50;
    private System.Random mapRng;

    [Header("References")]
    [SerializeField] private TargetAgent targetAgent;
    [SerializeField] private ChaserAI chaserAI;           // TrainTarget模式用
    [SerializeField] private ChaserAgent chaserAgent;     // TrainChaser模式用
    [SerializeField] private EnvironmentGenerator environmentGenerator;

    [Header("Catch Settings")]
    [SerializeField] private float catchRadius = 1f;

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

        // 根据模式查找对应的Chaser组件
        GameObject chaser = GameObject.Find("Chaser");
        if (chaser != null)
        {
            if (trainingMode == TrainingMode.TrainTarget)
            {
                chaserAI = chaser.GetComponent<ChaserAI>();
                if (chaserAI == null)
                    chaserAI = chaser.AddComponent<ChaserAI>();
            }
            else // TrainChaser
            {
                chaserAgent = chaser.GetComponent<ChaserAgent>();
                if (chaserAgent == null)
                    chaserAgent = chaser.AddComponent<ChaserAgent>();
            }
        }

        if (environmentGenerator == null)
        {
            environmentGenerator = FindObjectOfType<EnvironmentGenerator>();
        }

        // 初始化地图随机选择器（固定种子42保证可复现）
        mapRng = new System.Random(42);
        
        // 首次随机选择训练地图
        if (environmentGenerator != null)
        {
            // int fixedMap = 0;
            // environmentGenerator.SwitchToMap(fixedMap);

            int mapIndex = mapRng.Next(0, totalTrainingMaps);
            environmentGenerator.SwitchToMap(mapIndex);
        }

        Debug.Log($"[TrainingEnvironment] Mode: {trainingMode}");
    }

    void FixedUpdate()
    {
        if (episodeEnded) return;

        episodeTimer += Time.fixedDeltaTime;

        // 时间奖励（只在TrainTarget模式下给Target）
        if (trainingMode == TrainingMode.TrainTarget)
        {
            float timeThreshold = timeRewardInterval * timeRewardMultiplier;
            if (episodeTimer >= timeThreshold)
            {
                float reward = 0.8f * Mathf.Exp(-timeRewardDecay * timeRewardMultiplier);

                if (targetAgent != null)
                    targetAgent.OnTimeReward(reward);

                timeRewardMultiplier++;
            }
        }

        // 检查抓住（TrainChaser模式需要自己检测）
        if (trainingMode == TrainingMode.TrainChaser)
        {
            CheckCatch();
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            OnEpisodeTimeout();
        }
    }

    void CheckCatch()
    {
        if (targetAgent == null || chaserAgent == null) return;

        float distance = Vector2.Distance(
            chaserAgent.transform.position,
            targetAgent.transform.position
        );

        if (distance <= catchRadius)
        {
            OnTargetCaught();
        }
    }

    public void OnTargetCaught()
    {
        if (episodeEnded) return;

        episodeEnded = true;

        if (targetAgent != null)
        {
            targetAgent.OnCaught();
        }

        // TrainChaser模式：给Chaser奖励
        if (trainingMode == TrainingMode.TrainChaser && chaserAgent != null)
        {
            chaserAgent.OnCatchTarget();
        }

        Invoke(nameof(ResetEnvironment), 0.5f);
    }

    void OnEpisodeTimeout()
    {
        if (episodeEnded) return;

        episodeEnded = true;

        if (targetAgent != null)
        {
            if (trainingMode == TrainingMode.TrainTarget)
            {
                targetAgent.AddReward(1.0f);
            }
            targetAgent.EndByTimeout();
        }

        // TrainChaser模式：给Chaser惩罚
        if (trainingMode == TrainingMode.TrainChaser && chaserAgent != null)
        {
            chaserAgent.OnTimeout();
        }

        Invoke(nameof(ResetEnvironment), 0.5f);
    }

    public void ResetEnvironment()
    {
        episodeTimer = 0f;
        timeRewardMultiplier = 1;
        episodeEnded = false;

        if (environmentGenerator != null)
        {
            //environmentGenerator.ResetPlayerPositions();
            int mapIndex = mapRng.Next(0, totalTrainingMaps);
            environmentGenerator.SwitchToMap(mapIndex);
        }

        if (targetAgent != null)
            targetAgent.SyncAfterReset();

        // 根据模式重置对应的Chaser
        if (trainingMode == TrainingMode.TrainTarget)
        {
            if (chaserAI != null)
                chaserAI.ResetAI(chaserAI.transform.position);
        }
        else
        {
            if (chaserAgent != null)
                chaserAgent.SyncAfterReset();
        }

        if (environmentGenerator == null)
        {
            if (targetAgent != null)
                targetAgent.transform.position = new Vector3(19, 19, 0);

            if (trainingMode == TrainingMode.TrainTarget && chaserAI != null)
                chaserAI.ResetAI(new Vector3(1, 1, 0));
            else if (chaserAgent != null)
                chaserAgent.transform.position = new Vector3(1, 1, 0);
        }
    }

    #if UNITY_EDITOR
    void OnGUI()
    {
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        float y = 10;

        // 显示当前模式
        string modeStr = trainingMode == TrainingMode.TrainTarget ? 
            "=== TRAIN TARGET ===" : "=== TRAIN CHASER ===";
        GUI.Label(new Rect(10, y, 400, 30), modeStr, style);
        y += 30;

        if (environmentGenerator != null)
        {
            GUI.Label(new Rect(10, y, 400, 30), 
                $"Training Map: {environmentGenerator.currentMapIndex}", style);
            y += 30;
        }

        GUI.Label(new Rect(10, y, 300, 30), 
            $"Survival Time: {episodeTimer:F1}s", style);
        y += 30;

        float remaining = maxEpisodeTime - episodeTimer;
        GUI.Label(new Rect(10, y, 300, 30), 
            $"Remaining: {remaining:F1}s", style);
        y += 30;

        // 只在TrainTarget模式显示下次奖励时间
        if (trainingMode == TrainingMode.TrainTarget)
        {
            float nextReward = (timeRewardInterval * timeRewardMultiplier) - episodeTimer;
            if (nextReward > 0)
            {
                GUI.Label(new Rect(10, y, 300, 30), 
                    $"Next Reward: {nextReward:F1}s", style);
                y += 30;
            }
        }

        // 显示距离
        Transform chaserTransform = null;
        if (trainingMode == TrainingMode.TrainTarget && chaserAI != null)
            chaserTransform = chaserAI.transform;
        else if (chaserAgent != null)
            chaserTransform = chaserAgent.transform;

        if (targetAgent != null && chaserTransform != null)
        {
            float distance = Vector2.Distance(targetAgent.transform.position, chaserTransform.position);
            GUI.Label(new Rect(10, y, 300, 30), 
                $"Distance: {distance:F1}", style);
        }
    }
    #endif
}