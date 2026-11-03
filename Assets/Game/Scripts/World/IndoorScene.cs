using UnityEngine;

/// <summary>
/// Marks a scene as indoors. The global rain stays off while one is loaded.
/// </summary>
[DisallowMultipleComponent]
public class IndoorScene : MonoBehaviour
{
    public static bool IsActive => FindAnyObjectByType<IndoorScene>() != null;

    private void Awake()
    {
        EnvironmentRain.Stop();
    }
}
