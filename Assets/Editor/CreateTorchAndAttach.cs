using UnityEngine;

public static class CreateTorchAndAttach
{
    [UnityEditor.MenuItem("Tools/Setup Player Torch (Night)")]
    public static void SetupTorch()
    {
        Debug.Log(ApplyVisualPass.RebuildTorch());
    }
}
