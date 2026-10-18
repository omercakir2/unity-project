using System;
using UnityEngine;

public class InventoryHotbarView : MonoBehaviour
{
    private static readonly Color EmptyBackground = new Color(0.08f, 0.08f, 0.08f, 0.55f);
    private static readonly Color OccupiedBackground = new Color(0.16f, 0.12f, 0.08f, 0.82f);
    private static readonly Color ActiveBorder = new Color(1f, 0.72f, 0.28f, 1f);
    private static readonly Color IdleBorder = new Color(1f, 1f, 1f, 0.16f);

    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private SlotWidget[] slots;

    [Serializable]
    public class SlotWidget
    {
        [SerializeField] private UnityEngine.UI.Image background;
        [SerializeField] private UnityEngine.UI.Image highlight;
        [SerializeField] private GameObject torchGlyph;

        public void Apply(bool occupied, bool showPreview, bool active)
        {
            if (background != null)
                background.color = occupied ? OccupiedBackground : EmptyBackground;
            if (highlight != null)
                highlight.color = active ? ActiveBorder : IdleBorder;
            if (torchGlyph != null && torchGlyph.activeSelf != showPreview)
                torchGlyph.SetActive(showPreview);
        }
    }

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
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

    public void Refresh()
    {
        if (inventory == null || slots == null)
            return;

        int count = Mathf.Min(slots.Length, inventory.SlotCount);
        for (int i = 0; i < count; i++)
        {
            if (slots[i] == null)
                continue;

            InventorySlotState state = inventory.GetSlot(i);
            bool occupied = state != null && state.occupied;
            bool showPreview = occupied && state.preview && state.item == PlayerInventory.TorchItemId;
            slots[i].Apply(occupied, showPreview, inventory.ActiveSlot == i);
        }
    }
}
