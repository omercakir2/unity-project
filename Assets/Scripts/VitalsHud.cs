using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class VitalsHud : MonoBehaviour
{
    private static readonly Color HealthColor = new Color(0.35f, 0.75f, 0.4f, 1f);
    private static readonly Color EnergyColor = new Color(0.95f, 0.72f, 0.25f, 1f);
    private static readonly Color LowEnergyColor = new Color(0.85f, 0.18f, 0.16f, 1f);

    [SerializeField] private PlayerVitals vitals;
    [SerializeField] private UnityEngine.UI.Image healthFill;
    [SerializeField] private UnityEngine.UI.Image energyFill;
    [SerializeField] private TMP_Text healthValue;
    [SerializeField] private TMP_Text energyValue;
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private UnityEngine.UI.Button restartButton;

    private void Awake()
    {
        if (vitals == null)
            vitals = GetComponentInParent<PlayerVitals>();

        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);

        EnsureEventSystem();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || vitals == null)
            return;

        vitals.Changed += Refresh;
        vitals.Died += ShowDeath;
        if (deathPanel != null)
            deathPanel.SetActive(vitals.IsDead);
        Refresh();
    }

    private void OnDisable()
    {
        if (vitals == null)
            return;

        vitals.Changed -= Refresh;
        vitals.Died -= ShowDeath;
    }

    private void Restart()
    {
        if (vitals != null)
            vitals.RestartScene();
    }

    private void ShowDeath()
    {
        if (deathPanel != null)
            deathPanel.SetActive(true);
        Refresh();
    }

    private void Refresh()
    {
        if (vitals == null)
            return;

        SetBar(healthFill, healthValue, vitals.Health, vitals.MaxHealth, HealthColor);
        Color energyColor = vitals.IsEnergyLow ? LowEnergyColor : EnergyColor;
        SetBar(energyFill, energyValue, vitals.Energy, vitals.MaxEnergy, energyColor);
    }

    private static void SetBar(UnityEngine.UI.Image fill, TMP_Text label, float current, float max, Color color)
    {
        if (fill != null)
        {
            fill.color = color;
            fill.fillAmount = max <= 0.001f ? 0f : Mathf.Clamp01(current / max);
        }

        if (label != null)
        {
            int shownCurrent = Mathf.CeilToInt(current);
            int shownMax = Mathf.CeilToInt(max);
            label.text = shownCurrent + "/" + shownMax;
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }
}
