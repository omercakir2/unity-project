using UnityEngine;

/// <summary>
/// Lives on the Animator so OnAnimatorIK runs. Plants stair feet on the tread under them.
/// </summary>
public class StairFootIk : MonoBehaviour
{
    private void OnAnimatorIK(int layer)
    {
        if (layer != 0) return;

        Animator anim = GetComponent<Animator>();
        PlayerController player = GetComponentInParent<PlayerController>();
        if (player != null)
            player.UpdateStairFeet(anim);

        HeldItemVisual held = GetComponentInParent<HeldItemVisual>();
        if (held != null)
            held.ApplySearchlightArm(anim);
    }
}
