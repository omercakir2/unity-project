using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the crawl, stand, scream, and run sequence, then walks straight at the player.
/// A pistol hit plays death. Touching the player ends the run.
/// </summary>
public class ZombieController : MonoBehaviour
{
    [SerializeField] private float runSpeed = 6.2f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float gravity = -40f;
    [SerializeField] private float groundStick = 0.4f;

    [Header("Standing Capsule")]
    [SerializeField] private float standingHeight = 1.7f;
    [SerializeField] private float standingRadius = 0.35f;
    [SerializeField] private Vector3 standingCenter = new Vector3(0f, 0.85f, 0f);

    [Header("Crawl Capsule")]
    [SerializeField] private float crawlHeight = 0.6f;
    [SerializeField] private float crawlRadius = 0.4f;
    [SerializeField] private Vector3 crawlCenter = new Vector3(0f, 0.3f, 0f);

    private Animator animator;
    private CapsuleCollider body;
    private SkinnedMeshRenderer[] skins;
    private Mesh bakedSole;
    private readonly List<Vector3> bakedVertices = new List<Vector3>(8192);
    private Transform player;
    private CharacterController playerBody;
    private PlayerVitals playerVitals;
    private bool dead;
    private bool deathHeld;
    private bool lowBody = true;
    private bool chasing;
    private float verticalVelocity;

    private readonly int dieHash = Animator.StringToHash("Die");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        body = GetComponent<CapsuleCollider>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (GetComponent<ZombieAudio>() == null)
            gameObject.AddComponent<ZombieAudio>();
        if (GetComponent<ZombieCameraScare>() == null)
            gameObject.AddComponent<ZombieCameraScare>();

        skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        bakedSole = new Mesh();
    }

    private void OnDestroy()
    {
        if (bakedSole != null)
            Destroy(bakedSole);
    }

    private void Start()
    {
        FindPlayer();
        ApplyBodyShape();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (player == null)
            FindPlayer();

        if (dead)
        {
            ApplyGravity();
            HoldDeathPose();
            return;
        }

        FacePlayer();
        UpdateBodyShape();
        if (IsChasing())
            Chase();
        ApplyGravity();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || dead)
            return;

        TryCatchPlayer();
    }

    public void Kill()
    {
        if (dead)
            return;

        dead = true;
        if (body != null)
            body.enabled = false;
        if (animator != null)
            animator.SetTrigger(dieHash);
        GetComponent<ZombieAudio>()?.Silence();
        Debug.Log("[Zombie] Shot. Playing death.");
    }

    private void FindPlayer()
    {
        GameObject marked = GameObject.FindWithTag("Player");
        if (marked == null)
        {
            PlayerController found = FindAnyObjectByType<PlayerController>();
            marked = found != null ? found.gameObject : null;
        }

        if (marked == null)
            return;

        player = marked.transform;
        playerBody = marked.GetComponent<CharacterController>();
        playerVitals = marked.GetComponent<PlayerVitals>();
    }

    private void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return;

        Quaternion look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void Chase()
    {
        if (player == null)
            return;

        if (!chasing)
        {
            chasing = true;
            Debug.Log("[Zombie] Running toward the player.");
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return;

        transform.position += toPlayer.normalized * (runSpeed * Time.deltaTime);
    }

    private bool IsChasing()
    {
        if (animator == null)
            return false;

        if (animator.GetCurrentAnimatorStateInfo(0).IsName("Running"))
            return true;

        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Running");
    }

    private void UpdateBodyShape()
    {
        if (animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Stand Up") || state.IsName("Scream") || state.IsName("Running") || state.IsName("Death"))
            lowBody = false;
        else if (state.IsName("Crawl"))
            lowBody = true;

        ApplyBodyShape();
    }

    private void ApplyBodyShape()
    {
        if (body == null)
            return;

        if (lowBody)
        {
            body.height = crawlHeight;
            body.radius = crawlRadius;
            body.center = crawlCenter;
            return;
        }

        body.height = standingHeight;
        body.radius = standingRadius;
        body.center = standingCenter;
    }

    private void ApplyGravity()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        bool hasGround = TryGetGround(out float groundY);
        float lowest = LowestPoint();
        if (hasGround && lowest - groundY <= groundStick && verticalVelocity <= 0f)
        {
            // A couple of centimetres still reads as the feet floating.
            if (Mathf.Abs(lowest - groundY) > 0.004f)
                transform.position += Vector3.up * (groundY - lowest);
            verticalVelocity = 0f;
            return;
        }

        verticalVelocity += gravity * dt;
        float drop = verticalVelocity * dt;
        if (hasGround && lowest + drop < groundY)
        {
            transform.position += Vector3.up * (groundY - lowest);
            verticalVelocity = 0f;
            return;
        }

        transform.position += Vector3.up * drop;
    }

    private bool TryGetGround(out float groundY)
    {
        groundY = 0f;
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 250f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null)
                continue;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;
            if (player != null && (hitTransform == player || hitTransform.IsChildOf(player)))
                continue;

            groundY = hits[i].point.y;
            return true;
        }

        return false;
    }

    private float LowestPoint()
    {
        // The skinned bounds hang below the sole. Snapping those keeps the feet in the air,
        // and moving the model cannot close that gap because the bounds move with it.
        float lowest = transform.position.y;
        bool found = false;
        if (skins != null && bakedSole != null)
        {
            for (int i = 0; i < skins.Length; i++)
            {
                SkinnedMeshRenderer skin = skins[i];
                if (skin == null || skin.sharedMesh == null)
                    continue;

                bakedSole.Clear();
                skin.BakeMesh(bakedSole);
                bakedSole.GetVertices(bakedVertices);
                Transform space = skin.transform;
                for (int v = 0; v < bakedVertices.Count; v++)
                {
                    float y = space.TransformPoint(bakedVertices[v]).y;
                    if (!found || y < lowest)
                    {
                        lowest = y;
                        found = true;
                    }
                }
            }
        }

        if (found)
            return lowest;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            lowest = Mathf.Min(lowest, renderers[i].bounds.min.y);
        }

        return lowest;
    }

    private void TryCatchPlayer()
    {
        if (body == null || !body.enabled || playerBody == null || playerVitals == null || playerVitals.IsDead)
            return;

        if (!CapsulesOverlap())
            return;

        Debug.Log("[Zombie] Caught the player.");
        playerVitals.ApplyDamage(playerVitals.MaxHealth);
    }

    private bool CapsulesOverlap()
    {
        if (Physics.ComputePenetration(
                body, body.transform.position, body.transform.rotation,
                playerBody, playerBody.transform.position, playerBody.transform.rotation,
                out _, out float distance))
            return distance > 0f;

        Vector3 center = body.transform.TransformPoint(body.center);
        float heightScale = Mathf.Abs(body.transform.lossyScale.y);
        float radiusScale = Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z));
        float height = Mathf.Max(body.height * heightScale, body.radius * 2f * radiusScale);
        float radius = body.radius * radiusScale;
        Vector3 axis = body.transform.up * Mathf.Max(0f, height * 0.5f - radius);
        Collider[] hits = Physics.OverlapCapsule(center - axis, center + axis, radius * 0.92f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null || hits[i] == body)
                continue;
            if (hits[i].GetComponentInParent<PlayerVitals>() != null)
                return true;
        }

        return false;
    }

    private void HoldDeathPose()
    {
        if (deathHeld || animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName("Death") || animator.IsInTransition(0))
            return;
        if (state.normalizedTime < 0.98f)
            return;

        animator.speed = 0f;
        deathHeld = true;
    }
}
