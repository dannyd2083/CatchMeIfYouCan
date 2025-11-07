using UnityEngine;
using Unity.MLAgents;

public class TrainingEnvironment : MonoBehaviour
{
    [Header("Training Settings")]
    [SerializeField] private float maxEpisodeTime = 30f;
    [SerializeField] private float timeRewardInterval = 10f;
    
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
                {
                    targetAgent = target.AddComponent<TargetAgent>();
                }
            }
        }
        
        if (chaserAI == null)
        {
            GameObject chaser = GameObject.Find("Chaser");
            if (chaser != null)
            {
                chaserAI = chaser.GetComponent<ChaserAI>();
                if (chaserAI == null)
                {
                    chaserAI = chaser.AddComponent<ChaserAI>();
                }
            }
        }
        
        if (environmentGenerator == null)
        {
            environmentGenerator = FindObjectOfType<EnvironmentGenerator>();
        }
        
        ResetEnvironment();
    }
    
    void FixedUpdate()
    {
        if (episodeEnded) return;
        
        episodeTimer += Time.fixedDeltaTime;
        
        float timeThreshold = timeRewardInterval * timeRewardMultiplier;
        if (episodeTimer >= timeThreshold)
        {
            float reward = 0.5f * timeRewardMultiplier;
            if (targetAgent != null)
            {
                targetAgent.OnTimeReward(reward);
            }
            
            timeRewardMultiplier++;
            
            Debug.Log($"Time reward! Survived {episodeTimer:F1} seconds, gained {reward} reward");
        }
        
        if (episodeTimer >= maxEpisodeTime)
        {
            OnEpisodeSuccess();
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
    
    void OnEpisodeSuccess()
    {
        if (episodeEnded) return;
        
        episodeEnded = true;
        
        Debug.Log($"Success! Target survived {maxEpisodeTime} seconds!");
        
        if (targetAgent != null)
        {
            targetAgent.AddReward(2.0f);
            targetAgent.EndEpisode();
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
            environmentGenerator.RegenerateMaze();
            Debug.Log("New maze generated");
        }
        
        if (environmentGenerator != null)
        {
            Vector3 targetStartPos = new Vector3(environmentGenerator.width - 2, environmentGenerator.height - 2, 0);
            Vector3 chaserStartPos = new Vector3(1, 1, 0);
            
            if (targetAgent != null)
            {
                targetAgent.transform.position = targetStartPos;
            }
            
            if (chaserAI != null)
            {
                chaserAI.ResetAI(chaserStartPos);
            }
        }
        else
        {
            if (targetAgent != null)
            {
                targetAgent.transform.position = new Vector3(19, 19, 0);
            }
            
            if (chaserAI != null)
            {
                chaserAI.ResetAI(new Vector3(1, 1, 0));
            }
        }
        
        Debug.Log("Environment reset");
    }
    
    void OnGUI()
    {
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        
        float y = 10;
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

