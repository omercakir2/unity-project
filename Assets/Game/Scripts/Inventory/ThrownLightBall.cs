using UnityEngine;

/// <summary>
/// A light ball in flight or lying where it landed. Becomes a pickup once it has left the hand,
/// and goes back to the slot it was thrown from when picked up.
/// </summary>
public class ThrownLightBall : MonoBehaviour
{
    private const float PickupDelay = 0.6f;
    private const float LostHeight = -100f;

    private int returnSlot = -1;
    private float pickupTime;
    private bool pickupReady;

    public void Init(int slot)
    {
        returnSlot = slot;
        pickupTime = Time.time + PickupDelay;
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
