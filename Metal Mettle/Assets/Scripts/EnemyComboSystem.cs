using UnityEngine;

/// <summary>
/// Enhanced Enemy combat system with pause support
/// Handles attacks, combos, and hitboxes
/// Now respects cutscene and tutorial pause states
/// </summary>
public class EnemyComboSystem : MonoBehaviour
{
    [Header("Combat Settings")]
    public BoxCollider attackHitbox;
    public MeshRenderer debugRenderer;
    public float attackRange = 2.5f;
    public float attackDuration = 0.3f;
    public int minComboAttacks = 2;
    public int maxComboAttacks = 3;
    public float timeBetweenAttacks = 0.5f;
    public float comboCooldown = 1.0f;
    public float attackWindupTime = 0.5f;

    [Header("Animation Settings")]
    public string[] attackTriggers = { "Attack1", "Attack2", "Attack3", "Attack4" };
    public bool useAnimationEvents = true;

    [Header("Hitstun")]
    public float baseHitstunDuration = 0.4f;
    public float stunCooldown = 1.0f;
    public bool canBeStunnedDuringAttack = false;

    [Header("Debug")]
    public bool showDebug = true;

    // References
    private Animator animator;
    private EnemyAttackCollider enemyAttackCollider;
    private TutorialManager tutorialManager;

    // Combat state
    [SerializeField] private bool isAttacking = false;
    [SerializeField] private bool inCombat = false;
    [SerializeField] private int currentComboStep = 0;
    private int targetComboLength = 0;
    private float nextAttackTime = 0f;
    private float attackStartTime = 0f;

    // Hitstun
    [SerializeField] private bool isStunned = false;
    private float lastStunTime = -999f;

    // Pause tracking
    private bool isPaused = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        tutorialManager = FindFirstObjectByType<TutorialManager>();

