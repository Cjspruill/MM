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
    public float stoppingDistance = 2f;

    [Header("Vision Detection")]
    public float detectionRange = 15f;
    public float viewAngle = 90f;
    public LayerMask obstacleMask;
    public LayerMask playerMask; // What layer is the player on
    public bool alwaysChase = false;
    public float memoryDuration = 3f;
    public float visionCheckInterval = 0.2f; // How often to check vision

    [Header("Alert System")]
    public float alertRadius = 5f; // Range to alert nearby enemies
    public LayerMask enemyMask; // What layer are other enemies on

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
    public float stunCooldown = 1.0f; // Minimum time between stuns
    public bool canBeStunnedDuringAttack = false; // Hyperarmor during attacks

    [Header("Avoidance Settings")]
    public float avoidanceRadius = 0.5f;
    public int avoidancePriority = 50;
    public bool disableAvoidanceInCombat = true;

    [Header("Debug")]
    public bool showDebug = true;

    // State tracking
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
    private float lastStunTime = -999f; // Track last stun time

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
            agent.speed = chaseSpeed;
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

        // Determine chase state
        UpdateChaseState();

        // Handle combat and movement
        if (isChasing)
        {
            if (distanceToPlayer <= attackRange)
            {
                HandleCombatRange(distanceToPlayer);
            }
            else
            {
                HandleChaseMovement();
            }
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

        // Line of sight check - from eye level to player center
        Vector3 rayStart = transform.position + Vector3.up * 1.5f;
        Vector3 playerCenter = player.position + Vector3.up * 1f;
        Vector3 rayDirection = (playerCenter - rayStart).normalized;
        float rayDistance = Vector3.Distance(rayStart, playerCenter);

        // First check: obstacles blocking view
        RaycastHit obstacleHit;
        if (Physics.Raycast(rayStart, rayDirection, out obstacleHit, rayDistance, obstacleMask))
        {
            // Something is blocking vision
            if (showDebug)
            {
                Debug.DrawLine(rayStart, obstacleHit.point, Color.red, visionCheckInterval);
            }
            return;
        }

        // Second check: can we see the player?
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

    void UpdateChaseState()
    {
        if (alwaysChase)
        {
            isChasing = true;
        }
        else if (canSeePlayer)
        {
            isChasing = true;
            lastSeenTime = Time.time;
        }
        else if (isChasing && Time.time - lastSeenTime < memoryDuration)
        {
            isChasing = true;
        }
        else
        {
            isChasing = false;
        }
    }

    void HandleCombatRange(float distanceToPlayer)
    {
        agent.isStopped = true;

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

        // Attack if ready
        if (!isAttacking && Time.time >= nextAttackTime)
        {
            StartCombo();
        }
    }

    void HandleChaseMovement()
    {
        // Re-enable avoidance when chasing
        if (disableAvoidanceInCombat && agent.enabled)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        agent.isStopped = false;
        agent.SetDestination(player.position);

        if (showDebug)
        {
            Debug.DrawLine(transform.position, player.position, Color.red);
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

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} recovered from stun");
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

        if (currentComboStep < targetComboLength)
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
            Debug.Log($"{gameObject.name} combo finished. Cooldown: {comboCooldown}s");
        }
    }

    void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

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
    }

    public void StopChasing()
    {
        isChasing = false;
        if (agent != null)
        {
            agent.isStopped = true;
        }
    }

    public void ResumeChasing()
    {
        isChasing = true;
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
        // Find all colliders in alert radius
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, alertRadius, enemyMask);

        foreach (Collider col in nearbyColliders)
        {
            // Don't alert self
            if (col.transform == transform) continue;

            // Try to get EnemyAI component
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

    public bool IsAttacking() => isAttacking;
}