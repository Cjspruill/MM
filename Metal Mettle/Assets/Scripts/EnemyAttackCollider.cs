using UnityEngine;
using System.Collections.Generic;

public class EnemyAttackCollider : MonoBehaviour
{
    [Header("Damage Settings")]
    public float baseBloodDamage = 15f; // How much blood to drain from player
    public float blockedBloodDamage = 1f; // Blood drained when attack is blocked

    [Header("Block Reaction")]
    public float blockPushbackDistance = 1.5f; // How far to push enemy back when blocked
    public float blockPushbackDuration = 0.3f; // How long the pushback takes

    [Header("References")]
    public Transform forceOrigin; // For future use if needed
    public EnemyAI enemyAI; // Reference to the enemy AI

    private HashSet<Collider> hitThisAttack = new HashSet<Collider>();

    void Start()
    {
        if (forceOrigin == null)
        {
            forceOrigin = transform.parent; // Default to parent if not set
        }

        // Auto-find EnemyAI if not assigned
        if (enemyAI == null)
        {
            enemyAI = GetComponentInParent<EnemyAI>();
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

        // Get player blood system (used for both blocked and unblocked damage)
        var playerBloodSystem = other.GetComponent<BloodSystem>();

        // Check if player is actively blocking
        var comboController = other.GetComponent<ComboController>();
        if (comboController != null && comboController.IsBlockActive())
        {
            Debug.Log("Player blocked the attack!");

            // Drain reduced blood even when blocked
            if (playerBloodSystem != null)
            {
                playerBloodSystem.DrainBlood(blockedBloodDamage);
                Debug.Log($"✓ Enemy drained {blockedBloodDamage} blood from blocked player");
            }

            // Set successful block flag - bypasses recovery when player releases block
            comboController.SetSuccessfulBlock();

            // Trigger hit reaction on enemy
            if (enemyAI != null)
            {
                Animator enemyAnimator = enemyAI.GetComponent<Animator>();
                if (enemyAnimator != null)
                {
                    enemyAnimator.SetTrigger("HitReaction");
                    Debug.Log($"{enemyAI.gameObject.name} triggered HitReaction from block");
                }

                // Push enemy back
                Vector3 pushDirection = (enemyAI.transform.position - other.transform.position).normalized;
                pushDirection.y = 0; // Keep on ground
                Vector3 targetPosition = enemyAI.transform.position + (pushDirection * blockPushbackDistance);

                enemyAI.StartCoroutine(PushBackEnemy(enemyAI, targetPosition));

                // Cancel the enemy's current attack
                enemyAI.DeactivateHitbox();
            }

            return; // No full damage if blocking
        }

        // Damage player's blood system (full damage)
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

    private System.Collections.IEnumerator PushBackEnemy(EnemyAI enemy, Vector3 targetPosition)
    {
        Transform enemyTransform = enemy.transform;
        Vector3 startPosition = enemyTransform.position;
        float elapsed = 0f;

        // Disable NavMeshAgent during pushback
        UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        bool agentWasEnabled = agent != null && agent.enabled;

        if (agentWasEnabled)
        {
            agent.enabled = false; // Completely disable agent during pushback
        }

        while (elapsed < blockPushbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / blockPushbackDuration;

            // Ease-out cubic for smooth deceleration
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            // Lerp position each frame for gradual movement
            enemyTransform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);

            yield return null;
        }

        // Ensure we reach exact target position
        enemyTransform.position = targetPosition;

        // Re-enable NavMeshAgent and warp to new position
        if (agentWasEnabled)
        {
            agent.enabled = true;
            agent.Warp(targetPosition); // Sync agent with new position
        }
    }
}