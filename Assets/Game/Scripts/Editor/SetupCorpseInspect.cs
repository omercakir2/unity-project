using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupCorpseInspect
{
    private const string RemyPath = "Assets/Game/Models/Characters/Remy.fbx";
    private const string KneelPath = "Assets/Game/Animations/Player/Remy@Kneeling Down.fbx";
    private const string AimPath = "Assets/Game/Animations/Player/Remy@Aiming Gun.fbx";
    private const string ModelPath = "Assets/Game/Models/Characters/Ch15_nonPBR.fbx";
    private const string StandClipPath = "Assets/Game/Animations/Player/KneelStand.anim";
    private const string ControllerPath = "Assets/Game/Animations/Controllers/MaleAnimator.controller";
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/Player.prefab";
    private const string InspectTag = "inspectable-kneel";

    [MenuItem("Tools/Setup Corpse Inspect")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "inspect: exit play mode first";

        EnsureTag(InspectTag);

        Avatar avatar = FindHumanAvatar(RemyPath);
        if (avatar == null)
            return "inspect: missing Remy avatar";

        AnimationClip kneel = ConfigureClip(KneelPath, avatar, "Kneel", false);
        AnimationClip aim = ConfigureClip(AimPath, avatar, "Aiming Gun", true);
        if (kneel == null)
            return "inspect: missing kneel clip";

        AnimationClip stand = ReverseClip(kneel, StandClipPath);
        if (stand == null)
            return "inspect: could not build standup clip";

        string wired = WireController(kneel, stand);
        string model = ImportAndSwapModel();
        AssetDatabase.SaveAssets();
        string aimInfo = aim == null ? "aim=missing" : "aim=" + aim.length.ToString("0.00") + "s " + RootSpan(aim);
        return "inspect: kneel=" + kneel.length.ToString("0.00") + "s " + RootSpan(kneel)
            + " stand=" + stand.length.ToString("0.00") + "s "
            + aimInfo + " " + wired + " " + model;
    }

    private static void EnsureTag(string tag)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
            return;

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                return;
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }

    private static Avatar FindHumanAvatar(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar candidate && candidate.isHuman)
                return candidate;
        }

        return null;
    }

    private static AnimationClip ConfigureClip(string path, Avatar avatar, string clipName, bool loop)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return null;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            return null;

        ModelImporterClipAnimation clip = clips[0];
        clip.name = clipName;
        clip.loopTime = loop;
        clip.loopPose = loop;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = false;
        clip.heightFromFeet = false;
        clip.maskType = ClipAnimationMaskType.None;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        Object[] clipAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < clipAssets.Length; i++)
        {
            if (clipAssets[i] is AnimationClip candidate && !candidate.name.StartsWith("__"))
                return candidate;
        }

        return null;
    }

    private static AnimationClip ReverseClip(AnimationClip source, string assetPath)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Animations/Player"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Animations"))
                AssetDatabase.CreateFolder("Assets/Game", "Animations");
            AssetDatabase.CreateFolder("Assets/Game/Animations", "Player");
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "Kneel Stand", frameRate = source.frameRate, legacy = false };
            AssetDatabase.CreateAsset(clip, assetPath);
        }
        else
        {
            clip.ClearCurves();
            clip.name = "Kneel Stand";
            clip.frameRate = source.frameRate;
            clip.legacy = false;
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.loopTime = false;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        float length = source.length;
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
        for (int i = 0; i < bindings.Length; i++)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(source, bindings[i]);
            if (curve == null)
                continue;
            AnimationUtility.SetEditorCurve(clip, bindings[i], ReverseCurve(curve, length));
        }

        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationCurve ReverseCurve(AnimationCurve source, float length)
    {
        Keyframe[] keys = source.keys;
        var reversed = new Keyframe[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            Keyframe key = keys[keys.Length - 1 - i];
            reversed[i] = new Keyframe(length - key.time, key.value, -key.outTangent, -key.inTangent);
        }

        System.Array.Sort(reversed, (a, b) => a.time.CompareTo(b.time));
        return new AnimationCurve(reversed);
    }

    private static string RootSpan(AnimationClip clip)
    {
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
        if (curve == null || curve.length == 0)
            return "rootY=none";

        return "rootY " + curve.Evaluate(0f).ToString("0.00") + "->" + curve.Evaluate(clip.length).ToString("0.00");
    }

    private static string WireController(AnimationClip kneel, AnimationClip stand)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return "missing-controller";

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState kneelState = FindState(machine, "Kneel");
        if (kneelState == null)
            kneelState = machine.AddState("Kneel", new Vector3(820f, 80f, 0f));
        kneelState.motion = kneel;
        kneelState.speed = 1f;
        kneelState.iKOnFeet = false;
        kneelState.writeDefaultValues = true;

        AnimatorState standState = FindState(machine, "Kneel Stand");
        if (standState == null)
            standState = machine.AddState("Kneel Stand", new Vector3(820f, -60f, 0f));
        standState.motion = stand;
        standState.speed = 1f;
        standState.iKOnFeet = false;
        standState.writeDefaultValues = true;

        EditorUtility.SetDirty(controller);
        return "wired";
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == name)
                return states[i].state;
        }

        return null;
    }

    private static string ImportAndSwapModel()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            return "model=missing";

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.importCameras = false;
        importer.importLights = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;

        string materials = WireCh15Materials(importer);
        importer.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Avatar avatar = FindHumanAvatar(ModelPath);
        if (model == null || avatar == null)
            return "model=not-humanoid " + materials;

        string swapped = SetupPlayerVitals.SwapModel(model);
        string feet = PlantFeet();
        EnsureInspectComponent();
        string scene = SceneModelName();
        return "model=" + swapped + " avatar=" + avatar.name + " " + materials + " " + feet + " " + scene;
    }

    private const string TextureFolder = "Assets/Game/Models/Characters/Ch15_nonPBR.fbm";

    private static string WireCh15Materials(ModelImporter importer)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            return "materials=no-urp";

        if (!AssetDatabase.IsValidFolder(TextureFolder))
            AssetDatabase.CreateFolder("Assets/Game/Models/Characters", "Ch15_nonPBR.fbm");

        importer.ExtractTextures(TextureFolder);
        AssetDatabase.Refresh();

        PrepareTexture(TextureFolder + "/Ch15_1001_Diffuse.png", false);
        PrepareTexture(TextureFolder + "/Ch15_1002_Diffuse.png", false);
        PrepareTexture(TextureFolder + "/Ch15_1001_Normal.png", true);
        PrepareTexture(TextureFolder + "/Ch15_1002_Normal.png", true);
        PrepareTexture(TextureFolder + "/Ch15_1002_Emissive.png", false);

        if (!AssetDatabase.IsValidFolder("Assets/Game/Materials/Ch15"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Materials"))
                AssetDatabase.CreateFolder("Assets/Game", "Materials");
            AssetDatabase.CreateFolder("Assets/Game/Materials", "Ch15");
        }

        Material body = LitMaterial(
            lit,
            "Assets/Game/Materials/Ch15/Ch15_body.mat",
            TextureFolder + "/Ch15_1001_Diffuse.png",
            TextureFolder + "/Ch15_1001_Normal.png",
            null);
        Material clothes = LitMaterial(
            lit,
            "Assets/Game/Materials/Ch15/Ch15_body1.mat",
            TextureFolder + "/Ch15_1002_Diffuse.png",
            TextureFolder + "/Ch15_1002_Normal.png",
            TextureFolder + "/Ch15_1002_Emissive.png");

        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Ch15_body"), body);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Ch15_body1"), clothes);
        return "materials=remapped bodyTex=" + (body.GetTexture("_BaseMap") != null)
            + " clothesTex=" + (clothes.GetTexture("_BaseMap") != null);
    }

    private static void PrepareTexture(string path, bool normalMap)
    {
        var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        if (textureImporter == null)
            return;

        textureImporter.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        textureImporter.sRGBTexture = !normalMap;
        textureImporter.SaveAndReimport();
    }

    private static Material LitMaterial(Shader shader, string path, string albedoPath, string normalPath, string emissionPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>(albedoPath);
        Texture normal = AssetDatabase.LoadAssetAtPath<Texture>(normalPath);
        if (albedo != null)
        {
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
        }

        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        Texture emission = string.IsNullOrEmpty(emissionPath) ? null : AssetDatabase.LoadAssetAtPath<Texture>(emissionPath);
        if (emission != null)
        {
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.22f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static string PlantFeet()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            CharacterController capsule = root.GetComponent<CharacterController>();
            Transform slot = root.transform.Find("ModelSlot");
            if (capsule == null || slot == null || slot.childCount == 0)
                return "feet=missing";

            Transform visual = slot.GetChild(0);
            SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null)
                return "feet=no-skin";

            Mesh baked = new Mesh();
            skin.BakeMesh(baked);
            Vector3[] vertices = baked.vertices;
            float minY = float.PositiveInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                float y = root.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i])).y;
                if (y < minY)
                    minY = y;
            }

            UnityEngine.Object.DestroyImmediate(baked);
            if (float.IsPositiveInfinity(minY))
                return "feet=empty";

            float capsuleBottom = capsule.center.y - capsule.height * 0.5f;
            Vector3 local = visual.localPosition;
            local.y += capsuleBottom - minY;
            visual.localPosition = local;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return "feet=" + local.y.ToString("0.00");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureInspectComponent()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (root.GetComponent<CorpseInspect>() == null)
                root.AddComponent<CorpseInspect>();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string SceneModelName()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            return "scene=no-player";

        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null)
            return "scene=no-animator";

        return "scene=" + animator.gameObject.name;
    }
}

