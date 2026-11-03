using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The card under the minimap. A new objective slides in once the HUD is actually on screen,
/// so one set during a cinematic is still noticed when the cinematic ends.
/// </summary>
[DisallowMultipleComponent]
public class ObjectiveCard : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform body;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image accent;
    [SerializeField] private float revealTime = 0.6f;
    [SerializeField] private float slideDistance = 40f;
    [SerializeField] private Color accentColor = new Color(0.35f, 0.9f, 1f, 1f);

    private Canvas rootCanvas;
    private Vector2 restPosition;
    private string shown;
    private bool pending;
    private float reveal = 1f;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
            rootCanvas = rootCanvas.rootCanvas;
        if (body != null)
            restPosition = body.anchoredPosition;
    }

    private void OnEnable()
    {
        SceneObjective.Changed += OnChanged;
        OnChanged(SceneObjective.Current);
    }

    private void OnDisable()
    {
        SceneObjective.Changed -= OnChanged;
    }

    private void OnChanged(string text)
    {
        if (text == shown)
            return;

        shown = text;
        if (label != null)
            label.text = text;
        pending = !string.IsNullOrEmpty(text);
        reveal = 0f;
        Apply();
    }

    private void Update()
    {
        bool onScreen = rootCanvas == null || rootCanvas.enabled;
        if (pending && onScreen)
        {
            reveal = Mathf.MoveTowards(reveal, 1f, Time.unscaledDeltaTime / revealTime);
            if (reveal >= 1f)
                pending = false;
            Apply();
        }
    }

    private void Apply()
    {
        bool hasObjective = !string.IsNullOrEmpty(shown);
        float t = hasObjective ? 1f - Mathf.Pow(1f - reveal, 3f) : 0f;
        if (group != null)
            group.alpha = t;
        if (body != null)
            body.anchoredPosition = restPosition + new Vector2(-slideDistance * (1f - t), 0f);
        if (accent != null)
        {
            // The bar flashes bright as the card lands, then settles.
            float flash = Mathf.Clamp01(1f - Mathf.Abs(reveal - 0.7f) / 0.3f);
            accent.color = Color.Lerp(accentColor * new Color(1f, 1f, 1f, 0.8f), Color.white, flash * 0.6f);
        }
    }
}
