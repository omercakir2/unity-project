using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Adds the memory cuts to the wake-up cinematic and lays out their timeline: three cuts during
/// the orbit, none over the capsule shudder, two while the glass opens.
/// </summary>
public static class SetupWakeUpMemories
{
    private const string ScenePath = "Assets/Game/Scenes/Facility_WakeUp.unity";
    private const string Folder = "Assets/Game/Cinematics/WakeUpMemories/";
    private const string WalkAlonePath = Folder + "Memory_WalkAlone.jpg";
    private const string CrowdPath = Folder + "Memory_Crowd.mp4";
    private const string SunsetPath = Folder + "Memory_Sunset.mp4";

    [MenuItem("Tools/Setup Wake Up Memories")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "memories: exit play mode first";

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        string result = "memories: 'Wake Up Cinematic' not found";
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<WakeUpCinematic>() == null)
                continue;

            AddTo(root);
            EditorSceneManager.SaveScene(scene);
            result = "memories: added to " + ScenePath;
            break;
        }

        if (openedHere)
            EditorSceneManager.CloseScene(scene, true);
        return result;
    }

    /// <summary>Adds or refreshes the component on the cinematic's object.</summary>
    public static void AddTo(GameObject director)
    {
        ConfigureStillImport();
        Texture walkAlone = AssetDatabase.LoadAssetAtPath<Texture>(WalkAlonePath);
        VideoClip crowd = AssetDatabase.LoadAssetAtPath<VideoClip>(CrowdPath);
        VideoClip sunset = AssetDatabase.LoadAssetAtPath<VideoClip>(SunsetPath);

        WakeUpMemories memories = director.GetComponent<WakeUpMemories>();
        if (memories == null)
            memories = director.AddComponent<WakeUpMemories>();

        var so = new SerializedObject(memories);
        SerializedProperty cuts = so.FindProperty("cuts");
        cuts.arraySize = 0;
        AddCut(cuts, walkAlone, null, 2.6f, 1.4f, 0f);
        AddCut(cuts, null, crowd, 5.6f, 1.2f, 0f);
        AddCut(cuts, null, sunset, 8.4f, 1.3f, 0f);
        AddCut(cuts, null, crowd, 13f, 1f, 2.5f);
        AddCut(cuts, null, sunset, 15.2f, 1.1f, 2.5f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddCut(SerializedProperty cuts, Texture image, VideoClip video, float start, float duration, float offset)
    {
        int index = cuts.arraySize;
        cuts.InsertArrayElementAtIndex(index);
        SerializedProperty cut = cuts.GetArrayElementAtIndex(index);
        cut.FindPropertyRelative("image").objectReferenceValue = image;
        cut.FindPropertyRelative("video").objectReferenceValue = video;
        cut.FindPropertyRelative("startTime").floatValue = start;
        cut.FindPropertyRelative("duration").floatValue = duration;
        cut.FindPropertyRelative("videoOffset").floatValue = offset;
    }

    // Shown full screen, so keep it sharp: no mips, no size rounding.
    private static void ConfigureStillImport()
    {
        TextureImporter importer = AssetImporter.GetAtPath(WalkAlonePath) as TextureImporter;
        if (importer == null || !importer.mipmapEnabled)
            return;

        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }
}
