using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the mask's blood collection for the "Feed the Mask" objective.
/// Place this on the mask GameObject in your scene.
/// Tracks blood collected and notifies ObjectiveManager when complete.
/// </summary>
public class MaskFeeder : MonoBehaviour
{
    [Header("Objective Settings")]
    [SerializeField] private string objectiveTaskName = "Feed the Mask";
    [SerializeField] private float bloodRequired = 50f;
    [SerializeField] private bool completeObjectiveWhenFull = true;

    [Header("Objective Activation")]
    [SerializeField] private bool requireObjectiveActive = true;
    [SerializeField] private int requiredObjectiveIndex = -1; // -1 = auto-detect from task name

    [Header("Current State")]
    [SerializeField] private float currentBlood = 0f;

    [Header("Visual Feedback")]
    [SerializeField] private bool enableVisualFeedback = true;
    [SerializeField] private ParticleSystem absorptionParticles;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bloodAbsorbSound;
    [SerializeField] private float minTimeBetweenSounds = 0.2f;

    [Header("Scale/Glow Effect (Optional)")]
    [SerializeField] private bool scaleWithBlood = true;
    [SerializeField] private Vector3 startScale = Vector3.one;
    [SerializeField] private Vector3 maxScale = Vector3.one * 1.5f;
    [SerializeField] private MeshRenderer maskRenderer;
    [SerializeField] private string emissiveColorProperty = "_EmissionColor";
    [SerializeField] private Color startEmissionColor = Color.black;
    [SerializeField] private Color maxEmissionColor = Color.magenta;

    [Header("Events")]
    public UnityEvent onBloodAdded;
    public UnityEvent onMaskFull;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // References
    private ObjectiveController objectiveController;
    private MaterialPropertyBlock propertyBlock;
    private float lastSoundTime = 0f;
    private bool objectiveComplete = false;
    private bool isObjectiveActive = false;

    // Static reference to the UI (shared across all MaskFeeders)
    private static MaskFeedingUI sharedUI;

