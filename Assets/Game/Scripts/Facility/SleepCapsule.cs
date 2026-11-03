using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Drives the capsule's baked open animation by hand so a cinematic can scrub it.
/// The clip starts sealed (time 0) and ends with the glass retracted.
/// </summary>
[DisallowMultipleComponent]
public class SleepCapsule : MonoBehaviour
{
    [SerializeField] private AnimationClip openClip;
    [Tooltip("Clip time where the glass starts to move. Earlier frames are idle.")]
    [SerializeField] private float openStart = 1.2f;
    [Tooltip("Clip time where the capsule has settled open.")]
    [SerializeField] private float openEnd = 8f;
    [SerializeField] private Light podLight;
    [SerializeField] private Color sealedColor = new Color(0.3f, 0.85f, 1f);
    [SerializeField] private Color openColor = new Color(1f, 0.85f, 0.6f);

    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private float podIntensity;

    public float Openness { get; private set; }

    private void Awake()
    {
        if (podLight != null)
            podIntensity = podLight.intensity;

        if (openClip == null)
            return;

        Animator animator = GetComponent<Animator>();
        if (animator == null)
            animator = gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;

        graph = PlayableGraph.Create(name + " Open");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable = AnimationClipPlayable.Create(graph, openClip);
        playable.SetApplyFootIK(false);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Capsule", animator);
        output.SetSourcePlayable(playable);
        graph.Play();
        SetOpenness(0f);
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }

    /// <summary>0 is sealed, 1 is fully open.</summary>
    public void SetOpenness(float amount)
    {
        Openness = Mathf.Clamp01(amount);
        if (graph.IsValid())
        {
            float time = Openness <= 0f ? 0f : Mathf.Lerp(openStart, Mathf.Min(openEnd, openClip.length), Openness);
            playable.SetTime(time);
            graph.Evaluate(0f);
        }

        if (podLight != null)
        {
            podLight.color = Color.Lerp(sealedColor, openColor, Openness);
            podLight.intensity = podIntensity * Mathf.Lerp(1f, 0.6f, Openness);
        }
    }

    /// <summary>Kicks the pod light, e.g. while the capsule shudders.</summary>
    public void Pulse(float amount)
    {
        if (podLight != null)
            podLight.intensity = podIntensity * (1f + amount);
    }
}
