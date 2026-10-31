using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

public static class ApplyVisualPass
{
    private const string BarPath = "Assets/Game/UI/Bar.png";
    private const string PlayerPath = "Assets/Game/Prefabs/Characters/Player.prefab";
    private const string RemyPath = "Assets/Game/Models/Characters/Remy.fbx";
    private const string TextureFolder = "Assets/Game/Models/Characters/Remy/Textures";
    private const string MaterialFolder = "Assets/Game/Materials/Remy";
    private const string HairCompositePath = "Assets/Game/Models/Characters/Remy/Textures/Remy_Hair_Cutout.png";
    private const string TorchModelPath = "Assets/Game/Models/Items/WoodenTorch_Fire.fbx";
    private const string TorchPrefabPath = "Assets/Game/Prefabs/Items/Torch.prefab";
    private const string TorchMaterialFolder = "Assets/Game/Materials/Torch";
    private const string ClipPath = "Assets/Game/Animations/Player/Remy@Standing Torch Walk Forward.fbx";
    private const string ControllerPath = "Assets/Game/Animations/Controllers/MaleAnimator.controller";
    private const string MaskPath = "Assets/Game/Animations/Player/HoldUpperBody.mask";

    [MenuItem("Tools/Apply Visual Pass")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        var report = new StringBuilder();
        ConfigureBarSprite();
        report.AppendLine(PatchBars());
        report.AppendLine(PaintRemy());
        report.AppendLine(RebuildTorch());
        report.AppendLine(SetupHoldAnimation());
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    private static void ConfigureBarSprite()
    {
        var importer = AssetImporter.GetAtPath(BarPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.spritePixelsPerUnit = 8f;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.spriteBorder = Vector4.zero;
        importer.SaveAndReimport();
    }

    private static string PatchBars()
    {
        Sprite bar = AssetDatabase.LoadAssetAtPath<Sprite>(BarPath);
        if (bar == null)
            return "bars: missing-sprite";

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            int patched = 0;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != "HealthRow" && all[i].name != "EnergyRow")
                    continue;

                Transform trackTransform = all[i].Find("Track");
                Transform fillTransform = trackTransform != null ? trackTransform.Find("Fill") : null;
                Transform valueTransform = all[i].Find("Value");
                if (trackTransform == null || fillTransform == null || valueTransform == null)
                    return "bars: missing-row " + all[i].name;

                UnityEngine.UI.Image track = trackTransform.GetComponent<UnityEngine.UI.Image>();
                UnityEngine.UI.Image fill = fillTransform.GetComponent<UnityEngine.UI.Image>();
                track.sprite = bar;
                track.type = UnityEngine.UI.Image.Type.Simple;
                fill.sprite = bar;
                fill.type = UnityEngine.UI.Image.Type.Filled;
                fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;

                TMP_Text value = valueTransform.GetComponent<TMP_Text>();
                if (value != null)
                    value.text = "100";

                UnityEngine.UI.LayoutElement layout = valueTransform.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layout != null)
                {
                    layout.preferredWidth = 72f;
                    layout.minWidth = 72f;
                }

                patched++;
            }

            if (patched != 2)
                return "bars: patched " + patched;

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            return "bars: ok";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string PaintRemy()
    {
        Directory.CreateDirectory(MaterialFolder);
        MarkTexture(TextureFolder + "/Remy_Body_Diffuse.png", false);
        MarkTexture(TextureFolder + "/Remy_Top_Diffuse.png", false);
        MarkTexture(TextureFolder + "/Remy_Bottom_Diffuse.png", false);
        MarkTexture(TextureFolder + "/Remy_Shoes_Diffuse.png", false);
        MarkTexture(TextureFolder + "/Remy_Hair_Diffuse.png", false);
        MarkTexture(TextureFolder + "/Remy_Hair_Opacity.png", false);
        MarkTexture(TextureFolder + "/Remy_Body_Normal.png", true);
        MarkTexture(TextureFolder + "/Remy_Top_Normal.png", true);
        MarkTexture(TextureFolder + "/Remy_Bottom_Normal.png", true);
        MarkTexture(TextureFolder + "/Remy_Shoes_Normal.png", true);
        MarkTexture(TextureFolder + "/Remy_Hair_Normal.png", true);
        CompositeHair();
        MarkTexture(HairCompositePath, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return "remy: missing-shader";

        Material body = LitMaterial(shader, "Bodymat", TextureFolder + "/Remy_Body_Diffuse.png", TextureFolder + "/Remy_Body_Normal.png", false);
        Material top = LitMaterial(shader, "Topmat", TextureFolder + "/Remy_Top_Diffuse.png", TextureFolder + "/Remy_Top_Normal.png", false);
        Material bottom = LitMaterial(shader, "Bottommat", TextureFolder + "/Remy_Bottom_Diffuse.png", TextureFolder + "/Remy_Bottom_Normal.png", false);
        Material shoes = LitMaterial(shader, "Shoesmat", TextureFolder + "/Remy_Shoes_Diffuse.png", TextureFolder + "/Remy_Shoes_Normal.png", false);
        Material hair = LitMaterial(shader, "Hairmat", HairCompositePath, TextureFolder + "/Remy_Hair_Normal.png", true);
        Material lashes = LitMaterial(shader, "Eyelashmat", HairCompositePath, null, true);
        lashes.SetFloat("_Cutoff", 0.5f);

        var importer = AssetImporter.GetAtPath(RemyPath) as ModelImporter;
        if (importer == null)
            return "remy: missing-importer";

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        Remap(importer, "Bodymat", body);
        Remap(importer, "Bottommat", bottom);
        Remap(importer, "Eyelashmat", lashes);
        Remap(importer, "Hairmat", hair);
        Remap(importer, "Shoesmat", shoes);
        Remap(importer, "Topmat", top);
        importer.SaveAndReimport();

        GameObject remy = AssetDatabase.LoadAssetAtPath<GameObject>(RemyPath);
        var check = new StringBuilder();
        check.Append("remy:");
        SkinnedMeshRenderer[] renderers = remy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            Texture albedo = mat != null && mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            check.Append(" ").Append(renderers[i].name).Append("=").Append(mat != null ? mat.name : "null");
            check.Append(albedo != null ? "+tex" : "+white");
        }

        return check.ToString();
    }

