using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Flashes short memory cuts (stills or silent videos) over the wake-up shot. Each cut opens
/// and closes on a white flash and slowly pushes in. The shot keeps running underneath, so
/// the capsule is seen between the cuts. WakeUpCinematic starts and stops this.
/// </summary>
[DisallowMultipleComponent]
public class WakeUpMemories : MonoBehaviour
{
    [Serializable]
    private class Cut
    {
        [Tooltip("Still image. Ignored when a video is set.")]
        public Texture image;
        public VideoClip video;
        [Tooltip("Seconds after the wake-up shot starts.")]
        public float startTime;
        [Tooltip("How long the picture stays up, flashes included.")]
        public float duration = 1.25f;
        [Tooltip("Where in the video this cut starts, so repeats show a different moment.")]
        public float videoOffset;
    }

    [Header("Cuts (in time order)")]
    [SerializeField] private Cut[] cuts;

    [Header("Look")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashInTime = 0.08f;
    [SerializeField] private float flashOutTime = 0.18f;
    [Tooltip("Scale reached at the end of a cut.")]
    [SerializeField] private float pushIn = 1.06f;
    [Tooltip("Black bar baked into the top of the media, as a fraction of its height.")]
    [SerializeField, Range(0f, 0.3f)] private float barTop = 0.096f;
    [Tooltip("Black bar baked into the bottom of the media, as a fraction of its height.")]
    [SerializeField, Range(0f, 0.3f)] private float barBottom = 0.053f;

    // Videos start this early, hidden, so the seek has landed before the picture shows.
    private const float VideoLead = 0.4f;
    private const int SortingOrder = 999; // Just under the wake-up fade.

    private readonly Dictionary<VideoClip, VideoPlayer> videos = new Dictionary<VideoClip, VideoPlayer>();
    private readonly List<RenderTexture> targets = new List<RenderTexture>();
    private Canvas canvas;
    private RawImage picture;
    private Image flash;
    private Coroutine routine;

    /// <summary>Starts the timeline. Call on the first frame of the wake-up shot.</summary>
    public void Play()
    {
        if (cuts == null || cuts.Length == 0)
            return;

        CreateOverlay();
        for (int i = 0; i < cuts.Length; i++)
        {
            if (cuts[i].video != null)
                Video(cuts[i].video);
        }

        routine = StartCoroutine(Run());
    }

    /// <summary>Ends whatever is showing and frees the videos.</summary>
    public void Stop()
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = null;
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private IEnumerator Run()
    {
        float startedAt = Time.time;
        for (int i = 0; i < cuts.Length; i++)
        {
            Cut cut = cuts[i];
            VideoPlayer video = cut.video != null ? Video(cut.video) : null;
            if (video != null)
            {
                while (Time.time - startedAt < cut.startTime - VideoLead)
                    yield return null;

                video.time = cut.videoOffset;
                video.Play();
            }

            while (Time.time - startedAt < cut.startTime)
                yield return null;

            Texture texture = video != null ? video.targetTexture : cut.image;
            if (texture == null || (video != null && !IsShowingFrames(video)))
            {
                Debug.LogWarning($"[WakeUpMemories] Skipping cut {i}: nothing to show yet.");
                continue;
            }

            yield return Show(cut, texture);
            if (video != null)
                video.Pause();
        }

        routine = null;
        Cleanup();
    }

    private IEnumerator Show(Cut cut, Texture texture)
    {
        yield return Flash(0f, 1f, flashInTime);

        picture.texture = texture;
        picture.uvRect = CoverRect(texture);
        picture.enabled = true;

        // The flashes share the picture's time so "duration" is what the eye gets.
        float elapsed = 0f;
        while (elapsed < cut.duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cut.duration);
            picture.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, pushIn, t);
            float opening = 1f - Mathf.Clamp01(elapsed / flashOutTime);
            float closing = Mathf.Clamp01((elapsed - (cut.duration - flashInTime)) / flashInTime);
            SetFlash(Mathf.Max(opening, closing));
            yield return null;
        }

        picture.enabled = false;
        yield return Flash(1f, 0f, flashOutTime);
    }

    private IEnumerator Flash(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetFlash(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetFlash(to);
    }

    private void SetFlash(float alpha)
    {
        Color color = flashColor;
        color.a = alpha;
        flash.color = color;
    }

    private static bool IsShowingFrames(VideoPlayer video)
    {
        return video.isPrepared && video.isPlaying && video.frame >= 0;
    }

    // Crops the baked letterbox, then fills the screen without stretching.
    private Rect CoverRect(Texture texture)
    {
        float width = 1f;
        float height = 1f - barTop - barBottom;
        float centerY = barBottom + height * 0.5f;
        float mediaAspect = (float)texture.width / (texture.height * height);
        float screenAspect = (float)Screen.width / Screen.height;
        if (mediaAspect > screenAspect)
            width = screenAspect / mediaAspect;
        else
            height *= mediaAspect / screenAspect;

        return new Rect(0.5f - width * 0.5f, centerY - height * 0.5f, width, height);
    }

    private VideoPlayer Video(VideoClip clip)
    {
        if (videos.TryGetValue(clip, out VideoPlayer existing))
            return existing;

        RenderTexture target = new RenderTexture((int)clip.width, (int)clip.height, 0);
        targets.Add(target);

        VideoPlayer video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.isLooping = true;
        video.clip = clip;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = target;
        video.audioOutputMode = VideoAudioOutputMode.None;
        video.skipOnDrop = true;
        video.Prepare();
        videos.Add(clip, video);
        return video;
    }

    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("Wake Up Memories", typeof(RectTransform));
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CinematicUI.Exempt(canvas);

        GameObject pictureObject = FullScreenChild("Picture", canvasObject.transform);
        picture = pictureObject.AddComponent<RawImage>();
        picture.raycastTarget = false;
        picture.enabled = false;

        GameObject flashObject = FullScreenChild("Flash", canvasObject.transform);
        flash = flashObject.AddComponent<Image>();
        flash.raycastTarget = false;
        SetFlash(0f);
    }

    private static GameObject FullScreenChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return child;
    }

    private void Cleanup()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
        canvas = null;

        foreach (VideoPlayer video in videos.Values)
        {
            if (video != null)
                Destroy(video);
        }

        videos.Clear();
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
            {
                targets[i].Release();
                Destroy(targets[i]);
            }
        }

        targets.Clear();
    }
}
