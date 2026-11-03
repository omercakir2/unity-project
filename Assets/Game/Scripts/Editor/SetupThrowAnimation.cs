using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Imports the throw clip as humanoid and adds an upper-body "Throw" layer
/// that LightBallThrow plays and weights from code.
/// </summary>
public static class SetupThrowAnimation
{
    private const string ThrowPath = "Assets/Game/Animations/Player/Ch15_nonPBR@Throwing.fbx";
    private const string MaskPath = "Assets/Game/Animations/Player/HoldUpperBody.mask";
    private const string ControllerPath = "Assets/Game/Animations/Controllers/MaleAnimator.controller";
    private const string LayerName = "Throw";
    private const string EmptyStateName = "Empty";
    private const string ThrowStateName = "Throw";

    [MenuItem("Tools/Setup Throw Animation")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        AnimationClip clip = ConfigureClip();
        if (clip == null)
            return "throw-anim: missing-clip";

        string wired = WireController(clip);
        AssetDatabase.SaveAssets();
        return "throw-anim: clip=" + clip.name + " " + clip.length.ToString("0.00") + "s " + wired;
    }

    private static AnimationClip ConfigureClip()
    {
        var importer = AssetImporter.GetAtPath(ThrowPath) as ModelImporter;
        if (importer == null)
            return null;

        // The Ch15 rig has no eye bones, so Remy's avatar cannot be copied onto it.
        // Its own humanoid avatar retargets onto Remy just the same.
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

        ModelImporterClipAnimation clip = clips[0];
        clip.name = ThrowStateName;
        clip.loopTime = false;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        clip.maskType = ClipAnimationMaskType.None;
        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ThrowPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip candidate && !candidate.name.StartsWith("__preview__"))
                return candidate;
        }

        return null;
    }

    private static string WireController(AnimationClip clip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return "missing-controller";

        AnimatorControllerLayer[] layers = controller.layers;
        int index = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == LayerName)
                index = i;
        }

        if (index < 0)
        {
            controller.AddLayer(LayerName);
            layers = controller.layers;
            index = layers.Length - 1;
        }

        AnimatorControllerLayer layer = layers[index];
        layer.avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.defaultWeight = 0f;

        AnimatorStateMachine machine = layer.stateMachine;
        AnimatorState empty = FindState(machine, EmptyStateName) ?? machine.AddState(EmptyStateName, new Vector3(250f, 0f, 0f));
        AnimatorState throwState = FindState(machine, ThrowStateName) ?? machine.AddState(ThrowStateName, new Vector3(250f, 90f, 0f));
        empty.motion = null;
        throwState.motion = clip;
        throwState.writeDefaultValues = true;
        machine.defaultState = empty;

        if (throwState.transitions.Length == 0)
        {
            AnimatorStateTransition back = throwState.AddTransition(empty);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.05f;
        }

        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        return "layer=" + LayerName + " index=" + index;
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