    public static string RebuildTorch()
    {
        Directory.CreateDirectory(TorchMaterialFolder);
        var modelImporter = AssetImporter.GetAtPath(TorchModelPath) as ModelImporter;
        if (modelImporter == null)
            return "torch: missing-model";

        modelImporter.globalScale = 1f;
        modelImporter.importAnimation = false;
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        modelImporter.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TorchModelPath);
        if (model == null)
            return "torch: import-failed";

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material wood = TorchMaterial(shader, "TorchWood", new Color(0.34f, 0.18f, 0.08f), 0f, 0.18f, Color.black);
        Material wrap = TorchMaterial(shader, "TorchWrap", new Color(0.62f, 0.42f, 0.16f), 0f, 0.25f, Color.black);
        Material metal = TorchMaterial(shader, "TorchMetal", new Color(0.45f, 0.42f, 0.38f), 0.65f, 0.45f, Color.black);
        Material fire = TorchMaterial(shader, "TorchFire", new Color(1f, 0.42f, 0.08f), 0f, 0.2f, new Color(1f, 0.38f, 0.05f) * 5f);

        float length = ModelLength(model);
        float scale = length > 0.001f ? 1.35f / length : 1f;

        GameObject root = PrefabUtility.LoadPrefabContents(TorchPrefabPath);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            Component[] components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is Transform || components[i] is Light || components[i] is TorchFlicker)
                    continue;
                Object.DestroyImmediate(components[i]);
            }

            GameObject visual = Object.Instantiate(model, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * scale;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            var names = new StringBuilder();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] shared = renderers[i].sharedMaterials;
                for (int m = 0; m < shared.Length; m++)
                {
                    string materialName = shared[m] != null ? shared[m].name : "";
                    names.Append(materialName).Append(" ");
                    if (materialName.Contains("Fire"))
                        shared[m] = fire;
                    else if (materialName.Contains("Yellow"))
                        shared[m] = wrap;
                    else if (materialName.Contains("Grey") || materialName.Contains("Gray"))
                        shared[m] = metal;
                    else
                        shared[m] = wood;
                }

                renderers[i].sharedMaterials = shared;
                renderers[i].shadowCastingMode = ShadowCastingMode.On;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                Object.DestroyImmediate(colliders[i]);

            TorchFlicker oldFlicker = root.GetComponent<TorchFlicker>();
            if (oldFlicker != null)
                Object.DestroyImmediate(oldFlicker);
            Light oldLight = root.GetComponent<Light>();
            if (oldLight != null)
                Object.DestroyImmediate(oldLight);

            Transform flame = root.transform.Find("FlameLight");
            if (flame == null)
            {
                var flameObject = new GameObject("FlameLight");
                flameObject.transform.SetParent(root.transform, false);
                flame = flameObject.transform;
            }

            flame.localPosition = new Vector3(0f, 0f, 0.78f);
            flame.localRotation = Quaternion.identity;
            Light torchLight = flame.GetComponent<Light>();
            if (torchLight == null)
                torchLight = flame.gameObject.AddComponent<Light>();
            torchLight.type = LightType.Point;
            torchLight.color = new Color(1f, 0.55f, 0.15f);
            torchLight.intensity = 4.5f;
            torchLight.range = 9f;
            torchLight.shadows = LightShadows.Soft;
            torchLight.shadowStrength = 0.35f;

            TorchFlicker flicker = flame.GetComponent<TorchFlicker>();
            if (flicker == null)
                flicker = flame.gameObject.AddComponent<TorchFlicker>();
            flicker.baseIntensity = 4.5f;
            flicker.flickerIntensity = 2.2f;
            flicker.flickerSpeed = 12f;

            PrefabUtility.SaveAsPrefabAsset(root, TorchPrefabPath);
            return "torch: ok length=" + length.ToString("0.00") + " scale=" + scale.ToString("0.00") + " mats=" + names;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string SetupHoldAnimation()
    {
        Avatar avatar = null;
        Object[] remyAssets = AssetDatabase.LoadAllAssetsAtPath(RemyPath);
        for (int i = 0; i < remyAssets.Length; i++)
        {
            if (remyAssets[i] is Avatar candidate && candidate.isHuman)
                avatar = candidate;
        }

        if (avatar == null)
            return "hold: missing-avatar";

        var importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
        if (importer == null)
            return "hold: missing-clip";

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            return "hold: no-take";

        ModelImporterClipAnimation clip = clips[0];
        clip.name = "Torch Walk";
        clip.loopTime = true;
        clip.loopPose = true;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = false;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = false;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = false;
        clip.heightFromFeet = false;
        clip.maskType = ClipAnimationMaskType.None;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        AnimationClip motion = null;
        Object[] clipAssets = AssetDatabase.LoadAllAssetsAtPath(ClipPath);
        for (int i = 0; i < clipAssets.Length; i++)
        {
            if (clipAssets[i] is AnimationClip candidate && !candidate.name.StartsWith("__"))
                motion = candidate;
        }

        if (motion == null)
            return "hold: clip-not-imported";

        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, MaskPath);
        }

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        EditorUtility.SetDirty(mask);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return "hold: missing-controller";

        bool hasRate = false;
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == "HoldRate")
                hasRate = true;
        }

        if (!hasRate)
            controller.AddParameter("HoldRate", AnimatorControllerParameterType.Float);

        int holdIndex = -1;
        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == "Hold")
                holdIndex = i;
        }

        if (holdIndex < 0)
        {
            controller.AddLayer("Hold");
            layers = controller.layers;
            holdIndex = layers.Length - 1;
        }

        AnimatorControllerLayer layer = layers[holdIndex];
        layer.avatarMask = mask;
        layer.defaultWeight = 0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.iKPass = false;

        AnimatorStateMachine machine = layer.stateMachine;
        AnimatorState state = null;
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == "Torch Hold")
                state = states[i].state;
        }

        if (state == null)
            state = machine.AddState("Torch Hold", new Vector3(280f, 40f, 0f));

        state.motion = motion;
        state.speed = 1f;
        state.speedParameterActive = true;
        state.speedParameter = "HoldRate";
        state.writeDefaultValues = false;
        machine.defaultState = state;
        layers[holdIndex] = layer;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        return "hold: ok clip=" + motion.name + " length=" + motion.length.ToString("0.00");
    }

    private static void CompositeHair()
    {
        Texture2D diffuse = LoadReadable(TextureFolder + "/Remy_Hair_Diffuse.png");
        Texture2D opacity = LoadReadable(TextureFolder + "/Remy_Hair_Opacity.png");
        Color32[] colors = diffuse.GetPixels32();
        Color32[] mask = opacity.GetPixels32();
        int count = Mathf.Min(colors.Length, mask.Length);
        for (int i = 0; i < count; i++)
            colors[i].a = mask[i].r;

        var cutout = new Texture2D(diffuse.width, diffuse.height, TextureFormat.RGBA32, false);
        cutout.SetPixels32(colors);
        cutout.Apply(false, false);
        File.WriteAllBytes(HairCompositePath, cutout.EncodeToPNG());
        Object.DestroyImmediate(cutout);
        Object.DestroyImmediate(diffuse);
        Object.DestroyImmediate(opacity);
        AssetDatabase.ImportAsset(HairCompositePath);
    }

    private static Texture2D LoadReadable(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.SetPixels32(source.GetPixels32());
        copy.Apply(false, false);
        return copy;
    }

    private static void MarkTexture(string path, bool normal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = !normal;
        importer.SaveAndReimport();
    }

    private static Material LitMaterial(Shader shader, string materialName, string albedoPath, string normalPath, bool cutout)
    {
        string path = MaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", cutout ? 0.15f : 0.28f);
        Texture albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        material.SetTexture("_BaseMap", albedo);
        if (!string.IsNullOrEmpty(normalPath))
        {
            Texture normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale", 1f);
        }

        if (cutout)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.35f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.doubleSidedGI = true;
        }
        else
        {
            material.SetFloat("_AlphaClip", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material TorchMaterial(Shader shader, string materialName, Color color, float metallic, float smoothness, Color emission)
    {
        string path = TorchMaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.SetTexture("_BaseMap", null);
        if (emission.maxColorComponent > 0.01f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Remap(ModelImporter importer, string name, Material material)
    {
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
    }

    private static float ModelLength(GameObject model)
    {
        float longest = 0.01f;
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
                continue;
            Vector3 size = filters[i].sharedMesh.bounds.size;
            float axis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (axis > longest)
                longest = axis;
        }

        return longest;
    }
}
