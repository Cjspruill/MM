using UnityEngine;

/// <summary>
/// Enhanced EnemyAI with cutscene and tutorial pause support
/// Stops all movement and attacking during cutscenes and tutorials
/// </summary>
public class EnemyAI : MonoBehaviour, ICutsceneControllable
{
    [Header("Component References")]
    private EnemyMovement movementSystem;
    private EnemyComboSystem combatSystem;
    private Health health;
    private TutorialManager tutorialManager;

    [Header("State Management")]
    [Tooltip("How long to stay locked in attack state after player leaves (0 = instant transition)")]
    public float attackStateLockTime = 0.5f;

    // State tracking
    private enum EnemyState { Wandering, Chasing, Attacking }
    [SerializeField] private EnemyState currentState = EnemyState.Wandering;
    private float stateEnterTime = 0f;

    // Pause state tracking
    private bool isInCutscene = false;
    private bool wasPausedByTutorial = false;

    [Header("Debug")]
    public bool showDebug = true;

    void Start()
    {
        movementSystem = GetComponent<EnemyMovement>();
        combatSystem = GetComponent<EnemyComboSystem>();
        health = GetComponent<Health>();
        tutorialManager = FindFirstObjectByType<TutorialManager>();

        if (movementSystem == null)
        {
            Debug.LogError($"{gameObject.name}: Missing EnemyMovement!");
        }

        if (combatSystem == null)
        {
            Debug.LogError($"{gameObject.name}: Missing EnemyComboSystem!");
        }

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} initialized with cutscene/tutorial pause support");
        }
    }

    void Update()
    {
        // CRITICAL: Check if paused by cutscene or tutorial
        if (isInCutscene || IsPausedByTutorial())
        {
            // Stop all movement
            if (movementSystem != null)
            {
                movementSystem.StopMovement();
            }

            // Cancel any ongoing attacks
            if (combatSystem != null && combatSystem.IsAttacking())
            {
                combatSystem.CancelAttack();
            }

            return; // Don't process any AI logic while paused
        }

        // Normal AI logic resumes here
        if (combatSystem != null && combatSystem.IsStunned()) return;

        if (health != null && health.IsDead())
        {
            if (movementSystem != null)
            {
                movementSystem.StopMovement();
            }
            return;
        }

        if (movementSystem == null || combatSystem == null) return;

        movementSystem.UpdateMovement(combatSystem.IsInCombat(), combatSystem.IsStunned());

        float distanceToPlayer = movementSystem.GetDistanceToPlayer();
        bool isChasing = movementSystem.IsChasing();

        DetermineAndHandleState(distanceToPlayer, isChasing);
    }

    #region ICutsceneControllable Implementation

    public void OnCutsceneStart()
    {
        Debug.Log($"{gameObject.name}: Cutscene started - Pausing enemy AI");
        isInCutscene = true;

        // Stop all movement
        if (movementSystem != null)
        {
            movementSystem.StopMovement();
        }

        // Cancel any ongoing attacks
        if (combatSystem != null && combatSystem.IsAttacking())
        {
            combatSystem.CancelAttack();
        }
    }

    public void OnCutsceneEnd()
    {
        Debug.Log($"{gameObject.name}: Cutscene ended - Resuming enemy AI");
        isInCutscene = false;
    }

    #endregion

    private bool IsPausedByTutorial()
    {
        if (tutorialManager == null)
        {
            tutorialManager = FindFirstObjectByType<TutorialManager>();
        }

        bool isPaused = tutorialManager != null && tutorialManager.IsShowingTutorial;

        // Track state changes for debug logging
        if (isPaused && !wasPausedByTutorial)
        {
            wasPausedByTutorial = true;
            if (showDebug)
            {
                Debug.Log($"{gameObject.name}: Paused by tutorial");
            }
        }
        else if (!isPaused && wasPausedByTutorial)
        {
            wasPausedByTutorial = false;
            if (showDebug)
            {
                Debug.Log($"{gameObject.name}: Resumed after tutorial");
            }
        }

        return isPaused;
    }

    void DetermineAndHandleState(float distanceToPlayer, bool isChasing)
    {
        EnemyState previousState = currentState;
        float attackRange = combatSystem.GetAttackRange() * 1.3f;

        if (showDebug && Time.frameCount % 60 == 0)
        {
            Debug.Log($"🔍 {gameObject.name} | isChasing: {isChasing} | dist: {distanceToPlayer:F1} | attackRange: {attackRange:F1}");
        }

        EnemyState newState;

        if (!isChasing)
        {
            newState = EnemyState.Wandering;
        }
        else if (distanceToPlayer <= attackRange)
        {
            newState = EnemyState.Attacking;
        }
        else
        {
            newState = EnemyState.Chasing;
        }

        if (newState != previousState)
        {
            currentState = newState;
            stateEnterTime = Time.time;

            if (showDebug)
            {
                Debug.Log($"🔄 {gameObject.name} state: {previousState} → {currentState}");
            }
        }

        float timeInState = Time.time - stateEnterTime;

        switch (currentState)
        {
            case EnemyState.Wandering:
                HandleWandering();
                break;

            case EnemyState.Chasing:
                HandleChasing();
                break;

            case EnemyState.Attacking:
                HandleAttacking(distanceToPlayer, timeInState);
                break;
        }
    }

    void HandleWandering()
    {
        if (movementSystem != null)
        {
            movementSystem.HandleWandering();
        }
    }

    void HandleChasing()
    {
        if (movementSystem != null)
        {
            movementSystem.HandleChaseMovement();
        }
    }

    void HandleAttacking(float distanceToPlayer, float timeInState)
    {
        bool isActuallyAttacking = combatSystem != null && combatSystem.IsAttacking();

        if (!isActuallyAttacking && timeInState > attackStateLockTime)
        {
            float attackRange = combatSystem.GetAttackRange() * 1.3f;
            if (distanceToPlayer > attackRange * 1.5f)
            {
                if (showDebug)
                {
                    Debug.Log($"❌ {gameObject.name} TOO FAR from player ({distanceToPlayer:F1} > {attackRange * 1.5f:F1}) - returning to chase");
                }

                currentState = EnemyState.Chasing;
                stateEnterTime = Time.time;

                if (movementSystem != null)
                {
                    movementSystem.HandleChaseMovement();
                }
                return;
            }
        }

        if (movementSystem != null)
        {
            if (isActuallyAttacking)
            {
                movementSystem.HandleCombatMovement();
            }
            else
            {
                float attackRange = combatSystem.GetAttackRange() * 1.3f;

                if (distanceToPlayer > attackRange * 0.9f)
                {
                    movementSystem.HandleChaseMovement();
                }
                else
                {
                    movementSystem.HandleCombatMovement();
                }
            }
        }

        if (combatSystem != null && !isActuallyAttacking)
        {
            bool canAttack = combatSystem.CanAttack();
            bool wantsToAttack = combatSystem.WantsToAttack();
            float attackRange = combatSystem.GetAttackRange() * 1.3f;
            bool inRange = distanceToPlayer <= attackRange;

            if (showDebug && Time.frameCount % 60 == 0)
            {
                float cooldown = combatSystem.GetNextAttackTime() - Time.time;
                Debug.Log($"📊 {gameObject.name} | State: {currentState} | TimeInState: {timeInState:F1}s | Can: {canAttack} | Wants: {wantsToAttack} | InRange: {inRange} | Dist: {distanceToPlayer:F1}/{attackRange:F1} | Cooldown: {cooldown:F1}s");
            }

            if (canAttack)
            {
                if (wantsToAttack || inRange)
                {
                    if (showDebug)
                    {
                        Debug.Log($"⚔️⚔️⚔️ {gameObject.name} ATTACKING! ⚔️⚔️⚔️");
                    }
                    combatSystem.StartCombo();
                }
                else if (showDebug && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"❓ {gameObject.name} CAN attack but won't (wants:{wantsToAttack} inRange:{inRange})");
                }
            }
        }
    }

    public void ActivateHitbox(string collider = null)
    {
        if (combatSystem != null)
        {
            combatSystem.ActivateHitbox();
        }
    }

    public void DeactivateHitbox(string collider = null)
    {
        if (combatSystem != null)
        {
            combatSystem.DeactivateHitbox();
        }
    }

    public bool IsAttacking() => combatSystem != null && combatSystem.IsAttacking();
    public bool IsChasing() => movementSystem != null && movementSystem.IsChasing();
    public bool IsWandering() => currentState == EnemyState.Wandering;

    void OnDrawGizmosSelected()
    {
        if (combatSystem == null || !Application.isPlaying) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, combatSystem.GetAttackRange());

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, combatSystem.GetAttackRange() * 1.3f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3f,
            $"State: {currentState}\nCooldown: {Mathf.Max(0, combatSystem.GetNextAttackTime() - Time.time):F1}s\nPaused: {(isInCutscene || IsPausedByTutorial())}");
#endif
    }
}