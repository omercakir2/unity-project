using UnityEngine;

/// <summary>
/// Where the player stands after arriving through a <see cref="SceneDoor"/>.
/// Position is the feet, forward is the facing.
/// </summary>
public class SceneSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "Default";

    public string SpawnId => spawnId;

    public static SceneSpawnPoint Find(string id)
    {
        SceneSpawnPoint[] points = FindObjectsByType<SceneSpawnPoint>();
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].spawnId == id)
                return points[i];
        }

        return null;
    }

    /// <summary>Puts the player's feet on the point, facing its forward.</summary>
    public static void PlacePlayer(PlayerController player, Transform point)
    {
        CharacterController body = player.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (body != null)
            body.enabled = false;

        Transform root = player.transform;
        float feetOffset = body != null ? (body.center.y - body.height * 0.5f) * root.lossyScale.y : 0f;
        Vector3 forward = point.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        root.SetPositionAndRotation(point.position - Vector3.up * feetOffset, Quaternion.LookRotation(forward.normalized, Vector3.up));
        Physics.SyncTransforms();

        if (body != null)
            body.enabled = wasEnabled;

        CinemachineThirdPersonLook look = player.GetComponentInChildren<CinemachineThirdPersonLook>();
        if (look != null)
            look.SnapToBody();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + transform.forward * 0.8f);
    }
}
