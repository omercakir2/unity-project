using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tab opens a five-slot backpack with a craft slot above the hotbar and pauses the game.
/// Items drag between backpack, hotbar, and craft slot. Dropping a second item on the
/// craft slot turns a matching pair into the recipe result.
/// </summary>
[DisallowMultipleComponent]
public class InventoryBagView : MonoBehaviour
{
    private static readonly Color EmptyBackground = new Color(0.08f, 0.08f, 0.08f, 0.55f);
    private static readonly Color OccupiedBackground = new Color(0.16f, 0.12f, 0.08f, 0.82f);
    private static readonly Color IdleBorder = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color HoverBorder = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color CraftBorder = new Color(1f, 0.72f, 0.28f, 0.45f);
    private static readonly Color AcceptBorder = new Color(1f, 0.72f, 0.28f, 1f);
    private static readonly Color RejectBorder = new Color(0.9f, 0.26f, 0.2f, 1f);
    private static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.14f);
    private static readonly Color PanelFill = new Color(0.05f, 0.045f, 0.04f, 0.93f);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.42f);
    private static readonly Color TitleColor = new Color(0.96f, 0.91f, 0.8f, 1f);
    private static readonly Color MutedText = new Color(0.96f, 0.91f, 0.8f, 0.55f);
    private static readonly Color DraggedSourceTint = new Color(1f, 1f, 1f, 0.3f);

    private const float SlotSize = 80f;
    private const float SlotGap = 12f;
    private const float Padding = 24f;
    private const float TitleBand = 56f;
    private const float FooterBand = 44f;
    // Leaves room for the hotbar caption above the active slot.
    private const float PanelGap = 32f;
    private const float OpenTime = 0.16f;
    private const float FlashTime = 0.45f;
    private const float ToastTime = 2.2f;
    private const float ToastFade = 0.3f;

    private class SlotParts
    {
        public InventorySlotHandle handle;
        public CanvasGroup group;
        public Image border;
        public Image background;
        public Image icon;
    }

    private PlayerInventory inventory;
    private InventoryHotbarView hotbar;
    private PlayerController player;
    private PlayerVitals vitals;
    private PlayerInput playerInput;
    private InputAction inventoryAction;
    private TMP_FontAsset font;
    private Sprite panelSprite;

    private RectTransform canvasRect;
    private RectTransform overlay;
    private CanvasGroup overlayGroup;
    private RectTransform panel;
    private TextMeshProUGUI footer;
    private Image ghost;
    private RectTransform toast;
    private CanvasGroup toastGroup;
    private Image toastIcon;
    private TextMeshProUGUI toastLabel;

    private readonly List<SlotParts> hotbarSlots = new List<SlotParts>();
    private readonly List<SlotParts> bagSlots = new List<SlotParts>();
    private SlotParts craftSlot;
    private GameObject craftEmptyMark;

    private bool built;
    private bool open;
    private float openAmount;
    private InventorySlotHandle dragSource;
    private InventorySlotHandle hovered;
    private float flashLeft;
    private Color flashColor;
    private InventorySlotHandle flashTarget;
    private float toastLeft;
    private Vector2 hotbarTopLeft;
    private Vector2 panelHome;

    public bool IsOpen => open;

    private void Start()
    {
        hotbar = GetComponentInChildren<InventoryHotbarView>(true);
        inventory = hotbar != null ? hotbar.Inventory : null;
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
        if (inventory == null || hotbar == null)
        {
            Debug.LogError("[Inventory] Backpack needs the hotbar and PlayerInventory on the player.");
            enabled = false;
            return;
        }

        player = inventory.GetComponent<PlayerController>();
        vitals = inventory.GetComponent<PlayerVitals>();
        playerInput = inventory.GetComponent<PlayerInput>();
        canvasRect = GetComponent<RectTransform>();

        Build();
        BindToggle();
        inventory.Changed += RefreshSlots;
        inventory.ItemAdded += OnItemAdded;
        inventory.AddFailed += OnAddFailed;
        inventory.Crafted += OnCrafted;
    }

    private void OnDestroy()
    {
        if (open)
            Close();
        if (inventoryAction != null)
            inventoryAction.performed -= OnInventoryPerformed;
        if (inventory == null)
            return;

        inventory.Changed -= RefreshSlots;
        inventory.ItemAdded -= OnItemAdded;
        inventory.AddFailed -= OnAddFailed;
        inventory.Crafted -= OnCrafted;
    }

    private void OnDisable()
    {
        if (open)
            Close();
    }

    private void Update()
    {
        if (!built)
            return;

        if (inventoryAction == null)
            BindToggle();
        if (inventoryAction == null && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            Toggle();
        else if (open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();

        if (open && vitals != null && vitals.IsDead)
            Close();

        AnimateOpen();
        TickFlash();
        TickToast();
    }

    private void BindToggle()
    {
        if (inventoryAction != null || playerInput == null || playerInput.actions == null)
            return;

        inventoryAction = playerInput.actions.FindAction("Inventory", false);
        if (inventoryAction != null)
            inventoryAction.performed += OnInventoryPerformed;
    }

    private void OnInventoryPerformed(InputAction.CallbackContext context)
    {
        if (built && isActiveAndEnabled)
            Toggle();
    }

    private void Toggle()
    {
        if (open)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (open)
            return;
        // Cinematics block gameplay input too. Do not stack a pause on top of one.
        if (GameplayInput.IsBlocked)
            return;
        if (vitals != null && vitals.IsDead)
            return;
        if (player != null && (player.IsInspecting || player.IsTraversing))
            return;

        open = true;
        PlaceAboveHotbar();
        overlay.gameObject.SetActive(true);
        GameTime.SetPaused(this, true);
        GameplayInput.SetBlocked(this, true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        hovered = null;
        RefreshSlots();
        ShowFooter(null);
        PlaceToast();
    }

    public void Close()
    {
        if (!open)
            return;

        CancelDrag();
        open = false;
        hovered = null;
        GameTime.SetPaused(this, false);
        GameplayInput.SetBlocked(this, false);
        if (vitals == null || !vitals.IsDead)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (hotbar != null)
            hotbar.Refresh();
        PlaceToast();
    }

    private void AnimateOpen()
    {
        float target = open ? 1f : 0f;
        if (Mathf.Approximately(openAmount, target))
        {
            if (!open && overlay.gameObject.activeSelf)
                overlay.gameObject.SetActive(false);
            return;
        }

        openAmount = Mathf.MoveTowards(openAmount, target, Time.unscaledDeltaTime / OpenTime);
        float eased = 1f - (1f - openAmount) * (1f - openAmount);
        overlayGroup.alpha = eased;
        panel.anchoredPosition = panelHome + new Vector2(0f, -18f * (1f - eased));
    }

    // Drag and drop

    public void BeginDrag(InventorySlotHandle source, PointerEventData eventData)
    {
        if (!open || source == null)
            return;

        InventorySlotState state = inventory.GetSlot(source.Area, source.Index);
        if (state == null || !state.occupied)
            return;

        dragSource = source;
        ghost.sprite = ItemCatalog.Icon(state.item);
        ghost.gameObject.SetActive(ghost.sprite != null);
        ghost.transform.SetAsLastSibling();
        Drag(eventData);
        RefreshSlots();
    }

    public void Drag(PointerEventData eventData)
    {
        if (dragSource == null || eventData == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, null, out Vector2 local))
            ghost.rectTransform.anchoredPosition = local;
    }

    public void DropOn(InventorySlotHandle target)
    {
        if (dragSource == null || target == null || target == dragSource)
            return;

        InventoryArea fromArea = dragSource.Area;
        int fromIndex = dragSource.Index;
        string moving = inventory.GetSlot(fromArea, fromIndex)?.item;
        string resting = inventory.GetSlot(target.Area, target.Index)?.item;
        InventoryMoveResult result = inventory.Move(fromArea, fromIndex, target.Area, target.Index);
        if (result == InventoryMoveResult.Rejected)
        {
            Flash(target, RejectBorder);
            bool materialOnHotbar = (target.Area == InventoryArea.Hotbar && ItemCatalog.IsBagOnly(moving))
                || (fromArea == InventoryArea.Hotbar && ItemCatalog.IsBagOnly(resting));
            ShowToast(materialOnHotbar ? "Materials stay in the backpack" : "These don't combine", null);
        }
        else if (result == InventoryMoveResult.Crafted)
        {
            Flash(target, AcceptBorder);
        }
    }

    public void EndDrag()
    {
        CancelDrag();
    }

    private void CancelDrag()
    {
        dragSource = null;
        if (ghost != null)
            ghost.gameObject.SetActive(false);
        RefreshSlots();
    }

    public void Hover(InventorySlotHandle handle, bool entered)
    {
        if (!open)
            return;

        if (entered)
            hovered = handle;
        else if (hovered == handle)
            hovered = null;

        InventorySlotState state = hovered != null ? inventory.GetSlot(hovered.Area, hovered.Index) : null;
        ShowFooter(state != null && state.occupied ? state.item : null);
        RefreshSlots();
    }

    public void Click(InventorySlotHandle handle, PointerEventData eventData)
    {
        if (!open || dragSource != null || handle == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (handle.Area == InventoryArea.Hotbar)
            inventory.SelectSlot(handle.Index);
    }

    // Visual state

    private void RefreshSlots()
    {
        if (!built)
            return;

        for (int i = 0; i < bagSlots.Count; i++)
            ApplyBagSlot(bagSlots[i], IdleBorder);
        ApplyBagSlot(craftSlot, CraftBorder);
        InventorySlotState crafted = inventory.GetSlot(InventoryArea.Craft, 0);
        if (craftEmptyMark != null)
            craftEmptyMark.SetActive(crafted == null || !crafted.occupied);

        if (hotbar != null)
            hotbar.Refresh();
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            SlotParts parts = hotbarSlots[i];
            parts.group.alpha = parts.handle == dragSource ? DraggedSourceTint.a : 1f;
            Color? border = DragBorder(parts.handle);
            if (border.HasValue && parts.border != null)
                parts.border.color = border.Value;
        }
    }

    private void ApplyBagSlot(SlotParts parts, Color idle)
    {
        InventorySlotState state = inventory.GetSlot(parts.handle.Area, parts.handle.Index);
        bool occupied = state != null && state.occupied;
        Sprite icon = occupied ? ItemCatalog.Icon(state.item) : null;
        parts.icon.sprite = icon;
        parts.icon.enabled = icon != null;
        parts.background.color = occupied ? OccupiedBackground : EmptyBackground;
        parts.group.alpha = parts.handle == dragSource ? DraggedSourceTint.a : 1f;

        Color border = idle;
        Color? drag = DragBorder(parts.handle);
        if (drag.HasValue)
            border = drag.Value;
        else if (open && dragSource == null && hovered == parts.handle && occupied)
            border = HoverBorder;
        if (flashLeft > 0f && flashTarget == parts.handle)
            border = Color.Lerp(border, flashColor, flashLeft / FlashTime);
        parts.border.color = border;
    }

    private Color? DragBorder(InventorySlotHandle handle)
    {
        if (dragSource == null || hovered != handle || handle == dragSource)
            return null;

        InventoryMoveResult check = inventory.CheckMove(dragSource.Area, dragSource.Index, handle.Area, handle.Index);
        return check == InventoryMoveResult.Rejected ? RejectBorder : AcceptBorder;
    }

    private void Flash(InventorySlotHandle target, Color color)
    {
        flashTarget = target;
        flashColor = color;
        flashLeft = FlashTime;
    }

    private void TickFlash()
    {
        if (flashLeft <= 0f)
            return;

        flashLeft = Mathf.Max(0f, flashLeft - Time.unscaledDeltaTime);
        RefreshSlots();
    }

    private void ShowFooter(string itemId)
    {
        if (footer == null)
            return;

        string name = ItemCatalog.DisplayName(itemId);
        if (!string.IsNullOrEmpty(name))
        {
            footer.text = name;
            footer.color = TitleColor;
            return;
        }

        string hint = ItemCatalog.RecipeHint();
        footer.text = string.IsNullOrEmpty(hint)
            ? "Drag items between slots"
            : "Combine on Craft:  " + hint;
        footer.color = MutedText;
    }

    // Toasts

    private void OnItemAdded(string itemId)
    {
        ShowToast("Picked up " + ItemCatalog.DisplayName(itemId), ItemCatalog.Icon(itemId));
    }

    private void OnAddFailed(string itemId)
    {
        ShowToast("Backpack is full", ItemCatalog.Icon(itemId));
    }

    private void OnCrafted(string itemId)
    {
        ShowToast("Crafted " + ItemCatalog.DisplayName(itemId), ItemCatalog.Icon(itemId));
    }

    private void ShowToast(string text, Sprite icon)
    {
        if (toast == null)
            return;

        toastLabel.text = text;
        toastIcon.sprite = icon;
        toastIcon.gameObject.SetActive(icon != null);
        toastLabel.rectTransform.offsetMin = new Vector2(icon != null ? 64f : 18f, 0f);
        toastLeft = ToastTime;
        toastGroup.alpha = 1f;
        toast.gameObject.SetActive(true);
        toast.SetAsLastSibling();
        PlaceToast();
    }

    private void PlaceToast()
    {
        if (toast == null || panel == null)
            return;

        float y = open ? panelHome.y + panel.sizeDelta.y + 14f : hotbarTopLeft.y + PanelGap;
        toast.anchoredPosition = new Vector2(hotbarTopLeft.x, y);
    }

    private void TickToast()
    {
        if (toastLeft <= 0f || toast == null)
            return;

        toastLeft -= Time.unscaledDeltaTime;
        if (toastLeft <= 0f)
        {
            toast.gameObject.SetActive(false);
            return;
        }

        toastGroup.alpha = toastLeft < ToastFade ? toastLeft / ToastFade : 1f;
    }

    // The panel and toasts sit just above the hotbar, left edges aligned, wherever the hotbar is placed.
    private void PlaceAboveHotbar()
    {
        RectTransform bar = hotbar.transform as RectTransform;
        if (bar == null || canvasRect == null)
            return;

        Vector3[] corners = new Vector3[4];
        bar.GetWorldCorners(corners);
        hotbarTopLeft = canvasRect.InverseTransformPoint(corners[1]);
        panelHome = hotbarTopLeft + new Vector2(0f, PanelGap);
        if (panel != null)
            panel.anchoredPosition = panelHome;
        PlaceToast();
    }

    // Building

    private void Build()
    {
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
            font = TMP_Settings.defaultFontAsset;

        InventoryHotbarView.SlotWidget first = hotbar.GetWidget(0);
        panelSprite = first != null && first.Background != null ? first.Background.sprite : null;

        BuildOverlay();
        BuildHotbarHandles();
        BuildGhost();
        BuildToast();

        built = true;
        PlaceAboveHotbar();
        overlayGroup.alpha = 0f;
        overlay.gameObject.SetActive(false);
        RefreshSlots();
    }

    private void BuildOverlay()
    {
        overlay = CreateRect("BackpackOverlay", transform);
        Stretch(overlay, 0f);
        // Under the hotbar, so the hotbar stays on top and can still be dragged.
        overlay.SetSiblingIndex(hotbar.transform.GetSiblingIndex());
        overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();

        Image dim = CreateImage("Dim", overlay, null, Dim);
        Stretch(dim.rectTransform, 0f);

        float bagWidth = PlayerInventory.BagSlots * SlotSize + (PlayerInventory.BagSlots - 1) * SlotGap;
        float craftX = Padding + bagWidth + 24f + 1f + 24f;
        float width = craftX + SlotSize + Padding;
        float height = TitleBand + SlotSize + FooterBand;

        panel = CreateRect("BackpackPanel", overlay);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = Vector2.zero;
        panel.sizeDelta = new Vector2(width, height);

        Image frame = CreateImage("Frame", panel, panelSprite, PanelBorder);
        Stretch(frame.rectTransform, 0f);
        Image fill = CreateImage("Fill", panel, panelSprite, PanelFill);
        Stretch(fill.rectTransform, 2f);

        TextMeshProUGUI title = CreateLabel("Title", panel, "BACKPACK", 20f, TitleColor, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 8f;
        PlaceTopLeft(title.rectTransform, new Vector2(Padding, -16f), new Vector2(bagWidth, 26f));

        TextMeshProUGUI close = CreateLabel("CloseHint", panel, "Close [Tab]", 15f, MutedText, TextAlignmentOptions.Right);
        close.rectTransform.anchorMin = new Vector2(1f, 0f);
        close.rectTransform.anchorMax = new Vector2(1f, 0f);
        close.rectTransform.pivot = new Vector2(1f, 0f);
        close.rectTransform.anchoredPosition = new Vector2(-Padding, 12f);
        close.rectTransform.sizeDelta = new Vector2(120f, 22f);

        Image rule = CreateImage("Rule", panel, null, PanelBorder);
        PlaceTopLeft(rule.rectTransform, new Vector2(Padding, -46f), new Vector2(width - Padding * 2f, 1f));

        for (int i = 0; i < PlayerInventory.BagSlots; i++)
        {
            Vector2 at = new Vector2(Padding + i * (SlotSize + SlotGap), -TitleBand);
            bagSlots.Add(CreateBagSlot("BagSlot" + (i + 1), at, InventoryArea.Bag, i));
        }

        Image divider = CreateImage("Divider", panel, null, PanelBorder);
        PlaceTopLeft(divider.rectTransform, new Vector2(Padding + bagWidth + 24f, -TitleBand), new Vector2(1f, SlotSize));

        TextMeshProUGUI craftTitle = CreateLabel("CraftTitle", panel, "CRAFT", 15f, CraftBorder, TextAlignmentOptions.Center);
        craftTitle.fontStyle = FontStyles.Bold;
        craftTitle.characterSpacing = 6f;
        PlaceTopLeft(craftTitle.rectTransform, new Vector2(craftX, -20f), new Vector2(SlotSize, 22f));

        craftSlot = CreateBagSlot("CraftSlot", new Vector2(craftX, -TitleBand), InventoryArea.Craft, 0);
        TextMeshProUGUI plus = CreateLabel("EmptyMark", craftSlot.handle.transform, "+", 34f, new Color(1f, 0.72f, 0.28f, 0.25f), TextAlignmentOptions.Center);
        Stretch(plus.rectTransform, 0f);
        plus.transform.SetSiblingIndex(craftSlot.icon.transform.GetSiblingIndex());
        craftEmptyMark = plus.gameObject;

        footer = CreateLabel("Footer", panel, "", 16f, MutedText, TextAlignmentOptions.Left);
        footer.rectTransform.anchorMin = new Vector2(0f, 0f);
        footer.rectTransform.anchorMax = new Vector2(1f, 0f);
        footer.rectTransform.pivot = new Vector2(0f, 0f);
        footer.rectTransform.anchoredPosition = new Vector2(Padding, 12f);
        footer.rectTransform.sizeDelta = new Vector2(-Padding * 2f - 130f, 22f);
        ShowFooter(null);
    }

    private SlotParts CreateBagSlot(string name, Vector2 topLeft, InventoryArea area, int index)
    {
        RectTransform root = CreateRect(name, panel);
        PlaceTopLeft(root, topLeft, new Vector2(SlotSize, SlotSize));

        SlotParts parts = new SlotParts();
        parts.group = root.gameObject.AddComponent<CanvasGroup>();
        parts.border = CreateImage("Border", root, panelSprite, IdleBorder);
        Stretch(parts.border.rectTransform, 0f);
        parts.background = CreateImage("Background", root, panelSprite, EmptyBackground);
        Stretch(parts.background.rectTransform, 4f);
        parts.background.raycastTarget = true;

        parts.icon = CreateImage("Icon", root, null, Color.white);
        parts.icon.type = Image.Type.Simple;
        parts.icon.preserveAspect = true;
        parts.icon.rectTransform.sizeDelta = new Vector2(60f, 60f);

        parts.handle = root.gameObject.AddComponent<InventorySlotHandle>();
        parts.handle.View = this;
        parts.handle.Area = area;
        parts.handle.Index = index;
        return parts;
    }

    private void BuildHotbarHandles()
    {
        int count = Mathf.Min(hotbar.SlotWidgetCount, inventory.SlotCount);
        for (int i = 0; i < count; i++)
        {
            InventoryHotbarView.SlotWidget widget = hotbar.GetWidget(i);
            if (widget == null || widget.Root == null)
                continue;

            if (widget.Background != null)
                widget.Background.raycastTarget = true;

            SlotParts parts = new SlotParts();
            parts.group = widget.Root.GetComponent<CanvasGroup>();
            if (parts.group == null)
                parts.group = widget.Root.gameObject.AddComponent<CanvasGroup>();
            parts.border = widget.Highlight;
            parts.background = widget.Background;
            parts.handle = widget.Root.gameObject.AddComponent<InventorySlotHandle>();
            parts.handle.View = this;
            parts.handle.Area = InventoryArea.Hotbar;
            parts.handle.Index = i;
            hotbarSlots.Add(parts);
        }
    }

    private void BuildGhost()
    {
        ghost = CreateImage("DragGhost", transform, null, new Color(1f, 1f, 1f, 0.92f));
        ghost.type = Image.Type.Simple;
        ghost.preserveAspect = true;
        ghost.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        ghost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.rectTransform.sizeDelta = new Vector2(68f, 68f);
        ghost.gameObject.SetActive(false);
    }

    private void BuildToast()
    {
        toast = CreateRect("InventoryToast", transform);
        toast.anchorMin = new Vector2(0.5f, 0.5f);
        toast.anchorMax = new Vector2(0.5f, 0.5f);
        toast.pivot = Vector2.zero;
        toast.sizeDelta = new Vector2(380f, 56f);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;

        Image plate = CreateImage("Plate", toast, panelSprite, new Color(0.04f, 0.03f, 0.02f, 0.86f));
        Stretch(plate.rectTransform, 0f);

        toastIcon = CreateImage("Icon", toast, null, Color.white);
        toastIcon.type = Image.Type.Simple;
        toastIcon.preserveAspect = true;
        toastIcon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        toastIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        toastIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
        toastIcon.rectTransform.anchoredPosition = new Vector2(14f, 0f);
        toastIcon.rectTransform.sizeDelta = new Vector2(40f, 40f);

        toastLabel = CreateLabel("Label", toast, "", 20f, TitleColor, TextAlignmentOptions.Left);
        toastLabel.rectTransform.anchorMin = Vector2.zero;
        toastLabel.rectTransform.anchorMax = Vector2.one;
        toastLabel.rectTransform.offsetMin = new Vector2(64f, 0f);
        toastLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);
        toast.gameObject.SetActive(false);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
