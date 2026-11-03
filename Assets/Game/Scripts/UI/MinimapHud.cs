using UnityEngine;

/// <summary>
/// The map turns with the player, so the arrow stays up and the north marker
/// rides the rim to show where north went.
/// </summary>
[DisallowMultipleComponent]
public class MinimapHud : MonoBehaviour
{
    [SerializeField] private RectTransform northMarker;
    [SerializeField] private float northRadius = 140f;

    private Transform player;

    private void LateUpdate()
    {
        if (player == null)
        {
            PlayerController controller = GetComponentInParent<PlayerController>();
            if (controller == null)
                controller = FindAnyObjectByType<PlayerController>();
            if (controller == null)
                return;
            player = controller.transform;
        }

        if (northMarker == null)
            return;

        float angle = -player.eulerAngles.y * Mathf.Deg2Rad;
        northMarker.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * northRadius;
    }
}
