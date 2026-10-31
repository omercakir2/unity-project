using UnityEngine;

// Loops a quiet breath on the player. Walking and standing share one clip. Sprinting uses the other.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public class PlayerBreath : MonoBehaviour
{
    private const string WalkClipPath = "Audio/Player/walk-and-idle-breath";
    private const string RunClipPath = "Audio/Player/run-breath";
    private const float FadeTime = 0.2f;

    private PlayerController player;
    private PlayerVitals vitals;
    private AmbienceSettings settings;
    private AudioSource walkSource;
    private AudioSource runSource;
    private bool walkStarted;
    private bool runStarted;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        vitals = GetComponent<PlayerVitals>();
        settings = AmbienceSettings.LoadOrDefault();
        walkSource = CreateSource(WalkClipPath, "Walk breath");
        runSource = CreateSource(RunClipPath, "Run breath");
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        bool dead = vitals != null && vitals.IsDead;
        bool sprint = !dead && player != null && player.IsSprintingNow;
        float walk = settings != null ? settings.walkBreathVolume : 0.32f;
        float run = settings != null ? settings.runBreathVolume : 0.4f;
        Drive(walkSource, ref walkStarted, dead || sprint ? 0f : walk, walk);
        Drive(runSource, ref runStarted, !dead && sprint ? run : 0f, run);
    }

    private AudioSource CreateSource(string path, string label)
    {
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning(label + " clip is missing at Resources/" + path + ".");
            return null;
        }

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = 220;
        source.volume = 0f;
        source.bypassReverbZones = true;
        return source;
    }

    private static void Drive(AudioSource source, ref bool started, float target, float fullVolume)
    {
        if (source == null)
            return;

        bool want = target > 0.001f;
        if (want && !source.isPlaying)
        {
            if (!started)
            {
                source.Play();
                started = true;
            }
            else
                source.UnPause();
        }

        if (fullVolume <= 0.001f)
            source.volume = target;
        else
        {
            float speed = fullVolume / FadeTime;
            source.volume = Mathf.MoveTowards(source.volume, target, speed * Time.deltaTime);
        }

        if (!want && source.isPlaying && source.volume <= 0.001f)
        {
            source.volume = 0f;
            source.Pause();
        }
    }
}
