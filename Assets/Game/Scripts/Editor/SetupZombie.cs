using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupZombie
{
    private const string ModelPath = "Assets/Game/Models/Characters/Zombie/Zombiegirl W Kurniawan.fbx";
    private const string AnimFolder = "Assets/Game/Animations/Zombie/";
    private const string ControllerPath = "Assets/Game/Animations/Controllers/ZombieAnimator.controller";
    private const string PrefabPath = "Assets/Game/Prefabs/Characters/Zombie.prefab";
    private const string ScenePath = "Assets/Game/Scenes/IndustrialYard_Night.unity";
    private const float TargetHeight = 1.8f;

    [MenuItem("Tools/Setup Zombie")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "zombie: exit play mode first";

        Avatar avatar = ImportModel();
        if (avatar == null || !avatar.isHuman)
            return "zombie: model did not import as a humanoid avatar";

        AnimationClip crawl = ConfigureClip(AnimFolder + "Zombiegirl W Kurniawan@Zombie Crawl.fbx", avatar, "Crawl", true, false);
        AnimationClip stand = ConfigureClip(AnimFolder + "Zombiegirl W Kurniawan@Zombie Stand Up.fbx", avatar, "Stand Up", false, true);
        AnimationClip scream = ConfigureClip(AnimFolder + "Zombiegirl W Kurniawan@Zombie Scream.fbx", avatar, "Scream", false, true);
        AnimationClip run = ConfigureClip(AnimFolder + "Zombiegirl W Kurniawan@Zombie Running.fbx", avatar, "Running", true, false);
        AnimationClip death = ConfigureClip(AnimFolder + "Zombiegirl W Kurniawan@Zombie Death.fbx", avatar, "Death", false, true);
        if (crawl == null || stand == null || scream == null || run == null || death == null)
            return "zombie: missing clip"
                + " crawl=" + (crawl != null)
                + " stand=" + (stand != null)
                + " scream=" + (scream != null)
                + " run=" + (run != null)
                + " death=" + (death != null);

        float crawlExit = crawl.length < 1.6f ? Mathf.Max(1.05f, 2f / Mathf.Max(0.05f, crawl.length)) : 0.92f;
        BuildController(crawl, stand, scream, run, death, crawlExit);
        string materials = UpgradeMaterials();
        string placed = BuildPrefabAndPlace(avatar);
        AssetDatabase.SaveAssets();
        return "zombie: crawl=" + crawl.length.ToString("0.00")
            + "s exit=" + crawlExit.ToString("0.00")
            + " stand=" + stand.length.ToString("0.00")
            + " scream=" + scream.length.ToString("0.00")
            + " run=" + run.length.ToString("0.00")
            + " death=" + death.length.ToString("0.00")
            + " " + materials
            + " " + placed;
    }

    private static Avatar ImportModel()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            return null;

        // Reset the rig before creating the avatar. A second humanoid import
        // kept the old bone lengths and pushed the skeleton off the mesh.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.importAnimation = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.SaveAndReimport();

        importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.importCameras = false;
        importer.importLights = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.SaveAndReimport();

        float height = MeasureHeight();
        Debug.Log("[SetupZombie] Model height " + height.ToString("0.00") + "m, scale " + importer.globalScale);
        return FindHumanAvatar(ModelPath);
    }

    private static float MeasureHeight()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (asset == null)
            return 0f;

        GameObject temp = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (temp == null)
            return 0f;

        float height = 0f;
        Renderer[] renderers = temp.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            height = bounds.size.y;
        }

        Object.DestroyImmediate(temp);
        return height;
    }

    private static AnimationClip ConfigureClip(string path, Avatar avatar, string clipName, bool loop, bool bakeVertical)
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
        clip.lockRootHeightY = bakeVertical;
        clip.lockRootPositionXZ = false;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        clip.heightFromFeet = false;
        clip.maskType = ClipAnimationMaskType.None;
        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();
        return LoadClip(path, clipName);
    }

    private static AnimationClip LoadClip(string path, string clipName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        AnimationClip fallback = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is not AnimationClip clip)
                continue;
            if (clip.name.StartsWith("__preview__"))
                continue;
            if (clip.name == clipName)
                return clip;
            fallback = clip;
        }

        return fallback;
    }

    private static Avatar FindHumanAvatar(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar avatar && avatar.isHuman && avatar.isValid)
                return avatar;
        }

        return null;
    }

    private static void BuildController(
        AnimationClip crawl,
        AnimationClip stand,
        AnimationClip scream,
        AnimationClip run,
        AnimationClip death,
        float crawlExit)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState crawlState = machine.AddState("Crawl");
        crawlState.motion = crawl;
        AnimatorState standState = machine.AddState("Stand Up");
        standState.motion = stand;
        AnimatorState screamState = machine.AddState("Scream");
        screamState.motion = scream;
        AnimatorState runState = machine.AddState("Running");
        runState.motion = run;
        AnimatorState deathState = machine.AddState("Death");
        deathState.motion = death;
        machine.defaultState = crawlState;

        Link(crawlState, standState, crawlExit);
        Link(standState, screamState, 0.92f);
        Link(screamState, runState, 0.92f);

        AnimatorStateTransition die = machine.AddAnyStateTransition(deathState);
        die.hasExitTime = false;
        die.duration = 0.08f;
        die.hasFixedDuration = true;
        die.canTransitionToSelf = false;
        die.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        EditorUtility.SetDirty(controller);
    }

    private static void Link(AnimatorState from, AnimatorState to, float exitTime)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = 0.15f;
        transition.offset = 0f;
    }

    private static string UpgradeMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            return "materials=no-urp-shader";

        AssetDatabase.Refresh();

        const string textureFolder = "Assets/Game/Models/Characters/Zombie/Textures/";
        PrepareTexture(textureFolder + "zombie_normal.png", true);
        PrepareTexture(textureFolder + "zombie_body_diffuse.png", false);
        PrepareTexture(textureFolder + "zombie_diffuse.png", false);
        PrepareTexture(textureFolder + "zombie_specular.png", false);

        Texture2D body = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + "zombie_body_diffuse.png");
        Texture2D outfit = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + "zombie_diffuse.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + "zombie_normal.png");
        bool outfitCutout = outfit != null && TextureUsesAlpha(textureFolder + "zombie_diffuse.png");

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Game/Models/Characters/Zombie" });
        int textured = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            if (material.shader != lit)
                material.shader = lit;

            Texture2D albedo = material.name.Contains("body") ? body : outfit;
            if (albedo != null)
            {
                material.SetTexture("_BaseMap", albedo);
                material.SetTexture("_MainTex", albedo);
                textured++;
            }

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_Metallic", 0f);
            bool cutout = outfitCutout && albedo == outfit;
            SetSurface(material, cutout);
            EditorUtility.SetDirty(material);
        }

        return "materials=" + guids.Length + " textured=" + textured + " outfitCutout=" + outfitCutout;
    }

    private static void PrepareTexture(string path, bool normal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        bool changed = false;
        if (normal && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            changed = true;
        }

        if (!normal && importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }

        if (importer.maxTextureSize < 2048)
        {
            importer.maxTextureSize = 2048;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static bool TextureUsesAlpha(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return false;

        bool wasReadable = importer.isReadable;
        if (!wasReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            return false;

        Color32[] pixels = texture.GetPixels32();
        int partial = 0;
        int step = Mathf.Max(1, pixels.Length / 8000);
        int sampled = 0;
        for (int i = 0; i < pixels.Length; i += step)
        {
            sampled++;
            if (pixels[i].a < 250)
                partial++;
        }

        if (!wasReadable)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        return sampled > 0 && partial / (float)sampled > 0.02f;
    }

    private static void SetSurface(Material material, bool cutout)
    {
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        material.SetInt("_ZWrite", 1);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        if (cutout)
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        material.SetShaderPassEnabled("ShadowCaster", true);
        material.SetShaderPassEnabled("DepthOnly", true);
    }

    private static float LowestSole(GameObject model)
    {
        float lowest = float.PositiveInfinity;
        SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        Mesh baked = new Mesh();
        for (int i = 0; i < skins.Length; i++)
        {
            SkinnedMeshRenderer skin = skins[i];
            if (skin == null || skin.sharedMesh == null)
                continue;
            skin.BakeMesh(baked);
            Vector3[] vertices = baked.vertices;
            Transform space = skin.transform;
            for (int v = 0; v < vertices.Length; v++)
                lowest = Mathf.Min(lowest, space.TransformPoint(vertices[v]).y);
        }

        Object.DestroyImmediate(baked);
        if (lowest < float.PositiveInfinity)
            return lowest;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                lowest = Mathf.Min(lowest, renderers[i].bounds.min.y);
        }

        return lowest < float.PositiveInfinity ? lowest : model.transform.position.y;
    }

    private static string BuildPrefabAndPlace(Avatar avatar)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (modelAsset == null || controller == null)
            return "prefab=missing-source";

        GameObject root = new GameObject("Zombie");
        GameObject model = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
        model.transform.SetParent(root.transform, false);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float minY = LowestSole(model) - root.transform.position.y;
        float modelHeight = Mathf.Max(0.01f, bounds.size.y);
        float fit = modelHeight > 1.95f || modelHeight < 1.55f ? TargetHeight / modelHeight : 1f;
        model.transform.localScale = Vector3.one * fit;
        model.transform.localPosition = new Vector3(0f, -minY * fit, 0f);

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        float height = Mathf.Clamp(bounds.size.y, 1.4f, 2.2f);
        float radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.45f, 0.22f, 0.42f);
        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = height;
        capsule.radius = radius;
        capsule.center = new Vector3(0f, height * 0.5f, 0f);
        capsule.direction = 1;

        ZombieController zombie = root.AddComponent<ZombieController>();
        SerializedObject serialized = new SerializedObject(zombie);
        serialized.FindProperty("runSpeed").floatValue = 6.2f;
        serialized.FindProperty("standingHeight").floatValue = height;
        serialized.FindProperty("standingRadius").floatValue = radius;
        serialized.FindProperty("standingCenter").vector3Value = capsule.center;
        float crawlHeight = Mathf.Clamp(height * 0.34f, 0.45f, 0.75f);
        serialized.FindProperty("crawlHeight").floatValue = crawlHeight;
        serialized.FindProperty("crawlRadius").floatValue = Mathf.Clamp(radius * 1.15f, 0.28f, 0.48f);
        serialized.FindProperty("crawlCenter").vector3Value = new Vector3(0f, crawlHeight * 0.5f, 0f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        if (prefab == null)
            return "prefab=save-failed";

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 position = new Vector3(3f, 0f, 11f);
        Quaternion rotation = Quaternion.identity;
        if (player != null)
        {
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();
            position = FindClearSpawn(player, forward, out rotation);
        }

        GameObject[] stale = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < stale.Length; i++)
        {
            if (stale[i] == null || stale[i].name != "Zombie")
                continue;
            if (stale[i].GetComponent<ZombieController>() == null)
                continue;
            Object.DestroyImmediate(stale[i]);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        instance.transform.SetPositionAndRotation(position, rotation);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "prefab=ok height=" + height.ToString("0.00")
            + " radius=" + radius.ToString("0.00")
            + " at=" + position.ToString("0.0");
    }

    private static Vector3 FindClearSpawn(GameObject player, Vector3 forward, out Quaternion rotation)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        float footY = player.transform.position.y;
        if (controller != null)
            footY = player.transform.position.y + controller.center.y - controller.height * 0.5f;

        Vector3 best = player.transform.position + forward * 14f;
        best.y = footY;
        float bestScore = float.MaxValue;
        for (int i = 0; i < 18; i++)
        {
            float angle = i == 0 ? 0f : ((i + 1) / 2) * 10f * (i % 2 == 0 ? -1f : 1f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 candidate = player.transform.position + dir * 14f;
            Vector3 origin = new Vector3(candidate.x, footY + 2.5f, candidate.z);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                continue;
            if (Mathf.Abs(hit.point.y - footY) > 0.6f)
                continue;

            Vector3 low = hit.point + Vector3.up * 0.35f;
            Vector3 high = hit.point + Vector3.up * 1.55f;
            Collider[] block = Physics.OverlapCapsule(low, high, 0.45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int blockers = 0;
            for (int b = 0; b < block.Length; b++)
            {
                if (block[b] == null)
                    continue;
                if (block[b].GetComponentInParent<PlayerController>() != null)
                {
                    blockers += 4;
                    continue;
                }

                string hitName = block[b].name.ToLowerInvariant();
                if (hitName.Contains("floor") || hitName.Contains("road") || hitName.Contains("ground"))
                    continue;
                blockers++;
            }

            float score = blockers * 20f + Mathf.Abs(angle);
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = hit.point;
            if (blockers == 0)
                break;
        }

        Vector3 face = player.transform.position - best;
        face.y = 0f;
        rotation = face.sqrMagnitude > 0.001f ? Quaternion.LookRotation(face.normalized, Vector3.up) : Quaternion.identity;
        return best;
    }
}
