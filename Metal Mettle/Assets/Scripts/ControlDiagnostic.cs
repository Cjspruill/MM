using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Emergency diagnostic and fix script for stuck controls.
/// Attach to player and press P to force re-enable everything.
/// </summary>
public class ControlDiagnostic : MonoBehaviour
{
    private void Update()
    {
        // Press P to diagnose and force fix
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            DiagnoseAndFix();
        }
    }

    private void DiagnoseAndFix()
    {
        Debug.Log("========== CONTROL DIAGNOSTIC ==========");

        // Find all relevant components
        PlayerController playerController = FindObjectOfType<PlayerController>();
        CustomFreeLookCamera cameraController = FindObjectOfType<CustomFreeLookCamera>();
        PlayerInput playerInput = FindObjectOfType<PlayerInput>();
        CutsceneCameraController cutsceneController = FindObjectOfType<CutsceneCameraController>();
        OpeningCutscene openingCutscene = FindObjectOfType<OpeningCutscene>();

        // Check PlayerController
        if (playerController != null)
        {
            Debug.Log($"PlayerController: {(playerController.enabled ? "✅ ENABLED" : "❌ DISABLED")}");
            if (!playerController.enabled)
            {
                Debug.Log("  → Enabling PlayerController...");
                playerController.enabled = true;
            }
        }
        else
        {
            Debug.LogError("❌ PlayerController NOT FOUND!");
        }

        // Check Camera Controller
        if (cameraController != null)
        {
            Debug.Log($"CustomFreeLookCamera: {(cameraController.enabled ? "✅ ENABLED" : "❌ DISABLED")}");
            if (!cameraController.enabled)
            {
                Debug.Log("  → Enabling CustomFreeLookCamera...");
                cameraController.enabled = true;
            }
        }
        else
        {
            Debug.LogError("❌ CustomFreeLookCamera NOT FOUND!");
        }

        // Check PlayerInput
        if (playerInput != null)
        {
            Debug.Log($"PlayerInput: {(playerInput.enabled ? "✅ ENABLED" : "❌ DISABLED")}");
            Debug.Log($"  Current Action Map: {playerInput.currentActionMap?.name ?? "NULL"}");
            Debug.Log($"  Actions Enabled: {playerInput.currentActionMap?.enabled ?? false}");

            if (!playerInput.enabled)
            {
                Debug.Log("  → Enabling PlayerInput...");
                playerInput.enabled = true;
            }

            if (playerInput.currentActionMap != null && !playerInput.currentActionMap.enabled)
            {
                Debug.Log("  → Activating PlayerInput actions...");
                playerInput.ActivateInput();
            }
        }
        else
        {
            Debug.LogWarning("⚠️ PlayerInput component not found (might not be using it)");
        }

        // Check for cutscene scripts
        if (cutsceneController != null)
        {
            Debug.Log($"CutsceneCameraController found: isInCutscene = {cutsceneController.GetType().GetField("isInCutscene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(cutsceneController)}");
        }

        if (openingCutscene != null)
        {
            Debug.Log($"OpeningCutscene: {(openingCutscene.enabled ? "⚠️ STILL ENABLED" : "✅ Disabled")}");
            if (openingCutscene.enabled)
            {
                Debug.Log("  → Disabling OpeningCutscene script...");
                openingCutscene.enabled = false;
            }
        }

        // Force call OnCutsceneEnd on player scripts
        Debug.Log("\n🔧 Force calling OnCutsceneEnd()...");

        if (playerController != null)
        {
            playerController.OnCutsceneEnd();
            Debug.Log("  ✓ Called OnCutsceneEnd on PlayerController");
        }

        if (cameraController != null)
        {
            cameraController.OnCutsceneEnd();
            Debug.Log("  ✓ Called OnCutsceneEnd on CustomFreeLookCamera");
        }

        // Check InputManager
        if (InputManager.Instance != null)
        {
            Debug.Log($"InputManager.Instance.controls: {(InputManager.Instance.controls != null ? "✅ Found" : "❌ NULL")}");
            if (InputManager.Instance.controls != null)
            {
                Debug.Log("  → Enabling InputManager controls...");
                InputManager.Instance.controls.Enable();
            }
        }

        // Force cursor lock
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("🔒 Cursor locked");

        Debug.Log("\n========== FIX COMPLETE - TRY MOVING NOW ==========");
    }
}