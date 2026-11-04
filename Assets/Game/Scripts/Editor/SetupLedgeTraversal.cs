using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Imports the vault and jump-to-hang clips as humanoid and adds their states to the base layer.
/// "Hang Up" hands over to the existing "Climb Over" (Braced Hang To Crouch) state for tall ledges.
/// PlayerController cross-fades into both states from code, so they need no triggers.
/// </summary>
public static class SetupLedgeTraversal
{
    private const string VaultPath = "Assets/Game/Animations/Player/Ch15_nonPBR@Vault Over Box.fbx";
    private const string HangPath = "Assets/Game/Animations/Player/Ch15_nonPBR@Idle To Braced Hang.fbx";
    private const string LandPath = "Assets/Game/Animations/Player/Ch15_nonPBR@Hard Landing.fbx";
    private const string ControllerPath = "Assets/Game/Animations/Controllers/MaleAnimator.controller";
    private const string VaultStateName = "Vault Over";
    private const string HangStateName = "Hang Up";
    private const string LandStateName = "Hard Landing";
    private const string ClimbStateName = "Climb Over";
    private const string LocomotionStateName = "Blend Tree";

    [MenuItem("Tools/Setup Ledge Traversal")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        AnimationClip vault = ConfigureClip(VaultPath, VaultStateName, false);
        AnimationClip hang = ConfigureClip(HangPath, HangStateName, true);
        AnimationClip land = ConfigureClip(LandPath, LandStateName, false);
        if (vault == null || hang == null || land == null)
            return "ledge-traversal: missing-clip vault=" + (vault != null) + " hang=" + (hang != null) + " land=" + (land != null);

        string wired = WireController(vault, hang, land);
        AssetDatabase.SaveAssets();
        return "ledge-traversal: vault=" + vault.length.ToString("0.00") + "s hang=" + hang.length.ToString("0.00") + "s land=" + land.length.ToString("0.00") + "s " + wired;
    }

    private static AnimationClip ConfigureClip(string path, string clipName, bool bakeTravel)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return null;

        // Same as the throw clip: the Ch15 rig gets its own avatar, which retargets onto Remy.
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.clipAnimations = new ModelImporterClipAnimation[0];
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return null;

        // The hang is baked like "Climb Over": the body travels inside the clip and PlayerController
        // warps the transform from the bones. The vault and the hard landing travel a metre or more,
        // so they play in place and PlayerController moves the transform itself; otherwise the body
        // would snap back when the state blends out.
        ModelImporterClipAnimation clip = clips[0];
        clip.name = clipName;
        clip.loopTime = false;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = bakeTravel;
        clip.keepOriginalOrientation = false;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = false;
        clip.maskType = ClipAnimationMaskType.None;
        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip candidate && !candidate.name.StartsWith("__preview__"))
                return candidate;
        }

        return null;
    }

    private static string WireController(AnimationClip vault, AnimationClip hang, AnimationClip land)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return "missing-controller";

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = FindState(machine, LocomotionStateName);
        AnimatorState climb = FindState(machine, ClimbStateName);
        if (locomotion == null || climb == null)
            return "missing-base-states";

        AnimatorState vaultState = FindState(machine, VaultStateName) ?? machine.AddState(VaultStateName, new Vector3(520f, 260f, 0f));
        AnimatorState hangState = FindState(machine, HangStateName) ?? machine.AddState(HangStateName, new Vector3(520f, 330f, 0f));
        AnimatorState landState = FindState(machine, LandStateName) ?? machine.AddState(LandStateName, new Vector3(520f, 400f, 0f));
        Prepare(vaultState, vault);
        Prepare(hangState, hang);
        Prepare(landState, land);

        ClearTransitions(vaultState);
        AnimatorStateTransition vaultOut = vaultState.AddTransition(locomotion);
        vaultOut.hasExitTime = true;
        // The clip stands idle for its last half.
        vaultOut.exitTime = 0.5f;
        vaultOut.duration = 0.3f;
        vaultOut.hasFixedDuration = true;

        // The hang clip ends where Braced Hang To Crouch starts, so the hand-off is short.
        ClearTransitions(hangState);
        AnimatorStateTransition hangOut = hangState.AddTransition(climb);
        hangOut.hasExitTime = true;
        hangOut.exitTime = 0.97f;
        hangOut.duration = 0.1f;
        hangOut.hasFixedDuration = true;

        // Climb Over drops off the far side of the fence top with this clip.
        ClearTransitions(landState);
        AnimatorStateTransition landOut = landState.AddTransition(locomotion);
        landOut.hasExitTime = true;
        landOut.exitTime = 0.92f;
        landOut.duration = 0.3f;
        landOut.hasFixedDuration = true;

        EditorUtility.SetDirty(controller);
        return "states=" + VaultStateName + "," + HangStateName + "," + LandStateName;
    }

    private static void Prepare(AnimatorState state, AnimationClip clip)
    {
        state.motion = clip;
        state.tag = "Action";
        state.speed = 1f;
        state.writeDefaultValues = true;
        state.iKOnFeet = false;
    }

    private static void ClearTransitions(AnimatorState state)
    {
        AnimatorStateTransition[] transitions = state.transitions;
        for (int i = 0; i < transitions.Length; i++)
            state.RemoveTransition(transitions[i]);
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state.name == name)
                return states[i].state;
        }

        return null;
    }
}
