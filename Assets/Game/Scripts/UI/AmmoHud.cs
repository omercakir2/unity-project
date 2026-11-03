using TMPro;
using UnityEngine;

/// <summary>
/// Bottom-right "magazine / reserve" readout. Visible while the pistol is the active item.
/// </summary>
public class AmmoHud : MonoBehaviour
{
    private static readonly Color AmmoColor = new Color(0.96f, 0.91f, 0.8f, 1f);
    private static readonly Color EmptyColor = new Color(0.85f, 0.18f, 0.16f, 1f);

    private PlayerInventory inventory;
    private PistolShot pistol;
    private GameObject root;
    private TextMeshProUGUI label;
    private int shownMagazine = -1;
    private int shownReserve = -1;

    private void Start()
    {
        Build();
    }

    private void Update()
    {
        if (root == null)
            return;

        if (inventory == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player == null)
                return;
            inventory = player.GetComponent<PlayerInventory>();
        }
        if (pistol == null && inventory != null)
            pistol = inventory.GetComponent<PistolShot>();

        bool visible = pistol != null && inventory != null && inventory.ActiveItemId == PlayerInventory.PistolItemId;
        if (root.activeSelf != visible)
            root.SetActive(visible);
        if (!visible)
            return;

        int magazine = pistol.AmmoInMagazine;
        int reserve = pistol.ReserveAmmo;
        if (magazine == shownMagazine && reserve == shownReserve)
            return;

        shownMagazine = magazine;
        shownReserve = reserve;
        label.text = "<size=150%>" + magazine + "</size> / " + reserve;
        label.color = magazine > 0 ? AmmoColor : EmptyColor;
    }

    private void Build()
    {
        root = new GameObject("AmmoCounter");
        root.transform.SetParent(transform, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-48f, 40f);
        rect.sizeDelta = new Vector2(220f, 64f);

        UnityEngine.UI.Image plate = root.AddComponent<UnityEngine.UI.Image>();
        plate.color = new Color(0.1f, 0.08f, 0.07f, 0.75f);
        plate.raycastTarget = false;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(root.transform, false);
        label = labelObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = AmmoColor;
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
