using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A door to another scene. The target loads in the background from the start, so Interact
/// near the door only has to fade out and swap.
/// </summary>
[DisallowMultipleComponent]
public class SceneDoor : MonoBehaviour
{
    [Tooltip("Scene asset path, e.g. Assets/Game/Scenes/IndustrialYard_Night.unity. Must be in Build Settings.")]
    [SerializeField] private string targetScene;
    [Tooltip("SceneSpawnPoint id in the target scene.")]
    [SerializeField] private string targetSpawnId = "Default";
    [SerializeField] private string promptText = "'E' to open";
    [SerializeField] private bool preload = true;
    [SerializeField] private float range = 2.2f;
    [SerializeField] private float promptHeight = 1.6f;

    private PlayerController player;
    private InputAction interactAction;
    private Transform promptRoot;
    private AsyncOperation preloaded;
    private bool inRange;

    private void Start()
    {
        BuildPrompt();
        if (preload && !string.IsNullOrEmpty(targetScene))
            preloaded = SceneTransition.Preload(targetScene);
    }

    private void OnDestroy()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteract;
    }

    private void Update()
    {
        if (player == null && !FindPlayer())
            return;

        inRange = CanReach();
        if (promptRoot != null && promptRoot.gameObject.activeSelf != inRange)
            promptRoot.gameObject.SetActive(inRange);
    }

    private void LateUpdate()
    {
        if (promptRoot == null || !promptRoot.gameObject.activeSelf)
            return;

        promptRoot.position = transform.position + Vector3.up * promptHeight;
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 toCamera = cam.transform.position - promptRoot.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            promptRoot.rotation = Quaternion.LookRotation(toCamera, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!inRange || !CanReach())
            return;

        if (SceneTransition.Go(preloaded, targetScene, targetSpawnId))
            preloaded = null;
    }

    private bool FindPlayer()
    {
        player = FindAnyObjectByType<PlayerController>();
        if (player == null)
            return false;

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
            interactAction = playerInput.actions.FindAction("Interact", false);
        if (interactAction != null)
            interactAction.performed += OnInteract;
        return true;
    }

    private bool CanReach()
    {
        if (player == null || !player.enabled || player.IsInspecting || SceneTransition.IsBusy || GameplayInput.IsBlocked)
            return false;

        Vector3 delta = transform.position - player.transform.position;
        if (Mathf.Abs(delta.y) > 2f)
            return false;

        delta.y = 0f;
        return delta.sqrMagnitude <= range * range;
    }

    private void BuildPrompt()
    {
        GameObject canvasObject = new GameObject("DoorPrompt");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(420f, 72f);
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
        label.text = promptText;
        label.fontSize = 30f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.91f, 0.8f, 1f);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
