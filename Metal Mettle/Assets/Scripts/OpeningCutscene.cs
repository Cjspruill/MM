using UnityEngine;
using System.Collections;

/// <summary>
/// Opening cutscene that zooms into the player's face.
/// NOW WITH PROPER BROADCASTING AND TIMING!
/// </summary>
public class OpeningCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerHeadTransform;

    [Header("Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float cutsceneDuration = 5f;
    [SerializeField] private float startDistance = 5f;
    [SerializeField] private float endDistance = 1.5f;
    [SerializeField] private float horizontalAngle = 0f;
    [SerializeField] private float verticalAngle = 0f;
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0, 0, 0);

    [Header("Timing")]
    [SerializeField] private float startDelay = 0.1f; // Delay to ensure all Start() methods have run

    [Header("Optional")]
    [SerializeField] private bool findPlayerAutomatically = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Broadcasting")]
    [SerializeField] private bool broadcastCutsceneEvents = true;
    [SerializeField] private bool showBroadcastDebug = true;

    private bool hasPlayed = false;
    private bool isInCutscene = false;

    private void Start()
    {
        // Auto-find player if needed
        if (playerHeadTransform == null && findPlayerAutomatically)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                // Try to find head bone
                Animator animator = player.GetComponent<Animator>();
                if (animator != null)
                {
                    playerHeadTransform = animator.GetBoneTransform(HumanBodyBones.Head);
                }

                // Fallback to player root
                if (playerHeadTransform == null)
                {
                    playerHeadTransform = player.transform;
                    Debug.LogWarning("Could not find head bone, using player root transform");
                }
            }
            else
            {
                Debug.LogError($"Could not find player with tag '{playerTag}'");
                return;
            }
        }

        // Play cutscene on start if enabled - USE COROUTINE FOR TIMING
        if (playOnStart)
        {
            StartCoroutine(DelayedCutsceneStart());
        }
    }

    /// <summary>
    /// Delays cutscene start to ensure all Start() methods have executed
    /// This prevents race conditions with PlayerController initialization
    /// </summary>
    private IEnumerator DelayedCutsceneStart()
    {
        Debug.Log($"⏰ OpeningCutscene: Waiting {startDelay}s before starting cutscene...");

        // Wait for at least one frame AND the delay
        yield return null; // Wait one frame
        yield return new WaitForSeconds(startDelay);

        Debug.Log("⏰ OpeningCutscene: Starting cutscene NOW!");
        PlayOpeningCutscene();
    }

    public void PlayOpeningCutscene()
    {
        if (hasPlayed)
        {
            Debug.LogWarning("Opening cutscene already played!");
            return;
        }

        if (playerHeadTransform == null)
        {
            Debug.LogError("Cannot play opening cutscene - player head transform not assigned!");
            return;
        }

        if (CutsceneCameraController.Instance == null)
        {
            Debug.LogError("Cannot play opening cutscene - CutsceneCameraController not found!");
            return;
        }

        hasPlayed = true;
        isInCutscene = true;

        Debug.Log("🎬 ========== OPENING CUTSCENE STARTING ==========");

        // 🚨 BROADCAST CUTSCENE START **FIRST** BEFORE ANYTHING ELSE
        if (broadcastCutsceneEvents)
        {
            BroadcastCutsceneStart();
        }

        // Small delay to ensure broadcast is processed
        StartCoroutine(StartCutsceneAfterBroadcast());
    }

    private IEnumerator StartCutsceneAfterBroadcast()
    {
        // Wait one frame to ensure broadcast is fully processed
        yield return null;

        Debug.Log("🎬 Setting up camera positions...");

        // Calculate starting camera position (far away)
        Vector3 headPosition = playerHeadTransform.position;
        Vector3 playerForward = playerHeadTransform.root.forward;
        Vector3 playerRight = playerHeadTransform.root.right;
        Vector3 worldUp = Vector3.up;

        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
        float verticalRad = verticalAngle * Mathf.Deg2Rad;

        // Starting position (far)
        Vector3 horizontalDirection = (playerForward * Mathf.Cos(horizontalRad) + playerRight * Mathf.Sin(horizontalRad));
        Vector3 direction = (horizontalDirection * Mathf.Cos(verticalRad) + worldUp * Mathf.Sin(verticalRad)).normalized;
        Vector3 startPosition = headPosition + direction * startDistance;

        // Ending position (close)
        Vector3 endPosition = headPosition + direction * endDistance;

        // Look target
        Vector3 lookTarget = headPosition + lookAtOffset;

        // Start looking at target from far position
        Quaternion startRotation = Quaternion.LookRotation(lookTarget - startPosition);

        // Move camera to starting position instantly
        Camera.main.transform.position = startPosition;
        Camera.main.transform.rotation = startRotation;

        Debug.Log("🎬 Starting camera zoom...");

        // Then zoom in using the cutscene system
        CutsceneCameraController.Instance.PlayCustomCutscene(
            endPosition,
            Quaternion.LookRotation(lookTarget - endPosition),
            cutsceneDuration,
            OnCutsceneComplete
        );
    }

    private void OnCutsceneComplete()
    {
        Debug.Log("🎬 ========== OPENING CUTSCENE COMPLETE ==========");
        isInCutscene = false;

        // 🚨 BROADCAST CUTSCENE END
        if (broadcastCutsceneEvents)
        {
            BroadcastCutsceneEnd();
        }

        // Player controls automatically restored by CutsceneCameraController
    }

    // Optional: Call this from other scripts if you don't want to play on start
    public void TriggerCutscene()
    {
        PlayOpeningCutscene();
    }

    #region Broadcasting System

    /// <summary>
    /// Broadcasts OnCutsceneStart to ALL ICutsceneControllable objects in the scene
    /// </summary>
    private void BroadcastCutsceneStart()
    {
        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int broadcastCount = 0;

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 [OpeningCutscene] Broadcasting OnCutsceneStart to all ICutsceneControllable objects...");
        }

        foreach (MonoBehaviour behaviour in allBehaviours)
        {
            if (behaviour == null) continue;

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
            Debug.Log($"📡 [OpeningCutscene] Broadcast complete: {broadcastCount} objects notified");
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
            Debug.Log($"📡 [OpeningCutscene] Broadcasting OnCutsceneEnd to all ICutsceneControllable objects...");
        }

        foreach (MonoBehaviour behaviour in allBehaviours)
        {
            if (behaviour == null) continue;

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
            Debug.Log($"📡 [OpeningCutscene] Broadcast complete: {broadcastCount} objects notified");
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (playerHeadTransform == null) return;

        Vector3 headPosition = playerHeadTransform.position;
        Vector3 playerForward = playerHeadTransform.root.forward;
        Vector3 playerRight = playerHeadTransform.root.right;
        Vector3 worldUp = Vector3.up;

        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
        float verticalRad = verticalAngle * Mathf.Deg2Rad;

        Vector3 horizontalDirection = (playerForward * Mathf.Cos(horizontalRad) + playerRight * Mathf.Sin(horizontalRad));
        Vector3 direction = (horizontalDirection * Mathf.Cos(verticalRad) + worldUp * Mathf.Sin(verticalRad)).normalized;

        // Draw start position
        Vector3 startPosition = headPosition + direction * startDistance;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(startPosition, 0.3f);

        // Draw end position
        Vector3 endPosition = headPosition + direction * endDistance;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(endPosition, 0.2f);

        // Draw path
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPosition, endPosition);

        // Draw look target
        Vector3 lookTarget = headPosition + lookAtOffset;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(lookTarget, 0.15f);

        // Draw view lines
        Gizmos.color = Color.red;
        Gizmos.DrawLine(startPosition, lookTarget);
        Gizmos.DrawLine(endPosition, lookTarget);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(startPosition, "Start (Far)");
        UnityEditor.Handles.Label(endPosition, "End (Close)");
        UnityEditor.Handles.Label(lookTarget, "Look Target");
#endif
    }

    #endregion
}