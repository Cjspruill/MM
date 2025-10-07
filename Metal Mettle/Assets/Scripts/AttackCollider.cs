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
    public Transform forceOrigin;

    private ComboController comboController;
    private BloodSystem bloodSystem;
    private HashSet<Collider> hitThisAttack = new HashSet<Collider>();

    void Start()
    {
        comboController = GetComponentInParent<ComboController>();
        bloodSystem = GetComponentInParent<BloodSystem>();

        if (forceOrigin == null)
        {
            forceOrigin = transform.parent;
        }
    }

    void OnEnable()
    {
        ClearHitList();
    }

    void OnDisable()
    {
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

        // Apply withdrawal damage modifier
        float damageModifier = bloodSystem != null ? bloodSystem.GetDamageModifier() : 1f;
        damage *= damageModifier;

        Debug.Log($"Attack type: {(isHeavy ? "Heavy" : "Light")}, Damage: {damage} (modifier: {damageModifier:F2}), Force: {force}");

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
            // PASS isHeavy to TakeDamage so it knows how many orbs to drop!
            health.TakeDamage(damage, isHeavy);
            Debug.Log($"✓ Hit {other.name} for {damage} damage ({(isHeavy ? "HEAVY" : "LIGHT")} attack)");
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

            // Also ensure rigidbody is kinematic if present
            var rb = other.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                Debug.LogWarning($"{other.name} has NavMeshAgent but non-kinematic Rigidbody! Setting to kinematic.");
                rb.isKinematic = true;
            }

            return;
        }

        var rigidBody = other.GetComponent<Rigidbody>();
        if (rigidBody != null)
        {
            Vector3 forceDirection = (other.transform.position - forceOrigin.position).normalized;
            rigidBody.AddForce(forceDirection * force, forceMode);
            Debug.Log($"✓ Applied {force} force to {other.name}");
        }
        else
        {
            Debug.LogWarning($"✗ {other.name} has no Rigidbody!");
        }
    }

    public void SetDamage(float damage)
    {
        baseDamage = damage;
    }

    public void SetForce(float force)
    {
        baseForce = force;
    }
}