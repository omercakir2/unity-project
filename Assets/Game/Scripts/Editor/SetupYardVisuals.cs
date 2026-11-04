using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Night-yard polish: wet asphalt with puddles, reflection probes, glowing air and lit rain
/// around the lamps, film grain, a touch of camera motion blur and dithering for the dark
/// gradients. Safe to run again.
/// </summary>
public static class SetupYardVisuals
{
    private const string ScenePath = "Assets/Game/Scenes/IndustrialYard_Night.unity";
    private const string ProfilePath = "Assets/Game/Scenes/IndustrialYard_Night/Global Volume Profile.asset";
    private const string CameraPrefabPath = "Assets/Game/Prefabs/Cameras/Main Camera.prefab";
    private const string SourceAsphaltPath = "Assets/Imported/Industrial/Textures/Asphalt/Seamless_asphalt_v1/Seamless_asphalt_v1.mat";
    private const string WetMaterialPath = "Assets/Game/Materials/WetAsphalt.mat";
    private const string WetMaskPath = "Assets/Game/Materials/WetAsphalt_Mask.png";
    private const string ProbeRootName = "Reflection Probes";
    private const string LampRootName = "Lamp Atmosphere";
    private const string GlowTexturePath = "Assets/Game/Materials/LampGlow_Soft.png";
    private const string GlowMaterialPath = "Assets/Game/Materials/LampGlow.mat";
    private const string LampRainMaterialPath = "Assets/Game/Materials/LampRain.mat";
    private const string RainStreakPath = "Assets/Game/Resources/VFX/RainStreak.png";
    private const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";

    private const int MaskSize = 1024;
    private const float DrySmoothness = 0.42f;
    private const float PuddleSmoothness = 0.86f;
    private const float PuddleStart = 0.5f;
    private const float PuddleEnd = 0.66f;
    private const float ProbeIntensity = 0.55f;

    // The yard is 80 m wide and runs from z -90 to z 40.
    private static readonly Vector3[] ProbeCentres =
    {
        new Vector3(0f, 2f, -68f),
        new Vector3(0f, 2f, -25f),
        new Vector3(0f, 2f, 18f)
    };
    private static readonly Vector3 ProbeSize = new Vector3(80f, 16f, 48f);

