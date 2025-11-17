using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Inversion Settings")]
    public bool invertHorizontal;
    public bool invertVertical;

    // PlayerPrefs keys
    private const string PrefInvertH = "InvertHorizontal";
    private const string PrefInvertV = "InvertVertical";
    private const string PrefInputBindings = "InputBindings";

    // Public property for other scripts to access
    public InputActionAsset InputActions => inputActions;

    // Input action references (cached for performance)
    private InputAction lookAction;
    private InputAction moveAction;

    public InputSystem_Actions controls;

    void Awake()
    {

        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("=== InputManager Awake ===");

        controls = new InputSystem_Actions();

        // Check if inputActions is assigned
        if (inputActions == null)
        {
            Debug.LogError("InputActionAsset not assigned in InputManager!");
            return;
        }

        Debug.Log($"InputActionAsset found: {inputActions.name}");

        // Load saved inversion settings
        invertHorizontal = PlayerPrefs.GetInt(PrefInvertH, 0) == 1;
        invertVertical = PlayerPrefs.GetInt(PrefInvertV, 0) == 1;
        Debug.Log($"Inversion loaded: H={invertHorizontal}, V={invertVertical}");

        // IMPORTANT: Load bindings on startup
        LoadBindings();

        // Cache action references AFTER loading bindings
        lookAction = inputActions.FindAction("Look");
        moveAction = inputActions.FindAction("Move");

        // Subscribe to scene changes to reload bindings
        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log("=== InputManager Awake Complete ===");
    }

    private void OnDestroy()
    {
        // Unsubscribe when destroyed
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"=== Scene Loaded: {scene.name} ===");

        // Reload bindings whenever a new scene loads
        LoadBindings();

        Debug.Log("Bindings reloaded for new scene");
    }

    private void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Enable();
            Debug.Log($"InputActions enabled in scene: {SceneManager.GetActiveScene().name}");
        }
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }

    // ============================================
    // INVERSION METHODS
    // ============================================

    public void ToggleInvertVertical()
    {
        invertVertical = !invertVertical;
        PlayerPrefs.SetInt(PrefInvertV, invertVertical ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetInvertHorizontal(bool value)
    {
        invertHorizontal = value;
        PlayerPrefs.SetInt(PrefInvertH, invertHorizontal ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetInvertVertical(bool value)
    {
        invertVertical = value;
        PlayerPrefs.SetInt(PrefInvertV, invertVertical ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Get look input with inversion applied
    /// </summary>
    public Vector2 GetLookInput()
    {
        if (lookAction == null) return Vector2.zero;

        Vector2 look = lookAction.ReadValue<Vector2>();
        if (invertHorizontal) look.x = -look.x;
        if (invertVertical) look.y = -look.y;
        return look;
    }

    /// <summary>
    /// Get move input (doesn't need inversion typically, but available)
    /// </summary>
    public Vector2 GetMoveInput()
    {
        if (moveAction == null) return Vector2.zero;
        return moveAction.ReadValue<Vector2>();
    }

    // ============================================
    // REBINDING METHODS
    // ============================================

    /// <summary>
    /// Start interactive rebinding for a specific action
    /// </summary>
    public void StartRebind(string actionName, int bindingIndex, Action<bool> onComplete)
    {
        InputAction action = inputActions.FindAction(actionName);
        if (action == null)
        {
            Debug.LogError($"Action '{actionName}' not found!");
            onComplete?.Invoke(false);
            return;
        }

        // Disable the action while rebinding
        action.Disable();

        var rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithControlsExcluding("Keyboard")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation =>
            {
                action.Enable();
                SaveBindings();

                Debug.Log($"✓ Rebound {actionName} to {action.bindings[bindingIndex].effectivePath}");

                onComplete?.Invoke(true);
                operation.Dispose();
            })
            .OnCancel(operation =>
            {
                action.Enable();
                Debug.Log($"✗ Rebind cancelled for {actionName}");
                onComplete?.Invoke(false);
                operation.Dispose();
            });

        rebindOperation.Start();
    }

    /// <summary>
    /// Save all binding overrides to PlayerPrefs
    /// </summary>
    public void SaveBindings()
    {
        string rebinds = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(PrefInputBindings, rebinds);
        PlayerPrefs.Save();

        Debug.Log($"✓ Input bindings saved to PlayerPrefs");
        Debug.Log($"Saved data: {rebinds}");
    }

    /// <summary>
    /// Load binding overrides from PlayerPrefs
    /// </summary>
    public void LoadBindings()
    {
        Debug.Log("→ LoadBindings() called");

        string rebinds = PlayerPrefs.GetString(PrefInputBindings, string.Empty);

        if (!string.IsNullOrEmpty(rebinds))
        {
            inputActions.LoadBindingOverridesFromJson(rebinds);
            Debug.Log("✓ Input bindings loaded from PlayerPrefs");
            Debug.Log($"Loaded data: {rebinds}");

            // DEBUG: Verify a specific action
            InputAction attackAction = inputActions.FindAction("Attack");
            if (attackAction != null && attackAction.bindings.Count > 0)
            {
                Debug.Log($"Attack now bound to: {attackAction.bindings[0].effectivePath}");
            }
        }
        else
        {
            Debug.LogWarning("⚠ No saved bindings found in PlayerPrefs, using defaults");
        }
    }

    /// <summary>
    /// Reset all bindings to defaults
    /// </summary>
    public void ResetBindings()
    {
        foreach (InputActionMap map in inputActions.actionMaps)
        {
            map.RemoveAllBindingOverrides();
        }
        PlayerPrefs.DeleteKey(PrefInputBindings);
        SaveBindings();
        Debug.Log("✓ Input bindings reset to defaults");
    }

    /// <summary>
    /// Reset a specific action to default
    /// </summary>
    public void ResetAction(string actionName)
    {
        InputAction action = inputActions.FindAction(actionName);
        if (action != null)
        {
            action.RemoveAllBindingOverrides();
            SaveBindings();
            Debug.Log($"✓ Reset {actionName} to default");
        }
    }

    /// <summary>
    /// Get the current binding path for an action
    /// </summary>
    public string GetBindingName(string actionName, int bindingIndex)
    {
        InputAction action = inputActions.FindAction(actionName);
        if (action == null || bindingIndex >= action.bindings.Count)
        {
            return "None";
        }

        return action.bindings[bindingIndex].effectivePath;
    }

    /// <summary>
    /// Get human-readable binding name
    /// </summary>
    public string GetReadableBindingName(string actionName, int bindingIndex)
    {
        string bindingPath = GetBindingName(actionName, bindingIndex);
        return InputControlPath.ToHumanReadableString(
            bindingPath,
            InputControlPath.HumanReadableStringOptions.OmitDevice
        );
    }

    /// <summary>
    /// Check if an action has any overrides
    /// </summary>
    public bool HasBindingOverride(string actionName)
    {
        InputAction action = inputActions.FindAction(actionName);
        if (action == null) return false;

        foreach (var binding in action.bindings)
        {
            if (!string.IsNullOrEmpty(binding.overridePath))
            {
                return true;
            }
        }
        return false;
    }
}