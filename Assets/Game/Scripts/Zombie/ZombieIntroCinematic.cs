using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// When the gate releases the zombie, blends to a low camera in front of it for the crawl,
/// stand, and scream, then blends back to the player once it starts running.
/// Player input is off for the whole shot.
/// </summary>
[DisallowMultipleComponent]
public class ZombieIntroCinematic : MonoBehaviour
{
    [SerializeField] private float startDistance = 3.8f;
    [SerializeField] private float endDistance = 3.1f;
    [SerializeField] private float sideOffset = 0.9f;
    [SerializeField] private float cameraHeight = 0.6f;
    [SerializeField] private float fieldOfView = 42f;
    [SerializeField] private float blendTime = 1f;
    [SerializeField] private float maxShotTime = 16f;

    [Header("Fill Light")]
    [Tooltip("A soft light beside the shot camera so the face reads at night. 0 turns it off.")]
    [SerializeField] private float fillIntensity = 2.5f;
    [SerializeField] private float fillRange = 6f;
    [SerializeField] private Color fillColor = new Color(0.72f, 0.8f, 1f);

    private const int ShotPriority = 50;

    private ZombieController controller;
    private Animator animator;
    private Transform head;
    private Transform hips;
    private bool played;

    public static bool IsPlaying { get; private set; }

    private void Awake()
    {
        controller = GetComponent<ZombieController>();
        animator = GetComponentInChildren<Animator>();
        head = FindBone("Head");
        hips = FindBone("Hips");
        if (controller != null)
            controller.Released += Play;
    }

    private void OnDestroy()
    {
        if (controller != null)
            controller.Released -= Play;
        if (IsPlaying)
            Release();
    }

    private void Play()
    {
        if (played || !isActiveAndEnabled)
            return;

        played = true;
        StartCoroutine(Shot());
    }

    private IEnumerator Shot()
    {
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain == null)
        {
            Debug.LogWarning("[ZombieIntro] No CinemachineBrain on the main camera. Skipping the intro shot.");
            yield break;
        }

        IsPlaying = true;
        GameplayInput.SetBlocked(this, true);
        CinemachineBlendDefinition savedBlend = brain.DefaultBlend;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendTime);

        // Frame from where the zombie will face: toward the player.
        Vector3 facing = transform.forward;
        Transform target = controller != null ? controller.Player : null;
        if (target != null)
        {
            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                facing = toPlayer.normalized;
        }

        Vector3 side = Vector3.Cross(Vector3.up, facing).normalized;
        Vector3 anchor = BodyAnchor();
        Vector3 anchorVelocity = Vector3.zero;

        CinemachineCamera shot = CreateShotCamera();
        Transform shotTransform = shot.transform;
        shotTransform.position = ShotPosition(anchor, facing, side, startDistance);
        shotTransform.rotation = Quaternion.LookRotation(LookPoint() - shotTransform.position, Vector3.up);
        shot.Priority = ShotPriority;
        Debug.Log("[ZombieIntro] Cutting to the zombie.");

        float elapsed = 0f;
        while (elapsed < maxShotTime && !ScreamFinished())
        {
            elapsed += Time.deltaTime;
            float push = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 8f));
            // The crawl carries the body forward. Follow it so the framing holds.
            anchor = Vector3.SmoothDamp(anchor, BodyAnchor(), ref anchorVelocity, 0.5f);
            shotTransform.position = ShotPosition(anchor, facing, side, Mathf.Lerp(startDistance, endDistance, push));
            yield return null;
        }

        Debug.Log("[ZombieIntro] Back to the player.");
        shot.Priority = -1;
        Light fill = shot.GetComponentInChildren<Light>();
        float back = 0f;
        while (back < blendTime)
        {
            back += Time.deltaTime;
            if (fill != null)
                fill.intensity = fillIntensity * (1f - Mathf.Clamp01(back / blendTime));
            yield return null;
        }

        brain.DefaultBlend = savedBlend;
        Destroy(shot.gameObject);
        Release();
    }

    private void Release()
    {
        IsPlaying = false;
        GameplayInput.SetBlocked(this, false);
    }

    private Vector3 ShotPosition(Vector3 anchor, Vector3 facing, Vector3 side, float distance)
    {
        return anchor + facing * distance + side * sideOffset + Vector3.up * cameraHeight;
    }

    private Vector3 BodyAnchor()
    {
        if (hips == null)
            return transform.position;

        return new Vector3(hips.position.x, transform.position.y, hips.position.z);
    }

    private Vector3 LookPoint()
    {
        return head != null ? head.position : transform.position + Vector3.up * 0.5f;
    }

    private CinemachineCamera CreateShotCamera()
    {
        GameObject go = new GameObject("Zombie Intro Camera");
        CinemachineCamera vcam = go.AddComponent<CinemachineCamera>();
        vcam.Priority = -1;
        LensSettings lens = vcam.Lens;
        lens.FieldOfView = fieldOfView;
        lens.NearClipPlane = 0.05f;
        vcam.Lens = lens;

        // Track the head so the frame rises with it as the zombie stands.
        if (head != null)
        {
            vcam.LookAt = head;
            CinemachineRotationComposer composer = go.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.6f, 0.6f);
        }

        if (fillIntensity > 0f)
        {
            GameObject fill = new GameObject("Fill Light");
            fill.transform.SetParent(go.transform, false);
            fill.transform.localPosition = new Vector3(-0.6f, 0.5f, 0.2f);
            Light light = fill.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = fillColor;
            light.intensity = fillIntensity;
            light.range = fillRange;
            light.shadows = LightShadows.None;
        }

        return vcam;
    }

    private bool ScreamFinished()
    {
        if (controller == null || controller.IsDead)
            return true;
        if (animator == null)
            return true;

        return controller.IsChasing;
    }

    private Transform FindBone(string bareName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string name = all[i].name;
            int split = name.LastIndexOf(':');
            if ((split >= 0 ? name.Substring(split + 1) : name) == bareName)
                return all[i];
        }

        return null;
    }
}
