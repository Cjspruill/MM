using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, ICutsceneControllable
{
    [Header("References")]
    public Transform cameraTransform;
    private CharacterController controller;
    private Animator animator;
    private InputSystem_Actions controls;
    public BloodSystem bloodSystem;
    public ComboController comboController;
    public TutorialManager tutorialManager;
    public TargetLockSystem targetLockSystem; // Reference to target lock system

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float sprintBloodCost = 0.5f;
    public float sprintStopTime = 0.2f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Animation Settings")]
    public float animationSmoothTime = 0.1f;
    public float walkAnimationValue = 0.5f;
    public float sprintAnimationValue = 1f;
    [Tooltip("Name of the bool parameter in the Animator for cutscene state")]
    public string cutsceneAnimatorBool = "InCutscene";
    [Tooltip("Name of the trigger parameter in the Animator for jumping")]
    public string jumpAnimatorTrigger = "Jump";
    [Tooltip("Name of the bool parameter in the Animator for in-air state")]
    public string inAirAnimatorBool = "InAir";

    [Header("Combat Movement")]
    public bool allowMovementDuringAttack = false;
    public bool allowMovementDuringBlock = false;
    public float attackMovementSpeedMultiplier = 0.3f;
    [Tooltip("Movement speed multiplier when locked onto a target (0.25 = 25% speed)")]
    public float targetLockMovementMultiplier = 0.25f; // 75% reduction = 25% speed

    [Header("Cursor Settings")]
    public bool lockCursorOnAttack = true;

    [Header("Ground Check Settings")]
    [Tooltip("Distance to check for ground below player")]
    public float groundCheckDistance = 0.3f;
    [Tooltip("Layer mask for what counts as ground")]
    public LayerMask groundLayer = -1; // Default to everything

    [Header("Dash Settings (Target Lock Only)")]
    [Tooltip("Distance to dash in each direction")]
    public float dashDistance = 5f; // Increased from 3f to 5f for more noticeable movement
    [Tooltip("How long the dash takes")]
    public float dashDuration = 0.3f;
    [Tooltip("Animation curve for dash movement (ease in/out)")]
    public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Cooldown between dashes")]
    public float dashCooldown = 0.5f;
    [Tooltip("Name of the trigger parameter in Animator for dash")]
    public string dashAnimatorTrigger = "Dash";
    [Tooltip("Can dash while attacking?")]
    public bool canDashWhileAttacking = false;
    [Tooltip("Use circular dash that orbits around target instead of straight line?")]
    public bool useCircularDash = true;
    [Tooltip("Degrees to rotate around target when dashing left/right (if circular dash enabled)")]
    public float circularDashAngle = 45f;

    private Vector3 velocity;
    private bool isGrounded;
    private bool wasGrounded; // Track previous grounded state for landing detection
    private bool isJumping = false;
    private bool wantsToJump = false; // Flag for when player presses jump button
    private bool isDashing = false;
    private float dashTimeRemaining = 0f;
    private Vector3 dashDirection;
    private float lastDashTime = -999f;

    // Circular dash variables
    private bool isCircularDash = false;
    private Vector3 circularDashCenter; // Target position we're orbiting around
    private float circularDashStartAngle; // Starting angle
    private float circularDashEndAngle; // Ending angle
    private float circularDashRadius; // Distance from target

    private bool justStoppedSprinting = false;
    private bool wasSprinting = false;
    private bool isInCutscene = false;
    private Quaternion lockedRotation;

    // Animation smoothing
    private float currentAnimSpeed;
    private float currentAnimDirection;
    private float animSpeedVelocity;
    private float animDirectionVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        controls = InputManager.Instance.controls;
        tutorialManager = FindFirstObjectByType<TutorialManager>();
        controls.Enable();

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (animator == null)
        {
            Debug.LogError("Animator component not found on " + gameObject.name);
        }

        // Subscribe to attack input
        if (lockCursorOnAttack)
        {
            controls.Player.Attack.performed += OnAttackInput;
        }

        // **FIXED: Lock cursor on game start**
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("PlayerController: Cursor locked on start");
    }

    private void OnEnable()
    {
        // Re-enable controls when script is enabled (unless we're in a cutscene)
        if (controls != null && !isInCutscene)
        {
            controls.Enable();
            Debug.Log("PlayerController: Input controls enabled");
        }
    }

    #region ICutsceneControllable Implementation

    public void OnCutsceneStart()
    {
        Debug.Log("PlayerController: Cutscene started - Disabling input");
        isInCutscene = true;

        if (controls != null)
        {
            controls.Disable();
        }

        // 🎬 SET THE CUTSCENE ANIMATOR BOOL TO TRUE
        if (animator != null)
        {
            animator.SetBool(cutsceneAnimatorBool, true);
            Debug.Log($"✅ PlayerController: Set animator bool '{cutsceneAnimatorBool}' to TRUE");

            // Reset animation to idle during cutscene (set parameters to zero)
            UpdateAnimationParameters(0f, 0f);
        }
    }

    public void OnCutsceneEnd()
    {
        Debug.Log("PlayerController: Cutscene ended - Re-enabling input");
        isInCutscene = false;

        if (controls != null)
        {
            controls.Enable();
        }

        // **FIXED: Re-lock cursor after cutscene**
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("PlayerController: Cursor re-locked after cutscene");

        // 🎬 SET THE CUTSCENE ANIMATOR BOOL BACK TO FALSE
        if (animator != null)
        {
            animator.SetBool(cutsceneAnimatorBool, false);
            Debug.Log($"✅ PlayerController: Set animator bool '{cutsceneAnimatorBool}' to FALSE");

            // CRITICAL: Force animation parameters to update immediately
            // This prevents the "stuck in idle" bug after cutscene ends
            currentAnimSpeed = 0f;
            currentAnimDirection = 0f;
            animSpeedVelocity = 0f;
            animDirectionVelocity = 0f;
        }
    }

    #endregion

    void OnAttackInput(InputAction.CallbackContext context)
    {
        if (isInCutscene) return;
        if (TutorialManager.IsTutorialActive) return;

        if (Cursor.lockState != CursorLockMode.Locked && !PauseController.Instance.IsPaused())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("Cursor locked and hidden");
        }
    }

    void Update()
    {
        // Don't process input during cutscenes
        if (isInCutscene)
        {
            // Still apply gravity during cutscene
            if (!isGrounded)
            {
                velocity.y += gravity * Time.deltaTime;
                controller.Move(velocity * Time.deltaTime);
            }
            return;
        }

        if (TutorialManager.IsTutorialActive)
            return;

        // Store previous grounded state for landing detection
        wasGrounded = isGrounded;

        // Check if grounded - using CharacterController's built-in check AND raycast for reliability
        isGrounded = controller.isGrounded;

        // Additional ground check using raycast for more accuracy
        // This helps prevent the "floating" issue where CharacterController thinks we're still grounded
        if (!isGrounded)
        {
            RaycastHit hit;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f; // Slightly above bottom
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance + controller.skinWidth, groundLayer))
            {
                isGrounded = true;
            }
        }

        // Reset vertical velocity when grounded - use a smaller value to reduce "stickiness"
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -0.5f; // Reduced from -2f to make landing detection faster
        }

        // ⚡ UPDATE JUMP ANIMATION FIRST - BEFORE MOVEMENT
        UpdateJumpAnimation();

        // Check combat state from ComboController
        bool isAttacking = comboController != null && comboController.IsAttacking();
        bool isBlocking = comboController != null && comboController.IsBlocking();
        bool inRecovery = comboController != null && comboController.IsInRecovery();

        // Determine if movement should be blocked
        bool blockMovement = false;
        float movementMultiplier = 1f;

        if (isAttacking && !allowMovementDuringAttack)
        {
            blockMovement = true;
        }
        else if (isAttacking && allowMovementDuringAttack)
        {
            movementMultiplier = attackMovementSpeedMultiplier;
        }

        if (isBlocking && !allowMovementDuringBlock)
        {
            blockMovement = true;
        }

        if (inRecovery)
        {
            blockMovement = true;
        }

        // Get movement input
        Vector2 moveInput = controls.Player.Move.ReadValue<Vector2>();

        // If movement is blocked, smoothly return animation values to zero
        if (blockMovement)
        {
            UpdateAnimationParameters(0f, 0f);
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // Check if sprinting
        bool wantsToSprint = controls.Player.Sprint.IsPressed();
        bool isSprinting = wantsToSprint && !isAttacking && !isBlocking && moveInput.magnitude > 0.1f;

        // Calculate current speed
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
        currentSpeed *= movementMultiplier;

        // Apply target lock movement reduction
        if (targetLockSystem != null && targetLockSystem.IsLocked)
        {
            currentSpeed *= targetLockMovementMultiplier;
        }

        // Track sprint state
        if (wasSprinting && !isSprinting && !justStoppedSprinting)
        {
            justStoppedSprinting = true;
            Invoke(nameof(AllowActions), sprintStopTime);
        }
        wasSprinting = isSprinting;

        // Drain blood while sprinting
        if (isSprinting && moveInput.magnitude > 0.1f && bloodSystem != null)
        {
            bloodSystem.DrainBlood(sprintBloodCost * Time.deltaTime);
        }

        // Calculate movement direction relative to camera
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;

        // Move the character
        if (moveInput.magnitude > 0.1f)
        {
            controller.Move(moveDirection * currentSpeed * Time.deltaTime);
        }

        // Calculate animation values based on input and sprint state
        float animValue = isSprinting ? sprintAnimationValue : walkAnimationValue;

        float targetSpeed = 0f; // Speed (forward/back - Y axis)
        float targetDirection = 0f; // Direction (left/right strafe - X axis)

        if (moveInput.magnitude > 0.1f)
        {
            // Speed = forward/backward movement (Y input)
            targetSpeed = moveInput.y * animValue;

            // Direction = left/right strafe (X input)
            targetDirection = moveInput.x * animValue;
        }

        // Update animation parameters
        UpdateAnimationParameters(targetSpeed, targetDirection);

        // Rotation - face target if locked, otherwise face camera forward
        if (targetLockSystem != null && targetLockSystem.IsLocked)
        {
            // Target lock handles rotation in its LateUpdate - do nothing here
            // This allows smooth rotation towards the locked target
        }
        else
        {
            // Normal behavior - face camera forward
            if (cameraForward.magnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }

        // Jump OR Dash - depends on lock-on state
        if (controls.Player.Jump.triggered && isGrounded && !isAttacking && !isBlocking)
        {
            // Check if we're locked onto a target
            bool isLockedOn = targetLockSystem != null && targetLockSystem.IsLocked;

            if (isLockedOn)
            {
                // DASH when locked on
                TryDash(moveInput);
            }
            else
            {
                // JUMP when not locked on
                wantsToJump = true;

                // 🎯 TRIGGER JUMP ANIMATION IMMEDIATELY
                if (animator != null)
                {
                    animator.SetTrigger(jumpAnimatorTrigger);
                }
            }
        }

        // Handle ongoing dash movement
        if (isDashing)
        {
            HandleDashMovement();
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// Updates the in-air animation parameter for falling/landing states
    /// The jump trigger handles the jump start animation
    /// </summary>
    void UpdateJumpAnimation()
    {
        if (animator == null) return;

        // Set InAir bool - this will be false as soon as we're grounded
        animator.SetBool(inAirAnimatorBool, !isGrounded);

        // If we just landed (transitioned from air to ground), ensure animation updates immediately
        if (!wasGrounded && isGrounded)
        {
            // Force the animator to update immediately on landing
            animator.Update(0f);
        }
    }

    void UpdateAnimationParameters(float targetSpeed, float targetDirection)
    {
        if (animator == null) return;

        // Smoothly interpolate to target values
        currentAnimSpeed = Mathf.SmoothDamp(
            currentAnimSpeed,
            targetSpeed,
            ref animSpeedVelocity,
            animationSmoothTime
        );

        currentAnimDirection = Mathf.SmoothDamp(
            currentAnimDirection,
            targetDirection,
            ref animDirectionVelocity,
            animationSmoothTime
        );

        // Set animator parameters - YOUR ANIMATOR USES "Speed" and "Direction"
        animator.SetFloat("Speed", currentAnimSpeed);
        animator.SetFloat("Direction", currentAnimDirection);
    }

    void AllowActions()
    {
        justStoppedSprinting = false;
    }

    public bool CanAct() => !justStoppedSprinting;

    /// <summary>
    /// Public property for other systems to check if player is currently dashing
    /// </summary>
    public bool IsDashing() => isDashing;

    #region Animation Event Methods

    /// <summary>
    /// Called by Animation Event in the jump animation at the moment Marcus pushes off the ground
    /// Add this to your jump animation at the exact frame where he leaves the ground
    /// </summary>
    public void ApplyJumpVelocity()
    {
        if (wantsToJump && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            wantsToJump = false;
            isJumping = true;
            Debug.Log("✅ Jump velocity applied via animation event");
        }
    }

    /// <summary>
    /// Optional: Called by Animation Event when the land animation completes
    /// Add this to your land animation when you want to allow the next jump
    /// </summary>
    public void OnLandComplete()
    {
        isJumping = false;
        Debug.Log("✅ Land animation complete");
    }

    #endregion

    #region Dash System

    /// <summary>
    /// Attempts to initiate a dash in the direction of current movement input
    /// When locked on, left/right can either strafe or orbit around target based on useCircularDash
    /// </summary>
    private void TryDash(Vector2 moveInput)
    {
        // Check cooldown
        if (Time.time - lastDashTime < dashCooldown)
        {
            Debug.Log("Dash on cooldown");
            return;
        }

        // Check if we can dash (not while attacking unless allowed)
        bool isAttacking = comboController != null && comboController.IsAttacking();
        if (isAttacking && !canDashWhileAttacking)
        {
            Debug.Log("Cannot dash while attacking");
            return;
        }

        // Determine if this should be a circular dash (left/right when locked on)
        bool shouldBeCircular = useCircularDash && targetLockSystem != null &&
                                targetLockSystem.IsLocked && targetLockSystem.CurrentTarget != null &&
                                Mathf.Abs(moveInput.x) > 0.1f;

        if (shouldBeCircular)
        {
            InitCircularDash(moveInput.x > 0); // true = right, false = left
        }
        else
        {
            InitLinearDash(moveInput);
        }

        // Common dash initialization
        isDashing = true;
        dashTimeRemaining = dashDuration;
        lastDashTime = Time.time;

        // Trigger dash animation
        if (animator != null)
        {
            animator.SetTrigger(dashAnimatorTrigger);
        }
    }

    /// <summary>
    /// Initialize a circular dash that orbits around the locked target
    /// </summary>
    private void InitCircularDash(bool dashRight)
    {
        isCircularDash = true;

        // Get target position
        circularDashCenter = targetLockSystem.CurrentTarget.position;
        circularDashCenter.y = transform.position.y; // Keep on same Y plane

        // Calculate current angle and radius from target
        Vector3 toPlayer = transform.position - circularDashCenter;
        toPlayer.y = 0;
        circularDashRadius = toPlayer.magnitude;

        // Calculate start angle (current position)
        circularDashStartAngle = Mathf.Atan2(toPlayer.z, toPlayer.x) * Mathf.Rad2Deg;

        // Calculate end angle (rotate by circularDashAngle)
        float angleChange = dashRight ? circularDashAngle : -circularDashAngle;
        circularDashEndAngle = circularDashStartAngle + angleChange;

        Debug.Log($"🔵 CIRCULAR DASH: {(dashRight ? "RIGHT" : "LEFT")} - Angle: {circularDashStartAngle:F1}° → {circularDashEndAngle:F1}° (Radius: {circularDashRadius:F2}m)");
    }

    /// <summary>
    /// Initialize a standard linear dash
    /// </summary>
    private void InitLinearDash(Vector2 moveInput)
    {
        isCircularDash = false;

        Vector3 dashDir;

        if (moveInput.magnitude < 0.1f)
        {
            // No input - dash backwards (away from target)
            dashDir = -transform.forward;
            Debug.Log("🔵 LINEAR DASH: Backwards (no input)");
        }
        else
        {
            // Calculate dash direction RELATIVE TO PLAYER'S FACING
            Vector3 playerForward = transform.forward;
            Vector3 playerRight = transform.right;

            playerForward.y = 0f;
            playerRight.y = 0f;
            playerForward.Normalize();
            playerRight.Normalize();

            // For pure left/right movement, use ONLY the right vector
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            {
                dashDir = playerRight * moveInput.x;
                Debug.Log($"🔵 LINEAR DASH: Pure {(moveInput.x > 0 ? "RIGHT" : "LEFT")} strafe");
            }
            else if (Mathf.Abs(moveInput.y) > Mathf.Abs(moveInput.x))
            {
                dashDir = playerForward * moveInput.y;
                Debug.Log($"🔵 LINEAR DASH: Pure {(moveInput.y > 0 ? "FORWARD" : "BACKWARD")}");
            }
            else
            {
                dashDir = (playerForward * moveInput.y + playerRight * moveInput.x).normalized;
                Debug.Log($"🔵 LINEAR DASH: Diagonal ({moveInput})");
            }
        }

        dashDirection = dashDir;
    }

    /// <summary>
    /// Handles the actual dash movement each frame
    /// </summary>
    private void HandleDashMovement()
    {
        if (dashTimeRemaining <= 0f)
        {
            isDashing = false;
            isCircularDash = false;
            return;
        }

        // Calculate how far through the dash we are (0 to 1)
        float normalizedTime = 1f - (dashTimeRemaining / dashDuration);

        if (isCircularDash)
        {
            // Circular dash - move along an arc around the target
            float currentAngle = Mathf.Lerp(circularDashStartAngle, circularDashEndAngle, dashCurve.Evaluate(normalizedTime));

            // Calculate position on the circle
            float angleRad = currentAngle * Mathf.Deg2Rad;
            Vector3 targetPosition = circularDashCenter + new Vector3(
                Mathf.Cos(angleRad) * circularDashRadius,
                0,
                Mathf.Sin(angleRad) * circularDashRadius
            );
            targetPosition.y = transform.position.y; // Maintain Y position

            // Move to the target position
            Vector3 movement = targetPosition - transform.position;
            controller.Move(movement);

            Debug.Log($"⚡ Circular Dash: Angle = {currentAngle:F1}°, Movement = {movement}");
        }
        else
        {
            // Linear dash - move in a straight line
            float curveValue = dashCurve.Evaluate(normalizedTime);
            float previousCurveValue = normalizedTime > 0 ? dashCurve.Evaluate(normalizedTime - (Time.deltaTime / dashDuration)) : 0f;
            float deltaMovement = (curveValue - previousCurveValue) * dashDistance;

            Vector3 movement = dashDirection * deltaMovement;
            controller.Move(movement);

            Debug.Log($"⚡ Linear Dash: Movement = {movement}, Delta = {deltaMovement}");
        }

        // Countdown the dash time
        dashTimeRemaining -= Time.deltaTime;

        // End dash if time is up
        if (dashTimeRemaining <= 0f)
        {
            isDashing = false;
            isCircularDash = false;
            Debug.Log("✅ Dash complete");
        }
    }

    #endregion

    void OnDisable()
    {
        if (controls != null)
        {
            controls.Player.Attack.performed -= OnAttackInput;
            controls.Disable();
            Debug.Log("PlayerController: Input controls disabled");
        }
    }

    // Debug visualization for ground check and dash
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw ground check ray
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * groundCheckDistance);
        Gizmos.DrawWireSphere(rayOrigin + Vector3.down * groundCheckDistance, 0.1f);

        // Draw dash visualization
        if (isDashing)
        {
            if (isCircularDash)
            {
                // Draw circular dash arc
                Gizmos.color = Color.cyan;

                // Draw the center point (target)
                Gizmos.DrawWireSphere(circularDashCenter, 0.3f);

                // Draw the arc path
                int segments = 20;
                Vector3 previousPoint = Vector3.zero;
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float angle = Mathf.Lerp(circularDashStartAngle, circularDashEndAngle, t);
                    float angleRad = angle * Mathf.Deg2Rad;

                    Vector3 point = circularDashCenter + new Vector3(
                        Mathf.Cos(angleRad) * circularDashRadius,
                        transform.position.y,
                        Mathf.Sin(angleRad) * circularDashRadius
                    );

                    if (i > 0)
                    {
                        Gizmos.DrawLine(previousPoint, point);
                    }
                    previousPoint = point;
                }

                // Draw current position
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.2f);
            }
            else
            {
                // Draw linear dash direction
                Gizmos.color = Color.cyan;
                Vector3 dashStart = transform.position + Vector3.up * 1f;
                Vector3 dashEnd = dashStart + dashDirection * dashDistance;
                Gizmos.DrawLine(dashStart, dashEnd);
                Gizmos.DrawWireSphere(dashEnd, 0.3f);

                // Draw player forward and right for reference
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(dashStart, transform.forward * 2f);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(dashStart, transform.right * 2f);
            }
        }
    }
}