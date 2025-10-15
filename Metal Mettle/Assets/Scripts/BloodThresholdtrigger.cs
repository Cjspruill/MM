using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks blood meter and completes objectives based on thresholds
/// Examples: "Gain 100 blood", "Drain to 25% blood", "Maintain 75%+ for 10 seconds"
/// </summary>
public class BloodThresholdTrigger : MonoBehaviour
{
    [Header("Blood System Reference")]
    [SerializeField] private BloodSystem bloodSystem;

    [Header("Threshold Settings")]
    [SerializeField] private ThresholdType thresholdType;
    [SerializeField] private float targetThreshold = 100f;

    [Header("Time-Based Requirements (Optional)")]
    [Tooltip("Require threshold to be maintained for X seconds (0 = instant)")]
    [SerializeField] private float requiredDuration = 0f;
    private float timeAtThreshold = 0f;

    [Header("Objective Integration")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private string bloodTaskName = "Reach 100 Blood";

    [Header("Events")]
    public UnityEvent<float> onThresholdProgress; // 0-1 progress
    public UnityEvent onThresholdReached;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public enum ThresholdType
    {
        ReachAmount,        // Blood reaches specific amount (e.g., 100)
        ReachPercentage,    // Blood reaches % of max (e.g., 75%)
        DropBelow,          // Blood drops below amount
        GainAmount,         // Gain X blood from current (tracks delta)
        DrainAmount,        // Lose X blood from current (tracks delta)
        MaintainAbove       // Stay above threshold for X seconds
    }

    private bool objectiveCompleted = false;
    private float startingBlood = 0f;
    private bool isTracking = false;

    private void Start()
    {
        if (bloodSystem == null)
        {
            bloodSystem = FindFirstObjectByType<BloodSystem>();
            if (bloodSystem == null)
            {
                Debug.LogError("BloodThresholdTrigger: No BloodSystem found in scene!");
                return;
            }
        }

        if (objectiveController == null)
        {
            objectiveController = FindFirstObjectByType<ObjectiveController>();
        }

        startingBlood = bloodSystem.currentBlood;
        isTracking = true;
    }

    private void Update()
    {
        if (!isTracking || objectiveCompleted) return;

        CheckThreshold();
    }

    private void CheckThreshold()
    {
        float currentBlood = bloodSystem.currentBlood;
        float maxBlood = bloodSystem.maxBlood;
        bool thresholdMet = false;

        switch (thresholdType)
        {
            case ThresholdType.ReachAmount:
                thresholdMet = currentBlood >= targetThreshold;
                UpdateProgress(currentBlood / targetThreshold);
                break;

            case ThresholdType.ReachPercentage:
                float currentPercentage = (currentBlood / maxBlood) * 100f;
                thresholdMet = currentPercentage >= targetThreshold;
                UpdateProgress(currentPercentage / targetThreshold);
                break;

            case ThresholdType.DropBelow:
                thresholdMet = currentBlood <= targetThreshold;
                UpdateProgress(1f - (currentBlood / targetThreshold));
                break;

            case ThresholdType.GainAmount:
                float gained = currentBlood - startingBlood;
                thresholdMet = gained >= targetThreshold;
                UpdateProgress(gained / targetThreshold);
                break;

            case ThresholdType.DrainAmount:
                float drained = startingBlood - currentBlood;
                thresholdMet = drained >= targetThreshold;
                UpdateProgress(drained / targetThreshold);
                break;

            case ThresholdType.MaintainAbove:
                if (currentBlood >= targetThreshold)
                {
                    timeAtThreshold += Time.deltaTime;
                    thresholdMet = timeAtThreshold >= requiredDuration;
                    UpdateProgress(timeAtThreshold / requiredDuration);
                }
                else
                {
                    timeAtThreshold = 0f;
                    UpdateProgress(0f);
                }
                break;
        }

        if (thresholdMet)
        {
            CompleteObjective();
        }
    }

    private void UpdateProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        onThresholdProgress?.Invoke(progress);
    }

    private void CompleteObjective()
    {
        if (objectiveCompleted) return;

        objectiveCompleted = true;
        isTracking = false;

        if (showDebugLogs)
        {
            Debug.Log($"BloodThresholdTrigger: Threshold reached! Completing objective '{bloodTaskName}'");
        }

        onThresholdReached?.Invoke();

        if (objectiveController != null && !string.IsNullOrEmpty(bloodTaskName))
        {
            objectiveController.CompleteTask(bloodTaskName);
        }
    }

    public void ResetTracking()
    {
        objectiveCompleted = false;
        timeAtThreshold = 0f;
        startingBlood = bloodSystem.currentBlood;
        isTracking = true;
    }

    public float GetProgress()
    {
        float currentBlood = bloodSystem.currentBlood;

        switch (thresholdType)
        {
            case ThresholdType.ReachAmount:
                return currentBlood / targetThreshold;
            case ThresholdType.GainAmount:
                return (currentBlood - startingBlood) / targetThreshold;
            case ThresholdType.MaintainAbove:
                return timeAtThreshold / requiredDuration;
            default:
                return 0f;
        }
    }
}