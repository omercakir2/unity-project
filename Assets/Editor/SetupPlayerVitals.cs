using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupPlayerVitals
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string ControllerPath = "Assets/AnimatorControllers/MaleAnimator.controller";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string BarSpritePath = "Assets/UI/Bar.png";

    private static readonly Color TrackColor = new Color(0.08f, 0.08f, 0.08f, 0.72f);
    private static readonly Color HealthColor = new Color(0.35f, 0.75f, 0.4f, 1f);
    private static readonly Color EnergyColor = new Color(0.95f, 0.72f, 0.25f, 1f);
    private static readonly Color CardColor = new Color(0.12f, 0.1f, 0.08f, 0.94f);
    private static readonly Color ButtonColor = new Color(0.93f, 0.86f, 0.72f, 1f);
    private static readonly Color InkColor = new Color(0.15f, 0.12f, 0.1f, 1f);

    [MenuItem("Tools/Setup Player Vitals")]
    public static void SetupFromMenu()
    {
        Debug.Log("[Vitals] " + Setup());
    }

    [MenuItem("Tools/Player/Swap Model")]
    public static void OpenSwapWindow()
    {
        PlayerModelSwapWindow.Open();
    }

    public static string Setup()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            return "missing-tmp-font";

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
            return "missing-animator-controller";

        EnsurePlayableScenes();

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform slot = EnsureModelSlot(root.transform);
            Transform visual = FindLooseVisual(root.transform, slot);
            if (visual != null && visual.parent != slot)
                visual.SetParent(slot, false);

            if (slot.childCount == 0)
                return "missing-model";

            PlayerVitals vitals = root.GetComponent<PlayerVitals>();
            if (vitals == null)
                vitals = root.AddComponent<PlayerVitals>();

            SerializedObject vitalsObject = new SerializedObject(vitals);
            vitalsObject.FindProperty("healthRegenIdlePerSecond").floatValue = 3f;
            vitalsObject.FindProperty("healthRegenWalkPerSecond").floatValue = 1.25f;
            vitalsObject.FindProperty("sprintDrainPerSecond").floatValue = 20f;
            vitalsObject.FindProperty("energyRegenIdlePerSecond").floatValue = 4f;
            vitalsObject.FindProperty("energyRegenWalkPerSecond").floatValue = 2f;
            vitalsObject.FindProperty("jumpEnergyCostMin").floatValue = 20f;
            vitalsObject.FindProperty("jumpEnergyCostMax").floatValue = 25f;
            vitalsObject.ApplyModifiedPropertiesWithoutUndo();

            PlayerAvatar avatar = root.GetComponent<PlayerAvatar>();
            if (avatar == null)
                avatar = root.AddComponent<PlayerAvatar>();

            GameObject modelSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(slot.GetChild(0).gameObject) as GameObject;
            SerializedObject avatarObject = new SerializedObject(avatar);
            avatarObject.FindProperty("modelSlot").objectReferenceValue = slot;
            avatarObject.FindProperty("animatorController").objectReferenceValue = controller;
            if (modelSource != null)
                avatarObject.FindProperty("modelPrefab").objectReferenceValue = modelSource;
            avatarObject.ApplyModifiedPropertiesWithoutUndo();

            avatar.Bind(false);

            Transform canvas = EnsureCanvas(root.transform);
            BuildHud(canvas, vitals, font);

            Transform cameraTarget = root.transform.Find("CameraTarget");
            if (cameraTarget != null)
                cameraTarget.SetSiblingIndex(0);
            slot.SetSiblingIndex(1);
            canvas.SetSiblingIndex(root.transform.childCount - 1);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return "ok";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static string SwapModel(GameObject model)
    {
        if (model == null)
            return "missing-model";
        if (!IsHumanoid(model))
            return "not-humanoid";

        if (EditorApplication.isPlaying)
        {
            PlayerAvatar live = Object.FindAnyObjectByType<PlayerAvatar>();
            if (live == null)
                return "no-avatar";

            live.Swap(model);
            return "swapped-playmode";
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerAvatar avatar = root.GetComponent<PlayerAvatar>();
            Transform slot = root.transform.Find("ModelSlot");
            if (avatar == null || slot == null)
                return "run-setup-first";

            for (int i = slot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(slot.GetChild(i).gameObject);

            GameObject instance = PrefabUtility.InstantiatePrefab(model, slot) as GameObject;
            if (instance == null)
                return "instantiate-failed";

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            SerializedObject avatarObject = new SerializedObject(avatar);
            avatarObject.FindProperty("modelPrefab").objectReferenceValue = model;
            avatarObject.ApplyModifiedPropertiesWithoutUndo();

            avatar.Bind(true);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return "swapped";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsurePlayableScenes()
    {
        string[] wanted =
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/Scenes/Demo_01.unity",
            "Assets/Scenes/Demo_01_Night.unity"
        };

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int w = 0; w < wanted.Length; w++)
        {
            bool found = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != wanted[w])
                    continue;

                scenes[i] = new EditorBuildSettingsScene(wanted[w], true);
                found = true;
                break;
            }

            if (!found && AssetDatabase.LoadAssetAtPath<SceneAsset>(wanted[w]) != null)
                scenes.Add(new EditorBuildSettingsScene(wanted[w], true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static Transform EnsureModelSlot(Transform root)
    {
        Transform slot = root.Find("ModelSlot");
        if (slot != null)
            return slot;

        GameObject slotObject = new GameObject("ModelSlot");
        slotObject.transform.SetParent(root, false);
        return slotObject.transform;
    }

    private static Transform FindLooseVisual(Transform root, Transform slot)
    {
        if (slot != null)
        {
            for (int i = 0; i < slot.childCount; i++)
            {
                if (slot.GetChild(i).GetComponentInChildren<Animator>(true) != null)
                    return slot.GetChild(i);
            }
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == slot)
                continue;
            if (child.name == "CameraTarget" || child.name == "InventoryCanvas" || child.name == "ModelSlot")
                continue;
            if (child.GetComponentInChildren<Animator>(true) != null)
                return child;
        }

        return null;
    }

    private static Transform EnsureCanvas(Transform root)
    {
        Transform canvasTransform = root.Find("InventoryCanvas");
        if (canvasTransform != null)
            return canvasTransform;

        GameObject canvasObject = new GameObject("InventoryCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(root, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject.transform;
    }

    private static void BuildHud(Transform canvas, PlayerVitals vitals, TMP_FontAsset font)
    {
        DestroyChild(canvas, "Vitals");
        DestroyChild(canvas, "DeathPanel");

        Sprite panel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite bar = AssetDatabase.LoadAssetAtPath<Sprite>(BarSpritePath);
        if (bar == null)
            bar = panel;

        GameObject vitalsRoot = CreateUiObject("Vitals", canvas);
        RectTransform vitalsRect = vitalsRoot.GetComponent<RectTransform>();
        vitalsRect.anchorMin = new Vector2(0f, 1f);
        vitalsRect.anchorMax = new Vector2(0f, 1f);
        vitalsRect.pivot = new Vector2(0f, 1f);
        vitalsRect.anchoredPosition = new Vector2(32f, -32f);
        vitalsRect.sizeDelta = new Vector2(600f, 100f);

        VerticalLayoutGroup column = vitalsRoot.AddComponent<VerticalLayoutGroup>();
        column.spacing = 12f;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        BarWidgets health = CreateBar(vitalsRoot.transform, "HealthRow", "Health", bar, font, HealthColor);
        BarWidgets energy = CreateBar(vitalsRoot.transform, "EnergyRow", "Energy", bar, font, EnergyColor);

        GameObject death = CreateUiObject("DeathPanel", canvas);
        RectTransform deathRect = death.GetComponent<RectTransform>();
        deathRect.anchorMin = Vector2.zero;
        deathRect.anchorMax = Vector2.one;
        deathRect.offsetMin = Vector2.zero;
        deathRect.offsetMax = Vector2.zero;

        Image dim = CreateImage("Dim", death.transform, panel, new Color(0f, 0f, 0f, 0.62f));
        dim.raycastTarget = true;
        Stretch(dim.rectTransform, 0f);

        Image card = CreateImage("Card", death.transform, panel, CardColor);
        RectTransform cardRect = card.rectTransform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(440f, 200f);

        TextMeshProUGUI title = CreateText("Title", card.transform, font, "You died", 36f, Color.white, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(380f, 52f);

        Image buttonImage = CreateImage("RestartButton", card.transform, panel, ButtonColor);
        buttonImage.raycastTarget = true;
        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -28f);
        buttonRect.sizeDelta = new Vector2(220f, 52f);
        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        TextMeshProUGUI buttonLabel = CreateText("Label", buttonImage.transform, font, "Restart", 24f, InkColor, TextAlignmentOptions.Center);
        Stretch(buttonLabel.rectTransform, 0f);

        death.SetActive(false);

        VitalsHud hud = vitalsRoot.AddComponent<VitalsHud>();
        SerializedObject hudObject = new SerializedObject(hud);
        hudObject.FindProperty("vitals").objectReferenceValue = vitals;
        hudObject.FindProperty("healthFill").objectReferenceValue = health.Fill;
        hudObject.FindProperty("energyFill").objectReferenceValue = energy.Fill;
        hudObject.FindProperty("healthValue").objectReferenceValue = health.Value;
        hudObject.FindProperty("energyValue").objectReferenceValue = energy.Value;
        hudObject.FindProperty("deathPanel").objectReferenceValue = death;
        hudObject.FindProperty("restartButton").objectReferenceValue = button;
        hudObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private struct BarWidgets
    {
        public Image Fill;
        public TMP_Text Value;
    }

    private static BarWidgets CreateBar(Transform parent, string rowName, string label, Sprite panel, TMP_FontAsset font, Color fillColor)
    {
        GameObject row = CreateUiObject(rowName, parent);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 44f;
        rowLayout.minHeight = 44f;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI name = CreateText("Label", row.transform, font, label, 26f, Color.white, TextAlignmentOptions.MidlineLeft);
        LayoutElement nameLayout = name.gameObject.AddComponent<LayoutElement>();
        nameLayout.preferredWidth = 108f;
        nameLayout.minWidth = 108f;

        Image track = CreateImage("Track", row.transform, panel, TrackColor);
        track.type = Image.Type.Simple;
        LayoutElement trackLayout = track.gameObject.AddComponent<LayoutElement>();
        trackLayout.preferredWidth = 340f;
        trackLayout.preferredHeight = 30f;
        trackLayout.minHeight = 30f;

        Image fill = CreateImage("Fill", track.transform, panel, fillColor);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        Stretch(fill.rectTransform, 3f);

        TextMeshProUGUI value = CreateText("Value", row.transform, font, "100", 24f, Color.white, TextAlignmentOptions.MidlineRight);
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 72f;
        valueLayout.minWidth = 72f;

        return new BarWidgets { Fill = fill, Value = value };
    }

    private static bool IsHumanoid(GameObject model)
    {
        string path = AssetDatabase.GetAssetPath(model);
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null)
            return importer.animationType == ModelImporterAnimationType.Human;

        Animator animator = model.GetComponentInChildren<Animator>(true);
        return animator != null && animator.avatar != null && animator.avatar.isHuman;
    }

    private static void DestroyChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = CreateUiObject(objectName, parent);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiObject(objectName, parent);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}

public class PlayerModelSwapWindow : EditorWindow
{
    private GameObject model;

    public static void Open()
    {
        GetWindow<PlayerModelSwapWindow>("Swap Player Model");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Humanoid Mixamo model", EditorStyles.boldLabel);
        model = (GameObject)EditorGUILayout.ObjectField("Model", model, typeof(GameObject), false);
        EditorGUILayout.HelpBox("Import the FBX as Humanoid with Create From This Model. The shared MaleAnimator stays on the new mesh.", MessageType.Info);

        using (new EditorGUI.DisabledScope(model == null))
        {
            if (GUILayout.Button("Swap Model"))
                Debug.Log("[PlayerAvatar] " + SetupPlayerVitals.SwapModel(model));
        }
    }
}

[CustomEditor(typeof(PlayerAvatar))]
public class PlayerAvatarEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Swap Model"))
            PlayerModelSwapWindow.Open();
    }
}
