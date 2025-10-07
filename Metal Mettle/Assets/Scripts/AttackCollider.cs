using UnityEngine;
using System.Collections.Generic;

public class AttackCollider : MonoBehaviour
{
    [Header("Damage Settings")]
    public float baseDamage = 10f;
    public float heavyDamageMultiplier = 2f;

    [Header("Force Settings")]
    public float baseForce = 500f;
    public float heavyForceMultiplier = 1.5f;
    public ForceMode forceMode = ForceMode.Impulse;

    [Header("References")]
    public Transform forceOrigin; // Where force comes from (usually player transform)

    private ComboController comboController;
    private HashSet<Collider> hitThisAttack = new HashSet<Collider>();

    void Start()
    {
        comboController = GetComponentInParent<ComboController>();

        if (forceOrigin == null)
        {
            forceOrigin = transform.parent; // Default to parent if not set
        }
    }

    void OnEnable()
    {
        // Clear hit list when attack starts
        ClearHitList();
    }

    void OnDisable()
    {
        // Also clear when disabled (end of attack)
        ClearHitList();
    }

    public void ClearHitList()
    {
        hitThisAttack.Clear();
        Debug.Log("Hit list cleared");
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Attack collider hit: {other.name}");

        // Don't hit the same object twice in one attack
        if (hitThisAttack.Contains(other))
        {
            Debug.Log($"Already hit {other.name} this attack");
            return;
        }

        // Don't hit the player
        if (other.transform.IsChildOf(transform.root))
        {
            Debug.Log($"{other.name} is part of player, ignoring");
            return;
        }

        // Mark as hit
        hitThisAttack.Add(other);

        // Calculate damage
        bool isHeavy = comboController != null && comboController.IsHeavyCombo();
        float damage = baseDamage;
        float force = baseForce;

        if (isHeavy)
        {
            damage *= heavyDamageMultiplier;
            force *= heavyForceMultiplier;
        }

        Debug.Log($"Attack type: {(isHeavy ? "Heavy" : "Light")}, Damage: {damage}, Force: {force}");

        // Apply hitstun to enemy
        var enemyAI = other.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            float stunDuration = isHeavy ? 0.6f : 0.3f;
            enemyAI.ApplyHitstun(stunDuration);
        }

        // Try to apply damage (if object has health component)
        var health = other.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Debug.Log($"✓ Hit {other.name} for {damage} damage");
        }
        else
        {
            Debug.LogWarning($"✗ {other.name} has no Health component!");
        }

        // Try to apply force (only to objects WITHOUT NavMeshAgent)
        var navAgent = other.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (navAgent != null)
        {
            Debug.Log($"Skipped force on {other.name} - has NavMeshAgent");
            return; // Exit early, don't apply force
        }

        var rb = other.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 forceDirection = (other.transform.position - forceOrigin.position).normalized;
            rb.AddForce(forceDirection * force, forceMode);
            Debug.Log($"✓ Applied {force} force to {other.name}");
        }
        else
        {
            Debug.LogWarning($"✗ {other.name} has no Rigidbody!");
        }
    }

    // Public method to set damage for specific attacks
    public void SetDamage(float damage)
    {
        baseDamage = damage;
    }

    // Public method to set force for specific attacks
    public void SetForce(float force)
    {
        baseForce = force;
    }
}