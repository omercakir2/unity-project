using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds a LedgeMarker child on top of each listed prop prefab, sized from its meshes.
/// Local X runs along the prop's longer side, so Vault and Climb Over cross the shorter one.
/// </summary>
public static class SetupLedgeMarkers
{
    private const string MarkerName = "LedgeMarker";
    private const string Root = "Assets/Imported/Industrial/";

    private static readonly (string path, LedgeType type)[] Props =
    {
        ("Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_S", LedgeType.ClimbOver),
        ("Fences/Road_blocks/Road_block_v1/Road_block_v1", LedgeType.Vault),
        ("Boxes/Wooden_box_v1/Wooden_box_v1_LD1square", LedgeType.Vault),
        ("Boxes/Wooden_box_v1/Wooden_box_v1_LD2square", LedgeType.Vault),
        ("Boxes/Wooden_box_v1/Wooden_box_v1_LD1", LedgeType.Vault),
        ("Barrels/Barrel_v3/Barrel_v3_quadro", LedgeType.ClimbUp),
        ("Barrels/Barrel_v2/Barrel_v2_quadro", LedgeType.ClimbUp),
        ("Dumpsters/Dumpsters_v1/Dumpsters_v1_empty", LedgeType.ClimbUp),
        ("Other_props/Generators/Generator_v1/Generator_v1", LedgeType.ClimbUp),
        ("Containers/Cargo_container_v1/Cargo_container_v1_LD1close", LedgeType.ClimbUp),
        ("Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_1", LedgeType.ClimbUp),
    };

    [MenuItem("Tools/Setup Ledge Markers")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        var report = new System.Text.StringBuilder("ledge-markers:");
        foreach (var (path, type) in Props)
            report.Append(' ').Append(System.IO.Path.GetFileName(path)).Append('=').Append(AddMarker(Root + path + ".prefab", type));
        return report.ToString();
    }

    private static string AddMarker(string prefabPath, LedgeType type)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return "missing";

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform existing = root.transform.Find(MarkerName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            if (!TryLocalBounds(root.transform, out Bounds bounds))
                return "no-mesh";

            bool alongZ = bounds.size.z > bounds.size.x;
            float length = alongZ ? bounds.size.z : bounds.size.x;
            float depth = alongZ ? bounds.size.x : bounds.size.z;

            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            marker.transform.localRotation = alongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            marker.AddComponent<LedgeMarker>().Configure(type, length, depth);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return type + "(" + length.ToString("0.00") + "x" + depth.ToString("0.00") + "@" + bounds.max.y.ToString("0.00") + ")";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Mesh bounds in the prefab root's own space, so a rotated child still measures right.
    private static bool TryLocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null)
                continue;

            Bounds local = mesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 inRoot = root.InverseTransformPoint(filters[i].transform.TransformPoint(point));
                if (!found)
                {
                    bounds = new Bounds(inRoot, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(inRoot);
                }
            }
        }

        return found;
    }
}
