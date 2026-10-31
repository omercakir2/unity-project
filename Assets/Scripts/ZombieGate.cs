using UnityEngine;

/// <summary>
/// Invisible volume. Zombies stay on the first crawl frame until the player walks through.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class ZombieGate : MonoBehaviour
{
    private bool passed;

    private void Awake()
    {
        Collider volume = GetComponent<Collider>();
        if (volume != null && !volume.isTrigger)
            volume.isTrigger = true;

        MeshRenderer mesh = GetComponent<MeshRenderer>();
        if (mesh != null && mesh.enabled)
            mesh.enabled = false;

        HoldZombies();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPass(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryPass(other);
    }

    private void TryPass(Collider other)
    {
        if (passed || other == null || !IsPlayer(other))
            return;

        passed = true;
        ReleaseZombies();
    }

    private static void HoldZombies()
    {
        ZombieController[] zombies = Object.FindObjectsByType<ZombieController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < zombies.Length; i++)
        {
            if (zombies[i] != null)
                zombies[i].HoldUntilGate();
        }
    }

    private static void ReleaseZombies()
    {
        ZombieController[] zombies = Object.FindObjectsByType<ZombieController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int started = 0;
        for (int i = 0; i < zombies.Length; i++)
        {
            if (zombies[i] == null)
                continue;

            zombies[i].Begin();
            started++;
        }

        Debug.Log("[ZombieGate] Player passed the gate. Started " + started + " zombie(s).");
    }

    private static bool IsPlayer(Collider other)
    {
        if (other.CompareTag("Player"))
            return true;

        return other.GetComponentInParent<PlayerController>() != null;
    }
}
