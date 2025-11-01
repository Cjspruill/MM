using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Death Settings")]
    public bool destroyOnDeath = true;
    public float destroyDelay = 2f;

    [Header("Blood Drop Settings")]
    public GameObject bloodOrbPrefab;
    public bool dropBloodOnHit = true;
    public bool dropBloodOnDeath = true;
    public Vector3 bloodDropOffset = new Vector3(0, 1, 0);

    [Header("Blood Orb Count")]
    public int minLightOrbCount = 1;
    public int maxLightOrbCount = 3;
    public int minHeavyOrbCount = 2;
    public int maxHeavyOrbCount = 6;

    [Header("Alert Settings")]
    public bool alertOnDamage = true; // Alert AI when taking damage
    public float damageAnimationChance = 0.5f; // 50% chance to play damage animation

    [Header("Events")]
    public UnityEvent onDamage;
    public UnityEvent<bool> onLightAttack;  // NEW: Fires when hit by light attack (passes true)
    public UnityEvent<bool> onHeavyAttack;  // NEW: Fires when hit by heavy attack (passes true)
    public UnityEvent onDeath;
    public UnityEvent onExecution;  // NEW: Fires when killed by execution

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool isDead = false;
    private NavMeshAgent navAgent;
    private EnemyAI enemyAI;
    private Animator animator;

    void Start()
    {
        currentHealth = maxHealth;
        navAgent = GetComponent<NavMeshAgent>();
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();
    }

    public void TakeDamage(float damage, bool isHeavyAttack = false)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} took {damage} damage ({(isHeavyAttack ? "HEAVY" : "LIGHT")} attack). Health: {currentHealth}/{maxHealth}");
        }

        // Drop blood orbs on hit
        if (dropBloodOnHit)
        {
            DropBloodOrbs(isHeavyAttack);
        }

        // Alert enemy AI to player
        if (alertOnDamage && enemyAI != null)
        {
            enemyAI.AlertToPlayer();
        }

        // Play damage animation 50% of the time
        if (animator != null && Random.value <= damageAnimationChance)
        {
            animator.SetTrigger("Damage");

            if (showDebugLogs)
            {
                Debug.Log($"{gameObject.name} playing damage animation");
            }
        }

        // Fire the general damage event
        onDamage?.Invoke();

        // NEW: Fire specific attack type events for tracking
        if (isHeavyAttack)
        {
            onHeavyAttack?.Invoke(true);
        }
        else
        {
            onLightAttack?.Invoke(true);
        }

        if (currentHealth <= 0)
        {
            Die(isHeavyAttack);
        }
    }

    // NEW: Special execution kill method
    public void ExecutionKill()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0;

        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} was EXECUTED!");
        }

        // Disable NavMesh agent on death
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.enabled = false;
        }

        // Drop blood orbs (use heavy attack amount for executions)
        if (dropBloodOnDeath)
        {
            DropBloodOrbs(true);
        }

        // Fire execution event FIRST (so ObjectiveManager can track it)
        onExecution?.Invoke();

        // Then fire death event
        onDeath?.Invoke();

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} healed {amount}. Health: {currentHealth}/{maxHealth}");
        }
    }

    void Die(bool isHeavyAttack = false)
    {
        if (isDead) return;

        isDead = true;

        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} died!");
        }

        // Disable NavMesh agent on death
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.enabled = false;
        }

        // Drop blood orbs on death
        if (dropBloodOnDeath)
        {
            DropBloodOrbs(isHeavyAttack);
        }

        // Check if this is player's first kill (tutorial)
        TutorialManager tutorialManager = FindFirstObjectByType<TutorialManager>();
        if (tutorialManager != null)
        {
            var step = tutorialManager.tutorialSteps.Find(s =>
                s.triggerType == TutorialTriggerType.OnFirstKill);

            if (step != null && !tutorialManager.HasCompletedStep(step.stepID))
            {
                tutorialManager.TriggerTutorial(step.stepID);
            }
        }

        // Check for boss enemy
        BossEnemy bossEnemy = GetComponent<BossEnemy>();
        if (bossEnemy != null)
        {
            bossEnemy.UpdateObjectiveController();
        }

        onDeath?.Invoke();

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    void DropBloodOrbs(bool isHeavyAttack)
    {
        if (bloodOrbPrefab == null)
        {
            Debug.LogWarning($"{gameObject.name} has no blood orb prefab assigned!");
            return;
        }

        // Determine how many orbs to drop
        int orbCount;
        if (isHeavyAttack)
        {
            orbCount = Random.Range(minHeavyOrbCount, maxHeavyOrbCount + 1);
        }
        else
        {
            orbCount = Random.Range(minLightOrbCount, maxLightOrbCount + 1);
        }

        if (showDebugLogs)
        {
            Debug.Log($"Dropping {orbCount} blood orbs ({(isHeavyAttack ? "HEAVY" : "LIGHT")} attack)");
        }

        // Spawn orbs with explosion physics
        Vector3 spawnPosition = transform.position + bloodDropOffset;

        for (int i = 0; i < orbCount; i++)
        {
            GameObject orb = Instantiate(bloodOrbPrefab, spawnPosition, Quaternion.identity);

            // Apply random explosion force
            Rigidbody rb = orb.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = Mathf.Abs(randomDirection.y); // Keep orbs going up/out
                rb.AddForce(randomDirection * Random.Range(3f, 6f), ForceMode.Impulse);
            }
        }
    }

    public bool IsDead()
    {
        return isDead;
    }

    public float GetHealthPercent()
    {
        return currentHealth / maxHealth;
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }
}