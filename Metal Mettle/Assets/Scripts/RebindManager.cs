using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class RebindManager : MonoBehaviour
{
    public static RebindManager Instance { get; private set; }

    [SerializeField] private InputActionAsset inputActions;

    // Add this public property so other scripts can access it
    public InputActionAsset InputActions => inputActions;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadBindings();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Enable();
        }
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }

    // Rest of your existing code...

    public void StartRebind(string actionName, int bindingIndex, Action<bool> onComplete)
    {
        InputAction action = inputActions.FindAction(actionName);
        if (action == null)
        {
            Debug.LogError($"Action '{actionName}' not found!");
            onComplete?.Invoke(false);
            return;
        }

        action.Disable();

        var rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithControlsExcluding("Keyboard")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation =>
            {
                action.Enable();
                SaveBindings();
                Debug.Log($"Rebound {actionName} to {action.bindings[bindingIndex].effectivePath}");
                onComplete?.Invoke(true);
                operation.Dispose();
            })
            .OnCancel(operation =>
            {
                action.Enable();
                Debug.Log($"Rebind cancelled for {actionName}");
                onComplete?.Invoke(false);
                operation.Dispose();
            });

        rebindOperation.Start();
    }

    public void SaveBindings()
    {
        string rebinds = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("InputBindings", rebinds);
        PlayerPrefs.Save();
        Debug.Log("Input bindings saved");
    }

    public void LoadBindings()
    {
        string rebinds = PlayerPrefs.GetString("InputBindings", string.Empty);
        if (!string.IsNullOrEmpty(rebinds))
        {
            inputActions.LoadBindingOverridesFromJson(rebinds);
            Debug.Log("Input bindings loaded");
        }
        else
        {
            Debug.Log("No saved bindings found, using defaults");
        }
    }

    public void ResetBindings()
    {
        foreach (InputActionMap map in inputActions.actionMaps)
        {
            map.RemoveAllBindingOverrides();
        }
        PlayerPrefs.DeleteKey("InputBindings");
        SaveBindings();
        Debug.Log("Input bindings reset to defaults");
    }

    public void ResetAction(string actionName)
    {
        InputAction action = inputActions.FindAction(actionName);
        if (action != null)
        {
            action.RemoveAllBindingOverrides();
            SaveBindings();
            Debug.Log($"Reset {actionName} to default");
        }
    }

    public string GetBindingName(string actionName, int bindingIndex)
    {
        InputAction action = inputActions.FindAction(actionName);
        if (action == null || bindingIndex >= action.bindings.Count)
        {
            return "None";
        }

        return action.bindings[bindingIndex].effectivePath;
    }

    public string GetReadableBindingName(string actionName, int bindingIndex)
    {
        string bindingPath = GetBindingName(actionName, bindingIndex);
        return InputControlPath.ToHumanReadableString(
            bindingPath,
            InputControlPath.HumanReadableStringOptions.OmitDevice
        );
    }

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