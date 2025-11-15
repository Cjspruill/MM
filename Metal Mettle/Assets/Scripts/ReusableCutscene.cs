using UnityEngine;
using System.Collections;

/// <summary>
/// Reusable cutscene script that can be triggered from anywhere.
/// Based on OpeningCutscene but made modular for any cutscene use.
/// Call PlayCutscene() from triggers, events, or other scripts.
/// </summary>
public class ReusableCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform targetTransform; // What to look at (player head, object, etc.)

    [Header("Cutscene Settings")]
    [SerializeField] private float cutsceneDuration = 5f;
    [SerializeField] private float startDistance = 5f;
    [SerializeField] private float endDistance = 1.5f;
    [SerializeField] private float horizontalAngle = 0f; // Angle around target (0 = front, 90 = right, etc.)
    [SerializeField] private float verticalAngle = 0f;   // Angle above/below (positive = above)
    [SerializeField] private Vector3 lookAtOffset = Vector3.zero; // Offset from target position

    [Header("Playback Options")]
    [SerializeField] private bool playOnlyOnce = true; // If true, won't replay after first time
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Broadcasting")]
    [SerializeField] private bool broadcastCutsceneEvents = true;
    [SerializeField] private bool showBroadcastDebug = false;

    private bool hasPlayed = false;
    private bool isInCutscene = false;

    #region Public Methods

    /// <summary>
    /// Main method to trigger this cutscene. Call from anywhere!
    /// </summary>
    public void PlayCutscene()
    {
        // Check if already played and set to only play once
        if (playOnlyOnce && hasPlayed)
        {
            Debug.LogWarning($"Cutscene '{gameObject.name}' already played and is set to playOnlyOnce!");
            return;
        }

        // Check if already in this cutscene
        if (isInCutscene)
        {
            Debug.LogWarning($"Cutscene '{gameObject.name}' is already playing!");
            return;
        }

        // Auto-find target if needed
        if (targetTransform == null && autoFindPlayer)
        {
            if (!FindPlayerTarget())
            {
                Debug.LogError($"Cannot play cutscene '{gameObject.name}' - no target transform found!");
                return;
            }
        }

        // Validate required components
        if (targetTransform == null)
        {
            Debug.LogError($"Cannot play cutscene '{gameObject.name}' - target transform not assigned!");
            return;
        }

        if (CutsceneCameraController.Instance == null)
        {
            Debug.LogError($"Cannot play cutscene '{gameObject.name}' - CutsceneCameraController not found in scene!");
            return;
        }

        // Mark as played and in progress
        hasPlayed = true;
        isInCutscene = true;

        Debug.Log($"🎬 ========== CUTSCENE '{gameObject.name}' STARTING ==========");

        // Broadcast cutscene start FIRST
        if (broadcastCutsceneEvents)
        {
            BroadcastCutsceneStart();
        }

        // Start cutscene after broadcast is processed
        StartCoroutine(StartCutsceneAfterBroadcast());
    }

    /// <summary>
    /// Reset the cutscene so it can be played again (if playOnlyOnce is true)
    /// </summary>
    public void ResetCutscene()
    {
        hasPlayed = false;
        isInCutscene = false;
        Debug.Log($"🔄 Cutscene '{gameObject.name}' has been reset");
    }

    /// <summary>
    /// Check if this cutscene has already been played
    /// </summary>
    public bool HasPlayed => hasPlayed;

    /// <summary>
    /// Check if this cutscene is currently playing
    /// </summary>
    public bool IsPlaying => isInCutscene;

    #endregion

    #region Cutscene Execution

    private IEnumerator StartCutsceneAfterBroadcast()
    {
        // Wait one frame to ensure broadcast is fully processed
        yield return null;

        Debug.Log($"🎬 Setting up camera positions for '{gameObject.name}'...");

        // Calculate camera positions
        Vector3 targetPosition = targetTransform.position;
        Vector3 targetForward = targetTransform.root.forward;
        Vector3 targetRight = targetTransform.root.right;
        Vector3 worldUp = Vector3.up;

        // Convert angles to radians
        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
        float verticalRad = verticalAngle * Mathf.Deg2Rad;

        // Calculate direction from angles
        Vector3 horizontalDirection = (targetForward * Mathf.Cos(horizontalRad) +
                                       targetRight * Mathf.Sin(horizontalRad));
        Vector3 direction = (horizontalDirection * Mathf.Cos(verticalRad) +
                            worldUp * Mathf.Sin(verticalRad)).normalized;

        // Calculate start and end positions
        Vector3 startPosition = targetPosition + direction * startDistance;
        Vector3 endPosition = targetPosition + direction * endDistance;

        // Calculate look target with offset
        Vector3 lookTarget = targetPosition + lookAtOffset;

        // Set starting rotation
        Quaternion startRotation = Quaternion.LookRotation(lookTarget - startPosition);

        // Move camera to starting position instantly
        Camera.main.transform.position = startPosition;
        Camera.main.transform.rotation = startRotation;

        Debug.Log($"🎬 Starting camera movement for '{gameObject.name}'...");

        // Execute cutscene using CutsceneCameraController
        CutsceneCameraController.Instance.PlayCustomCutscene(
            endPosition,
            Quaternion.LookRotation(lookTarget - endPosition),
            cutsceneDuration,
            OnCutsceneComplete
        );
    }

    private void OnCutsceneComplete()
    {
        Debug.Log($"🎬 ========== CUTSCENE '{gameObject.name}' COMPLETE ==========");
        isInCutscene = false;

        // Broadcast cutscene end
        if (broadcastCutsceneEvents)
        {
            BroadcastCutsceneEnd();
        }

        // Player controls automatically restored by CutsceneCameraController
    }

    #endregion

    #region Helper Methods

    private bool FindPlayerTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            Debug.LogWarning($"Could not find player with tag '{playerTag}'");
            return false;
        }

        // Try to find head bone
        Animator animator = player.GetComponent<Animator>();
        if (animator != null)
        {
            targetTransform = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        // Fallback to player root
        if (targetTransform == null)
        {
            targetTransform = player.transform;
            Debug.LogWarning("Could not find head bone, using player root transform");
        }

        Debug.Log($"Auto-found target: {targetTransform.name}");
        return true;
    }

    #endregion

    #region Broadcasting System

    private void BroadcastCutsceneStart()
    {
        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int broadcastCount = 0;

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 [{gameObject.name}] Broadcasting OnCutsceneStart to all ICutsceneControllable objects...");
        }

        foreach (MonoBehaviour behaviour in allBehaviours)
        {
            if (behaviour == null) continue;

            if (behaviour is ICutsceneControllable controllable)
            {
                controllable.OnCutsceneStart();
                broadcastCount++;

                if (showBroadcastDebug)
                {
                    Debug.Log($"  📡 Sent OnCutsceneStart to: {behaviour.gameObject.name}.{behaviour.GetType().Name}");
                }
            }
        }

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 [{gameObject.name}] Broadcast complete! Sent to {broadcastCount} objects");
        }
    }

    private void BroadcastCutsceneEnd()
    {
        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int broadcastCount = 0;

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 [{gameObject.name}] Broadcasting OnCutsceneEnd to all ICutsceneControllable objects...");
        }

        foreach (MonoBehaviour behaviour in allBehaviours)
        {
            if (behaviour == null) continue;

            if (behaviour is ICutsceneControllable controllable)
            {
                controllable.OnCutsceneEnd();
                broadcastCount++;

                if (showBroadcastDebug)
                {
                    Debug.Log($"  📡 Sent OnCutsceneEnd to: {behaviour.gameObject.name}.{behaviour.GetType().Name}");
                }
            }
        }

        if (showBroadcastDebug)
        {
            Debug.Log($"📡 [{gameObject.name}] Broadcast complete! Sent to {broadcastCount} objects");
        }
    }

    #endregion

    #region Gizmos (Editor Only)

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (targetTransform == null && autoFindPlayer)
        {
            // Try to preview with player
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                Animator animator = player.GetComponent<Animator>();
                Transform previewTarget = animator?.GetBoneTransform(HumanBodyBones.Head) ?? player.transform;
                DrawCutsceneGizmos(previewTarget);
            }
        }
        else if (targetTransform != null)
        {
            DrawCutsceneGizmos(targetTransform);
        }
    }

    private void DrawCutsceneGizmos(Transform target)
    {
        Vector3 targetPosition = target.position;
        Vector3 targetForward = target.root.forward;
        Vector3 targetRight = target.root.right;
        Vector3 worldUp = Vector3.up;

        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
        float verticalRad = verticalAngle * Mathf.Deg2Rad;

        Vector3 horizontalDirection = (targetForward * Mathf.Cos(horizontalRad) +
                                       targetRight * Mathf.Sin(horizontalRad));
        Vector3 direction = (horizontalDirection * Mathf.Cos(verticalRad) +
                            worldUp * Mathf.Sin(verticalRad)).normalized;

        Vector3 startPosition = targetPosition + direction * startDistance;
        Vector3 endPosition = targetPosition + direction * endDistance;
        Vector3 lookTarget = targetPosition + lookAtOffset;

        // Draw start position (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPosition, 0.2f);
        Gizmos.DrawLine(startPosition, lookTarget);

        // Draw end position (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(endPosition, 0.15f);
        Gizmos.DrawLine(endPosition, lookTarget);

        // Draw camera path (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPosition, endPosition);

        // Draw look target (blue)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(lookTarget, 0.1f);
    }
#endif

    #endregion
}