using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Turns the player's gameplay actions off while any owner asks for it (inventory, cinematics).
/// Disabling an action cancels it, so movement and sprint drop to zero on their own.
/// </summary>
public static class GameplayInput
{
    private static readonly string[] BlockedActions =
    {
        "Move", "Sprint", "Look", "Jump", "Interact",
        "Hotbar1", "Hotbar2", "Hotbar3", "Hotbar4", "HotbarScroll",
        "Attack", "Aim", "SwapShoulder"
    };

    private static readonly HashSet<object> blockers = new HashSet<object>();

    public static bool IsBlocked => blockers.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        blockers.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            blockers.Clear();
    }

    public static void SetBlocked(object owner, bool blocked)
    {
        if (owner == null)
            return;

        bool wasBlocked = IsBlocked;
        if (blocked)
            blockers.Add(owner);
        else
            blockers.Remove(owner);

        if (wasBlocked != IsBlocked)
            Apply(IsBlocked);
    }

    private static void Apply(bool blocked)
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        PlayerInput playerInput = player != null ? player.GetComponent<PlayerInput>() : null;
        if (playerInput == null || playerInput.actions == null)
            return;

        // Death deactivates the whole map. Never switch actions back on behind it.
        if (!blocked && !playerInput.inputIsActive)
            return;

        for (int i = 0; i < BlockedActions.Length; i++)
        {
            InputAction action = playerInput.actions.FindAction(BlockedActions[i], false);
            if (action == null)
                continue;

            if (blocked)
                action.Disable();
            else
                action.Enable();
        }
    }
}
