using UnityEngine;

public class TutorialTrigger : MonoBehaviour
{
    [Header("Tutorial Settings")]
    [Tooltip("The Step ID of the tutorial to trigger (must match exactly)")]
    public string tutorialStepID;

    [Header("Trigger Settings")]
    [Tooltip("Should this trigger only work once?")]
    public bool triggerOnce = true;

    [Tooltip("Require specific tag to trigger (leave empty for any)")]
    public string requiredTag = "Player";

    [Tooltip("Destroy this GameObject after triggering?")]
    public bool destroyAfterTrigger = false;

    [Header("Visual Options")]
    [Tooltip("Show a gizmo in the editor?")]
    public bool showGizmo = true;
    public Color gizmoColor = new Color(1f, 1f, 0f, 0.3f); // Yellow transparent

    [Header("Debug")]
    public bool showDebugLogs = false;

    private TutorialManager tutorialManager;
    private bool hasTriggered = false;

    void Start()
    {
        // Find the tutorial manager
        tutorialManager = FindFirstObjectByType<TutorialManager>();

        if (tutorialManager == null)
        {
            Debug.LogError($"TutorialTrigger '{gameObject.name}': TutorialManager not found in scene!");
        }

        if (string.IsNullOrEmpty(tutorialStepID))
        {
            Debug.LogWarning($"TutorialTrigger '{gameObject.name}': Tutorial Step ID is not set!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if already triggered (if set to trigger once)
        if (triggerOnce && hasTriggered)
        {
            if (showDebugLogs)
                Debug.Log($"TutorialTrigger '{gameObject.name}': Already triggered, ignoring.");
            return;
        }

        // Check tag if required
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
        {
            if (showDebugLogs)
                Debug.Log($"TutorialTrigger '{gameObject.name}': Wrong tag '{other.tag}', expected '{requiredTag}'");
            return;
        }

        // Trigger the tutorial
        TriggerTutorial();
    }

    void TriggerTutorial()
    {
        if (tutorialManager == null)
        {
            Debug.LogError($"TutorialTrigger '{gameObject.name}': Cannot trigger tutorial - TutorialManager is null!");
            return;
        }

        if (string.IsNullOrEmpty(tutorialStepID))
        {
            Debug.LogError($"TutorialTrigger '{gameObject.name}': Cannot trigger tutorial - Step ID is empty!");
            return;
        }

        if (showDebugLogs)
            Debug.Log($"🎯 TutorialTrigger '{gameObject.name}': Triggering tutorial '{tutorialStepID}'");

        // Trigger the tutorial
        tutorialManager.TriggerTutorial(tutorialStepID);

        // Mark as triggered
        hasTriggered = true;

        // Destroy if set
        if (destroyAfterTrigger)
        {
            if (showDebugLogs)
                Debug.Log($"TutorialTrigger '{gameObject.name}': Destroying trigger GameObject");

            Destroy(gameObject);
        }
    }

    // Manual trigger method (can be called from other scripts)
    public void ManualTrigger()
    {
        if (showDebugLogs)
            Debug.Log($"TutorialTrigger '{gameObject.name}': Manually triggered");

        TriggerTutorial();
    }

    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        // Draw the trigger volume
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = gizmoColor;

            if (col is BoxCollider boxCol)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(boxCol.center, boxCol.size);

                // Draw wireframe
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider sphereCol)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawSphere(sphereCol.center, sphereCol.radius);

                // Draw wireframe
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireSphere(sphereCol.center, sphereCol.radius);
            }
            else if (col is CapsuleCollider capsuleCol)
            {
                // Capsule approximation
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawSphere(capsuleCol.center, capsuleCol.radius);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireSphere(capsuleCol.center, capsuleCol.radius);
            }
        }
        else
        {
            // No collider, draw a small sphere as indicator
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        // Draw label with tutorial step ID
        if (!string.IsNullOrEmpty(tutorialStepID))
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position, $"Tutorial: {tutorialStepID}");
#endif
        }
    }
}