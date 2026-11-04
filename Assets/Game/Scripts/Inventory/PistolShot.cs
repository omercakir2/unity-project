using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Hitscan shot for the equipped pistol. The ray leaves the muzzle along the barrel.
/// </summary>
public class PistolShot : MonoBehaviour
{
    [SerializeField] private float range = 70f;
    [SerializeField] private float cooldown = 0.32f;
    // No reload yet: the gun comes loaded and the reserve stays empty.
    [SerializeField] private int magazineSize = 7;
    [SerializeField] private int reserveAmmo = 0;

    [Header("Spread (degrees, cone half-angle)")]
    [SerializeField] private float idleSpread = 0.5f;
    [SerializeField] private float walkSpread = 2f;
    [SerializeField] private float sprintSpread = 5f;
    [SerializeField] private float airSpread = 7f;
    [SerializeField] private float shotSpreadKick = 1.5f;
    [SerializeField] private float maxShotSpread = 4f;
    [SerializeField] private float spreadSharpness = 10f;
    [SerializeField] private float shotSpreadRecovery = 6f;

    private int ammoInMagazine = -1;
    private float moveSpread;
    private float shotSpread;

    /// <summary>Current cone half-angle in degrees. The crosshair opens to match it.</summary>
    public float CurrentSpread => moveSpread + shotSpread;

    public int AmmoInMagazine => ammoInMagazine < 0 ? magazineSize : ammoInMagazine;
    public int ReserveAmmo => reserveAmmo;
    public event System.Action AmmoChanged;

    /// <summary>Raised on every zombie hit. The argument is true when the hit killed it.</summary>
    public event System.Action<bool> ZombieHit;

    private HeldItemVisual held;
    private PlayerInventory inventory;
    private PlayerController player;
    private CharacterController body;
    private InputAction attack;
    private float nextShotTime;
    private float slideKick;
    private Transform slide;
    private Vector3 slideRest;
    private bool slideCached;
    private const string ShotResourcePath = "Audio/FreeWeaponSFX_v1.1/Handgun/Gunshots/sfx_wpn_hgn_gunshot_03";
    private const string TailResourcePath = "Audio/FreeWeaponSFX_v1.1/Handgun/Gunshots/sfx_wpn_hgn_tail_01";
    private const string DryResourcePath = "Audio/FreeWeaponSFX_v1.1/Handgun/Reload/sfx_wpn_hgn_reload_01_slideLock";

    private AudioSource audioSource;
    private AmbienceSettings settings;
    private AudioClip shotClip;
    private AudioClip tailClip;
    private AudioClip dryClip;

    private void Awake()
    {
        held = GetComponent<HeldItemVisual>();
        inventory = GetComponent<PlayerInventory>();
        player = GetComponent<PlayerController>();
        body = GetComponent<CharacterController>();
        moveSpread = idleSpread;
        if (ammoInMagazine < 0)
            ammoInMagazine = magazineSize;
        settings = AmbienceSettings.LoadOrDefault();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        audioSource.dopplerLevel = 0f;
        shotClip = Resources.Load<AudioClip>(ShotResourcePath);
        tailClip = Resources.Load<AudioClip>(TailResourcePath);
        dryClip = Resources.Load<AudioClip>(DryResourcePath);
        if (shotClip == null)
            Debug.LogWarning("[PistolShot] Missing gunshot at Resources/" + ShotResourcePath + ".");
        else
            shotClip.LoadAudioData();
        if (tailClip == null)
            Debug.LogWarning("[PistolShot] Missing gunshot tail at Resources/" + TailResourcePath + ".");
        else
            tailClip.LoadAudioData();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        EaseSlide();
        UpdateSpread();
        if (!AttackPressed())
            return;

        Fire();
    }

