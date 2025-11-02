using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Door/barrier that unlocks when specific objective tasks are completed
/// Can slide, rotate, fade out, or simply disable
/// Can optionally trigger a cutscene camera when unlocked
/// </summary>
public class ObjectiveDoor : MonoBehaviour
{
    [Header("Objective Requirements")]
    [SerializeField] private ObjectiveController objectiveController;
    [Tooltip("The objective name that must be complete to unlock this door")]
    [SerializeField] private string requiredObjectiveName = "Tutorial Combat";
    [SerializeField] private bool checkOnUpdate = true;
    [SerializeField] private float checkInterval = 0.5f;

    [Header("Door Type")]
    [SerializeField] private DoorType doorType = DoorType.Slide;

    [Header("Slide Settings")]
    [SerializeField] private Vector3 slideDirection = Vector3.up;
    [SerializeField] private float slideDistance = 5f;
    [SerializeField] private float slideDuration = 2f;

    [Header("Rotate Settings")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationAngle = 90f;
    [SerializeField] private float rotateDuration = 2f;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 2f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject lockedEffect;
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material unlockedMaterial;
    [SerializeField] private Color lockedColor = Color.red;
    [SerializeField] private Color unlockedColor = Color.green;
    [SerializeField] private bool pulseWhenLocked = true;
    [SerializeField] private float pulseSpeed = 2f;

    [Header("Audio")]
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Cutscene Settings")]
    [Tooltip("Optional: Cutscene camera controller to focus on the door when it unlocks")]
    [SerializeField] private CutsceneCameraController cutsceneCamera;
    [SerializeField] private Transform focusTarget;
    [SerializeField] private float cutsceneDuration = 3f;
    [SerializeField] private float cutsceneDistance = 5f;
    [SerializeField] private float cutsceneAngle = 45f;

    [Header("Events")]
    public UnityEvent onDoorUnlock;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public enum DoorType
    {
        Slide,
        Rotate,
        FadeOut,
        Disable,
        MoveObject
    }

    private bool isUnlocked = false;
    private bool isMoving = false;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Renderer doorRenderer;
    private Collider doorCollider;
    private float lastCheckTime;

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        doorRenderer = GetComponent<Renderer>();
        doorCollider = GetComponent<Collider>();

        if (objectiveController == null)
            objectiveController = FindObjectOfType<ObjectiveController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (cutsceneCamera == null)
            cutsceneCamera = CutsceneCameraController.Instance;

        UpdateLockedAppearance();

        if (lockedEffect != null)
            lockedEffect.SetActive(true);
    }

    private void Update()
    {
        if (isUnlocked || isMoving) return;

        if (checkOnUpdate && Time.time - lastCheckTime >= checkInterval)
        {
            lastCheckTime = Time.time;
            CheckObjectiveCompletion();
        }

        if (pulseWhenLocked && doorRenderer != null)
        {
            float pulse = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            Color currentColor = Color.Lerp(lockedColor * 0.5f, lockedColor, pulse);
            doorRenderer.material.color = currentColor;
        }
    }

    private void CheckObjectiveCompletion()
    {
        if (objectiveController == null) return;

        bool objectiveComplete = false;
        foreach (var objective in GetAllObjectives())
        {
            if (objective.objectiveName == requiredObjectiveName && objective.isComplete)
            {
                objectiveComplete = true;
                break;
            }
        }

        if (objectiveComplete)
            UnlockDoor();
    }

    private System.Collections.Generic.List<ObjectiveController.Objective> GetAllObjectives()
    {
        var field = typeof(ObjectiveController).GetField("objectives",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
            return field.GetValue(objectiveController) as System.Collections.Generic.List<ObjectiveController.Objective>;

        return new System.Collections.Generic.List<ObjectiveController.Objective>();
    }

    public void UnlockDoor()
    {
        if (isUnlocked || isMoving) return;
        isUnlocked = true;

        if (showDebugLogs)
            Debug.Log($"ObjectiveDoor: Unlocking door '{gameObject.name}'");

        if (audioSource != null && unlockSound != null)
            audioSource.PlayOneShot(unlockSound);

        if (lockedEffect != null)
            lockedEffect.SetActive(false);

        UpdateUnlockedAppearance();

        onDoorUnlock?.Invoke();

        // 🎬 Play cutscene if available
        if (cutsceneCamera != null && !cutsceneCamera.IsInCutscene)
        {
            Transform target = focusTarget != null ? focusTarget : transform;

            cutsceneCamera.PlayOrbitCutscene(
                target,
                cutsceneDistance,
                cutsceneAngle,
                cutsceneDuration,
                () =>
                {
                    if (showDebugLogs)
                        Debug.Log($"ObjectiveDoor: Cutscene complete for '{gameObject.name}', opening door...");
                    StartCoroutine(PerformDoorAction());
                });
        }
        else
        {
            // No cutscene available, open immediately
            StartCoroutine(PerformDoorAction());
        }
    }

    private void UpdateLockedAppearance()
    {
        if (doorRenderer == null) return;

        if (lockedMaterial != null)
            doorRenderer.material = lockedMaterial;
        else
            doorRenderer.material.color = lockedColor;
    }

    private void UpdateUnlockedAppearance()
    {
        if (doorRenderer == null) return;

        if (unlockedMaterial != null)
            doorRenderer.material = unlockedMaterial;
        else
            doorRenderer.material.color = unlockedColor;
    }

    private IEnumerator PerformDoorAction()
    {
        isMoving = true;

        switch (doorType)
        {
            case DoorType.Slide:
                yield return StartCoroutine(SlideDoor());
                break;
            case DoorType.Rotate:
                yield return StartCoroutine(RotateDoor());
                break;
            case DoorType.FadeOut:
                yield return StartCoroutine(FadeDoor());
                break;
            case DoorType.Disable:
                DisableDoor();
                break;
        }

        isMoving = false;
    }

    private IEnumerator SlideDoor()
    {
        Vector3 targetPosition = startPosition + (slideDirection.normalized * slideDistance);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;

        if (doorCollider != null)
            doorCollider.enabled = false;
    }

    private IEnumerator RotateDoor()
    {
        Quaternion targetRotation = startRotation * Quaternion.Euler(rotationAxis * rotationAngle);
        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / rotateDuration);
            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation;

        if (doorCollider != null)
            doorCollider.enabled = false;
    }

    private IEnumerator FadeDoor()
    {
        if (doorRenderer == null)
        {
            DisableDoor();
            yield break;
        }

        Material mat = doorRenderer.material;
        Color startColor = mat.color;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            Color newColor = startColor;
            newColor.a = Mathf.Lerp(1f, 0f, t);
            mat.color = newColor;

            yield return null;
        }

        DisableDoor();
    }

    private void DisableDoor()
    {
        if (doorCollider != null)
            doorCollider.enabled = false;

        gameObject.SetActive(false);
    }

    public void ForceUnlock() => UnlockDoor();
    public bool IsUnlocked() => isUnlocked;
}
