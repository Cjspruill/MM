using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Detailed UI that shows each individual task with checkmarks AND progress numbers
/// Example display:
///   Tutorial Combat (2/3)
///   ✓ Land 10 Attacks (10/10)
///   ✓ Gain 10 Blood (10.0/10.0)
///   ○ Kill Enemy (0/1)
/// </summary>
public class DetailedObjectiveUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private ObjectiveManager objectiveManager; // NEW: For progress tracking

    [Header("UI Text Elements")]
    [SerializeField] private TextMeshProUGUI objectiveTitleText;
    [SerializeField] private TextMeshProUGUI objectiveProgressText;
    [SerializeField] private Transform taskListContainer; // Parent for task items
    [SerializeField] private GameObject taskItemPrefab; // Prefab for individual task display

    [Header("Manual Task Texts (Alternative to Prefab)")]
    [Tooltip("If you don't want to use prefabs, assign TextMeshPro texts manually")]
    [SerializeField] private List<TextMeshProUGUI> manualTaskTexts = new List<TextMeshProUGUI>();

    [Header("Display Settings")]
    [SerializeField] private string completePrefix = "[X] ";  // Changed from ✓
    [SerializeField] private string incompletePrefix = "[ ] "; // Changed from ○
    [SerializeField] private Color completeTitleColor = new Color(0.3f, 1f, 0.3f); // Light green
    [SerializeField] private Color incompleteTitleColor = Color.white;
    [SerializeField] private Color completeTaskColor = new Color(0.5f, 0.5f, 0.5f); // Gray
    [SerializeField] private Color incompleteTaskColor = Color.white;
    [SerializeField] private bool strikethroughCompleted = true;
    [SerializeField] private bool showProgressNumbers = true; // NEW: Toggle progress display

    [Header("Animation")]
    [SerializeField] private bool animateTaskCompletion = true;
    [SerializeField] private float taskCompleteScale = 1.2f;
    [SerializeField] private float taskAnimDuration = 0.3f;

    [Header("Update Settings")]
    [SerializeField] private bool autoUpdate = true;
    [SerializeField] private float updateInterval = 0.5f;
    private float lastUpdateTime;

    private List<GameObject> spawnedTaskItems = new List<GameObject>();

    private void Start()
    {
        if (objectiveController == null)
        {
            objectiveController = FindObjectOfType<ObjectiveController>();
        }

        // NEW: Auto-find ObjectiveManager
        if (objectiveManager == null)
        {
            objectiveManager = FindObjectOfType<ObjectiveManager>();
        }

        // Subscribe to objective controller events
        if (objectiveController != null)
        {
            objectiveController.onTaskCompleted.AddListener(OnTaskCompleted);
            objectiveController.onObjectiveChanged.AddListener(OnObjectiveChanged);
        }

        UpdateDisplay();
    }

    private void Update()
    {
        if (!autoUpdate) return;

        if (Time.time - lastUpdateTime >= updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdateDisplay();
        }
    }

    public void UpdateDisplay()
    {
        if (objectiveController == null) return;

        var currentObjective = objectiveController.GetCurrentObjective();

        if (currentObjective == null)
        {
            DisplayAllComplete();
            return;
        }

        // Update title
        if (objectiveTitleText != null)
        {
            objectiveTitleText.text = currentObjective.objectiveName;
            objectiveTitleText.color = currentObjective.isComplete ? completeTitleColor : incompleteTitleColor;
        }

        // Update progress
        if (objectiveProgressText != null)
        {
            int completed = objectiveController.GetCompletedTaskCount();
            int total = objectiveController.GetTotalTaskCount();
            objectiveProgressText.text = $"({completed}/{total})";
        }

        // Update tasks display
        if (taskItemPrefab != null && taskListContainer != null)
        {
            UpdateTasksWithPrefab(currentObjective);
        }
        else if (manualTaskTexts.Count > 0)
        {
            UpdateTasksManual(currentObjective);
        }
    }

    private void UpdateTasksWithPrefab(ObjectiveController.Objective objective)
    {
        // Clear existing task items
        foreach (var item in spawnedTaskItems)
        {
            Destroy(item);
        }
        spawnedTaskItems.Clear();

        // Create new task items
        foreach (var task in objective.tasks)
        {
            GameObject taskItem = Instantiate(taskItemPrefab, taskListContainer);
            spawnedTaskItems.Add(taskItem);

            TextMeshProUGUI taskText = taskItem.GetComponentInChildren<TextMeshProUGUI>();
            if (taskText != null)
            {
                UpdateTaskText(taskText, task);
            }
        }
    }

    private void UpdateTasksManual(ObjectiveController.Objective objective)
    {
        for (int i = 0; i < manualTaskTexts.Count; i++)
        {
            if (manualTaskTexts[i] == null) continue;

            if (i < objective.tasks.Count)
            {
                var task = objective.tasks[i];
                UpdateTaskText(manualTaskTexts[i], task);
                manualTaskTexts[i].gameObject.SetActive(true);
            }
            else
            {
                // Hide extra text elements
                manualTaskTexts[i].gameObject.SetActive(false);
            }
        }
    }

    private void UpdateTaskText(TextMeshProUGUI textElement, ObjectiveController.ObjectiveTask task)
    {
        if (textElement == null) return;

        // Set prefix and text
        string prefix = task.isComplete ? completePrefix : incompletePrefix;
        string taskText = task.taskName;

        // NEW: Add progress numbers if enabled
        if (showProgressNumbers && objectiveManager != null)
        {
            string progressString = GetTaskProgress(task.taskName);
            if (!string.IsNullOrEmpty(progressString))
            {
                taskText += $" {progressString}";
            }
        }

        // Add strikethrough if completed
        if (strikethroughCompleted && task.isComplete)
        {
            taskText = $"<s>{taskText}</s>";
        }

        textElement.text = prefix + taskText;

        // Set color
        textElement.color = task.isComplete ? completeTaskColor : incompleteTaskColor;
    }

    /// <summary>
    /// NEW: Get progress string for a specific task based on its name
    /// Returns format like "(7/10)" or "(5.0/10.0)" or empty string if no progress tracking
    /// </summary>
    private string GetTaskProgress(string taskName)
    {
        if (objectiveManager == null) return "";

        string lowerTaskName = taskName.ToLower();

        // Check for kill tasks
        if (lowerTaskName.Contains("kill") && !lowerTaskName.Contains("execution"))
        {
            int current = objectiveManager.GetCurrentKills();
            int required = objectiveManager.GetKillsRequired();
            if (required > 0)
                return $"({current}/{required})";
        }

        // Check for execution tasks
        if (lowerTaskName.Contains("execution") || lowerTaskName.Contains("execute"))
        {
            int current = objectiveManager.GetCurrentExecutions();
            int required = objectiveManager.GetExecutionsRequired();
            if (required > 0)
                return $"({current}/{required})";
        }

        // Check for hit/attack tasks (general)
        if ((lowerTaskName.Contains("hit") || lowerTaskName.Contains("land")) &&
            !lowerTaskName.Contains("light") && !lowerTaskName.Contains("heavy"))
        {
            int current = objectiveManager.GetCurrentHits();
            int required = objectiveManager.GetHitsRequired();
            if (required > 0)
                return $"({current}/{required})";
        }

        // Check for light attack tasks
        if (lowerTaskName.Contains("light"))
        {
            int current = objectiveManager.GetCurrentLightAttacks();
            int required = objectiveManager.GetLightAttacksRequired();
            if (required > 0)
                return $"({current}/{required})";
        }

        // Check for heavy attack tasks
        if (lowerTaskName.Contains("heavy"))
        {
            int current = objectiveManager.GetCurrentHeavyAttacks();
            int required = objectiveManager.GetHeavyAttacksRequired();
            if (required > 0)
                return $"({current}/{required})";
        }

        // Check for blood tasks
        if (lowerTaskName.Contains("blood"))
        {
            float current = objectiveManager.GetCurrentBloodGained();
            float required = objectiveManager.GetBloodRequired();
            if (required > 0)
                return $"({current:F1}/{required:F1})";
        }

        return "";
    }

    private void DisplayAllComplete()
    {
        if (objectiveTitleText != null)
        {
            objectiveTitleText.text = "All Objectives Complete!";
            objectiveTitleText.color = completeTitleColor;
        }

        if (objectiveProgressText != null)
        {
            objectiveProgressText.text = "";
        }

        // Clear all task displays
        foreach (var item in spawnedTaskItems)
        {
            Destroy(item);
        }
        spawnedTaskItems.Clear();

        foreach (var text in manualTaskTexts)
        {
            if (text != null)
            {
                text.gameObject.SetActive(false);
            }
        }
    }

    private void OnTaskCompleted()
    {
        if (animateTaskCompletion)
        {
            // Optional: Add animation here
        }

        UpdateDisplay();
    }

    private void OnObjectiveChanged()
    {
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (objectiveController != null)
        {
            objectiveController.onTaskCompleted.RemoveListener(OnTaskCompleted);
            objectiveController.onObjectiveChanged.RemoveListener(OnObjectiveChanged);
        }
    }
}