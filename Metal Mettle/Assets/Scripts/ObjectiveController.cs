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

    [Header("UI References (Optional)")]
    [SerializeField] private ObjectiveUI objectiveUI;

    private void Start()
    {
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
        UpdateObjectiveUI();
    }

    public void CompleteTask(string taskName)
    {
        if (currentObjectiveIndex >= objectives.Count)
        {
            Debug.LogWarning("No active objective to complete task for!");
            return;
        }

        Objective currentObjective = objectives[currentObjectiveIndex];

        foreach (ObjectiveTask task in currentObjective.tasks)
        {
            if (task.taskName == taskName && !task.isComplete)
            {
                task.isComplete = true;
                task.onTaskComplete?.Invoke();
                Debug.Log($"Task completed: {taskName}");

                CheckObjectiveCompletion();
                UpdateObjectiveUI();
                return;
            }
        }

        Debug.LogWarning($"Task not found or already complete: {taskName}");
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
}