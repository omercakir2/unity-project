using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds a "CameraIgnore" layer for see-through parts (chain-link wire) and keeps the third person
/// cameras from pulling in on it. Physics is unchanged: the player and shots still collide with it.
/// </summary>
public static class SetupCameraIgnoreLayer
{
    public const string LayerName = "CameraIgnore";
    private const int PreferredLayer = 8;
    private const string CamerasPrefabPath = "Assets/Game/Prefabs/Cameras/Cinemachine Cameras.prefab";

    // Prefab path and the child that is only wire.
    private static readonly (string prefab, string child)[] SeeThroughParts =
    {
        ("Assets/Imported/Industrial/Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_S.prefab", "Concrete_fence_v2_lattice_S"),
    };

    [MenuItem("Tools/Setup Camera Ignore Layer")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        int layer = EnsureLayer();
        if (layer < 0)
            return "camera-ignore: no free layer";

        var report = new System.Text.StringBuilder("camera-ignore: layer=" + layer);
        foreach (var (prefab, child) in SeeThroughParts)
            report.Append(' ').Append(child).Append('=').Append(SetPartLayer(prefab, child, layer));
        report.Append(" cameras=").Append(ExcludeFromCameras(layer));
        return report.ToString();
    }

    private static int EnsureLayer()
    {
        int existing = LayerMask.NameToLayer(LayerName);
        if (existing >= 0)
            return existing;

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        int slot = -1;
        if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(PreferredLayer).stringValue))
            slot = PreferredLayer;
        for (int i = PreferredLayer + 1; i < layers.arraySize && slot < 0; i++)
        {
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                slot = i;
        }

        if (slot < 0)
            return -1;

        layers.GetArrayElementAtIndex(slot).stringValue = LayerName;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    private static string SetPartLayer(string prefabPath, string childName, int layer)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform part = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName)
                    part = t;
            }

            if (part == null)
                return "missing";

            part.gameObject.layer = layer;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return "ok";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string ExcludeFromCameras(int layer)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CamerasPrefabPath);
        try
        {
            CinemachineThirdPersonFollow[] follows = root.GetComponentsInChildren<CinemachineThirdPersonFollow>(true);
            for (int i = 0; i < follows.Length; i++)
            {
                CinemachineThirdPersonFollow.ObstacleSettings avoid = follows[i].AvoidObstacles;
                avoid.CollisionFilter &= ~(1 << layer);
                follows[i].AvoidObstacles = avoid;
            }

            PrefabUtility.SaveAsPrefabAsset(root, CamerasPrefabPath);
            return follows.Length.ToString();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
