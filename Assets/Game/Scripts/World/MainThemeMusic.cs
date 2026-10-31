using UnityEngine;

// Loops the main theme as 2D background music for the whole play session.
[DisallowMultipleComponent]
public class MainThemeMusic : MonoBehaviour
{
    private const string ClipResourcePath = "Audio/MainTheme";

    private static MainThemeMusic instance;

    private AmbienceSettings settings;
    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartTheme()
    {
        if (instance != null)
            return;

        GameObject themeObject = new GameObject("Main Theme");
        themeObject.AddComponent<MainThemeMusic>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        settings = AmbienceSettings.LoadOrDefault();

        AudioClip clip = Resources.Load<AudioClip>(ClipResourcePath);
        if (clip == null)
        {
            Debug.LogWarning("Main theme clip is missing at Resources/" + ClipResourcePath + ".");
            return;
        }

        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = settings.themeVolume;
        source.priority = 0;
        source.dopplerLevel = 0f;
        source.bypassReverbZones = true;
        source.Play();
    }

    private void Update()
    {
        if (settings == null || source == null)
            return;

        if (!Mathf.Approximately(source.volume, settings.themeVolume))
            source.volume = settings.themeVolume;
    }
}
