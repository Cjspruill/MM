using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug script to identify why player input isn't working after cutscene.
/// Attach to player to diagnose the issue.
/// </summary>
public class InputDebugger : MonoBehaviour
{
    [Header("Check These Components")]
    [SerializeField] private MonoBehaviour[] scriptsToCheck;

    [Header("Input System")]
    [SerializeField] private PlayerInput playerInput; // If using PlayerInput component

    private void Update()
    {
        // Press F1 to show detailed status
        if (Input.GetKeyDown(KeyCode.F1) || Keyboard.current?.f1Key.wasPressedThisFrame == true)
        {
            ShowDetailedStatus();
        }

        // Press F2 to re-enable everything
        if (Input.GetKeyDown(KeyCode.F2) || Keyboard.current?.f2Key.wasPressedThisFrame == true)
        {
            ForceEnableEverything();
        }

        // Show real-time input status
        if (Input.GetKeyDown(KeyCode.F3) || Keyboard.current?.f3Key.wasPressedThisFrame == true)
        {
            ShowInputStatus();
        }
    }

    private void ShowDetailedStatus()
    {
        Debug.Log("========== DETAILED COMPONENT STATUS ==========");

        // Check all MonoBehaviours on this GameObject
        MonoBehaviour[] allScripts = GetComponents<MonoBehaviour>();
        Debug.Log($"Total scripts on {gameObject.name}: {allScripts.Length}");

        foreach (MonoBehaviour script in allScripts)
        {
            if (script == null) continue;

            string status = script.enabled ? "✅ ENABLED" : "❌ DISABLED";
            Debug.Log($"  {script.GetType().Name}: {status}");
        }

        // Check PlayerInput component
        if (playerInput != null)
        {
            Debug.Log($"\nPlayerInput Component:");
            Debug.Log($"  Enabled: {playerInput.enabled}");
            Debug.Log($"  Active: {playerInput.gameObject.activeInHierarchy}");
            Debug.Log($"  Current Action Map: {playerInput.currentActionMap?.name ?? "NULL"}");
            Debug.Log($"  Actions Enabled: {playerInput.currentActionMap?.enabled ?? false}");
        }
        else
        {
            PlayerInput foundInput = GetComponent<PlayerInput>();
            if (foundInput != null)
            {
                Debug.LogWarning("⚠️ PlayerInput component found but not assigned to InputDebugger!");
                playerInput = foundInput;
            }
            else
            {
                Debug.Log("No PlayerInput component found (might be using direct Input System)");
            }
        }

        // Check if GameObject is active
        Debug.Log($"\nGameObject Active: {gameObject.activeInHierarchy}");

        // Check Time.timeScale
        Debug.Log($"Time.timeScale: {Time.timeScale} (should be 1)");

        Debug.Log("==============================================\n");
    }

    private void ShowInputStatus()
    {
        Debug.Log("========== INPUT STATUS CHECK ==========");

        // Old Input System
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        Debug.Log("Old Input System:");
        Debug.Log($"  Horizontal: {h}");
        Debug.Log($"  Vertical: {v}");
        Debug.Log($"  Mouse X: {mouseX}");
        Debug.Log($"  Mouse Y: {mouseY}");

        // New Input System (if available)
        if (Keyboard.current != null)
        {
            Debug.Log("\nNew Input System (Keyboard):");
            Debug.Log($"  W: {Keyboard.current.wKey.isPressed}");
            Debug.Log($"  A: {Keyboard.current.aKey.isPressed}");
            Debug.Log($"  S: {Keyboard.current.sKey.isPressed}");
            Debug.Log($"  D: {Keyboard.current.dKey.isPressed}");
        }

        if (Mouse.current != null)
        {
            Debug.Log("\nNew Input System (Mouse):");
            Debug.Log($"  Delta: {Mouse.current.delta.ReadValue()}");
            Debug.Log($"  Left Button: {Mouse.current.leftButton.isPressed}");
        }

        // Cursor state
        Debug.Log($"\nCursor State:");
        Debug.Log($"  Lock State: {Cursor.lockState}");
        Debug.Log($"  Visible: {Cursor.visible}");

        Debug.Log("======================================\n");
    }

    private void ForceEnableEverything()
    {
        Debug.Log("🔧 FORCING ALL COMPONENTS ENABLED...");

        // Enable all MonoBehaviours
        MonoBehaviour[] allScripts = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in allScripts)
        {
            if (script != null && script != this)
            {
                script.enabled = true;
                Debug.Log($"  ✅ Enabled: {script.GetType().Name}");
            }
        }

        // Enable PlayerInput if exists
        if (playerInput != null)
        {
            playerInput.enabled = true;
            playerInput.ActivateInput();
            Debug.Log("  ✅ Enabled and activated PlayerInput");
        }

        // Ensure GameObject is active
        gameObject.SetActive(true);

        // Reset time scale
        Time.timeScale = 1f;

        // Lock cursor if needed
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("🔧 Force enable complete!");
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 12;
        style.alignment = TextAnchor.UpperLeft;
        style.normal.textColor = Color.yellow;

        string info = "INPUT DEBUGGER\n\n";
        info += "[F1] Show Detailed Status\n";
        info += "[F2] Force Enable Everything\n";
        info += "[F3] Show Input Status\n";

        GUI.Box(new Rect(Screen.width - 250, 10, 240, 100), info, style);
    }
}