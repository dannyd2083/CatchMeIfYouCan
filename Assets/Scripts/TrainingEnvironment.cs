using UnityEngine;
using Unity.MLAgents;

public enum TrainingMode
{
    TrainTarget,
    TrainChaser,
    TrainTargetUsingChaserAgent
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
    [SerializeField] private TargetAgent_Ablation_NoDist targetAgentNoDist;
    [SerializeField] private TargetAgent_Ablation_NoTurn targetAgentNoTurn;
    [SerializeField] private ChaserAI chaserAI;
    [SerializeField] private ChaserAgent chaserAgent;
    [SerializeField] private ChaserAgent_Ablation_NoDist chaserAgentNoDist;
    [SerializeField] private ChaserAgent_Ablation_NoBFS chaserAgentNoBFS;
    [SerializeField] private EnvironmentGenerator environmentGenerator;

    [Header("Catch Settings")]
    [SerializeField] private float catchRadius = 1f;

    private float episodeTimer = 0f;    
    private int timeRewardMultiplier = 1;
    private bool episodeEnded = false;
    private Transform targetTransform;

    void Start()
    {
        GameObject target = GameObject.Find("Target");
        if (target != null)
        {
            targetAgent = target.GetComponent<TargetAgent>();
            targetAgentNoDist = target.GetComponent<TargetAgent_Ablation_NoDist>();
            targetAgentNoTurn = target.GetComponent<TargetAgent_Ablation_NoTurn>();
            
            targetTransform = target.transform;
            
            string agentType = targetAgent != null ? "TargetAgent" :
                              targetAgentNoDist != null ? "TargetAgent_Ablation_NoDist" :
                              targetAgentNoTurn != null ? "TargetAgent_Ablation_NoTurn" : "None";
            Debug.Log($"[TrainingEnvironment] Detected TargetAgent: {agentType}");
        }

        GameObject chaser = GameObject.Find("Chaser");
        if (chaser != null)
        {
            if (trainingMode == TrainingMode.TrainTarget)
            {
                chaserAI = chaser.GetComponent<ChaserAI>();
                if (chaserAI == null)
                    chaserAI = chaser.AddComponent<ChaserAI>();
            }
            else if (trainingMode == TrainingMode.TrainChaser || 
                     trainingMode == TrainingMode.TrainTargetUsingChaserAgent)
            {
                chaserAgent = chaser.GetComponent<ChaserAgent>();
                chaserAgentNoDist = chaser.GetComponent<ChaserAgent_Ablation_NoDist>();
                chaserAgentNoBFS = chaser.GetComponent<ChaserAgent_Ablation_NoBFS>();
                
                string chaserType = chaserAgent != null ? "ChaserAgent" :
                                   chaserAgentNoDist != null ? "ChaserAgent_Ablation_NoDist" :
                                   chaserAgentNoBFS != null ? "ChaserAgent_Ablation_NoBFS" : "None";
                Debug.Log($"[TrainingEnvironment] Detected ChaserAgent: {chaserType}");
                
                chaserAI = null;
            }
        }

        if (environmentGenerator == null)
        {
            environmentGenerator = FindObjectOfType<EnvironmentGenerator>();
        }

        mapRng = new System.Random(42);

        if (environmentGenerator != null)
        {
            int mapIndex = 0;//mapRng.Next(0, totalTrainingMaps);
            environmentGenerator.SwitchToMap(mapIndex);
        }

        Debug.Log($"[TrainingEnvironment] Mode: {trainingMode}");
    }

    void FixedUpdate()
    {
        if (episodeEnded) return;

        episodeTimer += Time.fixedDeltaTime;

        if (trainingMode == TrainingMode.TrainTarget || 
            trainingMode == TrainingMode.TrainTargetUsingChaserAgent)
        {
            float timeThreshold = timeRewardInterval * timeRewardMultiplier;
            if (episodeTimer >= timeThreshold)
            {
                float reward = 0.8f * Mathf.Exp(-timeRewardDecay * timeRewardMultiplier);

                if (targetAgent != null)
                    targetAgent.OnTimeReward(reward);
                else if (targetAgentNoDist != null)
                    targetAgentNoDist.OnTimeReward(reward);
                else if (targetAgentNoTurn != null)
                    targetAgentNoTurn.OnTimeReward(reward);

                timeRewardMultiplier++;
            }
        }

        if (trainingMode == TrainingMode.TrainChaser)
        {
            CheckCatch();
        }

        if (trainingMode == TrainingMode.TrainTargetUsingChaserAgent)
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
        if (targetTransform == null) return;

        Transform chaserTransform = GetActiveChaserTransform();
        if (chaserTransform == null) return;

        float distance = Vector2.Distance(
            chaserTransform.position,
            targetTransform.position
        );

        if (distance <= catchRadius)
        {
            OnTargetCaught();
        }
    }

