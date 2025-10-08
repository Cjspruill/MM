using UnityEngine;

public class RagdollController : MonoBehaviour
{
    [Header("Ragdoll Settings")]
    public bool disableOnStart = true;
    public float ragdollDelay = 0f; // Delay before enabling ragdoll (for death animation)

    [Header("Force Settings")]
    public bool applyDeathForce = false;
    public float deathForceAmount = 500f;
    public Vector3 deathForceDirection = Vector3.forward;

    [Header("Components to Disable on Ragdoll")]
    public bool disableAnimator = true;
    public bool disableCharacterController = true;
    public bool disablePlayerController = true;
    public bool disableComboController = true;

    [Header("References")]
    public Animator animator;
    public CharacterController characterController;
    public PlayerController playerController;
    public ComboController comboController;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;
    private bool isRagdoll = false;

    void Start()
    {
        // Auto-find references if not assigned
        if (animator == null) animator = GetComponent<Animator>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (comboController == null) comboController = GetComponent<ComboController>();

        // Get all ragdoll rigidbodies and colliders (excluding the root)
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        if (disableOnStart)
        {
            DisableRagdoll();
        }

        if (showDebugLogs)
        {
            Debug.Log($"RagdollController initialized. Found {ragdollRigidbodies.Length} rigidbodies and {ragdollColliders.Length} colliders");
        }
    }

    public void EnableRagdoll()
    {
        if (isRagdoll) return;

        if (ragdollDelay > 0)
        {
            Invoke(nameof(ActivateRagdoll), ragdollDelay);
        }
        else
        {
            ActivateRagdoll();
        }
    }

    void ActivateRagdoll()
    {
        if (isRagdoll) return;

        isRagdoll = true;

        if (showDebugLogs)
        {
            Debug.Log("🎭 Enabling ragdoll");
        }

        // Disable animator
        if (disableAnimator && animator != null)
        {
            animator.enabled = false;
        }

        // Disable character controller
        if (disableCharacterController && characterController != null)
        {
            characterController.enabled = false;
        }

        // Disable player controllers
        if (disablePlayerController && playerController != null)
        {
            playerController.enabled = false;
        }

        if (disableComboController && comboController != null)
        {
            comboController.enabled = false;
        }

        // Enable all ragdoll rigidbodies
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null && rb.gameObject != gameObject) // Don't enable root rigidbody
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        // Enable all ragdoll colliders
        foreach (Collider col in ragdollColliders)
        {
            if (col != null && col != characterController) // Don't re-enable character controller
            {
                col.enabled = true;
            }
        }

        // Apply death force if enabled
        if (applyDeathForce)
        {
            ApplyDeathForce();
        }

        if (showDebugLogs)
        {
            Debug.Log($"✓ Ragdoll enabled - {ragdollRigidbodies.Length} rigidbodies activated");
        }
    }

    public void DisableRagdoll()
    {
        isRagdoll = false;

        if (showDebugLogs)
        {
            Debug.Log("🎭 Disabling ragdoll");
        }

        // Enable animator
        if (animator != null)
        {
            animator.enabled = true;
        }

        // Enable character controller
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Enable player controllers
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        if (comboController != null)
        {
            comboController.enabled = true;
        }

        // Disable all ragdoll rigidbodies
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null && rb.gameObject != gameObject)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        // Disable all ragdoll colliders (except character controller)
        foreach (Collider col in ragdollColliders)
        {
            if (col != null && col != characterController)
            {
                col.enabled = false;
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"✓ Ragdoll disabled");
        }
    }

    void ApplyDeathForce()
    {
        // Find the main body part to apply force to (usually spine or hips)
        Rigidbody mainBody = null;

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null && (rb.gameObject.name.ToLower().Contains("spine") ||
                               rb.gameObject.name.ToLower().Contains("hips") ||
                               rb.gameObject.name.ToLower().Contains("chest")))
            {
                mainBody = rb;
                break;
            }
        }

        // If no main body found, use first rigidbody
        if (mainBody == null && ragdollRigidbodies.Length > 0)
        {
            mainBody = ragdollRigidbodies[0];
        }

        if (mainBody != null)
        {
            Vector3 force = deathForceDirection.normalized * deathForceAmount;
            mainBody.AddForce(force, ForceMode.Impulse);

            if (showDebugLogs)
            {
                Debug.Log($"Applied death force: {force} to {mainBody.gameObject.name}");
            }
        }
    }

    // Public method to apply custom force on ragdoll
    public void ApplyForceToRagdoll(Vector3 force, Vector3 position)
    {
        if (!isRagdoll)
        {
            Debug.LogWarning("Cannot apply force - ragdoll not enabled");
            return;
        }

        // Find closest rigidbody to position
        Rigidbody closestRb = null;
        float closestDistance = float.MaxValue;

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null)
            {
                float distance = Vector3.Distance(rb.position, position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRb = rb;
                }
            }
        }

        if (closestRb != null)
        {
            closestRb.AddForce(force, ForceMode.Impulse);
        }
    }

    public bool IsRagdoll() => isRagdoll;
}