using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    private NavMeshAgent agent;
    private Health health;

    [Header("Movement Settings")]
    public float chaseSpeed = 3.5f;
    public float stoppingDistance = 2f;

    [Header("Detection")]
    public float detectionRange = 15f;
    public bool alwaysChase = true;

    [Header("Combat Settings")]
    public BoxCollider attackHitbox;
    public MeshRenderer debugRenderer; // Optional debug visual
    public float attackRange = 2.5f;
    public float attackDuration = 0.3f;
    public int minComboAttacks = 1;
    public int maxComboAttacks = 3;
    public float timeBetweenAttacks = 0.8f; // Time between attacks in a combo
    public float comboCooldown = 2f; // Time before starting a new combo
    public float attackWindupTime = 0.5f; // Increased for telegraph

    [Header("Hitstun")]
    public float baseHitstunDuration = 0.4f;

    [Header("Debug")]
    public bool showDebug = true;

    private bool isChasing = false;
    private bool isAttacking = false;
    private bool inCombat = false;
    private int currentComboStep = 0;
    private int targetComboLength = 0;
    private float lastAttackTime = 0f;
    private float nextAttackTime = 0f;
    private EnemyAttackCollider enemyAttackCollider;
    private bool isStunned = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

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

        // Set agent settings
        if (agent != null)
        {
            agent.speed = chaseSpeed;
            agent.stoppingDistance = stoppingDistance;
        }

        // Ensure hitbox is off and get EnemyAttackCollider reference
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
        // Don't do anything while stunned
        if (isStunned) return;

        // Don't chase if dead
        if (health != null && health.IsDead())
        {
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
            }
            return;
        }

        if (player == null || agent == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Check if should chase
        if (alwaysChase || distanceToPlayer <= detectionRange)
        {
            isChasing = true;
        }

        // Combat logic
        if (isChasing)
        {
            // Check if in attack range
            if (distanceToPlayer <= attackRange)
            {
                // Stop moving when in attack range
                agent.isStopped = true;

                // Face player
                Vector3 directionToPlayer = (player.position - transform.position).normalized;
                directionToPlayer.y = 0;
                if (directionToPlayer != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(directionToPlayer), 10f * Time.deltaTime);
                }

                // Attack logic
                if (!isAttacking && Time.time >= nextAttackTime)
                {
                    StartCombo();
                }
            }
            else
            {
                // Chase player
                agent.isStopped = false;
                agent.SetDestination(player.position);

                if (showDebug)
                {
                    Debug.DrawLine(transform.position, player.position, Color.red);
                }
            }
        }
    }

    // Called by player's AttackCollider when hit
    public void ApplyHitstun(float duration)
    {
        isStunned = true;

        // Stop current attack
        CancelInvoke();
        DeactivateHitbox();
        isAttacking = false;

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
        // Decide combo length randomly
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

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} attack {currentComboStep}/{targetComboLength}");
        }

        // Show telegraph during windup
        if (debugRenderer != null)
        {
            debugRenderer.enabled = true;
        }

        // Windup delay before hitbox activates
        Invoke(nameof(ActivateHitbox), attackWindupTime);
    }

    void ActivateHitbox()
    {
        // Clear hit list before activating
        if (enemyAttackCollider != null)
        {
            enemyAttackCollider.ClearHitList();
        }

        if (attackHitbox != null)
        {
            attackHitbox.enabled = true;
        }

        // Debug renderer stays on during attack

        // Disable hitbox after attack duration
        Invoke(nameof(DeactivateHitbox), attackDuration);
    }

    void DeactivateHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }

        // Disable debug renderer
        if (debugRenderer != null)
        {
            debugRenderer.enabled = false;
        }

        isAttacking = false;

        // Check if combo should continue
        if (currentComboStep < targetComboLength)
        {
            // Continue combo after delay
            Invoke(nameof(PerformAttack), timeBetweenAttacks);
        }
        else
        {
            // Combo finished
            EndCombo();
        }
    }

    void EndCombo()
    {
        inCombat = false;
        currentComboStep = 0;
        targetComboLength = 0;

        // Set cooldown before next combo
        nextAttackTime = Time.time + comboCooldown;

        if (showDebug)
        {
            Debug.Log($"{gameObject.name} combo finished. Cooldown: {comboCooldown}s");
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw stopping distance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);

        // Draw attack range
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    // Public method to stop chasing
    public void StopChasing()
    {
        isChasing = false;
        if (agent != null)
        {
            agent.isStopped = true;
        }
    }

    // Public method to resume chasing
    public void ResumeChasing()
    {
        isChasing = true;
        if (agent != null)
        {
            agent.isStopped = false;
        }
    }

    // Public getter for attack state
    public bool IsAttacking() => isAttacking;
}