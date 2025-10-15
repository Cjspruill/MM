using UnityEngine;

/// <summary>
/// Simple opening cutscene that zooms into the player's face over 5 seconds.
/// Attach to any GameObject in the scene (like a CutsceneManager).
/// </summary>
public class OpeningCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerHeadTransform;

    [Header("Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float cutsceneDuration = 5f;
    [SerializeField] private float startDistance = 5f; // Starting distance from face
    [SerializeField] private float endDistance = 1.5f; // Ending distance (close-up)
    [SerializeField] private float horizontalAngle = 0f; // 0=front, 45=side, etc.
    [SerializeField] private float verticalAngle = 0f; // 0=level, 10=slightly above
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0, 0, 0); // Offset where camera looks

    [Header("Optional")]
    [SerializeField] private bool findPlayerAutomatically = true;
    [SerializeField] private string playerTag = "Player";

    private bool hasPlayed = false;

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

        // Play cutscene on start if enabled
        if (playOnStart && CutsceneCameraController.Instance != null)
        {
            PlayOpeningCutscene();
        }
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

        Debug.Log("🎬 Playing opening cutscene - zooming to player face");

        // Calculate starting camera position (far away)
        Vector3 headPosition = playerHeadTransform.position;
        Vector3 playerForward = playerHeadTransform.root.forward; // Use root (player body) rotation
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
        Debug.Log("🎬 Opening cutscene complete!");
        // Player controls automatically restored by CutsceneCameraController
    }

    // Optional: Call this from other scripts if you don't want to play on start
    public void TriggerCutscene()
    {
        PlayOpeningCutscene();
    }

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
}