    [MenuItem("Tools/Setup Yard Visuals")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        EnableCameraDithering();
        AddPostProcessing();
        Material wet = BuildWetMaterial();
        int swapped = ApplyWetMaterial(wet);
        BuildProbes();
        BuildLampAtmosphere();
        QualitySettings.realtimeReflectionProbes = true;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupYardVisuals] Wet asphalt on " + swapped + " floor pieces.");
    }

    private static void EnableCameraDithering()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CameraPrefabPath);
        UniversalAdditionalCameraData data = root.GetComponentInChildren<UniversalAdditionalCameraData>(true);
        if (data != null)
            data.dithering = true;
        PrefabUtility.SaveAsPrefabAsset(root, CameraPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AddPostProcessing()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        FilmGrain grain = GetOrAdd<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.18f);
        grain.response.Override(0.8f);

        // Light on purpose: the video runs at a low frame rate and rain streaks smear easily.
        MotionBlur blur = GetOrAdd<MotionBlur>(profile);
        blur.mode.Override(MotionBlurMode.CameraOnly);
        blur.quality.Override(MotionBlurQuality.Medium);
        blur.intensity.Override(0.12f);
        blur.clamp.Override(0.05f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T existing))
            return existing;

        T added = profile.Add<T>();
        added.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(added, profile);
        return added;
    }

    private static Material BuildWetMaterial()
    {
        WriteMask();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(WetMaterialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(SourceAsphaltPath));
            AssetDatabase.CreateAsset(material, WetMaterialPath);
        }

        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WetMaskPath));
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Smoothness", 1f); // Multiplies the mask's alpha.
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    // Tiling smoothness map: damp asphalt with flat puddles where the noise runs high.
    private static void WriteMask()
    {
        Texture2D texture = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false, true);
        Color32[] pixels = new Color32[MaskSize * MaskSize];
        Random.State saved = Random.state;
        Random.InitState(7321);
        float[][] layers = { Grid(7), Grid(17), Grid(41), Grid(97) };
        Random.state = saved;
        for (int y = 0; y < MaskSize; y++)
        {
            for (int x = 0; x < MaskSize; x++)
            {
                float u = (float)x / MaskSize;
                float v = (float)y / MaskSize;
                float shape = Sample(layers[0], 7, u, v) * 0.5f + Sample(layers[1], 17, u, v) * 0.32f + Sample(layers[2], 41, u, v) * 0.18f;
                float puddle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PuddleStart, PuddleEnd, shape));
                float grit = (Sample(layers[3], 97, u, v) - 0.5f) * 0.1f;
                float smoothness = Mathf.Lerp(DrySmoothness + grit, PuddleSmoothness, puddle);
                pixels[y * MaskSize + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(smoothness) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(WetMaskPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(WetMaskPath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(WetMaskPath);
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();
    }

    private static float[] Grid(int period)
    {
        float[] grid = new float[period * period];
        for (int i = 0; i < grid.Length; i++)
            grid[i] = Random.value;
        return grid;
    }

    private static float Sample(float[] grid, int period, float u, float v)
    {
        float x = u * period;
        float y = v * period;
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = Mathf.SmoothStep(0f, 1f, x - x0);
        float ty = Mathf.SmoothStep(0f, 1f, y - y0);
        float bottom = Mathf.Lerp(At(grid, period, x0, y0), At(grid, period, x0 + 1, y0), tx);
        float top = Mathf.Lerp(At(grid, period, x0, y0 + 1), At(grid, period, x0 + 1, y0 + 1), tx);
        return Mathf.Lerp(bottom, top, ty);
    }

    private static float At(float[] grid, int period, int x, int y)
    {
        return grid[(y % period) * period + x % period];
    }

    private static int ApplyWetMaterial(Material wet)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceAsphaltPath);
        int swapped = 0;
        foreach (MeshRenderer floor in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            Material[] materials = floor.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != source)
                    continue;
                materials[i] = wet;
                changed = true;
            }

            if (!changed)
                continue;
            floor.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(floor);
            swapped++;
        }

        return swapped;
    }

    // URP has no volumetric fog. Large soft sprites stand in for lit air, and a dense local
    // rain sells the beam because the main rain ignores lights.
    private static void BuildLampAtmosphere()
    {
        GameObject existing = GameObject.Find(LampRootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        Material glow = BuildGlowMaterial();
        Material rain = BuildLampRainMaterial();
        GameObject root = new GameObject(LampRootName);
        Light flood = FindLight("Yard Floodlight");
        if (flood != null)
        {
            Vector3 centre = flood.transform.position + flood.transform.forward * 9f;
            centre.y = Mathf.Max(centre.y, 3.5f);
            AddLamp(root.transform, flood, centre, 7f, glow, rain);
        }

        Light gate = FindLight("Gate Lamp");
        if (gate != null)
            AddLamp(root.transform, gate, gate.transform.position + Vector3.down * 1.4f, 3.5f, glow, rain);
    }

    private static Light FindLight(string lightName)
    {
        GameObject go = GameObject.Find(lightName);
        return go != null ? go.GetComponent<Light>() : null;
    }

    private static void AddLamp(Transform root, Light lamp, Vector3 centre, float radius, Material glow, Material rain)
    {
        GameObject lampRoot = new GameObject(lamp.name + " Air");
        lampRoot.transform.SetParent(root, false);
        lampRoot.transform.position = centre;

        ParticleSystem mist = CreateSystem(lampRoot.transform, "Glow", glow);
        ParticleSystem.MainModule mistMain = mist.main;
        mistMain.startLifetime = 8f;
        mistMain.startSpeed = 0.12f;
        mistMain.startSize = new ParticleSystem.MinMaxCurve(radius * 0.7f, radius * 1.1f);
        mistMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        Color air = lamp.color;
        air.a = 0.028f;
        mistMain.startColor = air;
        mistMain.maxParticles = Mathf.RoundToInt(radius * 4f);
        ParticleSystem.EmissionModule mistEmission = mist.emission;
        mistEmission.rateOverTime = radius * 0.5f;
        ParticleSystem.ShapeModule mistShape = mist.shape;
        mistShape.enabled = true;
        mistShape.shapeType = ParticleSystemShapeType.Sphere;
        mistShape.radius = radius;
        ParticleSystem.ColorOverLifetimeModule fade = mist.colorOverLifetime;
        fade.enabled = true;
        Gradient alpha = new Gradient();
        alpha.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = alpha;
        mist.GetComponent<ParticleSystemRenderer>().maxParticleSize = 3f;

        ParticleSystem streaks = CreateSystem(lampRoot.transform, "Rain", rain);
        ParticleSystem.MainModule rainMain = streaks.main;
        rainMain.startLifetime = 0.5f;
        rainMain.startSpeed = 0f;
        rainMain.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
        Color lit = Color.Lerp(lamp.color, Color.white, 0.25f);
        lit.a = 0.6f;
        rainMain.startColor = lit;
        rainMain.maxParticles = Mathf.RoundToInt(radius * 60f);
        ParticleSystem.EmissionModule rainEmission = streaks.emission;
        rainEmission.rateOverTime = radius * 110f;
        ParticleSystem.ShapeModule rainShape = streaks.shape;
        rainShape.enabled = true;
        rainShape.shapeType = ParticleSystemShapeType.Box;
        rainShape.scale = new Vector3(radius * 2f, 0.5f, radius * 2f);
        rainShape.position = new Vector3(0f, 0f, radius);
        ParticleSystem.VelocityOverLifetimeModule fall = streaks.velocityOverLifetime;
        fall.enabled = true;
        fall.space = ParticleSystemSimulationSpace.World;
        fall.x = new ParticleSystem.MinMaxCurve(1.3f, 2.3f);
        fall.y = new ParticleSystem.MinMaxCurve(-22f, -19f);
        fall.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        ParticleSystemRenderer rainRenderer = streaks.GetComponent<ParticleSystemRenderer>();
        rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        rainRenderer.lengthScale = 0f;
        rainRenderer.velocityScale = 0.035f;
    }

    private static ParticleSystem CreateSystem(Transform parent, string systemName, Material material)
    {
        GameObject go = new GameObject(systemName);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystemRenderer particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.lightProbeUsage = LightProbeUsage.Off;
        particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return system;
    }

    private static Material BuildGlowMaterial()
    {
        WriteGlowTexture();
        Material material = AdditiveMaterial(GlowMaterialPath, AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath));
        // Fades where the sprites cut through the ground and walls.
        material.SetFloat("_SoftParticlesEnabled", 1f);
        material.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 1.5f, 0f, 0f));
        material.EnableKeyword("_SOFTPARTICLES_ON");
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static Material BuildLampRainMaterial()
    {
        Material material = AdditiveMaterial(LampRainMaterialPath, AssetDatabase.LoadAssetAtPath<Texture2D>(RainStreakPath));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static Material AdditiveMaterial(string path, Texture texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(ParticleShader));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static void WriteGlowTexture()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float step = 2f / size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) * step - 1f, (y + 0.5f) * step - 1f);
                float falloff = Mathf.Clamp01(1f - p.magnitude);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, falloff * falloff));
            }
        }

        texture.Apply();
        File.WriteAllBytes(GlowTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(GlowTexturePath);
    }

    // Almost nothing here is reflection-static, so a baked probe would come out empty.
    // Rendering once on load picks up the whole yard and costs nothing afterwards.
    private static void BuildProbes()
    {
        GameObject existing = GameObject.Find(ProbeRootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject root = new GameObject(ProbeRootName);
        for (int i = 0; i < ProbeCentres.Length; i++)
        {
            GameObject go = new GameObject("Yard Probe " + (i + 1));
            go.transform.SetParent(root.transform, false);
            go.transform.position = ProbeCentres[i];
            ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = ProbeSize;
            probe.boxProjection = true;
            probe.blendDistance = 4f;
            probe.resolution = 256;
            probe.hdr = true;
            probe.intensity = ProbeIntensity;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 120f;
        }
    }
}
