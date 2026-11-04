using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Four-bar crosshair whose centre gap matches the pistol's current spread cone on screen.
/// Replaces the static "+" bars it is added to.
/// </summary>
public class CrosshairSpread : MonoBehaviour
{
    private const float BarLength = 12f;
    private const float BarThickness = 3f;
    private const float MinGap = 4f;
    private static readonly Color BarColor = new Color(1f, 1f, 1f, 0.92f);

    private PistolShot pistol;
    private Canvas canvas;
    private RectTransform top;
    private RectTransform bottom;
    private RectTransform left;
    private RectTransform right;

    private void Awake()
    {
        pistol = GetComponentInParent<PistolShot>();
        if (pistol == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
                pistol = player.GetComponent<PistolShot>();
        }
        canvas = GetComponentInParent<Canvas>();

        // The old "+" bars cross in the middle and would hide the gap.
        for (int i = transform.childCount - 1; i >= 0; i--)
            transform.GetChild(i).gameObject.SetActive(false);

        top = CreateBar("Top", new Vector2(BarThickness, BarLength));
        bottom = CreateBar("Bottom", new Vector2(BarThickness, BarLength));
        left = CreateBar("Left", new Vector2(BarLength, BarThickness));
        right = CreateBar("Right", new Vector2(BarLength, BarThickness));
    }

    private void LateUpdate()
    {
        float gap = Mathf.Max(MinGap, SpreadToCanvasUnits());
        float offset = gap + BarLength * 0.5f;
        top.anchoredPosition = new Vector2(0f, offset);
        bottom.anchoredPosition = new Vector2(0f, -offset);
        left.anchoredPosition = new Vector2(-offset, 0f);
        right.anchoredPosition = new Vector2(offset, 0f);
    }

    private float SpreadToCanvasUnits()
    {
        Camera view = Camera.main;
        if (pistol == null || view == null)
            return MinGap;

        // Screen distance of the cone edge from the centre, using the vertical field of view.
        float halfFov = view.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float pixels = Mathf.Tan(pistol.CurrentSpread * Mathf.Deg2Rad) / Mathf.Tan(halfFov) * (Screen.height * 0.5f);
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        return pixels / Mathf.Max(0.01f, scale);
    }

    private RectTransform CreateBar(string barName, Vector2 size)
    {
        GameObject bar = new GameObject(barName, typeof(RectTransform));
        bar.transform.SetParent(transform, false);
        Image image = bar.AddComponent<Image>();
        image.color = BarColor;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return rect;
    }
}
