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

    private HeldItemVisual held;
    private PlayerInventory inventory;
    private PlayerController player;
    private InputAction attack;
    private float nextShotTime;
    private float slideKick;
    private Transform slide;
    private Vector3 slideRest;
    private bool slideCached;
    private const string ShotResourcePath = "Audio/FreeWeaponSFX_v1.1/Handgun/Gunshots/sfx_wpn_hgn_gunshot_03";
    private const string TailResourcePath = "Audio/FreeWeaponSFX_v1.1/Handgun/Gunshots/sfx_wpn_hgn_tail_01";

    private AudioSource audioSource;
    private AmbienceSettings settings;
    private AudioClip shotClip;
    private AudioClip tailClip;

    private void Awake()
    {
        held = GetComponent<HeldItemVisual>();
        inventory = GetComponent<PlayerInventory>();
        player = GetComponent<PlayerController>();
        settings = AmbienceSettings.LoadOrDefault();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        audioSource.dopplerLevel = 0f;
        shotClip = Resources.Load<AudioClip>(ShotResourcePath);
        tailClip = Resources.Load<AudioClip>(TailResourcePath);
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
        if (Time.time < nextShotTime)
            return false;

        nextShotTime = Time.time + cooldown;

        Vector3 muzzle = transform.position + Vector3.up * 1.4f;
        Vector3 shotDirection = transform.forward;
        if (!held.TryGetMuzzle(out muzzle, out shotDirection) || shotDirection.sqrMagnitude < 0.0001f)
            shotDirection = transform.forward;
        shotDirection.Normalize();

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
                zombie.Kill();
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
