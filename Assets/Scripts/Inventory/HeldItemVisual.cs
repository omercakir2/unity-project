using UnityEngine;

public class HeldItemVisual : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameObject torchPrefab;
    [SerializeField] private GameObject searchlightPrefab;
    [SerializeField] private Vector3 torchLocalPosition = new Vector3(0.01f, 0.27f, 0.05f);
    [SerializeField] private Vector3 torchLocalEuler = new Vector3(344.6f, 82.2f, 0f);
    [SerializeField] private Vector3 searchlightLocalPosition = new Vector3(0.021f, 0.118f, 0.002f);
    [SerializeField] private Vector3 searchlightLocalEuler = new Vector3(280.3f, 85f, 177.6f);

    private const string HoldLayerName = "Hold";
    private const string HoldStateName = "Torch Hold";
    private const float HoldIdleNormalizedTime = 0.2f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HoldRateHash = Animator.StringToHash("HoldRate");

    private GameObject heldTorch;
    private GameObject heldSearchlight;
    private Transform carryHand;
    private Animator animator;
    private bool holdPoseParked;

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

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        ApplyHoldLayer();
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

        ApplyHoldLayer();
    }

    private void ApplyHoldLayer()
    {
        Animator anim = FindAnimator();
        if (anim == null || anim.runtimeAnimatorController == null)
            return;

        int layer = anim.GetLayerIndex(HoldLayerName);
        if (layer < 0)
            return;

        bool holding = IsHoldingLight();
        anim.SetLayerWeight(layer, holding ? 1f : 0f);

        float speed = anim.GetFloat(SpeedHash);
        bool idle = speed < 0.05f;
        float holdRate = 0f;
        if (holding && !idle)
            holdRate = speed < 0.75f ? 1f : 1.35f;

        anim.SetFloat(HoldRateHash, holdRate);

        if (!holding || !idle)
        {
            holdPoseParked = false;
            return;
        }

        if (holdPoseParked)
            return;

        anim.Play(HoldStateName, layer, HoldIdleNormalizedTime);
        anim.Update(0f);
        holdPoseParked = true;
    }

    private bool IsHoldingLight()
    {
        if (inventory == null)
            return false;

        string item = inventory.ActiveItemId;
        return item == PlayerInventory.TorchItemId || item == PlayerInventory.SearchlightItemId;
    }

    private Animator FindAnimator()
    {
        if (animator != null)
            return animator;

        animator = GetComponentInChildren<Animator>(true);
        return animator;
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

        Transform hand = FindCarryHand();
        if (hand == null)
        {
            Debug.LogError("[Inventory] Carry hand bone was not found.");
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

        Transform hand = FindCarryHand();
        if (hand == null)
        {
            Debug.LogError("[Inventory] Carry hand bone was not found.");
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

    public void DetachHeldItems()
    {
        Park(heldTorch);
        Park(heldSearchlight);
        carryHand = null;
        animator = null;
        holdPoseParked = false;
    }

    public void RebindHand()
    {
        carryHand = null;
        Transform hand = FindCarryHand();
        if (hand == null)
        {
            if (heldTorch != null || heldSearchlight != null)
                Debug.LogError("[Inventory] Carry hand bone was not found.");
            return;
        }

        Attach(heldTorch, hand, torchLocalPosition, torchLocalEuler);
        Attach(heldSearchlight, hand, searchlightLocalPosition, searchlightLocalEuler);
    }

    private void Park(GameObject item)
    {
        if (item == null)
            return;

        item.transform.SetParent(transform, false);
    }

    private void Attach(GameObject item, Transform hand, Vector3 localPosition, Vector3 localEuler)
    {
        if (item == null)
            return;

        bool active = item.activeSelf;
        item.transform.SetParent(hand, false);
        ApplyGrip(item.transform, localPosition, localEuler);
        if (item.activeSelf != active)
            item.SetActive(active);
    }

    private Transform FindCarryHand()
    {
        if (carryHand != null)
            return carryHand;

        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            // The torch walk carries with the left hand. The right arm swings free.
            if (bones[i].name.EndsWith("LeftHand"))
            {
                carryHand = bones[i];
                return carryHand;
            }
        }

        return null;
    }
}
