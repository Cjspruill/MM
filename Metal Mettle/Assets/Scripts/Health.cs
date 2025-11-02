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
    public bool alertOnDamage = true;

    [Range(0f, 1f)]
    public float damageAnimationChance = 0.5f; // 0.5 = 50% chance to play animation
    public float damageAnimCooldown = 0.5f; // prevents animation spam
    private float lastDamageAnimTime = -999f;

    [Header("Events")]
    public UnityEvent onDamage;
    public UnityEvent<bool> onLightAttack;
    public UnityEvent<bool> onHeavyAttack;
    public UnityEvent onDeath;
    public UnityEvent onExecution;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool isDead = false;
    private NavMeshAgent navAgent;
    private EnemyMovement enemyMovement;
    private Animator animator;

    void Start()
    {
        currentHealth = maxHealth;
        navAgent = GetComponent<NavMeshAgent>();
        enemyMovement = GetComponent<EnemyMovement>();
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
            DropBloodOrbs(isHeavyAttack);

        // Alert AI to player
        if (alertOnDamage && enemyMovement != null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                enemyMovement.AlertToPlayer(playerObj.transform.position);
        }

        // Handle damage animation chance and cooldown
        TryPlayDamageAnimation(isHeavyAttack);

        // Fire events
        onDamage?.Invoke();
        if (isHeavyAttack)
            onHeavyAttack?.Invoke(true);
        else
            onLightAttack?.Invoke(true);

        // Check death
        if (currentHealth <= 0)
            Die(isHeavyAttack);
    }

    private void TryPlayDamageAnimation(bool isHeavyAttack)
    {
        if (animator == null) return;

        // Heavy attacks always force a damage animation
        bool shouldPlay = isHeavyAttack || Random.value < damageAnimationChance;

        if (shouldPlay && Time.time - lastDamageAnimTime >= damageAnimCooldown)
        {
            animator.SetTrigger("Damage");
            lastDamageAnimTime = Time.time;

            if (showDebugLogs)
            {
                Debug.Log($"{gameObject.name} playing damage animation ({(isHeavyAttack ? "HEAVY FORCE" : $"{damageAnimationChance * 100f}% chance")})");
            }
        }
    }

    public void ExecutionKill()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0;

        if (showDebugLogs)
            Debug.Log($"{gameObject.name} was EXECUTED!");

        if (navAgent != null && navAgent.enabled)
            navAgent.enabled = false;

        if (dropBloodOnDeath)
            DropBloodOrbs(true);

        onExecution?.Invoke();
        onDeath?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        if (showDebugLogs)
            Debug.Log($"{gameObject.name} healed {amount}. Health: {currentHealth}/{maxHealth}");
    }

    void Die(bool isHeavyAttack = false)
    {
        if (isDead) return;
        isDead = true;

        if (showDebugLogs)
            Debug.Log($"{gameObject.name} died!");

        if (navAgent != null && navAgent.enabled)
            navAgent.enabled = false;

        if (dropBloodOnDeath)
            DropBloodOrbs(isHeavyAttack);

        onDeath?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    void DropBloodOrbs(bool isHeavyAttack)
    {
        if (bloodOrbPrefab == null)
        {
            Debug.LogWarning($"{gameObject.name} has no bloodOrbPrefab assigned!");
            return;
        }

        int orbCount = isHeavyAttack
            ? Random.Range(minHeavyOrbCount, maxHeavyOrbCount + 1)
            : Random.Range(minLightOrbCount, maxLightOrbCount + 1);

        if (showDebugLogs)
            Debug.Log($"{gameObject.name} dropping {orbCount} blood orbs ({(isHeavyAttack ? "HEAVY" : "LIGHT")} attack)");

        for (int i = 0; i < orbCount; i++)
        {
            Vector3 spawnPosition = transform.position + bloodDropOffset;
            GameObject orb = Instantiate(bloodOrbPrefab, spawnPosition, Quaternion.identity);

            BloodOrb bloodOrbScript = orb.GetComponent<BloodOrb>();
            if (bloodOrbScript != null)
            {
                float baseAmount = bloodOrbScript.bloodAmount;
                float variance = Random.Range(-0.1f, 0.1f);
                bloodOrbScript.bloodAmount = baseAmount * (1f + variance);
            }
        }
    }

    public bool IsDead() => isDead;
    public float GetHealthPercent() => currentHealth / maxHealth;
}
