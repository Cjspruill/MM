using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Death Save System - Allows player to perform a desperate execution to recover from near-death
/// Triggers slow motion when health is below threshold
/// Can only be used once per checkpoint
/// </summary>
public class DeathSaveSystem : MonoBehaviour
{
    [Header("Death Save Settings")]
    [SerializeField] private float healthThreshold = 10f;
    [SerializeField] private float slowMotionScale = 0.3f;
    [SerializeField] private float executionRange = 3f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float fullHealthAmount = 100f;

    [Header("Visual Feedback")]
    [SerializeField] private float screenEdgeGlowIntensity = 0.8f;
    [SerializeField] private Color deathSaveColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private DeathSaveUI executionPromptUI; // Optional UI prompt

    [Header("Audio")]
    [SerializeField] private AudioClip deathSaveTriggerSound;
    [SerializeField] private AudioClip executionSound;

    // State tracking
    private bool isInDeathSaveMode = false;
    private bool hasUsedDeathSave = false;
    private bool wasInDeathSaveLastFrame = false;

    // Component references
    private BloodSystem bloodSystem;
    private InputSystem_Actions controls;
    private AudioSource audioSource;
    private Health playerHealth;

    // Input action for execution
    private InputAction executionAction;

    private void Awake()
    {
        bloodSystem = GetComponent<BloodSystem>();
        audioSource = GetComponent<AudioSource>();

        // Get or create audio source
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Try to find player health component
        playerHealth = GetComponent<Health>();
        if (playerHealth == null)
        {
            Debug.LogWarning("DeathSaveSystem: No Health component found on player. System will rely on BloodSystem instead.");
        }

        // Setup input system
        controls = new InputSystem_Actions();

        // Bind execution to a key (E for Execute)
        executionAction = controls.Player.Execution; // Or create custom action
    }

    private void OnEnable()
    {
        controls.Enable();
        executionAction.performed += OnExecutionInput;
    }

    private void OnDisable()
    {
        controls.Disable();
        executionAction.performed -= OnExecutionInput;
    }

    private void Update()
    {
        if (bloodSystem == null) return;

        float currentHealth = bloodSystem.currentBlood;
        bool shouldBeInDeathSave = currentHealth > 0 && currentHealth <= healthThreshold && !hasUsedDeathSave;

        // Enter death save mode
        if (shouldBeInDeathSave && !isInDeathSaveMode)
        {
            EnterDeathSaveMode();
        }
        // Exit death save mode
        else if (!shouldBeInDeathSave && isInDeathSaveMode)
        {
            ExitDeathSaveMode();
        }

        // Update UI prompt visibility
        if (executionPromptUI != null)
        {
            bool hasTarget = GetNearestEnemy() != null;

            if (isInDeathSaveMode && hasTarget)
            {
                executionPromptUI.Show();
            }
            else
            {
                executionPromptUI.Hide();
            }
        }

        wasInDeathSaveLastFrame = isInDeathSaveMode;
    }

    private void EnterDeathSaveMode()
    {
        isInDeathSaveMode = true;

        // Slow down time
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // Maintain physics accuracy

        // Play audio feedback
        if (audioSource != null && deathSaveTriggerSound != null)
        {
            audioSource.PlayOneShot(deathSaveTriggerSound);
        }

        Debug.Log("⚠️ DEATH SAVE MODE ACTIVATED - Press E to execute nearby enemy!");
    }

    private void ExitDeathSaveMode()
    {
        isInDeathSaveMode = false;

        // Restore normal time
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        Debug.Log("Death Save mode deactivated");
    }

    private void OnExecutionInput(InputAction.CallbackContext context)
    {
        if (!isInDeathSaveMode) return;

        // Find nearest enemy in range
        Health targetEnemy = GetNearestEnemy();

        if (targetEnemy != null)
        {
            PerformExecution(targetEnemy);
        }
        else
        {
            Debug.Log("No enemy in range for execution!");
        }
    }

    private Health GetNearestEnemy()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, executionRange, enemyLayer);

        Health nearestEnemy = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider col in nearbyColliders)
        {
            Health enemyHealth = col.GetComponent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead())
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEnemy = enemyHealth;
                }
            }
        }

        return nearestEnemy;
    }

    private void PerformExecution(Health targetEnemy)
    {
        // Mark death save as used
        hasUsedDeathSave = true;

        // Play execution audio
        if (audioSource != null && executionSound != null)
        {
            audioSource.PlayOneShot(executionSound);
        }

        // Kill the enemy instantly
        targetEnemy.TakeDamage(999999f, false); // Massive damage to guarantee kill

        // Restore player to full health
        if (bloodSystem != null)
        {
            float healthToRestore = fullHealthAmount - bloodSystem.currentBlood;
            bloodSystem.GainBlood(healthToRestore);
        }

        // Exit death save mode immediately
        ExitDeathSaveMode();

        // Optional: Trigger special execution animation/VFX here
        Debug.Log($"💀 EXECUTION COMPLETE - Health restored to {fullHealthAmount}!");
    }

    /// <summary>
    /// Call this when player reaches a checkpoint to reset death save availability
    /// </summary>
    public void ResetDeathSave()
    {
        hasUsedDeathSave = false;
        Debug.Log("✅ Death Save reset - available again!");
    }

    // Gizmos for debugging
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, executionRange);
    }

    // Public accessors
    public bool IsInDeathSaveMode => isInDeathSaveMode;
    public bool HasUsedDeathSave => hasUsedDeathSave;
    public float HealthThreshold => healthThreshold;
}