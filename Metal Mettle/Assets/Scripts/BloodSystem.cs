using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class BloodSystem : MonoBehaviour
{
    [Header("Blood Meter")]
    public float maxBlood = 100f;
    public float currentBlood = 50f;
    public float baseMaxBlood = 100f;

    [Header("Depletion")]
    public float passiveDrainRate = 0f;
    public float lightAttackCost = 2f;
    public float heavyAttackCost = 3f;

    [Header("Desperation (Self-Harm)")]
    public bool desperationUnlocked = false;
    public float desperationGain = 35f;
    public float desperationMaxCapacityLoss = 10f;
    public float desperationMinBlood = 25f;
    public int desperationUsesThisLife = 0;

    [Header("Withdrawal Effects (Visual/Mechanical)")]
    public float optimalMin = 75f;
    public float mildMin = 50f;
    public float moderateMin = 25f;

    [Header("UI")]
    public Slider bloodSlider;
    public Image bloodFillImage;
    public Color highBloodColor = Color.red;
    public Color lowBloodColor = new Color(0.3f, 0, 0);

    [Header("Post-Processing (Withdrawal)")]
    public bool enableWithdrawalEffects = true;
    public float maxVignetteIntensity = 0.6f;
    public float maxScreenDarkening = 0.4f;

    [Header("Death Settings")]
    public bool enableDeath = true;
    public bool useRagdollOnDeath = true; // NEW
    public float deathDelay = 2f;
    public string deathSceneName = "";
    public GameObject deathScreenUI;

    [Header("References")]
    public RagdollController ragdollController; // NEW

    [Header("Events")]
    public UnityEvent onBloodDepleted;
    public UnityEvent onDesperationUsed;
    public UnityEvent<float> onBloodChanged;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private bool isDead = false;

    void Start()
    {
        currentBlood = maxBlood * 0.5f;
        baseMaxBlood = maxBlood;
        isDead = false;

        // Auto-find ragdoll controller if not assigned
        if (ragdollController == null)
        {
            ragdollController = GetComponent<RagdollController>();
        }

        if (deathScreenUI != null)
        {
            deathScreenUI.SetActive(false);
        }

        UpdateUI();
    }

    void Update()
    {
        if (isDead) return;

        if (passiveDrainRate > 0)
        {
            DrainBlood(passiveDrainRate * Time.deltaTime);
        }

        if (enableWithdrawalEffects)
        {
            ApplyWithdrawalEffects();
        }

        UpdateUI();
    }

    public void DrainBlood(float amount)
    {
        if (isDead) return;

        currentBlood -= amount;
        currentBlood = Mathf.Max(currentBlood, 0);

        if (showDebugLogs)
        {
            Debug.Log($"Blood drained: {amount}. Current: {currentBlood}/{maxBlood}");
        }

        onBloodChanged?.Invoke(GetBloodPercent());

        if (currentBlood <= 0 && enableDeath && !isDead)
        {
            Death();
        }
    }

    public void GainBlood(float amount)
    {
        if (isDead) return;

        currentBlood += amount;
        currentBlood = Mathf.Min(currentBlood, maxBlood);

        if (showDebugLogs)
        {
            Debug.Log($"Blood gained: {amount}. Current: {currentBlood}/{maxBlood}");
        }

        onBloodChanged?.Invoke(GetBloodPercent());
    }

    public void OnAttack(bool isHeavy)
    {
        if (isDead) return;

        float cost = isHeavy ? heavyAttackCost : lightAttackCost;
        DrainBlood(cost);
    }

    public void UseDesperation()
    {
        if (isDead) return;

        if (!desperationUnlocked)
        {
            Debug.LogWarning("Desperation not unlocked yet!");
            return;
        }

        if (currentBlood > desperationMinBlood)
        {
            Debug.LogWarning($"Can only use Desperation below {desperationMinBlood}% blood!");
            return;
        }

        maxBlood -= desperationMaxCapacityLoss;
        maxBlood = Mathf.Max(maxBlood, 10f);

        GainBlood(desperationGain);
        currentBlood = Mathf.Min(currentBlood, maxBlood);

        desperationUsesThisLife++;

        if (showDebugLogs)
        {
            Debug.Log($"Desperation used! Gained {desperationGain} blood. Max capacity now: {maxBlood}");
        }

        onDesperationUsed?.Invoke();
    }

    public void ResetMaxCapacity()
    {
        maxBlood = baseMaxBlood;
        desperationUsesThisLife = 0;
        isDead = false;

        if (showDebugLogs)
        {
            Debug.Log("Max blood capacity reset to 100%");
        }
    }

    void Death()
    {
        if (isDead) return;

        isDead = true;

        if (showDebugLogs)
        {
            Debug.Log("💀 DEATH - Blood depleted!");
        }

        // Trigger death event
        onBloodDepleted?.Invoke();

        // Show death screen if available
        if (deathScreenUI != null)
        {
            deathScreenUI.SetActive(true);
        }

        // Enable ragdoll if available
        if (useRagdollOnDeath && ragdollController != null)
        {
            ragdollController.EnableRagdoll();
        }
        else
        {
            // Fallback to disabling controls
            DisablePlayerControls();
        }

        // Trigger respawn/reload after delay
        if (deathDelay > 0)
        {
            Invoke(nameof(HandleRespawn), deathDelay);
        }
        else
        {
            HandleRespawn();
        }
    }

    void DisablePlayerControls()
    {
        // Only used if ragdoll is disabled
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        ComboController comboController = GetComponent<ComboController>();
        if (comboController != null)
        {
            comboController.enabled = false;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Death");
        }

        if (showDebugLogs)
        {
            Debug.Log("Player controls disabled");
        }
    }

    void HandleRespawn()
    {
        if (showDebugLogs)
        {
            Debug.Log("Handling respawn...");
        }

        if (string.IsNullOrEmpty(deathSceneName))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            SceneManager.LoadScene(deathSceneName);
        }
    }

    public void Respawn()
    {
        HandleRespawn();
    }

    void ApplyWithdrawalEffects()
    {
        float bloodPercent = GetBloodPercent();
        WithdrawalState state = GetWithdrawalState();

        switch (state)
        {
            case WithdrawalState.Optimal:
                break;
            case WithdrawalState.Mild:
                break;
            case WithdrawalState.Moderate:
                break;
            case WithdrawalState.Severe:
                break;
        }
    }

    public WithdrawalState GetWithdrawalState()
    {
        float bloodPercent = GetBloodPercent();

        if (bloodPercent >= optimalMin) return WithdrawalState.Optimal;
        if (bloodPercent >= mildMin) return WithdrawalState.Mild;
        if (bloodPercent >= moderateMin) return WithdrawalState.Moderate;
        return WithdrawalState.Severe;
    }

    public float GetAttackSpeedModifier()
    {
        switch (GetWithdrawalState())
        {
            case WithdrawalState.Optimal: return 1.0f;
            case WithdrawalState.Mild: return 0.9f;
            case WithdrawalState.Moderate: return 0.75f;
            case WithdrawalState.Severe: return 0.6f;
            default: return 1.0f;
        }
    }

    public float GetDamageModifier()
    {
        switch (GetWithdrawalState())
        {
            case WithdrawalState.Optimal: return 1.0f;
            case WithdrawalState.Mild: return 0.9f;
            case WithdrawalState.Moderate: return 0.75f;
            case WithdrawalState.Severe: return 0.6f;
            default: return 1.0f;
        }
    }

    void UpdateUI()
    {
        if (bloodSlider != null)
        {
            bloodSlider.maxValue = baseMaxBlood;
            bloodSlider.value = currentBlood;
        }

        if (bloodFillImage != null)
        {
            bloodFillImage.color = Color.Lerp(lowBloodColor, highBloodColor, GetBloodPercent() / 100f);
        }
    }

    public float GetBloodPercent() => (currentBlood / maxBlood) * 100f;
    public float GetCurrentBlood() => currentBlood;
    public float GetMaxBlood() => maxBlood;
    public bool CanUseDesperation() => desperationUnlocked && currentBlood <= desperationMinBlood;
    public bool IsDead() => isDead;
}

public enum WithdrawalState
{
    Optimal,
    Mild,
    Moderate,
    Severe
}