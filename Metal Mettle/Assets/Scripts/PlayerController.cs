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

        // Can't move during recovery
        if (comboController != null && comboController.IsInRecovery())
        {
            // Apply gravity only
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // Get movement input
        Vector2 moveInput = controls.Player.Move.ReadValue<Vector2>();

        // Check if sprinting
        bool isSprinting = controls.Player.Sprint.IsPressed();
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

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

        // Always face camera forward direction
        Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);

        // Jump
        if (controls.Player.Jump.triggered && isGrounded)
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