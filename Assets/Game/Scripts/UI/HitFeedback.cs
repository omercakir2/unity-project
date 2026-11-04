using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hit marker at the screen centre: a small white X on a wounding hit, a large red X with a
/// red screen edge and a camera jolt on the killing hit. Runs on real time so the zombie
/// slow motion does not stretch it.
/// </summary>
public class HitFeedback : MonoBehaviour
{
    private const float HitSize = 44f;
    private const float HitTime = 0.15f;
    private const float HitPunch = 1.2f;
    private const float KillSize = 84f;
    private const float KillTime = 0.4f;
    private const float KillPunch = 1.35f;
    private const float PunchShare = 0.25f; // Part of the marker's life spent settling to size.
    private const float EdgeAlpha = 0.45f;
    private const float EdgeTime = 0.3f;
    private const float KillShake = 0.4f;

    private const string HitClipPath = "Audio/Hit/hit-marker";
    private const string KillClipPath = "Audio/Hit/hit-kill";

    private static readonly Color HitColor = Color.white;
    private static readonly Color KillColor = new Color(1f, 0.05f, 0.05f, 1f);
    private static readonly Color EdgeColor = new Color(0.55f, 0f, 0f, 1f);

    private static Sprite markerSprite;
    private static Sprite edgeSprite;

    private PistolShot pistol;
    private HeldItemVisual held;
    private AmbienceSettings settings;
    private AudioSource audioSource;
    private AudioClip hitClip;
    private AudioClip killClip;
    private Image marker;
    private Image edge;
    private Color markerColor;
    private float markerSize;
    private float markerPunch;
    private float markerTime;
    private float markerAge;
    private float edgeAge = EdgeTime;

    private void Start()
    {
        settings = AmbienceSettings.LoadOrDefault();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
        hitClip = Resources.Load<AudioClip>(HitClipPath);
        killClip = Resources.Load<AudioClip>(KillClipPath);
        if (hitClip == null)
            Debug.LogWarning("[HitFeedback] Missing hit sound at Resources/" + HitClipPath + ".");
        if (killClip == null)
            Debug.LogWarning("[HitFeedback] Missing kill sound at Resources/" + KillClipPath + ".");
        Build();
    }

    private void OnDestroy()
    {
        if (pistol != null)
            pistol.ZombieHit -= OnZombieHit;
    }

    private void Update()
    {
        if (pistol == null)
            BindPistol();

        AnimateMarker();
        AnimateEdge();
    }

    private void BindPistol()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null)
            return;

        pistol = player.GetComponent<PistolShot>();
        held = player.GetComponent<HeldItemVisual>();
        if (pistol != null)
            pistol.ZombieHit += OnZombieHit;
    }

    private void OnZombieHit(bool killed)
    {
        markerAge = 0f;
        markerColor = killed ? KillColor : HitColor;
        markerSize = killed ? KillSize : HitSize;
        markerPunch = killed ? KillPunch : HitPunch;
        markerTime = killed ? KillTime : HitTime;
        marker.enabled = true;

        AudioClip clip = killed ? killClip : hitClip;
        if (clip != null)
            audioSource.PlayOneShot(clip, Volume(killed));
        if (!killed)
            return;

        edgeAge = 0f;
        edge.enabled = true;
        if (held != null)
            held.ShakeCamera(KillShake);
    }

    private float Volume(bool killed)
    {
        if (settings == null)
            return killed ? 0.8f : 0.6f;
        return killed ? settings.killMarkerVolume : settings.hitMarkerVolume;
    }

    private void AnimateMarker()
    {
        if (!marker.enabled)
            return;

        markerAge += Time.unscaledDeltaTime;
        float t = markerAge / markerTime;
        if (t >= 1f)
        {
            marker.enabled = false;
            return;
        }

        float settle = Mathf.Clamp01(t / PunchShare);
        float size = markerSize * Mathf.Lerp(markerPunch, 1f, 1f - (1f - settle) * (1f - settle));
        marker.rectTransform.sizeDelta = new Vector2(size, size);
        Color color = markerColor;
        color.a = 1f - t * t * t; // Holds, then drops off at the end.
        marker.color = color;
    }

    private void AnimateEdge()
    {
        if (!edge.enabled)
            return;

        edgeAge += Time.unscaledDeltaTime;
        float t = edgeAge / EdgeTime;
        if (t >= 1f)
        {
            edge.enabled = false;
            return;
        }

        Color color = EdgeColor;
        color.a = EdgeAlpha * (1f - t) * (1f - t);
        edge.color = color;
    }

    private void Build()
    {
        edge = CreateImage("HitEdge", EdgeSprite());
        RectTransform edgeRect = edge.rectTransform;
        edgeRect.anchorMin = Vector2.zero;
        edgeRect.anchorMax = Vector2.one;
        edgeRect.offsetMin = Vector2.zero;
        edgeRect.offsetMax = Vector2.zero;

        marker = CreateImage("HitMarker", MarkerSprite());
        RectTransform markerRect = marker.rectTransform;
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
    }

    private Image CreateImage(string imageName, Sprite sprite)
    {
        GameObject go = new GameObject(imageName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    // White rounded bars in a black outline. The image colour tints only the white part.
    private static Sprite MarkerSprite()
    {
        if (markerSprite != null)
            return markerSprite;

        const int size = 192;
        const float barStart = 0.34f;
        const float barEnd = 0.8f;
        const float radius = 0.095f;
        const float outline = 0.06f;
        float pixel = 2f / size;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Mirrored into one quadrant, so one diagonal bar draws all four.
                Vector2 p = new Vector2(Mathf.Abs((x + 0.5f) * pixel - 1f), Mathf.Abs((y + 0.5f) * pixel - 1f));
                float along = Mathf.Clamp((p.x + p.y) * 0.5f, barStart, barEnd);
                float distance = Vector2.Distance(p, new Vector2(along, along));
                float core = Mathf.Clamp01((radius - distance) / pixel + 0.5f);
                float alpha = Mathf.Clamp01((radius + outline - distance) / pixel + 0.5f);
                pixels[y * size + x] = new Color(core, core, core, alpha);
            }
        }

        markerSprite = CreateSprite(size, pixels);
        return markerSprite;
    }

    private static Sprite EdgeSprite()
    {
        if (edgeSprite != null)
            return edgeSprite;

        const int size = 128;
        float pixel = 2f / size;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1.4f, p.magnitude)));
            }
        }

        edgeSprite = CreateSprite(size, pixels);
        return edgeSprite;
    }

    private static Sprite CreateSprite(int size, Color[] pixels)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
