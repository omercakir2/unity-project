using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hides every canvas while any owner asks for it (cinematics). Canvases that appear
/// mid-shot (pickup prompts, subtitles) are caught too. Exempt canvases stay visible.
/// </summary>
public static class CinematicUI
{
    private static readonly HashSet<object> owners = new HashSet<object>();
    private static readonly HashSet<Canvas> hidden = new HashSet<Canvas>();
    private static readonly HashSet<Canvas> exempt = new HashSet<Canvas>();
    private static Runner runner;

    public static bool IsHidden => owners.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        owners.Clear();
        hidden.Clear();
        exempt.Clear();
        runner = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        owners.Clear();
        hidden.Clear();
        exempt.Clear();
    }

    /// <summary>Keeps a canvas visible while the UI is hidden, e.g. a fade overlay.</summary>
    public static void Exempt(Canvas canvas)
    {
        if (canvas != null)
            exempt.Add(canvas);
    }

    public static void SetHidden(object owner, bool hide)
    {
        if (owner == null)
            return;

        bool wasHidden = IsHidden;
        if (hide)
            owners.Add(owner);
        else
            owners.Remove(owner);

        if (wasHidden == IsHidden)
            return;

        if (IsHidden)
        {
            EnsureRunner();
            HideVisible();
        }
        else
            RestoreHidden();
    }

    private static void HideVisible()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (!canvas.enabled || !canvas.isRootCanvas || exempt.Contains(canvas))
                continue;

            canvas.enabled = false;
            hidden.Add(canvas);
        }
    }

    private static void RestoreHidden()
    {
        foreach (Canvas canvas in hidden)
        {
            if (canvas != null)
                canvas.enabled = true;
        }

        hidden.Clear();
    }

    private static void EnsureRunner()
    {
        if (runner != null)
            return;

        GameObject go = new GameObject("CinematicUI");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    private sealed class Runner : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (IsHidden)
                HideVisible();
        }
    }
}
