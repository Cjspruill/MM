using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles all enemy movement: wandering, chasing, NavMeshAgent control
/// </summary>
public class EnemyMovement : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    private NavMeshAgent agent;
    private Animator animator;

    [Header("Movement Settings")]
    public float chaseSpeed = 3.5f;
    public float wanderSpeed = 2f;
    public float stoppingDistance = 1.5f;  // FIXED: Was 2.0, must be less than attackRange (2.5)

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

    [Header("Avoidance Settings")]
    public float avoidanceRadius = 0.5f;
    public int avoidancePriority = 50;
    public bool disableAvoidanceInCombat = true;

    [Header("Debug")]
    public bool showDebug = true;

    // Vision tracking
    private bool canSeePlayer = false;
    private float lastSeenTime = 0f;
    private float nextVisionCheck = 0f;

    // Wandering state
    private Vector3 wanderTarget;
    private float nextWanderTime = 0f;
    private Vector3 lastKnownPlayerPosition;
    private bool hasLastKnownPosition = false;

    // State tracking
    private bool isChasing = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

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
            agent.speed = wanderSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.radius = avoidanceRadius;
            agent.avoidancePriority = avoidancePriority;
        }

        SetWanderTarget();
    }

    public void UpdateMovement(bool isInCombat, bool isStunned)
    {
        if (isStunned || player == null || agent == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Check vision periodically
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
    }

    public void HandleChaseMovement()
    {
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

        Vector3 targetPosition = canSeePlayer ? player.position :
                                (hasLastKnownPosition ? lastKnownPlayerPosition : player.position);

        agent.SetDestination(targetPosition);

        if (showDebug)
        {
            Color lineColor = canSeePlayer ? Color.green : Color.yellow;
            Debug.DrawLine(transform.position, targetPosition, lineColor);
        }
    }

    public void HandleCombatMovement()
    {
        agent.isStopped = true;

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
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(directionToPlayer), 10f * Time.deltaTime);
        }
    }

    public void HandleWandering()
    {
        if (agent.speed != wanderSpeed)
        {
            agent.speed = wanderSpeed;
        }

        // Re-enable avoidance when wandering
        if (disableAvoidanceInCombat && agent.enabled)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        agent.isStopped = false;

        if (hasLastKnownPosition && Time.time - lastSeenTime < memoryDuration)
        {
            agent.SetDestination(lastKnownPlayerPosition);

            if (Vector3.Distance(transform.position, lastKnownPlayerPosition) < wanderPointReachThreshold)
            {
                hasLastKnownPosition = false;
            }
        }
        else
        {
            if (Time.time >= nextWanderTime)
            {
                SetWanderTarget();
            }

            agent.SetDestination(wanderTarget);

            if (Vector3.Distance(transform.position, wanderTarget) < wanderPointReachThreshold)
            {
                SetWanderTarget();
            }
        }
    }

    void SetWanderTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        Vector3 randomPoint = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPoint, out hit, wanderRadius, NavMesh.AllAreas))
        {
            wanderTarget = hit.position;
            nextWanderTime = Time.time + Random.Range(minWanderWaitTime, maxWanderWaitTime);

            if (showDebug)
            {
                Debug.Log($"{gameObject.name} new wander target: {wanderTarget}");
            }
        }
    }

    void CheckLineOfSight(float distanceToPlayer)
    {
        bool previousCanSee = canSeePlayer;

        if (alwaysChase)
        {
            canSeePlayer = true;
            isChasing = distanceToPlayer <= detectionRange;
            return;
        }

        canSeePlayer = false;
        isChasing = false;

        if (distanceToPlayer > detectionRange)
        {
            if (previousCanSee)
            {
                lastSeenTime = Time.time;
                lastKnownPlayerPosition = player.position;
                hasLastKnownPosition = true;
            }
            return;
        }

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if (angleToPlayer > viewAngle / 2f)
        {
            if (previousCanSee)
            {
                lastSeenTime = Time.time;
                lastKnownPlayerPosition = player.position;
                hasLastKnownPosition = true;
            }
            return;
        }

        Vector3 rayStart = transform.position + Vector3.up * 1.5f;
        Vector3 rayDirection = (player.position + Vector3.up * 1f) - rayStart;
        float rayDistance = rayDirection.magnitude;

        if (Physics.Raycast(rayStart, rayDirection.normalized, out RaycastHit hit, rayDistance, obstacleMask | playerMask))
        {
            if (hit.transform.CompareTag("Player"))
            {
                canSeePlayer = true;
                isChasing = true;
                lastSeenTime = Time.time;
                lastKnownPlayerPosition = player.position;
                hasLastKnownPosition = true;

                AlertNearbyEnemies();
            }
            else if (previousCanSee)
            {
                lastSeenTime = Time.time;
                lastKnownPlayerPosition = player.position;
                hasLastKnownPosition = true;
            }
        }

        if (showDebug)
        {
            Color rayColor = canSeePlayer ? Color.green : Color.red;
            Debug.DrawLine(rayStart, rayStart + rayDirection.normalized * rayDistance, rayColor, visionCheckInterval);
        }
    }

    void AlertNearbyEnemies()
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, alertRadius, enemyMask);

        foreach (Collider enemyCollider in nearbyEnemies)
        {
            if (enemyCollider.gameObject == gameObject) continue;

            EnemyMovement otherMovement = enemyCollider.GetComponent<EnemyMovement>();
            if (otherMovement != null && !otherMovement.IsChasing())
            {
                otherMovement.AlertToPlayer(player.position);
            }
        }
    }

    public void AlertToPlayer(Vector3 playerPosition)
    {
        lastKnownPlayerPosition = playerPosition;
        hasLastKnownPosition = true;
        lastSeenTime = Time.time;
        isChasing = true;

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} alerted to player at {playerPosition}");
        }
    }

    public void StopMovement()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }
        }
    }

    // Public getters
    public bool IsChasing() => isChasing;
    public bool CanSeePlayer() => canSeePlayer;
    public float GetDistanceToPlayer() => player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;
    public NavMeshAgent GetAgent() => agent;
}