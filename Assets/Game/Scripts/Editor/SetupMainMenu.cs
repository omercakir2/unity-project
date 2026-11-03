using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the title screen: the sealed capsule in the dark as a slow-moving backdrop,
/// with the title and menu on the left. The scene goes first in Build Settings.
/// </summary>
public static class SetupMainMenu
{
    private const string ScenePath = "Assets/Game/Scenes/MainMenu.unity";
    private const string FirstScenePath = "Assets/Game/Scenes/Facility_WakeUp.unity";
    private const string ShadePath = "Assets/Game/UI/MenuShade.png";
    private const string LookProfilePath = "Assets/Game/Scenes/Facility_WakeUp/FacilityLook.asset";
    private const string FactoryPrefabs = "Assets/Imported/TirgamesAssets/Factory/Prefabs/";
    private const string CapsuleModelPath = "Assets/Imported/SciFiCapsule/Models/Capsula_Animation.fbx";

    private static readonly Color Accent = new Color(0.35f, 0.9f, 1f, 1f);
    private static readonly Color TitleColor = new Color(0.93f, 0.96f, 1f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.68f, 0.72f, 1f);

    [MenuItem("Tools/Setup Main Menu")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "menu: exit play mode first";

        Texture2D shade = CreateShadeTexture();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        ConfigureRenderSettings();
        Transform capsule = BuildBackdrop();
        BuildCamera(capsule);
        BuildUi(shade);

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutFirstInBuild(ScenePath);
        return "menu: " + ScenePath;
    }

    // ---------------------------------------------------------------- backdrop

