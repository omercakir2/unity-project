using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Icons, the item icon set, the wood stick pickup, and the starting hotbar for the backpack.
/// Safe to run again.
/// </summary>
public static class SetupBackpackItems
{
    private const string IconDir = "Assets/Game/UI/Icons/";
    private const string IconSetPath = "Assets/Game/Resources/ItemIcons.asset";
    private const string StickPrefabPath = "Assets/Game/Prefabs/Items/WoodStick.prefab";
    private const string StickMaterialPath = "Assets/Game/Materials/WoodStick.mat";
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/Player.prefab";

    [MenuItem("Tools/Setup Backpack Items")]
    public static void SetupFromMenu()
    {
        Debug.Log(Setup());
    }

    public static string Setup()
    {
        ConfigureIcon("wood_stick.png");
        ConfigureIcon("fabric.png");
        string icons = CreateIconSet();
        GameObject stick = CreateStickPrefab();
        string hotbar = ClearStartingTorch();
        string placed = PlaceStickInScene(stick);
        AssetDatabase.SaveAssets();
        return "backpack: " + icons + " " + hotbar + " " + placed;
    }

    private static void ConfigureIcon(string fileName)
    {
        string path = IconDir + fileName;
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

    private static string CreateIconSet()
    {
        ItemIconSet set = AssetDatabase.LoadAssetAtPath<ItemIconSet>(IconSetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<ItemIconSet>();
            AssetDatabase.CreateAsset(set, IconSetPath);
        }

        string[,] entries =
        {
            { PlayerInventory.TorchItemId, "torch.png" },
            { PlayerInventory.SearchlightItemId, "searchlight.png" },
            { PlayerInventory.PistolItemId, "pistol.png" },
            { PlayerInventory.WoodStickItemId, "wood_stick.png" },
            { PlayerInventory.FabricItemId, "fabric.png" }
        };

        SerializedObject so = new SerializedObject(set);
        SerializedProperty list = so.FindProperty("entries");
        int count = entries.GetLength(0);
        list.arraySize = count;
        int missing = 0;
        for (int i = 0; i < count; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + entries[i, 1]);
            if (sprite == null)
                missing++;

            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("itemId").stringValue = entries[i, 0];
            entry.FindPropertyRelative("icon").objectReferenceValue = sprite;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        return "icons=" + (count - missing) + "/" + count;
    }

    private static GameObject CreateStickPrefab()
    {
        Material bark = AssetDatabase.LoadAssetAtPath<Material>(StickMaterialPath);
        if (bark == null)
        {
            bark = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(bark, StickMaterialPath);
        }

        bark.SetColor("_BaseColor", new Color(0.36f, 0.22f, 0.12f));
        bark.SetFloat("_Smoothness", 0.12f);
        // A faint warm glow so it can be spotted at night.
        bark.EnableKeyword("_EMISSION");
        bark.SetColor("_EmissionColor", new Color(0.09f, 0.05f, 0.02f));
        bark.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(bark);

        GameObject root = new GameObject("WoodStick");
        root.AddComponent<ItemPickup>();

        // Lying flat on the ground, a twig forking 40 degrees off the shaft.
        Part("Shaft", root.transform, bark,
            new Vector3(0f, 0.035f, 0f), Quaternion.Euler(0f, 18f, 90f), new Vector3(0.055f, 0.42f, 0.055f));
        Part("Twig", root.transform, bark,
            new Vector3(-0.16f, 0.03f, 0.13f), Quaternion.Euler(0f, 58f, 90f), new Vector3(0.03f, 0.12f, 0.03f));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, StickPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject Part(string name, Transform parent, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    // The torch is crafted now, so the player starts without one.
    private static string ClearStartingTorch()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerInventory inventory = root.GetComponent<PlayerInventory>();
            if (inventory == null)
                return "hotbar=no-inventory";

            SerializedObject so = new SerializedObject(inventory);
            SerializedProperty slots = so.FindProperty("slots");
            int cleared = 0;
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                if (slot.FindPropertyRelative("item").stringValue != PlayerInventory.TorchItemId)
                    continue;

                slot.FindPropertyRelative("occupied").boolValue = false;
                slot.FindPropertyRelative("item").stringValue = string.Empty;
                slot.FindPropertyRelative("preview").boolValue = false;
                cleared++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return "hotbar-torch-cleared=" + cleared;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string PlaceStickInScene(GameObject prefab)
    {
        if (prefab == null)
            return "stick=no-prefab";
        if (Object.FindAnyObjectByType<ItemPickup>() != null)
            return "stick=already-in-scene";

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
            return "stick=no-player";

        // A few steps ahead of the spawn. Move it wherever it fits the level.
        Vector3 guess = player.transform.position + player.transform.forward * 4f + player.transform.right * 1.5f;
        if (Physics.Raycast(guess + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            guess = hit.point;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = guess;
        Undo.RegisterCreatedObjectUndo(instance, "Place Wood Stick");
        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveScene(instance.scene);
        return "stick=" + guess;
    }
}
