using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

public static class SetupInventoryHotbar
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string TorchPrefabPath = "Assets/Prefabs/Torch.prefab";
    private const string SearchlightPrefabPath = "Assets/Prefabs/Searchlight.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Setup Inventory Hotbar")]
    public static void SetupFromMenu()
    {
        Debug.Log(Setup());
    }

    public static string Setup()
    {
        if (!HasTmpFont())
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            CloseTmpWindows();
            if (!HasTmpFont())
                return "waiting-for-tmp";
        }

        CloseTmpWindows();

        GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefabPath);
        if (torchPrefab == null)
            return "missing-torch-prefab";

        GameObject searchlightPrefab = CreateSearchlightPrefab();
        if (searchlightPrefab == null)
            return "missing-searchlight-prefab";

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerInputActions.inputactions");
        if (actions == null || actions.FindAction("Hotbar1", false) == null)
            return "missing-hotbar-actions";

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerInventory inventory = root.GetComponent<PlayerInventory>();
            if (inventory == null)
                inventory = root.AddComponent<PlayerInventory>();
            inventory.ApplyDefaultState();

            HeldItemVisual held = root.GetComponent<HeldItemVisual>();
            if (held == null)
                held = root.AddComponent<HeldItemVisual>();

            SerializedObject heldObject = new SerializedObject(held);
            heldObject.FindProperty("inventory").objectReferenceValue = inventory;
            heldObject.FindProperty("torchPrefab").objectReferenceValue = torchPrefab;
            heldObject.FindProperty("searchlightPrefab").objectReferenceValue = searchlightPrefab;
            heldObject.ApplyModifiedPropertiesWithoutUndo();

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            var canvases = new List<GameObject>();
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == "InventoryCanvas")
                    canvases.Add(transforms[i].gameObject);
            }
            for (int i = 0; i < canvases.Count; i++)
                Object.DestroyImmediate(canvases[i]);

            InventoryHotbarView view = BuildHotbar(root.transform, inventory);
            WireInput(root.GetComponent<PlayerInput>(), inventory, actions);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[Inventory] Hotbar saved on Player prefab.");
            return view == null ? "missing-view" : "ok";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool HasTmpFont()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null;
    }

    private static void CloseTmpWindows()
    {
        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        for (int i = 0; i < windows.Length; i++)
        {
            string typeName = windows[i].GetType().Name;
            if (typeName.Contains("TMP_PackageResourceImporter") || typeName.Contains("TMP_Importer"))
                windows[i].Close();
        }
    }

    private static InventoryHotbarView BuildHotbar(Transform player, PlayerInventory inventory)
    {
        Sprite panel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject canvasGo = new GameObject("InventoryCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(player, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject hotbar = CreateUiObject("Hotbar", canvasGo.transform);
        RectTransform hotbarRect = hotbar.GetComponent<RectTransform>();
        hotbarRect.anchorMin = new Vector2(0.5f, 0f);
        hotbarRect.anchorMax = new Vector2(0.5f, 0f);
        hotbarRect.pivot = new Vector2(0.5f, 0f);
        hotbarRect.anchoredPosition = new Vector2(0f, 24f);
        hotbarRect.sizeDelta = new Vector2(328f, 88f);

        HorizontalLayoutGroup layout = hotbar.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject crosshair = CreateCrosshair(canvasGo.transform, panel);

        InventoryHotbarView view = hotbar.AddComponent<InventoryHotbarView>();
        SerializedObject viewObject = new SerializedObject(view);
        viewObject.FindProperty("inventory").objectReferenceValue = inventory;
        viewObject.FindProperty("crosshair").objectReferenceValue = crosshair;
        SerializedProperty slots = viewObject.FindProperty("slots");
        slots.arraySize = 4;

        for (int i = 0; i < 4; i++)
        {
            string itemId = i == 0 ? PlayerInventory.TorchItemId : i == 1 ? PlayerInventory.SearchlightItemId : null;
            SlotParts parts = CreateSlot(hotbar.transform, panel, knob, font, i + 1, itemId, i == 0);
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("background").objectReferenceValue = parts.Background;
            slot.FindPropertyRelative("highlight").objectReferenceValue = parts.Highlight;
            slot.FindPropertyRelative("torchGlyph").objectReferenceValue = parts.TorchGlyph;
            slot.FindPropertyRelative("searchlightGlyph").objectReferenceValue = parts.SearchlightGlyph;
        }

        viewObject.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private struct SlotParts
    {
        public Image Background;
        public Image Highlight;
        public GameObject TorchGlyph;
        public GameObject SearchlightGlyph;
    }

    private static SlotParts CreateSlot(Transform parent, Sprite panel, Sprite knob, TMP_FontAsset font, int number, string itemId, bool active)
    {
        GameObject slot = CreateUiObject("Slot" + number, parent);
        LayoutElement element = slot.AddComponent<LayoutElement>();
        element.preferredWidth = 72f;
        element.preferredHeight = 72f;
        element.minWidth = 72f;
        element.minHeight = 72f;

        bool occupied = !string.IsNullOrEmpty(itemId);
        Image highlight = CreateImage("Highlight", slot.transform, panel, active
            ? new Color(1f, 0.72f, 0.28f, 1f)
            : new Color(1f, 1f, 1f, 0.16f));
        Stretch(highlight.rectTransform, 0f);

        Image background = CreateImage("Background", slot.transform, panel, occupied
            ? new Color(0.16f, 0.12f, 0.08f, 0.82f)
            : new Color(0.08f, 0.08f, 0.08f, 0.55f));
        Stretch(background.rectTransform, 4f);

        GameObject glyph = CreateUiObject("TorchGlyph", slot.transform);
        RectTransform glyphRect = glyph.GetComponent<RectTransform>();
        glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
        glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
        glyphRect.pivot = new Vector2(0.5f, 0.5f);
        glyphRect.anchoredPosition = new Vector2(0f, -2f);
        glyphRect.sizeDelta = new Vector2(28f, 48f);

        Image handle = CreateImage("Handle", glyph.transform, panel, new Color(0.4f, 0.25f, 0.12f, 1f));
        handle.type = Image.Type.Simple;
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0.5f, 0f);
        handleRect.anchorMax = new Vector2(0.5f, 0f);
        handleRect.pivot = new Vector2(0.5f, 0f);
        handleRect.anchoredPosition = new Vector2(0f, 2f);
        handleRect.sizeDelta = new Vector2(8f, 28f);

        Image flame = CreateImage("Flame", glyph.transform, knob != null ? knob : panel, new Color(1f, 0.55f, 0.12f, 1f));
        flame.type = Image.Type.Simple;
        RectTransform flameRect = flame.rectTransform;
        flameRect.anchorMin = new Vector2(0.5f, 1f);
        flameRect.anchorMax = new Vector2(0.5f, 1f);
        flameRect.pivot = new Vector2(0.5f, 1f);
        flameRect.anchoredPosition = new Vector2(0f, -2f);
        flameRect.sizeDelta = new Vector2(18f, 18f);

        glyph.SetActive(itemId == PlayerInventory.TorchItemId);

        GameObject searchGlyph = CreateUiObject("SearchlightGlyph", slot.transform);
        RectTransform searchRect = searchGlyph.GetComponent<RectTransform>();
        searchRect.anchorMin = new Vector2(0.5f, 0.5f);
        searchRect.anchorMax = new Vector2(0.5f, 0.5f);
        searchRect.pivot = new Vector2(0.5f, 0.5f);
        searchRect.sizeDelta = new Vector2(40f, 18f);

        Image searchBody = CreateImage("Body", searchGlyph.transform, panel, new Color(0.22f, 0.24f, 0.28f, 1f));
        searchBody.type = Image.Type.Simple;
        RectTransform searchBodyRect = searchBody.rectTransform;
        searchBodyRect.anchorMin = new Vector2(0f, 0.5f);
        searchBodyRect.anchorMax = new Vector2(0f, 0.5f);
        searchBodyRect.pivot = new Vector2(0f, 0.5f);
        searchBodyRect.anchoredPosition = new Vector2(2f, 0f);
        searchBodyRect.sizeDelta = new Vector2(22f, 8f);

        Image searchLens = CreateImage("Lens", searchGlyph.transform, knob != null ? knob : panel, new Color(0.75f, 0.88f, 1f, 1f));
        searchLens.type = Image.Type.Simple;
        RectTransform searchLensRect = searchLens.rectTransform;
        searchLensRect.anchorMin = new Vector2(1f, 0.5f);
        searchLensRect.anchorMax = new Vector2(1f, 0.5f);
        searchLensRect.pivot = new Vector2(1f, 0.5f);
        searchLensRect.anchoredPosition = new Vector2(-2f, 0f);
        searchLensRect.sizeDelta = new Vector2(12f, 12f);
        searchGlyph.SetActive(itemId == PlayerInventory.SearchlightItemId);

        GameObject labelGo = CreateUiObject("Index", slot.transform);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(6f, -2f);
        labelRect.sizeDelta = new Vector2(24f, 22f);

        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = number.ToString();
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = new Color(1f, 1f, 1f, 0.92f);
        label.raycastTarget = false;

        return new SlotParts
        {
            Background = background,
            Highlight = highlight,
            TorchGlyph = glyph,
            SearchlightGlyph = searchGlyph
        };
    }

    private static GameObject CreateCrosshair(Transform canvas, Sprite panel)
    {
        GameObject crosshair = CreateUiObject("Crosshair", canvas);
        RectTransform rect = crosshair.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(18f, 18f);

        Image horizontal = CreateImage("Horizontal", crosshair.transform, panel, new Color(1f, 1f, 1f, 0.92f));
        horizontal.type = Image.Type.Simple;
        RectTransform horizontalRect = horizontal.rectTransform;
        horizontalRect.anchorMin = new Vector2(0.5f, 0.5f);
        horizontalRect.anchorMax = new Vector2(0.5f, 0.5f);
        horizontalRect.pivot = new Vector2(0.5f, 0.5f);
        horizontalRect.sizeDelta = new Vector2(16f, 2f);

        Image vertical = CreateImage("Vertical", crosshair.transform, panel, new Color(1f, 1f, 1f, 0.92f));
        vertical.type = Image.Type.Simple;
        RectTransform verticalRect = vertical.rectTransform;
        verticalRect.anchorMin = new Vector2(0.5f, 0.5f);
        verticalRect.anchorMax = new Vector2(0.5f, 0.5f);
        verticalRect.pivot = new Vector2(0.5f, 0.5f);
        verticalRect.sizeDelta = new Vector2(2f, 16f);

        crosshair.SetActive(false);
        return crosshair;
    }

    private static GameObject CreateSearchlightPrefab()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[Inventory] URP Lit shader was not found.");
            return null;
        }

        Material bodyMaterial = SaveMaterial("Assets/Materials/SearchlightBody.mat", shader, new Color(0.12f, 0.13f, 0.15f), 0.45f, 0.65f, Color.black);
        Material lensMaterial = SaveMaterial("Assets/Materials/SearchlightLens.mat", shader, new Color(0.85f, 0.93f, 1f), 0.15f, 0.9f, new Color(0.55f, 0.75f, 1f) * 1.4f);

        GameObject root = new GameObject("Searchlight");
        GameObject lamp = new GameObject("Lamp");
        lamp.transform.SetParent(root.transform, false);

        CreateCylinder("Body", lamp.transform, bodyMaterial, new Vector3(0f, 0f, 0.05f), new Vector3(0.05f, 0.1f, 0.05f));
        CreateCylinder("Head", lamp.transform, bodyMaterial, new Vector3(0f, 0f, 0.15f), new Vector3(0.085f, 0.028f, 0.085f));
        CreateCylinder("Lens", lamp.transform, lensMaterial, new Vector3(0f, 0f, 0.18f), new Vector3(0.062f, 0.01f, 0.062f));

        GameObject beam = new GameObject("Beam");
        beam.transform.SetParent(lamp.transform, false);
        beam.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        Light spot = beam.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.78f, 0.9f, 1f);
        spot.intensity = 18f;
        spot.range = 28f;
        spot.spotAngle = 34f;
        spot.innerSpotAngle = 18f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.65f;

        SearchlightAim aim = root.AddComponent<SearchlightAim>();
        SerializedObject aimObject = new SerializedObject(aim);
        aimObject.FindProperty("lamp").objectReferenceValue = lamp.transform;
        aimObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SearchlightPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateCylinder(string name, Transform parent, Material material, Vector3 localPosition, Vector3 localScale)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localScale = localScale;
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material SaveMaterial(string path, Shader shader, Color color, float metallic, float smoothness, Color emission)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0.01f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = CreateUiObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void WireInput(PlayerInput playerInput, PlayerInventory inventory, InputActionAsset actions)
    {
        if (playerInput == null)
        {
            Debug.LogError("[Inventory] PlayerInput is missing on the player prefab.");
            return;
        }

        var events = new List<PlayerInput.ActionEvent>();
        ReadOnlyArray<PlayerInput.ActionEvent> current = playerInput.actionEvents;
        for (int i = 0; i < current.Count; i++)
            events.Add(current[i]);

        Bind(events, actions, "Hotbar1", inventory, inventory.OnHotbar1);
        Bind(events, actions, "Hotbar2", inventory, inventory.OnHotbar2);
        Bind(events, actions, "Hotbar3", inventory, inventory.OnHotbar3);
        Bind(events, actions, "Hotbar4", inventory, inventory.OnHotbar4);
        Bind(events, actions, "HotbarScroll", inventory, inventory.OnHotbarScroll);

        playerInput.actionEvents = new ReadOnlyArray<PlayerInput.ActionEvent>(events.ToArray());
        EditorUtility.SetDirty(playerInput);
    }

    private static void Bind(
        List<PlayerInput.ActionEvent> events,
        InputActionAsset actions,
        string actionName,
        PlayerInventory inventory,
        UnityAction<InputAction.CallbackContext> handler)
    {
        InputAction action = actions.FindAction(actionName, true);
        string id = action.id.ToString();
        PlayerInput.ActionEvent actionEvent = null;
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].actionId == id)
            {
                actionEvent = events[i];
                break;
            }
        }

        if (actionEvent == null)
        {
            actionEvent = new PlayerInput.ActionEvent(action);
            events.Add(actionEvent);
        }

        for (int i = actionEvent.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (actionEvent.GetPersistentMethodName(i) == handler.Method.Name)
                UnityEventTools.RemovePersistentListener(actionEvent, i);
        }

        UnityEventTools.AddPersistentListener(actionEvent, handler);
        int last = actionEvent.GetPersistentEventCount() - 1;
        if (last >= 0)
            actionEvent.SetPersistentListenerState(last, UnityEventCallState.RuntimeOnly);
    }
}
