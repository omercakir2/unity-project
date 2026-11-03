using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Opening shot: the camera circles the sealed capsule, the pod shudders, the glass opens
/// and the player gasps awake. A blink to black drops the player outside the capsule, then
/// the blurred view clears and control comes back. Input and UI are off for the whole shot.
/// </summary>
[DisallowMultipleComponent]
public class WakeUpCinematic : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private PlayerController player;
    [SerializeField] private SleepCapsule capsule;
    [Tooltip("Feet position inside the pod. Forward is where the player faces.")]
    [SerializeField] private Transform sleepPoint;
    [Tooltip("Feet position outside the pod where control is handed back.")]
    [SerializeField] private Transform exitPoint;
    [Tooltip("Centre of the pod the orbit looks at.")]
    [SerializeField] private Transform focusPoint;

    [Header("Orbit")]
    [SerializeField] private float fadeInTime = 2f;
    [SerializeField] private float orbitTime = 10f;
    [SerializeField] private float orbitDegrees = 300f;
    [SerializeField] private float startRadius = 4.4f;
    [SerializeField] private float endRadius = 3.2f;
    [SerializeField] private float startHeight = 3.1f;
    [SerializeField] private float endHeight = 1.8f;
    [SerializeField] private float fieldOfView = 40f;

    [Header("Shake")]
    [SerializeField] private float shakeTime = 1.6f;
    [SerializeField] private float shakeAmplitude = 0.07f;
    [SerializeField] private float shakeFrequency = 20f;

    [Header("Open")]
    [SerializeField] private float openTime = 4.5f;
    [Tooltip("How close the camera drifts to the face while the glass opens.")]
    [SerializeField] private float faceDistance = 2.2f;

    [Header("Wake")]
    [SerializeField] private Volume blurVolume;
    [SerializeField] private float exitBlinkTime = 0.25f;
    [SerializeField] private float wakeFadeTime = 0.9f;
    [SerializeField] private float blurClearTime = 3.5f;
    [SerializeField] private AudioClip breathClip;
    [SerializeField, Range(0f, 1f)] private float breathVolume = 0.85f;
    [SerializeField] private float breathFadeTime = 5f;

    private const int ShotPriority = 100;
    private const string BreathFallbackPath = "Audio/Player/run-breath";

    private CinemachineBrain brain;
    private CinemachineCamera shot;
    private Image fade;
    private AudioSource breath;
    private Transform head;

    public static bool IsPlaying { get; private set; }
    public event Action Finished;

    private void Start()
    {
        // Coming back through a door: the player is already awake.
        if (SceneTransition.IsArriving)
        {
            if (capsule != null)
                capsule.SetOpenness(1f);
            return;
        }

        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (player == null || capsule == null || sleepPoint == null || exitPoint == null)
        {
            Debug.LogWarning("[WakeUp] Missing scene references. Skipping the wake-up shot.");
            return;
        }

        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
            head = animator.GetBoneTransform(HumanBodyBones.Head);

        IsPlaying = true;
        GameplayInput.SetBlocked(this, true);
        CinematicUI.SetHidden(this, true);
        CreateFade();
        SetPlayerAsleep(true);
        PlaceFeet(sleepPoint);
        if (blurVolume != null)
            blurVolume.weight = 0f;

        StartCoroutine(Play());
    }

    private void OnDestroy()
    {
        if (IsPlaying)
            Release();
    }

    private IEnumerator Play()
    {
        shot = CreateShotCamera();
        Vector3 center = focusPoint != null ? focusPoint.position : capsule.transform.position + Vector3.up * 1.8f;
        Vector3 forward = Flat(capsule.transform.forward);
        float endAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float groundY = capsule.transform.position.y;

        // Orbit in from black.
        float elapsed = 0f;
        while (elapsed < orbitTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / orbitTime);
            float ease = 1f - Mathf.Pow(1f - t, 2.2f);
            float angle = endAngle - orbitDegrees * (1f - ease);
            float radius = Mathf.Lerp(startRadius, endRadius, ease);
            float height = Mathf.Lerp(startHeight, endHeight, Mathf.SmoothStep(0f, 1f, t));
            Frame(OrbitPosition(center, angle, radius, groundY + height), center);
            fade.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(elapsed / fadeInTime));
            yield return null;
        }

        fade.color = Color.clear;
        Vector3 holdPosition = OrbitPosition(center, endAngle, endRadius, groundY + endHeight);
        yield return Hold(holdPosition, center, 0.6f);

        // The pod shudders as it wakes.
        elapsed = 0f;
        float seed = UnityEngine.Random.value * 100f;
        while (elapsed < shakeTime)
        {
            elapsed += Time.deltaTime;
            float envelope = Mathf.Sin(Mathf.Clamp01(elapsed / shakeTime) * Mathf.PI);
            Vector3 jitter = new Vector3(
                Mathf.PerlinNoise(seed, elapsed * shakeFrequency) - 0.5f,
                Mathf.PerlinNoise(seed + 7f, elapsed * shakeFrequency) - 0.5f,
                Mathf.PerlinNoise(seed + 13f, elapsed * shakeFrequency) - 0.5f) * (2f * shakeAmplitude * envelope);
            Frame(holdPosition + jitter, center + jitter * 0.5f);
            capsule.Pulse(envelope * Mathf.PerlinNoise(seed, elapsed * 9f));
            yield return null;
        }

        capsule.Pulse(0f);

        // Glass opens. The gasp starts as it unseals and the view swims.
        StartBreath();
        Vector3 facePoint = HeadPoint();
        Vector3 pushPosition = facePoint + forward * faceDistance + Vector3.up * 0.1f;
        elapsed = 0f;
        while (elapsed < openTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / openTime);
            capsule.SetOpenness(t);
            float push = Mathf.SmoothStep(0f, 1f, t);
            Frame(Vector3.Lerp(holdPosition, pushPosition, push), Vector3.Lerp(center, facePoint, push));
            if (breath != null)
                breath.volume = breathVolume * Mathf.Clamp01(t * 3f);
            if (blurVolume != null)
                blurVolume.weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.3f) / 0.7f));
            yield return null;
        }

        // Blink: the player is out of the capsule when the eyes open again.
        yield return FadeTo(1f, exitBlinkTime);
        capsule.SetOpenness(1f);
        PlaceFeet(exitPoint);
        SetPlayerAsleep(false);
        CutToGameplay();
        yield return null;

        GameplayInput.SetBlocked(this, false);
        yield return FadeTo(0f, wakeFadeTime);
        yield return Blink();

        elapsed = 0f;
        float breathStart = breath != null ? breath.volume : 0f;
        while (elapsed < Mathf.Max(blurClearTime, breathFadeTime))
        {
            elapsed += Time.deltaTime;
            if (blurVolume != null)
                blurVolume.weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blurClearTime));
            if (breath != null)
                breath.volume = breathStart * (1f - Mathf.Clamp01(elapsed / breathFadeTime));
            if (elapsed >= blurClearTime && CinematicUI.IsHidden)
                CinematicUI.SetHidden(this, false);
            yield return null;
        }

        if (breath != null)
            Destroy(breath);
        if (blurVolume != null)
            blurVolume.weight = 0f;
        Release();
        Finished?.Invoke();
    }

    private IEnumerator Hold(Vector3 position, Vector3 lookAt, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Frame(position, lookAt);
            yield return null;
        }
    }

    private IEnumerator Blink()
    {
        yield return new WaitForSeconds(0.35f);
        yield return FadeTo(0.92f, 0.12f);
        yield return FadeTo(0f, 0.3f);
    }

    private IEnumerator FadeTo(float alpha, float duration)
    {
        float from = fade.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, alpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        fade.color = new Color(0f, 0f, 0f, alpha);
    }

    private void Release()
    {
        IsPlaying = false;
        GameplayInput.SetBlocked(this, false);
        CinematicUI.SetHidden(this, false);
        if (shot != null)
            Destroy(shot.gameObject);
        if (fade != null)
            Destroy(fade.canvas.gameObject);
        SetPlayerAsleep(false);
    }

    private void Frame(Vector3 position, Vector3 lookAt)
    {
        Transform t = shot.transform;
        t.position = position;
        Vector3 view = lookAt - position;
        if (view.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(view, Vector3.up);
    }

    private void CutToGameplay()
    {
        if (brain != null)
        {
            CinemachineBlendDefinition saved = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            StartCoroutine(RestoreBlend(saved));
        }

        if (shot != null)
            shot.Priority = -1;
    }

    private IEnumerator RestoreBlend(CinemachineBlendDefinition saved)
    {
        yield return null;
        yield return null;
        if (brain != null)
            brain.DefaultBlend = saved;
    }

    // Asleep the body is parked in the pod: no gravity, no controller, no idle motion.
    private void SetPlayerAsleep(bool asleep)
    {
        CharacterController body = player.GetComponent<CharacterController>();
        if (body != null)
            body.enabled = !asleep;
        player.enabled = !asleep;

        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator != null)
            animator.speed = asleep ? 0f : 1f;
        HeadLook headLook = player.GetComponentInChildren<HeadLook>();
        if (headLook != null)
            headLook.enabled = !asleep;

        PlayerBreath idleBreath = player.GetComponent<PlayerBreath>();
        if (idleBreath != null)
            idleBreath.enabled = !asleep;
    }

    private void PlaceFeet(Transform point)
    {
        SceneSpawnPoint.PlacePlayer(player, point);
    }

    private Vector3 HeadPoint()
    {
        if (head != null)
            return head.position;

        return sleepPoint.position + Vector3.up * 1.65f;
    }

    private void StartBreath()
    {
        AudioClip clip = breathClip != null ? breathClip : Resources.Load<AudioClip>(BreathFallbackPath);
        if (clip == null)
            return;

        breath = player.gameObject.AddComponent<AudioSource>();
        breath.clip = clip;
        breath.loop = true;
        breath.playOnAwake = false;
        breath.spatialBlend = 0f;
        breath.pitch = 0.92f;
        breath.volume = 0f;
        breath.bypassReverbZones = true;
        breath.Play();
    }

    private CinemachineCamera CreateShotCamera()
    {
        GameObject go = new GameObject("Wake Up Camera");
        CinemachineCamera vcam = go.AddComponent<CinemachineCamera>();
        LensSettings lens = vcam.Lens;
        lens.FieldOfView = fieldOfView;
        lens.NearClipPlane = 0.05f;
        vcam.Lens = lens;
        vcam.Priority = ShotPriority;
        return vcam;
    }

    private void CreateFade()
    {
        GameObject canvasObject = new GameObject("Wake Up Fade", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CinematicUI.Exempt(canvas);

        GameObject imageObject = new GameObject("Black", typeof(RectTransform));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fade = imageObject.AddComponent<Image>();
        fade.color = Color.black;
        fade.raycastTarget = false;
    }

    private static Vector3 OrbitPosition(Vector3 center, float angle, float radius, float y)
    {
        float rad = angle * Mathf.Deg2Rad;
        return new Vector3(center.x + Mathf.Sin(rad) * radius, y, center.z + Mathf.Cos(rad) * radius);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
