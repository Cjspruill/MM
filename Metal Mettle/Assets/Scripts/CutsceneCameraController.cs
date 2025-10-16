using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Modular cutscene camera controller that can be triggered from any script.
/// Supports orbit, pan, zoom, and custom transforms.
/// Smoothly transitions to/from player camera control.
/// </summary>
public class CutsceneCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform playerCameraRig; // Your existing camera rig

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 1f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Player Control")]
    [SerializeField] private MonoBehaviour playerCameraScript; // Reference to your camera control script
    [SerializeField] private PlayerController playerMovementScript; // Optional: player movement script
    [SerializeField] private bool disablePlayerInput = true; // Also disable PlayerInput component
    [SerializeField] private bool pauseTime = false; // Optionally pause time during cutscene

    private PlayerInput playerInput; // Cache PlayerInput if it exists
    private bool wasPlayerInputEnabled = false; // Track original state

    // State tracking
    private bool isInCutscene = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Coroutine activeCoroutine;

    // Singleton pattern for easy access
    public static CutsceneCameraController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (playerCameraRig == null)
            playerCameraRig = mainCamera.transform.parent;

        // Try to find PlayerInput component
        if (disablePlayerInput)
        {
            playerInput = FindFirstObjectByType<PlayerInput>();
            if (playerInput != null)
            {
                Debug.Log($"Found PlayerInput component on {playerInput.gameObject.name}");
            }
        }

        // Validation
        if (playerCameraScript == null)
        {
            Debug.LogError("CRITICAL: Player Camera Script not assigned in CutsceneCameraController! Player controls will not work correctly during cutscenes. Please assign your camera control script in the inspector.");
        }

        Debug.Log($"CutsceneCameraController initialized. Camera: {mainCamera?.name}, Rig: {playerCameraRig?.name}, Control Script: {playerCameraScript?.GetType().Name ?? "NOT ASSIGNED"}");
    }

    #region Public API

    /// <summary>
    /// Orbit around a target point
    /// </summary>
    public void PlayOrbitCutscene(Transform target, float radius, float angle, float duration, System.Action onComplete = null)
    {
        if (isInCutscene) return;

        Vector3 offset = Quaternion.Euler(0, angle, 0) * (Vector3.back * radius);
        Vector3 targetPosition = target.position + offset;

        activeCoroutine = StartCoroutine(CutsceneSequence(
            targetPosition,
            Quaternion.LookRotation(target.position - targetPosition),
            duration,
            onComplete
        ));
    }

    /// <summary>
    /// Pan to look at a specific point
    /// </summary>
    public void PlayPanCutscene(Vector3 targetPosition, float duration, System.Action onComplete = null)
    {
        if (isInCutscene) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetPosition - mainCamera.transform.position);

        activeCoroutine = StartCoroutine(CutsceneSequence(
            mainCamera.transform.position,
            targetRotation,
            duration,
            onComplete
        ));
    }

    /// <summary>
    /// Zoom toward a target
    /// </summary>
    public void PlayZoomCutscene(Transform target, float zoomDistance, float duration, System.Action onComplete = null)
    {
        if (isInCutscene) return;

        Vector3 direction = (target.position - mainCamera.transform.position).normalized;
        Vector3 targetPosition = target.position - (direction * zoomDistance);
        Quaternion targetRotation = Quaternion.LookRotation(target.position - targetPosition);

        activeCoroutine = StartCoroutine(CutsceneSequence(
            targetPosition,
            targetRotation,
            duration,
            onComplete
        ));
    }

    /// <summary>
    /// Move to a specific transform (most flexible option)
    /// </summary>
    public void PlayCustomCutscene(Transform targetTransform, float duration, System.Action onComplete = null)
    {
        if (isInCutscene) return;

        activeCoroutine = StartCoroutine(CutsceneSequence(
            targetTransform.position,
            targetTransform.rotation,
            duration,
            onComplete
        ));
    }

    /// <summary>
    /// Move to specific position and rotation
    /// </summary>
    public void PlayCustomCutscene(Vector3 targetPosition, Quaternion targetRotation, float duration, System.Action onComplete = null)
    {
        if (isInCutscene) return;

        activeCoroutine = StartCoroutine(CutsceneSequence(
            targetPosition,
            targetRotation,
            duration,
            onComplete
        ));
    }

    /// <summary>
    /// Stop current cutscene and return to player control immediately
    /// </summary>
    public void StopCutscene()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        // Force immediate return to original position
        if (isInCutscene)
        {
            mainCamera.transform.position = originalPosition;
            mainCamera.transform.rotation = originalRotation;
        }

        ReturnToPlayerControl();
    }

    #endregion

    #region Core Cutscene Logic

    private IEnumerator CutsceneSequence(Vector3 targetPosition, Quaternion targetRotation, float duration, System.Action onComplete)
    {
        // Store original camera state
        originalPosition = mainCamera.transform.position;
        originalRotation = mainCamera.transform.rotation;

        // Disable player camera control
        DisablePlayerControl();
        isInCutscene = true;

        Debug.Log($"🎬 Cutscene started - Duration: {duration}s");

        // Transition to cutscene camera
        yield return StartCoroutine(TransitionToTarget(targetPosition, targetRotation));

        Debug.Log($"🎬 Holding on target for {duration}s");

        // Hold on target for duration
        yield return new WaitForSeconds(duration);

        Debug.Log("🎬 Transitioning back to player");

        // Transition back to original position
        yield return StartCoroutine(TransitionToOriginal());

        // Re-enable player control
        ReturnToPlayerControl();
        isInCutscene = false;
        activeCoroutine = null;

        Debug.Log("🎬 Cutscene complete - Player controls restored");

        // Invoke callback if provided
        onComplete?.Invoke();
    }

    private IEnumerator TransitionToTarget(Vector3 targetPosition, Quaternion targetRotation)
    {
        float elapsed = 0f;
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);

            mainCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            yield return null;
        }

        mainCamera.transform.position = targetPosition;
        mainCamera.transform.rotation = targetRotation;
    }

    private IEnumerator TransitionToOriginal()
    {
        float elapsed = 0f;
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);

            mainCamera.transform.position = Vector3.Lerp(startPosition, originalPosition, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRotation, originalRotation, t);

            yield return null;
        }

        mainCamera.transform.position = originalPosition;
        mainCamera.transform.rotation = originalRotation;
    }

    #endregion

    #region Player Control Management

    private void DisablePlayerControl()
    {
        Debug.Log("🔒 Disabling player controls for cutscene...");

        // Try interface method first (preferred)
        if (playerCameraScript is ICutsceneControllable cameraControllable)
        {
            cameraControllable.OnCutsceneStart();
            Debug.Log($"  ✓ Called OnCutsceneStart on {playerCameraScript.GetType().Name}");
        }

        if (playerMovementScript is ICutsceneControllable movementControllable)
        {
            movementControllable.OnCutsceneStart();
            Debug.Log($"  ✓ Called OnCutsceneStart on {playerMovementScript.GetType().Name}");
        }

        // Disable your player camera script
        if (playerCameraScript != null)
        {
            playerCameraScript.enabled = false;
            Debug.Log($"  ✓ Disabled camera script: {playerCameraScript.GetType().Name}");
        }
        else
        {
            Debug.LogWarning("  ⚠️ Player camera script not assigned!");
        }

        // Disable player movement script if assigned
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
            Debug.Log($"  ✓ Disabled movement script: {playerMovementScript.GetType().Name}");
        }

        // Disable PlayerInput component if found
        if (disablePlayerInput && playerInput != null)
        {
            wasPlayerInputEnabled = playerInput.enabled;
            playerInput.DeactivateInput();
            Debug.Log($"  ✓ PlayerInput deactivated on {playerInput.gameObject.name}");
        }

        // Optionally pause time
        if (pauseTime)
        {
            Time.timeScale = 0f;
            Debug.Log("  ✓ Time paused");
        }

        Debug.Log("🔒 Player controls disabled");
    }

    private void ReturnToPlayerControl()
    {
        Debug.Log("🔓 Restoring player controls...");

        // Reset cutscene state
        isInCutscene = false;

        // Re-enable your player camera script
        if (playerCameraScript != null)
        {
            playerCameraScript.enabled = true;
            Debug.Log($"  ✓ Re-enabled camera script: {playerCameraScript.GetType().Name}");
        }
        else
        {
            Debug.LogWarning("  ⚠️ Player camera script not assigned!");
        }

        // Re-enable player movement script if assigned
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
            Debug.Log($"  ✓ Re-enabled movement script: {playerMovementScript.GetType().Name}");
        }

        // Try interface method (preferred for Input System)
        if (playerCameraScript is ICutsceneControllable cameraControllable)
        {
            cameraControllable.OnCutsceneEnd();
            Debug.Log($"  ✓ Called OnCutsceneEnd on {playerCameraScript.GetType().Name}");
        }

        if (playerMovementScript is ICutsceneControllable movementControllable)
        {
            movementControllable.OnCutsceneEnd();
            Debug.Log($"  ✓ Called OnCutsceneEnd on {playerMovementScript.GetType().Name}");
        }

        // Re-activate PlayerInput component if it was disabled
        if (disablePlayerInput && playerInput != null)
        {
            if (wasPlayerInputEnabled)
            {
                playerInput.ActivateInput();
                Debug.Log($"  ✓ PlayerInput re-activated on {playerInput.gameObject.name}");
            }
        }

        // Resume time if it was paused
        if (pauseTime)
        {
            Time.timeScale = 1f;
            Debug.Log("  ✓ Time resumed");
        }

        Debug.Log("🔓 Player controls restored");
    }

    #endregion

    #region Utility Properties

    public bool IsInCutscene => isInCutscene;

    #endregion
}