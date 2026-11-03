using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pointer events for one hotbar, bag, or craft slot. All the logic lives in InventoryBagView.
/// </summary>
public class InventorySlotHandle : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public InventoryBagView View { get; set; }
    public InventoryArea Area { get; set; }
    public int Index { get; set; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        View?.BeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        View?.Drag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        View?.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        View?.DropOn(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        View?.Hover(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        View?.Hover(this, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        View?.Click(this, eventData);
    }
}
