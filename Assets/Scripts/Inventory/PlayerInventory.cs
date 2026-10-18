using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class InventorySlotState
{
    public int slotIndex;
    public bool occupied;
    public string item;
    public bool preview;
}

public class PlayerInventory : MonoBehaviour
{
    public const int MaxItems = 4;
    public const string TorchItemId = "torch";

    [SerializeField] private int defaultMaxSlots = 4;
    [SerializeField] private int activeSlot;
    [SerializeField] private InventorySlotState[] slots;

    private float nextScrollTime;

    public event Action Changed;

    public int SlotCount => slots == null ? 0 : slots.Length;
    public int ActiveSlot => activeSlot;

    public string ActiveItemId
    {
        get
        {
            InventorySlotState slot = GetSlot(activeSlot);
            if (slot == null || !slot.occupied || string.IsNullOrEmpty(slot.item))
                return null;
            return slot.item;
        }
    }

    public InventorySlotState GetSlot(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
            return null;
        return slots[index];
    }

    private void Reset()
    {
        ApplyDefaultState();
    }

    private void Awake()
    {
        if (slots == null || slots.Length == 0)
            ApplyDefaultState();

        if (SlotCount > 0)
            activeSlot = Mathf.Clamp(activeSlot, 0, SlotCount - 1);
    }

    public void ApplyDefaultState()
    {
        int count = defaultMaxSlots <= 0 ? MaxItems : defaultMaxSlots;
        count = Mathf.Clamp(count, 1, MaxItems);
        slots = new InventorySlotState[count];
        for (int i = 0; i < count; i++)
        {
            slots[i] = new InventorySlotState
            {
                slotIndex = i,
                occupied = false,
                item = null,
                preview = false
            };
        }

        slots[0].occupied = true;
        slots[0].item = TorchItemId;
        slots[0].preview = true;
        activeSlot = 0;
    }

    public void SelectSlot(int index)
    {
        if (SlotCount == 0)
            return;

        index = Mathf.Clamp(index, 0, SlotCount - 1);
        if (activeSlot == index)
            return;

        activeSlot = index;
        Notify();
    }

    public void Cycle(int direction)
    {
        if (direction == 0 || SlotCount == 0)
            return;

        int step = direction > 0 ? 1 : -1;
        activeSlot = (activeSlot + step + SlotCount) % SlotCount;
        Notify();
    }

    public void OnHotbar1(InputAction.CallbackContext context)
    {
        if (context.performed)
            SelectSlot(0);
    }

    public void OnHotbar2(InputAction.CallbackContext context)
    {
        if (context.performed)
            SelectSlot(1);
    }

    public void OnHotbar3(InputAction.CallbackContext context)
    {
        if (context.performed)
            SelectSlot(2);
    }

    public void OnHotbar4(InputAction.CallbackContext context)
    {
        if (context.performed)
            SelectSlot(3);
    }

    public void OnHotbarScroll(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        float value = context.ReadValue<float>();
        if (Mathf.Abs(value) < 0.01f)
            return;
        if (Time.unscaledTime < nextScrollTime)
            return;

        nextScrollTime = Time.unscaledTime + 0.12f;
        Cycle(value > 0f ? 1 : -1);
    }

    private void Notify()
    {
        string item = ActiveItemId ?? "empty";
        Debug.Log("[Inventory] Active slot " + (activeSlot + 1) + " item " + item);
        Changed?.Invoke();
    }
}
