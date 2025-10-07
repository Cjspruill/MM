using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    private CharacterController controller;
    private InputSystem_Actions controls;
    public BloodSystem bloodSystem;
    public ComboController comboController;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float sprintBloodCost = 0.5f; // Blood drained per second while sprinting
    public float sprintStopTime = 0.2f; // Can't attack immediately after sprint
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Combat Movement")]
    public bool allowMovementDuringAttack = false; // Toggle for movement during attacks
    public bool allowMovementDuringBlock = false; // Toggle for movement during block
    public float attackMovementSpeedMultiplier = 0.3f; // Slow movement if allowed during attack

    private Vector3 velocity;
    private bool isGrounded;
    private bool justStoppedSprinting = false;
    private bool wasSprinting = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        controls = InputManager.Instance.Controls;
        controls.Enable();

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        // Check if grounded
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
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

        // Can't move during recovery (already existed)
        if (inRecovery)
        {
            blockMovement = true;
        }

        // If movement is blocked, only apply gravity
        if (blockMovement)
        {
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // Get movement input
        Vector2 moveInput = controls.Player.Move.ReadValue<Vector2>();

        // Check if sprinting (can't sprint during attacks/blocks)
        bool wantsToSprint = controls.Player.Sprint.IsPressed();
        bool isSprinting = wantsToSprint && !isAttacking && !isBlocking;

        // Calculate current speed
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
        currentSpeed *= movementMultiplier; // Apply combat multiplier if attacking

        // Track sprint state for stop delay
        if (wasSprinting && !isSprinting && !justStoppedSprinting)
        {
            justStoppedSprinting = true;
            Invoke(nameof(AllowActions), sprintStopTime);
        }
        wasSprinting = isSprinting;

        // Drain blood while sprinting and moving
        if (isSprinting && moveInput.magnitude > 0.1f && bloodSystem != null)
        {
            bloodSystem.DrainBlood(sprintBloodCost * Time.deltaTime);
        }

        // Calculate movement direction relative to camera
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        // Flatten camera directions (ignore Y component)
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        // Calculate move direction
        Vector3 moveDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;

        // Move the character
        controller.Move(moveDirection * currentSpeed * Time.deltaTime);

        // Rotation handling
        if (isAttacking || isBlocking)
        {
            // Lock rotation to camera forward during combat
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }
        else
        {
            // Normal rotation - face camera forward
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }

        // Jump (can't jump during attacks/blocks)
        if (controls.Player.Jump.triggered && isGrounded && !isAttacking && !isBlocking)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void AllowActions()
    {
        justStoppedSprinting = false;
    }

    public bool CanAct() => !justStoppedSprinting;

    void OnDisable()
    {
        controls.Disable();
    }
}