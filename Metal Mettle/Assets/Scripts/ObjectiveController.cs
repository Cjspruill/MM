using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class ObjectiveController : MonoBehaviour
{
    [System.Serializable]
    public class ObjectiveTask
    {
        public string taskName;
        public string taskDescription;
        public bool isComplete = false;
        public UnityEvent onTaskComplete;
    }

    [System.Serializable]
    public class Objective
    {
        public string objectiveName;
        public string objectiveDescription;
        public List<ObjectiveTask> tasks = new List<ObjectiveTask>();
        public bool requireAllTasks = true;
        public bool isComplete = false;
        public UnityEvent onObjectiveComplete;
    }

    [Header("Objectives")]
    [SerializeField] private List<Objective> objectives = new List<Objective>();

    [Header("Current Objective")]
    [SerializeField] private int currentObjectiveIndex = 0;

    [Header("Events")]
    public UnityEvent onAllObjectivesComplete;
    public UnityEvent onTaskCompleted; // NEW: Fires when any task completes
    public UnityEvent onObjectiveChanged; // NEW: Fires when objective changes

    [Header("UI References (Optional)")]
    [SerializeField] private ObjectiveUI objectiveUI;

    private void Start()
    {
        // DEBUG: Log initial state
        Debug.Log($"=== ObjectiveController Start ===");
        Debug.Log($"Current Objective Index: {currentObjectiveIndex}");
        Debug.Log($"Total Objectives: {objectives.Count}");

        if (currentObjectiveIndex < objectives.Count)
        {
            Debug.Log($"Starting Objective: {objectives[currentObjectiveIndex].objectiveName}");
            Debug.Log($"Tasks in this objective: {objectives[currentObjectiveIndex].tasks.Count}");
            foreach (var task in objectives[currentObjectiveIndex].tasks)
            {
                Debug.Log($"  - Task: {task.taskName} (Complete: {task.isComplete})");
            }
        }

        UpdateObjectiveUI();
    }

    // Call this when resetting (like respawn or restart)
    public void ResetObjectives()
    {
        currentObjectiveIndex = 0;

        // Reset all objectives and tasks
        foreach (Objective objective in objectives)
        {
            objective.isComplete = false;

            foreach (ObjectiveTask task in objective.tasks)
            {
                task.isComplete = false;
            }
        }

        Debug.Log("Objectives reset!");
        onObjectiveChanged?.Invoke();
        UpdateObjectiveUI();
    }

    public void CompleteTask(string taskName)
    {
        Debug.Log($"=== CompleteTask Called: {taskName} ===");
        Debug.Log($"Current Objective Index: {currentObjectiveIndex}");

        if (currentObjectiveIndex >= objectives.Count)
        {
            Debug.LogWarning("No active objective to complete task for!");
            return;
        }

        Objective currentObjective = objectives[currentObjectiveIndex];
        Debug.Log($"Current Objective: {currentObjective.objectiveName}");

        foreach (ObjectiveTask task in currentObjective.tasks)
        {
            if (task.taskName == taskName && !task.isComplete)
            {
                task.isComplete = true;
                task.onTaskComplete?.Invoke();
                Debug.Log($"✓ Task completed: {taskName}");

                onTaskCompleted?.Invoke(); // NEW: Notify UI of task completion

                CheckObjectiveCompletion();
                UpdateObjectiveUI();
                return;
            }
        }

        Debug.LogWarning($"Task not found or already complete: {taskName} in objective '{currentObjective.objectiveName}'");
        Debug.LogWarning($"Available tasks in this objective:");
        foreach (ObjectiveTask task in currentObjective.tasks)
        {
            Debug.LogWarning($"  - {task.taskName} (Complete: {task.isComplete})");
        }
    }

    private void CheckObjectiveCompletion()
    {
        if (currentObjectiveIndex >= objectives.Count) return;

        Objective currentObjective = objectives[currentObjectiveIndex];

        if (currentObjective.requireAllTasks)
        {
            bool allComplete = true;
            foreach (ObjectiveTask task in currentObjective.tasks)
            {
                if (!task.isComplete)
                {
                    allComplete = false;
                    break;
                }
            }

            if (allComplete)
            {
                CompleteCurrentObjective();
            }
        }
        else
        {
            foreach (ObjectiveTask task in currentObjective.tasks)
            {
                if (task.isComplete)
                {
                    CompleteCurrentObjective();
                    break;
                }
            }
        }
    }

    private void CompleteCurrentObjective()
    {
        Objective completedObjective = objectives[currentObjectiveIndex];
        completedObjective.isComplete = true;
        completedObjective.onObjectiveComplete?.Invoke();

        Debug.Log($"Objective completed: {completedObjective.objectiveName}");

        currentObjectiveIndex++;

        if (currentObjectiveIndex >= objectives.Count)
        {
            Debug.Log("ALL OBJECTIVES COMPLETE!");
            onAllObjectivesComplete?.Invoke();
        }
        else
        {
            Debug.Log($"New objective: {objectives[currentObjectiveIndex].objectiveName}");
            onObjectiveChanged?.Invoke(); // NEW: Notify UI of objective change
        }

        UpdateObjectiveUI();
    }

    public Objective GetCurrentObjective()
    {
        if (currentObjectiveIndex < objectives.Count)
        {
            return objectives[currentObjectiveIndex];
        }
        return null;
    }

    // NEW: Get all tasks for current objective
    public List<ObjectiveTask> GetCurrentTasks()
    {
        if (currentObjectiveIndex < objectives.Count)
        {
            return objectives[currentObjectiveIndex].tasks;
        }
        return new List<ObjectiveTask>();
    }

    // NEW: Get specific task by name from current objective
    public ObjectiveTask GetTask(string taskName)
    {
        if (currentObjectiveIndex >= objectives.Count) return null;

        Objective currentObjective = objectives[currentObjectiveIndex];
        foreach (ObjectiveTask task in currentObjective.tasks)
        {
            if (task.taskName == taskName)
            {
                return task;
            }
        }
        return null;
    }

    // NEW: Check if specific task is complete
    public bool IsTaskComplete(string taskName)
    {
        ObjectiveTask task = GetTask(taskName);
        return task != null && task.isComplete;
    }

    public float GetCurrentObjectiveProgress()
    {
        if (currentObjectiveIndex >= objectives.Count) return 1f;

        Objective current = objectives[currentObjectiveIndex];
        if (current.tasks.Count == 0) return 0f;

        int completed = 0;
        foreach (ObjectiveTask task in current.tasks)
        {
            if (task.isComplete) completed++;
        }

        return (float)completed / current.tasks.Count;
    }

    public string GetProgressString()
    {
        if (currentObjectiveIndex >= objectives.Count) return "Complete";

        Objective current = objectives[currentObjectiveIndex];
        int completed = 0;
        foreach (ObjectiveTask task in current.tasks)
        {
            if (task.isComplete) completed++;
        }

        return $"{completed}/{current.tasks.Count}";
    }

    // NEW: Get completed task count
    public int GetCompletedTaskCount()
    {
        if (currentObjectiveIndex >= objectives.Count) return 0;

        Objective current = objectives[currentObjectiveIndex];
        int completed = 0;
        foreach (ObjectiveTask task in current.tasks)
        {
            if (task.isComplete) completed++;
        }
        return completed;
    }

    // NEW: Get total task count
    public int GetTotalTaskCount()
    {
        if (currentObjectiveIndex >= objectives.Count) return 0;
        return objectives[currentObjectiveIndex].tasks.Count;
    }

    private void UpdateObjectiveUI()
    {
        if (objectiveUI != null)
        {
            objectiveUI.UpdateDisplay(this);
        }
    }

    public void AddObjective(Objective newObjective)
    {
        objectives.Add(newObjective);
    }

    public bool AreAllObjectivesComplete()
    {
        return currentObjectiveIndex >= objectives.Count;
    }

    // Force UI update (useful for debugging or external calls)
    public void ForceUIUpdate()
    {
        UpdateObjectiveUI();
    }

    // NEW: Public access to objectives list for UI/Door systems
    public List<Objective> GetAllObjectives()
    {
        return objectives;
    }
}