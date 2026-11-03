using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the abandoned-facility wake-up scene: fixes the Tirgames materials for URP,
/// dresses the sci-fi capsule, lays out a small sealed room and wires the opening cinematic.
/// </summary>
public static class SetupWakeUpFacility
{
    private const string TirgamesFolder = "Assets/Imported/TirgamesAssets";
    private const string FactoryPrefabs = "Assets/Imported/TirgamesAssets/Factory/Prefabs/";
    private const string CapsuleFolder = "Assets/Imported/SciFiCapsule/";
    private const string CapsuleModelPath = CapsuleFolder + "Models/Capsula_Animation.fbx";
    private const string CapsuleTextures = CapsuleFolder + "Textures/";
    private const string CapsuleMaterials = CapsuleFolder + "Materials/";
    private const string ScenePath = "Assets/Game/Scenes/Facility_WakeUp.unity";
    private const string YardScenePath = "Assets/Game/Scenes/IndustrialYard_Night.unity";
    private const string FromYardSpawn = "FromYard";
    private const string FromFacilitySpawn = "FromFacility";
    private const string SceneFolder = "Assets/Game/Scenes/Facility_WakeUp/";
    private const string PlayerPath = "Assets/Game/Prefabs/Characters/Player.prefab";
    private const string MainCameraPath = "Assets/Game/Prefabs/Cameras/Main Camera.prefab";
    private const string CameraRigPath = "Assets/Game/Prefabs/Cameras/Cinemachine Cameras.prefab";

    private const float CapsuleScale = 0.82f;
    private static readonly Vector3 CapsulePosition = new Vector3(-6f, 0f, 9f);

    [MenuItem("Tools/Setup Wake Up Facility")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "facility: exit play mode first";

        string materials = ConvertTirgamesMaterials();
        string capsule = DressCapsule();
        string scene = BuildScene();
        AssetDatabase.SaveAssets();
        return "facility: " + materials + " " + capsule + " " + scene;
    }

    public static string ConvertMaterialsOnly()
    {
        string result = ConvertTirgamesMaterials();
        AssetDatabase.SaveAssets();
        return result;
    }

    // ---------------------------------------------------------------- materials

    // The pack ships Built-in Standard and Legacy materials, which render magenta under URP.
    private static string ConvertTirgamesMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            return "materials=no-urp-shader";

        var standard = new StandardUpgrader("Standard");
        var specular = new StandardUpgrader("Standard (Specular setup)");
        int converted = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { TirgamesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null)
                continue;

            string shaderName = material.shader.name;
            if (shaderName == "Standard")
                MaterialUpgrader.Upgrade(material, standard, MaterialUpgrader.UpgradeFlags.None);
            else if (shaderName == "Standard (Specular setup)")
                MaterialUpgrader.Upgrade(material, specular, MaterialUpgrader.UpgradeFlags.None);
            else if (shaderName.StartsWith("Legacy Shaders/"))
                UpgradeLegacy(material, lit);
            else
                continue;

