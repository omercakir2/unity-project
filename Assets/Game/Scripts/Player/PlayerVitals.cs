using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(50)]
public class PlayerVitals : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float health = 100f;
    [SerializeField] private float healthRegenIdlePerSecond = 3f;
    [SerializeField] private float healthRegenWalkPerSecond = 1.25f;

    [Header("Energy")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energy = 100f;
    [SerializeField] private float sprintDrainPerSecond = 20f;
    [SerializeField] private float energyRegenIdlePerSecond = 4f;
    [SerializeField] private float energyRegenWalkPerSecond = 2f;
    [SerializeField] private float jumpEnergyCostMin = 20f;
    [SerializeField] private float jumpEnergyCostMax = 25f;
    [SerializeField, Range(0.05f, 0.9f)] private float lowEnergyFraction = 0.2f;

    public event Action Changed;
    public event Action Died;

    private PlayerController player;
    private PlayerInput input;
    private bool frozeTime;

    public float Health => health;
    public float MaxHealth => maxHealth;
    public float Energy => energy;
    public float MaxEnergy => maxEnergy;
    public bool IsDead { get; private set; }

    public bool IsEnergyLow => maxEnergy <= 0.001f || energy / maxEnergy < lowEnergyFraction;

    private float EnergyRegenPerSecond => player != null && player.IsMoving
        ? energyRegenWalkPerSecond
        : energyRegenIdlePerSecond;

    private float HealthRegenPerSecond => player != null && player.IsMoving
        ? healthRegenWalkPerSecond
        : healthRegenIdlePerSecond;
    public bool CanSprint => !IsDead && !IsEnergyLow;
    public bool CanJump => !IsDead && !IsEnergyLow;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        input = GetComponent<PlayerInput>();
        health = Mathf.Clamp(health, 0f, Mathf.Max(0.001f, maxHealth));
        energy = Mathf.Clamp(energy, 0f, Mathf.Max(0.001f, maxEnergy));
    }

    private void Update()
    {
        if (IsDead || player == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        bool changed = false;
        if (player.IsSprintingNow)
            changed |= AddEnergy(-sprintDrainPerSecond * dt);
        else
            changed |= AddEnergy(EnergyRegenPerSecond * dt);

        bool canRecover = player.IsGrounded && !player.IsSprintingNow && !player.IsTraversing;
        if (canRecover)
            changed |= AddHealth(HealthRegenPerSecond * dt);

        if (changed)
            Changed?.Invoke();
    }

    public void SpendJumpEnergy()
    {
        if (IsDead)
            return;

        float min = Mathf.Min(jumpEnergyCostMin, jumpEnergyCostMax);
        float max = Mathf.Max(jumpEnergyCostMin, jumpEnergyCostMax);
        if (AddEnergy(-UnityEngine.Random.Range(min, max)))
            Changed?.Invoke();
    }

    public void ApplyDamage(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        AddHealth(-amount);
        if (health <= 0f)
            Die();
        else
            Changed?.Invoke();
    }

    public void RestartScene()
    {
        if (frozeTime)
        {
            GameTime.SetPaused(this, false);
            frozeTime = false;
        }

        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    [ContextMenu("Debug Kill")]
    private void DebugKill()
    {
        if (!Application.isPlaying)
            return;

        ApplyDamage(Mathf.Max(health, maxHealth));
    }

    [ContextMenu("Debug Drain Energy")]
    private void DebugDrainEnergy()
    {
        if (!Application.isPlaying || IsDead)
            return;

        energy = maxEnergy * lowEnergyFraction * 0.5f;
        Changed?.Invoke();
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        health = 0f;
        GameTime.SetPaused(this, true);
        frozeTime = true;

        if (input != null)
            input.DeactivateInput();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Changed?.Invoke();
        Died?.Invoke();
    }

    private void OnDisable()
    {
        if (!frozeTime)
            return;

        GameTime.SetPaused(this, false);
        frozeTime = false;
    }

    private bool AddHealth(float delta)
    {
        float next = Mathf.Clamp(health + delta, 0f, maxHealth);
        if (Mathf.Approximately(next, health))
            return false;

        health = next;
        return true;
    }

    private bool AddEnergy(float delta)
    {
        float next = Mathf.Clamp(energy + delta, 0f, maxEnergy);
        if (Mathf.Approximately(next, energy))
            return false;

        energy = next;
        return true;
    }
}
