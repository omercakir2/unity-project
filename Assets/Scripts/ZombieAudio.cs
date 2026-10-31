using UnityEngine;

// Crawl loop while the zombie is on the ground, and one scream as it stands up.
[DisallowMultipleComponent]
public class ZombieAudio : MonoBehaviour
{
    private const string CrawlClipPath = "Audio/Zombie/zombie-crawling-sound";
    private const string ScreamClipPath = "Audio/Zombie/zombie-scream";

    private Animator animator;
    private ZombieController controller;
    private AmbienceSettings settings;
    private AudioSource crawlSource;
    private AudioSource screamSource;
    private bool screamPlayed;
    private bool silenced;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<ZombieController>();
        settings = AmbienceSettings.LoadOrDefault();
        float crawl = CrawlLevel();
        float scream = ScreamLevel();
        // Full loudness across the yard, then a linear fade. Logarithmic rolloff
        // dropped the sound so fast that every level above zero sounded the same.
        crawlSource = CreateSource(CrawlClipPath, "Zombie crawl", true, crawl, 16f, 50f, 160);
        screamSource = CreateSource(ScreamClipPath, "Zombie scream", false, scream, 20f, 60f, 48);
    }

    public void Silence()
    {
        silenced = true;
        if (crawlSource != null)
            crawlSource.Stop();
        if (screamSource != null)
            screamSource.Stop();
    }

    private void Update()
    {
        if (!Application.isPlaying || silenced || animator == null || animator.runtimeAnimatorController == null)
            return;

        if (controller != null && controller.IsWaitingForGate)
        {
            if (crawlSource != null && crawlSource.isPlaying)
                crawlSource.Stop();
            return;
        }

        ApplyLevel(crawlSource, CrawlLevel());
        ApplyLevel(screamSource, ScreamLevel());

        bool crawling = InState("Crawl");
        if (crawlSource != null)
        {
            if (crawling)
            {
                if (!crawlSource.isPlaying)
                    crawlSource.Play();
            }
            else if (crawlSource.isPlaying)
                crawlSource.Stop();
        }

        if (!screamPlayed && InState("Scream") && screamSource != null && screamSource.clip != null)
        {
            screamPlayed = true;
            screamSource.Play();
        }
    }

    private bool InState(string stateName)
    {
        if (animator.IsInTransition(0))
            return animator.GetNextAnimatorStateInfo(0).IsName(stateName);

        return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
    }

    private AudioSource CreateSource(string path, string label, bool loop, float volume, float minDistance, float maxDistance, int priority)
    {
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning(label + " clip is missing at Resources/" + path + ".");
            return null;
        }

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.priority = priority;
        source.volume = volume;
        return source;
    }

    // A playing 3D source keeps its old loudness until it is started again.
    // Zero is the exception, which is why the slider seemed to work only when muted.
    private static void ApplyLevel(AudioSource source, float level)
    {
        if (source == null || Mathf.Approximately(source.volume, level))
            return;

        float time = source.time;
        bool wasPlaying = source.isPlaying;
        source.volume = level;
        if (!wasPlaying)
            return;

        source.Stop();
        source.Play();
        if (source.clip != null && time > 0f && time < source.clip.length)
            source.time = time;
    }

    private float CrawlLevel()
    {
        if (settings == null)
            return 0.65f;

        return Mathf.Clamp01(settings.zombieCrawlVolume);
    }

    private float ScreamLevel()
    {
        if (settings == null)
            return 0.9f;

        return Mathf.Clamp01(settings.zombieScreamVolume);
    }
}
