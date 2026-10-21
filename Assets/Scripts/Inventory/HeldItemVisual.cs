using UnityEngine;

public class HeldItemVisual : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameObject torchPrefab;
    [SerializeField] private GameObject searchlightPrefab;
    [SerializeField] private GameObject pistolPrefab;
    [SerializeField] private Vector3 torchLocalPosition = new Vector3(0.01f, 0.27f, 0.05f);
    [SerializeField] private Vector3 torchLocalEuler = new Vector3(344.6f, 82.2f, 0f);
    [SerializeField] private Vector3 searchlightLocalPosition = new Vector3(0.021f, 0.118f, 0.002f);
    [SerializeField] private Vector3 searchlightLocalEuler = new Vector3(280.3f, 85f, 177.6f);
    [Header("Pistol In Hand")]
    [Tooltip("Gun offset in the right hand. +Y runs from the wrist toward the fingertips. The hand bone is half scale, so 0.10 here is about 5 cm.")]
    [SerializeField] private Vector3 pistolLocalPosition = new Vector3(0.02f, 0.28f, 0.06f);
    [Tooltip("Gun rotation in the right hand, degrees. X of -90 points the barrel along the fingers.")]
    [SerializeField] private Vector3 pistolLocalEuler = new Vector3(-90f, 0f, 0f);

    [Header("Pistol Arms")]
    [Tooltip("Right hand offset from the head, in meters. X is the character's right, Y is up, Z is forward along the aim.")]
    [SerializeField] private Vector3 pistolRightHandOffset = new Vector3(0.08f, 0f, 0.24f);
    [Tooltip("Extra height on the right hand for each radian the aim looks up or down, in meters.")]
    [SerializeField] private float pistolPitchLift = 0.05f;
    [Tooltip("How close and how far the right hand may sit from its shoulder, in meters. X is the minimum, Y is the maximum.")]
    [SerializeField] private Vector2 pistolRightReach = new Vector2(0.28f, 0.72f);
    [Tooltip("Left hand offset from the right hand, in meters. X is the character's right, Y is up, Z is forward along the aim.")]
    [SerializeField] private Vector3 pistolLeftHandOffset = new Vector3(-0.01f, 0f, 0.1f);
    [Tooltip("How close and how far the left hand may sit from its shoulder, in meters. X is the minimum, Y is the maximum.")]
    [SerializeField] private Vector2 pistolLeftReach = new Vector2(0.24f, 0.75f);
    [Tooltip("Right elbow hint, offset from the right shoulder. X is the character's right, Y is up, Z is forward along the aim.")]
    [SerializeField] private Vector3 pistolRightElbowOffset = new Vector3(0.22f, 0.06f, -0.05f);
    [Tooltip("Left elbow hint, offset from the left shoulder. X is the character's right, Y is up, Z is forward along the aim.")]
    [SerializeField] private Vector3 pistolLeftElbowOffset = new Vector3(-0.18f, 0.08f, 0.05f);
    [Tooltip("Roll of the right palm around the barrel, in degrees.")]
    [SerializeField] private float pistolRightGripRoll = -12f;
    [Tooltip("Roll of the left palm around the barrel, in degrees.")]
    [SerializeField] private float pistolLeftSupportRoll = 180f;
    [Tooltip("Finger curl, in degrees. X closes the fingers into the palm.")]
    [SerializeField] private Vector3 pistolFingerCurl = new Vector3(75f, 0f, 0f);
    [Tooltip("Right thumb curl, in degrees.")]
    [SerializeField] private Vector3 pistolRightThumbCurl = new Vector3(35f, -40f, 25f);
    [Tooltip("Left thumb curl, in degrees.")]
    [SerializeField] private Vector3 pistolLeftThumbCurl = new Vector3(35f, 40f, -25f);

    private const string HoldLayerName = "Hold";
    private const string HoldStateName = "Torch Hold";
    private const float HoldIdleNormalizedTime = 0.2f;
    private const float PoseSmoothTime = 0.32f;
    private const float PoseVisibleThreshold = 0.03f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HoldRateHash = Animator.StringToHash("HoldRate");

    // Torch-carry wrist, turned so its upright shaft lies along the searchlight aim.
    private static readonly Quaternion TorchGripHandLocal = new Quaternion(0.300f, 0.506f, 0.541f, 0.601f);
    // Handle offset in that fist, in left-hand local space.
    private static readonly Vector3 SearchlightGripLocalPosition = new Vector3(-0.051f, 0.291f, 0.141f);

    // Opened torch-carry curl so the fingers close around the flashlight instead of into the palm.
    private static readonly string[] FistBoneSuffixes =
    {
        "LeftHandThumb1", "LeftHandThumb2", "LeftHandThumb3",
        "LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3",
        "LeftHandMiddle1", "LeftHandMiddle2", "LeftHandMiddle3",
        "LeftHandRing1", "LeftHandRing2", "LeftHandRing3",
        "LeftHandPinky1", "LeftHandPinky2", "LeftHandPinky3"
    };

    private static readonly Quaternion[] FistBoneRotations =
    {
        new Quaternion(0.351f, -0.041f, -0.118f, 0.928f), new Quaternion(-0.064f, 0.020f, 0.170f, 0.983f), new Quaternion(-0.116f, 0.002f, 0.312f, 0.943f),
        new Quaternion(0.254f, -0.041f, 0.066f, 0.964f), new Quaternion(0.307f, 0f, 0f, 0.952f), new Quaternion(0.275f, 0f, 0f, 0.962f),
        new Quaternion(0.302f, -0.019f, 0.038f, 0.952f), new Quaternion(0.307f, 0f, 0f, 0.952f), new Quaternion(0.205f, 0f, 0f, 0.979f),
        new Quaternion(0.362f, 0.109f, 0.101f, 0.920f), new Quaternion(0.275f, 0f, 0f, 0.962f), new Quaternion(0.256f, 0f, 0f, 0.967f),
        new Quaternion(0.467f, 0.281f, 0.176f, 0.820f), new Quaternion(0.307f, 0f, 0f, 0.952f), new Quaternion(0.261f, 0f, 0f, 0.965f)
    };

    private static readonly string[] RightFistBoneSuffixes =
    {
        "RightHandThumb1", "RightHandThumb2", "RightHandThumb3",
        "RightHandIndex1", "RightHandIndex2", "RightHandIndex3",
        "RightHandMiddle1", "RightHandMiddle2", "RightHandMiddle3",
        "RightHandRing1", "RightHandRing2", "RightHandRing3",
        "RightHandPinky1", "RightHandPinky2", "RightHandPinky3"
    };

    private GameObject heldTorch;
    private GameObject heldSearchlight;
    private GameObject heldPistol;
    private Vector3 pistolAuthoredScale = Vector3.one;
    private Transform carryHand;
    private Transform gripHand;
    private Animator animator;
    private PlayerController player;
    private bool holdPoseParked;
    private bool poseReady;
    private string posedItem;
    private int poseFrame = -1;
    private float torchPoseWeight;
    private float torchPoseVelocity;
    private float searchPoseWeight;
    private float searchPoseVelocity;
    private float pistolPoseWeight;
    private float pistolPoseVelocity;
    private Transform[] fistBones;
    private Transform[] rightFistBones;

    public float PistolPoseWeight => pistolPoseWeight;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();
        player = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || inventory == null)
            return;

        inventory.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.Changed -= Refresh;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        ApplyPose();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        SeatSearchlight();
        SeatPistol();
    }

    private void Refresh()
    {
        if (!Application.isPlaying || inventory == null)
            return;

        if (inventory.ActiveItemId == PlayerInventory.TorchItemId)
            EnsureTorch();
        if (inventory.ActiveItemId == PlayerInventory.SearchlightItemId)
            EnsureSearchlight();
        if (inventory.ActiveItemId == PlayerInventory.PistolItemId)
            EnsurePistol();

        ApplyPose();
    }

    private void ApplyPose()
    {
        Animator anim = FindAnimator();
        if (anim == null || anim.runtimeAnimatorController == null || inventory == null)
            return;

        int layer = anim.GetLayerIndex(HoldLayerName);
        if (layer < 0)
            return;

        if (poseFrame == Time.frameCount)
            return;
        poseFrame = Time.frameCount;

        bool selectedTorch = inventory.ActiveItemId == PlayerInventory.TorchItemId;
        bool selectedSearchlight = inventory.ActiveItemId == PlayerInventory.SearchlightItemId;
        bool selectedPistol = inventory.ActiveItemId == PlayerInventory.PistolItemId;
        bool wantTorch = selectedTorch;
        bool wantSearchlight = selectedSearchlight && (player == null || !player.IsTraversing);
        bool wantPistol = selectedPistol && (player == null || !player.IsTraversing);
        float torchTarget = wantTorch ? 1f : 0f;
        float searchTarget = wantSearchlight ? 1f : 0f;
        float pistolTarget = wantPistol ? 1f : 0f;

        if (!poseReady)
        {
            torchPoseWeight = torchTarget;
            searchPoseWeight = searchTarget;
            pistolPoseWeight = pistolTarget;
            torchPoseVelocity = 0f;
            searchPoseVelocity = 0f;
            pistolPoseVelocity = 0f;
            poseReady = true;
        }
        else
        {
            torchPoseWeight = Mathf.SmoothDamp(torchPoseWeight, torchTarget, ref torchPoseVelocity, PoseSmoothTime);
            searchPoseWeight = Mathf.SmoothDamp(searchPoseWeight, searchTarget, ref searchPoseVelocity, PoseSmoothTime);
            pistolPoseWeight = Mathf.SmoothDamp(pistolPoseWeight, pistolTarget, ref pistolPoseVelocity, PoseSmoothTime);
        }

        torchPoseWeight = Mathf.Clamp01(torchPoseWeight);
        searchPoseWeight = Mathf.Clamp01(searchPoseWeight);
        pistolPoseWeight = Mathf.Clamp01(pistolPoseWeight);

        if (selectedTorch && torchPoseWeight >= searchPoseWeight && torchPoseWeight >= pistolPoseWeight)
            posedItem = PlayerInventory.TorchItemId;
        else if (selectedSearchlight && ((searchPoseWeight >= torchPoseWeight && searchPoseWeight >= pistolPoseWeight) || !wantSearchlight))
            posedItem = PlayerInventory.SearchlightItemId;
        else if (selectedPistol && ((pistolPoseWeight >= torchPoseWeight && pistolPoseWeight >= searchPoseWeight) || !wantPistol))
            posedItem = PlayerInventory.PistolItemId;
        else if (!selectedTorch && !selectedSearchlight && !selectedPistol
            && torchPoseWeight <= PoseVisibleThreshold
            && searchPoseWeight <= PoseVisibleThreshold
            && pistolPoseWeight <= PoseVisibleThreshold)
            posedItem = null;

        bool showTorch = posedItem == PlayerInventory.TorchItemId
            && (selectedTorch || torchPoseWeight > PoseVisibleThreshold);
        bool showSearchlight = posedItem == PlayerInventory.SearchlightItemId
            && (selectedSearchlight || searchPoseWeight > PoseVisibleThreshold);
        bool showPistol = posedItem == PlayerInventory.PistolItemId
            && (selectedPistol || pistolPoseWeight > PoseVisibleThreshold);

        if (showTorch)
            EnsureTorch();
        if (showSearchlight)
            EnsureSearchlight();
        if (showPistol)
            EnsurePistol();

        if (heldTorch != null && heldTorch.activeSelf != showTorch)
            heldTorch.SetActive(showTorch);
        if (heldSearchlight != null && heldSearchlight.activeSelf != showSearchlight)
            heldSearchlight.SetActive(showSearchlight);
        if (heldPistol != null && heldPistol.activeSelf != showPistol)
            heldPistol.SetActive(showPistol);

        anim.SetLayerWeight(layer, torchPoseWeight);

        float speed = anim.GetFloat(SpeedHash);
        bool idle = speed < 0.05f;
        float holdRate = 0f;
        if (torchPoseWeight > PoseVisibleThreshold && !idle)
            holdRate = speed < 0.75f ? 1f : 1.35f;
        anim.SetFloat(HoldRateHash, holdRate);
        ParkHoldIdle(anim, layer, wantTorch, idle);
    }

    private void ParkHoldIdle(Animator anim, int layer, bool wantTorch, bool idle)
    {
        if (!wantTorch || !idle)
        {
            holdPoseParked = false;
            return;
        }

        if (holdPoseParked)
            return;

        // Park while the layer is still invisible so equipping does not pop the arms.
        if (torchPoseWeight < 0.05f)
        {
            anim.Play(HoldStateName, layer, HoldIdleNormalizedTime);
            holdPoseParked = true;
            return;
        }

        float length = 1.1f;
        AnimatorClipInfo[] clips = anim.GetCurrentAnimatorClipInfo(layer);
        if (clips.Length > 0 && clips[0].clip != null)
            length = clips[0].clip.length;
        anim.CrossFadeInFixedTime(HoldStateName, 0.25f, layer, HoldIdleNormalizedTime * length);
        holdPoseParked = true;
    }

    public void ApplySearchlightArm(Animator anim)
    {
        if (anim == null || !anim.isHuman)
            return;

        if (searchPoseWeight <= 0.001f)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            return;
        }

        Transform shoulder = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        if (shoulder == null || chest == null)
            return;

        Vector3 aim = SearchlightAimDirection(anim.transform, chest.position);
        Vector3 flatAim = Vector3.ProjectOnPlane(aim, Vector3.up);
        if (flatAim.sqrMagnitude < 0.0001f)
            flatAim = anim.transform.forward;
        flatAim.Normalize();

        float pitch = Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f));
        // Keep the light at chest height. The lamp, not the whole arm, follows a steep look.
        Vector3 handTarget = chest.position
            + flatAim * 0.28f
            - anim.transform.right * 0.14f
            + Vector3.up * (Mathf.Clamp(pitch, -0.35f, 0.35f) * 0.08f + 0.02f);

        Vector3 reach = handTarget - shoulder.position;
        float reachLength = reach.magnitude;
        const float minReach = 0.22f;
        const float maxReach = 0.48f;
        if (reachLength > maxReach)
            handTarget = shoulder.position + reach / reachLength * maxReach;
        else if (reachLength < minReach)
            handTarget = shoulder.position + reach / Mathf.Max(reachLength, 0.001f) * minReach;

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, searchPoseWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, searchPoseWeight);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, handTarget);
        anim.SetIKRotation(AvatarIKGoal.LeftHand, GripRotation(anim.transform, aim));

        Vector3 hint = shoulder.position
            - anim.transform.right * 0.24f
            - flatAim * 0.02f
            + Vector3.down * 0.16f;
        anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, searchPoseWeight);
        anim.SetIKHintPosition(AvatarIKHint.LeftElbow, hint);
    }

    public void ApplyPistolArms(Animator anim)
    {
        if (anim == null || !anim.isHuman)
            return;

        bool leading = pistolPoseWeight > 0.001f && pistolPoseWeight >= searchPoseWeight;
        if (!leading)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            return;
        }

        Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        Transform rightShoulder = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform leftShoulder = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (chest == null || rightShoulder == null || leftShoulder == null)
            return;

        Vector3 aim = SearchlightAimDirection(anim.transform, head != null ? head.position : chest.position);
        Vector3 flatAim = Vector3.ProjectOnPlane(aim, Vector3.up);
        if (flatAim.sqrMagnitude < 0.0001f)
            flatAim = anim.transform.forward;
        flatAim.Normalize();

        // Keep the grip beside the head so the barrel sits at eye level.
        Vector3 headPosition = head != null ? head.position : chest.position + Vector3.up * 0.32f;
        float pitch = Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f));
        Vector3 rightHandTarget = headPosition
            + anim.transform.right * pistolRightHandOffset.x
            + Vector3.up * (pistolRightHandOffset.y + Mathf.Clamp(pitch, -0.2f, 0.25f) * pistolPitchLift)
            + flatAim * pistolRightHandOffset.z;
        rightHandTarget = ClampReach(rightShoulder.position, rightHandTarget, pistolRightReach.x, pistolRightReach.y);

        float weight = Mathf.Clamp01(pistolPoseWeight);
        Quaternion rightGrip = PointedGrip(anim.transform, aim, pistolRightGripRoll);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
        anim.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget);
        anim.SetIKRotation(AvatarIKGoal.RightHand, rightGrip);

        Vector3 rightHint = rightShoulder.position
            + anim.transform.right * pistolRightElbowOffset.x
            + Vector3.up * pistolRightElbowOffset.y
            + flatAim * pistolRightElbowOffset.z;
        anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);
        anim.SetIKHintPosition(AvatarIKHint.RightElbow, rightHint);

        Vector3 leftHandTarget = rightHandTarget
            + anim.transform.right * pistolLeftHandOffset.x
            + Vector3.up * pistolLeftHandOffset.y
            + flatAim * pistolLeftHandOffset.z;
        leftHandTarget = ClampReach(leftShoulder.position, leftHandTarget, pistolLeftReach.x, pistolLeftReach.y);
        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget);
        anim.SetIKRotation(AvatarIKGoal.LeftHand, PointedGrip(anim.transform, aim, pistolLeftSupportRoll));

        Vector3 leftHint = leftShoulder.position
            + anim.transform.right * pistolLeftElbowOffset.x
            + Vector3.up * pistolLeftElbowOffset.y
            + flatAim * pistolLeftElbowOffset.z;
        anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, weight);
        anim.SetIKHintPosition(AvatarIKHint.LeftElbow, leftHint);
    }

    private static Vector3 ClampReach(Vector3 shoulder, Vector3 handTarget, float minReach, float maxReach)
    {
        Vector3 reach = handTarget - shoulder;
        float reachLength = reach.magnitude;
        if (reachLength > maxReach)
            return shoulder + reach / reachLength * maxReach;
        if (reachLength < minReach)
            return shoulder + reach / Mathf.Max(reachLength, 0.001f) * minReach;
        return handTarget;
    }

    private static Quaternion PointedGrip(Transform body, Vector3 aim, float roll)
    {
        if (aim.sqrMagnitude < 0.0001f)
            aim = body.forward;
        aim.Normalize();

        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(aim, Vector3.up)) > 0.92f)
            up = body.forward;

        // Local +Y on a Mixamo hand runs wrist to fingertips. Pitch that axis onto the aim, then roll the palm around it.
        Quaternion pointed = Quaternion.LookRotation(aim, up) * Quaternion.Euler(90f, 0f, 0f);
        return pointed * Quaternion.AngleAxis(roll, Vector3.up);
    }

    private static Quaternion GripRotation(Transform body, Vector3 aim)
    {
        Quaternion upright = body.rotation * TorchGripHandLocal;
        if (aim.sqrMagnitude < 0.0001f)
            return upright;
        return Quaternion.FromToRotation(Vector3.up, aim.normalized) * upright;
    }

    private void SeatSearchlight()
    {
        if (searchPoseWeight <= 0.001f || heldSearchlight == null || !heldSearchlight.activeInHierarchy)
            return;

        Animator anim = FindAnimator();
        if (anim == null || !anim.isHuman)
            return;

        Transform hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        if (hand == null || chest == null)
            return;

        if (!CurlFist(anim, searchPoseWeight))
            return;

        Vector3 aim = SearchlightAimDirection(anim.transform, chest.position);
        float blend = Mathf.Clamp01(searchPoseWeight);
        hand.rotation = Quaternion.Slerp(hand.rotation, GripRotation(anim.transform, aim), blend);

        Transform light = heldSearchlight.transform;
        light.localPosition = SearchlightGripLocalPosition;
        light.localRotation = Quaternion.Euler(torchLocalEuler);

        SearchlightAim lampAim = heldSearchlight.GetComponent<SearchlightAim>();
        if (lampAim != null)
            lampAim.AimNow();
    }

    private void SeatPistol()
    {
        if (pistolPoseWeight <= 0.001f || pistolPoseWeight < searchPoseWeight)
            return;
        if (heldPistol == null || !heldPistol.activeInHierarchy)
            return;

        Animator anim = FindAnimator();
        if (anim == null || !anim.isHuman)
            return;

        Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        Transform rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        if (chest == null || rightHand == null || leftHand == null)
            return;

        if (!CurlPistolFingers(anim, pistolPoseWeight))
            return;

        Vector3 aim = SearchlightAimDirection(anim.transform, chest.position);
        float blend = Mathf.Clamp01(pistolPoseWeight);
        rightHand.rotation = Quaternion.Slerp(rightHand.rotation, PointedGrip(anim.transform, aim, pistolRightGripRoll), blend);
        leftHand.rotation = Quaternion.Slerp(leftHand.rotation, PointedGrip(anim.transform, aim, pistolLeftSupportRoll), blend);

        Transform gun = heldPistol.transform;
        gun.localPosition = pistolLocalPosition;
        gun.localRotation = Quaternion.Euler(pistolLocalEuler);
        // The hand bone is scaled with the avatar. Keep the prefab's own size, and never mirror it:
        // a negative scale on this skinned mesh inverts the view and blows the bounds.
        float boneScale = Mathf.Abs(rightHand.lossyScale.x);
        if (boneScale > 0.01f)
            gun.localScale = pistolAuthoredScale / boneScale;
    }

    private bool CurlPistolFingers(Animator anim, float weight)
    {
        if (!EnsureFistBones(anim) || !EnsureRightFistBones(anim))
            return false;

        Quaternion fingers = Quaternion.Euler(pistolFingerCurl);
        CurlSide(fistBones, weight, Quaternion.Euler(pistolLeftThumbCurl), fingers);
        CurlSide(rightFistBones, weight, Quaternion.Euler(pistolRightThumbCurl), fingers);
        return true;
    }

    private static void CurlSide(Transform[] bones, float weight, Quaternion thumb, Quaternion fingers)
    {
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null)
                continue;
            Quaternion target = i < 3 ? thumb : fingers;
            bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, target, weight);
        }
    }

    private bool EnsureRightFistBones(Animator anim)
    {
        if (rightFistBones != null && rightFistBones.Length == RightFistBoneSuffixes.Length && rightFistBones[0] != null)
            return true;

        rightFistBones = new Transform[RightFistBoneSuffixes.Length];
        Transform[] bones = anim.GetComponentsInChildren<Transform>(true);
        int found = 0;
        for (int i = 0; i < bones.Length; i++)
        {
            string name = bones[i].name;
            for (int s = 0; s < RightFistBoneSuffixes.Length; s++)
            {
                if (rightFistBones[s] != null || !name.EndsWith(RightFistBoneSuffixes[s]))
                    continue;
                rightFistBones[s] = bones[i];
                found++;
                break;
            }
        }

        return found > 0;
    }

    private bool CurlFist(Animator anim, float weight)
    {
        if (!EnsureFistBones(anim))
            return false;

        for (int i = 0; i < fistBones.Length; i++)
        {
            Transform bone = fistBones[i];
            if (bone == null)
                continue;
            bone.localRotation = Quaternion.Slerp(bone.localRotation, FistBoneRotations[i], weight);
        }

        return fistBones[1] != null && fistBones[4] != null && fistBones[6] != null && fistBones[7] != null;
    }

    private bool EnsureFistBones(Animator anim)
    {
        if (fistBones != null && fistBones.Length == FistBoneSuffixes.Length && fistBones[0] != null)
            return true;

        fistBones = new Transform[FistBoneSuffixes.Length];
        Transform[] bones = anim.GetComponentsInChildren<Transform>(true);
        int found = 0;
        for (int i = 0; i < bones.Length; i++)
        {
            string name = bones[i].name;
            for (int s = 0; s < FistBoneSuffixes.Length; s++)
            {
                if (fistBones[s] != null || !name.EndsWith(FistBoneSuffixes[s]))
                    continue;
                fistBones[s] = bones[i];
                found++;
                break;
            }
        }

        return found > 0;
    }

    private Vector3 SearchlightAimDirection(Transform body, Vector3 from)
    {
        Vector3 aimPoint = from + body.forward * 12f;
        if (heldSearchlight != null)
        {
            SearchlightAim aim = heldSearchlight.GetComponent<SearchlightAim>();
            if (aim != null)
                aim.TryGetAimPoint(out aimPoint);
        }

        Vector3 toPoint = aimPoint - from;
        Vector3 flat = Vector3.ProjectOnPlane(toPoint, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f)
            flat = body.forward;
        else
            flat.Normalize();

        // Keep the arm in front of the body when the crosshair falls behind or on the player.
        if (Vector3.Dot(flat, body.forward) < 0.2f)
            flat = Vector3.Slerp(body.forward, flat, 0.25f).normalized;

        float pitch = 0f;
        if (toPoint.sqrMagnitude > 0.0001f)
            pitch = Mathf.Asin(Mathf.Clamp(toPoint.normalized.y, -1f, 1f));
        pitch = Mathf.Clamp(pitch, -32f * Mathf.Deg2Rad, 32f * Mathf.Deg2Rad);
        return flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
    }

    private Animator FindAnimator()
    {
        if (animator != null)
            return animator;

        animator = GetComponentInChildren<Animator>(true);
        return animator;
    }

    private void EnsureTorch()
    {
        if (heldTorch != null)
            return;

        if (torchPrefab == null)
        {
            Debug.LogError("[Inventory] Torch prefab is not assigned.");
            return;
        }

        Transform hand = FindCarryHand();
        if (hand == null)
        {
            Debug.LogError("[Inventory] Carry hand bone was not found.");
            return;
        }

        heldTorch = Instantiate(torchPrefab, hand);
        heldTorch.name = "HeldTorch";
        ApplyGrip(heldTorch.transform, torchLocalPosition, torchLocalEuler);
        DisableColliders(heldTorch);
    }

    private void EnsureSearchlight()
    {
        if (heldSearchlight != null)
            return;

        if (searchlightPrefab == null)
        {
            Debug.LogError("[Inventory] Searchlight prefab is not assigned.");
            return;
        }

        Transform hand = FindCarryHand();
        if (hand == null)
        {
            Debug.LogError("[Inventory] Carry hand bone was not found.");
            return;
        }

        heldSearchlight = Instantiate(searchlightPrefab, hand);
        heldSearchlight.name = "HeldSearchlight";
        ApplyGrip(heldSearchlight.transform, searchlightLocalPosition, searchlightLocalEuler);

        SearchlightAim aim = heldSearchlight.GetComponent<SearchlightAim>();
        if (aim != null)
            aim.SetOwner(transform);

        DisableColliders(heldSearchlight);
    }

    private void EnsurePistol()
    {
        if (heldPistol != null)
            return;

        if (pistolPrefab == null)
        {
            Debug.LogError("[Inventory] Pistol prefab is not assigned.");
            return;
        }

        Transform hand = FindGripHand();
        if (hand == null)
        {
            Debug.LogError("[Inventory] Grip hand bone was not found.");
            return;
        }

        heldPistol = Instantiate(pistolPrefab, hand);
        heldPistol.name = "HeldPistol";
        Vector3 authored = heldPistol.transform.localScale;
        pistolAuthoredScale = new Vector3(Mathf.Abs(authored.x), Mathf.Abs(authored.y), Mathf.Abs(authored.z));
        if (pistolAuthoredScale.sqrMagnitude < 0.0001f)
            pistolAuthoredScale = Vector3.one;
        ApplyGrip(heldPistol.transform, pistolLocalPosition, pistolLocalEuler);
        DisableColliders(heldPistol);
        SilenceImportedSceneObjects(heldPistol);
    }

    private static void ApplyGrip(Transform item, Vector3 localPosition, Vector3 localEuler)
    {
        item.localPosition = localPosition;
        item.localRotation = Quaternion.Euler(localEuler);
    }

    private static void DisableColliders(GameObject item)
    {
        Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    // Blender exports often keep the scene camera and sun inside the gun file.
    // Parenting those to the hand steals the game view and floods the scene with light.
    private static void SilenceImportedSceneObjects(GameObject item)
    {
        Camera[] cameras = item.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].gameObject.SetActive(false);

        Light[] lights = item.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            lights[i].gameObject.SetActive(false);
    }

    public void DetachHeldItems()
    {
        if (animator != null)
        {
            int layer = animator.GetLayerIndex(HoldLayerName);
            if (layer >= 0)
                animator.SetLayerWeight(layer, 0f);
        }

        Park(heldTorch);
        Park(heldSearchlight);
        Park(heldPistol);
        carryHand = null;
        gripHand = null;
        animator = null;
        holdPoseParked = false;
        poseReady = false;
        torchPoseWeight = 0f;
        searchPoseWeight = 0f;
        pistolPoseWeight = 0f;
        torchPoseVelocity = 0f;
        searchPoseVelocity = 0f;
        pistolPoseVelocity = 0f;
        fistBones = null;
        rightFistBones = null;
    }

    public void RebindHand()
    {
        carryHand = null;
        gripHand = null;
        Transform hand = FindCarryHand();
        Transform pistolHand = FindGripHand();
        if (hand == null && (heldTorch != null || heldSearchlight != null))
            Debug.LogError("[Inventory] Carry hand bone was not found.");
        if (pistolHand == null && heldPistol != null)
            Debug.LogError("[Inventory] Grip hand bone was not found.");

        Attach(heldTorch, hand, torchLocalPosition, torchLocalEuler);
        Attach(heldSearchlight, hand, searchlightLocalPosition, searchlightLocalEuler);
        Attach(heldPistol, pistolHand, pistolLocalPosition, pistolLocalEuler);
    }

    private void Park(GameObject item)
    {
        if (item == null)
            return;

        item.transform.SetParent(transform, false);
    }

    private void Attach(GameObject item, Transform hand, Vector3 localPosition, Vector3 localEuler)
    {
        if (item == null || hand == null)
            return;

        bool active = item.activeSelf;
        item.transform.SetParent(hand, false);
        ApplyGrip(item.transform, localPosition, localEuler);
        if (item.activeSelf != active)
            item.SetActive(active);
    }

    private Transform FindCarryHand()
    {
        if (carryHand != null)
            return carryHand;

        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            // The torch walk carries with the left hand. The right arm swings free.
            if (bones[i].name.EndsWith("LeftHand"))
            {
                carryHand = bones[i];
                return carryHand;
            }
        }

        return null;
    }

    private Transform FindGripHand()
    {
        if (gripHand != null)
            return gripHand;

        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].name.EndsWith("RightHand"))
            {
                gripHand = bones[i];
                return gripHand;
            }
        }

        return null;
    }
}
