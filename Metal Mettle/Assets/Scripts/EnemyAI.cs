using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    private NavMeshAgent agent;
    private Health health;
    private Animator animator;

    [Header("Movement Settings")]
    public float chaseSpeed = 3.5f;
    public float wanderSpeed = 2f;
    public float stoppingDistance = 2f;

    [Header("Vision Detection")]
    public float detectionRange = 15f;
    public float viewAngle = 90f;
    public LayerMask obstacleMask;
    public LayerMask playerMask;
    public bool alwaysChase = false;
    public float memoryDuration = 3f;
    public float visionCheckInterval = 0.2f;

    [Header("Wandering Settings")]
    public float wanderRadius = 10f;
    public float minWanderWaitTime = 2f;
    public float maxWanderWaitTime = 5f;
    public float wanderPointReachThreshold = 1f;

    [Header("Alert System")]
    public float alertRadius = 5f;
    public LayerMask enemyMask;

    [Header("Combat Settings")]
    public BoxCollider attackHitbox;
    public MeshRenderer debugRenderer;
    public float attackRange = 2.5f;
    public float attackDuration = 0.3f;
    public int minComboAttacks = 1;
    public int maxComboAttacks = 3;
    public float timeBetweenAttacks = 0.8f;
    public float comboCooldown = 2f;
    public float attackWindupTime = 0.5f;

    [Header("Animation Settings")]
    public string[] attackTriggers = { "Attack1", "Attack2", "Attack3" };
    public bool useAnimationEvents = true;

    [Header("Hitstun")]
    public float baseHitstunDuration = 0.4f;
    public float stunCooldown = 1.0f;
    public bool canBeStunnedDuringAttack = false;

    [Header("Avoidance Settings")]
    public float avoidanceRadius = 0.5f;
    public int avoidancePriority = 50;
    public bool disableAvoidanceInCombat = true;

    [Header("Debug")]
    public bool showDebug = true;

    // State tracking
    private enum EnemyState { Wandering, Chasing, Attacking }
    private EnemyState currentState = EnemyState.Wandering;
    private bool isChasing = false;
    private bool isAttacking = false;
    private bool inCombat = false;
    private int currentComboStep = 0;
    private int targetComboLength = 0;
    private float lastAttackTime = 0f;
    private float nextAttackTime = 0f;
    private EnemyAttackCollider enemyAttackCollider;
    private bool isStunned = false;

    // Vision tracking
    private bool canSeePlayer = false;
    private float lastSeenTime = 0f;
    private float nextVisionCheck = 0f;
    private float lastStunTime = -999f;

    // Wandering state
    private Vector3 wanderTarget;
    private float nextWanderTime = 0f;
    private Vector3 lastKnownPlayerPosition;
    private bool hasLastKnownPosition = false;

    // Track if we're in attack range
    private bool wasInAttackRange = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError($"{gameObject.name}: No Animator component found!");
        }

        // Find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                Debug.LogError("No player found! Assign player or tag player with 'Player' tag");
            }
        }

        // Configure NavMeshAgent
        if (agent != null)
        {
            agent.speed = wanderSpeed; // Start with wander speed
            agent.stoppingDistance = stoppingDistance;
            agent.radius = avoidanceRadius;
            agent.avoidancePriority = avoidancePriority;
        }

        // Setup attack hitbox
        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
            enemyAttackCollider = attackHitbox.GetComponent<EnemyAttackCollider>();
        }

        if (debugRenderer != null)
        {
            debugRenderer.enabled = false;
        }

        // Start wandering
        SetWanderTarget();
    }

    void Update()
    {
        if (isStunned) return;

        // Don't update if dead
        if (health != null && health.IsDead())
        {
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsAttacking", false);
            }
            return;
        }

        if (player == null || agent == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Check vision periodically to reduce overhead
        if (Time.time >= nextVisionCheck)
        {
            CheckLineOfSight(distanceToPlayer);
            nextVisionCheck = Time.time + visionCheckInterval;
        }

        // Update animator
        if (animator != null)
        {
            float currentSpeed = agent.velocity.magnitude;
            animator.SetFloat("Speed", currentSpeed);
        }

        // Determine and handle current state
        UpdateState(distanceToPlayer);
    }

    void UpdateState(float distanceToPlayer)
    {
        EnemyState previousState = currentState;

        // Determine state based on vision and memory
        if (alwaysChase)
        {
            currentState = EnemyState.Chasing;
            isChasing = true;
        }
        else if (canSeePlayer)
        {
            currentState = EnemyState.Chasing;
            isChasing = true;
            lastSeenTime = Time.time;
            lastKnownPlayerPosition = player.position;
            hasLastKnownPosition = true;
        }
        else if (isChasing && Time.time - lastSeenTime < memoryDuration)
        {
            // Still in memory window - chase last known position
            currentState = EnemyState.Chasing;
            isChasing = true;
        }
        else
        {
            // Lost sight for too long - wander
            currentState = EnemyState.Wandering;
            isChasing = false;

            // Clean up combat state when transitioning to wandering
            if (previousState != EnemyState.Wandering)
            {
                CancelCombat();
                hasLastKnownPosition = false;

                if (showDebug)
                {
                    Debug.Log($"{gameObject.name} lost player - entering wander state");
                }
            }
        }

        // Handle behavior based on state
        switch (currentState)
        {
            case EnemyState.Wandering:
                HandleWandering();
                break;

            case EnemyState.Chasing:
                bool inAttackRange = distanceToPlayer <= attackRange;

                if (inAttackRange)
                {
                    // Just entered attack range
                    if (!wasInAttackRange && !isAttacking)
                    {
                        if (showDebug)
                        {
                            Debug.Log($"{gameObject.name} entered attack range - resetting cooldown");
                        }
                        nextAttackTime = Time.time;
                    }

                    HandleCombatRange(distanceToPlayer);
                    wasInAttackRange = true;
                }
                else
                {
                    HandleChaseMovement();
                    wasInAttackRange = false;
                }
                break;
        }
    }

    void HandleWandering()
    {
        // Set wander speed
        if (agent.speed != wanderSpeed)
        {
            agent.speed = wanderSpeed;
        }

        // Re-enable avoidance when wandering
        if (disableAvoidanceInCombat && agent.enabled)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        // Check if we've reached our wander target or need a new one
        if (!agent.pathPending && agent.remainingDistance <= wanderPointReachThreshold)
        {
            // Wait at current position
            if (Time.time >= nextWanderTime)
            {
                SetWanderTarget();
            }
            else
            {
                agent.isStopped = true;
            }
        }
        else
        {
            agent.isStopped = false;
        }

        if (showDebug && agent.hasPath)
        {
            Debug.DrawLine(transform.position, wanderTarget, Color.blue);
        }
    }

    void SetWanderTarget()
    {
        // Generate random point within wander radius
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;
        randomDirection.y = transform.position.y; // Keep on same Y level

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
        {
            wanderTarget = hit.position;
            agent.SetDestination(wanderTarget);
            agent.isStopped = false;

            // Set next wander time (when to pick a new point after reaching this one)
            nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);

            if (showDebug)
            {
                Debug.Log($"{gameObject.name} new wander target set at {wanderTarget}");
            }
        }
        else
        {
            // Couldn't find valid point, try again soon
            nextWanderTime = Time.time + 1f;
        }
    }

    void CheckLineOfSight(float distanceToPlayer)
    {
        canSeePlayer = false;

        // Range check
        if (distanceToPlayer > detectionRange) return;

        // Angle check
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if (angleToPlayer > viewAngle / 2f) return;

        // Line of sight check
        Vector3 rayStart = transform.position + Vector3.up * 1.5f;
        Vector3 playerCenter = player.position + Vector3.up * 1f;
        Vector3 rayDirection = (playerCenter - rayStart).normalized;
        float rayDistance = Vector3.Distance(rayStart, playerCenter);

        // Check obstacles
        RaycastHit obstacleHit;
        if (Physics.Raycast(rayStart, rayDirection, out obstacleHit, rayDistance, obstacleMask))
        {
            if (showDebug)
            {
                Debug.DrawLine(rayStart, obstacleHit.point, Color.red, visionCheckInterval);
            }
            return;
        }

        // Check player
        RaycastHit playerHit;
        if (Physics.Raycast(rayStart, rayDirection, out playerHit, rayDistance, playerMask))
        {
            if (playerHit.transform == player || playerHit.transform.root == player.root)
            {
                canSeePlayer = true;
                if (showDebug)
                {
                    Debug.DrawLine(rayStart, playerCenter, Color.green, visionCheckInterval);
                }
            }
        }
        else if (showDebug)
        {
            Debug.DrawLine(rayStart, rayStart + rayDirection * rayDistance, Color.yellow, visionCheckInterval);
        }
    }

    void HandleCombatRange(float distanceToPlayer)
    {
        agent.isStopped = true;

        // Set chase speed for combat responsiveness
        if (agent.speed != chaseSpeed)
        {
            agent.speed = chaseSpeed;
        }

        // Disable avoidance in combat
        if (disableAvoidanceInCombat && agent.enabled)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        // Face player
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        directionToPlayer.y = 0;
        if (directionToPlayer != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(directionToPlayer), 10f * Time.deltaTime);
        }

        // Debug attack readiness
        if (showDebug && !isAttacking)
        {
            float timeUntilAttack = nextAttackTime - Time.time;
            if (timeUntilAttack > 0)
            {
                Debug.Log($"{gameObject.name} waiting {timeUntilAttack:F2}s before next attack");
            }
        }

        // Attack if ready
        if (!isAttacking && !isStunned && Time.time >= nextAttackTime)
        {
            StartCombo();
        }
    }

    void HandleChaseMovement()
    {
        // Set chase speed
        if (agent.speed != chaseSpeed)
        {
            agent.speed = chaseSpeed;
        }

        // Re-enable avoidance when chasing
        if (disableAvoidanceInCombat && agent.enabled)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        agent.isStopped = false;

        // Chase current player position if visible, otherwise last known position
        Vector3 targetPosition = canSeePlayer ? player.position :
                                (hasLastKnownPosition ? lastKnownPlayerPosition : player.position);

        agent.SetDestination(targetPosition);

        if (showDebug)
        {
            Color lineColor = canSeePlayer ? Color.red : Color.yellow;
            Debug.DrawLine(transform.position, targetPosition, lineColor);
        }
    }

    public void ApplyHitstun(float duration)
    {
        // Check if enemy can be stunned right now
        if (Time.time - lastStunTime < stunCooldown)
        {
            if (showDebug)
            {
                Debug.Log($"{gameObject.name} immune to stun (cooldown active)");
            }
            return;
        }

        // Check if attacking and has hyperarmor
        if (isAttacking && !canBeStunnedDuringAttack)
        {
            if (showDebug)
            {
                Debug.Log($"{gameObject.name} immune to stun (hyperarmor during attack)");
            }
            return;
        }

        isStunned = true;
        lastStunTime = Time.time;

        // Only cancel attack if we can be stunned during attacks
        if (canBeStunnedDuringAttack)
        {
            CancelInvoke();
            DeactivateHitbox();
            isAttacking = false;
        }

        if (agent != null && agent.enabled)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        if (animator != null && canBeStunnedDuringAttack)
        {
            animator.SetBool("IsAttacking", false);
            animator.SetInteger("ComboStep", 0);
        }

        Invoke(nameof(RecoverFromStun), duration);

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} stunned for {duration}s");
        }
    }

    void RecoverFromStun()
    {
        isStunned = false;

        // Reset attack cooldown when recovering from stun
        if (!isAttacking)
        {
            nextAttackTime = Time.time;
            if (showDebug)
            {
                Debug.Log($"{gameObject.name} recovered from stun - can attack immediately");
            }
        }
    }

    void StartCombo()
    {
        targetComboLength = Random.Range(minComboAttacks, maxComboAttacks + 1);
        currentComboStep = 0;
        inCombat = true;

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} starting {targetComboLength}-attack combo");
        }

        PerformAttack();
    }

    void PerformAttack()
    {
        // Safety check - ensure we're still valid to attack
        if (!isChasing || isStunned)
        {
            if (showDebug)
            {
                Debug.Log($"{gameObject.name} attack cancelled - not chasing or stunned");
            }
            EndCombo();
            return;
        }

        currentComboStep++;
        isAttacking = true;
        lastAttackTime = Time.time;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", true);
            animator.SetInteger("ComboStep", currentComboStep);

            if (currentComboStep <= attackTriggers.Length)
            {
                animator.SetTrigger(attackTriggers[currentComboStep - 1]);
            }
        }

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} attack {currentComboStep}/{targetComboLength}");
        }

        if (debugRenderer != null && showDebug)
        {
            debugRenderer.enabled = true;
        }

        if (!useAnimationEvents)
        {
            Invoke(nameof(ActivateHitbox), attackWindupTime);
        }
    }

    public void ActivateHitbox()
    {
        if (enemyAttackCollider != null)
        {
            enemyAttackCollider.ClearHitList();
        }

        if (attackHitbox != null)
        {
            attackHitbox.enabled = true;
        }

        if (!useAnimationEvents)
        {
            Invoke(nameof(DeactivateHitbox), attackDuration);
        }
    }

    public void DeactivateHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }

        if (debugRenderer != null && showDebug)
        {
            debugRenderer.enabled = false;
        }

        isAttacking = false;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }

        // Only continue combo if we're still in combat and chasing
        if (currentComboStep < targetComboLength && inCombat && isChasing && !isStunned)
        {
            Invoke(nameof(PerformAttack), timeBetweenAttacks);
        }
        else
        {
            EndCombo();
        }
    }

    void EndCombo()
    {
        inCombat = false;
        currentComboStep = 0;
        targetComboLength = 0;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.SetInteger("ComboStep", 0);
        }

        nextAttackTime = Time.time + comboCooldown;

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} combo finished. Next attack at: {nextAttackTime:F2} (current: {Time.time:F2})");
        }
    }

    void CancelCombat()
    {
        // Cancel any pending invokes
        CancelInvoke(nameof(PerformAttack));
        CancelInvoke(nameof(ActivateHitbox));
        CancelInvoke(nameof(DeactivateHitbox));

        // Reset combat state
        isAttacking = false;
        inCombat = false;
        currentComboStep = 0;
        targetComboLength = 0;

        // Deactivate hitbox safely
        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }

        if (debugRenderer != null)
        {
            debugRenderer.enabled = false;
        }

        // Reset animator
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.SetInteger("ComboStep", 0);
        }

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} combat cancelled");
        }
    }

    void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Wander radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);

        // Stopping distance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);

        // Attack range
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // View cone
        Gizmos.color = canSeePlayer ? Color.green : Color.blue;
        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward * detectionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward * detectionRange;

        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Draw arc
        Vector3 previousPoint = transform.position + leftBoundary;
        for (int i = 1; i <= 20; i++)
        {
            float angle = -viewAngle / 2f + (viewAngle / 20f) * i;
            Vector3 newPoint = transform.position + Quaternion.Euler(0, angle, 0) * transform.forward * detectionRange;
            Gizmos.DrawLine(previousPoint, newPoint);
            previousPoint = newPoint;
        }

        // Draw current wander target
        if (Application.isPlaying && currentState == EnemyState.Wandering)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(wanderTarget, 0.5f);
        }
    }

    public void StopChasing()
    {
        isChasing = false;
        currentState = EnemyState.Wandering;
        if (agent != null)
        {
            agent.isStopped = true;
        }
    }

    public void ResumeChasing()
    {
        isChasing = true;
        currentState = EnemyState.Chasing;
        if (agent != null)
        {
            agent.isStopped = false;
        }
    }

    public void AlertToPlayer()
    {
        // Called when enemy takes damage - forces them to chase
        if (!isChasing)
        {
            isChasing = true;
            canSeePlayer = true;
            lastSeenTime = Time.time;
            currentState = EnemyState.Chasing;
            lastKnownPlayerPosition = player.position;
            hasLastKnownPosition = true;

            // Reset attack cooldown when newly alerted
            if (!isAttacking)
            {
                nextAttackTime = Time.time;
            }

            if (showDebug)
            {
                Debug.Log($"{gameObject.name} alerted to player!");
            }
        }

        // Alert nearby enemies
        AlertNearbyEnemies();
    }

    void AlertNearbyEnemies()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, alertRadius, enemyMask);

        foreach (Collider col in nearbyColliders)
        {
            if (col.transform == transform) continue;

            EnemyAI nearbyEnemy = col.GetComponent<EnemyAI>();
            if (nearbyEnemy != null && !nearbyEnemy.isChasing)
            {
                nearbyEnemy.AlertToPlayer();

                if (showDebug)
                {
                    Debug.Log($"{gameObject.name} alerted {col.name} to player!");
                }
            }
        }
    }

    // Public getters
    public bool IsAttacking() => isAttacking;
    public bool IsChasing() => isChasing;
    public bool IsWandering() => currentState == EnemyState.Wandering;
}