    private void Start()
    {
        // Find objective controller
        objectiveController = FindFirstObjectByType<ObjectiveController>();
        if (objectiveController == null && showDebugLogs)
        {
            Debug.LogWarning("MaskFeeder: No ObjectiveController found in scene!");
        }

        // Find the shared UI if we haven't already
        if (sharedUI == null)
        {
            sharedUI = FindFirstObjectByType<MaskFeedingUI>();
            if (sharedUI == null && showDebugLogs)
            {
                Debug.LogWarning("MaskFeeder: No MaskFeedingUI found in scene!");
            }
        }

        // Subscribe to objective changes
        if (objectiveController != null)
        {
            objectiveController.onObjectiveChanged.AddListener(CheckObjectiveStatus);
            CheckObjectiveStatus(); // Check immediately
        }

        // Set up material property block for emission
        if (maskRenderer != null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        // Auto-find audio source if needed
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Store initial scale
        if (scaleWithBlood)
        {
            startScale = transform.localScale;
        }

        if (showDebugLogs)
        {
            Debug.Log($"MaskFeeder initialized - Requires {bloodRequired} blood");
            Debug.Log($"Require Objective Active: {requireObjectiveActive}");
        }

        UpdateVisuals();
    }

    private void OnEnable()
    {
        // When this MaskFeeder is enabled, notify the UI
        if (sharedUI != null)
        {
            sharedUI.SetActiveMaskFeeder(this);
            if (showDebugLogs)
            {
                Debug.Log($"MaskFeeder: Registered with UI on enable");
            }
        }
    }

    private void OnDisable()
    {
        // When this MaskFeeder is disabled, unregister from UI if it's the current one
        if (sharedUI != null && sharedUI.GetActiveMaskFeeder() == this)
        {
            sharedUI.SetActiveMaskFeeder(null);
            if (showDebugLogs)
            {
                Debug.Log($"MaskFeeder: Unregistered from UI on disable");
            }
        }
    }

    private void OnDestroy()
    {
        if (objectiveController != null)
        {
            objectiveController.onObjectiveChanged.RemoveListener(CheckObjectiveStatus);
        }
    }

    private void CheckObjectiveStatus()
    {
        if (!requireObjectiveActive)
        {
            isObjectiveActive = true;
            return;
        }

        if (objectiveController == null)
        {
            isObjectiveActive = false;
            return;
        }

        var currentObj = objectiveController.GetCurrentObjective();
        if (currentObj == null)
        {
            isObjectiveActive = false;
            return;
        }

        // Get current objective index
        var allObjectives = objectiveController.GetAllObjectives();
        int currentIndex = allObjectives.IndexOf(currentObj);

        // If we have a specific index requirement, check it
        if (requiredObjectiveIndex >= 0)
        {
            isObjectiveActive = (currentIndex == requiredObjectiveIndex);
        }
        else
        {
            // Otherwise, check if current objective has our task
            isObjectiveActive = false;
            foreach (var task in currentObj.tasks)
            {
                if (task.taskName == objectiveTaskName)
                {
                    isObjectiveActive = true;
                    break;
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"MaskFeeder: Objective active = {isObjectiveActive} (Current index: {currentIndex})");
        }
    }

    /// <summary>
    /// Add blood to the mask. Called by MaskSiphon when it's siphoned.
    /// </summary>
    public void AddBlood(float amount)
    {
        // CRITICAL: Don't accept blood if objective isn't active
        if (requireObjectiveActive && !isObjectiveActive)
        {
            if (showDebugLogs)
            {
                Debug.Log($"MaskFeeder: REJECTED blood ({amount}) - objective not active yet");
            }
            return; // Exit early - don't add blood
        }

        if (objectiveComplete) return;

        currentBlood += amount;
        currentBlood = Mathf.Min(currentBlood, bloodRequired); // Cap at max

        if (showDebugLogs)
        {
            Debug.Log($"MaskFeeder: ACCEPTED {amount} blood. Current: {currentBlood}/{bloodRequired} ({GetProgress():F1}%)");
        }

        // Visual/audio feedback
        PlayFeedback();
        UpdateVisuals();

        // Fire event
        onBloodAdded?.Invoke();

        // Check if objective complete
        if (currentBlood >= bloodRequired && !objectiveComplete)
        {
            CompleteMaskFeeding();
        }
    }

    private void CompleteMaskFeeding()
    {
        objectiveComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"MaskFeeder: Mask is FULL! Completing objective: {objectiveTaskName}");
        }

        // Fire event
        onMaskFull?.Invoke();

        // Complete objective task
        if (completeObjectiveWhenFull && objectiveController != null)
        {
            objectiveController.CompleteTask(objectiveTaskName);
        }
    }

    private void PlayFeedback()
    {
        // Particles
        if (enableVisualFeedback && absorptionParticles != null && !absorptionParticles.isPlaying)
        {
            absorptionParticles.Play();
        }

        // Sound (with throttling)
        if (audioSource != null && bloodAbsorbSound != null)
        {
            float timeSinceLastSound = Time.time - lastSoundTime;
            if (timeSinceLastSound >= minTimeBetweenSounds)
            {
                audioSource.PlayOneShot(bloodAbsorbSound);
                lastSoundTime = Time.time;
            }
        }
    }

    private void UpdateVisuals()
    {
        float progress = GetProgress() / 100f; // 0-1

        // Scale
        if (scaleWithBlood)
        {
            transform.localScale = Vector3.Lerp(startScale, maxScale, progress);
        }

        // Emission glow
        if (maskRenderer != null && propertyBlock != null)
        {
            Color emissionColor = Color.Lerp(startEmissionColor, maxEmissionColor, progress);
            propertyBlock.SetColor(emissiveColorProperty, emissionColor);
            maskRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    #region Public Getters

    public float GetCurrentBlood() => currentBlood;
    public float GetRequiredBlood() => bloodRequired;
    public float GetProgress() => (currentBlood / bloodRequired) * 100f;
    public bool IsFull() => currentBlood >= bloodRequired;
    public bool IsObjectiveActive() => isObjectiveActive;
    public bool IsComplete() => objectiveComplete;

    #endregion
}