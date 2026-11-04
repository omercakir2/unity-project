using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows one subtitle line on its own canvas. It is not part of the player HUD and is
/// exempt from CinematicUI, so the same call works during cinematics and gameplay.
/// </summary>
[DisallowMultipleComponent]
public class Subtitles : MonoBehaviour
{
    private const int SortingOrder = 1100; // Above the wake-up fade, so lines read over black.
    private const float FadeTime = 0.25f;
    private const float BottomOffset = 156f;
    private const float PlateHeight = 64f;
    private const float PlatePadding = 56f;

    private static Subtitles instance;
    private static Sprite whiteSprite;

    private CanvasGroup group;
    private RectTransform plate;
    private TextMeshProUGUI label;
    private float shownFor;
    private float duration;

    /// <summary>Replaces whatever line is showing. Duration is in real seconds, fades included.</summary>
    public static void Show(string text, float seconds)
    {
        if (instance == null)
        {
            GameObject canvasObject = new GameObject("Subtitles", typeof(RectTransform));
            instance = canvasObject.AddComponent<Subtitles>();
            instance.Build();
        }

        instance.Display(text, seconds);
    }

    private void Display(string text, float seconds)
    {
        label.text = text;
        float width = label.GetPreferredValues(text).x + PlatePadding;
        plate.sizeDelta = new Vector2(width, PlateHeight);
        shownFor = 0f;
        duration = seconds;
        group.alpha = 0f;
        group.gameObject.SetActive(true);
    }

    // Real time, so a line spoken in slow motion stays up as long as its audio.
    private void Update()
    {
        if (!group.gameObject.activeSelf)
            return;

        shownFor += Time.unscaledDeltaTime;
        if (shownFor >= duration)
        {
            group.gameObject.SetActive(false);
            return;
        }

        float fadeIn = shownFor / FadeTime;
        float fadeOut = (duration - shownFor) / FadeTime;
        group.alpha = Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut));
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CinematicUI.Exempt(canvas);

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject root = new GameObject("Line", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, BottomOffset);
        rect.sizeDelta = new Vector2(1600f, 72f);

        group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject plateObject = new GameObject("Plate", typeof(RectTransform));
        plateObject.transform.SetParent(root.transform, false);
        plate = plateObject.GetComponent<RectTransform>();
        plate.anchorMin = new Vector2(0.5f, 0.5f);
        plate.anchorMax = new Vector2(0.5f, 0.5f);
        plate.pivot = new Vector2(0.5f, 0.5f);
        Image plateImage = plateObject.AddComponent<Image>();
        plateImage.sprite = WhiteSprite();
        plateImage.type = Image.Type.Simple;
        plateImage.color = new Color(0.04f, 0.03f, 0.02f, 0.78f);
        plateImage.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = SubtitleFont();
        label.fontSize = 34f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.96f, 0.88f, 0.96f);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineWidth = 0.16f;
        label.outlineColor = new Color(0f, 0f, 0f, 0.92f);

        root.SetActive(false);
    }

    private static TMP_FontAsset SubtitleFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return font != null ? font : TMP_Settings.defaultFontAsset;
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }
}
