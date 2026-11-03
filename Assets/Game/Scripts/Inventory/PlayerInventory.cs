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

public enum InventoryArea
{
    Hotbar,
    Bag,
    Craft
}

public enum InventoryMoveResult
{
    None,
    Moved,
    Swapped,
    Crafted,
    Rejected
}

public class PlayerInventory : MonoBehaviour
{
    public const int MaxItems = 4;
    public const int BagSlots = 5;
    public const string TorchItemId = "torch";
    public const string SearchlightItemId = "searchlight";
    public const string PistolItemId = "pistol";
    public const string WoodStickItemId = "wood_stick";
    public const string FabricItemId = "fabric";

    [SerializeField] private int defaultMaxSlots = 4;
    [SerializeField] private int activeSlot;
    [SerializeField] private InventorySlotState[] slots;
    [SerializeField] private InventorySlotState[] bag;
    [SerializeField] private InventorySlotState craft;

    private float nextScrollTime;

    public event Action Changed;
    public event Action<string> ItemAdded;
    public event Action<string> AddFailed;
    public event Action<string> Crafted;

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

    public InventorySlotState GetSlot(InventoryArea area, int index)
    {
        switch (area)
        {
            case InventoryArea.Hotbar:
                return GetSlot(index);
            case InventoryArea.Bag:
                return bag == null || index < 0 || index >= bag.Length ? null : bag[index];
            case InventoryArea.Craft:
                return index == 0 ? craft : null;
            default:
                return null;
        }
    }

    private void Reset()
    {
        ApplyDefaultState();
    }

    private void Awake()
    {
        if (slots == null || slots.Length == 0)
            ApplyDefaultState();
        EnsureBag();

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

        // The torch is crafted from a wood stick and fabric, so slot 1 starts empty.
        if (count > 1)
        {
            slots[1].occupied = true;
            slots[1].item = SearchlightItemId;
            slots[1].preview = true;
        }
        if (count > 2)
        {
            slots[2].occupied = true;
            slots[2].item = PistolItemId;
            slots[2].preview = true;
        }
        activeSlot = 0;
        bag = null;
        craft = null;
        EnsureBag();
    }

    private void EnsureBag()
    {
        if (bag == null || bag.Length != BagSlots)
        {
            InventorySlotState[] old = bag;
            bag = new InventorySlotState[BagSlots];
            for (int i = 0; i < BagSlots; i++)
                bag[i] = old != null && i < old.Length && old[i] != null ? old[i] : new InventorySlotState();
        }

        for (int i = 0; i < bag.Length; i++)
            bag[i].slotIndex = i;

        if (craft == null)
            craft = new InventorySlotState();
    }

    // New finds go to the bag. Gear can fall back to an empty hotbar slot.
    public bool TryAdd(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;

        EnsureBag();
        InventorySlotState target = FirstEmpty(bag);
        if (target == null && !ItemCatalog.IsBagOnly(itemId))
            target = FirstEmpty(slots);

        if (target == null)
        {
            Debug.Log("[Inventory] No room for " + itemId + ".");
            AddFailed?.Invoke(itemId);
            return false;
        }

        Put(target, itemId);
        Debug.Log("[Inventory] Added " + itemId + ".");
        Changed?.Invoke();
        ItemAdded?.Invoke(itemId);
        return true;
    }

    // Back into a given hotbar slot when it is still free, otherwise the usual add.
    public bool TryAddToSlot(string itemId, int slotIndex)
    {
        InventorySlotState slot = GetSlot(slotIndex);
        if (string.IsNullOrEmpty(itemId) || slot == null || slot.occupied || !CanPlace(itemId, InventoryArea.Hotbar))
            return TryAdd(itemId);

        Put(slot, itemId);
        Debug.Log("[Inventory] Returned " + itemId + " to slot " + (slotIndex + 1) + ".");
        Changed?.Invoke();
        ItemAdded?.Invoke(itemId);
        return true;
    }

    // Removes the selected item, e.g. when it is thrown.
    public bool TakeActive()
    {
        InventorySlotState slot = GetSlot(activeSlot);
        if (slot == null || !slot.occupied)
            return false;

        Clear(slot);
        Changed?.Invoke();
        return true;
    }

    public bool CanPlace(string itemId, InventoryArea area)
    {
        return area != InventoryArea.Hotbar || !ItemCatalog.IsBagOnly(itemId);
    }

    // Drag and drop. Dropping on an item swaps them, except in the craft slot,
    // where two items that form a recipe turn into the result.
    public InventoryMoveResult CheckMove(InventoryArea fromArea, int fromIndex, InventoryArea toArea, int toIndex)
    {
        InventorySlotState from = GetSlot(fromArea, fromIndex);
        InventorySlotState to = GetSlot(toArea, toIndex);
        if (from == null || to == null || from == to || !from.occupied)
            return InventoryMoveResult.None;

        string moving = from.item;
        string resting = to.occupied ? to.item : null;
        if (!CanPlace(moving, toArea))
            return InventoryMoveResult.Rejected;

        if (toArea == InventoryArea.Craft && resting != null)
            return ItemCatalog.TryCombine(moving, resting, out _) ? InventoryMoveResult.Crafted : InventoryMoveResult.Rejected;

        if (resting != null && !CanPlace(resting, fromArea))
            return InventoryMoveResult.Rejected;

        return resting == null ? InventoryMoveResult.Moved : InventoryMoveResult.Swapped;
    }

    public InventoryMoveResult Move(InventoryArea fromArea, int fromIndex, InventoryArea toArea, int toIndex)
    {
        InventoryMoveResult result = CheckMove(fromArea, fromIndex, toArea, toIndex);
        InventorySlotState from = GetSlot(fromArea, fromIndex);
        InventorySlotState to = GetSlot(toArea, toIndex);
        string moving = from != null ? from.item : null;
        string resting = to != null && to.occupied ? to.item : null;

        switch (result)
        {
            case InventoryMoveResult.Crafted:
                ItemCatalog.TryCombine(moving, resting, out string crafted);
                Clear(from);
                Put(to, crafted);
                Debug.Log("[Inventory] Crafted " + crafted + " from " + moving + " and " + resting + ".");
                Changed?.Invoke();
                Crafted?.Invoke(crafted);
                break;
            case InventoryMoveResult.Moved:
                Clear(from);
                Put(to, moving);
                Changed?.Invoke();
                break;
            case InventoryMoveResult.Swapped:
                Put(from, resting);
                Put(to, moving);
                Changed?.Invoke();
                break;
        }

        return result;
    }

    private static InventorySlotState FirstEmpty(InventorySlotState[] list)
    {
        if (list == null)
            return null;

        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && !list[i].occupied)
                return list[i];
        }

        return null;
    }

    private static void Put(InventorySlotState slot, string itemId)
    {
        slot.occupied = true;
        slot.item = itemId;
        slot.preview = true;
    }

    private static void Clear(InventorySlotState slot)
    {
        slot.occupied = false;
        slot.item = null;
        slot.preview = false;
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
