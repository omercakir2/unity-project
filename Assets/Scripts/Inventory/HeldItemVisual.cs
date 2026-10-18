using UnityEngine;

public class HeldItemVisual : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameObject torchPrefab;
    [SerializeField] private GameObject searchlightPrefab;
    [SerializeField] private Vector3 torchLocalPosition = new Vector3(-0.67f, -0.45f, 0.11f);
    [SerializeField] private Vector3 torchLocalEuler = new Vector3(47.51f, 223.53f, 262.7f);
    [SerializeField] private Vector3 searchlightLocalPosition = new Vector3(0.05f, 0.12f, 0f);
    [SerializeField] private Vector3 searchlightLocalEuler = Vector3.zero;

    private GameObject heldTorch;
    private GameObject heldSearchlight;
    private Transform rightHand;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || inventory == null)
            return;

        inventory.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (!Application.isPlaying || inventory == null)
            return;

        bool showTorch = inventory.ActiveItemId == PlayerInventory.TorchItemId;
        bool showSearchlight = inventory.ActiveItemId == PlayerInventory.SearchlightItemId;

        if (showTorch)
            EnsureTorch();
        if (showSearchlight)
            EnsureSearchlight();

        if (heldTorch != null && heldTorch.activeSelf != showTorch)
            heldTorch.SetActive(showTorch);
        if (heldSearchlight != null && heldSearchlight.activeSelf != showSearchlight)
            heldSearchlight.SetActive(showSearchlight);
    }

    private void EnsureTorch()
    {
        if (heldTorch != null)
            return;

        if (torchPrefab == null)
        {
            Debug.LogError("[Inventory] Torch prefab is not assigned.");
            return;
        }

        Transform hand = FindRightHand();
        if (hand == null)
        {
            Debug.LogError("[Inventory] Right hand bone was not found.");
            return;
        }

        heldTorch = Instantiate(torchPrefab, hand);
        heldTorch.name = "HeldTorch";
        ApplyGrip(heldTorch.transform, torchLocalPosition, torchLocalEuler);
        DisableColliders(heldTorch);
    }

    private void EnsureSearchlight()
    {
        if (heldSearchlight != null)
            return;

        if (searchlightPrefab == null)
        {
            Debug.LogError("[Inventory] Searchlight prefab is not assigned.");
            return;
        }

        Transform hand = FindRightHand();
        if (hand == null)
        {
            Debug.LogError("[Inventory] Right hand bone was not found.");
            return;
        }

        heldSearchlight = Instantiate(searchlightPrefab, hand);
        heldSearchlight.name = "HeldSearchlight";
        ApplyGrip(heldSearchlight.transform, searchlightLocalPosition, searchlightLocalEuler);

        SearchlightAim aim = heldSearchlight.GetComponent<SearchlightAim>();
        if (aim != null)
            aim.SetOwner(transform);

        DisableColliders(heldSearchlight);
    }

    private static void ApplyGrip(Transform item, Vector3 localPosition, Vector3 localEuler)
    {
        item.localPosition = localPosition;
        item.localRotation = Quaternion.Euler(localEuler);
    }

    private static void DisableColliders(GameObject item)
    {
        Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private Transform FindRightHand()
    {
        if (rightHand != null)
            return rightHand;

        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].name.EndsWith("RightHand"))
            {
                rightHand = bones[i];
                return rightHand;
            }
        }

        return null;
    }
}
