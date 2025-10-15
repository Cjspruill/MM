using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced spawner that works with your EnemySpawnController (with multiple spawn areas)
/// Automatically registers all spawned enemies with ObjectiveManager
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

    private void Start()
    {
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

        if (spawnOnStart)
        {
            if (spawnAllAtOnce)
            {
                SpawnAllEnemies();
            }
            else
            {
                // Spawn first enemy immediately
                SpawnEnemy();
            }
        }
    }

    private void Update()
    {
        // Spawn enemies over time
        if (!spawnAllAtOnce && spawnedCount < enemiesToSpawn)
        {
            if (Time.time - lastSpawnTime >= spawnInterval)
            {
                SpawnEnemy();
            }
        }
    }

    public void SpawnAllEnemies()
    {
        if (showDebugLogs)
        {
            Debug.Log($"EnemySpawnerWithObjectives: Spawning all {enemiesToSpawn} enemies at once");
        }

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnEnemy();
        }
    }

    public GameObject SpawnEnemy()
    {
        if (spawnedCount >= enemiesToSpawn)
        {
            if (showDebugLogs)
            {
                Debug.Log("EnemySpawnerWithObjectives: All enemies spawned!");
            }
            return null;
        }

        if (spawnController == null)
        {
            Debug.LogError("EnemySpawnerWithObjectives: No spawn controller assigned!");
            return null;
        }

        GameObject spawnedEnemy = null;

        // Spawn using your existing controller
        if (useSpecificArea)
        {
            // Spawn in specific area
            int enemyIndex = spawnRandomTypes ? -1 : specificEnemyIndex;
            spawnedEnemy = spawnController.SpawnEnemyInArea(specificAreaIndex, enemyIndex);
        }
        else
        {
            // Use spawn controller's area selection logic
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
            Debug.LogWarning("EnemySpawnerWithObjectives: Failed to spawn enemy!");
            return null;
        }

        spawnedCount++;
        lastSpawnTime = Time.time;
        spawnedEnemies.Add(spawnedEnemy);

        // AUTOMATICALLY register with ObjectiveManager
        RegisterEnemyWithObjectives(spawnedEnemy);

        if (showDebugLogs)
        {
            Debug.Log($"EnemySpawnerWithObjectives: Spawned and registered enemy {spawnedCount}/{enemiesToSpawn}");
        }

        return spawnedEnemy;
    }

    private void RegisterEnemyWithObjectives(GameObject enemy)
    {
        Health enemyHealth = enemy.GetComponent<Health>();

        if (enemyHealth == null)
        {
            Debug.LogWarning($"EnemySpawnerWithObjectives: Enemy {enemy.name} has no Health component!");
            return;
        }

        if (ObjectiveManager.Instance == null)
        {
            Debug.LogWarning("EnemySpawnerWithObjectives: No active ObjectiveManager found in scene!");

            // Try to find ANY ObjectiveManager and use it
            ObjectiveManager[] allManagers = FindObjectsOfType<ObjectiveManager>();
            if (allManagers.Length > 0)
            {
                Debug.Log($"EnemySpawnerWithObjectives: Found {allManagers.Length} ObjectiveManagers, registering with all active ones");
                foreach (var manager in allManagers)
                {
                    manager.RegisterEnemy(enemyHealth);
                }
            }
            return;
        }

        // Register with the active instance
        ObjectiveManager.Instance.RegisterEnemy(enemyHealth);

        if (showDebugLogs)
        {
            Debug.Log($"EnemySpawnerWithObjectives: Registered {enemy.name} with active ObjectiveManager");
        }
    }

    public void SpawnWave(int count)
    {
        if (showDebugLogs)
        {
            Debug.Log($"EnemySpawnerWithObjectives: Spawning wave of {count} enemies");
        }

        for (int i = 0; i < count; i++)
        {
            SpawnEnemy();
        }
    }

    /// <summary>
    /// Spawn enemies in a specific area (useful for room-based spawning)
    /// </summary>
    public void SpawnWaveInArea(int count, int areaIndex)
    {
        if (showDebugLogs)
        {
            Debug.Log($"EnemySpawnerWithObjectives: Spawning wave of {count} enemies in area {areaIndex}");
        }

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

    // Clean up destroyed enemies from list
    public void CleanupDestroyedEnemies()
    {
        spawnedEnemies.RemoveAll(enemy => enemy == null);
    }

    // Reset spawner
    public void ResetSpawner()
    {
        spawnedCount = 0;
        lastSpawnTime = 0f;
        spawnedEnemies.Clear();
    }

    // Get count of living enemies
    public int GetLivingEnemyCount()
    {
        CleanupDestroyedEnemies();
        return spawnedEnemies.Count;
    }
}