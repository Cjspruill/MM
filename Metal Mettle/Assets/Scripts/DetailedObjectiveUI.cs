using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Detailed UI that shows each individual task with checkmarks
/// Example display:
///   Tutorial Combat (2/3)
///   ✓ Land 10 Attacks
///   ✓ Gain 10 Blood
///   ○ Kill Enemy
/// </summary>
public class DetailedObjectiveUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObjectiveController objectiveController;

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

        // Add strikethrough if completed
        if (strikethroughCompleted && task.isComplete)
        {
            taskText = $"<s>{taskText}</s>";
        }

        textElement.text = prefix + taskText;

        // Set color
        textElement.color = task.isComplete ? completeTaskColor : incompleteTaskColor;
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

        // Clear all tasks
        foreach (var item in spawnedTaskItems)
        {
            Destroy(item);
        }
        spawnedTaskItems.Clear();

        foreach (var text in manualTaskTexts)
        {
            if (text != null)
                text.gameObject.SetActive(false);
        }
    }

    private void OnTaskCompleted()
    {
        if (animateTaskCompletion)
        {
            AnimateTaskCompletion();
        }

        UpdateDisplay();
    }

    private void OnObjectiveChanged()
    {
        UpdateDisplay();
    }

    private void AnimateTaskCompletion()
    {
        // Find the most recently completed task and animate it
        // This is a simple pulse effect
        if (manualTaskTexts.Count > 0)
        {
            var currentObjective = objectiveController.GetCurrentObjective();
            if (currentObjective == null) return;

            for (int i = 0; i < currentObjective.tasks.Count && i < manualTaskTexts.Count; i++)
            {
                if (currentObjective.tasks[i].isComplete && manualTaskTexts[i] != null)
                {
                    StartCoroutine(PulseText(manualTaskTexts[i]));
                }
            }
        }
    }

    private System.Collections.IEnumerator PulseText(TextMeshProUGUI textElement)
    {
        Vector3 originalScale = textElement.transform.localScale;
        float elapsed = 0f;

        // Scale up
        while (elapsed < taskAnimDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (taskAnimDuration / 2f);
            textElement.transform.localScale = Vector3.Lerp(originalScale, originalScale * taskCompleteScale, t);
            yield return null;
        }

        elapsed = 0f;

        // Scale back down
        while (elapsed < taskAnimDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (taskAnimDuration / 2f);
            textElement.transform.localScale = Vector3.Lerp(originalScale * taskCompleteScale, originalScale, t);
            yield return null;
        }

        textElement.transform.localScale = originalScale;
    }

    // Public method to force update
    public void ForceUpdate()
    {
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (objectiveController != null)
        {
            objectiveController.onTaskCompleted.RemoveListener(OnTaskCompleted);
            objectiveController.onObjectiveChanged.RemoveListener(OnObjectiveChanged);
        }
    }
}