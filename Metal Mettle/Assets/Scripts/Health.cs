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
    public bool dropBloodOnHit = true; // Drop on every hit
    public bool dropBloodOnDeath = true; // Also drop on death
    public float bloodDropAmount = 25f; // Random range base
    public float bloodDropVariance = 10f; // ±10% randomization
    public Vector3 bloodDropOffset = new Vector3(0, 1, 0); // Spawn slightly above enemy

    [Header("Events")]
    public UnityEvent onDamage;
    public UnityEvent onDeath;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool isDead = false;
    private NavMeshAgent navAgent;

    void Start()
    {
        currentHealth = maxHealth;
        navAgent = GetComponent<NavMeshAgent>();
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
        }

        // Drop blood orb on hit
        if (dropBloodOnHit)
        {
            DropBloodOrb();
        }

        onDamage?.Invoke();

        if (currentHealth <= 0)
        {
            Die();
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

    void Die()
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

        // Drop blood orb on death
        if (dropBloodOnDeath)
        {
            DropBloodOrb();
        }

        onDeath?.Invoke();

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    void DropBloodOrb()
    {
        if (bloodOrbPrefab != null)
        {
            Debug.Log("Blood orb prefab found, spawning...");

            // Calculate random blood amount
            float variance = Random.Range(-bloodDropVariance, bloodDropVariance);
            float finalBloodAmount = bloodDropAmount + variance;
            finalBloodAmount = Mathf.Max(finalBloodAmount, 5f); // Minimum 5 blood

            // Spawn blood orb
            Vector3 spawnPos = transform.position + bloodDropOffset;
            GameObject orb = Instantiate(bloodOrbPrefab, spawnPos, Quaternion.identity);

            Debug.Log($"Blood orb spawned at {spawnPos}");

            // Set blood amount on orb
            BloodOrb orbScript = orb.GetComponent<BloodOrb>();
            if (orbScript != null)
            {
                orbScript.bloodAmount = finalBloodAmount;
                Debug.Log($"Set blood amount to {finalBloodAmount}");
            }
            else
            {
                Debug.LogError("Blood orb prefab is missing BloodOrb script!");
            }

            if (showDebugLogs)
            {
                Debug.Log($"Dropped blood orb with {finalBloodAmount} blood");
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} has no blood orb prefab assigned!");
        }
    }

    // Public getters
    public bool IsDead() => isDead;
    public float GetHealthPercent() => currentHealth / maxHealth;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}