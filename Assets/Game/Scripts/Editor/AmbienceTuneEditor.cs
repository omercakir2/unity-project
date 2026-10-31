using UnityEditor;
using UnityEngine;

// Sliders for every sound. Writes the same asset the game reads while playing.
public class SoundLevelsWindow : EditorWindow
{
    private static readonly string[] Properties =
    {
        "themeVolume",
        "rainVolume",
        "footstepVolume",
        "walkBreathVolume",
        "runBreathVolume",
        "zombieCrawlVolume",
        "zombieScreamVolume",
        "wtfVolume",
        "ghostStingVolume",
        "torchVolume",
        "pistolShotVolume",
        "pistolTailVolume",
    };

    private SerializedObject settingsObject;

    [MenuItem("Tools/Sound Levels")]
    public static void Open()
    {
        GetWindow<SoundLevelsWindow>("Sound Levels");
    }

    private void OnEnable()
    {
        settingsObject = null;
    }

    private void OnGUI()
    {
        AmbienceSettings settings = AmbienceSettings.Load();
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Missing Assets/Game/Resources/AmbienceSettings.asset.", MessageType.Warning);
            return;
        }

        if (settingsObject == null || settingsObject.targetObject != settings)
            settingsObject = new SerializedObject(settings);

        EditorGUILayout.HelpBox(
            "Saved on Ambience Settings. Change these while playing. The values stay after you stop. 1 is full loudness.",
            MessageType.Info);

        settingsObject.Update();
        for (int i = 0; i < Properties.Length; i++)
        {
            SerializedProperty property = settingsObject.FindProperty(Properties[i]);
            if (property == null)
                continue;

            // AudioSource cannot play louder than 1. Values above that all sound identical.
            float current = Mathf.Clamp01(property.floatValue);
            float next = EditorGUILayout.Slider(property.displayName, current, 0f, 1f);
            if (!Mathf.Approximately(property.floatValue, next))
                property.floatValue = next;
        }

        settingsObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(MainThemeMusic))]
public class MainThemeMusicEditor : AmbienceTuneEditor
{
}

[CustomEditor(typeof(EnvironmentRain))]
public class EnvironmentRainEditor : AmbienceTuneEditor
{
}

// Draws the saved ambience asset on the live theme and rain objects.
public class AmbienceTuneEditor : Editor
{
    private Editor settingsEditor;

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        if (settingsEditor != null)
            DestroyImmediate(settingsEditor);
    }

    public override void OnInspectorGUI()
    {
        AmbienceSettings settings = AmbienceSettings.Load();
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Missing Assets/Game/Resources/AmbienceSettings.asset.", MessageType.Warning);
            return;
        }

        if (settingsEditor == null || settingsEditor.target != settings)
            Rebuild();

        EditorGUILayout.HelpBox(
            "Saved on Ambience Settings. Change these while playing. The values stay after you stop. 1 is full loudness.",
            MessageType.Info);

        if (settingsEditor != null)
            settingsEditor.OnInspectorGUI();
    }

    private void Rebuild()
    {
        if (settingsEditor != null)
            DestroyImmediate(settingsEditor);

        AmbienceSettings settings = AmbienceSettings.Load();
        if (settings != null)
            settingsEditor = CreateEditor(settings);
    }
}
