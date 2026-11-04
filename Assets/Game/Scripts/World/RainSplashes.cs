using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Small rings flashing on the ground around the player, so the rain seems to land.
/// They are placed at random instead of from drop collisions, which would cost far more.
/// </summary>
[DisallowMultipleComponent]
public class RainSplashes : MonoBehaviour
{
    private const float Area = 20f;
    private const int RingsPerSecond = 320;
    private const float Lifetime = 0.16f;
    private const float GroundProbeInterval = 0.2f;
    private const float GroundOffset = 0.03f;
    private const int RingTextureSize = 64;
    private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";

    private static readonly Color RingColor = new Color(0.72f, 0.78f, 0.9f, 0.34f);

    private Transform splashes;
    private Material material;
    private Texture2D ringTexture;
    private float nextProbeTime;

    private void Start()
    {
        GameObject go = new GameObject("Rain Splashes");
        go.transform.SetParent(transform, false);
        splashes = go.transform;
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        Configure(system);
        system.Play();
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
        if (ringTexture != null)
            Destroy(ringTexture);
    }

    private void LateUpdate()
    {
        if (splashes == null || Time.time < nextProbeTime)
            return;

        nextProbeTime = Time.time + GroundProbeInterval;
        Vector3 origin = transform.position + Vector3.up;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            splashes.position = hit.point + Vector3.up * GroundOffset;
    }

    private void Configure(ParticleSystem system)
    {
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Lifetime * 0.7f, Lifetime);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.26f);
        main.startColor = RingColor;
        main.maxParticles = 128;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = RingsPerSecond;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Area, 0f, Area);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1f));

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.lightProbeUsage = LightProbeUsage.Off;
        particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        particleRenderer.sharedMaterial = CreateMaterial();
    }

    private Material CreateMaterial()
    {
        ringTexture = CreateRingTexture();
        material = new Material(Shader.Find(ShaderName));
        material.SetTexture("_BaseMap", ringTexture);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static Texture2D CreateRingTexture()
    {
        Texture2D texture = new Texture2D(RingTextureSize, RingTextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float step = 2f / RingTextureSize;
        for (int y = 0; y < RingTextureSize; y++)
        {
            for (int x = 0; x < RingTextureSize; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) * step - 1f, (y + 0.5f) * step - 1f);
                float band = 1f - Mathf.Abs(p.magnitude - 0.72f) / 0.2f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(band)));
            }
        }

        texture.Apply(false, true);
        return texture;
    }
}
