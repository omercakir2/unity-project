using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attack with the light ball selected plays the throw clip on the "Throw" layer. The ball moves to the
/// right hand for the wind-up and leaves it at the release frame toward the crosshair. It keeps glowing in
/// flight and where it lands, and can be picked up again.
/// </summary>
public class LightBallThrow : MonoBehaviour
{
    private const string LayerName = "Throw";
    private const string StateName = "Throw";

    [SerializeField] private float throwSpeed = 13f;
    [SerializeField] private float upwardBoost = 1.5f;
    [SerializeField] private float aimRange = 60f;
    // Times in the clip, in seconds. The clip idles before the wind-up, so it starts part way in.
    [SerializeField] private float clipStart = 0.8f;
    [SerializeField] private float releaseTime = 1.62f;
    [SerializeField] private float blendOutTime = 2.6f;
    [SerializeField] private float blendDuration = 0.18f;

    private HeldItemVisual held;
    private PlayerInventory inventory;
    private PlayerController player;
    private InputAction attack;
    private Animator animator;
    private int layer = -1;
    private bool throwing;
    private bool released;
    private float clipTime;
    private int thrownFromSlot = -1;
    private GameObject ball;

    public bool IsThrowing => throwing;

    private void Awake()
    {
        held = GetComponent<HeldItemVisual>();
        inventory = GetComponent<PlayerInventory>();
        player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (throwing)
        {
            Advance();
            return;
        }

        if (AttackPressed() && CanThrow())
            Begin();
    }

    private bool CanThrow()
    {
        if (held == null || inventory == null)
            return false;
        if (inventory.ActiveItemId != PlayerInventory.SearchlightItemId)
            return false;
        if (player != null && (player.IsInspecting || player.IsTraversing))
            return false;
        if (held.SearchPoseWeight < 0.55f)
            return false;
        return FindLayer();
    }

    private void Begin()
    {
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        GameObject prefab = held.SearchlightPrefab;
        if (hand == null || prefab == null)
            return;

        thrownFromSlot = inventory.ActiveSlot;
        if (!inventory.TakeActive())
            return;

        throwing = true;
        released = false;
        clipTime = clipStart;
        held.HideSearchlight = true;

        ball = Instantiate(prefab, hand);
        ball.name = "ThrownLightBall";
        WeaponGrip grip = ball.GetComponent<WeaponGrip>();
        if (grip != null)
            grip.AlignToParentOrigin();
        Collider[] colliders = ball.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        AnimationClip clip = ThrowClip();
        float length = clip != null ? clip.length : 5.5f;
        animator.Play(StateName, layer, Mathf.Clamp01(clipStart / length));
        animator.SetLayerWeight(layer, 0f);
    }

    private void Advance()
    {
        clipTime += Time.deltaTime;

        float weight;
        if (clipTime < clipStart + blendDuration)
            weight = (clipTime - clipStart) / blendDuration;
        else
            weight = 1f - (clipTime - blendOutTime) / blendDuration;
        if (animator != null && layer >= 0)
            animator.SetLayerWeight(layer, Mathf.Clamp01(weight));

        if (!released && clipTime >= releaseTime)
            Release();

        if (clipTime >= blendOutTime + blendDuration)
            Finish();
    }

    private void Release()
    {
        released = true;
        if (ball == null)
            return;

        Transform ballTransform = ball.transform;
        ballTransform.SetParent(null, true);
        Vector3 origin = ballTransform.position;
        Vector3 direction = (AimPoint(origin) - origin).normalized;

        SphereCollider sphere = EnsureCollider(ball);
        CharacterController body = GetComponent<CharacterController>();
        if (body != null && sphere != null)
            Physics.IgnoreCollision(sphere, body);

        Rigidbody rigidbody = ball.AddComponent<Rigidbody>();
        rigidbody.mass = 0.3f;
        rigidbody.linearDamping = 0.05f;
        rigidbody.angularDamping = 1.5f;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.linearVelocity = direction * throwSpeed + Vector3.up * upwardBoost;

        ThrownLightBall thrown = ball.AddComponent<ThrownLightBall>();
        thrown.Init(thrownFromSlot);
        ball = null;
    }

    private void Finish()
    {
        if (!released)
            Release();

        throwing = false;
        if (animator != null && layer >= 0)
            animator.SetLayerWeight(layer, 0f);
        if (held != null)
            held.HideSearchlight = false;
    }

    // The ball itself is the sphere under Lamp; its collider matches the mesh at default radius.
    private static SphereCollider EnsureCollider(GameObject item)
    {
        MeshFilter[] meshes = item.GetComponentsInChildren<MeshFilter>(true);
        GameObject host = meshes.Length > 0 ? meshes[0].gameObject : item;
        SphereCollider sphere = host.GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = host.AddComponent<SphereCollider>();
        sphere.enabled = true;

        PhysicsMaterial material = new PhysicsMaterial("LightBall")
        {
            bounciness = 0.35f,
            dynamicFriction = 0.6f,
            staticFriction = 0.7f,
            bounceCombine = PhysicsMaterialCombine.Average,
            frictionCombine = PhysicsMaterialCombine.Average
        };
        sphere.sharedMaterial = material;
        return sphere;
    }

    private Vector3 AimPoint(Vector3 origin)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return origin + transform.forward * aimRange;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, aimRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hit = hits[i].transform;
            if (hit == null || hit == transform || hit.IsChildOf(transform))
                continue;
            return hits[i].point;
        }

        return ray.origin + ray.direction * aimRange;
    }

    private bool FindLayer()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
            layer = -1;
        }
        if (animator == null || animator.runtimeAnimatorController == null || !animator.isHuman)
            return false;
        if (layer < 0)
            layer = animator.GetLayerIndex(LayerName);
        if (layer < 0)
            Debug.LogWarning("[LightBallThrow] Animator has no \"" + LayerName + "\" layer. Run Tools/Setup Throw Animation.");
        return layer >= 0;
    }

    private AnimationClip ThrowClip()
    {
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == StateName)
                return clips[i];
        }

        return null;
    }

    private bool AttackPressed()
    {
        if (attack == null)
            attack = FindAttack();
        if (attack != null)
            return attack.WasPressedThisFrame();
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private InputAction FindAttack()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
            return null;
        return playerInput.actions.FindAction("Attack", false);
    }
}
