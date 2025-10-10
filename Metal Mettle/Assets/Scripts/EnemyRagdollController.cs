using UnityEngine;
using UnityEngine.AI;

public class EnemyRagdollController : MonoBehaviour
{
    [Header("Ragdoll Settings")]
    public bool disableOnStart = true;
    public float ragdollDelay = 0f;
    public float ragdollDuration = 5f;

    [Header("Force Settings")]
    public bool applyDeathForce = true;
    public float deathForceAmount = 300f;
    public bool useAttackDirection = true;
    public Vector3 fallbackForceDirection = Vector3.forward;
    public float upwardForceMultiplier = 0.3f;

    [Header("Collision Settings")]
    public bool disableMainCollider = true; // NEW: Disable main collider to prevent conflicts
    public LayerMask ragdollLayer; // NEW: Optional separate layer for ragdolls

    [Header("Components to Disable on Ragdoll")]
    public bool disableAnimator = true;
    public bool disableNavMeshAgent = true;
    public bool disableAI = true;

    [Header("References")]
    public Animator animator;
    public NavMeshAgent navAgent;
    public Health health;
    public Collider mainCollider; // NEW: Reference to main collider

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;
    private bool isRagdoll = false;
    private Vector3 lastAttackDirection = Vector3.zero;
    private bool wasHeavyAttack = false;

    void Start()
    {
        // Auto-find references if not assigned
        if (animator == null) animator = GetComponent<Animator>();
        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<Health>();

        // Find main collider (the one on the root, not on ragdoll bones)
        if (mainCollider == null)
        {
            Collider[] allColliders = GetComponents<Collider>();
            if (allColliders.Length > 0)
            {
                mainCollider = allColliders[0]; // Usually the capsule collider
            }
        }

        // Get all ragdoll rigidbodies and colliders (children only)
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        // Filter out the main collider from ragdoll colliders
        System.Collections.Generic.List<Collider> ragdollCollidersList = new System.Collections.Generic.List<Collider>();
        foreach (Collider col in ragdollColliders)
        {
            if (col != mainCollider && col.transform != transform) // Not main collider, not root
            {
                ragdollCollidersList.Add(col);
            }
        }
        ragdollColliders = ragdollCollidersList.ToArray();

        if (disableOnStart)
        {
            DisableRagdoll();
        }

        // Subscribe to health death event
        if (health != null)
        {
            health.onDeath.AddListener(OnDeath);
        }

        if (showDebugLogs)
        {
            Debug.Log($"EnemyRagdollController initialized on {gameObject.name}. Found {ragdollRigidbodies.Length} rigidbodies and {ragdollColliders.Length} ragdoll colliders");
        }
    }

    void OnDeath()
    {
        if (showDebugLogs)
        {
            Debug.Log($"💀 Death event received for {gameObject.name}");
        }

        EnableRagdoll();
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
            Debug.Log($"🎭 Enabling ragdoll on {gameObject.name}");
        }

        // IMPORTANT: Disable components BEFORE enabling ragdoll physics

        // Disable NavMeshAgent FIRST (most important)
        if (disableNavMeshAgent && navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true; // Stop movement
            navAgent.velocity = Vector3.zero; // Clear velocity
            navAgent.enabled = false;
        }

        // Disable animator SECOND
        if (disableAnimator && animator != null)
        {
            animator.enabled = false;
        }

        // Disable main collider to prevent conflicts
        if (disableMainCollider && mainCollider != null)
        {
            mainCollider.enabled = false;
            if (showDebugLogs)
            {
                Debug.Log($"   Disabled main collider: {mainCollider.name}");
            }
        }

