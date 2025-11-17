using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Death Save System - Allows player to perform a desperate execution to recover from near-death
/// Triggers slow motion when health is below threshold
/// Can only be used once per checkpoint
/// Supports dual-grip execution for VR/gamepad controllers
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

    [Header("Grip Execution")]
    [Tooltip("Time window (in seconds) for both grips to be pressed to count as simultaneous")]
    [SerializeField] private float gripSimultaneousWindow = 0.2f;
    [Tooltip("Enable grip-based execution")]
    [SerializeField] private bool enableGripExecution = true;

    [Header("Audio")]
    [SerializeField] private AudioClip deathSaveTriggerSound;
    [SerializeField] private AudioClip executionSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // State tracking
    private bool isInDeathSaveMode = false;
    private bool hasUsedDeathSave = false;
    private bool wasInDeathSaveLastFrame = false;

    // Grip state tracking
    private bool leftGripPressed = false;
    private bool rightGripPressed = false;
    private float leftGripPressTime = -1f;
    private float rightGripPressTime = -1f;

    // Component references
    private BloodSystem bloodSystem;
    private InputSystem_Actions controls;
    private AudioSource audioSource;
    private Health playerHealth;
    private Animator playerAnimator;

    // Input actions
    private InputAction executionAction;
    private InputAction leftGripInput;
    private InputAction rightGripInput;

    private void Awake()
    {
        bloodSystem = GetComponent<BloodSystem>();
        audioSource = GetComponent<AudioSource>();
        playerAnimator = GetComponent<Animator>();

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

        // Check for animator
        if (playerAnimator == null)
        {
            Debug.LogWarning("DeathSaveSystem: No Animator component found on player. Execution animation will not play.");
        }

        // Setup input system
        controls = InputManager.Instance.controls;

        // Bind execution actions
        executionAction = controls.Player.Execution;
        leftGripInput = controls.Player.LeftGrip;
        rightGripInput = controls.Player.RightGrip;
    }

    private void OnEnable()
    {
        controls.Enable();
        executionAction.performed += OnExecutionInput;

        if (enableGripExecution)
        {
            leftGripInput.performed += OnLeftGripPressed;
            leftGripInput.canceled += OnLeftGripReleased;
            rightGripInput.performed += OnRightGripPressed;
            rightGripInput.canceled += OnRightGripReleased;
        }
    }

    private void OnDisable()
    {
        controls.Disable();
        executionAction.performed -= OnExecutionInput;

        if (enableGripExecution)
        {
            leftGripInput.performed -= OnLeftGripPressed;
            leftGripInput.canceled -= OnLeftGripReleased;
            rightGripInput.performed -= OnRightGripPressed;
            rightGripInput.canceled -= OnRightGripReleased;
        }
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

        // Check for grip-based execution
        if (isInDeathSaveMode && enableGripExecution)
        {
            CheckGripExecution();
        }

        // Update UI prompt visibility
        if (executionPromptUI != null)
        {
            Health nearestEnemy = GetNearestEnemy();
            bool hasTarget = nearestEnemy != null;

            if (isInDeathSaveMode && hasTarget)
            {
                executionPromptUI.Show();
                if (showDebugLogs)
                {
                    Debug.Log($"Showing UI - Enemy in range: {nearestEnemy.gameObject.name}");
                }
            }
            else
            {
                executionPromptUI.Hide();
                if (isInDeathSaveMode && !hasTarget && showDebugLogs)
                {
                    Debug.Log("Death Save active but no enemy in range");
                }
            }
        }
        else if (isInDeathSaveMode && showDebugLogs)
        {
            Debug.LogWarning("DeathSaveSystem: executionPromptUI is not assigned!");
        }

        wasInDeathSaveLastFrame = isInDeathSaveMode;
    }

    // ============================================
    // GRIP INPUT HANDLERS
    // ============================================

    private void OnLeftGripPressed(InputAction.CallbackContext context)
    {
        leftGripPressed = true;
        leftGripPressTime = Time.unscaledTime; // Use unscaled time since we're in slow motion

        if (showDebugLogs)
        {
            Debug.Log("🖐️ [Death Save] Left grip pressed");
        }
    }

    private void OnLeftGripReleased(InputAction.CallbackContext context)
    {
        leftGripPressed = false;
        leftGripPressTime = -1f;

        if (showDebugLogs)
        {
            Debug.Log("🖐️ [Death Save] Left grip released");
        }
    }

    private void OnRightGripPressed(InputAction.CallbackContext context)
    {
        rightGripPressed = true;
        rightGripPressTime = Time.unscaledTime; // Use unscaled time since we're in slow motion

        if (showDebugLogs)
        {
            Debug.Log("🖐️ [Death Save] Right grip pressed");
        }
    }

    private void OnRightGripReleased(InputAction.CallbackContext context)
    {
        rightGripPressed = false;
        rightGripPressTime = -1f;

        if (showDebugLogs)
        {
            Debug.Log("🖐️ [Death Save] Right grip released");
        }
    }

    private void CheckGripExecution()
    {
        // Check if both grips are pressed
        if (leftGripPressed && rightGripPressed)
        {
            // Check if they were pressed within the simultaneous window
            float timeDifference = Mathf.Abs(leftGripPressTime - rightGripPressTime);

            if (timeDifference <= gripSimultaneousWindow)
            {
                if (showDebugLogs)
                {
                    Debug.Log("🙌 [Death Save] Both grips pressed simultaneously! Attempting execution...");
                }

                TryExecuteEnemy();

                // Reset grip states to prevent repeated execution
                leftGripPressed = false;
                rightGripPressed = false;
                leftGripPressTime = -1f;
                rightGripPressTime = -1f;
            }
        }
    }

    // ============================================
    // DEATH SAVE MODE
    // ============================================

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

        Debug.Log("⚠️ DEATH SAVE MODE ACTIVATED - Press E or grip both controllers to execute nearby enemy!");
    }

    private void ExitDeathSaveMode()
    {
        isInDeathSaveMode = false;

        // Restore normal time
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Reset grip states
        leftGripPressed = false;
        rightGripPressed = false;
        leftGripPressTime = -1f;
        rightGripPressTime = -1f;

        Debug.Log("Death Save mode deactivated");
    }

    // ============================================
    // EXECUTION
    // ============================================

    private void OnExecutionInput(InputAction.CallbackContext context)
    {
        if (!isInDeathSaveMode) return;
        TryExecuteEnemy();
    }

    private void TryExecuteEnemy()
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
            if (showDebugLogs)
            {
                Debug.Log("No enemy in range for execution!");
            }
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

        // Trigger execution animation
        if (playerAnimator != null)
        {
            playerAnimator.Play("Execution");
            if (showDebugLogs)
            {
                Debug.Log("Playing execution animation");
            }
        }

        // Play execution audio
        if (audioSource != null && executionSound != null)
        {
            audioSource.PlayOneShot(executionSound);
        }

        // Use ExecutionKill method instead of TakeDamage
        // This will properly fire the onExecution event for tracking
        targetEnemy.ExecutionKill();

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