    private Transform GetActiveChaserTransform()
    {
        if (chaserAgent != null) return chaserAgent.transform;
        if (chaserAgentNoDist != null) return chaserAgentNoDist.transform;
        if (chaserAgentNoBFS != null) return chaserAgentNoBFS.transform;
        return null;
    }

    public void OnTargetCaught()
    {
        if (episodeEnded) return;

        episodeEnded = true;

        if (targetAgent != null)
            targetAgent.OnCaught();
        else if (targetAgentNoDist != null)
            targetAgentNoDist.OnCaught();
        else if (targetAgentNoTurn != null)
            targetAgentNoTurn.OnCaught();

        if (trainingMode == TrainingMode.TrainChaser)
        {
            if (chaserAgent != null)
                chaserAgent.OnCatchTarget();
            else if (chaserAgentNoDist != null)
                chaserAgentNoDist.OnCatchTarget();
            else if (chaserAgentNoBFS != null)
                chaserAgentNoBFS.OnCatchTarget();
        }

        Invoke(nameof(ResetEnvironment), 0.5f);
    }

    void OnEpisodeTimeout()
    {
        if (episodeEnded) return;

        episodeEnded = true;

        bool isTrainTarget = trainingMode == TrainingMode.TrainTarget ||
                             trainingMode == TrainingMode.TrainTargetUsingChaserAgent;

        if (targetAgent != null)
        {
            if (isTrainTarget) targetAgent.AddReward(1.0f);
            targetAgent.EndByTimeout();
        }
        else if (targetAgentNoDist != null)
        {
            if (isTrainTarget) targetAgentNoDist.AddReward(1.0f);
            targetAgentNoDist.EndByTimeout();
        }
        else if (targetAgentNoTurn != null)
        {
            if (isTrainTarget) targetAgentNoTurn.AddReward(1.0f);
            targetAgentNoTurn.EndByTimeout();
        }

        if (trainingMode == TrainingMode.TrainChaser)
        {
            if (chaserAgent != null)
                chaserAgent.OnTimeout();
            else if (chaserAgentNoDist != null)
                chaserAgentNoDist.OnTimeout();
            else if (chaserAgentNoBFS != null)
                chaserAgentNoBFS.OnTimeout();
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
            int mapIndex = 0;//mapRng.Next(0, totalTrainingMaps);
            environmentGenerator.SwitchToMap(mapIndex);
        }

        if (targetAgent != null)
            targetAgent.SyncAfterReset();
        else if (targetAgentNoDist != null)
            targetAgentNoDist.SyncAfterReset();
        else if (targetAgentNoTurn != null)
            targetAgentNoTurn.SyncAfterReset();

        if (trainingMode == TrainingMode.TrainTarget)
        {
            if (chaserAI != null)
                chaserAI.ResetAI(chaserAI.transform.position);
        }
        else if (trainingMode == TrainingMode.TrainChaser || 
                 trainingMode == TrainingMode.TrainTargetUsingChaserAgent)
        {
            if (chaserAgent != null)
                chaserAgent.SyncAfterReset();
            else if (chaserAgentNoDist != null)
                chaserAgentNoDist.SyncAfterReset();
            else if (chaserAgentNoBFS != null)
                chaserAgentNoBFS.SyncAfterReset();
        }

        if (environmentGenerator == null)
        {
            if (targetTransform != null)
                targetTransform.position = new Vector3(19, 19, 0);

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

        string modeStr =
            trainingMode == TrainingMode.TrainTarget ? "=== TRAIN TARGET ===" :
            trainingMode == TrainingMode.TrainChaser ? "=== TRAIN CHASER ===" :
            "=== TRAIN TARGET (RL CHASER) ===";
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

        if (trainingMode == TrainingMode.TrainTarget ||
            trainingMode == TrainingMode.TrainTargetUsingChaserAgent)
        {
            float nextReward = (timeRewardInterval * timeRewardMultiplier) - episodeTimer;
            if (nextReward > 0)
            {
                GUI.Label(new Rect(10, y, 300, 30), 
                    $"Next Reward: {nextReward:F1}s", style);
                y += 30;
            }
        }

        Transform chaserTransform = null;
        if (trainingMode == TrainingMode.TrainTarget && chaserAI != null)
            chaserTransform = chaserAI.transform;
        else
            chaserTransform = GetActiveChaserTransform();

        if (targetTransform != null && chaserTransform != null)
        {
            float distance = Vector2.Distance(targetTransform.position, chaserTransform.position);
            GUI.Label(new Rect(10, y, 300, 30), 
                $"Distance: {distance:F1}", style);
        }
    }
#endif
}
