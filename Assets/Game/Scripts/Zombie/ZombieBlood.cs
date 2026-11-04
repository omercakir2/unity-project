using UnityEngine;

/// <summary>
/// One blood spray per pistol hit, thrown out to a random side and upward. Straight back
/// along the shot it would come at the camera and read as a dot.
/// The droplets live in world space, so the running zombie leaves them behind.
/// </summary>
[DisallowMultipleComponent]
public class ZombieBlood : MonoBehaviour
{
    private const string PrefabPath = "VFX/BloodHit";

    [SerializeField] private float hitSize = 1f;
    [SerializeField] private float killSize = 1.5f;
    [SerializeField] private float backwardShare = 0.35f;
    [SerializeField] private float upwardShare = 0.5f;
    [Tooltip("Scaled seconds, so the spray outlasts the slow motion too.")]
    [SerializeField] private float lifetime = 1.2f;

    private GameObject prefab;

    private void Awake()
    {
        prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
            Debug.LogWarning("[ZombieBlood] Missing effect at Resources/" + PrefabPath + ". Run Tools/Setup Blood Hit.");
    }

    public void Play(Vector3 point, Vector3 shotDirection, bool killed)
    {
        if (prefab == null)
            return;

        Vector3 side = Vector3.Cross(Vector3.up, shotDirection).normalized * (Random.value < 0.5f ? -1f : 1f);
        Vector3 spray = (side - shotDirection * backwardShare + Vector3.up * upwardShare).normalized;
        GameObject burst = Instantiate(prefab, point, Quaternion.LookRotation(spray));
        burst.transform.localScale = Vector3.one * (killed ? killSize : hitSize);
        Destroy(burst, lifetime);
    }
}
