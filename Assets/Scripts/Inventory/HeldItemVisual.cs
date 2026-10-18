using UnityEngine;

public class HeldItemVisual : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameObject torchPrefab;
    [SerializeField] private Vector3 torchLocalPosition = new Vector3(0.05f, 0.12f, 0f);
    [SerializeField] private Vector3 torchLocalEuler = new Vector3(-90f, 0f, 90f);

    private GameObject heldTorch;
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
        if (!showTorch)
        {
            if (heldTorch != null)
                heldTorch.SetActive(false);
            return;
        }

        EnsureTorch();
        if (heldTorch != null)
            heldTorch.SetActive(true);
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
        heldTorch.transform.localPosition = torchLocalPosition;
        heldTorch.transform.localRotation = Quaternion.Euler(torchLocalEuler);

        Collider[] colliders = heldTorch.GetComponentsInChildren<Collider>(true);
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
