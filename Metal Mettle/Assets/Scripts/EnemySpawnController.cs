using UnityEngine;
using System.Collections.Generic;

public class EnemySpawnController : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();

    [Header("Spawn Area")]
    [SerializeField] private BoxCollider spawnArea;
    [SerializeField] private LayerMask spawnBlockLayer;

    [Header("Spawn Settings")]
    [SerializeField] private int maxSpawnAttempts = 10;
    [SerializeField] private float spawnCheckRadius = 1f;
    [SerializeField] private float minDistanceBetweenSpawns = 2f;

    [Header("Rotation Settings")]
    [SerializeField] private bool randomRotation = true;
    [SerializeField] private bool randomYAxisOnly = true; // Only rotate on Y axis (typical for ground enemies)

    [Header("Testing")]
    [SerializeField] private bool spawnOnStart = false;
    [SerializeField] private int spawnCountOnStart = 1;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool enableDebugLogs = true;

    private List<Vector3> activeSpawnPositions = new List<Vector3>();

    private void Awake()
    {
        // Clear spawn positions on scene load to fix restart issue
        activeSpawnPositions.Clear();
    }

    private void Start()
    {
        // Find or validate spawn area
        if (spawnArea == null)
        {
            spawnArea = GetComponent<BoxCollider>();
            if (spawnArea == null)
            {
                Debug.LogError("EnemySpawnController: No BoxCollider found! Please assign a spawn area or add a BoxCollider component.");
                return;
            }
        }

        // Validate setup
        if (enemyPrefabs.Count == 0)
        {
            Debug.LogError("EnemySpawnController: No enemy prefabs assigned! Add prefabs to the list.");
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"EnemySpawnController initialized with {enemyPrefabs.Count} enemy prefabs");
            Debug.Log($"Spawn area bounds: {spawnArea.bounds}");
            Debug.Log($"Spawn block layer mask value: {spawnBlockLayer.value}");
            Debug.Log($"Random rotation: {randomRotation} (Y-axis only: {randomYAxisOnly})");
        }

        // Test spawn on start if enabled
        if (spawnOnStart)
        {
            Debug.Log($"Spawning {spawnCountOnStart} enemies on start...");
            SpawnMultipleEnemies(spawnCountOnStart);
        }
    }

    /// <summary>
    /// Spawns a random enemy from the prefab list
    /// </summary>
    public GameObject SpawnRandomEnemy()
    {
        if (enemyPrefabs.Count == 0)
        {
            Debug.LogWarning("EnemySpawnController: No enemy prefabs assigned to spawn!");
            return null;
        }

        if (spawnArea == null)
        {
            Debug.LogError("EnemySpawnController: No spawn area assigned!");
            return null;
        }

        Vector3 spawnPosition = GetValidSpawnPosition();

        if (spawnPosition == Vector3.zero)
        {
            Debug.LogWarning($"EnemySpawnController: Could not find valid spawn position after {maxSpawnAttempts} attempts.");
            Debug.LogWarning("Try increasing maxSpawnAttempts, decreasing spawnCheckRadius, or checking your spawn block layer.");
            return null;
        }

        int randomIndex = Random.Range(0, enemyPrefabs.Count);

        if (enemyPrefabs[randomIndex] == null)
        {
            Debug.LogError($"EnemySpawnController: Enemy prefab at index {randomIndex} is null!");
            return null;
        }

        Quaternion spawnRotation = GetRandomRotation();
        GameObject enemy = Instantiate(enemyPrefabs[randomIndex], spawnPosition, spawnRotation);

        activeSpawnPositions.Add(spawnPosition);

        if (enableDebugLogs)
        {
            Debug.Log($"EnemySpawnController: Spawned {enemy.name} at {spawnPosition} with rotation {spawnRotation.eulerAngles}");
        }

        return enemy;
    }

    /// <summary>
    /// Spawns a specific enemy by index
    /// </summary>
    public GameObject SpawnEnemyByIndex(int index)
    {
        if (index < 0 || index >= enemyPrefabs.Count)
        {
            Debug.LogError($"EnemySpawnController: Enemy index {index} out of range! Valid range: 0-{enemyPrefabs.Count - 1}");
            return null;
        }

        if (enemyPrefabs[index] == null)
        {
            Debug.LogError($"EnemySpawnController: Enemy prefab at index {index} is null!");
            return null;
        }

        Vector3 spawnPosition = GetValidSpawnPosition();

        if (spawnPosition == Vector3.zero)
        {
            Debug.LogWarning("EnemySpawnController: Could not find valid spawn position.");
            return null;
        }

        Quaternion spawnRotation = GetRandomRotation();
        GameObject enemy = Instantiate(enemyPrefabs[index], spawnPosition, spawnRotation);
        activeSpawnPositions.Add(spawnPosition);

        if (enableDebugLogs)
        {
            Debug.Log($"EnemySpawnController: Spawned {enemy.name} at {spawnPosition} with rotation {spawnRotation.eulerAngles}");
        }

        return enemy;
    }

    /// <summary>
    /// Spawns multiple random enemies
    /// </summary>
    public List<GameObject> SpawnMultipleEnemies(int count)
    {
        List<GameObject> spawnedEnemies = new List<GameObject>();

        for (int i = 0; i < count; i++)
        {
            GameObject enemy = SpawnRandomEnemy();
            if (enemy != null)
            {
                spawnedEnemies.Add(enemy);
            }
        }

        if (enableDebugLogs)
        {
            Debug.Log($"EnemySpawnController: Successfully spawned {spawnedEnemies.Count} out of {count} requested enemies");
        }

        return spawnedEnemies;
    }

    /// <summary>
    /// Gets a random rotation based on settings
    /// </summary>
    private Quaternion GetRandomRotation()
    {
        if (!randomRotation)
        {
            return Quaternion.identity;
        }

        if (randomYAxisOnly)
        {
            // Random rotation only on Y axis (0-360 degrees)
            float randomYRotation = Random.Range(0f, 360f);
            return Quaternion.Euler(0f, randomYRotation, 0f);
        }
        else
        {
            // Completely random rotation on all axes
            return Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
            );
        }
    }

    /// <summary>
    /// Gets a valid spawn position within the spawn area
    /// </summary>
    private Vector3 GetValidSpawnPosition()
    {
        if (spawnArea == null)
        {
            Debug.LogError("EnemySpawnController: Spawn area is null!");
            return Vector3.zero;
        }

        Bounds bounds = spawnArea.bounds;

        if (enableDebugLogs)
        {
            Debug.Log($"EnemySpawnController: Searching for spawn position in bounds {bounds.min} to {bounds.max}");
        }

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            // Generate random position within bounds
            Vector3 randomPosition = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            if (enableDebugLogs && i == 0)
            {
                Debug.Log($"EnemySpawnController: Attempt {i + 1} - Testing position {randomPosition}");
            }

            // Check if position is valid
            if (IsValidSpawnPosition(randomPosition))
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"EnemySpawnController: Found valid position at {randomPosition} on attempt {i + 1}");
                }
                return randomPosition;
            }
        }

        Debug.LogWarning($"EnemySpawnController: Failed to find valid position after {maxSpawnAttempts} attempts");
        return Vector3.zero; // Failed to find valid position
    }

    /// <summary>
    /// Checks if a position is valid for spawning
    /// </summary>
    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check if position overlaps with spawn block layer
        Collider[] blockColliders = Physics.OverlapSphere(position, spawnCheckRadius, spawnBlockLayer);
        if (blockColliders.Length > 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"EnemySpawnController: Position {position} blocked by {blockColliders.Length} colliders on block layer");
                foreach (Collider col in blockColliders)
                {
                    Debug.Log($"  - Blocked by: {col.gameObject.name} on layer {LayerMask.LayerToName(col.gameObject.layer)}");
                }
            }
            return false;
        }

        // Check minimum distance from other spawn positions
        foreach (Vector3 spawnPos in activeSpawnPositions)
        {
            if (Vector3.Distance(position, spawnPos) < minDistanceBetweenSpawns)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"EnemySpawnController: Position {position} too close to existing spawn at {spawnPos}");
                }
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Clears the list of active spawn positions
    /// </summary>
    public void ClearSpawnPositions()
    {
        activeSpawnPositions.Clear();
        if (enableDebugLogs)
        {
            Debug.Log("EnemySpawnController: Cleared all spawn positions");
        }
    }

    /// <summary>
    /// Removes a spawn position from tracking (call when enemy dies)
    /// </summary>
    public void RemoveSpawnPosition(Vector3 position)
    {
        activeSpawnPositions.Remove(position);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Draw spawn area bounds
        if (spawnArea != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(spawnArea.bounds.center, spawnArea.bounds.size);
        }

        // Draw active spawn positions
        Gizmos.color = Color.red;
        foreach (Vector3 pos in activeSpawnPositions)
        {
            Gizmos.DrawWireSphere(pos, spawnCheckRadius);
        }

        // Draw spawn check radius at center for reference
        if (spawnArea != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnArea.bounds.center, spawnCheckRadius);
        }
    }

    // Editor testing method
    [ContextMenu("Test Spawn One Enemy")]
    private void TestSpawnOne()
    {
        SpawnRandomEnemy();
    }

    [ContextMenu("Test Spawn Five Enemies")]
    private void TestSpawnFive()
    {
        SpawnMultipleEnemies(5);
    }
}