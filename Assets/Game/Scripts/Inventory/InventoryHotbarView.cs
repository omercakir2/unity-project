using System;
using TMPro;
using UnityEngine;

public class InventoryHotbarView : MonoBehaviour
{
    private static readonly Color EmptyBackground = new Color(0.08f, 0.08f, 0.08f, 0.55f);
    private static readonly Color OccupiedBackground = new Color(0.16f, 0.12f, 0.08f, 0.82f);
    private static readonly Color ActiveBorder = new Color(1f, 0.72f, 0.28f, 1f);
    private static readonly Color IdleBorder = new Color(1f, 1f, 1f, 0.16f);

    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private SlotWidget[] slots;
    [SerializeField] private GameObject crosshair;

    [Serializable]
    public class SlotWidget
    {
        [SerializeField] private UnityEngine.UI.Image background;
        [SerializeField] private UnityEngine.UI.Image highlight;
        [SerializeField] private GameObject torchGlyph;
        [SerializeField] private GameObject searchlightGlyph;
        [SerializeField] private GameObject pistolGlyph;
        [SerializeField] private TMP_Text selectionLabel;

        public RectTransform Root => background != null ? background.transform.parent as RectTransform : null;
        public UnityEngine.UI.Image Background => background;
        public UnityEngine.UI.Image Highlight => highlight;

        // The builder only gave Slot3 a pistol icon. Items can move now, so every
        // slot needs every icon.
        public void FillMissingGlyphs(SlotWidget source)
        {
            if (source == null || Root == null)
                return;

            torchGlyph = torchGlyph != null ? torchGlyph : CloneGlyph(source.torchGlyph);
            searchlightGlyph = searchlightGlyph != null ? searchlightGlyph : CloneGlyph(source.searchlightGlyph);
            pistolGlyph = pistolGlyph != null ? pistolGlyph : CloneGlyph(source.pistolGlyph);
        }

        public GameObject GlyphTemplate(string itemId)
        {
            if (itemId == PlayerInventory.TorchItemId)
                return torchGlyph;
            if (itemId == PlayerInventory.SearchlightItemId)
                return searchlightGlyph;
            if (itemId == PlayerInventory.PistolItemId)
                return pistolGlyph;
            return null;
        }

        private GameObject CloneGlyph(GameObject template)
        {
            if (template == null)
                return null;

            GameObject copy = UnityEngine.Object.Instantiate(template, Root, false);
            copy.name = template.name;
            copy.SetActive(false);
            Transform index = Root.Find("Index");
            if (index != null)
                copy.transform.SetSiblingIndex(index.GetSiblingIndex());
            return copy;
        }

        public void Apply(bool occupied, string itemId, bool showPreview, bool active)
        {
            if (background != null)
                background.color = occupied ? OccupiedBackground : EmptyBackground;
            if (highlight != null)
                highlight.color = active ? ActiveBorder : IdleBorder;

            bool showTorch = showPreview && itemId == PlayerInventory.TorchItemId;
            bool showSearchlight = showPreview && itemId == PlayerInventory.SearchlightItemId;
            bool showPistol = showPreview && itemId == PlayerInventory.PistolItemId;
            if (torchGlyph != null && torchGlyph.activeSelf != showTorch)
                torchGlyph.SetActive(showTorch);
            if (searchlightGlyph != null && searchlightGlyph.activeSelf != showSearchlight)
                searchlightGlyph.SetActive(showSearchlight);
            if (pistolGlyph != null && pistolGlyph.activeSelf != showPistol)
                pistolGlyph.SetActive(showPistol);

            string caption = active && occupied ? CaptionFor(itemId) : null;
            if (selectionLabel == null)
                return;

            bool showCaption = !string.IsNullOrEmpty(caption);
            if (selectionLabel.gameObject.activeSelf != showCaption)
                selectionLabel.gameObject.SetActive(showCaption);
            if (showCaption)
                selectionLabel.text = caption;
        }

        private static string CaptionFor(string itemId)
        {
            return ItemCatalog.DisplayName(itemId);
        }
    }

    public PlayerInventory Inventory => inventory;
    public int SlotWidgetCount => slots == null ? 0 : slots.Length;

    public SlotWidget GetWidget(int index)
    {
        return slots == null || index < 0 || index >= slots.Length ? null : slots[index];
    }

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
        if (!Application.isPlaying)
            return;

        FillMissingGlyphs();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<InventoryBagView>() == null)
            canvas.gameObject.AddComponent<InventoryBagView>();
        if (canvas != null && canvas.GetComponent<AmmoHud>() == null)
            canvas.gameObject.AddComponent<AmmoHud>();
    }

    private void FillMissingGlyphs()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            for (int j = 0; j < slots.Length; j++)
            {
                if (i != j && slots[i] != null)
                    slots[i].FillMissingGlyphs(slots[j]);
            }
        }
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
            bool showPreview = occupied && state.preview;
            string itemId = state != null ? state.item : null;
            slots[i].Apply(occupied, itemId, showPreview, inventory.ActiveSlot == i);
        }

        if (crosshair != null)
        {
            bool showCrosshair = inventory.ActiveItemId == PlayerInventory.PistolItemId;
            if (crosshair.activeSelf != showCrosshair)
                crosshair.SetActive(showCrosshair);
        }
    }
}
