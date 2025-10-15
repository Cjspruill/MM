using UnityEngine;
using UnityEngine.InputSystem;

public class CustomFreeLookCamera : MonoBehaviour, ICutsceneControllable
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
    public float castStartOffset = 0.5f; // Start cast slightly away from focal point
    public LayerMask collisionLayers = -1;
    public float collisionSmoothing = 10f;

    [Header("Input")]
    public InputSystem_Actions controls;

    private float currentPitch = 0f;
    private bool usingGamepad = false;
    private float currentDistance;
    private bool isInCutscene = false; // Track cutscene state

    private void Start()
    {
        controls = InputManager.Instance.Controls;
        controls.Enable();
        currentDistance = distance;
    }

    private void OnEnable()
    {
        // Re-enable controls when script is enabled (unless we're in a cutscene)
        if (controls != null && !isInCutscene)
        {
            controls.Enable();
            Debug.Log("CustomFreeLookCamera: Input controls enabled");
        }
    }

    private void OnDisable()
    {
        if (controls != null)
        {
            controls.Disable();
            Debug.Log("CustomFreeLookCamera: Input controls disabled");
        }
    }

    #region ICutsceneControllable Implementation

    public void OnCutsceneStart()
    {
        Debug.Log("CustomFreeLookCamera: Cutscene started - Disabling input");
        isInCutscene = true;

        if (controls != null)
        {
            controls.Disable();
        }

        // Store the player's current rotation so we can restore it
        if (player != null)
        {
            Debug.Log($"Storing player rotation: {player.eulerAngles.y}");
        }
    }

    public void OnCutsceneEnd()
    {
        Debug.Log("CustomFreeLookCamera: Cutscene ended - Re-enabling input");
        isInCutscene = false;

        if (controls != null)
        {
            controls.Enable();
        }

        // Player rotation will naturally resume from where it was
    }

    #endregion

    private void LateUpdate()
    {
        // CRITICAL: Skip ALL camera updates during cutscenes
        // This must be the FIRST check before any other code runs
        if (isInCutscene)
        {
            Debug.LogWarning("LateUpdate running during cutscene! This shouldn't happen.");
            return;
        }

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

        // Start cast slightly away from focal point to avoid self-collision
        Vector3 castStart = focalPoint.position + desiredDirection * castStartOffset;
        float castDistance = distance - castStartOffset;

        // Check for collisions and adjust distance
        float targetDistance = distance;
        RaycastHit hit;

        if (Physics.SphereCast(
            castStart,
            collisionRadius,
            desiredDirection,
            out hit,
            castDistance,
            collisionLayers,
            QueryTriggerInteraction.Ignore)) // Ignore triggers
        {
            // Push camera in when hitting something
            targetDistance = Mathf.Max(hit.distance + castStartOffset, minDistance);
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
        Vector3 castStart = focalPoint.position + direction * castStartOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(castStart, collisionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(castStart, castStart + direction * (distance - castStartOffset));
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(focalPoint.position + direction * currentDistance, collisionRadius);
    }
}