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
    public UnityEvent onDeath;

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

        onDamage?.Invoke();

        if (currentHealth <= 0)
        {
            Die(isHeavyAttack);
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

        // Check if this is player's first kill
        TutorialManager tutorialManager = FindFirstObjectByType<TutorialManager>();
        if (tutorialManager != null)
        {
            // Find OnFirstKill tutorial and trigger it
            var step = tutorialManager.tutorialSteps.Find(s =>
                s.triggerType == TutorialTriggerType.OnFirstKill);

            if (step != null && !tutorialManager.HasCompletedStep(step.stepID))
            {
                tutorialManager.TriggerTutorial(step.stepID);
            }
        }

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

        // Spawn orbs in a spread pattern
        for (int i = 0; i < orbCount; i++)
        {
            // Random spread around the spawn point
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.5f, 0.5f),
                0,
                Random.Range(-0.5f, 0.5f)
            );
            Vector3 spawnPos = transform.position + bloodDropOffset + randomOffset;

            // Spawn blood orb - it will use its own bloodAmount from the prefab
            GameObject orb = Instantiate(bloodOrbPrefab, spawnPos, Quaternion.identity);

            // Set the source position on the orb so it knows where it came from
            BloodOrb bloodOrbComponent = orb.GetComponent<BloodOrb>();
            if (bloodOrbComponent != null)
            {
                bloodOrbComponent.SetSourcePosition(transform.position + bloodDropOffset);
            }
        }
    }

    // Public getters
    public bool IsDead() => isDead;
    public float GetHealthPercent() => currentHealth / maxHealth;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}