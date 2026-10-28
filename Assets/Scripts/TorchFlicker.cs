using UnityEngine;

[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] public float baseIntensity = 4.5f;
    [SerializeField] public float flickerIntensity = 2.2f;
    [SerializeField] public float flickerSpeed = 12f;
    [SerializeField] public float colorFlickerAmount = 0.15f;

    [Header("Flame Visuals (if ParticleSystem present)")]
    [SerializeField] private ParticleSystem flameParticles;

    private const string FireClipPath = "Audio/Torch/torch-fire-sound";

    private Light torchLight;
    private AmbienceSettings settings;
    private AudioSource fireSource;
    private bool missingClipLogged;
    private float timer;
    private float targetIntensity;
    private Color baseColor = new Color(1f, 0.55f, 0.15f); // Warm fire orange

    private void Awake()
    {
        settings = AmbienceSettings.LoadOrDefault();
        torchLight = GetComponent<Light>();
        torchLight.type = LightType.Point;
        torchLight.color = baseColor;
        torchLight.range = 9f;
        torchLight.shadowStrength = 0.4f;
        torchLight.shadows = LightShadows.Soft;
        // renderMode omitted - URP handles automatically for dynamic lights

        if (flameParticles == null)
        {
            flameParticles = GetComponentInChildren<ParticleSystem>();
        }

        targetIntensity = baseIntensity;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        StartFire();
    }

    private void OnDisable()
    {
        if (fireSource != null)
            fireSource.Stop();
    }

    private void StartFire()
    {
        if (fireSource == null)
        {
            AudioClip clip = Resources.Load<AudioClip>(FireClipPath);
            if (clip == null)
            {
                if (!missingClipLogged)
                {
                    missingClipLogged = true;
                    Debug.LogWarning("Torch fire clip is missing at Resources/" + FireClipPath + ".");
                }

                return;
            }

            fireSource = gameObject.AddComponent<AudioSource>();
            fireSource.clip = clip;
            fireSource.loop = true;
            fireSource.playOnAwake = false;
            fireSource.spatialBlend = 1f;
            fireSource.rolloffMode = AudioRolloffMode.Logarithmic;
            fireSource.minDistance = 0.6f;
            fireSource.maxDistance = 10f;
            fireSource.volume = settings != null ? Mathf.Clamp01(settings.torchVolume) : 0.4f;
            fireSource.dopplerLevel = 0f;
            fireSource.priority = 180;
        }

        if (!fireSource.isPlaying)
            fireSource.Play();
    }

    // A playing 3D source keeps its old loudness until it is started again.
    private void ApplyFireLevel(float level)
    {
        if (fireSource == null || Mathf.Approximately(fireSource.volume, level))
            return;

        float time = fireSource.time;
        bool wasPlaying = fireSource.isPlaying;
        fireSource.volume = level;
        if (!wasPlaying)
            return;

        fireSource.Stop();
        fireSource.Play();
        if (fireSource.clip != null && time > 0f && time < fireSource.clip.length)
            fireSource.time = time;
    }

    private void Update()
    {
        if (Application.isPlaying && fireSource != null && settings != null)
            ApplyFireLevel(Mathf.Clamp01(settings.torchVolume));

        // Flicker the light intensity like a real torch
        timer += Time.deltaTime * flickerSpeed;
        if (timer > 0.1f)
        {
            targetIntensity = baseIntensity + Random.Range(-flickerIntensity, flickerIntensity * 0.6f);
            timer = 0f;
        }

        torchLight.intensity = Mathf.Lerp(torchLight.intensity, targetIntensity, Time.deltaTime * 15f);

        // Subtle color variation for realistic fire (more yellow/orange shifts)
        float r = baseColor.r + Random.Range(-colorFlickerAmount, colorFlickerAmount * 0.5f);
        float g = baseColor.g + Random.Range(-colorFlickerAmount * 0.6f, colorFlickerAmount);
        torchLight.color = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), baseColor.b * 0.8f);
    }

    // Optional: Call this to vary flicker based on wind or movement
    public void SetFlickerMultiplier(float multiplier)
    {
        flickerIntensity = 2.2f * multiplier;
    }
}
