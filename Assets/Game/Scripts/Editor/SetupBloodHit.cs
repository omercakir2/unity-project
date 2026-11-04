using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the zombie hit effect from the Blood Gush spray without touching the package:
/// a one-shot prefab variant in Resources with URP materials replacing the legacy ones.
/// </summary>
public static class SetupBloodHit
{
    private const string SourcePrefab = "Assets/BloodDecalsAndEffects/BloodGushFX/BloodSprayFX.prefab";
    private const string Folder = "Assets/Game/Resources/VFX";
    private const string PrefabPath = Folder + "/BloodHit.prefab";
    private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";

    private const float SprayTime = 0.12f;
    private const float SecondBurstTime = 0.05f;
    private const short FirstBurst = 30;
    private const short SecondBurst = 15;
    private const float SpeedScale = 0.4f; // At full speed the droplets leave the body as one clump.
    private const float LifetimeScale = 1.7f;
    private const float ConeAngle = 20f;
    private static readonly Color Tint = new Color(0.42f, 0.02f, 0.02f, 1f); // Unlit reads too bright at night.

    [MenuItem("Tools/Setup Blood Hit")]
    public static void Run()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (source == null)
        {
            Debug.LogError("[SetupBloodHit] Missing " + SourcePrefab + ".");
            return;
        }

        Directory.CreateDirectory(Folder);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
            MakeOneShot(systems[i], i);

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        Debug.Log("[SetupBloodHit] Wrote " + PrefabPath + ".");
    }

    private static void MakeOneShot(ParticleSystem system, int index)
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.duration = SprayTime;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy; // ZombieBlood sizes it from the root.
        main.startSpeedMultiplier *= SpeedScale;
        main.startLifetimeMultiplier *= LifetimeScale;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.angle = ConeAngle;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, FirstBurst),
            new ParticleSystem.Burst(SecondBurstTime, SecondBurst)
        });

        ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = BuildMaterial(particleRenderer.sharedMaterial, index);
    }

    // The package's legacy particle shader is not a URP shader. Unlit also stays readable at night.
    private static Material BuildMaterial(Material source, int index)
    {
        string path = Folder + "/BloodHit_" + index + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(ShaderName));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", source.mainTexture);
        material.SetColor("_BaseColor", Tint);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }
}
