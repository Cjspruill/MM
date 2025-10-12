using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    private CharacterController controller;
    private Animator animator;
    private InputSystem_Actions controls;
    public BloodSystem bloodSystem;
    public ComboController comboController;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float sprintBloodCost = 0.5f;
    public float sprintStopTime = 0.2f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Animation Settings")]
    public float animationSmoothTime = 0.1f; // How quickly animation values interpolate
    public float walkAnimationValue = 0.5f; // Animation value for walking
    public float sprintAnimationValue = 1f; // Animation value for sprinting

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

    // Animation smoothing
    private float currentAnimSpeed; // Direction Y (forward/back)
    private float currentAnimDirection; // Direction X (left/right strafe)
    private float animSpeedVelocity;
    private float animDirectionVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        controls = InputManager.Instance.Controls;
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
    }

    void OnAttackInput(InputAction.CallbackContext context)
    {
        // Lock cursor if not already locked
        if (Cursor.lockState != CursorLockMode.Locked && !PauseController.Instance.IsPaused())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("Cursor locked and hidden");
        }
    }

    void Update()
    {
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

        // Set animator parameters
        animator.SetFloat("Speed", currentAnimSpeed);      // Y-axis (forward/back)
        animator.SetFloat("Direction", currentAnimDirection); // X-axis (left/right)
    }

    void AllowActions()
    {
        justStoppedSprinting = false;
    }

    public bool CanAct() => !justStoppedSprinting;

    void OnDisable()
    {
        if (lockCursorOnAttack)
        {
            controls.Player.Attack.performed -= OnAttackInput;
        }
        controls.Disable();
    }
}