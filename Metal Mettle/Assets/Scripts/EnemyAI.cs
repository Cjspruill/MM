using UnityEngine;

/// <summary>
/// TRULY FIXED - Handles state transitions properly and re-engages
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Component References")]
    private EnemyMovement movementSystem;
    private EnemyComboSystem combatSystem;
    private Health health;

    [Header("State Management")]
    [Tooltip("How long to stay locked in attack state after player leaves (0 = instant transition)")]
    public float attackStateLockTime = 0.5f;  // Very short lock

    // State tracking
    private enum EnemyState { Wandering, Chasing, Attacking }
    [SerializeField]private EnemyState currentState = EnemyState.Wandering;
    private float stateEnterTime = 0f;

    [Header("Debug")]
    public bool showDebug = true;

    void Start()
    {
        movementSystem = GetComponent<EnemyMovement>();
        combatSystem = GetComponent<EnemyComboSystem>();
        health = GetComponent<Health>();

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
            Debug.Log($"{gameObject.name} initialized with TRULY FIXED AI");
        }
    }

    void Update()
    {
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

    void DetermineAndHandleState(float distanceToPlayer, bool isChasing)
    {
        EnemyState previousState = currentState;
        float attackRange = combatSystem.GetAttackRange() * 1.3f; // 30% buffer

        // Debug current conditions every second
        if (showDebug && Time.frameCount % 60 == 0)
        {
            Debug.Log($"🔍 {gameObject.name} | isChasing: {isChasing} | dist: {distanceToPlayer:F1} | attackRange: {attackRange:F1}");
        }

        // STATE DETERMINATION - Clear priority system
        EnemyState newState;

        if (!isChasing)
        {
            // Not chasing at all = Wander
            newState = EnemyState.Wandering;
        }
        else if (distanceToPlayer <= attackRange)
        {
            // In attack range = Attack
            newState = EnemyState.Attacking;
        }
        else
        {
            // Chasing but out of range = Chase
            newState = EnemyState.Chasing;
        }

        // IMPORTANT: Don't transition out of Attacking state instantly
        // This prevents jitter when player moves slightly
        if (currentState == EnemyState.Attacking && newState != EnemyState.Attacking)
        {
            float timeInState = Time.time - stateEnterTime;

            // Only transition if we've been in attack state for minimum time AND not actively attacking
            bool isActuallyAttacking = combatSystem.IsAttacking() || combatSystem.IsInCombat();

            if (isActuallyAttacking)
            {
                // Still attacking, stay in state
                newState = EnemyState.Attacking;
            }
            else if (timeInState < attackStateLockTime)
            {
                // Just finished attacking, brief lock before transitioning
                newState = EnemyState.Attacking;
            }
            // else: enough time passed and not attacking, allow transition
        }

        // Apply state change
        if (newState != currentState)
        {
            if (showDebug)
            {
                Debug.Log($"🔄 {gameObject.name}: {currentState} → {newState} (dist: {distanceToPlayer:F1}, range: {attackRange:F1}, chasing: {isChasing})");
            }

            currentState = newState;
            stateEnterTime = Time.time;
        }

        // Handle current state
        switch (currentState)
        {
            case EnemyState.Wandering:
                HandleWandering();
                break;

            case EnemyState.Chasing:
                HandleChasing();
                break;

            case EnemyState.Attacking:
                HandleAttacking(distanceToPlayer);
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

    void HandleAttacking(float distanceToPlayer)
    {
        // EMERGENCY UNSTUCK: If in attack state too long without attacking, force transition
        float timeInState = Time.time - stateEnterTime;
        bool isActuallyAttacking = combatSystem.IsAttacking() || combatSystem.IsInCombat();

        if (!isActuallyAttacking && timeInState > 3f)
        {
            // Been in attack state for 3+ seconds without attacking = STUCK
            Debug.LogWarning($"🚨 {gameObject.name} STUCK in attack state for {timeInState:F1}s - FORCING TRANSITION!");

            // Force transition to chase
            currentState = EnemyState.Chasing;
            stateEnterTime = Time.time;

            if (movementSystem != null)
            {
                movementSystem.HandleChaseMovement();
            }
            return;
        }

        // Movement during attack state
        if (movementSystem != null)
        {
            if (isActuallyAttacking)
            {
                // Actually attacking - stop moving
                movementSystem.HandleCombatMovement();
            }
            else
            {
                // Waiting to attack - can move closer
                float attackRange = combatSystem.GetAttackRange() * 1.3f;

                if (distanceToPlayer > attackRange * 0.9f)
                {
                    // Too far, move closer
                    movementSystem.HandleChaseMovement();
                }
                else
                {
                    // Close enough, face player
                    movementSystem.HandleCombatMovement();
                }
            }
        }

        // Attack decision - CHECK EVERY FRAME
        if (combatSystem != null && !isActuallyAttacking)
        {
            bool canAttack = combatSystem.CanAttack();
            bool wantsToAttack = combatSystem.WantsToAttack();
            float attackRange = combatSystem.GetAttackRange() * 1.3f;
            bool inRange = distanceToPlayer <= attackRange;

            // Debug output every second
            if (showDebug && Time.frameCount % 60 == 0)
            {
                float cooldown = combatSystem.GetNextAttackTime() - Time.time;
                Debug.Log($"📊 {gameObject.name} | State: {currentState} | TimeInState: {timeInState:F1}s | Can: {canAttack} | Wants: {wantsToAttack} | InRange: {inRange} | Dist: {distanceToPlayer:F1}/{attackRange:F1} | Cooldown: {cooldown:F1}s");
            }

            // ATTACK if ready
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

    // Public interface
    public void ActivateHitbox(string collider = null)
    {
        if (combatSystem != null)
        {
            combatSystem.ActivateHitbox();
        }
    }

    // Public interface
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

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (combatSystem == null || !Application.isPlaying) return;

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, combatSystem.GetAttackRange());

        // Buffered attack range (what we actually use)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, combatSystem.GetAttackRange() * 1.3f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3f,
            $"State: {currentState}\nCooldown: {Mathf.Max(0, combatSystem.GetNextAttackTime() - Time.time):F1}s");
#endif
    }
}