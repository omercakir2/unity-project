using System;
using UnityEngine;

/// <summary>
/// The objective a scene starts with. Gameplay can change it later with <see cref="Set"/>.
/// </summary>
[DisallowMultipleComponent]
public class SceneObjective : MonoBehaviour
{
    [SerializeField, TextArea(1, 3)] private string objective = "Discover your surroundings";

    public static string Current { get; private set; }
    public static event Action<string> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        Current = null;
        Changed = null;
    }

    private void Awake()
    {
        Set(objective);
    }

    private void OnDestroy()
    {
        // Leaving the scene: don't carry its objective into one that has none.
        if (Current == objective)
            Current = null;
    }

    public static void Set(string text)
    {
        if (Current == text)
            return;

        Current = text;
        Changed?.Invoke(text);
    }
}
