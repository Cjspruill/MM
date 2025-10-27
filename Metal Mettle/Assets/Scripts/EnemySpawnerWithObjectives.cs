using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced spawner that works with your EnemySpawnController (with multiple spawn areas)
/// Automatically registers all spawned enemies with ObjectiveManager
/// NOW SUPPORTS: Continuous spawning during objectives (especially attack-count objectives)
/// </summary>
public class EnemySpawnerWithObjectives : MonoBehaviour
{
    [Header("Spawn Controller Reference")]
    [SerializeField] private EnemySpawnController spawnController;

    [Header("Spawn Settings")]
    [SerializeField] private int enemiesToSpawn = 10;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool spawnAllAtOnce = false;

    [Header("Continuous Spawning (NEW)")]
    [SerializeField] private bool continuousSpawning = false;
    [SerializeField] private int maxActiveEnemies = 5; // Only for continuous mode
    [SerializeField] private int maxTotalSpawns = -1; // -1 = unlimited, only for continuous mode
    [SerializeField] private bool stopOnObjectiveComplete = true; // Stop when objective completes

    [Header("Objective Integration (NEW)")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private string targetObjectiveName; // Which objective to monitor
    [SerializeField] private string targetTaskName; // Which task to monitor (optional)
    [SerializeField] private bool onlySpawnDuringObjective = false; // Only spawn when objective is active

    [Header("Enemy Selection")]
    [SerializeField] private bool spawnRandomTypes = true;
    [SerializeField] private int specificEnemyIndex = 0; // Which enemy prefab to spawn if not random

    [Header("Area Selection (Optional)")]
    [SerializeField] private bool useSpecificArea = false; // Override spawn controller's area selection
    [SerializeField] private int specificAreaIndex = 0; // Which area to spawn in

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private int spawnedCount = 0;
    private float lastSpawnTime = 0f;
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private bool isSpawningActive = false;
    private bool objectiveComplete = false;

    private void Start()
    {
        Debug.Log("=== EnemySpawnerWithObjectives START ===");

        // Auto-find spawn controller if not assigned
        if (spawnController == null)
        {
            spawnController = GetComponent<EnemySpawnController>();
            if (spawnController == null)
            {
                spawnController = FindObjectOfType<EnemySpawnController>();
            }
        }

        if (spawnController == null)
        {
            Debug.LogError("EnemySpawnerWithObjectives: No EnemySpawnController found!");
            return;
        }
        else
        {
            Debug.Log($"EnemySpawnerWithObjectives: Found spawn controller: {spawnController.name}");
        }

        // Auto-find objective controller if not assigned
        if (objectiveController == null && (onlySpawnDuringObjective || stopOnObjectiveComplete))
        {
            objectiveController = FindObjectOfType<ObjectiveController>();
        }

        if (objectiveController != null)
        {
            Debug.Log($"EnemySpawnerWithObjectives: Found objective controller");
            // Subscribe to objective events
            objectiveController.onTaskCompleted.AddListener(CheckObjectiveStatus);
            objectiveController.onObjectiveChanged.AddListener(CheckObjectiveStatus);
        }
        else
        {
            Debug.Log("EnemySpawnerWithObjectives: No objective controller found");
        }

        // Log configuration
        Debug.Log($"Continuous Spawning: {continuousSpawning}");
        Debug.Log($"Spawn On Start: {spawnOnStart}");
        Debug.Log($"Only Spawn During Objective: {onlySpawnDuringObjective}");
        Debug.Log($"Target Objective: {targetObjectiveName}");
        Debug.Log($"Max Active Enemies: {maxActiveEnemies}");
        Debug.Log($"Spawn Interval: {spawnInterval}");

        // Check if we should start spawning
        if (spawnOnStart)
        {
            if (onlySpawnDuringObjective)
            {
                Debug.Log("Checking objective status before spawning...");
                CheckObjectiveStatus(); // Will start if objective is active
            }
            else
            {
                Debug.Log("Starting spawning immediately (not waiting for objective)");
                StartSpawning();
            }
        }
        else
        {
            Debug.Log("Spawn on start is FALSE - waiting for manual trigger");
        }

        Debug.Log($"isSpawningActive after Start: {isSpawningActive}");
    }

    private void Update()
    {
        if (!isSpawningActive)
        {
            // DEBUG: Show why we're not spawning every 2 seconds
            if (Time.frameCount % 120 == 0) // Every ~2 seconds at 60fps
            {
                Debug.Log($"NOT SPAWNING - isSpawningActive: {isSpawningActive}, objectiveComplete: {objectiveComplete}");
            }
            return;
        }

        // Continuous spawning mode
        if (continuousSpawning)
        {
            if (Time.time - lastSpawnTime >= spawnInterval)
            {
                CleanupDestroyedEnemies();

                if (showDebugLogs)
                {
                    Debug.Log($"Spawn check - Active enemies: {spawnedEnemies.Count}/{maxActiveEnemies}, Total spawned: {spawnedCount}, Can spawn more: {CanSpawnMore()}");
                }

                if (CanSpawnMore())
                {
                    SpawnEnemy();
                }
                else
                {
                    if (showDebugLogs)
                    {
                        Debug.Log($"Cannot spawn more - Active: {spawnedEnemies.Count}/{maxActiveEnemies}");
                    }
                }
            }
        }
        // Original timed spawning mode
        else if (!spawnAllAtOnce && spawnedCount < enemiesToSpawn)
        {
            if (Time.time - lastSpawnTime >= spawnInterval)
            {
                SpawnEnemy();
            }
        }
    }

    private void CheckObjectiveStatus()
    {
        Debug.Log("=== CheckObjectiveStatus Called ===");

        if (objectiveController == null)
        {
            Debug.Log("No objective controller - cannot check status");
            // If no objective controller and we're supposed to spawn, just start
            if (!onlySpawnDuringObjective && spawnOnStart)
            {
                Debug.Log("No objective requirement, starting spawning");
                StartSpawning();
            }
            return;
        }

        var currentObjective = objectiveController.GetCurrentObjective();

        if (currentObjective == null)
        {
            Debug.Log("No active objective");
            if (isSpawningActive)
            {
                Debug.Log("Stopping spawn - no active objective");
                StopSpawning();
            }
            return;
        }

        Debug.Log($"Current objective: {currentObjective.objectiveName}");
        Debug.Log($"Target objective: {targetObjectiveName}");
        Debug.Log($"Objective complete: {currentObjective.isComplete}");

        // Check if this is our target objective
        bool isTargetObjective = string.IsNullOrEmpty(targetObjectiveName) ||
                                 currentObjective.objectiveName == targetObjectiveName;

        Debug.Log($"Is target objective: {isTargetObjective}");

        if (!isTargetObjective)
        {
            if (isSpawningActive)
            {
                Debug.Log("Not target objective - stopping spawn");
                StopSpawning();
            }
            return;
        }

        // Check task-specific completion if needed
        if (!string.IsNullOrEmpty(targetTaskName))
        {
            bool taskComplete = objectiveController.IsTaskComplete(targetTaskName);
            Debug.Log($"Target task '{targetTaskName}' complete: {taskComplete}");

            if (taskComplete)
            {
                Debug.Log($"Target task complete - stopping spawn");
                objectiveComplete = true;
                if (stopOnObjectiveComplete)
                {
                    StopSpawning();
                }
                return;
            }
        }

        // Check objective completion
        if (stopOnObjectiveComplete && currentObjective.isComplete)
        {
            Debug.Log("Objective complete - stopping spawn");
            objectiveComplete = true;
            StopSpawning();
            return;
        }

        // If we got here and we're the target objective, start spawning
        if (!isSpawningActive)
        {
            Debug.Log("Target objective is active and not complete - STARTING SPAWNING");
            StartSpawning();
        }
        else
        {
            Debug.Log("Already spawning");
        }
    }

    private bool CanSpawnMore()
    {
        // Check if objective is complete
        if (objectiveComplete && stopOnObjectiveComplete)
        {
            if (showDebugLogs)
            {
                Debug.Log("Cannot spawn - objective complete");
            }
            return false;
        }

        // For continuous spawning
        if (continuousSpawning)
        {
            // Check total spawn limit
            if (maxTotalSpawns > 0 && spawnedCount >= maxTotalSpawns)
            {
                if (showDebugLogs)
                {
                    Debug.Log("Cannot spawn - hit max total spawns");
                }
                return false;
            }

            // Check active enemy limit
            CleanupDestroyedEnemies();
            int activeCount = spawnedEnemies.Count;

            if (activeCount >= maxActiveEnemies)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"Cannot spawn - at max active enemies ({activeCount}/{maxActiveEnemies})");
                }
                return false;
            }

