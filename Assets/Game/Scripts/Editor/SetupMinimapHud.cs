using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Restyles the minimap (dark disc, cyan rim, player arrow, north marker, scanner grade),
/// adds the objective card under it, and gives each scene its objective and map camera.
/// </summary>
public static class SetupMinimapHud
{
    private const string PlayerPath = "Assets/Game/Prefabs/Characters/Player.prefab";
    private const string UiFolder = "Assets/Game/UI/Minimap/";
    private const string RenderTexturePath = "Assets/Game/Textures/MinimapRenderTexture.renderTexture";
    private const string FacilityScenePath = "Assets/Game/Scenes/Facility_WakeUp.unity";
    private const string YardScenePath = "Assets/Game/Scenes/IndustrialYard_Night.unity";
    private const string LayerName = "Minimap";

    public const string FacilityObjective = "Discover your surroundings";
    public const string YardObjective = "Follow the path";

    private static readonly Color Accent = new Color(0.35f, 0.9f, 1f, 1f);
    private static readonly Color Plate = new Color(0.02f, 0.04f, 0.06f, 0.78f);

    [MenuItem("Tools/Setup Minimap And Objectives")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "minimap: exit play mode first";

        EnsureLayer();
        string prefab = PatchPlayerPrefab();

        EditorSceneManager.OpenScene(FacilityScenePath, OpenSceneMode.Single);
        AddToActiveScene(FacilityObjective, true);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        EditorSceneManager.OpenScene(YardScenePath, OpenSceneMode.Single);
        AddToActiveScene(YardObjective, false);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        return "minimap: " + prefab + " scenes=facility,yard";
    }

    // ---------------------------------------------------------------- scene

    /// <summary>Objective plus a top-down map camera for the open scene. Safe to run again.</summary>
    public static void AddToActiveScene(string objective, bool indoor)
    {
        int layer = EnsureLayer();

        foreach (SceneObjective old in Object.FindObjectsByType<SceneObjective>(FindObjectsInactive.Include))
            Object.DestroyImmediate(old.gameObject);
        var objectiveObject = new GameObject("Scene Objective");
        SceneObjective sceneObjective = objectiveObject.AddComponent<SceneObjective>();
        var so = new SerializedObject(sceneObjective);
        so.FindProperty("objective").stringValue = objective;
        so.ApplyModifiedPropertiesWithoutUndo();

        MinimapFollow follow = Object.FindAnyObjectByType<MinimapFollow>(FindObjectsInactive.Include);
        if (follow == null)
        {
            var go = new GameObject("MiniMapCamera");
            go.AddComponent<Camera>();
            follow = go.AddComponent<MinimapFollow>();
        }

        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            follow.player = player.transform;
            RemoveOldMarker(player.transform);
        }

        // Indoors the camera sits under the ceiling. Outdoors it keeps its authored height.
        Camera cam = follow.GetComponent<Camera>();
        if (indoor)
        {
            follow.height = 3.5f;
            cam.orthographicSize = 9f;
            cam.farClipPlane = 12f;
        }

