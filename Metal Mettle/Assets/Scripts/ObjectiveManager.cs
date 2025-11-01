using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Enhanced ObjectiveManager with light/heavy attack tracking and execution kills
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

        [Header("Execution Kill Tracking")]
        public bool trackExecutions = false;
        public int executionsRequired = 5;
        public string executionTaskName = "Execute 5 Enemies";
        [HideInInspector] public int currentExecutions = 0;
        [HideInInspector] public bool executionTaskComplete = false;

        [Header("Hit Tracking (All Attacks)")]
        public bool trackHits = false;
        public int hitsRequired = 10;
        public string hitTaskName = "Land 10 Attacks";
        [HideInInspector] public int currentHits = 0;
        [HideInInspector] public bool hitTaskComplete = false;

        [Header("Light Attack Tracking")]
        public bool trackLightAttacks = false;
        public int lightAttacksRequired = 15;
        public string lightAttackTaskName = "Land 15 Light Attacks";
        [HideInInspector] public int currentLightAttacks = 0;
        [HideInInspector] public bool lightAttackTaskComplete = false;

        [Header("Heavy Attack Tracking")]
        public bool trackHeavyAttacks = false;
        public int heavyAttacksRequired = 10;
        public string heavyAttackTaskName = "Land 10 Heavy Attacks";
        [HideInInspector] public int currentHeavyAttacks = 0;
        [HideInInspector] public bool heavyAttackTaskComplete = false;

        [Header("Blood Tracking")]
        public bool trackBloodGain = true;
        public float bloodRequired = 10f;
        public string bloodTaskName = "Gain 10 Blood";

        // Track total blood gained, not net blood
        [HideInInspector] public float totalBloodGained = 0f;
        [HideInInspector] public float lastBloodValue = 0f;
        [HideInInspector] public bool bloodTaskComplete = false;
    }

    [Header("References")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private BloodSystem bloodSystem;

    [Header("Multi-Objective Tracking")]
    [SerializeField] private List<ObjectiveTracking> objectives = new List<ObjectiveTracking>();

    [Header("Events")]
    public UnityEvent<int> onKillCountChanged;
    public UnityEvent<int> onExecutionCountChanged;
    public UnityEvent<int> onHitCountChanged;
    public UnityEvent<int> onLightAttackCountChanged;
    public UnityEvent<int> onHeavyAttackCountChanged;
    public UnityEvent<float> onBloodChanged;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private ObjectiveTracking currentTracking;
    private Dictionary<Health, System.Action> damageCallbacks = new Dictionary<Health, System.Action>();
    private Dictionary<Health, System.Action<bool>> lightAttackCallbacks = new Dictionary<Health, System.Action<bool>>();
    private Dictionary<Health, System.Action<bool>> heavyAttackCallbacks = new Dictionary<Health, System.Action<bool>>();
    private Dictionary<Health, System.Action> deathCallbacks = new Dictionary<Health, System.Action>();
    private Dictionary<Health, System.Action> executionCallbacks = new Dictionary<Health, System.Action>();

    // Track if we're subscribed to blood events
    private bool subscribedToBloodEvents = false;

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
                UnsubscribeFromBloodEvents();
            }

            // Activate new tracking
            currentTracking = newTracking;

            // Initialize blood tracking properly
            if (bloodSystem != null && currentTracking.trackBloodGain)
            {
                currentTracking.lastBloodValue = bloodSystem.currentBlood;
                currentTracking.totalBloodGained = 0f;
                SubscribeToBloodEvents();
            }

            // Register all existing enemies
            RegisterAllExistingEnemies();

            if (showDebugLogs)
            {
                Debug.Log($"✓ Activated tracking for: {currentTracking.objectiveName}");
                Debug.Log($"  - Kills: {currentTracking.trackKills} ({currentTracking.killsRequired} required)");
                Debug.Log($"  - Executions: {currentTracking.trackExecutions} ({currentTracking.executionsRequired} required)");
                Debug.Log($"  - Hits: {currentTracking.trackHits} ({currentTracking.hitsRequired} required)");
                Debug.Log($"  - Light Attacks: {currentTracking.trackLightAttacks} ({currentTracking.lightAttacksRequired} required)");
                Debug.Log($"  - Heavy Attacks: {currentTracking.trackHeavyAttacks} ({currentTracking.heavyAttacksRequired} required)");
                Debug.Log($"  - Blood: {currentTracking.trackBloodGain} ({currentTracking.bloodRequired} required)");
                Debug.Log($"  - Starting blood value: {currentTracking.lastBloodValue}");
            }
        }
        else
        {
            // This objective doesn't need tracking (e.g., mask collection only)
            if (currentTracking != null)
            {
                UnregisterAllEnemies();
                UnsubscribeFromBloodEvents();
                currentTracking = null;
            }

            if (showDebugLogs)
            {
                Debug.Log($"No tracking needed for objective index {currentIndex}");
            }
        }
    }

    private void SubscribeToBloodEvents()
    {
        if (bloodSystem == null || subscribedToBloodEvents) return;

        subscribedToBloodEvents = true;

        if (showDebugLogs)
        {
            Debug.Log("Subscribed to blood tracking");
        }
    }

    private void UnsubscribeFromBloodEvents()
    {
        if (!subscribedToBloodEvents) return;

        subscribedToBloodEvents = false;
    }

    private void Update()
    {
        if (currentTracking == null || bloodSystem == null) return;

        // Track only INCREASES in blood (gained), not net change
        if (currentTracking.trackBloodGain && !currentTracking.bloodTaskComplete)
        {
            float currentBlood = bloodSystem.currentBlood;
            float bloodDelta = currentBlood - currentTracking.lastBloodValue;

            // Only count positive changes (blood gained)
            if (bloodDelta > 0.01f) // Small threshold to avoid floating point errors
            {
                currentTracking.totalBloodGained += bloodDelta;

                if (showDebugLogs)
                {
                    Debug.Log($"Blood gained: +{bloodDelta:F2} (total: {currentTracking.totalBloodGained:F2}/{currentTracking.bloodRequired:F2})");
                }

                onBloodChanged?.Invoke(currentTracking.totalBloodGained);

                if (currentTracking.totalBloodGained >= currentTracking.bloodRequired)
                {
                    CompleteBloodTask();
                }
            }

            // Update last value regardless of delta
            currentTracking.lastBloodValue = currentBlood;
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

        // Register general hit tracking (any damage)
        if (currentTracking.trackHits)
        {
            System.Action damageCallback = () => OnEnemyHit(enemy);
            damageCallbacks[enemy] = damageCallback;
            enemy.onDamage.AddListener(new UnityEngine.Events.UnityAction(damageCallback));
        }

        // Register light attack tracking
        if (currentTracking.trackLightAttacks)
        {
            System.Action<bool> lightCallback = (isLight) => OnLightAttackHit(enemy, isLight);
            lightAttackCallbacks[enemy] = lightCallback;
            enemy.onLightAttack.AddListener(new UnityEngine.Events.UnityAction<bool>(lightCallback));
        }

        // Register heavy attack tracking
        if (currentTracking.trackHeavyAttacks)
        {
            System.Action<bool> heavyCallback = (isHeavy) => OnHeavyAttackHit(enemy, isHeavy);
            heavyAttackCallbacks[enemy] = heavyCallback;
            enemy.onHeavyAttack.AddListener(new UnityEngine.Events.UnityAction<bool>(heavyCallback));
        }

        // Register kill tracking
        if (currentTracking.trackKills)
        {
            System.Action deathCallback = () => OnEnemyKilled(enemy);
            deathCallbacks[enemy] = deathCallback;
            enemy.onDeath.AddListener(new UnityEngine.Events.UnityAction(deathCallback));
        }

        // Register execution tracking
        if (currentTracking.trackExecutions)
        {
            System.Action executionCallback = () => OnEnemyExecuted(enemy);
            executionCallbacks[enemy] = executionCallback;
            enemy.onExecution.AddListener(new UnityEngine.Events.UnityAction(executionCallback));
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

        // Unsubscribe light attack callbacks
        foreach (var kvp in lightAttackCallbacks)
        {
            if (kvp.Key != null)
            {
                kvp.Key.onLightAttack.RemoveListener(new UnityEngine.Events.UnityAction<bool>(kvp.Value));
            }
        }
        lightAttackCallbacks.Clear();

        // Unsubscribe heavy attack callbacks
        foreach (var kvp in heavyAttackCallbacks)
        {
            if (kvp.Key != null)
            {
                kvp.Key.onHeavyAttack.RemoveListener(new UnityEngine.Events.UnityAction<bool>(kvp.Value));
            }
        }
        heavyAttackCallbacks.Clear();

        // Unsubscribe death callbacks
        foreach (var kvp in deathCallbacks)
        {
            if (kvp.Key != null)
            {
                kvp.Key.onDeath.RemoveListener(new UnityEngine.Events.UnityAction(kvp.Value));
            }
        }
        deathCallbacks.Clear();

        // Unsubscribe execution callbacks
        foreach (var kvp in executionCallbacks)
        {
            if (kvp.Key != null)
            {
                kvp.Key.onExecution.RemoveListener(new UnityEngine.Events.UnityAction(kvp.Value));
            }
        }
        executionCallbacks.Clear();
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

    private void OnLightAttackHit(Health enemy, bool isLight)
    {
        if (currentTracking == null || currentTracking.lightAttackTaskComplete || !isLight) return;

        currentTracking.currentLightAttacks++;
        onLightAttackCountChanged?.Invoke(currentTracking.currentLightAttacks);

        if (showDebugLogs)
        {
            Debug.Log($"Light attack count: {currentTracking.currentLightAttacks}/{currentTracking.lightAttacksRequired}");
        }

        if (currentTracking.currentLightAttacks >= currentTracking.lightAttacksRequired)
        {
            CompleteLightAttackTask();
        }
    }

    private void OnHeavyAttackHit(Health enemy, bool isHeavy)
    {
        if (currentTracking == null || currentTracking.heavyAttackTaskComplete || !isHeavy) return;

        currentTracking.currentHeavyAttacks++;
        onHeavyAttackCountChanged?.Invoke(currentTracking.currentHeavyAttacks);

        if (showDebugLogs)
        {
            Debug.Log($"Heavy attack count: {currentTracking.currentHeavyAttacks}/{currentTracking.heavyAttacksRequired}");
        }

        if (currentTracking.currentHeavyAttacks >= currentTracking.heavyAttacksRequired)
        {
            CompleteHeavyAttackTask();
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

    private void OnEnemyExecuted(Health enemy)
    {
        if (currentTracking == null || currentTracking.executionTaskComplete) return;

        currentTracking.currentExecutions++;
        onExecutionCountChanged?.Invoke(currentTracking.currentExecutions);

        if (showDebugLogs)
        {
            Debug.Log($"Execution count: {currentTracking.currentExecutions}/{currentTracking.executionsRequired}");
        }

        if (currentTracking.currentExecutions >= currentTracking.executionsRequired)
        {
            CompleteExecutionTask();
        }
    }

    private void CompleteHitTask()
    {
        if (currentTracking.hitTaskComplete) return;

        currentTracking.hitTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Hit task complete: {currentTracking.hitTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.hitTaskName);
        }
    }

    private void CompleteLightAttackTask()
    {
        if (currentTracking.lightAttackTaskComplete) return;

        currentTracking.lightAttackTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Light attack task complete: {currentTracking.lightAttackTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.lightAttackTaskName);
        }
    }

    private void CompleteHeavyAttackTask()
    {
        if (currentTracking.heavyAttackTaskComplete) return;

        currentTracking.heavyAttackTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Heavy attack task complete: {currentTracking.heavyAttackTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.heavyAttackTaskName);
        }
    }

    private void CompleteKillTask()
    {
        if (currentTracking.killTaskComplete) return;

        currentTracking.killTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Kill task complete: {currentTracking.killTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.killTaskName);
        }
    }

    private void CompleteExecutionTask()
    {
        if (currentTracking.executionTaskComplete) return;

        currentTracking.executionTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Execution task complete: {currentTracking.executionTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.executionTaskName);
        }
    }

    private void CompleteBloodTask()
    {
        if (currentTracking.bloodTaskComplete) return;

        currentTracking.bloodTaskComplete = true;

        if (showDebugLogs)
        {
            Debug.Log($"✓ Blood task complete: {currentTracking.bloodTaskName}");
        }

        if (objectiveController != null)
        {
            objectiveController.CompleteTask(currentTracking.bloodTaskName);
        }
    }

    // Public getters for UI
    public int GetCurrentKills() => currentTracking?.currentKills ?? 0;
    public int GetCurrentExecutions() => currentTracking?.currentExecutions ?? 0;
    public int GetCurrentHits() => currentTracking?.currentHits ?? 0;
    public int GetCurrentLightAttacks() => currentTracking?.currentLightAttacks ?? 0;
    public int GetCurrentHeavyAttacks() => currentTracking?.currentHeavyAttacks ?? 0;
    public float GetCurrentBloodGained() => currentTracking?.totalBloodGained ?? 0f;

    public int GetKillsRequired() => currentTracking?.killsRequired ?? 0;
    public int GetExecutionsRequired() => currentTracking?.executionsRequired ?? 0;
    public int GetHitsRequired() => currentTracking?.hitsRequired ?? 0;
    public int GetLightAttacksRequired() => currentTracking?.lightAttacksRequired ?? 0;
    public int GetHeavyAttacksRequired() => currentTracking?.heavyAttacksRequired ?? 0;
    public float GetBloodRequired() => currentTracking?.bloodRequired ?? 0f;
}