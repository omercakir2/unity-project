using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Main menu entry: on hover or keyboard focus the label slides right, brightens and
/// an accent bar grows in from the left.
/// </summary>
[DisallowMultipleComponent]
public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text index;
    [SerializeField] private Image accent;
    [SerializeField] private float slide = 26f;
    [SerializeField] private float speed = 12f;
    [SerializeField] private Color idleColor = new Color(0.62f, 0.68f, 0.72f, 1f);
    [SerializeField] private Color hotColor = Color.white;
    [SerializeField] private Color accentColor = new Color(0.35f, 0.9f, 1f, 1f);

    private Selectable selectable;
    private Vector2 restPosition;
    private bool hovered;
    private bool selected;
    private float heat;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (content != null)
            restPosition = content.anchoredPosition;
        Apply(0f);
    }

    private void OnDisable()
    {
        hovered = false;
        selected = false;
        heat = 0f;
        Apply(0f);
    }

    private void Update()
    {
        bool interactable = selectable == null || selectable.IsInteractable();
        float target = interactable && (hovered || selected) ? 1f : 0f;
        heat = Mathf.MoveTowards(heat, target, speed * Time.unscaledDeltaTime);
        Apply(Mathf.SmoothStep(0f, 1f, heat));
    }

    // Hovering also moves keyboard focus, so mouse and keys never highlight two entries.
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (EventSystem.current != null && selectable != null && selectable.IsInteractable())
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    private void Apply(float t)
    {
        if (content != null)
            content.anchoredPosition = restPosition + new Vector2(slide * t, 0f);
        if (label != null)
            label.color = Color.Lerp(idleColor, hotColor, t);
        if (index != null)
            index.color = Color.Lerp(new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f), accentColor, t);
        if (accent != null)
        {
            accent.color = new Color(accentColor.r, accentColor.g, accentColor.b, t);
            accent.rectTransform.localScale = new Vector3(Mathf.Max(0.001f, t), 1f, 1f);
        }
    }
}
