using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(250)]
public class HeldItemVisual : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameObject torchPrefab;
    [SerializeField] private GameObject searchlightPrefab;
    [SerializeField] private GameObject pistolPrefab;
    [SerializeField] CinemachineImpulseSource impulse;

    private const string HoldLayerName = "Hold";
    private const string HoldStateName = "Torch Hold";
    private const float HoldIdleNormalizedTime = 0.2f;
    private const float PoseSmoothTime = 0.32f;
    private const float PoseVisibleThreshold = 0.03f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HoldRateHash = Animator.StringToHash("HoldRate");
    private static readonly int PistolHash = Animator.StringToHash("Pistol");
    private static readonly int PistolSideHash = Animator.StringToHash("PistolSide");

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
    private float pistolMirror;
    private bool restHandsReady;
    private Quaternion restRightHand = Quaternion.identity;
    private Quaternion restLeftHand = Quaternion.identity;
    private Animator sideParameterChecked;
    private bool hasSideParameter;

    public float PistolPoseWeight => pistolPoseWeight;

    // 0 holds the pistol right-handed as authored, 1 is the mirrored left-handed hold.
    // The camera shoulder swap drives it.
    public float PistolMirror
    {
        get => pistolMirror;
        set => pistolMirror = Mathf.Clamp01(value);
    }
    public float SearchPoseWeight => searchPoseWeight;
    public GameObject SearchlightPrefab => searchlightPrefab;

    // The thrown ball replaces the carried one, so the carried one stays hidden while it fades out.
    public bool HideSearchlight { get; set; }

    private float gunRecoil;
    


    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();
        player = GetComponent<PlayerController>();
        if (Application.isPlaying && GetComponent<PistolShot>() == null)
            gameObject.AddComponent<PistolShot>();
        if (Application.isPlaying && GetComponent<LightBallThrow>() == null)
            gameObject.AddComponent<LightBallThrow>();
    }

    public void KickPistol()
    {
        gunRecoil = 1f;
        // Hafif yukarı ve rastgele yana tepme
        Vector3 kick = new Vector3(Random.Range(-0.3f, 0.3f), 1f, 0f);
        impulse.GenerateImpulse(kick * 0.15f);
    }

    public bool TryGetMuzzle(out Vector3 origin, out Vector3 direction)
    {
        origin = transform.position + Vector3.up * 1.4f;
        direction = transform.forward;
        if (heldPistol == null || !heldPistol.activeInHierarchy || pistolPoseWeight < 0.2f)
            return false;

        direction = heldPistol.transform.forward;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        direction.Normalize();

        Renderer renderer = heldPistol.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            origin = heldPistol.transform.position + direction * 0.12f;
            return true;
        }

        Bounds bounds = renderer.bounds;
        Vector3 absoluteDirection = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
        float half = Vector3.Dot(bounds.extents, absoluteDirection);
        origin = bounds.center + direction * (half + 0.02f);
        return true;
    }

    public void AimPistolAt(Vector3 worldPoint, float weight)
    {
        if (weight <= 0.001f || heldPistol == null || !heldPistol.activeInHierarchy)
            return;
        // The muzzle is ahead of the pivot, so one twist leaves the barrel short of the crosshair.
        float blend = Mathf.Clamp01(weight);
        for (int i = 0; i < 3; i++)
        {
            if (!TryGetMuzzle(out Vector3 origin, out Vector3 direction))
                return;

            Vector3 aim = worldPoint - origin;
            if (aim.sqrMagnitude < 0.0001f)
                return;

            Quaternion correction = Quaternion.FromToRotation(direction, aim.normalized);
            heldPistol.transform.rotation = Quaternion.Slerp(Quaternion.identity, correction, blend) * heldPistol.transform.rotation;
        }
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

        if (player != null && player.IsInspecting)
        {
            anim.SetLayerWeight(layer, 0f);
            return;
        }

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
            && (selectedSearchlight || searchPoseWeight > PoseVisibleThreshold)
            && !HideSearchlight;
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
        // Drop the holstered gun once the arm blend has finished, then spawn a fresh one next equip.
        if (heldPistol != null && !showPistol && !selectedPistol)
        {
            Destroy(heldPistol);
            heldPistol = null;
        }

        float carryWeight = 0f;
        if (posedItem == PlayerInventory.TorchItemId)
            carryWeight = torchPoseWeight;
        else if (posedItem == PlayerInventory.SearchlightItemId)
            carryWeight = searchPoseWeight;
        anim.SetLayerWeight(layer, carryWeight);

        float speed = anim.GetFloat(SpeedHash);
        bool idle = speed < 0.05f;
        // Freeze the held pose while standing. While moving, play it at the clip rate.
        float holdRate = carryWeight > PoseVisibleThreshold && !idle ? 1f : 0f;
        anim.SetFloat(HoldRateHash, holdRate);
        ParkHoldIdle(anim, layer, wantTorch || wantSearchlight, idle);
        // The pistol clips replace locomotion. Jump, climb, and stairs keep their own states.
        anim.SetBool(PistolHash, selectedPistol);
        if (HasSideParameter(anim))
            anim.SetFloat(PistolSideHash, pistolMirror);
    }

    private bool HasSideParameter(Animator anim)
    {
        if (sideParameterChecked == anim)
            return hasSideParameter;

        sideParameterChecked = anim;
        hasSideParameter = false;
        AnimatorControllerParameter[] parameters = anim.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == PistolSideHash)
                hasSideParameter = true;
        }

        return hasSideParameter;
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

    private void SeatSearchlight()
    {
        if (searchPoseWeight <= 0.001f || heldSearchlight == null || !heldSearchlight.activeInHierarchy)
            return;

        Animator anim = FindAnimator();
        if (anim == null || !anim.isHuman)
            return;

        CurlFist(anim, searchPoseWeight);
        AlignHeld(heldSearchlight.transform);
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

        Transform rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand == null)
            return;

        // Past the middle of the side blend the mirrored clips have the gun arm on the left.
        Transform leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        bool leftHanded = pistolMirror >= 0.5f && leftHand != null && CacheRestHands(anim);
        Transform hand = leftHanded ? leftHand : rightHand;

        Transform gun = heldPistol.transform;
        if (gun.parent != hand)
            gun.SetParent(hand, false);
        // The hand bone is scaled with the avatar. Keep the prefab's own size, and never mirror it:
        // a negative scale on this skinned mesh inverts the view and blows the bounds.
        FitScaleToBone(gun, hand, pistolAuthoredScale);
        AlignHeld(gun);
        if (leftHanded)
            MirrorHoldToLeftHand(gun);

        gunRecoil = Mathf.Lerp(gunRecoil, 0f, 1f - Mathf.Exp(-16f * Time.deltaTime));
        Vector3 barrel = gun.localRotation * Vector3.forward;
        if (barrel.sqrMagnitude > 0.0001f)
            gun.localPosition -= barrel.normalized * (0.05f * gunRecoil);
    }

    // AlignHeld seats the gun for the right hand. Reflect that hold across the body's
    // middle plane (avatar X) and express it in the left hand's rest frame. The pistol is
    // symmetric, so a reflected forward and up give the same look as a mirrored mesh.
    private void MirrorHoldToLeftHand(Transform gun)
    {
        Quaternion inRoot = restRightHand * gun.localRotation;
        Vector3 forward = MirrorX(inRoot * Vector3.forward);
        Vector3 up = MirrorX(inRoot * Vector3.up);
        Quaternion leftInverse = Quaternion.Inverse(restLeftHand);
        gun.localRotation = leftInverse * Quaternion.LookRotation(forward, up);
        gun.localPosition = leftInverse * MirrorX(restRightHand * gun.localPosition);
    }

    private static Vector3 MirrorX(Vector3 v)
    {
        return new Vector3(-v.x, v.y, v.z);
    }

    // Root-relative hand rotations in the avatar's rest pose, which is left/right symmetric.
    private bool CacheRestHands(Animator anim)
    {
        if (restHandsReady)
            return true;
        if (anim.avatar == null || !anim.avatar.isHuman)
            return false;

        SkeletonBone[] skeleton = anim.avatar.humanDescription.skeleton;
        if (skeleton == null || skeleton.Length == 0)
            return false;

        restRightHand = RestRotation(anim, anim.GetBoneTransform(HumanBodyBones.RightHand), skeleton);
        restLeftHand = RestRotation(anim, anim.GetBoneTransform(HumanBodyBones.LeftHand), skeleton);
        restHandsReady = true;
        return true;
    }

    private static Quaternion RestRotation(Animator anim, Transform bone, SkeletonBone[] skeleton)
    {
        Quaternion rotation = Quaternion.identity;
        for (Transform cursor = bone; cursor != null && cursor != anim.transform; cursor = cursor.parent)
        {
            Quaternion local = cursor.localRotation;
            for (int i = 0; i < skeleton.Length; i++)
            {
                if (skeleton[i].name == cursor.name)
                {
                    local = skeleton[i].rotation;
                    break;
                }
            }

            rotation = local * rotation;
        }

        return rotation;
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

    private Animator FindAnimator()
    {
        if (animator != null)
            return animator;

        animator = GetComponentInChildren<Animator>(true);
        WeaponHandIk.Ensure(animator);
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
        AlignHeld(heldTorch.transform);
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
        AlignHeld(heldSearchlight.transform);
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
        FitScaleToBone(heldPistol.transform, hand, pistolAuthoredScale);
        AlignHeld(heldPistol.transform);
        DisableColliders(heldPistol);
        SilenceImportedSceneObjects(heldPistol);
    }

    private static void AlignHeld(Transform item)
    {
        WeaponGrip grip = item.GetComponent<WeaponGrip>();
        if (grip == null)
        {
            Debug.LogError("[Inventory] " + item.name + " has no WeaponGrip. The hold point lives on the prefab.");
            return;
        }

        grip.AlignToParentOrigin();
    }

    private static void FitScaleToBone(Transform item, Transform bone, Vector3 authoredScale)
    {
        Vector3 boneScale = bone.lossyScale;
        item.localScale = new Vector3(
            authoredScale.x / Mathf.Max(0.0001f, Mathf.Abs(boneScale.x)),
            authoredScale.y / Mathf.Max(0.0001f, Mathf.Abs(boneScale.y)),
            authoredScale.z / Mathf.Max(0.0001f, Mathf.Abs(boneScale.z)));
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

        // The imported gun clip would fight the slide kick.
        Animator[] imported = item.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < imported.Length; i++)
            imported[i].enabled = false;
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
        gunRecoil = 0f;
        restHandsReady = false;
        sideParameterChecked = null;
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

        Attach(heldTorch, hand, false, Vector3.one);
        Attach(heldSearchlight, hand, false, Vector3.one);
        Attach(heldPistol, pistolHand, true, pistolAuthoredScale);
    }

    private void Park(GameObject item)
    {
        if (item == null)
            return;

        item.transform.SetParent(transform, false);
    }

    private void Attach(GameObject item, Transform hand, bool fitBoneScale, Vector3 authoredScale)
    {
        if (item == null || hand == null)
            return;

        bool active = item.activeSelf;
        item.transform.SetParent(hand, false);
        if (fitBoneScale)
            FitScaleToBone(item.transform, hand, authoredScale);
        AlignHeld(item.transform);
        if (item.activeSelf != active)
            item.SetActive(active);
    }

    private Transform FindCarryHand()
    {
        // The torch walk carries with the left hand. The right arm swings free.
        return FindHumanBone(HumanBodyBones.LeftHand, "LeftHand", ref carryHand);
    }

    private Transform FindGripHand()
    {
        return FindHumanBone(HumanBodyBones.RightHand, "RightHand", ref gripHand);
    }

    private Transform FindHumanBone(HumanBodyBones bone, string suffix, ref Transform cache)
    {
        if (cache != null)
            return cache;

        Animator anim = FindAnimator();
        if (anim != null && anim.isHuman)
        {
            cache = anim.GetBoneTransform(bone);
            if (cache != null)
                return cache;
        }

        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            if (!bones[i].name.EndsWith(suffix))
                continue;
            cache = bones[i];
            return cache;
        }

        return null;
    }
}