        if (animator == null)
            Debug.LogError($"{gameObject.name}: No Animator component found!");

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
            enemyAttackCollider = attackHitbox.GetComponent<EnemyAttackCollider>();
        }

        if (debugRenderer != null)
            debugRenderer.enabled = false;

        if (showDebug)
            Debug.Log($"{gameObject.name} initialized combat system with pause support (cooldown: {comboCooldown}s)");
    }

    void Update()
    {
        // Check if paused by tutorial
        if (tutorialManager == null)
        {
            tutorialManager = FindFirstObjectByType<TutorialManager>();
        }

        bool newPauseState = tutorialManager != null && tutorialManager.IsShowingTutorial;

        if (newPauseState != isPaused)
        {
            isPaused = newPauseState;

            if (isPaused)
            {
                if (showDebug)
                {
                    Debug.Log($"{gameObject.name} combat paused by tutorial");
                }

                // Cancel any ongoing attacks immediately
                if (isAttacking || inCombat)
                {
                    CancelAttack();
                }
            }
            else if (showDebug)
            {
                Debug.Log($"{gameObject.name} combat resumed after tutorial");
            }
        }

        // Emergency failsafe - if stuck attacking too long, reset
        if (isAttacking && Time.time - attackStartTime > attackDuration + 2f)
        {
            Debug.LogWarning($"⚠️ {gameObject.name} stuck attacking for {Time.time - attackStartTime:F1}s - forcing reset!");
            CancelAttack();
        }
    }

    public bool CanAttack()
    {
        // CRITICAL: Don't allow attacks while paused
        if (isPaused)
        {
            return false;
        }

        bool canAttack = !isAttacking && !isStunned && Time.time >= nextAttackTime;
        return canAttack;
    }

    public bool WantsToAttack()
    {
        // CRITICAL: Don't want to attack while paused
        if (isPaused)
        {
            return false;
        }

        return Time.time >= nextAttackTime;
    }

    public void StartCombo()
    {
        if (!CanAttack())
        {
            if (showDebug)
                Debug.LogWarning($"❌ {gameObject.name} StartCombo() BLOCKED by CanAttack()");
            return;
        }

        // Double-check pause state
        if (isPaused)
        {
            if (showDebug)
                Debug.LogWarning($"❌ {gameObject.name} StartCombo() BLOCKED by pause state");
            return;
        }

        inCombat = true;
        currentComboStep = 0;
        targetComboLength = Random.Range(minComboAttacks, maxComboAttacks + 1);

        if (showDebug)
        {
            Debug.Log($"═══════════════════════════════════════");
            Debug.Log($"🔥 {gameObject.name} COMBO STARTING with {targetComboLength} attacks 🔥");
            Debug.Log($"═══════════════════════════════════════");
        }

        PerformAttack();
    }

    void PerformAttack()
    {
        // CRITICAL: Check pause state before performing attack
        if (isPaused)
        {
            if (showDebug)
                Debug.Log($"❌ {gameObject.name} PerformAttack() cancelled - paused");
            EndCombo();
            return;
        }

        if (isStunned)
        {
            EndCombo();
            return;
        }

        isAttacking = true;
        attackStartTime = Time.time;
        currentComboStep++;

        if (animator != null)
        {
            int attackIndex = (currentComboStep - 1) % attackTriggers.Length;
            string triggerName = attackTriggers[attackIndex];

            animator.SetTrigger(triggerName);
            animator.SetBool("IsAttacking", true);
            animator.SetInteger("ComboStep", currentComboStep);

            if (showDebug)
                Debug.Log($"⚔️ {gameObject.name} attack {currentComboStep}/{targetComboLength} - {triggerName}");
        }

        // Always schedule fallback activation/deactivation
        CancelInvoke(nameof(ActivateHitbox));
        CancelInvoke(nameof(DeactivateHitbox));
        Invoke(nameof(ActivateHitbox), attackWindupTime);
        Invoke(nameof(DeactivateHitbox), attackWindupTime + attackDuration + 0.05f);
    }

    public void ActivateHitbox()
    {
        // CRITICAL: Don't activate hitbox if paused
        if (isPaused)
        {
            if (showDebug)
                Debug.Log($"❌ {gameObject.name} ActivateHitbox() cancelled - paused");
            return;
        }

        if (enemyAttackCollider != null)
            enemyAttackCollider.ClearHitList();

        if (attackHitbox != null)
            attackHitbox.enabled = true;

        if (debugRenderer != null && showDebug)
            debugRenderer.enabled = true;
    }

    public void DeactivateHitbox()
    {
        if (!isAttacking && !inCombat)
            return;

        if (attackHitbox != null)
            attackHitbox.enabled = false;

        if (debugRenderer != null && showDebug)
            debugRenderer.enabled = false;

        isAttacking = false;

        if (animator != null)
            animator.SetBool("IsAttacking", false);

        // Continue combo if not finished and not paused
        if (currentComboStep < targetComboLength && inCombat && !isStunned && !isPaused)
        {
            if (showDebug)
                Debug.Log($"➡️ {gameObject.name} preparing next attack in combo ({currentComboStep}/{targetComboLength})...");
            Invoke(nameof(PerformAttack), timeBetweenAttacks);
        }
        else
        {
            EndCombo();
        }
    }

    void EndCombo()
    {
        isAttacking = false;
        inCombat = false;
        currentComboStep = 0;
        targetComboLength = 0;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.SetInteger("ComboStep", 0);
        }

        nextAttackTime = Time.time + comboCooldown;

        if (showDebug)
            Debug.Log($"✅ {gameObject.name} combo ENDED. Next attack ready at {nextAttackTime:F2}");
    }

    public void ApplyHitstun(float duration)
    {
        if (isStunned) return;

        float timeSinceLastStun = Time.time - lastStunTime;
        if (timeSinceLastStun < stunCooldown)
            return;

        if (isAttacking && !canBeStunnedDuringAttack)
            return;

        isStunned = true;
        lastStunTime = Time.time;

        if (animator != null)
            animator.SetTrigger("HitReaction");

        CancelInvoke(nameof(PerformAttack));
        CancelInvoke(nameof(ActivateHitbox));
        CancelInvoke(nameof(DeactivateHitbox));

        if (attackHitbox != null)
            attackHitbox.enabled = false;

        isAttacking = false;
        EndCombo();

        Invoke(nameof(RecoverFromHitstun), duration);
    }

    void RecoverFromHitstun()
    {
        isStunned = false;
        if (showDebug)
            Debug.Log($"{gameObject.name} recovered from hitstun and ready!");
    }

    public void CancelAttack()
    {
        CancelInvoke(nameof(PerformAttack));
        CancelInvoke(nameof(ActivateHitbox));
        CancelInvoke(nameof(DeactivateHitbox));

        if (attackHitbox != null)
            attackHitbox.enabled = false;

        if (debugRenderer != null && showDebug)
            debugRenderer.enabled = false;

        isAttacking = false;
        EndCombo();
    }

    // Public getters
    public bool IsAttacking() => isAttacking;
    public bool IsStunned() => isStunned;
    public bool IsInCombat() => inCombat;
    public float GetAttackRange() => attackRange;
    public float GetNextAttackTime() => nextAttackTime;
}