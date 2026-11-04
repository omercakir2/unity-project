using UnityEngine;

/// <summary>
/// A light ball in flight or lying where it landed. Becomes a pickup once it has left the hand,
/// and goes back to the slot it was thrown from when picked up.
/// </summary>
public class ThrownLightBall : MonoBehaviour
{
    private const float PickupDelay = 0.6f;
    private const float LostHeight = -100f;
    // The held lamp only lights the player's surroundings; a thrown ball has to light a room.
    private const float ThrownIntensity = 12f;
    private const float ThrownRange = 14f;

    private int returnSlot = -1;
    private float pickupTime;
    private bool pickupReady;

    public void Init(int slot)
    {
        returnSlot = slot;
        pickupTime = Time.time + PickupDelay;

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].intensity = ThrownIntensity;
            lights[i].range = ThrownRange;
        }
    }

    private void Update()
    {
        if (!pickupReady && Time.time >= pickupTime)
        {
            pickupReady = true;
            ItemPickup pickup = gameObject.AddComponent<ItemPickup>();
            pickup.Configure(PlayerInventory.SearchlightItemId, returnSlot);
        }

        // Fell out of the world: hand it back instead of losing the only light.
        if (transform.position.y < LostHeight)
        {
            PlayerInventory inventory = FindAnyObjectByType<PlayerInventory>();
            if (inventory != null)
                inventory.TryAddToSlot(PlayerInventory.SearchlightItemId, returnSlot);
            Destroy(gameObject);
        }
    }
}
