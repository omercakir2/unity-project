using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Local rain around the player, plus a 2D ambience loop.
// A field curtain stays near the player. A second, longer layer sits in front of the camera.
// The rest of the scene is not simulated.
[DisallowMultipleComponent]
public class EnvironmentRain : MonoBehaviour
{
    private const string ClipResourcePath = "Audio/Environment_sound/rain-loop-sound-effect-yart-0-r";
    private const string StreakResourcePath = "VFX/RainStreak";
    private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";

    private static EnvironmentRain instance;

    private AmbienceSettings settings;
    private ParticleSystem rain;
    private ParticleSystem foreground;
    private Material rainMaterial;
    private AudioSource source;
    private PlayerController player;
    private Camera viewCamera;
    private bool emitting;
    private bool tuningApplied;
    private float appliedArea;
    private float appliedHeight;
    private float appliedFallSpeed;
    private float appliedLifetime;
    private float appliedRate;
    private int appliedMaxDrops;
    private float appliedWidth;
    private Color appliedColor;
    private float appliedStreak;
    private float appliedWind;
    private float appliedVolume;
    private int appliedPriority;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartRain()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureRain();
    }

    // The game can start indoors, so outdoor scenes loaded later bring the rain up themselves.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRain();
    }

    private static void EnsureRain()
    {
        if (instance != null || IndoorScene.IsActive)
            return;

        GameObject rainObject = new GameObject("Environment Rain");
        rainObject.AddComponent<EnvironmentRain>();
    }

    /// <summary>Removes the rain, e.g. when an indoor scene loads.</summary>
    public static void Stop()
    {
        if (instance != null)
            Destroy(instance.gameObject);
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
        transform.localScale = Vector3.one;
        settings = AmbienceSettings.LoadOrDefault();

        SetupAudio();
        SetupParticles();
        ApplyTuning();
        gameObject.AddComponent<RainSplashes>();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (rainMaterial != null)
            Destroy(rainMaterial);
    }

    private void LateUpdate()
    {
        ApplyTuning();

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                SetEmitting(false);
                return;
            }
        }

        // World-up only. Parenting to the player would tilt the rain when the character turns.
        transform.SetPositionAndRotation(player.transform.position, Quaternion.identity);
        PlaceForeground();
        SetEmitting(true);
    }

    private void SetupAudio()
    {
        AudioClip clip = Resources.Load<AudioClip>(ClipResourcePath);
        if (clip == null)
        {
            Debug.LogWarning("Rain clip is missing at Resources/" + ClipResourcePath + ".");
            return;
        }

        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = settings.rainVolume;
        source.priority = settings.rainPriority;
        source.dopplerLevel = 0f;
        source.bypassReverbZones = true;
        source.Play();
    }

    private void SetupParticles()
    {
        rain = GetComponent<ParticleSystem>();
        if (rain == null)
            rain = gameObject.AddComponent<ParticleSystem>();

        GameObject nearObject = new GameObject("Rain Foreground");
        nearObject.transform.SetParent(transform, false);
        foreground = nearObject.AddComponent<ParticleSystem>();

        ConfigureSystem(rain);
        ConfigureSystem(foreground);
        AssignRainMaterial(rain.GetComponent<ParticleSystemRenderer>());
        foreground.GetComponent<ParticleSystemRenderer>().sharedMaterial = rainMaterial;
        emitting = false;
    }

    private static void ConfigureSystem(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Custom;
        main.emitterVelocity = Vector3.zero;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.cullingMode = ParticleSystemCullingMode.Automatic;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.randomDirectionAmount = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = false;
        ParticleSystem.TrailModule trails = ps.trails;
        trails.enabled = false;
        ParticleSystem.LightsModule lights = ps.lights;
        lights.enabled = false;
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = false;
        ParticleSystem.SubEmittersModule subEmitters = ps.subEmitters;
        subEmitters.enabled = false;
        ParticleSystem.TriggerModule trigger = ps.trigger;
        trigger.enabled = false;

        ParticleSystemRenderer particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.lengthScale = 0f;
        particleRenderer.cameraVelocityScale = 0f;
        particleRenderer.maxParticleSize = 1f;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.lightProbeUsage = LightProbeUsage.Off;
        particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        particleRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void ApplyTuning()
    {
        if (settings == null)
            return;

        if (source != null &&
            (!Mathf.Approximately(appliedVolume, settings.rainVolume) || appliedPriority != settings.rainPriority))
        {
            source.volume = settings.rainVolume;
            source.priority = settings.rainPriority;
            appliedVolume = settings.rainVolume;
            appliedPriority = settings.rainPriority;
        }

        if (rain == null)
            return;

        bool same = tuningApplied
            && Mathf.Approximately(appliedArea, settings.areaSize)
            && Mathf.Approximately(appliedHeight, settings.emitHeight)
            && Mathf.Approximately(appliedFallSpeed, settings.fallSpeed)
            && Mathf.Approximately(appliedLifetime, settings.dropLifetime)
            && Mathf.Approximately(appliedRate, settings.dropsPerSecond)
            && appliedMaxDrops == settings.maxDrops
            && Mathf.Approximately(appliedWidth, settings.dropWidth)
            && appliedColor == settings.dropColor
            && Mathf.Approximately(appliedStreak, settings.streakLength)
            && Mathf.Approximately(appliedWind, settings.wind);
        if (same)
            return;

        ApplyLayer(rain, 1f, 1f, 1f, 1f, Mathf.Max(1f, settings.areaSize), settings.emitHeight, 1f);
        if (foreground != null)
        {
            float nearArea = Mathf.Clamp(settings.areaSize * 0.45f, 6f, 12f);
            ApplyLayer(foreground, 0.8f, 0.55f, 0.55f, 1.8f, nearArea, 3.2f, 0.65f);
            ParticleSystem.ShapeModule nearShape = foreground.shape;
            Vector3 nearScale = nearShape.scale;
            // A shallow sheet in front of the lens. A deep box spawns streaks inside the camera.
            nearShape.scale = new Vector3(nearScale.x, nearScale.y, 2.2f);
        }

        appliedArea = settings.areaSize;
        appliedHeight = settings.emitHeight;
        appliedFallSpeed = settings.fallSpeed;
        appliedLifetime = settings.dropLifetime;
        appliedRate = settings.dropsPerSecond;
        appliedMaxDrops = settings.maxDrops;
        appliedWidth = settings.dropWidth;
        appliedColor = settings.dropColor;
        appliedStreak = settings.streakLength;
        appliedWind = settings.wind;
        tuningApplied = true;
    }

    private void ApplyLayer(
        ParticleSystem ps,
        float rateScale,
        float maxScale,
        float widthScale,
        float streakScale,
        float area,
        float height,
        float lifetimeScale)
    {
        float fall = Mathf.Max(0f, settings.fallSpeed);
        float width = Mathf.Max(0.001f, settings.dropWidth * widthScale);
        float lifetime = Mathf.Max(0.05f, settings.dropLifetime * lifetimeScale);
        Color color = settings.dropColor;
        Color faint = color;
        faint.a *= 0.35f;
        Color bright = color;
        bright.a = Mathf.Clamp01(color.a * 1.6f);

        ParticleSystem.MainModule main = ps.main;
        main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(settings.maxDrops * maxScale));
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.55f, width);
        main.startColor = new ParticleSystem.MinMaxGradient(faint, bright);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = Mathf.Max(0f, settings.dropsPerSecond * rateScale);

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.scale = new Vector3(area, 0.6f, area);
        shape.position = new Vector3(0f, height, 0f);

        float wind = settings.wind;
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(wind - 0.5f, wind + 0.5f);
        velocity.y = new ParticleSystem.MinMaxCurve(-fall, -fall * 0.82f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        ParticleSystemRenderer particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            // Length is in meters, independent of width, so close streaks stay long without turning into bars.
            float streakMeters = Mathf.Max(0.3f, settings.streakLength * 18f * streakScale);
            particleRenderer.velocityScale = settings.streakLength * streakScale * 0.35f;
            particleRenderer.lengthScale = streakMeters / width;
        }
    }

    private void PlaceForeground()
    {
        if (foreground == null)
            return;

        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera == null)
            return;

        Vector3 flatForward = viewCamera.transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        // Just in front of the lens, still world-upright, so the frame fills with long streaks.
        foreground.transform.SetPositionAndRotation(
            viewCamera.transform.position + flatForward * 3.4f,
            Quaternion.identity);
    }

    private void AssignRainMaterial(ParticleSystemRenderer particleRenderer)
    {
        Shader shader = Shader.Find(ParticleShaderName);
        if (shader == null)
        {
            Debug.LogWarning("Rain particles kept the default material because " + ParticleShaderName + " was not found.");
            return;
        }

        Material template = null;
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset pipeline)
            template = pipeline.defaultParticleMaterial;

        if (template != null && template.shader == shader)
        {
            rainMaterial = new Material(template);
        }
        else
        {
            rainMaterial = new Material(shader);
            rainMaterial.SetFloat("_Surface", 1f);
            rainMaterial.SetFloat("_Blend", 0f);
            rainMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            rainMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            rainMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            rainMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            rainMaterial.SetFloat("_ZWrite", 0f);
            rainMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            rainMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            rainMaterial.DisableKeyword("_ALPHATEST_ON");
            rainMaterial.SetOverrideTag("RenderType", "Transparent");
            rainMaterial.renderQueue = (int)RenderQueue.Transparent;
            if (template != null && template.HasProperty("_BaseMap"))
                rainMaterial.SetTexture("_BaseMap", template.GetTexture("_BaseMap"));
        }

        Texture2D streak = Resources.Load<Texture2D>(StreakResourcePath);
        if (streak != null)
        {
            rainMaterial.SetTexture("_BaseMap", streak);
            rainMaterial.SetTexture("_MainTex", streak);
        }

        rainMaterial.name = "Rain Streaks";
        particleRenderer.sharedMaterial = rainMaterial;
    }

    private void SetEmitting(bool shouldEmit)
    {
        if (rain == null || emitting == shouldEmit)
            return;

        emitting = shouldEmit;
        SetOneEmitting(rain, shouldEmit);
        SetOneEmitting(foreground, shouldEmit);
    }

    private static void SetOneEmitting(ParticleSystem ps, bool shouldEmit)
    {
        if (ps == null)
            return;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = shouldEmit;
        if (shouldEmit)
            ps.Play();
        else
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
