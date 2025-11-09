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

    [Header("Combat Movement")]
    public bool allowMovementDuringAttack = false;
    public bool allowMovementDuringBlock = false;
    public float attackMovementSpeedMultiplier = 0.3f;

    [Header("Cursor Settings")]
    public bool lockCursorOnAttack = true;

    private Vector3 velocity;
    private bool isGrounded;
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
        controls = InputManager.Instance.Controls;
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

        // Check if grounded
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

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

        // Rotation - always face camera forward
        if (cameraForward.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }

        // Jump
        if (controls.Player.Jump.triggered && isGrounded && !isAttacking && !isBlocking)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
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

    void OnDisable()
    {
        if (controls != null)
        {
            controls.Player.Attack.performed -= OnAttackInput;
            controls.Disable();
            Debug.Log("PlayerController: Input controls disabled");
        }
    }
}