            EditorUtility.SetDirty(material);
            converted++;
        }

        return "materials=" + converted;
    }

    private static void UpgradeLegacy(Material material, Shader lit)
    {
        bool transparent = material.shader.name.Contains("Transparent");
        bool cutout = material.shader.name.Contains("Cutout");
        Texture main = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        Vector2 scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
        Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
        Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        Texture bump = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;

        material.shader = lit;
        material.SetTexture("_BaseMap", main);
        material.SetTextureScale("_BaseMap", scale);
        material.SetTextureOffset("_BaseMap", offset);
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BumpMap", bump);
        material.SetFloat("_Smoothness", 0.1f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Surface", transparent && !cutout ? 1f : 0f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
    }

    // ---------------------------------------------------------------- capsule

    private static string DressCapsule()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (lit == null || unlit == null)
            return "capsule=no-urp-shader";

        Directory.CreateDirectory(CapsuleMaterials);
        PrepareTexture(CapsuleTextures + "Capsula_Low_Capsule_Normal.png", TextureImporterType.NormalMap, true);
        PrepareTexture(CapsuleTextures + "Capsula_Low_Supports_Normal.png", TextureImporterType.NormalMap, true);
        PrepareTexture(CapsuleTextures + "Capsula_Low_Capsule_BaseColor.png", TextureImporterType.Default, true);
        PrepareTexture(CapsuleTextures + "Capsula_Low_Supports_BaseColor.png", TextureImporterType.Default, true);
        PrepareTexture(CapsuleTextures + "Capsula_Low_Capsule_Emissive.png", TextureImporterType.Default, true);
        PrepareTexture(CapsuleTextures + "Capsula_Low_Supports_Emissive.png", TextureImporterType.Default, true);
        PrepareTexture(CapsuleTextures + "Capsula_Low_Glass_Emissive.png", TextureImporterType.Default, true);
        PrepareTexture(CapsuleTextures + "kisspng-light-green-white-point-halo-green.png", TextureImporterType.Default, true);
        PrepareTexture(CapsuleTextures + "mascara1.png", TextureImporterType.Default, true);
        PrepareTexture(CapsuleTextures + "mascara2.png", TextureImporterType.Default, true);
        PrepareTexture(CapsuleTextures + "hexagon_keyboard_by_hxtremechaos_d865owr-f.png", TextureImporterType.Default, true);

        Texture2D capsuleMask = PackMetallicSmoothness("Capsule");
        Texture2D supportsMask = PackMetallicSmoothness("Supports");
        Texture2D glassBase = PackAlpha(CapsuleTextures + "Capsula_Low_Glass_BaseColor.png",
            CapsuleTextures + "Capsula_Low_Glass_Opacity.png", CapsuleTextures + "Capsula_Low_Glass_BaseAlpha.png");

        Material capsule = LitMaterial("Capsule", lit, "Capsule", capsuleMask);
        Material supports = LitMaterial("Supports", lit, "Supports", supportsMask);

        Material glass = LoadOrCreate(CapsuleMaterials + "Glass.mat", lit);
        glass.SetTexture("_BaseMap", glassBase);
        glass.SetColor("_BaseColor", new Color(0.3f, 0.4f, 0.45f, 0.3f));
        glass.SetFloat("_Metallic", 0f);
        glass.SetFloat("_Smoothness", 0.95f);
        glass.SetFloat("_Surface", 1f);
        glass.SetFloat("_Blend", 0f);
        glass.SetFloat("_Cull", 0f);
        SetEmission(glass, Load(CapsuleTextures + "Capsula_Low_Glass_Emissive.png"), new Color(0.3f, 0.8f, 1f) * 0.25f);
        BaseShaderGUI.SetMaterialKeywords(glass, LitGUI.SetMaterialKeywords);

        Material halo = AdditiveMaterial("Halo", unlit, CapsuleTextures + "kisspng-light-green-white-point-halo-green.png", new Color(0.12f, 0.32f, 0.4f, 1f));
        Material holo1 = AdditiveMaterial("Hologram_1", unlit, CapsuleTextures + "mascara1.png", new Color(0.3f, 0.85f, 1f, 1f));
        Material holo2 = AdditiveMaterial("Hologram_2", unlit, CapsuleTextures + "hexagon_keyboard_by_hxtremechaos_d865owr-f.png", new Color(0.3f, 0.85f, 1f, 1f));
        Material holo3 = AdditiveMaterial("Hologram_3", unlit, CapsuleTextures + "mascara2.png", new Color(0.3f, 0.85f, 1f, 1f));

        var importer = AssetImporter.GetAtPath(CapsuleModelPath) as ModelImporter;
        if (importer == null)
            return "capsule=no-model";

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = true;
        Remap(importer, "Capsule", capsule);
        Remap(importer, "Supports", supports);
        Remap(importer, "Glass", glass);
        Remap(importer, "Halo", halo);
        Remap(importer, "Hologram_1", holo1);
        Remap(importer, "Hologram_2", holo2);
        Remap(importer, "Hologram_3", holo3);
        importer.SaveAndReimport();
        return "capsule=ok";
    }

    private static Material LitMaterial(string name, Shader lit, string part, Texture2D mask)
    {
        Material material = LoadOrCreate(CapsuleMaterials + name + ".mat", lit);
        material.SetTexture("_BaseMap", Load(CapsuleTextures + "Capsula_Low_" + part + "_BaseColor.png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", Load(CapsuleTextures + "Capsula_Low_" + part + "_Normal.png"));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 1f);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        SetEmission(material, Load(CapsuleTextures + "Capsula_Low_" + part + "_Emissive.png"), new Color(0.4f, 0.9f, 1f) * 2f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material AdditiveMaterial(string name, Shader unlit, string texturePath, Color tint)
    {
        Material material = LoadOrCreate(CapsuleMaterials + name + ".mat", unlit);
        material.SetTexture("_BaseMap", Load(texturePath));
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_Cull", 0f);
        BaseShaderGUI.SetMaterialKeywords(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetEmission(Material material, Texture map, Color color)
    {
        material.SetTexture("_EmissionMap", map);
        material.SetColor("_EmissionColor", map != null ? color : Color.black);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    // URP Lit reads metallic from R and smoothness from A of one map.
    private static Texture2D PackMetallicSmoothness(string part)
    {
        string metallic = CapsuleTextures + "Capsula_Low_" + part + "_Metallic.png";
        string roughness = CapsuleTextures + "Capsula_Low_" + part + "_Roughness.png";
        string output = CapsuleTextures + "Capsula_Low_" + part + "_MetallicSmoothness.png";
        if (!File.Exists(output))
        {
            Texture2D m = ReadPng(metallic);
            Texture2D r = ReadPng(roughness);
            if (m == null || r == null)
                return null;

            Color32[] mp = m.GetPixels32();
            Color32[] rp = Resize(r, m.width, m.height).GetPixels32();
            for (int i = 0; i < mp.Length; i++)
            {
                byte metal = mp[i].r;
                mp[i] = new Color32(metal, metal, metal, (byte)(255 - rp[i].r));
            }

            WritePng(output, mp, m.width, m.height);
        }

        PrepareTexture(output, TextureImporterType.Default, false);
        return Load(output);
    }

    private static Texture2D PackAlpha(string colorPath, string alphaPath, string output)
    {
        if (!File.Exists(output))
        {
            Texture2D c = ReadPng(colorPath);
            Texture2D a = ReadPng(alphaPath);
            if (c == null || a == null)
                return Load(colorPath);

            Color32[] cp = c.GetPixels32();
            Color32[] ap = Resize(a, c.width, c.height).GetPixels32();
            for (int i = 0; i < cp.Length; i++)
                cp[i].a = ap[i].r;
            WritePng(output, cp, c.width, c.height);
        }

        PrepareTexture(output, TextureImporterType.Default, true);
        return Load(output);
    }

    private static Texture2D ReadPng(string path)
    {
        if (!File.Exists(path))
            return null;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        texture.LoadImage(File.ReadAllBytes(path));
        return texture;
    }

    private static Texture2D Resize(Texture2D source, int width, int height)
    {
        if (source.width == width && source.height == height)
            return source;

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var result = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    private static void WritePng(string path, Color32[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
    }

    private static void PrepareTexture(string path, TextureImporterType type, bool srgb)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;
        if (importer.textureType == type && importer.sRGBTexture == srgb && importer.maxTextureSize == 2048)
            return;

        importer.textureType = type;
        importer.sRGBTexture = type != TextureImporterType.NormalMap && srgb;
        importer.alphaIsTransparency = type == TextureImporterType.Default && srgb;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void Remap(ModelImporter importer, string sourceName, Material material)
    {
        if (material != null)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), material);
    }

    private static Material LoadOrCreate(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
            material.shader = shader;

        return material;
    }

    private static Texture2D Load(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- scene

    private static string BuildScene()
    {
        Directory.CreateDirectory(SceneFolder);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        ConfigureRenderSettings();
        Transform room = new GameObject("Facility").transform;
        room.gameObject.AddComponent<IndoorScene>();
        BuildShell(room);
        Transform props = new GameObject("Props").transform;
        props.SetParent(room, false);
        DressRoom(props);
        Transform lights = new GameObject("Lights").transform;
        BuildLights(lights);
        BuildVolumes(out Volume blur);

        GameObject capsuleObject = PlaceCapsule(out SleepCapsule capsule, out Light podLight);
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MainCameraPath));
        GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPath));
        if (player == null || rig == null)
            return "scene=missing-player-or-camera";

        WireCameraRig(rig, player);

        Transform capsuleRoot = capsuleObject.transform;
        Transform sleepPoint = Marker("Sleep Point", capsuleRoot, new Vector3(0f, 1.02f, 0f));
        Transform exitPoint = Marker("Exit Point", capsuleRoot, new Vector3(0f, 0f, 3.8f));
        Transform focusPoint = Marker("Focus Point", capsuleRoot, new Vector3(0f, 2.2f, 0f));
        player.transform.SetPositionAndRotation(exitPoint.position + Vector3.up * 1.3f, capsuleRoot.rotation);

        GameObject director = new GameObject("Wake Up Cinematic");
        WakeUpCinematic cinematic = director.AddComponent<WakeUpCinematic>();
        var so = new SerializedObject(cinematic);
        so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
        so.FindProperty("capsule").objectReferenceValue = capsule;
        so.FindProperty("sleepPoint").objectReferenceValue = sleepPoint;
        so.FindProperty("exitPoint").objectReferenceValue = exitPoint;
        so.FindProperty("focusPoint").objectReferenceValue = focusPoint;
        so.FindProperty("blurVolume").objectReferenceValue = blur;
        so.ApplyModifiedPropertiesWithoutUndo();

        // The side door leads out to the yard. Coming back puts the player just inside it.
        SceneDoor door = NewDoor("Door To Yard", new Vector3(-0.6f, 0f, 14.8f), YardScenePath, FromFacilitySpawn);
        door.transform.SetParent(room, true);
        Spawn(FromYardSpawn, new Vector3(-2.2f, 0f, 14.8f), Vector3.left);

        SetupMinimapHud.AddToActiveScene(SetupMinimapHud.FacilityObjective, true);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AddToBuild(YardScenePath);
        return "scene=" + ScenePath;
    }

    [MenuItem("Tools/Add Facility Door To Yard")]
    public static void AddYardDoorFromMenu()
    {
        Debug.Log(AddYardDoor());
    }

    /// <summary>
    /// Puts the facility entrance on the hangar wall next to the yard's player start,
    /// with the spawn point used when walking out of the facility.
    /// </summary>
    public static string AddYardDoor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "yard door: exit play mode first";

        Scene scene = EditorSceneManager.OpenScene(YardScenePath, OpenSceneMode.Single);
        GameObject old = GameObject.Find("Facility Entrance");
        if (old != null)
            Object.DestroyImmediate(old);

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
            return "yard door: no player in scene";

        // The hangar south of the player start, straight along +Z from a point beside it.
        Vector3 origin = new Vector3(-12f, 1.2f, player.transform.position.z);
        if (!Physics.Raycast(origin, Vector3.forward, out RaycastHit wall, 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return "yard door: no wall found from " + origin;

        Vector3 normal = wall.normal;
        normal.y = 0f;
        normal.Normalize();
        Vector3 ground = wall.point;
        if (Physics.Raycast(wall.point + normal * 0.5f + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            ground.y = floor.point.y;
        else
            ground.y = 0f;

        GameObject root = new GameObject("Facility Entrance");
        Quaternion doorRotation = Quaternion.LookRotation(-normal, Vector3.up);
        GameObject frame = Place("DoorIndustrial01_1", ground + normal * 0.1f + doorRotation * Vector3.right * 1.2f, 0f, root.transform);
        frame.transform.rotation = doorRotation;

        // A weak lamp over the frame so the entrance reads in the hangar's shadow.
        NewLight("Entrance Light", LightType.Point, root.transform, ground + normal * 0.5f + Vector3.up * 2.6f, new Color(1f, 0.6f, 0.3f), 2.5f, 5f);

        SceneDoor door = NewDoor("Door To Facility", ground + normal * 0.6f, ScenePath, FromYardSpawn);
        door.transform.SetParent(root.transform, true);
        Spawn(FromFacilitySpawn, ground + normal * 2f, normal).SetParent(root.transform, true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuild(ScenePath);
        AddToBuild(YardScenePath);
        return "yard door: on " + wall.collider.name + " at " + ground.ToString("F2");
    }

    private static SceneDoor NewDoor(string name, Vector3 position, string targetScene, string spawnId)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        SceneDoor door = go.AddComponent<SceneDoor>();
        var so = new SerializedObject(door);
        so.FindProperty("targetScene").stringValue = targetScene;
        so.FindProperty("targetSpawnId").stringValue = spawnId;
        so.ApplyModifiedPropertiesWithoutUndo();
        return door;
    }

    private static Transform Spawn(string id, Vector3 position, Vector3 facing)
    {
        var go = new GameObject("Spawn " + id);
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, Vector3.up));
        SceneSpawnPoint point = go.AddComponent<SceneSpawnPoint>();
        var so = new SerializedObject(point);
        so.FindProperty("spawnId").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go.transform;
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.065f, 0.072f, 0.085f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.35f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.03f);
    }

    // A 12 x 24 m sealed hall: floor x[-12,0] z[0,24], walls stand just outside it.
    private static void BuildShell(Transform room)
    {
        Transform shell = new GameObject("Shell").transform;
        shell.SetParent(room, false);

        Place("Factory2Floor01", new Vector3(0f, 0f, 0f), 0f, shell);
        Place("Factory2Floor01", new Vector3(0f, 0f, 12f), 0f, shell);
        Place("Factory2Ceiling01", new Vector3(0f, 4.5f, 0f), 0f, shell);
        Place("Factory2Ceiling01", new Vector3(0f, 4.5f, 12f), 0f, shell);
        Place("Factory2Roof01", new Vector3(0.3f, 4.9f, 0f), 0f, shell);
        Place("Factory2Roof01", new Vector3(0.3f, 4.9f, 12f), 0f, shell);

        Place("Factory2Wall03", new Vector3(0f, -0.1f, 24f), 0f, shell);
        Place("Factory2Wall02", new Vector3(-12f, -0.1f, 0f), 180f, shell);
        Place("Factory2Wall01", new Vector3(0f, -0.1f, 0f), 90f, shell);
        Place("Factory2Wall06", new Vector3(0f, -0.1f, 12f), 90f, shell);
        Place("Factory2Wall02", new Vector3(-12f, -0.1f, 12f), 270f, shell);
        Place("Factory2Wall01", new Vector3(-12f, -0.1f, 24f), 270f, shell);

        Place("DoorIndustrial01_1", new Vector3(0.3f, 0f, 13.6f), 90f, shell);
        Place("LargeGates1_1", new Vector3(-6f, 0f, 23.95f), 180f, shell);

        for (int i = 1; i <= 3; i++)
            Place("Factory2Columns01", new Vector3(0f, 0f, 6f * i), 0f, shell);

        // The wall pieces have window openings. Brick them up from outside so the hall is sealed.
        Material bricks = BrickFillMaterial();
        BrickFill(new Vector3(-6f, 2.4f, 24.7f), 0f, shell, bricks);
        BrickFill(new Vector3(-6f, 2.4f, -0.7f), 0f, shell, bricks);
        BrickFill(new Vector3(0.7f, 2.4f, 6f), 90f, shell, bricks);
        BrickFill(new Vector3(0.7f, 2.4f, 18f), 90f, shell, bricks);
        BrickFill(new Vector3(-12.7f, 2.4f, 6f), 90f, shell, bricks);
        BrickFill(new Vector3(-12.7f, 2.4f, 18f), 90f, shell, bricks);

        SetStatic(shell.gameObject);
    }

    private static Material BrickFillMaterial()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imported/TirgamesAssets/Factory/Models/Materials/CommonBricks01.mat");
        Material material = LoadOrCreate(SceneFolder + "BrickFill.mat", Shader.Find("Universal Render Pipeline/Lit"));
        if (source != null)
        {
            material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
        }

        material.SetTextureScale("_BaseMap", new Vector2(6f, 2.6f));
        material.SetColor("_BaseColor", new Color(0.55f, 0.52f, 0.5f));
        material.SetFloat("_Smoothness", 0.1f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BrickFill(Vector3 position, float yaw, Transform parent, Material material)
    {
        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "Brick Fill";
        fill.transform.SetParent(parent, false);
        fill.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        fill.transform.localScale = new Vector3(12.4f, 5.2f, 0.25f);
        fill.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void DressRoom(Transform props)
    {
        // Machine wall behind the capsule.
        Place("PowerBox01_1", new Vector3(-8.6f, 0f, 0.05f), 0f, props);
        Place("PowerBox01_2", new Vector3(-3.4f, 0f, 0.05f), 0f, props);
        Place("PowerBox03", new Vector3(-0.05f, 0f, 4.2f), 270f, props);
        Place("PowerBox02_1", new Vector3(-0.05f, 1.2f, 6.6f), 270f, props);
        Place("PowerBox02_2", new Vector3(-0.05f, 1.2f, 7.4f), 270f, props);

        Place("MetalCabinet01_1", new Vector3(-11.6f, 0f, 8.6f), 90f, props);
        Place("MetalCabinet01_2", new Vector3(-11.6f, 0f, 9.6f), 90f, props);
        GameObject fallen = Place("MetalCabinet01_1", new Vector3(-10.4f, 0.34f, 14.2f), 90f, props);
        fallen.transform.rotation = Quaternion.Euler(0f, 70f, 90f);

        Place("Barrel01a", new Vector3(-10.6f, 0f, 21.6f), 0f, props);
        Place("Barrel01b", new Vector3(-11.2f, 0f, 22.5f), 40f, props);
        Place("Barrel01c", new Vector3(-10.1f, 0f, 22.7f), 15f, props);
        GameObject tipped = Place("Barrel01d", new Vector3(-8.9f, 0.33f, 20.4f), 0f, props);
        tipped.transform.rotation = Quaternion.Euler(0f, 35f, 90f);

        Place("GasBallone01_2", new Vector3(-1.1f, 0f, 19.6f), 0f, props);
        Place("GasBallone01_2", new Vector3(-1.5f, 0f, 20.3f), 60f, props);
        Place("FireExtinguisher01_1", new Vector3(-0.2f, 1f, 11.2f), 270f, props);
        GameObject extinguisher = Place("FireExtinguisher01_2", new Vector3(-2.4f, 0.1f, 16.2f), 0f, props);
        extinguisher.transform.rotation = Quaternion.Euler(0f, 30f, 90f);

        Place("FanBig01Motor01", new Vector3(-11.75f, 2.2f, 17f), 90f, props);

        Place("Debris01_8", new Vector3(-6f, 0.01f, 16f), 20f, props);
        Place("Debris01_7", new Vector3(-3f, 0.01f, 20f), 110f, props);
        Place("Debris01_12", new Vector3(-9.5f, 0.01f, 3.5f), 0f, props);
        Place("Debris01_1", new Vector3(-2.5f, 0f, 14.5f), 35f, props);
        Place("Debris01_3", new Vector3(-9.3f, 0f, 16.2f), 200f, props);
        Place("Debris01_5", new Vector3(-7.8f, 0.1f, 22f), 80f, props);
        Place("Debris01_13", new Vector3(-1.6f, 0f, 1.6f), 300f, props);

        SetStatic(props.gameObject);
    }

    private static void BuildLights(Transform lights)
    {
        // Dead fixtures in a grid. A few still burn.
        float[] xs = { -3f, -9f };
        float[] zs = { 3f, 9f, 15f, 21f };
        for (int xi = 0; xi < xs.Length; xi++)
        {
            for (int zi = 0; zi < zs.Length; zi++)
            {
                Vector3 position = new Vector3(xs[xi], 4.08f, zs[zi]);
                bool working = (xi == 0 && zi == 2) || (xi == 1 && zi == 3);
                GameObject lamp = Place(working ? "LampCeiling01Night" : "LampCeiling01", position, 0f, lights);
                if (!working)
                    continue;

                Light light = lamp.GetComponentInChildren<Light>();
                if (light == null)
                    continue;

                light.color = new Color(0.85f, 0.92f, 1f);
                light.intensity = 3f;
                light.range = 9f;
                light.shadows = LightShadows.Soft;
                if (xi == 0)
                    light.gameObject.AddComponent<LightFlicker>();
            }
        }

        // Cold top light on the capsule so it reads as the room's centrepiece.
        Light spot = NewLight("Capsule Spot", LightType.Spot, lights, CapsulePosition + new Vector3(0f, 4.3f, 1.2f), new Color(0.7f, 0.85f, 1f), 10f, 9f);
        spot.transform.rotation = Quaternion.LookRotation(CapsulePosition + Vector3.up * 1.5f - spot.transform.position);
        spot.spotAngle = 55f;
        spot.innerSpotAngle = 25f;
        spot.shadows = LightShadows.Soft;

        NewLight("Emergency Light", LightType.Point, lights, new Vector3(-6f, 4.1f, 23.2f), new Color(1f, 0.12f, 0.08f), 2.5f, 8f);
        NewLight("Door Light", LightType.Point, lights, new Vector3(-0.6f, 2.8f, 14.6f), new Color(1f, 0.55f, 0.2f), 1.2f, 4f)
            .gameObject.AddComponent<LightFlicker>();
    }

    private static Light NewLight(string name, LightType type, Transform parent, Vector3 position, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        return light;
    }

    private static void BuildVolumes(out Volume blur)
    {
        VolumeProfile look = CreateProfile(SceneFolder + "FacilityLook.asset");
        Tonemapping tonemapping = Override<Tonemapping>(look);
        tonemapping.mode.Override(TonemappingMode.ACES);
        Bloom bloom = Override<Bloom>(look);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.65f);
        Vignette vignette = Override<Vignette>(look);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.5f);
        ColorAdjustments color = Override<ColorAdjustments>(look);
        color.postExposure.Override(0.4f);
        color.contrast.Override(12f);
        color.saturation.Override(-18f);
        color.colorFilter.Override(new Color(0.92f, 0.97f, 1f));
        FilmGrain grain = Override<FilmGrain>(look);
        grain.intensity.Override(0.25f);

        VolumeProfile wake = CreateProfile(SceneFolder + "WakeBlur.asset");
        DepthOfField dof = Override<DepthOfField>(wake);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(0f);
        dof.gaussianEnd.Override(0.6f);
        dof.gaussianMaxRadius.Override(1.5f);
        dof.highQualitySampling.Override(true);
        Vignette wakeVignette = Override<Vignette>(wake);
        wakeVignette.intensity.Override(0.55f);
        wakeVignette.smoothness.Override(1f);
        ChromaticAberration aberration = Override<ChromaticAberration>(wake);
        aberration.intensity.Override(0.7f);
        ColorAdjustments wakeColor = Override<ColorAdjustments>(wake);
        wakeColor.postExposure.Override(0.8f);
        wakeColor.saturation.Override(-45f);
        EditorUtility.SetDirty(look);
        EditorUtility.SetDirty(wake);

        var global = new GameObject("Global Volume").AddComponent<Volume>();
        global.isGlobal = true;
        global.sharedProfile = look;

        blur = new GameObject("Wake Blur Volume").AddComponent<Volume>();
        blur.isGlobal = true;
        blur.priority = 10f;
        blur.weight = 0f;
        blur.sharedProfile = wake;
    }

    private static VolumeProfile CreateProfile(string path)
    {
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static GameObject PlaceCapsule(out SleepCapsule capsule, out Light podLight)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(CapsuleModelPath);
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = "Sleep Capsule";
        go.transform.SetPositionAndRotation(CapsulePosition, Quaternion.identity);
        go.transform.localScale = Vector3.one * CapsuleScale;

        // Solid base so the player walks around it, not through it.
        CapsuleCollider body = go.AddComponent<CapsuleCollider>();
        body.direction = 1;
        body.radius = 1f;
        body.height = 5.2f;
        body.center = new Vector3(0f, 2.6f, 0f);

        podLight = NewLight("Pod Light", LightType.Point, go.transform, Vector3.zero, new Color(0.3f, 0.85f, 1f), 2.6f, 5f);
        podLight.transform.localPosition = new Vector3(0f, 3.35f, 0.15f);

        AnimationClip clip = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CapsuleModelPath))
        {
            if (asset is AnimationClip c && !c.name.StartsWith("__preview__"))
                clip = c;
        }

        capsule = go.AddComponent<SleepCapsule>();
        var so = new SerializedObject(capsule);
        so.FindProperty("openClip").objectReferenceValue = clip;
        so.FindProperty("podLight").objectReferenceValue = podLight;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    private static void WireCameraRig(GameObject rig, GameObject player)
    {
        Transform cameraTarget = FindChild(player.transform, "CameraTarget");
        PlayerController controller = player.GetComponent<PlayerController>();
        foreach (CinemachineCamera vcam in rig.GetComponentsInChildren<CinemachineCamera>(true))
        {
            var so = new SerializedObject(vcam);
            so.FindProperty("Target.TrackingTarget").objectReferenceValue = cameraTarget;
            so.FindProperty("Target.LookAtTarget").objectReferenceValue = cameraTarget;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (MonoBehaviour behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var so = new SerializedObject(behaviour);
            SerializedProperty field = so.FindProperty("player");
            if (field == null || field.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            field.objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Transform Marker(string name, Transform parent, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    private static GameObject Place(string prefab, Vector3 position, float yaw, Transform parent)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(FactoryPrefabs + prefab + ".prefab");
        if (asset == null)
        {
            Debug.LogWarning("[Facility] Missing prefab " + prefab);
            return new GameObject(prefab + " (missing)");
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return go;
    }

    private static void SetStatic(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return root;
    }

    private static void AddToBuild(string path)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene s in scenes)
        {
            if (s.path == path)
                return;
        }

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes) { new EditorBuildSettingsScene(path, true) };
        EditorBuildSettings.scenes = list.ToArray();
    }
}
