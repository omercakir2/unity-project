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
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/Player.prefab";
    private const string TorchPrefabPath = "Assets/Game/Prefabs/Items/Torch.prefab";
    private const string SearchlightPrefabPath = "Assets/Game/Prefabs/Items/Searchlight.prefab";
    private const string PistolPrefabPath = "Assets/Game/Prefabs/Items/Pistol.prefab";
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

        GameObject pistolPrefab = CreatePistolPrefab();
        if (pistolPrefab == null)
            return "missing-pistol-prefab";

        WeaponGripSetup.Setup(false);

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Game/Input/PlayerInputActions.inputactions");
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
            heldObject.FindProperty("pistolPrefab").objectReferenceValue = pistolPrefab;
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
            string itemId = i == 0 ? PlayerInventory.TorchItemId : i == 1 ? PlayerInventory.SearchlightItemId : i == 2 ? PlayerInventory.PistolItemId : null;
            SlotParts parts = CreateSlot(hotbar.transform, panel, knob, font, i + 1, itemId, i == 0);
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("background").objectReferenceValue = parts.Background;
            slot.FindPropertyRelative("highlight").objectReferenceValue = parts.Highlight;
            slot.FindPropertyRelative("torchGlyph").objectReferenceValue = parts.TorchGlyph;
            slot.FindPropertyRelative("searchlightGlyph").objectReferenceValue = parts.SearchlightGlyph;
            slot.FindPropertyRelative("pistolGlyph").objectReferenceValue = parts.PistolGlyph;
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
        public GameObject PistolGlyph;
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

        GameObject glyph = CreateIconGlyph("TorchGlyph", slot.transform, LoadHotbarIcon("torch.png"));
        glyph.SetActive(itemId == PlayerInventory.TorchItemId);

        GameObject searchGlyph = CreateIconGlyph("SearchlightGlyph", slot.transform, LoadHotbarIcon("searchlight.png"));
        searchGlyph.SetActive(itemId == PlayerInventory.SearchlightItemId);

        GameObject pistolGlyph = CreateIconGlyph("PistolGlyph", slot.transform, LoadHotbarIcon("pistol.png"));
        pistolGlyph.SetActive(itemId == PlayerInventory.PistolItemId);

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
            SearchlightGlyph = searchGlyph,
            PistolGlyph = pistolGlyph
        };
    }

    private const string HotbarIconDir = "Assets/Game/UI/Icons/";

    private static void ConfigureHotbarIcon(string fileName)
    {
        string path = HotbarIconDir + fileName;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();
    }

    private static Sprite LoadHotbarIcon(string fileName)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HotbarIconDir + fileName);
        if (sprite == null)
            Debug.LogError("[Inventory] Missing hotbar icon " + HotbarIconDir + fileName);
        return sprite;
    }

    private static GameObject CreateIconGlyph(string name, Transform parent, Sprite icon)
    {
        GameObject glyph = CreateUiObject(name, parent);
        RectTransform rect = glyph.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -2f);
        rect.sizeDelta = new Vector2(56f, 56f);

        Image image = glyph.AddComponent<Image>();
        image.sprite = icon;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        return glyph;
    }

    public static string ApplyHotbarIcons()
    {
        ConfigureHotbarIcon("torch.png");
        ConfigureHotbarIcon("searchlight.png");
        ConfigureHotbarIcon("pistol.png");

        Sprite torch = LoadHotbarIcon("torch.png");
        Sprite searchlight = LoadHotbarIcon("searchlight.png");
        Sprite pistol = LoadHotbarIcon("pistol.png");
        if (torch == null || searchlight == null || pistol == null)
            return "missing-icons";

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            int updated = 0;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            var glyphs = new List<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null)
                    continue;
                string glyphName = candidate.name;
                if (glyphName == "TorchGlyph" || glyphName == "SearchlightGlyph" || glyphName == "PistolGlyph")
                    glyphs.Add(candidate);
            }

            for (int i = 0; i < glyphs.Count; i++)
            {
                Transform glyph = glyphs[i];
                Sprite icon = null;
                if (glyph.name == "TorchGlyph")
                    icon = torch;
                else if (glyph.name == "SearchlightGlyph")
                    icon = searchlight;
                else if (glyph.name == "PistolGlyph")
                    icon = pistol;
                if (icon == null)
                    continue;

                for (int child = glyph.childCount - 1; child >= 0; child--)
                    Object.DestroyImmediate(glyph.GetChild(child).gameObject);

                Image image = glyph.GetComponent<Image>();
                if (image == null)
                    image = glyph.gameObject.AddComponent<Image>();
                image.sprite = icon;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                image.raycastTarget = false;

                RectTransform rect = glyph.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -2f);
                rect.sizeDelta = new Vector2(56f, 56f);
                updated++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return "icons=" + updated;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static string InstallPistol()
    {
        GameObject pistolPrefab = CreatePistolPrefab();
        if (pistolPrefab == null)
            return "missing-pistol-prefab";

        WeaponGripSetup.Setup(false);

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerInventory inventory = root.GetComponent<PlayerInventory>();
            if (inventory == null)
                return "missing-inventory";
            inventory.ApplyDefaultState();

            HeldItemVisual held = root.GetComponent<HeldItemVisual>();
            if (held == null)
                return "missing-held-visual";

            SerializedObject heldObject = new SerializedObject(held);
            heldObject.FindProperty("pistolPrefab").objectReferenceValue = pistolPrefab;
            heldObject.ApplyModifiedPropertiesWithoutUndo();

            Transform slot = root.transform.Find("InventoryCanvas/Hotbar/Slot3");
            if (slot == null)
                return "missing-slot3";

            Transform existing = slot.Find("PistolGlyph");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            GameObject glyph = CreateIconGlyph("PistolGlyph", slot, LoadHotbarIcon("pistol.png"));
            glyph.SetActive(true);

            InventoryHotbarView view = root.GetComponentInChildren<InventoryHotbarView>(true);
            if (view == null)
                return "missing-view";

            SerializedObject viewObject = new SerializedObject(view);
            SerializedProperty slots = viewObject.FindProperty("slots");
            if (slots == null || slots.arraySize < 3)
                return "missing-slots";
            slots.GetArrayElementAtIndex(2).FindPropertyRelative("pistolGlyph").objectReferenceValue = glyph;
            viewObject.ApplyModifiedPropertiesWithoutUndo();

            Transform crosshair = root.transform.Find("InventoryCanvas/Crosshair");
            if (crosshair != null)
            {
                Transform horizontalBar = crosshair.Find("Horizontal");
                Transform verticalBar = crosshair.Find("Vertical");
                if (horizontalBar is RectTransform horizontalRect)
                    horizontalRect.sizeDelta = new Vector2(28f, 4f);
                if (verticalBar is RectTransform verticalRect)
                    verticalRect.sizeDelta = new Vector2(4f, 28f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return "ok";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
        horizontalRect.sizeDelta = new Vector2(28f, 4f);

        Image vertical = CreateImage("Vertical", crosshair.transform, panel, new Color(1f, 1f, 1f, 0.92f));
        vertical.type = Image.Type.Simple;
        RectTransform verticalRect = vertical.rectTransform;
        verticalRect.anchorMin = new Vector2(0.5f, 0.5f);
        verticalRect.anchorMax = new Vector2(0.5f, 0.5f);
        verticalRect.pivot = new Vector2(0.5f, 0.5f);
        verticalRect.sizeDelta = new Vector2(4f, 28f);

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

        Material bodyMaterial = SaveMaterial("Assets/Game/Materials/SearchlightBody.mat", shader, new Color(0.12f, 0.13f, 0.15f), 0.45f, 0.65f, Color.black);
        Material lensMaterial = SaveMaterial("Assets/Game/Materials/SearchlightLens.mat", shader, new Color(0.85f, 0.93f, 1f), 0.15f, 0.9f, new Color(0.55f, 0.75f, 1f) * 1.4f);

        GameObject root = new GameObject("Searchlight");
        GameObject lamp = new GameObject("Lamp");
        lamp.transform.SetParent(root.transform, false);

        CreateCylinder("Body", lamp.transform, bodyMaterial, new Vector3(0f, 0f, 0.05f), new Vector3(0.05f, 0.1f, 0.05f));
        CreateCylinder("Head", lamp.transform, bodyMaterial, new Vector3(0f, 0f, 0.15f), new Vector3(0.085f, 0.028f, 0.085f));
        CreateCylinder("Lens", lamp.transform, lensMaterial, new Vector3(0f, 0f, 0.18f), new Vector3(0.062f, 0.01f, 0.062f));

        GameObject source = new GameObject("Light");
        source.transform.SetParent(lamp.transform, false);
        source.transform.localPosition = new Vector3(0f, 0f, 0.18f);
        Light point = source.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = new Color(0.82f, 0.9f, 1f);
        point.intensity = 6f;
        point.range = 8f;
        point.shadows = LightShadows.Soft;
        point.shadowStrength = 0.45f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SearchlightPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreatePistolPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPrefabPath);
        if (existing != null && existing.GetComponentInChildren<Renderer>(true) != null)
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[Inventory] URP Lit shader was not found.");
            return null;
        }

        Material slideMaterial = SaveMaterial("Assets/Game/Materials/PistolSlide.mat", shader, new Color(0.16f, 0.17f, 0.19f), 0.72f, 0.58f, Color.black);
        Material gripMaterial = SaveMaterial("Assets/Game/Materials/PistolGrip.mat", shader, new Color(0.08f, 0.08f, 0.09f), 0.05f, 0.32f, Color.black);

        GameObject root = new GameObject("Pistol");
        Quaternion barrelSpin = Quaternion.Euler(-90f, 0f, 0f);
        CreatePart("Slide", root.transform, PrimitiveType.Cube, slideMaterial, new Vector3(0f, 0.018f, 0.03f), Quaternion.identity, new Vector3(0.028f, 0.03f, 0.15f));
        CreatePart("Barrel", root.transform, PrimitiveType.Cylinder, slideMaterial, new Vector3(0f, 0.016f, 0.125f), barrelSpin, new Vector3(0.012f, 0.032f, 0.012f));
        CreatePart("Sight", root.transform, PrimitiveType.Cube, slideMaterial, new Vector3(0f, 0.04f, 0.09f), Quaternion.identity, new Vector3(0.006f, 0.014f, 0.01f));
        CreatePart("Grip", root.transform, PrimitiveType.Cube, gripMaterial, new Vector3(0f, -0.04f, -0.012f), Quaternion.Euler(16f, 0f, 0f), new Vector3(0.026f, 0.078f, 0.036f));
        CreatePart("Guard", root.transform, PrimitiveType.Cube, slideMaterial, new Vector3(0f, -0.008f, 0.028f), Quaternion.identity, new Vector3(0.02f, 0.026f, 0.006f));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PistolPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreatePart(string name, Transform parent, PrimitiveType type, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
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
