using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PlayerAvatar : MonoBehaviour
{
    [SerializeField] private Transform modelSlot;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private float heightMatchTolerance = 0.15f;

    public Transform ModelSlot => modelSlot;
    public GameObject ModelPrefab => modelPrefab;
    public Animator BoundAnimator { get; private set; }

    private void Awake()
    {
        if (modelSlot == null)
            modelSlot = transform.Find("ModelSlot");

        Bind(false);
    }

    public void Bind(bool alignToCapsule)
    {
        if (modelSlot == null)
        {
            Debug.LogError("[PlayerAvatar] ModelSlot is missing.");
            return;
        }

        BoundAnimator = modelSlot.GetComponentInChildren<Animator>(true);
        if (BoundAnimator == null)
        {
            Debug.LogError("[PlayerAvatar] No Animator under ModelSlot. The Mixamo model needs a Humanoid avatar.");
            return;
        }

        if (BoundAnimator.avatar == null || !BoundAnimator.avatar.isHuman)
        {
            Debug.LogError("[PlayerAvatar] Import the Mixamo FBX as Humanoid, Avatar Definition: Create From This Model.");
        }

        if (animatorController != null)
            BoundAnimator.runtimeAnimatorController = animatorController;

        BoundAnimator.applyRootMotion = false;

        if (alignToCapsule)
            AlignToCapsule();

        if (!Application.isPlaying)
            return;

        GetComponent<PlayerController>()?.RecacheLimbs();
        GetComponent<HeldItemVisual>()?.RebindHand();
    }

    public void Swap(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[PlayerAvatar] No model assigned.");
            return;
        }

        if (modelSlot == null)
            modelSlot = transform.Find("ModelSlot");
        if (modelSlot == null)
        {
            Debug.LogError("[PlayerAvatar] ModelSlot is missing.");
            return;
        }

        HeldItemVisual held = GetComponent<HeldItemVisual>();
        held?.DetachHeldItems();

        for (int i = modelSlot.childCount - 1; i >= 0; i--)
            DestroyImmediate(modelSlot.GetChild(i).gameObject);

        GameObject instance = Instantiate(prefab, modelSlot);
        instance.name = prefab.name;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        modelPrefab = prefab;
        Bind(true);
    }

    public void AlignToCapsule()
    {
        CharacterController capsule = GetComponent<CharacterController>();
        if (capsule == null || modelSlot == null || modelSlot.childCount == 0)
            return;

        Transform visual = modelSlot.GetChild(0);
        if (!TryMeasureVisual(visual, out float bottom, out float top))
        {
            Debug.LogError("[PlayerAvatar] Model has no mesh bounds to align.");
            return;
        }

        float height = top - bottom;
        float target = capsule.height * 0.96f;
        if (height > 0.05f && target > 0.05f && Mathf.Abs(height - target) / target > heightMatchTolerance)
        {
            visual.localScale *= target / height;
            if (!TryMeasureVisual(visual, out bottom, out top))
                return;
        }

        float capsuleBottom = capsule.center.y - capsule.height * 0.5f;
        Vector3 local = visual.localPosition;
        local.y += capsuleBottom - bottom;
        visual.localPosition = local;
    }

    private bool TryMeasureVisual(Transform visual, out float bottom, out float top)
    {
        bottom = float.PositiveInfinity;
        top = float.NegativeInfinity;
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Bounds bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude < 0.0001f)
                continue;

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                float y = transform.InverseTransformPoint(center + Vector3.Scale(extents, sign)).y;
                if (y < bottom)
                    bottom = y;
                if (y > top)
                    top = y;
            }
        }

        return top > bottom;
    }
}
