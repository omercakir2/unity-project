using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupPistolLocomotion
{
    private const string RemyPath = "Assets/Game/Models/Characters/Remy.fbx";
    private const string IdlePath = "Assets/Game/Animations/Player/Remy@Pistol Idle.fbx";
    private const string WalkPath = "Assets/Game/Animations/Player/Remy@Pistol Walk.fbx";
    private const string ControllerPath = "Assets/Game/Animations/Controllers/MaleAnimator.controller";
    private const string PistolStateName = "Pistol Locomotion";
    private const string SpeedTreeName = "Pistol Locomotion Right";
    private const string MirroredTreeName = "Pistol Locomotion Left";
    private const string SideParameter = "PistolSide";

    [MenuItem("Tools/Setup Pistol Locomotion")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        Avatar avatar = FindHumanAvatar();
        if (avatar == null)
            return "pistol-anim: missing-avatar";

        AnimationClip idle = ConfigureClip(IdlePath, avatar, "Pistol Idle");
        AnimationClip walk = ConfigureClip(WalkPath, avatar, "Pistol Walk");
        if (idle == null || walk == null)
            return "pistol-anim: missing-clip idle=" + (idle != null) + " walk=" + (walk != null);

        string wired = WireController(idle, walk);
        AssetDatabase.SaveAssets();
        return "pistol-anim: idle=" + idle.name + " " + idle.length.ToString("0.00")
            + "s walk=" + walk.name + " " + walk.length.ToString("0.00") + "s " + wired;
    }

    private static Avatar FindHumanAvatar()
    {
        Object[] remyAssets = AssetDatabase.LoadAllAssetsAtPath(RemyPath);
        for (int i = 0; i < remyAssets.Length; i++)
        {
            if (remyAssets[i] is Avatar candidate && candidate.isHuman)
                return candidate;
        }

        return null;
    }

    private static AnimationClip ConfigureClip(string path, Avatar avatar, string clipName)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return null;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            return null;

        ModelImporterClipAnimation clip = clips[0];
        clip.name = clipName;
        clip.loopTime = true;
        clip.loopPose = true;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = false;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = false;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = false;
        clip.heightFromFeet = false;
        clip.maskType = ClipAnimationMaskType.None;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        Object[] clipAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < clipAssets.Length; i++)
        {
            if (clipAssets[i] is AnimationClip candidate && !candidate.name.StartsWith("__"))
                return candidate;
        }

        return null;
    }

    private static string WireController(AnimationClip idle, AnimationClip walk)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return "missing-controller";

        EnsureFloat(controller, "PistolRate", 1f);
        EnsureBool(controller, "Pistol");

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = FindState(machine, "Blend Tree");
        AnimatorState walkUp = FindState(machine, "Walk Up Stairs");
        AnimatorState runUp = FindState(machine, "Run Up Stairs");
        AnimatorState descend = FindState(machine, "Descend Stairs");
        if (locomotion == null || walkUp == null || runUp == null || descend == null)
            return "missing-state";

        AnimatorState pistol = FindState(machine, PistolStateName);
        if (pistol == null)
            pistol = machine.AddState(PistolStateName, new Vector3(80f, 260f, 0f));

        BlendTree tree = SpeedTree(pistol.motion as BlendTree);
        if (tree == null)
        {
            tree = new BlendTree();
            AssetDatabase.AddObjectToAsset(tree, controller);
        }

        tree.name = SpeedTreeName;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.children = new ChildMotion[0];
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 0.5f);
        tree.minThreshold = 0f;
        tree.maxThreshold = 0.5f;
        // Keep an existing PistolSide wrapper. WireMirror rebuilds its mirrored half.
        if (!(pistol.motion is BlendTree wrapper && wrapper.blendParameter == SideParameter))
            pistol.motion = tree;
        WireMirror(controller, pistol);
        pistol.iKOnFeet = true;
        pistol.writeDefaultValues = true;
        pistol.speed = 1f;
        pistol.speedParameterActive = false;
        pistol.speedParameter = "";

        EnsureTransition(locomotion, pistol, 0.25f, AnimatorConditionMode.If, 0f, "Pistol");
        EnsureTransition(pistol, locomotion, 0.25f, AnimatorConditionMode.IfNot, 0f, "Pistol");
        EnsureStairTransition(pistol, walkUp, false);
        EnsureStairTransition(pistol, runUp, true);
        EnsureDescendTransition(pistol, descend);

        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        return "wired";
    }

    // The state's own tree once WireMirror has wrapped it, else the tree itself.
    private static BlendTree SpeedTree(BlendTree stateTree)
    {
        if (stateTree == null || stateTree.blendParameter != SideParameter)
            return stateTree;

        ChildMotion[] children = stateTree.children;
        return children.Length > 0 ? children[0].motion as BlendTree : null;
    }

    [MenuItem("Tools/Setup Pistol Mirror")]
    public static void WireMirrorFromMenu()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorState pistol = controller != null ? FindState(controller.layers[0].stateMachine, PistolStateName) : null;
        if (pistol == null)
        {
            Debug.LogError("pistol-mirror: missing " + PistolStateName);
            return;
        }

        WireMirror(controller, pistol);
        AssetDatabase.SaveAssets();
        Debug.Log("pistol-mirror: wired");
    }

    /// <summary>
    /// Wraps the speed tree in a PistolSide blend: 0 is the authored right-handed hold,
    /// 1 is the same clips mirrored by the humanoid rig, so the gun sits in the left hand.
    /// </summary>
    private static void WireMirror(AnimatorController controller, AnimatorState pistol)
    {
        EnsureFloat(controller, SideParameter, 0f);

        BlendTree speed = SpeedTree(pistol.motion as BlendTree);
        if (speed == null)
            return;
        speed.name = SpeedTreeName;

        BlendTree side = pistol.motion as BlendTree;
        BlendTree mirrored = null;
        if (side != null && side.blendParameter == SideParameter)
        {
            ChildMotion[] old = side.children;
            if (old.Length > 1)
                mirrored = old[1].motion as BlendTree;
        }
        else
        {
            side = new BlendTree();
            AssetDatabase.AddObjectToAsset(side, controller);
        }

        if (mirrored == null)
        {
            mirrored = new BlendTree();
            AssetDatabase.AddObjectToAsset(mirrored, controller);
        }

        mirrored.name = MirroredTreeName;
        mirrored.blendType = speed.blendType;
        mirrored.blendParameter = speed.blendParameter;
        mirrored.useAutomaticThresholds = false;
        ChildMotion[] copies = speed.children;
        for (int i = 0; i < copies.Length; i++)
            copies[i].mirror = true;
        mirrored.children = copies;
        mirrored.minThreshold = speed.minThreshold;
        mirrored.maxThreshold = speed.maxThreshold;

        side.name = PistolStateName;
        side.blendType = BlendTreeType.Simple1D;
        side.blendParameter = SideParameter;
        side.useAutomaticThresholds = false;
        side.children = new ChildMotion[0];
        side.AddChild(speed, 0f);
        side.AddChild(mirrored, 1f);
        side.minThreshold = 0f;
        side.maxThreshold = 1f;
        pistol.motion = side;

        EditorUtility.SetDirty(speed);
        EditorUtility.SetDirty(mirrored);
        EditorUtility.SetDirty(side);
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureFloat(AnimatorController controller, string name, float defaultValue)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != name)
                continue;
            parameters[i].defaultFloat = defaultValue;
            controller.parameters = parameters;
            return;
        }

        controller.AddParameter(name, AnimatorControllerParameterType.Float);
        parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != name)
                continue;
            parameters[i].defaultFloat = defaultValue;
            controller.parameters = parameters;
            return;
        }
    }

    private static void EnsureBool(AnimatorController controller, string name)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name)
                return;
        }

        controller.AddParameter(name, AnimatorControllerParameterType.Bool);
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == name)
                return states[i].state;
        }

        return null;
    }

    private static void EnsureTransition(
        AnimatorState source,
        AnimatorState destination,
        float duration,
        AnimatorConditionMode mode,
        float threshold,
        string parameter)
    {
        if (HasDestination(source, destination))
            return;

        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.exitTime = 0.9f;
        transition.offset = 0f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(mode, threshold, parameter);
    }

    private static void EnsureStairTransition(AnimatorState source, AnimatorState destination, bool sprinting)
    {
        if (HasDestination(source, destination))
            return;

        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.3f;
        transition.exitTime = 0.9f;
        transition.offset = 0f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "OnStairs");
        transition.AddCondition(AnimatorConditionMode.Greater, 0.2f, "StairDirection");
        transition.AddCondition(sprinting ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsSprinting");
    }

    private static void EnsureDescendTransition(AnimatorState source, AnimatorState destination)
    {
        if (HasDestination(source, destination))
            return;

        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.3f;
        transition.exitTime = 0.9f;
        transition.offset = 0f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "OnStairs");
        transition.AddCondition(AnimatorConditionMode.Less, -0.2f, "StairDirection");
    }

    private static bool HasDestination(AnimatorState source, AnimatorState destination)
    {
        AnimatorStateTransition[] transitions = source.transitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            if (transitions[i].destinationState == destination)
                return true;
        }

        return false;
    }
}
