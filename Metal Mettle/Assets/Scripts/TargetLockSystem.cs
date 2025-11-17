using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Target lock system for Metal Mettle - allows player to lock onto enemies/bosses
/// Handles target detection, switching, and maintains facing direction toward locked target
/// Integrates with PlayerController for strafe-based movement when locked
/// </summary>
public class TargetLockSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlayerController playerController; // To check dash state

    [Header("Target Detection")]
    [SerializeField] private LayerMask targetLayers; // Set to Enemy and Boss layers
    [SerializeField] private float lockOnRange = 15f;
    [SerializeField] private float lockOnAngle = 60f; // Cone angle for initial lock-on
    [SerializeField] private float breakLockDistance = 25f; // Distance at which lock breaks

    [Header("Target Switching")]
    [SerializeField] private float switchCooldown = 0.3f; // Prevent rapid switching
    [SerializeField] private float switchAngle = 45f; // How far to look for next target when switching
    [SerializeField] private bool autoRelockOnDeath = true; // Automatically lock onto new target when current dies

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 10f; // How fast player rotates to face target
    [SerializeField] private bool smoothRotation = true;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject lockOnIndicatorPrefab; // UI element above locked target
    [SerializeField] private Vector3 indicatorOffset = new Vector3(0, 2f, 0); // Height above target

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color lockRangeColor = new Color(1f, 1f, 0f, 0.2f);
    [SerializeField] private Color lockedTargetColor = Color.red;

    // State
    private Transform currentTarget;
    private GameObject lockOnIndicator;
    private bool isLocked = false;
    private float lastSwitchTime = 0f;
    private InputSystem_Actions controls;

    // Public properties
    public bool IsLocked => isLocked;
    public Transform CurrentTarget => currentTarget;

    private void Start()
    {
        controls = InputManager.Instance.controls;

        if (playerTransform == null)
            playerTransform = transform;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Subscribe to input events
        controls.Player.TargetLock.performed += ctx => ToggleTargetLock();
        controls.Player.SwitchTarget.performed += OnSwitchTargetInput;

        Debug.Log("TargetLockSystem initialized");
    }

    /// <summary>
    /// Handles switch target input - works with buttons, stick, or any control type
    /// </summary>
    private void OnSwitchTargetInput(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (!isLocked || currentTarget == null)
            return;

        // Try to read as Vector2 first (for stick/dpad)
        try
        {
            Vector2 input = ctx.ReadValue<Vector2>();
            if (Mathf.Abs(input.x) > 0.5f)
            {
                SwitchTargetButton(input.x > 0);
            }
        }
        catch
        {
            // If that fails, it's a button press - just switch right
            SwitchTargetButton(true);
        }
    }

    private void Update()
    {
        if (isLocked)
        {
            UpdateTargetLock();
            UpdateLockOnIndicator();
        }
    }

    private void LateUpdate()
    {
        // Rotate player to face target AFTER movement is calculated
        if (isLocked && currentTarget != null)
        {
            RotateTowardsTarget();
        }
    }

    #region Target Lock Toggle

    /// <summary>
    /// Toggles target lock on/off. If off, finds nearest valid target. If on, releases lock.
    /// </summary>
    private void ToggleTargetLock()
    {
        if (isLocked)
        {
            ReleaseLock();
        }
        else
        {
            AcquireLock();
        }
    }

    /// <summary>
    /// Attempts to lock onto the nearest valid target in front of the player
    /// </summary>
    private void AcquireLock()
    {
        Transform target = FindNearestTarget();

        if (target != null)
        {
            currentTarget = target;
            isLocked = true;
            CreateLockOnIndicator();
            Debug.Log($"Locked onto: {currentTarget.name}");
        }
        else
        {
            Debug.Log("No valid targets in range");
        }
    }

    /// <summary>
    /// Releases the current target lock
    /// </summary>
    private void ReleaseLock()
    {
        isLocked = false;
        currentTarget = null;
        DestroyLockOnIndicator();
        Debug.Log("Target lock released");
    }

    #endregion

    #region Target Detection

    /// <summary>
    /// Finds the nearest valid target within lock-on range and angle
    /// </summary>
    private Transform FindNearestTarget()
    {
        Collider[] potentialTargets = Physics.OverlapSphere(playerTransform.position, lockOnRange, targetLayers);

        Transform bestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider col in potentialTargets)
        {
            // Skip if the target is the player itself
            if (col.transform == playerTransform)
                continue;

            Vector3 directionToTarget = col.transform.position - playerTransform.position;
            float distance = directionToTarget.magnitude;

            // Check if target is within the lock-on cone
            Vector3 cameraForward = mainCamera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            Vector3 directionFlat = directionToTarget;
            directionFlat.y = 0;
            directionFlat.Normalize();

            float angle = Vector3.Angle(cameraForward, directionFlat);

            // Target must be within angle cone and closer than current best
            if (angle <= lockOnAngle && distance < closestDistance)
            {
                // Raycast to ensure line of sight
                if (HasLineOfSight(col.transform))
                {
                    closestDistance = distance;
                    bestTarget = col.transform;
                }
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Checks if there's an unobstructed line of sight to the target
    /// </summary>
    private bool HasLineOfSight(Transform target)
    {
        Vector3 directionToTarget = target.position - playerTransform.position;
        float distance = directionToTarget.magnitude;

        // Raycast from player chest height to target center
        Vector3 rayStart = playerTransform.position + Vector3.up * 1.5f;
        Vector3 targetPoint = target.position + Vector3.up * 1f;

        RaycastHit hit;
        if (Physics.Raycast(rayStart, (targetPoint - rayStart).normalized, out hit, distance, ~targetLayers))
        {
            // Hit something that's not the target
            return hit.transform == target;
        }

        return true; // No obstruction
    }

    #endregion

    #region Target Switching

    /// <summary>
    /// Switches target using button input (simpler - just cycles right or left)
    /// </summary>
    private void SwitchTargetButton(bool switchRight)
    {
        if (!isLocked || currentTarget == null)
            return;

        // Cooldown check to prevent rapid switching
        if (Time.time - lastSwitchTime < switchCooldown)
            return;

        Transform newTarget = FindNextTarget(switchRight);

        if (newTarget != null && newTarget != currentTarget)
        {
            currentTarget = newTarget;
            UpdateLockOnIndicator();
            lastSwitchTime = Time.time;
            Debug.Log($"Switched target to: {currentTarget.name}");
        }
    }

    /// <summary>
    /// Finds the next valid target to the left or right of the current target
    /// </summary>
    private Transform FindNextTarget(bool searchRight)
    {
        Collider[] potentialTargets = Physics.OverlapSphere(playerTransform.position, lockOnRange, targetLayers);

        // Get direction from player to current target
        Vector3 toCurrentTarget = (currentTarget.position - playerTransform.position);
        toCurrentTarget.y = 0;
        toCurrentTarget.Normalize();

        Transform bestTarget = null;
        float bestScore = Mathf.Infinity;

        foreach (Collider col in potentialTargets)
        {
            if (col.transform == currentTarget || col.transform == playerTransform)
                continue;

            Vector3 toCandidate = (col.transform.position - playerTransform.position);
            toCandidate.y = 0;
            toCandidate.Normalize();

            // Calculate signed angle (-180 to 180)
            float angle = Vector3.SignedAngle(toCurrentTarget, toCandidate, Vector3.up);

            // Check if target is in the correct direction
            bool isInCorrectDirection = searchRight ? (angle > 0) : (angle < 0);

            if (isInCorrectDirection && Mathf.Abs(angle) <= switchAngle)
            {
                // Prioritize targets closer to the switch direction
                float score = Mathf.Abs(angle);

                if (score < bestScore && HasLineOfSight(col.transform))
                {
                    bestScore = score;
                    bestTarget = col.transform;
                }
            }
        }

        return bestTarget;
    }

    #endregion

    #region Target Lock Maintenance

    /// <summary>
    /// Updates the target lock state, breaks lock if target is too far or invalid
    /// </summary>
    private void UpdateTargetLock()
    {
        // Check if target is still valid
        if (currentTarget == null)
        {
            ReleaseLock();
            return;
        }

        // Check if target is too far
        float distance = Vector3.Distance(playerTransform.position, currentTarget.position);
        if (distance > breakLockDistance)
        {
            Debug.Log("Target too far, breaking lock");
            ReleaseLock();
            return;
        }

        // Check if target is still active/alive
        if (!currentTarget.gameObject.activeInHierarchy)
        {
            Debug.Log("Target destroyed, breaking lock");
            ReleaseLock();
            return;
        }

        // 🔥 NEW: Check if target has died via Health component
        Health targetHealth = currentTarget.GetComponent<Health>();
        if (targetHealth != null && targetHealth.IsDead())
        {
            Debug.Log($"Target {currentTarget.name} died, breaking lock");
            ReleaseLock();

            // Try to auto-lock onto another nearby target if enabled
            if (autoRelockOnDeath)
            {
                TryAutoRelock();
            }
            return;
        }
    }

    /// <summary>
    /// Rotates the player to face the locked target
    /// Skips rotation during dash to maintain dash direction
    /// </summary>
    private void RotateTowardsTarget()
    {
        if (currentTarget == null)
            return;

        // Don't rotate during dash - let the dash complete in its original direction
        if (playerController != null && playerController.IsDashing())
            return;

        // Calculate direction to target (flatten Y to keep player upright)
        Vector3 directionToTarget = currentTarget.position - playerTransform.position;
        directionToTarget.y = 0;

        if (directionToTarget.sqrMagnitude < 0.01f)
            return; // Too close, don't rotate

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        if (smoothRotation)
        {
            playerTransform.rotation = Quaternion.Slerp(
                playerTransform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
        else
        {
            playerTransform.rotation = targetRotation;
        }
    }

    #endregion

    #region Visual Feedback

    /// <summary>
    /// Creates the lock-on indicator above the target
    /// </summary>
    private void CreateLockOnIndicator()
    {
        if (lockOnIndicatorPrefab == null || currentTarget == null)
            return;

        lockOnIndicator = Instantiate(lockOnIndicatorPrefab);
        UpdateLockOnIndicator();
    }

    /// <summary>
    /// Updates the lock-on indicator position
    /// </summary>
    private void UpdateLockOnIndicator()
    {
        if (lockOnIndicator != null && currentTarget != null)
        {
            lockOnIndicator.transform.position = currentTarget.position + indicatorOffset;
            lockOnIndicator.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
        }
    }

    /// <summary>
    /// Destroys the lock-on indicator
    /// </summary>
    private void DestroyLockOnIndicator()
    {
        if (lockOnIndicator != null)
        {
            Destroy(lockOnIndicator);
            lockOnIndicator = null;
        }
    }

    #endregion

    #region Auto Relock

    /// <summary>
    /// Attempts to automatically lock onto another nearby target when current target dies
    /// </summary>
    private void TryAutoRelock()
    {
        Transform newTarget = FindNearestTarget();

        if (newTarget != null)
        {
            currentTarget = newTarget;
            isLocked = true;
            CreateLockOnIndicator();
            Debug.Log($"Auto-relocked onto: {currentTarget.name}");
        }
        else
        {
            Debug.Log("No nearby targets for auto-relock");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Manually set a specific target (useful for boss transitions, forced locks, etc.)
    /// </summary>
    public void SetTarget(Transform target)
    {
        if (target == null)
        {
            ReleaseLock();
            return;
        }

        currentTarget = target;
        isLocked = true;
        UpdateLockOnIndicator();
        Debug.Log($"Target manually set to: {currentTarget.name}");
    }

    /// <summary>
    /// Get the direction from player to target (for PlayerController to use)
    /// </summary>
    public Vector3 GetDirectionToTarget()
    {
        if (!isLocked || currentTarget == null)
            return playerTransform.forward;

        Vector3 direction = currentTarget.position - playerTransform.position;
        direction.y = 0;
        return direction.normalized;
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || playerTransform == null)
            return;

        // Draw lock-on range sphere
        Gizmos.color = lockRangeColor;
        Gizmos.DrawWireSphere(playerTransform.position, lockOnRange);

        // Draw break distance sphere
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Gizmos.DrawWireSphere(playerTransform.position, breakLockDistance);

        // Draw lock-on cone
        if (mainCamera != null)
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            Vector3 leftBound = Quaternion.Euler(0, -lockOnAngle, 0) * cameraForward * lockOnRange;
            Vector3 rightBound = Quaternion.Euler(0, lockOnAngle, 0) * cameraForward * lockOnRange;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(playerTransform.position, playerTransform.position + leftBound);
            Gizmos.DrawLine(playerTransform.position, playerTransform.position + rightBound);
            Gizmos.DrawLine(playerTransform.position, playerTransform.position + cameraForward * lockOnRange);
        }

        // Draw line to current target
        if (isLocked && currentTarget != null)
        {
            Gizmos.color = lockedTargetColor;
            Gizmos.DrawLine(playerTransform.position, currentTarget.position);
            Gizmos.DrawWireSphere(currentTarget.position, 1f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || !isLocked || currentTarget == null)
            return;

        // Draw detailed target info
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(currentTarget.position + indicatorOffset, 0.3f);

        // Draw line of sight check
        Vector3 rayStart = playerTransform.position + Vector3.up * 1.5f;
        Vector3 targetPoint = currentTarget.position + Vector3.up * 1f;
        Gizmos.color = HasLineOfSight(currentTarget) ? Color.green : Color.red;
        Gizmos.DrawLine(rayStart, targetPoint);
    }

    #endregion

    private void OnDisable()
    {
        if (controls != null)
        {
            controls.Player.TargetLock.performed -= ctx => ToggleTargetLock();
            controls.Player.SwitchTarget.performed -= OnSwitchTargetInput;
        }

        ReleaseLock();
    }
}