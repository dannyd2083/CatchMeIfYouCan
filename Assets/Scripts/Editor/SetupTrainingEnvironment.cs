using UnityEngine;
using UnityEditor;

public class SetupTrainingEnvironment : EditorWindow
{
    [MenuItem("Tools/Setup Training Environment")]
    public static void Setup()
    {
        GameObject environment = GameObject.Find("Environment");
        if (environment == null)
        {
            environment = new GameObject("Environment");
            Debug.Log("Created Environment object");
        }
        
        EnvironmentGenerator envGen = environment.GetComponent<EnvironmentGenerator>();
        if (envGen == null)
        {
            envGen = environment.AddComponent<EnvironmentGenerator>();
            Debug.Log("Added EnvironmentGenerator component");
        }
        
        TrainingEnvironment trainEnv = environment.GetComponent<TrainingEnvironment>();
        if (trainEnv == null)
        {
            trainEnv = environment.AddComponent<TrainingEnvironment>();
            Debug.Log("Added TrainingEnvironment component");
        }
        
        GameObject target = GameObject.Find("Target");
        if (target == null)
        {
            target = new GameObject("Target");
            target.transform.position = new Vector3(19, 19, 0);
            
            SpriteRenderer targetRenderer = target.AddComponent<SpriteRenderer>();
            targetRenderer.color = Color.red;
            targetRenderer.sprite = CreateSquareSprite();
            
            Debug.Log("Created Target object (red square)");
        }
        
        GameObject chaser = GameObject.Find("Chaser");
        if (chaser == null)
        {
            chaser = new GameObject("Chaser");
            chaser.transform.position = new Vector3(1, 1, 0);
            
            SpriteRenderer chaserRenderer = chaser.AddComponent<SpriteRenderer>();
            chaserRenderer.color = Color.blue;
            chaserRenderer.sprite = CreateSquareSprite();
            
            Debug.Log("Created Chaser object (blue square)");
        }
        
        EditorUtility.DisplayDialog("Setup Complete", 
            "Training environment setup completed!\n\n" +
            "Now you can:\n" +
            "1. Press play to test (use arrow keys to control red square)\n" +
            "2. Or start ML-Agents training", 
            "OK");
    }
    
    static Sprite CreateSquareSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }
}

