using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Title screen. The first scene loads in the background while the menu is up, so Play
/// only fades out and swaps. A pod readout types itself out in the corner.
/// </summary>
[DisallowMultipleComponent]
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string firstScene = "Assets/Game/Scenes/Facility_WakeUp.unity";
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private Image fade;
    [SerializeField] private float fadeInTime = 2f;
    [SerializeField] private float menuDelay = 0.8f;

    [Header("Pod Readout")]
    [SerializeField] private TMP_Text readout;
    [SerializeField, TextArea(3, 8)] private string readoutText =
        "CRYO-POD 07  //  SUBJECT: UNKNOWN\nSTATUS: SEALED\nSLEEP CYCLE: 36,524 DAYS\nLIFE SUPPORT: 3% RESERVE";
    [SerializeField] private float typeInterval = 0.035f;

    private AsyncOperation preloaded;
    private bool leaving;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playButton != null)
            playButton.onClick.AddListener(Play);
        if (quitButton != null)
            quitButton.onClick.AddListener(Quit);

        preloaded = SceneTransition.Preload(firstScene);
        StartCoroutine(Intro());
    }

    private void Update()
    {
        // Keep keyboard and gamepad users on a button once the mouse wanders off.
        EventSystem events = EventSystem.current;
        if (!leaving && events != null && events.currentSelectedGameObject == null && menuGroup != null && menuGroup.interactable && playButton != null)
            events.SetSelectedGameObject(playButton.gameObject);
    }

    private IEnumerator Intro()
    {
        if (menuGroup != null)
        {
            menuGroup.alpha = 0f;
            menuGroup.interactable = false;
        }

        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            elapsed += Time.unscaledDeltaTime;
            if (fade != null)
                fade.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, elapsed / fadeInTime));
            if (menuGroup != null)
                menuGroup.alpha = Mathf.Clamp01((elapsed - menuDelay) / (fadeInTime - menuDelay));
            yield return null;
        }

        if (fade != null)
            fade.color = Color.clear;
        if (menuGroup != null)
        {
            menuGroup.alpha = 1f;
            menuGroup.interactable = true;
        }

        if (readout != null)
            StartCoroutine(TypeReadout());
    }

    private IEnumerator TypeReadout()
    {
        readout.text = readoutText;
        readout.maxVisibleCharacters = 0;
        for (int i = 1; i <= readoutText.Length; i++)
        {
            readout.maxVisibleCharacters = i;
            char c = readoutText[i - 1];
            yield return new WaitForSecondsRealtime(c == '\n' ? typeInterval * 8f : typeInterval);
        }
    }

    private void Play()
    {
        if (leaving)
            return;

        leaving = true;
        if (menuGroup != null)
            menuGroup.interactable = false;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (SceneTransition.Go(preloaded, firstScene, null))
            preloaded = null;
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
