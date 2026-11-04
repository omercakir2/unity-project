using System;
using System.Collections.Generic;
using UnityEngine;

// Loops the main theme as 2D background music for the whole play session.
// Cinematics and chases can crossfade to their own track and hand the theme back.
[DisallowMultipleComponent]
public class MainThemeMusic : MonoBehaviour
{
    private const string ClipResourcePath = "Audio/MainTheme";

    private static MainThemeMusic instance;

    private AmbienceSettings settings;
    private AudioSource source;
    private AudioSource overrideSource;
    private readonly HashSet<object> overrideOwners = new HashSet<object>();
    private Func<AmbienceSettings, float> overrideLevel;
    private float fadeTime = 1f;
    private float overrideMix; // 0 is the theme, 1 is the override.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartTheme()
    {
        if (instance != null)
            return;

        GameObject themeObject = new GameObject("Main Theme");
        themeObject.AddComponent<MainThemeMusic>();
    }

    /// <summary>Crossfades to a track until every owner has called EndOverride.</summary>
    public static void PlayOverride(object owner, string clipPath, Func<AmbienceSettings, float> level, float fade, bool loop)
    {
        // Scene objects can ask before the after-scene-load hook has run.
        StartTheme();
        instance.BeginOverride(owner, clipPath, level, fade, loop);
    }

    /// <summary>Safe to call more than once. The theme returns once no owner is left.</summary>
    public static void EndOverride(object owner, float fade)
    {
        if (instance == null || !instance.overrideOwners.Remove(owner))
            return;

        instance.fadeTime = fade;
    }

    private void BeginOverride(object owner, string clipPath, Func<AmbienceSettings, float> level, float fade, bool loop)
    {
        AudioClip clip = Resources.Load<AudioClip>(clipPath);
        if (clip == null)
        {
            Debug.LogWarning("[MainThemeMusic] Missing track at Resources/" + clipPath + ".");
            return;
        }

        overrideOwners.Add(owner);
        overrideLevel = level;
        fadeTime = fade;
        if (overrideSource == null)
            overrideSource = CreateSource();

        overrideSource.loop = loop;
        if (overrideSource.clip == clip && overrideSource.isPlaying)
            return;

        overrideSource.clip = clip;
        overrideSource.volume = level(settings) * overrideMix;
        overrideSource.Play();
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

        source = CreateSource();
        source.clip = clip;
        source.loop = true;
        source.volume = settings.themeVolume;
        source.Play();
    }

    private AudioSource CreateSource()
    {
        AudioSource created = gameObject.AddComponent<AudioSource>();
        created.playOnAwake = false;
        created.spatialBlend = 0f;
        created.priority = 0;
        created.dopplerLevel = 0f;
        created.bypassReverbZones = true;
        return created;
    }

    private void Update()
    {
        if (settings == null)
            return;

        // Real time, so the fades do not crawl during slow motion.
        float target = overrideOwners.Count > 0 ? 1f : 0f;
        overrideMix = Mathf.MoveTowards(overrideMix, target, Time.unscaledDeltaTime / Mathf.Max(fadeTime, 0.01f));

        if (source != null)
            source.volume = settings.themeVolume * (1f - overrideMix);

        if (overrideSource == null || overrideLevel == null)
            return;

        overrideSource.volume = overrideLevel(settings) * overrideMix;
        if (overrideMix <= 0f && overrideSource.isPlaying)
            overrideSource.Stop();
    }
}
