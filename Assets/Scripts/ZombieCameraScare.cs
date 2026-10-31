using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The first confirmed look plays the what-the-fuck line and the ghost sting.
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class ZombieCameraScare : MonoBehaviour
{
    private const string WtfPath = "Audio/Stingers/wtf-is-that-sound";
    private const string StingPath = "Audio/Stingers/scary-ghost-effect";
    private const string WtfLine = "What the fuck is that?";
    private const float EnterDelay = 0.15f;
    private const float SubtitleFade = 0.25f;

    private AmbienceSettings settings;
    private ZombieController controller;
    private AudioSource wtfSource;
    private AudioSource stingSource;
    private AudioClip wtf;
    private AudioClip sting;
    private Transform head;
    private Transform chest;
    private Transform hips;
    private float inFrameTime;
    private bool wtfPlayed;
    private bool stingPlayed;
    private GameObject subtitleRoot;
    private CanvasGroup subtitleGroup;
    private float subtitleTime;

    private void Awake()
    {
        settings = AmbienceSettings.LoadOrDefault();
        controller = GetComponent<ZombieController>();
        CacheBodyPoints();
        wtf = Resources.Load<AudioClip>(WtfPath);
        sting = Resources.Load<AudioClip>(StingPath);
        if (wtf == null)
            Debug.LogWarning("What the fuck clip is missing at Resources/" + WtfPath + ".");
        if (sting == null)
            Debug.LogWarning("Scary ghost clip is missing at Resources/" + StingPath + ".");

        wtfSource = CreateVoice(36, settings != null ? settings.wtfVolume : 0.9f);
        stingSource = CreateVoice(32, settings != null ? settings.ghostStingVolume : 0.75f);
    }

    private void OnDestroy()
    {
        if (subtitleRoot != null)
            Destroy(subtitleRoot);
    }

    private AudioSource CreateVoice(int priority, float volume)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = priority;
        source.volume = volume;
        source.bypassReverbZones = true;
        return source;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        if (controller != null && controller.IsWaitingForGate)
            return;

        TickSubtitle();

        if (settings != null)
        {
            if (wtfSource != null)
                wtfSource.volume = settings.wtfVolume;
            if (stingSource != null)
                stingSource.volume = settings.ghostStingVolume;
        }

        if (wtfPlayed && stingPlayed)
            return;

        if (!InGameplayFrame())
        {
            inFrameTime = 0f;
            return;
        }

        inFrameTime += Time.deltaTime;
        if (inFrameTime < EnterDelay)
            return;

        PlayWtf();
        PlaySting();
    }

    private void PlayWtf()
    {
        if (wtfPlayed)
            return;

        wtfPlayed = true;
        Debug.Log("[Zombie] First look. Playing what the fuck.");
        if (wtfSource != null && wtf != null)
            wtfSource.PlayOneShot(wtf);
        ShowSubtitle();
    }

    private void ShowSubtitle()
    {
        EnsureSubtitle();
        if (subtitleGroup == null)
            return;

        subtitleTime = wtf != null ? wtf.length : 2.5f;
        subtitleGroup.alpha = 1f;
        subtitleRoot.SetActive(true);
    }

    private void TickSubtitle()
    {
        if (subtitleRoot == null || !subtitleRoot.activeSelf)
            return;

        subtitleTime -= Time.deltaTime;
        if (subtitleTime <= 0f)
        {
            subtitleRoot.SetActive(false);
            return;
        }

        subtitleGroup.alpha = subtitleTime < SubtitleFade ? subtitleTime / SubtitleFade : 1f;
    }

    private void EnsureSubtitle()
    {
        if (subtitleRoot != null)
            return;

        Transform parent = FindHud();
        if (parent == null)
            parent = CreateFallbackCanvas().transform;

        subtitleRoot = new GameObject("WtfSubtitle", typeof(RectTransform));
        subtitleRoot.transform.SetParent(parent, false);
        subtitleRoot.transform.SetAsLastSibling();

        RectTransform rect = subtitleRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 156f);
        rect.sizeDelta = new Vector2(980f, 72f);

        subtitleGroup = subtitleRoot.AddComponent<CanvasGroup>();
        subtitleGroup.blocksRaycasts = false;
        subtitleGroup.interactable = false;

        GameObject plateObject = new GameObject("Plate", typeof(RectTransform));
        plateObject.transform.SetParent(subtitleRoot.transform, false);
        RectTransform plateRect = plateObject.GetComponent<RectTransform>();
        plateRect.anchorMin = new Vector2(0.5f, 0.5f);
        plateRect.anchorMax = new Vector2(0.5f, 0.5f);
        plateRect.pivot = new Vector2(0.5f, 0.5f);
        plateRect.sizeDelta = new Vector2(640f, 64f);
        Image plate = plateObject.AddComponent<Image>();
        plate.sprite = WhiteSprite();
        plate.type = Image.Type.Simple;
        plate.color = new Color(0.04f, 0.03f, 0.02f, 0.78f);
        plate.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(subtitleRoot.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = WtfLine;
        label.font = HudFont();
        label.fontSize = 34f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.96f, 0.88f, 0.96f);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineWidth = 0.16f;
        label.outlineColor = new Color(0f, 0f, 0f, 0.92f);

        subtitleRoot.SetActive(false);
    }

    private static Transform FindHud()
    {
        GameObject canvas = GameObject.Find("InventoryCanvas");
        return canvas != null ? canvas.transform : null;
    }

    private static Canvas CreateFallbackCanvas()
    {
        GameObject canvasObject = new GameObject("SubtitleCanvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static TMP_FontAsset HudFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null)
            return font;

        return TMP_Settings.defaultFontAsset;
    }

    private static Sprite whiteSprite;

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

    private void PlaySting()
    {
        stingPlayed = true;
        Debug.Log("[Zombie] First look. Playing the scary sting.");
        if (stingSource != null && sting != null)
            stingSource.PlayOneShot(sting);
    }

    private void CacheBodyPoints()
    {
        Transform spine = null;
        Transform spine1 = null;
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string bare = BareName(all[i].name);
            if (bare == "Head")
                head = all[i];
            else if (bare == "Hips")
                hips = all[i];
            else if (bare == "Spine2")
                chest = all[i];
            else if (bare == "Spine1")
                spine1 = all[i];
            else if (bare == "Spine")
                spine = all[i];
        }

        if (chest == null)
            chest = spine1 != null ? spine1 : spine;
    }

    private static string BareName(string boneName)
    {
        int split = boneName.LastIndexOf(':');
        return split >= 0 ? boneName.Substring(split + 1) : boneName;
    }

    // The skinned bounds hang below the crawler, so their center leaves the
    // frame while the head and chest are still visible.
    private bool InGameplayFrame()
    {
        Camera view = Camera.main;
        if (view == null)
            return false;

        if (PointInFrame(view, head) || PointInFrame(view, chest) || PointInFrame(view, hips))
            return true;

        if (head == null && chest == null && hips == null)
            return PointInFrame(view, transform.position + Vector3.up);

        return false;
    }

    private static bool PointInFrame(Camera view, Transform point)
    {
        return point != null && PointInFrame(view, point.position);
    }

    private static bool PointInFrame(Camera view, Vector3 world)
    {
        Vector3 viewport = view.WorldToViewportPoint(world);
        return viewport.z > 0f
            && viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
    }
}
