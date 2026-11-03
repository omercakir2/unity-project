using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A loose item in the world. Near the player it shows a prompt; Interact puts it in the backpack.
/// </summary>
[DisallowMultipleComponent]
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private string itemId = PlayerInventory.WoodStickItemId;
    [SerializeField] private float range = 2f;
    [SerializeField] private float promptHeight = 0.55f;

    private PlayerInventory inventory;
    private PlayerController player;
    private InputAction interactAction;
    private Transform promptRoot;
    private bool inRange;

    private void Start()
    {
        BuildPrompt();
    }

    private void OnDestroy()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteract;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (inventory == null && !FindPlayer())
            return;

        inRange = CanReach();
        ShowPrompt(inRange);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (inRange && CanReach())
            TakeItem();
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
        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        promptRoot.rotation = Quaternion.LookRotation(toCamera, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
    }

    private bool FindPlayer()
    {
        player = FindAnyObjectByType<PlayerController>();
        if (player == null)
            return false;

        inventory = player.GetComponent<PlayerInventory>();
        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
            interactAction = playerInput.actions.FindAction("Interact", false);
        if (interactAction != null)
            interactAction.performed += OnInteract;
        return inventory != null;
    }

    private bool CanReach()
    {
        if (player == null || player.IsInspecting || player.IsTraversing)
            return false;

        Vector3 delta = transform.position - player.transform.position;
        if (Mathf.Abs(delta.y) > 1.75f)
            return false;

        delta.y = 0f;
        return delta.sqrMagnitude <= range * range;
    }

    private void TakeItem()
    {
        if (!inventory.TryAdd(itemId))
            return;

        Destroy(gameObject);
    }

    private void ShowPrompt(bool visible)
    {
        if (promptRoot != null && promptRoot.gameObject.activeSelf != visible)
            promptRoot.gameObject.SetActive(visible);
    }

    private void BuildPrompt()
    {
        if (promptRoot != null)
            return;

        GameObject canvasObject = new GameObject("PickupPrompt");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(420f, 72f);
        // Undo the item's own scale so the prompt reads the same on every pickup.
        Vector3 parentScale = transform.lossyScale;
        float inverse = Mathf.Max(0.0001f, Mathf.Max(parentScale.x, Mathf.Max(parentScale.y, parentScale.z)));
        canvasRect.localScale = Vector3.one * (0.0024f / inverse);

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
        label.text = "'E' to pick up " + (ItemCatalog.DisplayName(itemId) ?? itemId);
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
        promptRoot.rotation = Quaternion.identity;
        canvasObject.SetActive(false);
    }
}
