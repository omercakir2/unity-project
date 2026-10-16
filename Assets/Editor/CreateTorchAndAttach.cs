using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

public static class CreateTorchAndAttach
{
    [MenuItem("Tools/Setup Player Torch (Night)")]
    public static void SetupTorch()
    {
        // 1. Create Torch Prefab
        string torchPath = "Assets/Prefabs/Torch.prefab";
        GameObject torchRoot = new GameObject("Torch");
        torchRoot.transform.position = Vector3.zero;

        // Handle (simple wood cylinder)
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Handle";
        handle.transform.SetParent(torchRoot.transform);
        handle.transform.localPosition = new Vector3(0, -0.6f, 0);
        handle.transform.localRotation = Quaternion.Euler(90, 0, 0);
        handle.transform.localScale = new Vector3(0.08f, 0.7f, 0.08f);
        
        // Simple wood material (dark brown)
        Renderer handleRenderer = handle.GetComponent<Renderer>();
        Material woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        woodMat.color = new Color(0.4f, 0.25f, 0.15f);
        woodMat.SetFloat("_Smoothness", 0.1f);
        handleRenderer.material = woodMat;

        // Flame holder (small sphere or empty for particles)
        GameObject flameHolder = new GameObject("Flame");
        flameHolder.transform.SetParent(torchRoot.transform);
        flameHolder.transform.localPosition = new Vector3(0, 0.4f, 0);

        // Particle System for fire
        ParticleSystem ps = flameHolder.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.1f), new Color(1f, 0.9f, 0.3f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 45;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15;
        shape.radius = 0.05f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.0f), new GradientColorKey(new Color(1f, 0.4f, 0.0f), 0.6f), new GradientColorKey(Color.black, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(0.7f, 0.6f);
        curve.AddKey(1.0f, 0.0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Add Light
        Light torchLight = torchRoot.AddComponent<Light>();
        torchLight.type = LightType.Point;
        torchLight.color = new Color(1f, 0.55f, 0.15f);
        torchLight.intensity = 4.5f;
        torchLight.range = 9f;
        torchLight.shadows = LightShadows.Soft;
        torchLight.shadowStrength = 0.35f;
        // renderMode omitted for URP compatibility

        // Add Flicker script
        TorchFlicker flicker = torchRoot.AddComponent<TorchFlicker>();
        flicker.baseIntensity = 4.5f;
        flicker.flickerIntensity = 2.2f;

        // Save as Prefab
        PrefabUtility.SaveAsPrefabAsset(torchRoot, torchPath);
        Object.DestroyImmediate(torchRoot);
        AssetDatabase.Refresh();

        Debug.Log("Torch prefab created at " + torchPath);

        // 2. Attach to Player Prefab
        string playerPrefabPath = "Assets/Prefabs/Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab not found!");
            return;
        }

        // Work on an instance of the prefab to edit it
        GameObject playerInstance = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;

        // Find the right hand bone
        Transform rightHand = FindDeepChild(playerInstance.transform, "mixamorig:RightHand");
        if (rightHand == null)
        {
            Debug.LogWarning("RightHand bone not found. Searching for 'RightHand'...");
            rightHand = FindDeepChild(playerInstance.transform, "RightHand");
        }

        if (rightHand != null)
        {
            // Instantiate torch and parent to right hand
            GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(torchPath);
            GameObject torchInstance = PrefabUtility.InstantiatePrefab(torchPrefab, playerInstance.transform) as GameObject;
            torchInstance.transform.SetParent(rightHand, false);
            
            // Position and rotate for natural torch holding (adjust as needed)
            torchInstance.transform.localPosition = new Vector3(0.05f, 0.12f, 0.0f); // Slightly offset in hand
            torchInstance.transform.localRotation = Quaternion.Euler(-90f, 0f, 90f); // Point flame up, handle in fist
            
            Debug.Log("Torch attached to RightHand bone in Player prefab.");
        }
        else
        {
            Debug.LogError("Could not find RightHand bone in Player. Torch added to root instead.");
            // Fallback
            GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(torchPath);
            GameObject torchInstance = PrefabUtility.InstantiatePrefab(torchPrefab, playerInstance.transform) as GameObject;
            torchInstance.transform.localPosition = new Vector3(0.8f, 1.2f, 0.3f);
        }

        // Apply changes back to prefab and clean up instance
        PrefabUtility.SaveAsPrefabAsset(playerInstance, playerPrefabPath);
        Object.DestroyImmediate(playerInstance);

        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("✅ Torch setup complete! Player now holds a flickering fire torch. Test in Demo_01_Night scene.");
        Debug.Log("Tip: Adjust torch position/rotation in the Player prefab if the holding pose doesn't look perfect.");
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name || child.name.Contains(name))
                return child;
        }
        return null;
    }
}
