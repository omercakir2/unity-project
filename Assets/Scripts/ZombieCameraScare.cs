using UnityEngine;

// Plays the ghost sting once, on the second time the zombie's body stays in the gameplay camera frame.
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class ZombieCameraScare : MonoBehaviour
{
    private const string StingPath = "Audio/Stingers/scary-ghost-effect";
    private const float EnterDelay = 0.15f;
    private const float ExitDelay = 0.4f;

    private AmbienceSettings settings;
    private AudioSource stingSource;
    private AudioClip sting;
    private Renderer[] renderers;
    private float inFrameTime;
    private float outFrameTime;
    private bool sightingOpen;
    private int sightings;
    private bool stingPlayed;

    private void Awake()
    {
        sting = Resources.Load<AudioClip>(StingPath);
        if (sting == null)
        {
            Debug.LogWarning("Scary ghost clip is missing at Resources/" + StingPath + ".");
            return;
        }

        stingSource = gameObject.AddComponent<AudioSource>();
        stingSource.playOnAwake = false;
        stingSource.loop = false;
        stingSource.spatialBlend = 0f;
        stingSource.dopplerLevel = 0f;
        stingSource.priority = 32;
        settings = AmbienceSettings.LoadOrDefault();
        stingSource.volume = settings != null ? settings.ghostStingVolume : 0.75f;
        stingSource.bypassReverbZones = true;
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        if (stingSource != null && settings != null)
            stingSource.volume = settings.ghostStingVolume;

        if (stingPlayed)
            return;

        if (InGameplayFrame())
        {
            outFrameTime = 0f;
            inFrameTime += Time.deltaTime;
            if (sightingOpen || inFrameTime < EnterDelay)
                return;

            sightingOpen = true;
            sightings++;
            if (sightings == 1)
                Debug.Log("[Zombie] First look.");
            else if (sightings == 2)
                PlaySting();
        }
        else
        {
            inFrameTime = 0f;
            if (!sightingOpen)
                return;

            outFrameTime += Time.deltaTime;
            if (outFrameTime < ExitDelay)
                return;

            sightingOpen = false;
            outFrameTime = 0f;
        }
    }

    private void PlaySting()
    {
        stingPlayed = true;
        Debug.Log("[Zombie] Second look. Playing the scary sting.");
        if (stingSource != null && sting != null)
            stingSource.PlayOneShot(sting);
    }

    private bool InGameplayFrame()
    {
        Camera view = Camera.main;
        if (view == null || renderers == null)
            return false;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(view);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                continue;
            if (!GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                continue;

            Vector3 viewport = view.WorldToViewportPoint(renderer.bounds.center);
            if (viewport.z <= 0f)
                continue;
            if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                continue;
            return true;
        }

        return false;
    }
}