    private bool AttackPressed()
    {
        if (attack == null)
            attack = FindAttack();
        if (attack != null)
            return attack.WasPressedThisFrame();
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    public bool Fire()
    {
        if (held == null || inventory == null)
            return false;
        if (player != null && player.IsInspecting)
            return false;
        if (inventory.ActiveItemId != PlayerInventory.PistolItemId)
            return false;
        if (held.PistolPoseWeight < 0.55f)
            return false;
        if (player != null && player.IsTraversing)
            return false;
        // Real time: in the zombie slow motion a scaled cooldown stretches to over a second.
        if (Time.unscaledTime < nextShotTime)
            return false;

        nextShotTime = Time.unscaledTime + cooldown;
        if (AmmoInMagazine <= 0)
        {
            if (audioSource != null && dryClip != null)
                audioSource.PlayOneShot(dryClip, 0.8f);
            return false;
        }

        ammoInMagazine = AmmoInMagazine - 1;
        AmmoChanged?.Invoke();

        Vector3 muzzle = transform.position + Vector3.up * 1.4f;
        Vector3 shotDirection = transform.forward;
        if (!held.TryGetMuzzle(out muzzle, out shotDirection) || shotDirection.sqrMagnitude < 0.0001f)
            shotDirection = transform.forward;
        shotDirection = Scatter(shotDirection.normalized, CurrentSpread);
        shotSpread = Mathf.Min(maxShotSpread, shotSpread + shotSpreadKick);

        Vector3 end = muzzle + shotDirection * range;
        RaycastHit[] hits = Physics.RaycastAll(muzzle, shotDirection, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;
            end = hits[i].point;
            ZombieController zombie = hitTransform.GetComponentInParent<ZombieController>();
            if (zombie != null)
                ZombieHit?.Invoke(zombie.TakeHit(end, shotDirection));
            break;
        }

        SpawnTracer(muzzle, end);
        held.KickPistol();
        KickSlide();

        float shotVolume = settings != null ? settings.pistolShotVolume : 0.35f;
        float tailVolume = settings != null ? settings.pistolTailVolume : 1f;
        if (audioSource != null && shotClip != null)
            audioSource.PlayOneShot(shotClip, shotVolume);
        if (audioSource != null && tailClip != null)
            audioSource.PlayOneShot(tailClip, tailVolume);

        return true;
    }

    private void UpdateSpread()
    {
        float target = idleSpread;
        if (player != null && body != null && body.enabled)
        {
            Vector3 planar = body.velocity;
            planar.y = 0f;
            float speed = planar.magnitude;
            float walk = Mathf.Max(0.01f, player.WalkSpeed);
            float sprint = Mathf.Max(walk + 0.01f, player.SprintSpeed);
            target = speed <= walk
                ? Mathf.Lerp(idleSpread, walkSpread, speed / walk)
                : Mathf.Lerp(walkSpread, sprintSpread, (speed - walk) / (sprint - walk));
            if (!player.IsGrounded)
                target = Mathf.Max(target, airSpread);
        }
        else if (player != null && player.IsTraversing)
        {
            target = airSpread;
        }

        moveSpread = Mathf.Lerp(moveSpread, target, 1f - Mathf.Exp(-spreadSharpness * Time.deltaTime));
        shotSpread = Mathf.MoveTowards(shotSpread, 0f, shotSpreadRecovery * Time.deltaTime);
    }

    // Uniform pick inside the cone, so the hits fill the crosshair gap evenly.
    private static Vector3 Scatter(Vector3 direction, float halfAngle)
    {
        if (halfAngle <= 0.001f)
            return direction;
        Vector2 offset = Random.insideUnitCircle * Mathf.Tan(halfAngle * Mathf.Deg2Rad);
        Quaternion basis = Quaternion.LookRotation(direction);
        return (direction + basis * new Vector3(offset.x, offset.y, 0f)).normalized;
    }

    private void KickSlide()
    {
        if (!slideCached)
            CacheSlide();
        slideKick = 1f;
    }

    private void CacheSlide()
    {
        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].name != "SLIDE")
                continue;
            slide = bones[i];
            slideRest = slide.localPosition;
            slideCached = true;
            return;
        }
    }

    private void EaseSlide()
    {
        if (slide == null || slide.parent == null)
            return;

        slideKick = Mathf.Lerp(slideKick, 0f, 1f - Mathf.Exp(-18f * Time.deltaTime));
        if (slideKick < 0.001f)
        {
            slideKick = 0f;
            slide.localPosition = slideRest;
            return;
        }

        Vector3 barrel = transform.forward;
        if (held != null && held.TryGetMuzzle(out _, out Vector3 muzzleForward))
            barrel = muzzleForward;
        Vector3 restWorld = slide.parent.TransformPoint(slideRest);
        Vector3 kicked = restWorld - barrel.normalized * (0.018f * slideKick);
        slide.localPosition = slide.parent.InverseTransformPoint(kicked);
    }

    private void SpawnTracer(Vector3 from, Vector3 to)
    {
        GameObject tracer = new GameObject("ShotTracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = 0.018f;
        line.endWidth = 0.008f;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = BeamMaterial(new Color(1f, 0.92f, 0.7f, 1f));
        line.startColor = new Color(1f, 0.95f, 0.75f, 1f);
        line.endColor = new Color(1f, 0.75f, 0.35f, 0.15f);
        Destroy(tracer, 0.07f);
    }

    private InputAction FindAttack()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
            return null;
        return playerInput.actions.FindAction("Attack", false);
    }

    private static Material beamMaterial;

    private static Material BeamMaterial(Color tint)
    {
        if (beamMaterial != null)
            return beamMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        beamMaterial = new Material(shader);
        if (beamMaterial.HasProperty("_BaseColor"))
            beamMaterial.SetColor("_BaseColor", tint);
        if (beamMaterial.HasProperty("_Surface"))
            beamMaterial.SetFloat("_Surface", 1f);
        if (beamMaterial.HasProperty("_Blend"))
            beamMaterial.SetFloat("_Blend", 2f);
        beamMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        beamMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        beamMaterial.SetInt("_DstBlend", (int)BlendMode.One);
        beamMaterial.SetInt("_ZWrite", 0);
        beamMaterial.renderQueue = (int)RenderQueue.Transparent;
        return beamMaterial;
    }
}
