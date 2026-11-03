using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single owner of Time.timeScale. Any pause request freezes time; otherwise the
/// slowest slow-motion request wins. Cleared on every scene load.
/// </summary>
public static class GameTime
{
    private static readonly HashSet<object> pauses = new HashSet<object>();
    private static readonly Dictionary<object, float> slowdowns = new Dictionary<object, float>();
    private static float baseFixedDelta = -1f;

    public static bool Paused => pauses.Count > 0;

    public static float SlowMotion
    {
        get
        {
            float scale = 1f;
            foreach (float value in slowdowns.Values)
                scale = Mathf.Min(scale, value);
            return scale;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        pauses.Clear();
        slowdowns.Clear();
        baseFixedDelta = -1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        pauses.Clear();
        slowdowns.Clear();
        Apply();
    }

    public static void SetPaused(object owner, bool paused)
    {
        if (owner == null)
            return;

        if (paused)
            pauses.Add(owner);
        else
            pauses.Remove(owner);
        Apply();
    }

    public static void SetSlowMotion(object owner, float scale)
    {
        if (owner == null)
            return;

        slowdowns[owner] = Mathf.Clamp(scale, 0.01f, 1f);
        Apply();
    }

    public static void ClearSlowMotion(object owner)
    {
        if (owner == null || !slowdowns.Remove(owner))
            return;

        Apply();
    }

    private static void Apply()
    {
        if (baseFixedDelta < 0f)
            baseFixedDelta = Time.fixedDeltaTime;

        if (Paused)
        {
            Time.timeScale = 0f;
            Time.fixedDeltaTime = baseFixedDelta;
            return;
        }

        float scale = SlowMotion;
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDelta * scale;
    }
}
