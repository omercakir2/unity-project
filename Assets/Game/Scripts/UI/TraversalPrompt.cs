using TMPro;
using UnityEngine;

/// <summary>
/// Centre-screen "'Space' to vault / climb up / climb over" hint, shown while a LedgeMarker is in reach.
/// </summary>
public class TraversalPrompt : MonoBehaviour
{
    private static readonly Color TextColor = new Color(0.96f, 0.91f, 0.8f, 1f);

    private PlayerController player;
    private GameObject root;
    private TextMeshProUGUI label;
    private bool hasShown;
    private LedgeType shownType;

    private void Start()
    {
        Build();
    }

    private void Update()
    {
        if (root == null)
            return;

        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        LedgeType type = LedgeType.Vault;
        bool visible = player != null && player.TryGetAvailableTraversal(out type);
        if (root.activeSelf != visible)
            root.SetActive(visible);
        if (!visible || (hasShown && type == shownType))
            return;

        hasShown = true;
        shownType = type;
        label.text = "'Space' to " + Verb(type);
    }

    private static string Verb(LedgeType type)
    {
        switch (type)
        {
            case LedgeType.ClimbUp:
                return "climb up";
            case LedgeType.ClimbOver:
                return "climb over";
            default:
                return "vault";
        }
    }

    private void Build()
    {
        root = new GameObject("TraversalPrompt");
        root.transform.SetParent(transform, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        // Below the crosshair, clear of its widest spread.
        rect.anchoredPosition = new Vector2(0f, -160f);
        rect.sizeDelta = new Vector2(320f, 52f);

        UnityEngine.UI.Image plate = root.AddComponent<UnityEngine.UI.Image>();
        plate.color = new Color(0.1f, 0.08f, 0.07f, 0.75f);
        plate.raycastTarget = false;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(root.transform, false);
        label = labelObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = TextColor;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null)
            label.font = font;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 4f);
        labelRect.offsetMax = new Vector2(-12f, -4f);

        root.SetActive(false);
    }
}
