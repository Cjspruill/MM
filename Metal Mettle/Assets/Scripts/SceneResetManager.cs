using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent manager that handles resetting controllers after scene reload
/// This persists across scene loads to trigger resets
/// </summary>
public class SceneResetManager : MonoBehaviour
{
    private static SceneResetManager instance;
    private static bool shouldResetOnNextLoad = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private void Awake()
    {
        // Singleton that persists across scenes
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (debugLogs)
        {
            Debug.Log("SceneResetManager: Initialized and persisting across scenes");
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Call this before reloading a scene to trigger reset after load
    /// </summary>
    public static void MarkForReset()
    {
        shouldResetOnNextLoad = true;

        if (instance != null && instance.debugLogs)
        {
            Debug.Log("SceneResetManager: Scene marked for reset on next load");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (shouldResetOnNextLoad)
        {
            shouldResetOnNextLoad = false;

            if (debugLogs)
            {
                Debug.Log($"SceneResetManager: Scene '{scene.name}' loaded, starting reset sequence...");
            }

            // Wait a frame to ensure all objects are initialized
            StartCoroutine(ResetControllersAfterFrame());
        }
    }

    private System.Collections.IEnumerator ResetControllersAfterFrame()
    {
        // Wait for end of frame to ensure all Start() methods have run
        yield return new WaitForEndOfFrame();

        if (debugLogs)
        {
            Debug.Log("SceneResetManager: Executing controller resets...");
        }

        ResetAllControllers();
    }

    private void ResetAllControllers()
    {
        int resetCount = 0;

        // Reset ObjectiveController
        ObjectiveController objectiveController = FindObjectOfType<ObjectiveController>();
        if (objectiveController != null)
        {
            objectiveController.ResetObjectives();
            resetCount++;
            if (debugLogs)
            {
                Debug.Log("SceneResetManager: ✓ ObjectiveController reset");
            }
        }
        else if (debugLogs)
        {
            Debug.LogWarning("SceneResetManager: ObjectiveController not found");
        }

        // Reset MaskController
        MaskController maskController = FindObjectOfType<MaskController>();
        if (maskController != null)
        {
            maskController.ResetMask();
            resetCount++;
            if (debugLogs)
            {
                Debug.Log("SceneResetManager: ✓ MaskController reset");
            }
        }
        else if (debugLogs)
        {
            Debug.LogWarning("SceneResetManager: MaskController not found");
        }

        // Reset EnemySpawnController
        EnemySpawnController spawnController = FindObjectOfType<EnemySpawnController>();
        if (spawnController != null)
        {
            spawnController.ClearSpawnPositions();
            resetCount++;
            if (debugLogs)
            {
                Debug.Log("SceneResetManager: ✓ EnemySpawnController spawn positions cleared");
            }
        }
        else if (debugLogs)
        {
            Debug.LogWarning("SceneResetManager: EnemySpawnController not found");
        }

        if (debugLogs)
        {
            Debug.Log($"SceneResetManager: Reset complete! ({resetCount} controllers reset)");
        }
    }

    /// <summary>
    /// Manual reset trigger for testing
    /// </summary>
    [ContextMenu("Force Reset All Controllers")]
    public void ForceReset()
    {
        Debug.Log("SceneResetManager: Manual reset triggered");
        ResetAllControllers();
    }
}