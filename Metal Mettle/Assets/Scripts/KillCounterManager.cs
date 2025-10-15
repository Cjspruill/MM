using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Singleton manager to track kills across all enemies and trigger objectives
/// </summary>
public class KillCounterManager : MonoBehaviour
{
    public static KillCounterManager Instance { get; private set; }

    [Header("Kill Tracking")]
    [SerializeField] private int totalKills = 0;
    [SerializeField] private int requiredKills = 5;

    [Header("Objective Integration")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private string killTaskName = "Kill 5 Enemies";

    [Header("Events")]
    public UnityEvent<int> onKillCountChanged; // Passes current kill count
    public UnityEvent onRequiredKillsReached;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool objectiveCompleted = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (objectiveController == null)
        {
            objectiveController = FindFirstObjectByType<ObjectiveController>();
        }

        // Subscribe all existing enemies in scene
        RegisterAllEnemies();
    }

    private void RegisterAllEnemies()
    {
        Health[] enemies = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (Health enemy in enemies)
        {
            if (enemy.CompareTag("Enemy"))
            {
                RegisterEnemy(enemy);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"KillCounterManager: Registered {enemies.Length} enemies");
        }
    }

    public void RegisterEnemy(Health enemy)
    {
        if (enemy == null) return;

        // Subscribe to death event (onDeath is lowercase in your Health.cs)
        enemy.onDeath.AddListener(() => OnEnemyKilled(enemy));
    }

    private void OnEnemyKilled(Health enemy)
    {
        totalKills++;

        if (showDebugLogs)
        {
            Debug.Log($"KillCounterManager: Enemy killed! Total: {totalKills}/{requiredKills}");
        }

        // Invoke event for UI updates
        onKillCountChanged?.Invoke(totalKills);

        // Check if objective is complete
        if (totalKills >= requiredKills && !objectiveCompleted)
        {
            objectiveCompleted = true;
            onRequiredKillsReached?.Invoke();

            if (objectiveController != null && !string.IsNullOrEmpty(killTaskName))
            {
                objectiveController.CompleteTask(killTaskName);
            }

            if (showDebugLogs)
            {
                Debug.Log($"KillCounterManager: Required kills reached! Objective completed.");
            }
        }
    }

    public int GetKillCount() => totalKills;
    public int GetRequiredKills() => requiredKills;
    public float GetKillProgress() => (float)totalKills / requiredKills;

    public void ResetKillCount()
    {
        totalKills = 0;
        objectiveCompleted = false;
        onKillCountChanged?.Invoke(totalKills);
    }
}