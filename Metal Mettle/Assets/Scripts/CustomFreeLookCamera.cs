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

    [Header("Input")]
    public InputSystem_Actions controls;

    private float currentPitch = 0f;
    private bool usingGamepad = false;

    private void Start()
    {
        controls = InputManager.Instance.Controls;
        controls.Enable();
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

        // Calculate camera position
        Quaternion rotation = Quaternion.Euler(currentPitch, player.eulerAngles.y, 0f);
        Vector3 backOffset = rotation * Vector3.back * distance;
        Vector3 targetPos = focalPoint.position + backOffset;

        // Smoothly move and rotate camera
        cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, Time.deltaTime * transitionSpeed);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rotation, Time.deltaTime * transitionSpeed);
    }
}