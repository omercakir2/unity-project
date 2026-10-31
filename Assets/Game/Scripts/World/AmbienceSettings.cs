using UnityEngine;

// Saved knobs for sound levels, the rain loop, and the local rain particles.
// Edits in Play mode stay on this asset after you stop.
[CreateAssetMenu(fileName = "AmbienceSettings", menuName = "Game/Ambience Settings")]
public class AmbienceSettings : ScriptableObject
{
    public const string ResourcePath = "AmbienceSettings";

    [Header("Main Theme")]
    [Range(0f, 1f)] public float themeVolume = 0.5f;

    [Header("Rain Audio")]
    [Range(0f, 1f)] public float rainVolume = 0.35f;
    [Range(0, 256)] public int rainPriority = 128;
    [Range(0f, 1f)] public float footstepVolume = 0.5f;

    [Header("Player")]
    [Range(0f, 1f)] public float walkBreathVolume = 0.32f;
    [Range(0f, 1f)] public float runBreathVolume = 0.4f;

    [Header("Zombie")]
    [Range(0f, 15f)] public float zombieCrawlVolume = 0.65f;
    [Range(0f, 15f)] public float zombieScreamVolume = 0.9f;
    [InspectorName("What the fuck")]
    [Range(0f, 1f)] public float wtfVolume = 0.9f;
    [Range(0f, 1f)] public float ghostStingVolume = 0.75f;

    [Header("Torch")]
    [Range(0f, 1f)] public float torchVolume = 0.4f;

    [Header("Pistol")]
    [Range(0f, 1f)] public float pistolShotVolume = 0.35f;
    [Tooltip("The tail file is much quieter than the shot, so full level still sits under the crack.")]
    [Range(0f, 1f)] public float pistolTailVolume = 1f;

    [Header("Rain Particles")]
    [Tooltip("Width and depth of the rain around the player, in meters.")]
    [Range(4f, 80f)] public float areaSize = 18f;
    [Tooltip("How high above the player the drops spawn, in meters.")]
    [Range(1f, 40f)] public float emitHeight = 11f;
    [Tooltip("Downward speed, in meters per second.")]
    [Range(0f, 40f)] public float fallSpeed = 22f;
    [Range(0.05f, 4f)] public float dropLifetime = 0.55f;
    [Range(0f, 2500f)] public float dropsPerSecond = 1400f;
    [Range(1, 2500)] public int maxDrops = 1600;
    [Tooltip("Width of each streak, in meters.")]
    [Range(0.002f, 0.08f)] public float dropWidth = 0.02f;
    public Color dropColor = new Color(0.95f, 0.97f, 1f, 0.8f);
    [Tooltip("Extra length from speed. The streaks also have a fixed length so they stay visible up close.")]
    [Range(0f, 1f)] public float streakLength = 0.2f;
    [Tooltip("Sideways lean, in meters per second. The reference rain is nearly vertical with a small tilt.")]
    [Range(-12f, 12f)] public float wind = 1.8f;

    private static AmbienceSettings fallback;

    public static AmbienceSettings Load()
    {
        return Resources.Load<AmbienceSettings>(ResourcePath);
    }

    public static AmbienceSettings LoadOrDefault()
    {
        AmbienceSettings loaded = Load();
        if (loaded != null)
            return loaded;

        if (fallback == null)
        {
            Debug.LogWarning("Ambience Settings asset is missing at Resources/" + ResourcePath + ".");
            fallback = CreateInstance<AmbienceSettings>();
        }

        return fallback;
    }
}