            return true;
        }
        // For regular spawning
        else
        {
            return spawnedCount < enemiesToSpawn;
        }
    }

    public void StartSpawning()
    {
        Debug.Log("=== StartSpawning Called ===");

        if (isSpawningActive)
        {
            Debug.Log("Already spawning - ignoring");
            return;
        }

        isSpawningActive = true;
        objectiveComplete = false;
        lastSpawnTime = Time.time; // Reset timer

        string mode = continuousSpawning ? "continuous" : "limited";
        Debug.Log($"✓ SPAWNING STARTED ({mode} mode)");
        Debug.Log($"  - Max Active: {maxActiveEnemies}");
        Debug.Log($"  - Spawn Interval: {spawnInterval}s");
        Debug.Log($"  - isSpawningActive: {isSpawningActive}");

        // If spawn all at once, do it now
        if (spawnAllAtOnce && !continuousSpawning)
        {
            SpawnAllEnemies();
        }
        // Otherwise spawn first enemy immediately
        else
        {
            Debug.Log("Spawning first enemy immediately...");
            SpawnEnemy();
        }
    }

    public void StopSpawning()
    {
        if (!isSpawningActive)
        {
            Debug.Log("Already stopped - ignoring");
            return;
        }

        isSpawningActive = false;
        Debug.Log($"✗ SPAWNING STOPPED (Total spawned: {spawnedCount})");
    }

    public void SpawnAllEnemies()
    {
        Debug.Log($"Spawning all {enemiesToSpawn} enemies at once");

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnEnemy();
        }
    }

    public GameObject SpawnEnemy()
    {
        Debug.Log("=== SpawnEnemy Called ===");

        // Check if we can spawn
        if (!continuousSpawning && spawnedCount >= enemiesToSpawn)
        {
            Debug.Log("Cannot spawn - reached enemy limit (non-continuous mode)");
            return null;
        }

        if (continuousSpawning && !CanSpawnMore())
        {
            Debug.Log("Cannot spawn - CanSpawnMore returned false");
            return null;
        }

        if (spawnController == null)
        {
            Debug.LogError("Cannot spawn - no spawn controller!");
            return null;
        }

        GameObject spawnedEnemy = null;

        // Spawn using your existing controller
        if (useSpecificArea)
        {
            Debug.Log($"Spawning in specific area {specificAreaIndex}");
            int enemyIndex = spawnRandomTypes ? -1 : specificEnemyIndex;
            spawnedEnemy = spawnController.SpawnEnemyInArea(specificAreaIndex, enemyIndex);
        }
        else
        {
            Debug.Log("Spawning using controller's area selection");
            if (spawnRandomTypes)
            {
                spawnedEnemy = spawnController.SpawnRandomEnemy();
            }
            else
            {
                spawnedEnemy = spawnController.SpawnEnemyByIndex(specificEnemyIndex);
            }
        }

        if (spawnedEnemy == null)
        {
            Debug.LogWarning("Spawn controller returned null!");
            return null;
        }

        spawnedCount++;
        lastSpawnTime = Time.time;
        spawnedEnemies.Add(spawnedEnemy);

        // AUTOMATICALLY register with ObjectiveManager
        RegisterEnemyWithObjectives(spawnedEnemy);

        if (continuousSpawning)
        {
            Debug.Log($"✓ Spawned enemy {spawnedCount} (Active: {spawnedEnemies.Count}/{maxActiveEnemies})");
        }
        else
        {
            Debug.Log($"✓ Spawned enemy {spawnedCount}/{enemiesToSpawn}");
        }

        return spawnedEnemy;
    }

    private void RegisterEnemyWithObjectives(GameObject enemy)
    {
        Health enemyHealth = enemy.GetComponent<Health>();

        if (enemyHealth == null)
        {
            Debug.LogWarning($"Enemy {enemy.name} has no Health component!");
            return;
        }

        if (ObjectiveManager.Instance == null)
        {
            // Try to find ANY ObjectiveManager and use it
            ObjectiveManager[] allManagers = FindObjectsOfType<ObjectiveManager>();
            if (allManagers.Length > 0)
            {
                Debug.Log($"Registering with {allManagers.Length} ObjectiveManagers");
                foreach (var manager in allManagers)
                {
                    manager.RegisterEnemy(enemyHealth);
                }
            }
            else
            {
                Debug.LogWarning("No ObjectiveManager found to register enemy!");
            }
            return;
        }

        // Register with the active instance
        ObjectiveManager.Instance.RegisterEnemy(enemyHealth);
        Debug.Log($"Registered {enemy.name} with ObjectiveManager");
    }

    public void SpawnWave(int count)
    {
        Debug.Log($"Spawning wave of {count} enemies");

        for (int i = 0; i < count; i++)
        {
            SpawnEnemy();
        }
    }

    public void SpawnWaveInArea(int count, int areaIndex)
    {
        Debug.Log($"Spawning wave of {count} enemies in area {areaIndex}");

        // Temporarily override area settings
        bool originalUseSpecific = useSpecificArea;
        int originalAreaIndex = specificAreaIndex;

        useSpecificArea = true;
        specificAreaIndex = areaIndex;

        for (int i = 0; i < count; i++)
        {
            SpawnEnemy();
        }

        // Restore original settings
        useSpecificArea = originalUseSpecific;
        specificAreaIndex = originalAreaIndex;
    }

    public int GetSpawnedCount() => spawnedCount;
    public int GetRemainingCount() => enemiesToSpawn - spawnedCount;
    public List<GameObject> GetSpawnedEnemies() => new List<GameObject>(spawnedEnemies);

    public void CleanupDestroyedEnemies()
    {
        int before = spawnedEnemies.Count;
        spawnedEnemies.RemoveAll(enemy => enemy == null);
        int after = spawnedEnemies.Count;

        if (before != after && showDebugLogs)
        {
            Debug.Log($"Cleaned up {before - after} destroyed enemies. Active count: {after}");
        }
    }

    public void ResetSpawner()
    {
        spawnedCount = 0;
        lastSpawnTime = 0f;
        spawnedEnemies.Clear();
        isSpawningActive = false;
        objectiveComplete = false;
        Debug.Log("Spawner reset");
    }

    public int GetLivingEnemyCount()
    {
        CleanupDestroyedEnemies();
        return spawnedEnemies.Count;
    }

    public bool IsSpawning() => isSpawningActive;

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (objectiveController != null)
        {
            objectiveController.onTaskCompleted.RemoveListener(CheckObjectiveStatus);
            objectiveController.onObjectiveChanged.RemoveListener(CheckObjectiveStatus);
        }
    }
}