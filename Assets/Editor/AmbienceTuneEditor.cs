using UnityEditor;
using UnityEngine;

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
            EditorGUILayout.HelpBox("Missing Assets/Resources/AmbienceSettings.asset.", MessageType.Warning);
            return;
        }

        if (settingsEditor == null || settingsEditor.target != settings)
            Rebuild();

        EditorGUILayout.HelpBox(
            "Saved on Ambience Settings. Change these while playing. The values stay after you stop.",
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
