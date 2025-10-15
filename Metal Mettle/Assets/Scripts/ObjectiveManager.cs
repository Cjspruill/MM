using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CENTRALIZED objective tracking - attach ONE component to ONE GameObject
/// Automatically tracks: kills, hits, blood, triggers - NO per-enemy setup needed!
/// IMPORTANT: Only ONE ObjectiveManager should be active at a time!
/// </summary>
public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private BloodSystem bloodSystem;

    [Header("Activation")]
    [SerializeField] private bool startActive = true;
    [Tooltip("Which objective index this manager is for (0 = first objective, 1 = second, etc.)")]
    [SerializeField] private int targetObjectiveIndex = 0;

    [Header("Kill Tracking")]
    [SerializeField] private bool trackKills = true;
    [SerializeField] private int killsRequired = 10;
    [SerializeField] private string killTaskName = "Kill 10 Enemies";
    private int currentKills = 0;

    [Header("Hit Tracking (Any Enemy)")]
    [SerializeField] private bool trackHits = true;
    [SerializeField] private int hitsRequired = 10;
    [SerializeField] private string hitTaskName = "Land 10 Attacks";
    private int currentHits = 0;

    [Header("Blood Tracking")]
    [SerializeField] private bool trackBloodGain = true;
    [SerializeField] private float bloodRequired = 10f;
    [SerializeField] private string bloodTaskName = "Gain 10 Blood";
    private float startingBlood = 0f;

    [Header("Events")]
    public UnityEvent<int> onKillCountChanged;
    public UnityEvent<int> onHitCountChanged;
    public UnityEvent<float> onBloodChanged;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool killTaskComplete = false;
    private bool hitTaskComplete = false;
    private bool bloodTaskComplete = false;
    private bool isActive = false;

    private void Awake()
    {
        // Singleton - but allow multiple managers, just swap active one
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // Auto-find references
        if (objectiveController == null)
            objectiveController = FindFirstObjectByType<ObjectiveController>();

        if (bloodSystem == null)
            bloodSystem = FindFirstObjectByType<BloodSystem>();

        // Record starting blood
        if (bloodSystem != null)
        {
            startingBlood = bloodSystem.currentBlood;
        }

        // Check if this manager should be active based on current objective
        CheckIfShouldBeActive();

        // Subscribe to objective changes
        if (objectiveController != null)
        {
            objectiveController.onObjectiveChanged.AddListener(OnObjectiveChanged);
        }

        // DEBUG: Show what we're tracking
        if (showDebugLogs)
        {
            Debug.Log($"=== ObjectiveManager '{gameObject.name}' Start ===");
            Debug.Log($"Target Objective Index: {targetObjectiveIndex}");
            Debug.Log($"Start Active: {startActive}");
            Debug.Log($"Is Active: {isActive}");
            Debug.Log($"Tracking Kills: {trackKills} - Task: '{killTaskName}'");
            Debug.Log($"Tracking Hits: {trackHits} - Task: '{hitTaskName}'");
            Debug.Log($"Tracking Blood: {trackBloodGain} - Task: '{bloodTaskName}'");
        }

        if (isActive)
        {
            ActivateTracking();
        }
    }

    private void CheckIfShouldBeActive()
    {
        if (objectiveController == null)
        {
            isActive = startActive;
            return;
        }

        var currentObj = objectiveController.GetCurrentObjective();
        if (currentObj == null)
        {
            isActive = false;
            return;
        }

        // Get current objective index
        var allObjectives = objectiveController.GetAllObjectives();
        int currentIndex = allObjectives.IndexOf(currentObj);

        // Activate if we're on the right objective
        isActive = (currentIndex == targetObjectiveIndex);

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Current objective index is {currentIndex}, target is {targetObjectiveIndex}. Active: {isActive}");
        }
    }

    private void OnObjectiveChanged()
    {
        // When objective changes, check if we should activate/deactivate
        bool wasActive = isActive;
        CheckIfShouldBeActive();

        // If we just became active, register everything
        if (isActive && !wasActive)
        {
            if (showDebugLogs)
            {
                Debug.Log($"ObjectiveManager '{gameObject.name}' ACTIVATED for objective {targetObjectiveIndex}");
            }

            // Reset tracking for new objective
            ResetTracking();

            // Re-register all enemies (they might have been spawned while we were inactive)
            RegisterAllExistingEnemies();

            // Set as active instance
            Instance = this;
        }
        else if (!isActive && wasActive)
        {
            if (showDebugLogs)
            {
                Debug.Log($"ObjectiveManager '{gameObject.name}' DEACTIVATED");
            }
            DeactivateTracking();
        }
    }

    private void ActivateTracking()
    {
        // Set as active instance
        Instance = this;

        // Register all existing enemies
        RegisterAllExistingEnemies();

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}' tracking activated!");
        }
    }

    private void DeactivateTracking()
    {
        // Unregister all enemies before deactivating
        UnregisterAllEnemies();

        // Clear instance if we were it
        if (Instance == this)
        {
            Instance = null;
        }

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}' tracking deactivated!");
        }
    }

    private void Update()
    {
        if (!isActive) return; // Don't track if not active!

        // Check blood threshold
        if (trackBloodGain && !bloodTaskComplete && bloodSystem != null)
        {
            float bloodGained = bloodSystem.currentBlood - startingBlood;

            if (bloodGained >= bloodRequired)
            {
                CompleteBloodTask();
            }

            onBloodChanged?.Invoke(bloodGained);
        }
    }

    // Store references to registered enemies and their callbacks
    private Dictionary<Health, System.Action> damageCallbacks = new Dictionary<Health, System.Action>();
    private Dictionary<Health, System.Action> deathCallbacks = new Dictionary<Health, System.Action>();

    /// <summary>
    /// Automatically finds and subscribes to all enemies with Health component
    /// </summary>
    private void RegisterAllExistingEnemies()
    {
        if (!isActive) return;

        Health[] allEnemies = FindObjectsByType<Health>(FindObjectsSortMode.None);

        int registeredCount = 0;
        foreach (Health enemy in allEnemies)
        {
            // Only register if tagged as Enemy and is not the player
            if (enemy != null && enemy.CompareTag("Enemy"))
            {
                RegisterEnemy(enemy);
                registeredCount++;
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Registered {registeredCount} existing enemies");
        }
    }

    /// <summary>
    /// Call this when spawning new enemies - auto-subscribes them to tracking
    /// Only the ACTIVE manager will register them!
    /// </summary>
    public void RegisterEnemy(Health enemy)
    {
        if (enemy == null) return;

        // IMPORTANT: Check if this manager is active OR if it's the Instance
        bool shouldRegister = isActive || (Instance == this);

        if (!shouldRegister)
        {
            if (showDebugLogs)
            {
                Debug.Log($"ObjectiveManager '{gameObject.name}': Skipping registration (not active). Enemy: {enemy.gameObject.name}");
            }
            return;
        }

        // Don't register twice
        if (damageCallbacks.ContainsKey(enemy) || deathCallbacks.ContainsKey(enemy))
        {
            if (showDebugLogs)
            {
                Debug.Log($"ObjectiveManager '{gameObject.name}': Enemy {enemy.gameObject.name} already registered, skipping");
            }
            return;
        }

        // Create and store the callback references so we can unsubscribe later
        if (trackHits)
        {
            System.Action damageCallback = () => OnEnemyHit(enemy);
            damageCallbacks[enemy] = damageCallback;
            enemy.onDamage.AddListener(new UnityEngine.Events.UnityAction(damageCallback));
        }

        if (trackKills)
        {
            System.Action deathCallback = () => OnEnemyKilled(enemy);
            deathCallbacks[enemy] = deathCallback;
            enemy.onDeath.AddListener(new UnityEngine.Events.UnityAction(deathCallback));
        }

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Successfully registered enemy {enemy.gameObject.name}. Total: {damageCallbacks.Count}");
        }
    }

    private void UnregisterAllEnemies()
    {
        // Unsubscribe damage callbacks
        foreach (var kvp in damageCallbacks)
        {
            if (kvp.Key != null)
            {
                kvp.Key.onDamage.RemoveListener(new UnityEngine.Events.UnityAction(kvp.Value));
            }
        }
        damageCallbacks.Clear();

        // Unsubscribe death callbacks
        foreach (var kvp in deathCallbacks)
        {
            if (kvp.Key != null)
            {
                kvp.Key.onDeath.RemoveListener(new UnityEngine.Events.UnityAction(kvp.Value));
            }
        }
        deathCallbacks.Clear();

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Unregistered all enemies");
        }
    }

    private void OnEnemyHit(Health enemy)
    {
        if (!isActive) return; // Don't count if not active!
        if (hitTaskComplete) return;

        currentHits++;

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Hit count {currentHits}/{hitsRequired}");
        }

        onHitCountChanged?.Invoke(currentHits);

        if (currentHits >= hitsRequired)
        {
            CompleteHitTask();
        }
    }

    private void OnEnemyKilled(Health enemy)
    {
        if (!isActive) return; // Don't count if not active!
        if (killTaskComplete) return;

        currentKills++;

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Kill count {currentKills}/{killsRequired}");
        }

        onKillCountChanged?.Invoke(currentKills);

        if (currentKills >= killsRequired)
        {
            CompleteKillTask();
        }
    }

    private void CompleteHitTask()
    {
        if (hitTaskComplete) return;
        hitTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Hit objective complete! Attempting to complete task: '{hitTaskName}'");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(hitTaskName);
        }
        else
        {
            Debug.LogError("ObjectiveManager: ObjectiveController is null!");
        }
    }

    private void CompleteKillTask()
    {
        if (killTaskComplete) return;
        killTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Kill objective complete! Attempting to complete task: '{killTaskName}'");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(killTaskName);
        }
        else
        {
            Debug.LogError("ObjectiveManager: ObjectiveController is null!");
        }
    }

    private void CompleteBloodTask()
    {
        if (bloodTaskComplete) return;
        bloodTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Blood objective complete! Attempting to complete task: '{bloodTaskName}'");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(bloodTaskName);
        }
        else
        {
            Debug.LogError("ObjectiveManager: ObjectiveController is null!");
        }
    }

    // Public getters for UI
    public int GetKillCount() => currentKills;
    public int GetKillsRequired() => killsRequired;
    public float GetKillProgress() => (float)currentKills / killsRequired;

    public int GetHitCount() => currentHits;
    public int GetHitsRequired() => hitsRequired;
    public float GetHitProgress() => (float)currentHits / hitsRequired;

    public float GetBloodGained() => bloodSystem != null ? bloodSystem.currentBlood - startingBlood : 0f;
    public float GetBloodRequired() => bloodRequired;
    public float GetBloodProgress() => GetBloodGained() / bloodRequired;

    /// <summary>
    /// Manually activate this manager (if you want manual control)
    /// </summary>
    public void Activate()
    {
        isActive = true;
        Instance = this;
        ActivateTracking();
    }

    /// <summary>
    /// Manually deactivate this manager
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
        DeactivateTracking();
    }

    /// <summary>
    /// Reset all tracking (for respawn or level restart)
    /// </summary>
    public void ResetTracking()
    {
        currentKills = 0;
        currentHits = 0;
        killTaskComplete = false;
        hitTaskComplete = false;
        bloodTaskComplete = false;

        if (bloodSystem != null)
        {
            startingBlood = bloodSystem.currentBlood;
        }

        if (showDebugLogs)
        {
            Debug.Log($"ObjectiveManager '{gameObject.name}': Tracking reset");
        }
    }

    private void OnDestroy()
    {
        if (objectiveController != null)
        {
            objectiveController.onObjectiveChanged.RemoveListener(OnObjectiveChanged);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}