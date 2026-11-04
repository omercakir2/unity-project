using UnityEngine;

// The first confirmed look plays the what-the-fuck line and the ghost sting.
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class ZombieCameraScare : MonoBehaviour
{
    private const string WtfPath = "Audio/Stingers/wtf-is-that-sound";
    private const string StingPath = "Audio/Stingers/scary-ghost-effect";
    private const string WtfLine = "What the fuck is that?";
    private const float EnterDelay = 0.15f;

    private AmbienceSettings settings;
    private ZombieController controller;
    private AudioSource wtfSource;
    private AudioSource stingSource;
    private AudioClip wtf;
    private AudioClip sting;
    private Transform head;
    private Transform chest;
    private Transform hips;
    private float inFrameTime;
    private bool wtfPlayed;
    private bool stingPlayed;

    private void Awake()
    {
        settings = AmbienceSettings.LoadOrDefault();
        controller = GetComponent<ZombieController>();
        CacheBodyPoints();
        wtf = Resources.Load<AudioClip>(WtfPath);
        sting = Resources.Load<AudioClip>(StingPath);
        if (wtf == null)
            Debug.LogWarning("What the fuck clip is missing at Resources/" + WtfPath + ".");
        if (sting == null)
            Debug.LogWarning("Scary ghost clip is missing at Resources/" + StingPath + ".");

        wtfSource = CreateVoice(36, settings != null ? settings.wtfVolume : 0.9f);
        stingSource = CreateVoice(32, settings != null ? settings.ghostStingVolume : 0.75f);
    }

    private AudioSource CreateVoice(int priority, float volume)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = priority;
        source.volume = volume;
        source.bypassReverbZones = true;
        return source;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        if (controller != null && controller.IsWaitingForGate)
            return;

        if (settings != null)
        {
            if (wtfSource != null)
                wtfSource.volume = settings.wtfVolume;
            if (stingSource != null)
                stingSource.volume = settings.ghostStingVolume;
        }

        if (wtfPlayed && stingPlayed)
            return;

        if (!InGameplayFrame())
        {
            inFrameTime = 0f;
            return;
        }

        inFrameTime += Time.deltaTime;
        if (inFrameTime < EnterDelay)
            return;

        PlayWtf();
        PlaySting();
    }

    private void PlayWtf()
    {
        if (wtfPlayed)
            return;

        wtfPlayed = true;
        Debug.Log("[Zombie] First look. Playing what the fuck.");
        if (wtfSource != null && wtf != null)
            wtfSource.PlayOneShot(wtf);
        Subtitles.Show(WtfLine, wtf != null ? wtf.length : 2.5f);
    }

    private void PlaySting()
    {
        stingPlayed = true;
        Debug.Log("[Zombie] First look. Playing the scary sting.");
        if (stingSource != null && sting != null)
            stingSource.PlayOneShot(sting);
    }

    private void CacheBodyPoints()
    {
        Transform spine = null;
        Transform spine1 = null;
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string bare = BareName(all[i].name);
            if (bare == "Head")
                head = all[i];
            else if (bare == "Hips")
                hips = all[i];
            else if (bare == "Spine2")
                chest = all[i];
            else if (bare == "Spine1")
                spine1 = all[i];
            else if (bare == "Spine")
                spine = all[i];
        }

        if (chest == null)
            chest = spine1 != null ? spine1 : spine;
    }

    private static string BareName(string boneName)
    {
        int split = boneName.LastIndexOf(':');
        return split >= 0 ? boneName.Substring(split + 1) : boneName;
    }

    // The skinned bounds hang below the crawler, so their center leaves the
    // frame while the head and chest are still visible.
    private bool InGameplayFrame()
    {
        Camera view = Camera.main;
        if (view == null)
            return false;

        if (PointInFrame(view, head) || PointInFrame(view, chest) || PointInFrame(view, hips))
            return true;

        if (head == null && chest == null && hips == null)
            return PointInFrame(view, transform.position + Vector3.up);

        return false;
    }

    private static bool PointInFrame(Camera view, Transform point)
    {
        return point != null && PointInFrame(view, point.position);
    }

    private static bool PointInFrame(Camera view, Vector3 world)
    {
        Vector3 viewport = view.WorldToViewportPoint(world);
        return viewport.z > 0f
            && viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
    }
}
