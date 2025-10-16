using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Enhanced ObjectiveManager that can track multiple objectives
/// Supports both combat objectives AND mask collection objectives seamlessly
/// </summary>
public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    [System.Serializable]
    public class ObjectiveTracking
    {
        [Header("Objective Settings")]
        public string objectiveName = "Objective 1";
        public int objectiveIndex = 0;

        [Header("Kill Tracking")]
        public bool trackKills = true;
        public int killsRequired = 10;
        public string killTaskName = "Kill 10 Enemies";
        [HideInInspector] public int currentKills = 0;
        [HideInInspector] public bool killTaskComplete = false;

        [Header("Hit Tracking")]
        public bool trackHits = false;
        public int hitsRequired = 10;
        public string hitTaskName = "Land 10 Attacks";
        [HideInInspector] public int currentHits = 0;
        [HideInInspector] public bool hitTaskComplete = false;

        [Header("Blood Tracking")]
        public bool trackBloodGain = true;
        public float bloodRequired = 10f;
        public string bloodTaskName = "Gain 10 Blood";
        [HideInInspector] public float startingBlood = 0f;
        [HideInInspector] public bool bloodTaskComplete = false;
    }

    [Header("References")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private BloodSystem bloodSystem;

    [Header("Multi-Objective Tracking")]
    [SerializeField] private List<ObjectiveTracking> objectives = new List<ObjectiveTracking>();

    [Header("Events")]
    public UnityEvent<int> onKillCountChanged;
    public UnityEvent<int> onHitCountChanged;
    public UnityEvent<float> onBloodChanged;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private ObjectiveTracking currentTracking;
    private Dictionary<Health, System.Action> damageCallbacks = new Dictionary<Health, System.Action>();
    private Dictionary<Health, System.Action> deathCallbacks = new Dictionary<Health, System.Action>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning($"Multiple ObjectiveManagers detected! Only one should be active at a time. Disabling {gameObject.name}");
            enabled = false;
        }
    }

    private void Start()
    {
        // Auto-find references
        if (objectiveController == null)
            objectiveController = FindFirstObjectByType<ObjectiveController>();

        if (bloodSystem == null)
            bloodSystem = FindFirstObjectByType<BloodSystem>();

        // Subscribe to objective changes
        if (objectiveController != null)
        {
            objectiveController.onObjectiveChanged.AddListener(OnObjectiveChanged);
        }

        // Initialize first objective
        OnObjectiveChanged();

        if (showDebugLogs)
        {
            Debug.Log($"=== ObjectiveManager Start ===");
            Debug.Log($"Configured {objectives.Count} objective trackings");
        }
    }

    private void OnObjectiveChanged()
    {
        if (objectiveController == null) return;

        var currentObj = objectiveController.GetCurrentObjective();
        if (currentObj == null) return;

        // Get current objective index
        var allObjectives = objectiveController.GetAllObjectives();
        int currentIndex = allObjectives.IndexOf(currentObj);

        if (showDebugLogs)
        {
            Debug.Log($"=== Objective Changed to Index {currentIndex} ===");
        }

        // Find matching tracking configuration
        ObjectiveTracking newTracking = null;
        foreach (var tracking in objectives)
        {
            if (tracking.objectiveIndex == currentIndex)
            {
                newTracking = tracking;
                break;
            }
        }

        // If we found a matching tracking configuration
        if (newTracking != null)
        {
            // Deactivate old tracking
            if (currentTracking != null)
            {
                UnregisterAllEnemies();
            }

            // Activate new tracking
            currentTracking = newTracking;

            // Record starting blood for this objective
            if (bloodSystem != null)
            {
                currentTracking.startingBlood = bloodSystem.currentBlood;
            }

            // Register all existing enemies
            RegisterAllExistingEnemies();

            if (showDebugLogs)
            {
                Debug.Log($"✓ Activated tracking for: {currentTracking.objectiveName}");
                Debug.Log($"  - Kills: {currentTracking.trackKills} ({currentTracking.killsRequired} required)");
                Debug.Log($"  - Hits: {currentTracking.trackHits} ({currentTracking.hitsRequired} required)");
                Debug.Log($"  - Blood: {currentTracking.trackBloodGain} ({currentTracking.bloodRequired} required)");
            }
        }
        else
        {
            // This objective doesn't need tracking (e.g., mask collection only)
            if (currentTracking != null)
            {
                UnregisterAllEnemies();
                currentTracking = null;
            }

            if (showDebugLogs)
            {
                Debug.Log($"No tracking needed for objective index {currentIndex}");
            }
        }
    }

    private void Update()
    {
        if (currentTracking == null) return;

        // Check blood threshold
        if (currentTracking.trackBloodGain && !currentTracking.bloodTaskComplete && bloodSystem != null)
        {
            float bloodGained = bloodSystem.currentBlood - currentTracking.startingBlood;

            if (bloodGained >= currentTracking.bloodRequired)
            {
                CompleteBloodTask();
            }

            onBloodChanged?.Invoke(bloodGained);
        }
    }

    private void RegisterAllExistingEnemies()
    {
        if (currentTracking == null) return;

        Health[] allEnemies = FindObjectsByType<Health>(FindObjectsSortMode.None);
        int registeredCount = 0;

        foreach (Health enemy in allEnemies)
        {
            if (enemy != null && enemy.CompareTag("Enemy"))
            {
                RegisterEnemy(enemy);
                registeredCount++;
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"Registered {registeredCount} existing enemies for {currentTracking.objectiveName}");
        }
    }

    public void RegisterEnemy(Health enemy)
    {
        if (enemy == null || currentTracking == null) return;

        // Don't register twice
        if (damageCallbacks.ContainsKey(enemy) || deathCallbacks.ContainsKey(enemy))
            return;

        // Register hit tracking
        if (currentTracking.trackHits)
        {
            System.Action damageCallback = () => OnEnemyHit(enemy);
            damageCallbacks[enemy] = damageCallback;
            enemy.onDamage.AddListener(new UnityEngine.Events.UnityAction(damageCallback));
        }

        // Register kill tracking
        if (currentTracking.trackKills)
        {
            System.Action deathCallback = () => OnEnemyKilled(enemy);
            deathCallbacks[enemy] = deathCallback;
            enemy.onDeath.AddListener(new UnityEngine.Events.UnityAction(deathCallback));
        }

        if (showDebugLogs)
        {
            Debug.Log($"Registered enemy: {enemy.gameObject.name}");
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
    }

    private void OnEnemyHit(Health enemy)
    {
        if (currentTracking == null || currentTracking.hitTaskComplete) return;

        currentTracking.currentHits++;
        onHitCountChanged?.Invoke(currentTracking.currentHits);

        if (showDebugLogs)
        {
            Debug.Log($"Hit count: {currentTracking.currentHits}/{currentTracking.hitsRequired}");
        }

        if (currentTracking.currentHits >= currentTracking.hitsRequired)
        {
            CompleteHitTask();
        }
    }

    private void OnEnemyKilled(Health enemy)
    {
        if (currentTracking == null || currentTracking.killTaskComplete) return;

        currentTracking.currentKills++;
        onKillCountChanged?.Invoke(currentTracking.currentKills);

        if (showDebugLogs)
        {
            Debug.Log($"Kill count: {currentTracking.currentKills}/{currentTracking.killsRequired}");
        }

        if (currentTracking.currentKills >= currentTracking.killsRequired)
        {
            CompleteKillTask();
        }
    }

    private void CompleteHitTask()
    {
        if (currentTracking.hitTaskComplete) return;
        currentTracking.hitTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Hit objective complete: {currentTracking.hitTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.hitTaskName);
        }
    }

    private void CompleteKillTask()
    {
        if (currentTracking.killTaskComplete) return;
        currentTracking.killTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Kill objective complete: {currentTracking.killTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.killTaskName);
        }
    }

    private void CompleteBloodTask()
    {
        if (currentTracking.bloodTaskComplete) return;
        currentTracking.bloodTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Blood objective complete: {currentTracking.bloodTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.bloodTaskName);
        }
    }

    // Public getters for UI
    public int GetKillCount() => currentTracking?.currentKills ?? 0;
    public int GetKillsRequired() => currentTracking?.killsRequired ?? 0;
    public float GetKillProgress() => currentTracking != null ? (float)currentTracking.currentKills / currentTracking.killsRequired : 0f;

    public int GetHitCount() => currentTracking?.currentHits ?? 0;
    public int GetHitsRequired() => currentTracking?.hitsRequired ?? 0;
    public float GetHitProgress() => currentTracking != null ? (float)currentTracking.currentHits / currentTracking.hitsRequired : 0f;

    public float GetBloodGained() => currentTracking != null && bloodSystem != null ? bloodSystem.currentBlood - currentTracking.startingBlood : 0f;
    public float GetBloodRequired() => currentTracking?.bloodRequired ?? 0f;
    public float GetBloodProgress() => currentTracking != null ? GetBloodGained() / currentTracking.bloodRequired : 0f;

    public void ResetAllTracking()
    {
        foreach (var tracking in objectives)
        {
            tracking.currentKills = 0;
            tracking.currentHits = 0;
            tracking.killTaskComplete = false;
            tracking.hitTaskComplete = false;
            tracking.bloodTaskComplete = false;
        }

        if (bloodSystem != null && currentTracking != null)
        {
            currentTracking.startingBlood = bloodSystem.currentBlood;
        }
    }

    private void OnDestroy()
    {
        if (objectiveController != null)
        {
            objectiveController.onObjectiveChanged.RemoveListener(OnObjectiveChanged);
        }

        UnregisterAllEnemies();

        if (Instance == this)
        {
            Instance = null;
        }
    }
}