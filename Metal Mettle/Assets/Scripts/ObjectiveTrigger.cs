using UnityEngine;

/// <summary>
/// Attach this to triggers, enemies, or use as a manager to complete objective tasks
/// based on different game events.
/// </summary>
public class ObjectiveTrigger : MonoBehaviour
{
    [Header("Objective Reference")]
    [SerializeField] private ObjectiveController objectiveController;

    [Header("Task to Complete")]
    [Tooltip("The exact name of the task in the ObjectiveController")]
    [SerializeField] private string taskName;

    [Header("Trigger Settings")]
    [SerializeField] private TriggerType triggerType;
    [SerializeField] private string playerTag = "Player";

    [Header("Counter Settings (for combat/stat triggers)")]
    [Tooltip("How many times this needs to happen before completing")]
    [SerializeField] private int requiredCount = 1;
    private int currentCount = 0;

    [Header("Blood/Energy Thresholds")]
    [SerializeField] private float requiredBloodAmount = 0f;
    [SerializeField] private float requiredEnergyAmount = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public enum TriggerType
    {
        OnTriggerEnter,      // When player enters trigger zone
        OnEnemyDeath,        // Attach to enemy - triggers on death
        OnAttackHitCount,    // Track number of hits on this enemy
        OnKillCount,         // Track total kills (use as manager)
        OnBloodGained,       // When blood meter reaches threshold
        OnEnergyGained,      // When energy reaches threshold
        OnBloodDrained,      // When blood drops below threshold
        ManualCall           // Call CompleteObjectiveTask() manually
    }

    private void Start()
    {
        // Auto-find ObjectiveController if not assigned
        if (objectiveController == null)
        {
            objectiveController = FindObjectOfType<ObjectiveController>();

            if (objectiveController == null)
            {
                Debug.LogError("ObjectiveTrigger: No ObjectiveController found in scene!");
            }
        }

        // Subscribe to events based on trigger type
        SetupEventListeners();
    }

    private void SetupEventListeners()
    {
        switch (triggerType)
        {
            case TriggerType.OnEnemyDeath:
                Health health = GetComponent<Health>();
                if (health != null)
                {
                    health.onDeath.AddListener(OnEnemyDeathTriggered);
                }
                else
                {
                    Debug.LogWarning($"ObjectiveTrigger on {gameObject.name}: No Health component found for OnEnemyDeath trigger!");
                }
                break;

            case TriggerType.OnBloodGained:
            case TriggerType.OnBloodDrained:
                BloodSystem bloodSystem = FindObjectOfType<BloodSystem>();
                if (bloodSystem != null)
                {
                    // We'll check this in Update for threshold-based triggers
                }
                break;
        }
    }

    private void Update()
    {
        // Check threshold-based triggers
        if (triggerType == TriggerType.OnBloodGained || triggerType == TriggerType.OnBloodDrained)
        {
            CheckBloodThreshold();
        }
        else if (triggerType == TriggerType.OnEnergyGained)
        {
            CheckEnergyThreshold();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerType != TriggerType.OnTriggerEnter) return;

        if (other.CompareTag(playerTag))
        {
            IncrementAndCheckCompletion();
        }
    }

    private void OnEnemyDeathTriggered()
    {
        if (triggerType == TriggerType.OnEnemyDeath)
        {
            IncrementAndCheckCompletion();
        }
    }

    public void OnAttackHit()
    {
        if (triggerType == TriggerType.OnAttackHitCount)
        {
            IncrementAndCheckCompletion();
        }
    }

    public void OnEnemyKilled()
    {
        if (triggerType == TriggerType.OnKillCount)
        {
            IncrementAndCheckCompletion();
        }
    }

    private void CheckBloodThreshold()
    {
        BloodSystem bloodSystem = FindFirstObjectByType<BloodSystem>();
        if (bloodSystem == null) return;

        float currentBlood = bloodSystem.currentBlood;

        if (triggerType == TriggerType.OnBloodGained)
        {
            if (currentBlood >= requiredBloodAmount && currentCount == 0)
            {
                IncrementAndCheckCompletion();
            }
        }
        else if (triggerType == TriggerType.OnBloodDrained)
        {
            if (currentBlood <= requiredBloodAmount && currentCount == 0)
            {
                IncrementAndCheckCompletion();
            }
        }
    }

    private void CheckEnergyThreshold()
    {
        // Implement if you have an energy system
        // Similar to CheckBloodThreshold
    }

    private void IncrementAndCheckCompletion()
    {
        currentCount++;

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveTrigger: Progress {currentCount}/{requiredCount} for task '{taskName}'");
        }

        if (currentCount >= requiredCount)
        {
            CompleteObjectiveTask();
        }
    }

    public void CompleteObjectiveTask()
    {
        if (objectiveController == null)
        {
            Debug.LogError("ObjectiveTrigger: Cannot complete task - ObjectiveController not assigned!");
            return;
        }

        if (string.IsNullOrEmpty(taskName))
        {
            Debug.LogError("ObjectiveTrigger: Task name is empty!");
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveTrigger: Completing task '{taskName}'");
        }

        objectiveController.CompleteTask(taskName);
    }

    public void ResetProgress()
    {
        currentCount = 0;
    }

    private void OnDestroy()
    {
        // Clean up event listeners
        if (triggerType == TriggerType.OnEnemyDeath)
        {
            Health health = GetComponent<Health>();
            if (health != null)
            {
                health.onDeath.RemoveListener(OnEnemyDeathTriggered);
            }
        }
    }
}