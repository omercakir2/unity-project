using UnityEngine;

/// <summary>
/// A failing fluorescent: steady most of the time, then a burst of quick cut-outs.
/// Dims a lamp's emissive renderer along with the light when one is set.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [SerializeField] private Vector2 steadyTime = new Vector2(1.5f, 6f);
    [SerializeField] private Vector2 burstTime = new Vector2(0.15f, 0.9f);
    [SerializeField, Range(0f, 1f)] private float lowLevel = 0.05f;
    [SerializeField] private Renderer emissiveRenderer;
    [SerializeField] private Color emissionColor = Color.white;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Light lamp;
    private float baseIntensity;
    private float timer;
    private bool bursting;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        timer = Random.Range(steadyTime.x, steadyTime.y);
        if (emissiveRenderer != null)
            block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            bursting = !bursting;
            timer = bursting ? Random.Range(burstTime.x, burstTime.y) : Random.Range(steadyTime.x, steadyTime.y);
        }

        float level = 1f;
        if (bursting)
            level = Random.value < 0.45f ? lowLevel : Random.Range(0.6f, 1f);

        lamp.intensity = baseIntensity * level;
        if (block != null)
        {
            emissiveRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, emissionColor * level);
            emissiveRenderer.SetPropertyBlock(block);
        }
    }
}
