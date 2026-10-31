using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Near a dead body tagged inspectable-kneel, shows a prompt and plays kneel, a short hold, then stand.
/// </summary>
public class CorpseInspect : MonoBehaviour
{
    public const string InspectTag = "inspectable-kneel";
    private const string KneelState = "Kneel";
    private const string StandState = "Kneel Stand";

    [SerializeField] private float range = 2.5f;
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private float promptHeight = 1.05f;

    private PlayerController player;
    private PlayerVitals vitals;
    private PlayerInput playerInput;
    private InputAction interactAction;
    private Transform promptRoot;
    private Transform current;
    private bool inspecting;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        vitals = GetComponent<PlayerVitals>();
        playerInput = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        BindInteract();
        BuildPrompt();
    }

    private void OnEnable()
    {
        BindInteract();
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteract;
        interactAction = null;
        if (inspecting)
            player?.SetInspectLock(false);
        inspecting = false;
    }

    private void BindInteract()
    {
        if (interactAction != null || playerInput == null || playerInput.actions == null)
            return;

        interactAction = playerInput.actions.FindAction("Interact", false);
        if (interactAction != null)
            interactAction.performed += OnInteract;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        BindInteract();
        if (inspecting)
        {
            ShowPrompt(null);
            return;
        }

        current = FindNearest();
        ShowPrompt(current);
    }

    private void LateUpdate()
    {
        if (promptRoot == null || current == null || !promptRoot.gameObject.activeSelf)
            return;

        promptRoot.position = current.position + Vector3.up * promptHeight;
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 toCamera = cam.transform.position - promptRoot.position;
        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        promptRoot.rotation = Quaternion.LookRotation(toCamera, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        TryBegin();
    }

    public void TryBegin()
    {
        if (inspecting || !isActiveAndEnabled)
            return;
        if (player != null && (player.IsTraversing || player.IsInspecting))
            return;
        if (vitals != null && vitals.IsDead)
            return;

        Transform target = FindNearest();
        if (target == null)
            return;

        current = target;
        StartCoroutine(Inspect(target));
    }

    private IEnumerator Inspect(Transform target)
    {
        inspecting = true;
        ShowPrompt(null);
        player?.SetInspectLock(true);

        float turnTime = 0.16f;
        while (turnTime > 0f && target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                float t = 1f - Mathf.Pow(0.001f, Time.deltaTime / 0.1f);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, t);
            }

            turnTime -= Time.deltaTime;
            yield return null;
        }

        Animator anim = player != null ? player.GetComponentInChildren<Animator>() : GetComponentInChildren<Animator>();
        if (anim == null || anim.runtimeAnimatorController == null || !anim.HasState(0, Animator.StringToHash(KneelState)))
        {
            Debug.LogError("[Inspect] Kneel state is missing on the player animator.");
            Finish();
            yield break;
        }

        anim.speed = 1f;
        anim.ResetTrigger("Jump");
        anim.ResetTrigger("Climb");
        anim.ResetTrigger("JumpDown");
        anim.CrossFadeInFixedTime(KneelState, 0.12f, 0, 0f);
        yield return WaitForState(anim, KneelState, 0.98f, 8f);

        float hold = holdSeconds;
        while (hold > 0f)
        {
            hold -= Time.deltaTime;
            yield return null;
        }

        if (!anim.HasState(0, Animator.StringToHash(StandState)))
        {
            Debug.LogError("[Inspect] Kneel Stand state is missing on the player animator.");
            Finish();
            yield break;
        }

        anim.speed = 1f;
        anim.CrossFadeInFixedTime(StandState, 0.05f, 0, 0f);
        yield return WaitForState(anim, StandState, 0.98f, 8f);

        bool pistol = anim.GetBool("Pistol");
        anim.speed = 1f;
        anim.CrossFadeInFixedTime(pistol ? "Pistol Locomotion" : "Blend Tree", 0.22f, 0, 0f);
        Finish();
    }

    private void Finish()
    {
        inspecting = false;
        player?.SetInspectLock(false);
    }

    private static IEnumerator WaitForState(Animator anim, string stateName, float normalizedTime, float timeout)
    {
        float elapsed = 0f;
        while (elapsed < timeout && anim != null)
        {
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
            if (!anim.IsInTransition(0) && info.IsName(stateName) && info.normalizedTime >= normalizedTime)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private Transform FindNearest()
    {
        GameObject[] marked;
        try
        {
            marked = GameObject.FindGameObjectsWithTag(InspectTag);
        }
        catch (UnityException)
        {
            return null;
        }

        Transform best = null;
        float bestSqr = range * range;
        Vector3 origin = transform.position;
        for (int i = 0; i < marked.Length; i++)
        {
            if (marked[i] == null)
                continue;

            Vector3 delta = marked[i].transform.position - origin;
            if (Mathf.Abs(delta.y) > 1.75f)
                continue;

            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr > bestSqr)
                continue;

            bestSqr = sqr;
            best = marked[i].transform;
        }

        return best;
    }

    private void ShowPrompt(Transform target)
    {
        if (promptRoot == null)
            return;

        bool visible = target != null;
        if (promptRoot.gameObject.activeSelf != visible)
            promptRoot.gameObject.SetActive(visible);
    }

    private void BuildPrompt()
    {
        if (promptRoot != null)
            return;

        GameObject canvasObject = new GameObject("InspectPrompt");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(360f, 72f);
        canvasRect.localScale = Vector3.one * 0.0024f;

        GameObject plateObject = new GameObject("Plate");
        plateObject.transform.SetParent(canvasObject.transform, false);
        UnityEngine.UI.Image plate = plateObject.AddComponent<UnityEngine.UI.Image>();
        plate.color = new Color(0.1f, 0.08f, 0.07f, 0.9f);
        plate.raycastTarget = false;
        RectTransform plateRect = plateObject.GetComponent<RectTransform>();
        plateRect.anchorMin = Vector2.zero;
        plateRect.anchorMax = Vector2.one;
        plateRect.offsetMin = Vector2.zero;
        plateRect.offsetMax = Vector2.zero;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(plateObject.transform, false);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "'E' to inspect";
        label.fontSize = 32f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.91f, 0.8f, 1f);
        label.raycastTarget = false;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null)
            label.font = font;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 6f);
        labelRect.offsetMax = new Vector2(-12f, -6f);

        promptRoot = canvasObject.transform;
        canvasObject.SetActive(false);
    }
}