    private static void ConfigureRenderSettings()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.04f, 0.048f, 0.058f);
        RenderSettings.reflectionIntensity = 0.3f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.06f;
        RenderSettings.fogColor = new Color(0.015f, 0.02f, 0.025f);
    }

    private static Transform BuildBackdrop()
    {
        Transform room = new GameObject("Backdrop").transform;
        room.gameObject.AddComponent<IndoorScene>();

        Place("Factory2Floor01", new Vector3(0f, 0f, 0f), 0f, room);
        Place("Factory2Floor01", new Vector3(0f, 0f, 12f), 0f, room);
        Place("Factory2Ceiling01", new Vector3(0f, 4.5f, 0f), 0f, room);
        Place("Factory2Ceiling01", new Vector3(0f, 4.5f, 12f), 0f, room);
        Place("Factory2Wall02", new Vector3(-12f, -0.1f, 0f), 180f, room);
        Place("Factory2Wall01", new Vector3(0f, -0.1f, 0f), 90f, room);
        Place("Factory2Wall01", new Vector3(-12f, -0.1f, 12f), 270f, room);
        Place("Factory2Columns01", new Vector3(0f, 0f, 3f), 0f, room);
        Place("PowerBox01_1", new Vector3(-8.6f, 0f, 0.05f), 0f, room);
        Place("PowerBox01_2", new Vector3(-3.4f, 0f, 0.05f), 0f, room);
        Place("MetalCabinet01_1", new Vector3(-11.6f, 0f, 5.5f), 90f, room);
        Place("Barrel01a", new Vector3(-10.6f, 0f, 2.2f), 0f, room);
        Place("Barrel01c", new Vector3(-10.1f, 0f, 1.4f), 30f, room);
        Place("Debris01_12", new Vector3(-4f, 0.01f, 9f), 30f, room);
        Place("Debris01_3", new Vector3(-8.5f, 0f, 8f), 200f, room);
        Place("LampCeiling01", new Vector3(-3f, 4.08f, 9f), 0f, room);
        Place("LampCeiling01", new Vector3(-9f, 4.08f, 9f), 0f, room);

        GameObject capsule = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CapsuleModelPath));
        capsule.name = "Sleep Capsule";
        capsule.transform.SetPositionAndRotation(new Vector3(-6f, 0f, 4.5f), Quaternion.identity);
        capsule.transform.localScale = Vector3.one * 0.82f;

        Light pod = NewLight("Pod Light", LightType.Point, capsule.transform, Vector3.zero, Accent, 2.6f, 5f);
        pod.transform.localPosition = new Vector3(0f, 3.35f, 0.15f);
        AnimationClip clip = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CapsuleModelPath))
        {
            if (asset is AnimationClip c && !c.name.StartsWith("__preview__"))
                clip = c;
        }

        SleepCapsule sleep = capsule.AddComponent<SleepCapsule>();
        var so = new SerializedObject(sleep);
        so.FindProperty("openClip").objectReferenceValue = clip;
        so.FindProperty("podLight").objectReferenceValue = pod;
        so.ApplyModifiedPropertiesWithoutUndo();

        Light spot = NewLight("Capsule Spot", LightType.Spot, room, new Vector3(-6f, 4.3f, 5.8f), new Color(0.7f, 0.85f, 1f), 10f, 9f);
        spot.transform.rotation = Quaternion.LookRotation(new Vector3(-6f, 1.5f, 4.5f) - spot.transform.position);
        spot.spotAngle = 55f;
        spot.innerSpotAngle = 25f;
        spot.shadows = LightShadows.Soft;
        NewLight("Emergency Light", LightType.Point, room, new Vector3(-11f, 3.6f, 1f), new Color(1f, 0.12f, 0.08f), 2f, 6f);
        Light lamp = NewLight("Failing Lamp", LightType.Point, room, new Vector3(-3f, 3.9f, 9f), new Color(0.85f, 0.92f, 1f), 1.6f, 7f);
        lamp.gameObject.AddComponent<LightFlicker>();

        var volume = new GameObject("Global Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookProfilePath);

        return capsule.transform;
    }

    private static void BuildCamera(Transform capsule)
    {
        var go = new GameObject("Menu Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = 38f;
        cam.nearClipPlane = 0.1f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        go.AddComponent<AudioListener>();

        MenuCameraDrift drift = go.AddComponent<MenuCameraDrift>();
        var so = new SerializedObject(drift);
        so.FindProperty("target").objectReferenceValue = capsule;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.transform.position = capsule.position + new Vector3(-2.5f, 1.6f, 4.9f);
        go.transform.LookAt(capsule.position + Vector3.up * 1.8f);
    }

    // ---------------------------------------------------------------- UI

    private static void BuildUi(Texture2D shade)
    {
        var events = new GameObject("EventSystem");
        events.AddComponent<UnityEngine.EventSystems.EventSystem>();
        events.AddComponent<InputSystemUIInputModule>();

        var canvasObject = new GameObject("Menu Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)canvasObject.transform;

        // Darkens the left side so the text reads over the scene.
        RawImage leftShade = Rect<RawImage>("Left Shade", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1250f, 0f));
        leftShade.texture = shade;
        leftShade.color = new Color(0f, 0f, 0f, 0.9f);
        leftShade.raycastTarget = false;

        RectTransform menu = Rect<RectTransform>("Menu", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        CanvasGroup group = menu.gameObject.AddComponent<CanvasGroup>();

        RectTransform titleBlock = Rect<RectTransform>("Title Block", menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(150f, 40f), new Vector2(1000f, 320f));
        Text("Eyebrow", titleBlock, "CRYOGENIC RESEARCH FACILITY 07", 22f, new Color(Accent.r, Accent.g, Accent.b, 0.85f), FontStyles.Bold, 18f, new Vector2(0f, 250f), new Vector2(1000f, 40f));
        Text("Title", titleBlock, "DORMANT", 150f, TitleColor, FontStyles.Bold, 16f, new Vector2(-6f, 95f), new Vector2(1000f, 170f));
        Image rule = Rect<Image>("Rule", titleBlock, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 78f), new Vector2(420f, 2f));
        rule.color = new Color(Accent.r, Accent.g, Accent.b, 0.7f);
        rule.raycastTarget = false;
        Text("Tagline", titleBlock, "You slept for a hundred years. You remember nothing.", 26f, new Color(Muted.r, Muted.g, Muted.b, 0.9f), FontStyles.Italic, 2f, new Vector2(0f, 16f), new Vector2(1000f, 44f));

        RectTransform buttons = Rect<RectTransform>("Buttons", menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(150f, -20f), new Vector2(520f, 150f));
        VerticalLayoutGroup layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        Button play = MenuEntry(buttons, "Play Button", "01", "PLAY");
        Button quit = MenuEntry(buttons, "Quit Button", "02", "QUIT");
        LinkVertical(play, quit);

        TMP_Text readout = Text("Pod Readout", menu, "", 20f, new Color(Accent.r, Accent.g, Accent.b, 0.75f), FontStyles.Normal, 6f, Vector2.zero, new Vector2(760f, 150f));
        RectTransform readoutRect = readout.rectTransform;
        readoutRect.anchorMin = readoutRect.anchorMax = new Vector2(1f, 0f);
        readoutRect.pivot = new Vector2(1f, 0f);
        readoutRect.anchoredPosition = new Vector2(-80f, 60f);
        readout.alignment = TextAlignmentOptions.BottomRight;
        readout.lineSpacing = 12f;

        TMP_Text footer = Text("Footer", menu, "v0.1  //  PROTOTYPE", 16f, new Color(Muted.r, Muted.g, Muted.b, 0.45f), FontStyles.Normal, 8f, Vector2.zero, new Vector2(500f, 30f));
        footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = Vector2.zero;
        footer.rectTransform.pivot = Vector2.zero;
        footer.rectTransform.anchoredPosition = new Vector2(150f, 56f);

        Image fade = Rect<Image>("Fade", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fade.color = Color.black;
        fade.raycastTarget = false;

        MainMenu controller = canvasObject.AddComponent<MainMenu>();
        var so = new SerializedObject(controller);
        so.FindProperty("firstScene").stringValue = FirstScenePath;
        so.FindProperty("playButton").objectReferenceValue = play;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("menuGroup").objectReferenceValue = group;
        so.FindProperty("fade").objectReferenceValue = fade;
        so.FindProperty("readout").objectReferenceValue = readout;
        so.ApplyModifiedPropertiesWithoutUndo();

        events.GetComponent<UnityEngine.EventSystems.EventSystem>().firstSelectedGameObject = play.gameObject;
    }

    private static Button MenuEntry(RectTransform parent, string name, string number, string text)
    {
        Image hit = Rect<Image>(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(520f, 64f));
        hit.color = new Color(0f, 0f, 0f, 0f);
        Button button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;

        Image accent = Rect<Image>("Accent", hit.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 40f));
        accent.raycastTarget = false;

        RectTransform content = Rect<RectTransform>("Content", hit.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        content.offsetMin = new Vector2(24f, 0f);
        content.offsetMax = Vector2.zero;
        TMP_Text index = Text("Index", content, number, 20f, Accent, FontStyles.Bold, 4f, Vector2.zero, new Vector2(48f, 64f));
        index.alignment = TextAlignmentOptions.MidlineLeft;
        Center(index.rectTransform, 0f);
        TMP_Text label = Text("Label", content, text, 40f, Muted, FontStyles.Bold, 14f, Vector2.zero, new Vector2(420f, 64f));
        label.alignment = TextAlignmentOptions.MidlineLeft;
        Center(label.rectTransform, 56f);

        MenuButton fx = hit.gameObject.AddComponent<MenuButton>();
        var so = new SerializedObject(fx);
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("index").objectReferenceValue = index;
        so.FindProperty("accent").objectReferenceValue = accent;
        so.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    private static void LinkVertical(Button top, Button bottom)
    {
        Navigation a = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = bottom, selectOnUp = bottom };
        Navigation b = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = top, selectOnDown = top };
        top.navigation = a;
        bottom.navigation = b;
    }

    private static void Center(RectTransform rect, float x)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
    }

    private static TMP_Text Text(string name, RectTransform parent, string value, float size, Color color, FontStyles style, float spacing, Vector2 position, Vector2 box)
    {
        TextMeshProUGUI text = Rect<TextMeshProUGUI>(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, position, box);
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.characterSpacing = spacing;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static T Rect<T>(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return typeof(T) == typeof(RectTransform) ? rect as T : go.AddComponent<T>();
    }

    // Black on the left fading to clear, eased so the edge never shows.
    private static Texture2D CreateShadeTexture()
    {
        if (!File.Exists(ShadePath))
        {
            const int width = 256;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 0.85f));
                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
            }

            texture.Apply();
            File.WriteAllBytes(ShadePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(ShadePath);
        }

        var importer = AssetImporter.GetAtPath(ShadePath) as TextureImporter;
        if (importer != null && (importer.wrapMode != TextureWrapMode.Clamp || importer.mipmapEnabled))
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(ShadePath);
    }

    // ---------------------------------------------------------------- helpers

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
        return light;
    }

    private static GameObject Place(string prefab, Vector3 position, float yaw, Transform parent)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(FactoryPrefabs + prefab + ".prefab");
        if (asset == null)
        {
            Debug.LogWarning("[MainMenu] Missing prefab " + prefab);
            return new GameObject(prefab + " (missing)");
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return go;
    }

    private static void PutFirstInBuild(string path)
    {
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path != path)
                scenes.Add(s);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