        // Disable AI scripts
        if (disableAI)
        {
            MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                if (script != this && script != health)
                {
                    string typeName = script.GetType().Name.ToLower();
                    if (typeName.Contains("ai") ||
                        typeName.Contains("enemy") ||
                        typeName.Contains("patrol") ||
                        typeName.Contains("chase") ||
                        typeName.Contains("combat"))
                    {
                        script.enabled = false;
                        if (showDebugLogs)
                        {
                            Debug.Log($"   Disabled AI script: {script.GetType().Name}");
                        }
                    }
                }
            }
        }

        // NOW enable ragdoll physics
        // Enable all ragdoll rigidbodies
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null && rb.transform != transform) // Don't enable root rigidbody if it exists
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // Clear any existing velocities to prevent snapping
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // Enable all ragdoll colliders
        foreach (Collider col in ragdollColliders)
        {
            if (col != null)
            {
                col.enabled = true;
            }
        }

        // IMPORTANT: Apply force AFTER a small delay to let physics stabilize
        if (applyDeathForce)
        {
            Invoke(nameof(ApplyDeathForce), 0.02f); // Small delay prevents snapping
        }

        // Schedule destruction if duration is set
        if (ragdollDuration > 0)
        {
            Destroy(gameObject, ragdollDuration);
        }

        if (showDebugLogs)
        {
            Debug.Log($"✓ Ragdoll enabled on {gameObject.name}");
        }
    }

    public void DisableRagdoll()
    {
        isRagdoll = false;

        if (showDebugLogs)
        {
            Debug.Log($"🎭 Disabling ragdoll on {gameObject.name}");
        }

        // Disable all ragdoll rigidbodies FIRST
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null && rb.transform != transform)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // Disable all ragdoll colliders
        foreach (Collider col in ragdollColliders)
        {
            if (col != null)
            {
                col.enabled = false;
            }
        }

        // Re-enable main collider
        if (mainCollider != null)
        {
            mainCollider.enabled = true;
        }

        // Enable animator
        if (animator != null)
        {
            animator.enabled = true;
        }

        // Enable NavMeshAgent
        if (navAgent != null)
        {
            navAgent.enabled = true;
        }

        if (showDebugLogs)
        {
            Debug.Log($"✓ Ragdoll disabled on {gameObject.name}");
        }
    }

    void ApplyDeathForce()
    {
        Vector3 forceDirection;

        // Use attack direction if available, otherwise use fallback
        if (useAttackDirection && lastAttackDirection != Vector3.zero)
        {
            forceDirection = lastAttackDirection.normalized;
        }
        else
        {
            forceDirection = fallbackForceDirection.normalized;
        }

        // Add upward component for more dramatic ragdoll
        forceDirection += Vector3.up * upwardForceMultiplier;
        forceDirection.Normalize();

        // Find the main body part to apply force to
        Rigidbody mainBody = FindMainBody();

        if (mainBody != null)
        {
            // Increase force for heavy attacks
            float finalForce = deathForceAmount;
            if (wasHeavyAttack)
            {
                finalForce *= 1.5f;
                if (showDebugLogs)
                {
                    Debug.Log($"   Heavy attack - increased death force to {finalForce}");
                }
            }

            Vector3 force = forceDirection * finalForce;
            mainBody.AddForce(force, ForceMode.Impulse);

            // Add some random torque for more natural ragdoll
            Vector3 randomTorque = new Vector3(
                Random.Range(-30f, 30f),
                Random.Range(-30f, 30f),
                Random.Range(-30f, 30f)
            );
            mainBody.AddTorque(randomTorque, ForceMode.Impulse);

            if (showDebugLogs)
            {
                Debug.Log($"   Applied death force: {force.magnitude} in direction {forceDirection} to {mainBody.gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"Could not find main body rigidbody on {gameObject.name}!");
        }
    }

    Rigidbody FindMainBody()
    {
        // Try to find spine, hips, or chest
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null)
            {
                string name = rb.gameObject.name.ToLower();
                if (name.Contains("spine") ||
                    name.Contains("hips") ||
                    name.Contains("chest") ||
                    name.Contains("pelvis"))
                {
                    return rb;
                }
            }
        }

        // Fallback to first rigidbody
        if (ragdollRigidbodies.Length > 0)
        {
            return ragdollRigidbodies[0];
        }

        return null;
    }

    public void RecordAttack(Vector3 attackDirection, bool isHeavy)
    {
        lastAttackDirection = attackDirection;
        wasHeavyAttack = isHeavy;

        if (showDebugLogs)
        {
            Debug.Log($"Recorded attack - Direction: {attackDirection}, Heavy: {isHeavy}");
        }
    }

    public void ApplyExplosionForce(float force, Vector3 position, float radius)
    {
        if (!isRagdoll)
        {
            Debug.LogWarning("Cannot apply explosion force - ragdoll not enabled");
            return;
        }

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null)
            {
                rb.AddExplosionForce(force, position, radius, 1f, ForceMode.Impulse);
            }
        }
    }

    public bool IsRagdoll() => isRagdoll;

    void OnDestroy()
    {
        // Unsubscribe from events
        if (health != null)
        {
            health.onDeath.RemoveListener(OnDeath);
        }
    }
}