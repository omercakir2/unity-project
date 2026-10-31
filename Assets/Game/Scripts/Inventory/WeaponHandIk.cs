using UnityEngine;

/// <summary>
/// Lives on the Animator so OnAnimatorIK runs. Pulls the left hand to LeftHandGrip
/// when the equipped right-hand weapon has one.
/// </summary>
public class WeaponHandIk : MonoBehaviour
{
    public static void Ensure(Animator anim)
    {
        if (anim == null || anim.GetComponent<WeaponHandIk>() == null)
        {
            if (anim == null)
                return;
            anim.gameObject.AddComponent<WeaponHandIk>();
        }
    }

    private void OnAnimatorIK(int layer)
    {
        Animator anim = GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
            return;

        Transform target = FindLeftHandTarget(anim);
        if (target == null)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            return;
        }

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, target.position);
        anim.SetIKRotation(AvatarIKGoal.LeftHand, target.rotation);
    }

    private static Transform FindLeftHandTarget(Animator anim)
    {
        Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
            return null;

        for (int i = 0; i < hand.childCount; i++)
        {
            Transform child = hand.GetChild(i);
            if (!child.gameObject.activeInHierarchy)
                continue;

            WeaponGrip grip = child.GetComponent<WeaponGrip>();
            if (grip == null || !grip.HasLeftHandGrip)
                continue;

            return grip.LeftHandGrip;
        }

        return null;
    }
}
