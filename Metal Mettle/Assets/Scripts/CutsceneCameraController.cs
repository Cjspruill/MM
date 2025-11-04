using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Modular cutscene camera controller that can be triggered from any script.
/// Supports orbit, pan, zoom, and custom transforms.
/// Smoothly transitions to/from player camera control.
/// NOW BROADCASTS TO ALL ICutsceneControllable OBJECTS (including enemies)!
/// </summary>
public class CutsceneCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform playerCameraRig;

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 1f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Player Control")]
    [SerializeField] private MonoBehaviour playerCameraScript;
    [SerializeField] private PlayerController playerMovementScript;
    [SerializeField] private bool disablePlayerInput = true;
    [SerializeField] private bool pauseTime = false;

    [Header("Broadcasting")]
    [SerializeField] private bool broadcastToAllControllables = true;
    [SerializeField] private bool showBroadcastDebug = true;

    private PlayerInput playerInput;
    private bool wasPlayerInputEnabled = false;

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

        if (disablePlayerInput)
        {
            playerInput = FindFirstObjectByType<PlayerInput>();
            if (playerInput != null)
            {
                Debug.Log($"Found PlayerInput component on {playerInput.gameObject.name}");
            }
        }

        if (playerCameraScript == null)
        {
            Debug.LogError("CRITICAL: Player Camera Script not assigned in CutsceneCameraController! Player controls will not work correctly during cutscenes. Please assign your camera control script in the inspector.");
        }

        Debug.Log($"CutsceneCameraController initialized. Camera: {mainCamera?.name}, Rig: {playerCameraRig?.name}, Control Script: {playerCameraScript?.GetType().Name ?? "NOT ASSIGNED"}");
    }

    #region Public API

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

    public void StopCutscene()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

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
        originalPosition = mainCamera.transform.position;
        originalRotation = mainCamera.transform.rotation;

        DisablePlayerControl();
        isInCutscene = true;

        Debug.Log($"🎬 Cutscene started - Duration: {duration}s");

        yield return StartCoroutine(TransitionToTarget(targetPosition, targetRotation));

        Debug.Log($"🎬 Holding on target for {duration}s");

        yield return new WaitForSeconds(duration);

        Debug.Log("🎬 Transitioning back to player");

        yield return StartCoroutine(TransitionToOriginal());

        ReturnToPlayerControl();
        isInCutscene = false;
        activeCoroutine = null;

        Debug.Log("🎬 Cutscene complete - Player controls restored");

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

        // Call interface method on player scripts
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

        // Disable player scripts
        if (playerCameraScript != null)
        {
            playerCameraScript.enabled = false;
            Debug.Log($"  ✓ Disabled camera script: {playerCameraScript.GetType().Name}");
        }
        else
        {
            Debug.LogWarning("  ⚠️ Player camera script not assigned!");
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
            Debug.Log($"  ✓ Disabled movement script: {playerMovementScript.GetType().Name}");
        }

        // Disable PlayerInput component
        if (disablePlayerInput && playerInput != null)
        {
            wasPlayerInputEnabled = playerInput.enabled;
            playerInput.DeactivateInput();
            Debug.Log($"  ✓ PlayerInput deactivated on {playerInput.gameObject.name}");
        }

        // 🚨 NEW: Broadcast to ALL ICutsceneControllable objects (including enemies!)
        if (broadcastToAllControllables)
        {
            BroadcastCutsceneStart();
        }

        // Pause time if needed
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

        isInCutscene = false;

        // Re-enable player scripts
        if (playerCameraScript != null)
        {
            playerCameraScript.enabled = true;
            Debug.Log($"  ✓ Re-enabled camera script: {playerCameraScript.GetType().Name}");
        }
        else
        {
            Debug.LogWarning("  ⚠️ Player camera script not assigned!");
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
            Debug.Log($"  ✓ Re-enabled movement script: {playerMovementScript.GetType().Name}");
        }

        // Call interface method on player scripts
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

        // Re-activate PlayerInput
        if (disablePlayerInput && playerInput != null)
        {
            if (wasPlayerInputEnabled)
            {
                playerInput.ActivateInput();
                Debug.Log($"  ✓ PlayerInput re-activated on {playerInput.gameObject.name}");
            }
        }

        // 🚨 NEW: Broadcast to ALL ICutsceneControllable objects (including enemies!)
        if (broadcastToAllControllables)
        {
            BroadcastCutsceneEnd();
        }

        // Resume time if needed
        if (pauseTime)
        {
            Time.timeScale = 1f;
            Debug.Log("  ✓ Time resumed");
        }

        Debug.Log("🔓 Player controls restored");
    }

    #endregion

    #region Broadcasting System

    /// <summary>
    /// Broadcasts OnCutsceneStart to ALL ICutsceneControllable objects in the scene
    /// This includes enemies, NPCs, interactive objects, etc.
    /// </summary>
    private void BroadcastCutsceneStart()
    {
        // Find ALL MonoBehaviours that implement ICutsceneControllable
        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int broadcastCount = 0;

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 Broadcasting OnCutsceneStart to all ICutsceneControllable objects...");
        }

        foreach (MonoBehaviour behaviour in allBehaviours)
        {
            if (behaviour == null) continue;

            // Skip the player scripts (already handled separately)
            if (behaviour == playerCameraScript || behaviour == playerMovementScript)
                continue;

            // Check if it implements ICutsceneControllable
            if (behaviour is ICutsceneControllable controllable)
            {
                controllable.OnCutsceneStart();
                broadcastCount++;

                if (showBroadcastDebug)
                {
                    Debug.Log($"  📡 Sent OnCutsceneStart to: {behaviour.gameObject.name} ({behaviour.GetType().Name})");
                }
            }
        }

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 Broadcast complete: {broadcastCount} objects notified");
        }
    }

    /// <summary>
    /// Broadcasts OnCutsceneEnd to ALL ICutsceneControllable objects in the scene
    /// </summary>
    private void BroadcastCutsceneEnd()
    {
        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int broadcastCount = 0;

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 Broadcasting OnCutsceneEnd to all ICutsceneControllable objects...");
        }

        foreach (MonoBehaviour behaviour in allBehaviours)
        {
            if (behaviour == null) continue;

            // Skip the player scripts (already handled separately)
            if (behaviour == playerCameraScript || behaviour == playerMovementScript)
                continue;

            // Check if it implements ICutsceneControllable
            if (behaviour is ICutsceneControllable controllable)
            {
                controllable.OnCutsceneEnd();
                broadcastCount++;

                if (showBroadcastDebug)
                {
                    Debug.Log($"  📡 Sent OnCutsceneEnd to: {behaviour.gameObject.name} ({behaviour.GetType().Name})");
                }
            }
        }

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 Broadcast complete: {broadcastCount} objects notified");
        }
    }

    #endregion

    #region Utility Properties

    public bool IsInCutscene => isInCutscene;

    #endregion
}