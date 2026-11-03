using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fades to black, swaps to a scene that was loaded in the background, drops the player on
/// the matching spawn point, then fades back in. Lives across scene loads.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    private const float FadeOutTime = 0.7f;
    private const float FadeInTime = 0.9f;
    private const float SettleTime = 0.35f;

    private static SceneTransition instance;

    private Canvas canvas;
    private Image fade;

    public static bool IsBusy { get; private set; }

    /// <summary>Spawn id the incoming scene should use. Set from fade-out until the player is placed.</summary>
    public static string ArrivingSpawnId { get; private set; }

    public static bool IsArriving => !string.IsNullOrEmpty(ArrivingSpawnId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        instance = null;
        IsBusy = false;
        ArrivingSpawnId = null;
    }

    /// <summary>Starts loading a scene in the background without showing it.</summary>
    public static AsyncOperation Preload(string scenePath)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        if (op != null)
            op.allowSceneActivation = false;
        return op;
    }

    public static bool Go(AsyncOperation preloaded, string scenePath, string spawnId)
    {
        if (IsBusy)
            return false;

        EnsureInstance();
        instance.StartCoroutine(instance.Run(preloaded, scenePath, spawnId));
        return true;
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("Scene Transition");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SceneTransition>();
        instance.BuildFade();
    }

    private IEnumerator Run(AsyncOperation op, string scenePath, string spawnId)
    {
        IsBusy = true;
        Lock(true);
        yield return FadeTo(1f, FadeOutTime);

        ArrivingSpawnId = spawnId;
        if (op == null)
            op = Preload(scenePath);
        if (op == null)
        {
            Debug.LogWarning("[SceneTransition] Could not load " + scenePath + ". Is it in Build Settings?");
            ArrivingSpawnId = null;
            Lock(false);
            yield return FadeTo(0f, FadeInTime);
            IsBusy = false;
            yield break;
        }

        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;

        // One frame so the new scene's Awake/Start and load callbacks have run.
        yield return null;
        Lock(true);

        // No spawn id means the scene starts the way it was authored (e.g. menu to the wake-up).
        if (!string.IsNullOrEmpty(spawnId))
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            SceneSpawnPoint point = SceneSpawnPoint.Find(spawnId);
            if (player != null && point != null)
                SceneSpawnPoint.PlacePlayer(player, point.transform);
            else
                Debug.LogWarning("[SceneTransition] No spawn point '" + spawnId + "' in " + scenePath + ".");
        }

        ArrivingSpawnId = null;

        // Let the gameplay camera catch up behind the black.
        yield return new WaitForSeconds(SettleTime);
        Lock(false);
        yield return FadeTo(0f, FadeInTime);
        IsBusy = false;
    }

    private void Lock(bool locked)
    {
        // A scene load clears both registries, so the overlay is exempted again each time.
        CinematicUI.Exempt(canvas);
        GameplayInput.SetBlocked(this, locked);
        CinematicUI.SetHidden(this, locked);
    }

    private IEnumerator FadeTo(float alpha, float duration)
    {
        float from = fade.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, alpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        fade.color = new Color(0f, 0f, 0f, alpha);
    }

    private void BuildFade()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;

        GameObject imageObject = new GameObject("Black", typeof(RectTransform));
        imageObject.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fade = imageObject.AddComponent<Image>();
        fade.color = Color.clear;
        fade.raycastTarget = false;
    }
}
