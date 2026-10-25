using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupCinemachineCamera
{
    private const string CameraPrefabPath = "Assets/Prefabs/Main Camera.prefab";
    private const string CamerasPrefabPath = "Assets/Prefabs/Cinemachine Cameras.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string CamerasRootName = "Cinemachine Cameras";
    private const string RigName = "Third Person Aim Camera";
    private const string PistolRigName = "Pistol Aim Camera";
    private const string PivotName = "Third Person Aim";

    private static readonly string[] GameplayScenes =
    {
        "Assets/Scenes/IndustrialYard_Night.unity",
        "Assets/Scenes/IndustrialYard.unity",
        "Assets/Scenes/SampleScene.unity",
        "Assets/Scenes/Demo_01.unity",
        "Assets/Scenes/Demo_01_Night.unity",
        "Assets/Models/Pure Poly/Free Low Poly Nature Pack/Scenes/Demo_01.unity"
    };

    [MenuItem("Tools/Setup Cinemachine Camera")]
    public static void SetupFromMenu()
    {
        Debug.Log("[Cinemachine] " + Setup());
    }

    public static string Setup()
    {
        InstallPlayerPivot();
        InstallCameraRig();
        PlaceCamerasInScenes();
        return "Cinemachine cameras sit beside the brain, not under it.";
    }

    private static void InstallPlayerPivot()
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform pivot = player.transform.Find(PivotName);
            if (pivot == null)
            {
                GameObject pivotObject = new GameObject(PivotName);
                pivot = pivotObject.transform;
                pivot.SetParent(player.transform, false);
            }

            pivot.localPosition = new Vector3(0f, 0.35f, 0f);
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            if (pivot.GetComponent<CinemachineThirdPersonLook>() == null)
                pivot.gameObject.AddComponent<CinemachineThirdPersonLook>();

            Collider[] cols = player.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                cols[i].gameObject.tag = "Player";

            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    private static void InstallCameraRig()
    {
        float fieldOfView = 60f;
        float farClip = 1000f;
        GameObject cameraRoot = PrefabUtility.LoadPrefabContents(CameraPrefabPath);
        try
        {
            ThirdPersonCamera retired = cameraRoot.GetComponent<ThirdPersonCamera>();
            if (retired != null)
                Object.DestroyImmediate(retired);

            CinemachineBrain brain = cameraRoot.GetComponent<CinemachineBrain>();
            if (brain == null)
                brain = cameraRoot.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.4f);

            Camera output = cameraRoot.GetComponent<Camera>();
            if (output != null)
            {
                output.nearClipPlane = 0.15f;
                fieldOfView = output.fieldOfView;
                farClip = output.farClipPlane;
            }

            // A virtual camera parented to the brain is the setup error Cinemachine reports.
            RemoveVirtualCameras(cameraRoot);
            PrefabUtility.SaveAsPrefabAsset(cameraRoot, CameraPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(cameraRoot);
        }

        EnsureCamerasPrefab(fieldOfView, farClip);
    }

    private static void RemoveVirtualCameras(GameObject cameraRoot)
    {
        CinemachineCamera[] vcams = cameraRoot.GetComponentsInChildren<CinemachineCamera>(true);
        for (int i = 0; i < vcams.Length; i++)
        {
            if (vcams[i] != null)
                Object.DestroyImmediate(vcams[i].gameObject);
        }

        Transform oldRig = cameraRoot.transform.Find("CM Third Person");
        if (oldRig != null)
            Object.DestroyImmediate(oldRig.gameObject);
    }

    private static void EnsureCamerasPrefab(float fieldOfView, float farClip)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CamerasPrefabPath) == null)
        {
            GameObject temp = new GameObject(CamerasRootName);
            PrefabUtility.SaveAsPrefabAsset(temp, CamerasPrefabPath);
            Object.DestroyImmediate(temp);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CamerasPrefabPath);
        try
        {
            root.name = CamerasRootName;
            CinemachineBrain strayBrain = root.GetComponent<CinemachineBrain>();
            if (strayBrain != null)
                Object.DestroyImmediate(strayBrain);
            CinemachineCamera strayCamera = root.GetComponent<CinemachineCamera>();
            if (strayCamera != null)
                Object.DestroyImmediate(strayCamera);

            GameObject rig = EnsureRig(root.transform, RigName);
            CinemachineCamera vcam = rig.GetComponent<CinemachineCamera>();
            ApplyLens(vcam, fieldOfView, farClip);
            vcam.Priority = 10;
            vcam.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            ConfigureFollow(rig, new Vector3(0.55f, 0.05f, 0f), 0.2f, 3.3f);

            GameObject pistol = EnsureRig(root.transform, PistolRigName);
            CinemachineCamera pistolCamera = pistol.GetComponent<CinemachineCamera>();
            ApplyLens(pistolCamera, fieldOfView, farClip);
            pistolCamera.Priority = 0;
            pistolCamera.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            ConfigureFollow(pistol, new Vector3(1.15f, 0.02f, 0.15f), 0.05f, 2.9f);

            PrefabUtility.SaveAsPrefabAsset(root, CamerasPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject EnsureRig(Transform parent, string name)
    {
        Transform rigTransform = parent.Find(name);
        if (rigTransform == null)
        {
            GameObject rigObject = new GameObject(name);
            rigTransform = rigObject.transform;
            rigTransform.SetParent(parent, false);
        }

        GameObject rig = rigTransform.gameObject;
        if (rig.GetComponent<CinemachineCamera>() == null)
            rig.AddComponent<CinemachineCamera>();
        return rig;
    }

    private static void ApplyLens(CinemachineCamera vcam, float fieldOfView, float farClip)
    {
        if (vcam == null)
            return;

        LensSettings lens = vcam.Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Perspective;
        lens.FieldOfView = fieldOfView;
        lens.NearClipPlane = 0.15f;
        lens.FarClipPlane = farClip;
        vcam.Lens = lens;
    }

    private static void ConfigureFollow(GameObject rig, Vector3 shoulder, float armLength, float distance)
    {
        CinemachineThirdPersonFollow follow = rig.GetComponent<CinemachineThirdPersonFollow>();
        if (follow == null)
            follow = rig.AddComponent<CinemachineThirdPersonFollow>();
        follow.Damping = new Vector3(0.08f, 0.15f, 0.2f);
        follow.ShoulderOffset = shoulder;
        follow.CameraSide = 1f;
        follow.VerticalArmLength = armLength;
        follow.CameraDistance = distance;
        CinemachineThirdPersonFollow.ObstacleSettings avoid = follow.AvoidObstacles;
        avoid.Enabled = true;
        avoid.CollisionFilter = ~0;
        avoid.IgnoreTag = "Player";
        avoid.CameraRadius = 0.2f;
        avoid.DampingIntoCollision = 0f;
        avoid.DampingFromCollision = 0.25f;
        follow.AvoidObstacles = avoid;

        CinemachineThirdPersonAim aim = rig.GetComponent<CinemachineThirdPersonAim>();
        if (aim == null)
            aim = rig.AddComponent<CinemachineThirdPersonAim>();
        aim.AimCollisionFilter = ~0;
        aim.IgnoreTag = "Player";
        aim.AimDistance = 80f;
        aim.NoiseCancellation = true;

        CinemachineOrbitalFollow orbital = rig.GetComponent<CinemachineOrbitalFollow>();
        if (orbital != null)
            Object.DestroyImmediate(orbital);
        CinemachineHardLookAt hardLook = rig.GetComponent<CinemachineHardLookAt>();
        if (hardLook != null)
            Object.DestroyImmediate(hardLook);
        CinemachineDeoccluder deoccluder = rig.GetComponent<CinemachineDeoccluder>();
        if (deoccluder != null)
            Object.DestroyImmediate(deoccluder);
        CinemachineInputAxisController strayInput = rig.GetComponent<CinemachineInputAxisController>();
        if (strayInput != null)
            Object.DestroyImmediate(strayInput);
    }

    private static void PlaceCamerasInScenes()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CamerasPrefabPath);
        try
        {
            for (int i = 0; i < GameplayScenes.Length; i++)
            {
                string path = GameplayScenes[i];
                if (!File.Exists(path))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (Object.FindAnyObjectByType<CinemachineBrain>() == null)
                    continue;

                DetachCamerasFromBrains();
                if (GameObject.Find(CamerasRootName) == null && prefab != null)
                    PrefabUtility.InstantiatePrefab(prefab, scene);

                WireScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (previous != null && previous.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    private static void DetachCamerasFromBrains()
    {
        CinemachineCamera[] vcams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
        for (int i = 0; i < vcams.Length; i++)
        {
            CinemachineCamera vcam = vcams[i];
            if (vcam == null)
                continue;
            if (vcam.GetComponentInParent<CinemachineBrain>(true) == null)
                continue;
            Object.DestroyImmediate(vcam.gameObject);
        }
    }

    private static void WireScene(Scene scene)
    {
        CinemachineThirdPersonLook look = Object.FindAnyObjectByType<CinemachineThirdPersonLook>();
        if (look == null || look.gameObject.scene != scene)
            return;

        CinemachineThirdPersonFollow[] follows = Object.FindObjectsByType<CinemachineThirdPersonFollow>(FindObjectsInactive.Exclude);
        for (int i = 0; i < follows.Length; i++)
        {
            if (follows[i].gameObject.scene != scene)
                continue;
            CinemachineCamera vcam = follows[i].GetComponent<CinemachineCamera>();
            if (vcam == null)
                continue;
            vcam.Follow = look.transform;
            vcam.LookAt = look.transform;
        }
    }
}
