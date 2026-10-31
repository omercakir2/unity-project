using UnityEditor;
using UnityEngine;

/// <summary>
/// Stamps Grip children onto the held-item prefabs. Re-running does not move a Grip that is already there.
/// </summary>
public static class WeaponGripSetup
{
    private const string TorchPath = "Assets/Game/Prefabs/Items/Torch.prefab";
    private const string SearchlightPath = "Assets/Game/Prefabs/Items/Searchlight.prefab";
    private const string PistolPath = "Assets/Game/Prefabs/Items/Pistol.prefab";

    [MenuItem("Tools/Setup Weapon Grips")]
    public static void SetupFromMenu()
    {
        Debug.Log(Setup(false));
    }

    public static string Setup(bool overwrite)
    {
        string torch = Edit(TorchPath, root => ApplyReproducedHold(root, new Vector3(0.01f, 0.27f, 0.05f), new Vector3(344.6f, 82.2f, 0f), Vector3.one, overwrite));
        string light = Edit(SearchlightPath, root => ApplyReproducedHold(root, new Vector3(0.021f, 0.118f, 0.002f), new Vector3(280.3f, 85f, 177.6f), Vector3.one, overwrite));
        string pistol = Edit(PistolPath, root => ApplyPistol(root, overwrite));
        return torch + " " + light + " " + pistol;
    }

    public static void ApplyToOpenTorch(GameObject root)
    {
        ApplyReproducedHold(root, new Vector3(0.01f, 0.27f, 0.05f), new Vector3(344.6f, 82.2f, 0f), Vector3.one, false);
    }

    private static string Edit(string path, System.Action<GameObject> apply)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
            return path + ":missing";

        try
        {
            apply(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return path + ":ok";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Barrel (+Z) runs along the fingers (hand +Y). Slide top (+Y) runs along hand -X,
    // which is up while the pistol aim clip is playing. The point is the rear of the magazine.
    private static void ApplyPistol(GameObject root, bool overwrite)
    {
        Transform grip = EnsureGrip(root.transform);
        bool unauthored = grip.localPosition.sqrMagnitude < 0.0001f && grip.localRotation == Quaternion.identity;
        if (overwrite || unauthored)
        {
            grip.localPosition = new Vector3(0f, -0.05f, -0.92f);
            grip.localRotation = Quaternion.Inverse(Quaternion.LookRotation(Vector3.up, Vector3.left));
            grip.localScale = Vector3.one;
        }

        Wire(root, grip);
    }

    // These grips reproduce the previous left-hand offsets. The empty is where the
    // wrist lands, which is not the same point as the middle of the mesh.
    private static void ApplyReproducedHold(GameObject root, Vector3 holdPosition, Vector3 holdEuler, Vector3 weaponScale, bool overwrite)
    {
        Transform grip = EnsureGrip(root.transform);
        bool unauthored = grip.localPosition.sqrMagnitude < 0.0001f && grip.localRotation == Quaternion.identity;
        if (overwrite || unauthored)
        {
            Quaternion hold = Quaternion.Euler(holdEuler);
            Vector3 scaled = Quaternion.Inverse(hold) * (-holdPosition);
            grip.localPosition = new Vector3(scaled.x / weaponScale.x, scaled.y / weaponScale.y, scaled.z / weaponScale.z);
            grip.localRotation = Quaternion.Inverse(hold);
            grip.localScale = Vector3.one;
        }

        Wire(root, grip);
    }

    private static Transform EnsureGrip(Transform root)
    {
        Transform grip = FindNamed(root, "Grip");
        if (grip != null)
            return grip;

        GameObject gripObject = new GameObject("Grip");
        gripObject.transform.SetParent(root, false);
        return gripObject.transform;
    }

    private static void Wire(GameObject root, Transform grip)
    {
        WeaponGrip component = root.GetComponent<WeaponGrip>();
        if (component == null)
            component = root.AddComponent<WeaponGrip>();

        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty("grip").objectReferenceValue = grip;
        Transform left = FindNamed(root.transform, "LeftHandGrip");
        serialized.FindProperty("leftHandGrip").objectReferenceValue = left;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindNamed(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != root && children[i].name == childName)
                return children[i];
        }

        return null;
    }
}
