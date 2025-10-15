using UnityEngine;
using System.Collections.Generic;

public class EnemySpawnController : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();

    [Header("Spawn Areas")]
    [SerializeField] private BoxCollider[] spawnAreas; // CHANGED: Now an array
    [SerializeField] private LayerMask spawnBlockLayer;

    [Header("Spawn Settings")]
    [SerializeField] private int maxSpawnAttempts = 10;
    [SerializeField] private float spawnCheckRadius = 1f;
    [SerializeField] private float minDistanceBetweenSpawns = 2f;

    [Header("Area Selection")]
    [SerializeField] private bool useRandomArea = true; // Pick random spawn area each time
    [SerializeField] private int preferredAreaIndex = 0; // If not random, use this area

    [Header("Rotation Settings")]
    [SerializeField] private bool randomRotation = true;
    [SerializeField] private bool randomYAxisOnly = true;

    [Header("Testing")]
    [SerializeField] private bool spawnOnStart = false;
    [SerializeField] private int spawnCountOnStart = 1;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool enableDebugLogs = true;

    private List<Vector3> activeSpawnPositions = new List<Vector3>();

    private void Awake()
    {
        activeSpawnPositions.Clear();
    }

    private void Start()
    {
        // Find or validate spawn areas
        if (spawnAreas == null || spawnAreas.Length == 0)
        {
            // Try to find BoxColliders on this GameObject or children
            BoxCollider localCollider = GetComponent<BoxCollider>();
            if (localCollider != null)
            {
                spawnAreas = new BoxCollider[] { localCollider };
                Debug.Log("EnemySpawnController: Using BoxCollider on this GameObject as spawn area");
            }
            else
            {
                Debug.LogError("EnemySpawnController: No spawn areas assigned! Add BoxColliders to the array.");
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
            Debug.Log($"Spawn areas: {spawnAreas.Length}");
            for (int i = 0; i < spawnAreas.Length; i++)
            {
                if (spawnAreas[i] != null)
                {
                    Debug.Log($"  Area {i}: {spawnAreas[i].gameObject.name} - Bounds: {spawnAreas[i].bounds}");
                }
            }
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

        if (spawnAreas == null || spawnAreas.Length == 0)
        {
            Debug.LogError("EnemySpawnController: No spawn areas assigned!");
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
    /// Spawns an enemy in a specific area
    /// </summary>
    public GameObject SpawnEnemyInArea(int areaIndex, int enemyIndex = -1)
    {
        if (areaIndex < 0 || areaIndex >= spawnAreas.Length)
        {
            Debug.LogError($"EnemySpawnController: Area index {areaIndex} out of range!");
            return null;
        }

        if (spawnAreas[areaIndex] == null)
        {
            Debug.LogError($"EnemySpawnController: Spawn area at index {areaIndex} is null!");
            return null;
        }

        // Get spawn position from specific area
        Vector3 spawnPosition = GetValidSpawnPositionInArea(spawnAreas[areaIndex]);

        if (spawnPosition == Vector3.zero)
        {
            Debug.LogWarning($"EnemySpawnController: Could not find valid position in area {areaIndex}");
            return null;
        }

        // Spawn random or specific enemy
        int spawnIndex = enemyIndex >= 0 ? enemyIndex : Random.Range(0, enemyPrefabs.Count);

        if (spawnIndex >= enemyPrefabs.Count || enemyPrefabs[spawnIndex] == null)
        {
            Debug.LogError($"EnemySpawnController: Invalid enemy index {spawnIndex}");
            return null;
        }

        Quaternion spawnRotation = GetRandomRotation();
        GameObject enemy = Instantiate(enemyPrefabs[spawnIndex], spawnPosition, spawnRotation);
        activeSpawnPositions.Add(spawnPosition);

        if (enableDebugLogs)
        {
            Debug.Log($"EnemySpawnController: Spawned {enemy.name} in area {areaIndex} at {spawnPosition}");
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
            float randomYRotation = Random.Range(0f, 360f);
            return Quaternion.Euler(0f, randomYRotation, 0f);
        }
        else
        {
            return Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
            );
        }
    }

    /// <summary>
    /// Gets a valid spawn position from any spawn area
    /// </summary>
    private Vector3 GetValidSpawnPosition()
    {
        if (spawnAreas == null || spawnAreas.Length == 0)
        {
            Debug.LogError("EnemySpawnController: No spawn areas assigned!");
            return Vector3.zero;
        }

        // Choose which area to spawn in
        BoxCollider chosenArea;
        if (useRandomArea)
        {
            // Pick a random valid area
            List<BoxCollider> validAreas = new List<BoxCollider>();
            foreach (BoxCollider area in spawnAreas)
            {
                if (area != null) validAreas.Add(area);
            }

            if (validAreas.Count == 0)
            {
                Debug.LogError("EnemySpawnController: No valid spawn areas!");
                return Vector3.zero;
            }

            chosenArea = validAreas[Random.Range(0, validAreas.Count)];
        }
        else
        {
            // Use preferred area
            if (preferredAreaIndex >= spawnAreas.Length || spawnAreas[preferredAreaIndex] == null)
            {
                Debug.LogError($"EnemySpawnController: Preferred area index {preferredAreaIndex} is invalid!");
                return Vector3.zero;
            }
            chosenArea = spawnAreas[preferredAreaIndex];
        }

        return GetValidSpawnPositionInArea(chosenArea);
    }

    /// <summary>
    /// Gets a valid spawn position within a specific area
    /// </summary>
    private Vector3 GetValidSpawnPositionInArea(BoxCollider area)
    {
        if (area == null)
        {
            Debug.LogError("EnemySpawnController: Spawn area is null!");
            return Vector3.zero;
        }

        Bounds bounds = area.bounds;

        if (enableDebugLogs)
        {
            Debug.Log($"EnemySpawnController: Searching for spawn position in {area.gameObject.name} bounds {bounds.min} to {bounds.max}");
        }

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 randomPosition = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            if (enableDebugLogs && i == 0)
            {
                Debug.Log($"EnemySpawnController: Attempt {i + 1} - Testing position {randomPosition}");
            }

            if (IsValidSpawnPosition(randomPosition))
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"EnemySpawnController: Found valid position at {randomPosition} on attempt {i + 1}");
                }
                return randomPosition;
            }
        }

        Debug.LogWarning($"EnemySpawnController: Failed to find valid position in {area.gameObject.name} after {maxSpawnAttempts} attempts");
        return Vector3.zero;
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

        // Draw all spawn area bounds
        if (spawnAreas != null)
        {
            for (int i = 0; i < spawnAreas.Length; i++)
            {
                if (spawnAreas[i] != null)
                {
                    // Different color for each area
                    Gizmos.color = new Color(0f, 1f, 0f, 0.3f + (i * 0.1f));
                    Gizmos.DrawWireCube(spawnAreas[i].bounds.center, spawnAreas[i].bounds.size);

                    // Draw area index label
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(spawnAreas[i].bounds.center + Vector3.up * 2, $"Area {i}");
#endif
                }
            }
        }

        // Draw active spawn positions
        Gizmos.color = Color.red;
        foreach (Vector3 pos in activeSpawnPositions)
        {
            Gizmos.DrawWireSphere(pos, spawnCheckRadius);
        }
    }

    // Editor testing methods
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

    [ContextMenu("Test Spawn In Each Area")]
    private void TestSpawnInEachArea()
    {
        if (spawnAreas == null) return;

        for (int i = 0; i < spawnAreas.Length; i++)
        {
            SpawnEnemyInArea(i);
        }
    }
}