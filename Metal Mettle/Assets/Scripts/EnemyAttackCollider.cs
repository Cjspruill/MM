using UnityEngine;
using System.Collections.Generic;

public class EnemyAttackCollider : MonoBehaviour
{
    [Header("Damage Settings")]
    public float baseBloodDamage = 15f; // How much blood to drain from player

    [Header("References")]
    public Transform forceOrigin; // For future use if needed

    private HashSet<Collider> hitThisAttack = new HashSet<Collider>();

    void Start()
    {
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
        Debug.Log("Enemy hit list cleared");
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Enemy attack collider hit: {other.name}");

        // Don't hit the same object twice in one attack
        if (hitThisAttack.Contains(other))
        {
            Debug.Log($"Already hit {other.name} this attack");
            return;
        }

        // Don't hit the enemy itself
        if (other.transform.IsChildOf(transform.root))
        {
            Debug.Log($"{other.name} is part of enemy, ignoring");
            return;
        }

        // Only damage the player
        if (!other.CompareTag("Player"))
        {
            Debug.Log($"{other.name} is not the player, ignoring");
            return;
        }

        // Mark as hit
        hitThisAttack.Add(other);

        // Check if player is actively blocking
        var comboController = other.GetComponent<ComboController>();
        if (comboController != null && comboController.IsBlockActive())
        {
            Debug.Log("Player blocked the attack!");
            return; // No damage if blocking
        }

        // Damage player's blood system
        var playerBloodSystem = other.GetComponent<BloodSystem>();
        if (playerBloodSystem != null)
        {
            playerBloodSystem.DrainBlood(baseBloodDamage);
            Debug.Log($"✓ Enemy drained {baseBloodDamage} blood from player");
        }
        else
        {
            Debug.LogWarning($"✗ Player has no BloodSystem component!");
        }
    }

    // Public method to set damage for specific attacks
    public void SetBloodDamage(float damage)
    {
        baseBloodDamage = damage;
    }
}