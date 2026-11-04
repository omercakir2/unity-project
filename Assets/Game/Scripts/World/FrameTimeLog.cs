#if DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Development builds only: writes the average and worst frame time to Player.log every
/// few seconds, so performance can be read from a real build instead of the Editor.
/// </summary>
public class FrameTimeLog : MonoBehaviour
{
    private const float Window = 5f;

    private float elapsed;
    private float worst;
    private int frames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject("Frame Time Log");
        DontDestroyOnLoad(go);
        go.AddComponent<FrameTimeLog>();
    }

    // "-benchPos x z" moves the player there, to measure a chosen view without playing.
    private void Start()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int index = System.Array.IndexOf(args, "-benchPos");
        if (index < 0 || index + 2 >= args.Length)
            return;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null)
            return;

        CharacterController body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = new Vector3(
            float.Parse(args[index + 1], System.Globalization.CultureInfo.InvariantCulture),
            1.4f,
            float.Parse(args[index + 2], System.Globalization.CultureInfo.InvariantCulture));
        body.enabled = true;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        elapsed += dt;
        frames++;
        worst = Mathf.Max(worst, dt);
        if (elapsed < Window)
            return;

        float average = elapsed / frames;
        Debug.Log("[FrameTimeLog] " + Screen.width + "x" + Screen.height + " avg " + (average * 1000f).ToString("F1")
            + " ms (" + (1f / average).ToString("F0") + " fps), worst " + (worst * 1000f).ToString("F1") + " ms");
        elapsed = 0f;
        worst = 0f;
        frames = 0;
    }
}
#endif
