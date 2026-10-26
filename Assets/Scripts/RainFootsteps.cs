using UnityEngine;

// Plays a random rain footstep while the player is walking on the ground.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public class RainFootsteps : MonoBehaviour
{
    private const string ClipFolder = "Audio/footsteps_audios";
    private const float WalkStride = 2.05f;
    private const float SprintStride = 2.35f;
    private const float MinSpeed = 0.4f;

    private PlayerController player;
    private CharacterController body;
    private AudioSource source;
    private AudioClip[] clips;
    private AmbienceSettings settings;
    private int lastClip = -1;
    private float traveled;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        body = GetComponent<CharacterController>();
        settings = AmbienceSettings.LoadOrDefault();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = 180;

        clips = Resources.LoadAll<AudioClip>(ClipFolder);
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("Rain footstep clips are missing at Resources/" + ClipFolder + ".");
            return;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                clips[i].LoadAudioData();
        }
    }

    private void Update()
    {
        if (clips == null || clips.Length == 0 || player == null)
            return;

        if (!player.IsGrounded || !player.IsMoving || player.IsTraversing)
        {
            traveled = 0f;
            return;
        }

        Vector3 velocity = body != null ? body.velocity : Vector3.zero;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        if (speed < MinSpeed)
            return;

        traveled += speed * Time.deltaTime;
        float stride = player.IsSprintingNow ? SprintStride : WalkStride;
        if (traveled < stride)
            return;

        traveled -= stride;
        PlayStep();
    }

    private void PlayStep()
    {
        int index = clips.Length == 1 ? 0 : NextClip();
        AudioClip clip = clips[index];
        if (clip == null || source == null)
            return;

        lastClip = index;
        source.pitch = Random.Range(0.94f, 1.06f);
        float volume = settings != null ? settings.footstepVolume : 0.5f;
        source.PlayOneShot(clip, volume);
    }

    private int NextClip()
    {
        int index = Random.Range(0, clips.Length);
        if (index == lastClip)
            index = (index + 1) % clips.Length;
        return index;
    }
}
