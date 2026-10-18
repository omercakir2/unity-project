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
            heldObject.ApplyModifiedPropertiesWithoutUndo();

            Transform existingCanvas = root.transform.Find("InventoryCanvas");
            if (existingCanvas != null)
                Object.DestroyImmediate(existingCanvas);

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

        InventoryHotbarView view = hotbar.AddComponent<InventoryHotbarView>();
        SerializedObject viewObject = new SerializedObject(view);
        viewObject.FindProperty("inventory").objectReferenceValue = inventory;
        SerializedProperty slots = viewObject.FindProperty("slots");
        slots.arraySize = 4;

        for (int i = 0; i < 4; i++)
        {
            bool torchSlot = i == 0;
            SlotParts parts = CreateSlot(hotbar.transform, panel, knob, font, i + 1, torchSlot);
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("background").objectReferenceValue = parts.Background;
            slot.FindPropertyRelative("highlight").objectReferenceValue = parts.Highlight;
            slot.FindPropertyRelative("torchGlyph").objectReferenceValue = parts.TorchGlyph;
        }

        viewObject.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private struct SlotParts
    {
        public Image Background;
        public Image Highlight;
        public GameObject TorchGlyph;
    }

    private static SlotParts CreateSlot(Transform parent, Sprite panel, Sprite knob, TMP_FontAsset font, int number, bool showTorch)
    {
        GameObject slot = CreateUiObject("Slot" + number, parent);
        LayoutElement element = slot.AddComponent<LayoutElement>();
        element.preferredWidth = 72f;
        element.preferredHeight = 72f;
        element.minWidth = 72f;
        element.minHeight = 72f;

        Image highlight = CreateImage("Highlight", slot.transform, panel, showTorch
            ? new Color(1f, 0.72f, 0.28f, 1f)
            : new Color(1f, 1f, 1f, 0.16f));
        Stretch(highlight.rectTransform, 0f);

        Image background = CreateImage("Background", slot.transform, panel, showTorch
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

        glyph.SetActive(showTorch);

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
            TorchGlyph = glyph
        };
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
