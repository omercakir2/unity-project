using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupStrafeLocomotion
{
    private const string RemyPath = "Assets/Game/Models/Characters/Remy.fbx";
    private const string IdlePath = "Assets/Game/Animations/Player/Male@Breathing Idle.fbx";
    private const string TiredPath = "Assets/Game/Animations/Player/Remy@Tired-Idle.fbx";
    private const string WalkPath = "Assets/Game/Animations/Player/Male@Walking.fbx";
    private const string RunPath = "Assets/Game/Animations/Player/Remy@Fast Run.fbx";
    private const string BackPath = "Assets/Game/Animations/Player/Remy@Walking Backwards.fbx";
    private const string LeftPath = "Assets/Game/Animations/Player/Remy@Left Strafe Walk.fbx";
    private const string RightPath = "Assets/Game/Animations/Player/Remy@Right Strafe Walk.fbx";
    private const string ControllerPath = "Assets/Game/Animations/Controllers/MaleAnimator.controller";
    private const string IdleTreeName = "Idle Tired";

    [MenuItem("Tools/Setup Strafe Locomotion")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        Avatar avatar = FindHumanAvatar();
        if (avatar == null)
            return "strafe-anim: missing-avatar";

        AnimationClip back = ConfigureClip(BackPath, avatar, "Walk Back");
        AnimationClip left = ConfigureClip(LeftPath, avatar, "Strafe Left");
        AnimationClip right = ConfigureClip(RightPath, avatar, "Strafe Right");
        AnimationClip idle = LoadClip(IdlePath);
        AnimationClip tired = LoadClip(TiredPath);
        AnimationClip walk = LoadClip(WalkPath);
        AnimationClip run = ConfigureClip(RunPath, avatar, "Fast Run");
        if (back == null || left == null || right == null || idle == null || tired == null || walk == null || run == null)
        {
            return "strafe-anim: missing-clip"
                + " back=" + (back != null)
                + " left=" + (left != null)
                + " right=" + (right != null)
                + " idle=" + (idle != null)
                + " tired=" + (tired != null)
                + " walk=" + (walk != null)
                + " run=" + (run != null);
        }

        string wired = WireController(idle, tired, walk, run, back, left, right);
        AssetDatabase.SaveAssets();
        return "strafe-anim: back=" + back.length.ToString("0.00")
            + "s left=" + left.length.ToString("0.00")
            + "s right=" + right.length.ToString("0.00")
            + "s " + wired;
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
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as ModelImporter;
        }

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
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return null;
            clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;
        }

        if (clips == null || clips.Length == 0)
            return null;

        // Match the in-place forward walk. Lateral travel stays root motion and is
        // ignored, because the controller moves the body. Baking that travel into
        // the pose would scoot the mesh off the capsule and pop every loop.
        ModelImporterClipAnimation clip = clips[0];
        clip.name = clipName;
        clip.loopTime = true;
        clip.loopPose = true;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = false;
        clip.lockRootPositionXZ = false;
        clip.keepOriginalOrientation = false;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = false;
        clip.heightFromFeet = false;
        clip.maskType = ClipAnimationMaskType.None;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return LoadClip(path);
    }

    private static AnimationClip LoadClip(string path)
    {
        Object[] clipAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < clipAssets.Length; i++)
        {
            if (clipAssets[i] is AnimationClip candidate && !candidate.name.StartsWith("__"))
                return candidate;
        }

        return null;
    }

    private static string WireController(
        AnimationClip idle,
        AnimationClip tired,
        AnimationClip walk,
        AnimationClip run,
        AnimationClip back,
        AnimationClip left,
        AnimationClip right)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return "missing-controller";

        EnsureFloat(controller, "MoveX", 0f);
        EnsureFloat(controller, "MoveY", 0f);
        EnsureFloat(controller, "LocomotionRate", 1f);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = FindState(machine, "Blend Tree");
        if (locomotion == null)
            return "missing-state";

        BlendTree tree = locomotion.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree { name = "Blend Tree", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, controller);
            locomotion.motion = tree;
        }

        BlendTree idleTree = FindBlend(IdleTreeName);
        if (idleTree == null)
        {
            idleTree = new BlendTree { name = IdleTreeName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(idleTree, controller);
        }

        idleTree.blendType = BlendTreeType.Simple1D;
        idleTree.blendParameter = "Tired";
        idleTree.useAutomaticThresholds = false;
        ClearChildren(idleTree);
        idleTree.AddChild(idle, 0f);
        idleTree.AddChild(tired, 1f);
        idleTree.minThreshold = 0f;
        idleTree.maxThreshold = 1f;

        // Directional keeps diagonals on the walk ring. Cartesian pulled idle into
        // those poses, so the feet slowed down while the body kept full speed.
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";
        tree.useAutomaticThresholds = false;
        ClearChildren(tree);
        tree.AddChild(idleTree, new Vector2(0f, 0f));
        tree.AddChild(walk, new Vector2(0f, 0.5f));
        tree.AddChild(run, new Vector2(0f, 1f));
        tree.AddChild(back, new Vector2(0f, -0.5f));
        tree.AddChild(left, new Vector2(-0.5f, 0f));
        tree.AddChild(right, new Vector2(0.5f, 0f));
        locomotion.motion = tree;
        locomotion.iKOnFeet = true;
        locomotion.speed = 1f;
        locomotion.speedParameterActive = false;
        locomotion.speedParameter = "";

        EditorUtility.SetDirty(idleTree);
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        return "wired";
    }

    private static void ClearChildren(BlendTree tree)
    {
        while (tree.children != null && tree.children.Length > 0)
            tree.RemoveChild(0);
    }

    private static BlendTree FindBlend(string name)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is BlendTree tree && tree.name == name)
                return tree;
        }

        return null;
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
}
