using UnityEngine;
using UnityEngine.InputSystem;

public class CustomFreeLookCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera cam;
    public Transform focalPoint;

    [Header("Settings")]
    public float distance = 5f;
    public float lookSpeed = 120f;
    public float minPitch = -30f;
    public float maxPitch = 60f;
    public float transitionSpeed = 10f;

    [Header("Collision")]
    public float collisionRadius = 0.3f;
    public float minDistance = 1f;
    public LayerMask collisionLayers = -1;
    public float collisionSmoothing = 10f;

    [Header("Input")]
    public InputSystem_Actions controls;

    private float currentPitch = 0f;
    private bool usingGamepad = false;
    private float currentDistance;

    private void Start()
    {
        controls = InputManager.Instance.Controls;
        controls.Enable();
        currentDistance = distance;
    }

    private void OnDisable() => controls.Disable();

    private void LateUpdate()
    {
        if (!player || !focalPoint) return;

        // Detect input device
        InputDevice device = controls.Player.Look.activeControl?.device;
        usingGamepad = device is Gamepad;

        // Handle look input
        Vector2 lookInput = InputManager.Instance.GetLookInput();
        float sensitivity = usingGamepad ? 3f : 1f;

        // Rotate player horizontally
        player.Rotate(Vector3.up, lookInput.x * lookSpeed * sensitivity * Time.deltaTime);

        // Update pitch
        currentPitch -= lookInput.y * lookSpeed * sensitivity * Time.deltaTime;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        // Calculate desired camera position
        Quaternion rotation = Quaternion.Euler(currentPitch, player.eulerAngles.y, 0f);
        Vector3 desiredDirection = rotation * Vector3.back;

        // Check for collisions and adjust distance
        float targetDistance = distance;
        RaycastHit hit;

        if (Physics.SphereCast(
            focalPoint.position,
            collisionRadius,
            desiredDirection,
            out hit,
            distance,
            collisionLayers))
        {
            // Push camera in when hitting something
            targetDistance = Mathf.Max(hit.distance - collisionRadius, minDistance);
        }

        // Smoothly interpolate the actual distance
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * collisionSmoothing);

        // Calculate final camera position with adjusted distance
        Vector3 targetPos = focalPoint.position + desiredDirection * currentDistance;

        // Smoothly move and rotate camera
        cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, Time.deltaTime * transitionSpeed);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rotation, Time.deltaTime * transitionSpeed);
    }

    // Optional: Visualize collision detection in editor
    private void OnDrawGizmosSelected()
    {
        if (focalPoint == null) return;

        Quaternion rotation = Quaternion.Euler(currentPitch, player ? player.eulerAngles.y : 0f, 0f);
        Vector3 direction = rotation * Vector3.back;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(focalPoint.position, collisionRadius);
        Gizmos.DrawLine(focalPoint.position, focalPoint.position + direction * distance);
        Gizmos.DrawWireSphere(focalPoint.position + direction * currentDistance, collisionRadius);
    }
}