        cam.orthographic = true;
        cam.nearClipPlane = 0.05f;
        cam.targetTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.035f, 0.05f, 1f);
        cam.depth = -10f;
        AudioListener listener = follow.GetComponent<AudioListener>();
        if (listener != null)
            Object.DestroyImmediate(listener);

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.renderShadows = false;
        data.volumeLayerMask = 1 << layer;

        // A grade only the map camera sees: cold, flat and bright enough to read in the dark.
        foreach (Volume old in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            if (old.name == "Minimap Volume")
                Object.DestroyImmediate(old.gameObject);
        }

        var volumeObject = new GameObject("Minimap Volume");
        volumeObject.layer = layer;
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = MapProfile(indoor);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // An older scene-level dot on the minimap sat over the centre. The arrow replaces it.
    private static void RemoveOldMarker(Transform player)
    {
        Transform panel = Find(player, "MinimapPanel");
        if (panel == null)
            return;

        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            Transform child = panel.GetChild(i);
            if (child.name == "player" && PrefabUtility.IsAddedGameObjectOverride(child.gameObject))
                Object.DestroyImmediate(child.gameObject);
        }
    }

    private static VolumeProfile MapProfile(bool indoor)
    {
        string path = UiFolder + (indoor ? "MinimapIndoor.asset" : "MinimapOutdoor.asset");
        Directory.CreateDirectory(UiFolder);
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(indoor ? 2.3f : 1.4f);
        color.contrast.Override(22f);
        color.saturation.Override(-75f);
        color.colorFilter.Override(new Color(0.72f, 0.93f, 1f));
        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.Neutral);
        foreach (VolumeComponent component in profile.components)
        {
            component.name = component.GetType().Name;
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static int EnsureLayer()
    {
        int existing = LayerMask.NameToLayer(LayerName);
        if (existing >= 0)
            return existing;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 6; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue))
                continue;

            slot.stringValue = LayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }

        throw new InvalidOperationException("No free layer for " + LayerName);
    }

    // ---------------------------------------------------------------- HUD

    private static string PatchPlayerPrefab()
    {
        Sprite circle = MakeSprite("Circle.png", 256, (u, v) => Disc(u, v, 0.98f));
        Sprite ring = MakeSprite("Ring.png", 512, Ring);
        Sprite vignette = MakeSprite("Vignette.png", 256, (u, v) =>
        {
            float r = Radius(u, v);
            return new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, r)) * 0.85f);
        });
        Sprite arrow = MakeSprite("Arrow.png", 128, Arrow);

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            RectTransform panel = Find(root.transform, "MinimapPanel") as RectTransform;
            if (panel == null)
                return "prefab=no-minimap-panel";

            foreach (string name in new[] { "Backdrop", "Vignette", "Arrow", "North", "Objective Card" })
            {
                Transform old = Find(panel, name);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }

            Image backdrop = Child<Image>("Backdrop", panel, new Vector2(272f, 272f));
            backdrop.sprite = circle;
            backdrop.color = Plate;
            backdrop.raycastTarget = false;

            RectTransform border = Find(panel, "Border") as RectTransform;
            if (border != null)
            {
                Image image = border.GetComponent<Image>();
                image.sprite = ring;
                image.color = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
                image.raycastTarget = false;
                border.anchoredPosition = Vector2.zero;
                border.sizeDelta = new Vector2(300f, 300f);
            }

            RectTransform mask = Find(panel, "Mask") as RectTransform;
            if (mask != null)
            {
                mask.sizeDelta = new Vector2(262f, 262f);
                mask.GetComponent<Image>().sprite = circle;
                mask.GetComponent<Mask>().showMaskGraphic = false;
                RectTransform map = Find(mask, "RawImage") as RectTransform;
                if (map != null)
                {
                    map.sizeDelta = new Vector2(262f, 262f);
                    map.GetComponent<RawImage>().color = Color.white;
                }

                Image shade = Child<Image>("Vignette", mask, new Vector2(262f, 262f));
                shade.sprite = vignette;
                shade.raycastTarget = false;
            }

            Image playerArrow = Child<Image>("Arrow", panel, new Vector2(32f, 32f));
            playerArrow.sprite = arrow;
            playerArrow.color = Color.white;
            playerArrow.raycastTarget = false;

            Image north = Child<Image>("North", panel, new Vector2(30f, 30f));
            north.sprite = circle;
            north.color = new Color(0.02f, 0.04f, 0.06f, 0.95f);
            north.raycastTarget = false;
            TMP_Text n = Label("N", (RectTransform)north.transform, "N", 16f, Accent, FontStyles.Bold, 0f);
            Stretch(n.rectTransform, Vector2.zero, Vector2.zero);
            n.alignment = TextAlignmentOptions.Center;

            MinimapHud hud = panel.GetComponent<MinimapHud>();
            if (hud == null)
                hud = panel.gameObject.AddComponent<MinimapHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("northMarker").objectReferenceValue = north.rectTransform;
            hudSo.FindProperty("northRadius").floatValue = 140f;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // Back to front: disc, map, rim and ticks over the map edge, then the markers.
            backdrop.transform.SetAsFirstSibling();
            if (mask != null)
                mask.SetSiblingIndex(1);
            if (border != null)
                border.SetSiblingIndex(2);
            playerArrow.transform.SetAsLastSibling();
            north.transform.SetAsLastSibling();

            BuildObjectiveCard(panel);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            return "prefab=ok";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildObjectiveCard(RectTransform panel)
    {
        var cardObject = new GameObject("Objective Card", typeof(RectTransform));
        RectTransform card = (RectTransform)cardObject.transform;
        card.SetParent(panel, false);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0f, 1f);
        card.anchoredPosition = new Vector2(-150f, -168f);
        card.sizeDelta = new Vector2(330f, 82f);
        CanvasGroup group = cardObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Image body = Child<Image>("Body", card, Vector2.zero);
        Stretch(body.rectTransform, Vector2.zero, Vector2.zero);
        body.color = Plate;
        body.raycastTarget = false;

        Image accent = Child<Image>("Accent", body.rectTransform, Vector2.zero);
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(3f, 0f);
        accentRect.anchoredPosition = Vector2.zero;
        accent.color = Accent;
        accent.raycastTarget = false;

        TMP_Text header = Label("Header", body.rectTransform, "OBJECTIVE", 15f, new Color(Accent.r, Accent.g, Accent.b, 0.9f), FontStyles.Bold, 14f);
        Stretch(header.rectTransform, new Vector2(18f, 50f), new Vector2(-12f, -10f));
        header.alignment = TextAlignmentOptions.TopLeft;

        TMP_Text label = Label("Label", body.rectTransform, FacilityObjective, 22f, new Color(0.93f, 0.96f, 1f, 1f), FontStyles.Normal, 1f);
        Stretch(label.rectTransform, new Vector2(18f, 8f), new Vector2(-12f, -32f));
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;

        ObjectiveCard objective = cardObject.AddComponent<ObjectiveCard>();
        var so = new SerializedObject(objective);
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("body").objectReferenceValue = body.rectTransform;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("accent").objectReferenceValue = accent;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Child<T>(string name, RectTransform parent, Vector2 size) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return go.AddComponent<T>();
    }

    private static TMP_Text Label(string name, RectTransform parent, string text, float size, Color color, FontStyles style, float spacing)
    {
        TextMeshProUGUI label = Child<TextMeshProUGUI>(name, parent, Vector2.zero);
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.characterSpacing = spacing;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    // Insets are left/bottom and right/top offsets from a full stretch.
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }

    // ---------------------------------------------------------------- sprites

    private static float Radius(float u, float v)
    {
        float x = u * 2f - 1f;
        float y = v * 2f - 1f;
        return Mathf.Sqrt(x * x + y * y);
    }

    private static Color Disc(float u, float v, float edge)
    {
        float aa = 2f / 256f;
        return new Color(1f, 1f, 1f, Mathf.Clamp01((edge - Radius(u, v)) / aa));
    }

    // Thin rim with tick marks; every 90 degrees the tick is longer.
    private static Color Ring(float u, float v)
    {
        float r = Radius(u, v);
        float aa = 2f / 512f;
        float rim = Mathf.Clamp01(1f - Mathf.Abs(r - 0.94f) / (0.008f + aa));
        float rimGlow = Mathf.Clamp01(1f - Mathf.Abs(r - 0.94f) / 0.04f) * 0.25f;

        float angle = Mathf.Atan2(v * 2f - 1f, u * 2f - 1f) * Mathf.Rad2Deg + 360f;
        float step = angle % 15f;
        float distance = Mathf.Min(step, 15f - step);
        bool major = Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round(angle / 90f) * 90f)) < 1f;
        float inner = major ? 0.8f : 0.86f;
        float tick = distance < (major ? 0.8f : 0.45f) && r > inner && r < 0.91f ? (major ? 1f : 0.55f) : 0f;

        return new Color(1f, 1f, 1f, Mathf.Clamp01(rim + rimGlow + tick));
    }

    private static Color Arrow(float u, float v)
    {
        // A chevron pointing up with a notch cut from the bottom, outlined dark so it reads on bright ground.
        float x = Mathf.Abs(u - 0.5f) * 2f;
        bool core = Chevron(x, v, 0f);
        bool outline = Chevron(x, v, 0.09f);
        if (core)
            return Color.white;
        return outline ? new Color(0.02f, 0.04f, 0.06f, 0.9f) : Color.clear;
    }

    private static bool Chevron(float x, float y, float grow)
    {
        float outer = 0.86f - x * 0.82f + grow;
        float notch = 0.34f - x * 0.3f - grow;
        return y < outer && y > notch && y > 0.12f - grow;
    }

    private static Sprite MakeSprite(string file, int size, Func<float, float, Color> pixel)
    {
        Directory.CreateDirectory(UiFolder);
        string path = UiFolder + file;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
        }

        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
