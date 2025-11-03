using UnityEngine;

/// <summary>
/// Extension to ObjectiveManager for tracking "Feed the Mask" objectives.
/// Add this as a component alongside ObjectiveManager (or integrate into it).
/// Tracks blood collected by the mask and updates ObjectiveController.
/// </summary>
public class MaskFeedingObjectiveTracking : MonoBehaviour
{
    [Header("Objective Configuration")]
    [SerializeField] private int objectiveIndex = 0;
    [SerializeField] private string objectiveTaskName = "Feed the Mask";
    [SerializeField] private float bloodRequired = 50f;

    [Header("References")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private MaskFeeder maskFeeder;

    [Header("UI Updates")]
    [SerializeField] private bool updateUIAutomatically = true;
    [SerializeField] private ObjectiveUI objectiveUI; // Optional

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool isTrackingActive = false;
    private bool objectiveComplete = false;
    private float lastBloodValue = 0f;

    private void Start()
    {
        // Auto-find references
        if (objectiveController == null)
        {
            objectiveController = FindFirstObjectByType<ObjectiveController>();
        }

        if (maskFeeder == null)
        {
            maskFeeder = FindFirstObjectByType<MaskFeeder>();
        }

        if (objectiveUI == null)
        {
            objectiveUI = FindFirstObjectByType<ObjectiveUI>();
        }

        // Validate
        if (objectiveController == null)
        {
            Debug.LogError("MaskFeedingObjectiveTracking: No ObjectiveController found!");
            enabled = false;
            return;
        }

        if (maskFeeder == null)
        {
            Debug.LogError("MaskFeedingObjectiveTracking: No MaskFeeder found!");
            enabled = false;
            return;
        }

        // Subscribe to objective changes
        if (objectiveController != null)
        {
            objectiveController.onObjectiveChanged.AddListener(OnObjectiveChanged);
        }

        // Check if this objective is already active
        OnObjectiveChanged();

        if (showDebugLogs)
        {
            Debug.Log($"MaskFeedingObjectiveTracking initialized for objective index {objectiveIndex}");
        }
    }

    private void OnDestroy()
    {
        if (objectiveController != null)
        {
            objectiveController.onObjectiveChanged.RemoveListener(OnObjectiveChanged);
        }
    }

    private void OnObjectiveChanged()
    {
        if (objectiveController == null) return;

        var currentObj = objectiveController.GetCurrentObjective();
        if (currentObj == null) return;

        // Get current objective index
        var allObjectives = objectiveController.GetAllObjectives();
        int currentIndex = allObjectives.IndexOf(currentObj);

        // Check if this is our objective
        if (currentIndex == objectiveIndex)
        {
            StartTracking();
        }
        else
        {
            StopTracking();
        }
    }

    private void StartTracking()
    {
        if (isTrackingActive) return;

        isTrackingActive = true;
        objectiveComplete = false;
        lastBloodValue = maskFeeder != null ? maskFeeder.GetCurrentBlood() : 0f;

        if (showDebugLogs)
        {
            Debug.Log($"Started tracking 'Feed the Mask' objective (Index: {objectiveIndex})");
        }
    }

    private void StopTracking()
    {
        if (!isTrackingActive) return;

        isTrackingActive = false;

        if (showDebugLogs)
        {
            Debug.Log($"Stopped tracking 'Feed the Mask' objective");
        }
    }

    private void Update()
    {
        if (!isTrackingActive || objectiveComplete || maskFeeder == null) return;

        // Check for blood changes
        float currentBlood = maskFeeder.GetCurrentBlood();

        if (currentBlood != lastBloodValue)
        {
            lastBloodValue = currentBlood;

            if (showDebugLogs)
            {
                Debug.Log($"Mask Blood: {currentBlood}/{bloodRequired} ({maskFeeder.GetProgress():F1}%)");
            }

            // Update UI if available
            if (updateUIAutomatically && objectiveUI != null)
            {
                UpdateObjectiveUI();
            }
        }

        // Check if objective complete
        if (maskFeeder.IsFull() && !objectiveComplete)
        {
            CompleteObjective();
        }
    }

    private void CompleteObjective()
    {
        objectiveComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"'Feed the Mask' objective COMPLETE! Notifying ObjectiveController.");
        }

        // The MaskFeeder will call objectiveController.CompleteTask()
        // But we can also do it here as a backup
        if (objectiveController != null && !objectiveController.IsTaskComplete(objectiveTaskName))
        {
            objectiveController.CompleteTask(objectiveTaskName);
        }
    }

    private void UpdateObjectiveUI()
    {
        if (objectiveUI == null || maskFeeder == null) return;

        // You can create a custom UI update method if needed
        // For now, we'll just trigger the normal UI update
        // objectiveUI.UpdateDisplay(); // Uncomment if this method exists
    }

    /// <summary>
    /// Get progress for external systems (e.g., UI)
    /// </summary>
    public float GetProgress()
    {
        return maskFeeder != null ? maskFeeder.GetProgress() : 0f;
    }

    /// <summary>
    /// Get progress string for UI display
    /// </summary>
    public string GetProgressString()
    {
        if (maskFeeder == null) return "0/0";
        return $"{maskFeeder.GetCurrentBlood():F0}/{maskFeeder.GetRequiredBlood():F0}";
    }
}