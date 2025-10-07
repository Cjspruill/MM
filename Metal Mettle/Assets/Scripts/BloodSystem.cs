using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class BloodSystem : MonoBehaviour
{
    [Header("Blood Meter")]
    public float maxBlood = 100f;
    public float currentBlood = 50f; // Starts at 50%
    public float baseMaxBlood = 100f; // For checkpoint resets

    [Header("Depletion")]
    public float passiveDrainRate = 0f; // No passive drain outside combat
    public float lightAttackCost = 2f;
    public float heavyAttackCost = 3f;

    [Header("Desperation (Self-Harm)")]
    public bool desperationUnlocked = false; // Unlocks Chapter 2
    public float desperationGain = 35f;
    public float desperationMaxCapacityLoss = 10f;
    public float desperationMinBlood = 25f; // Can only use below 25%
    public int desperationUsesThisLife = 0;

    [Header("Withdrawal Effects (Visual/Mechanical)")]
    public float optimalMin = 75f; // 75-100% = optimal
    public float mildMin = 50f; // 50-74% = mild withdrawal
    public float moderateMin = 25f; // 25-49% = moderate withdrawal
    // 0-24% = severe withdrawal

    [Header("UI")]
    public Slider bloodSlider;
    public Image bloodFillImage;
    public Color highBloodColor = Color.red;
    public Color lowBloodColor = new Color(0.3f, 0, 0); // Dark red

    [Header("Post-Processing (Withdrawal)")]
    public bool enableWithdrawalEffects = true;
    public float maxVignetteIntensity = 0.6f;
    public float maxScreenDarkening = 0.4f;

    [Header("Events")]
    public UnityEvent onBloodDepleted; // Death at 0%
    public UnityEvent onDesperationUsed;
    public UnityEvent<float> onBloodChanged; // Passes current blood %

    [Header("Debug")]
    public bool showDebugLogs = false;

    void Start()
    {
        currentBlood = maxBlood * 0.5f; // Start at 50%
        baseMaxBlood = maxBlood;
        UpdateUI();
    }

    void Update()
    {
        // Passive drain (currently 0, but available for combat-only drain)
        if (passiveDrainRate > 0)
        {
            DrainBlood(passiveDrainRate * Time.deltaTime);
        }

        // Update withdrawal effects
        if (enableWithdrawalEffects)
        {
            ApplyWithdrawalEffects();
        }

        UpdateUI();
    }

    public void DrainBlood(float amount)
    {
        currentBlood -= amount;
        currentBlood = Mathf.Max(currentBlood, 0);

        if (showDebugLogs)
        {
            Debug.Log($"Blood drained: {amount}. Current: {currentBlood}/{maxBlood}");
        }

        onBloodChanged?.Invoke(GetBloodPercent());

        if (currentBlood <= 0)
        {
            Death();
        }
    }

    public void GainBlood(float amount)
    {
        currentBlood += amount;
        currentBlood = Mathf.Min(currentBlood, maxBlood);

        if (showDebugLogs)
        {
            Debug.Log($"Blood gained: {amount}. Current: {currentBlood}/{maxBlood}");
        }

        onBloodChanged?.Invoke(GetBloodPercent());
    }

    // Called when player attacks
    public void OnAttack(bool isHeavy)
    {
        float cost = isHeavy ? heavyAttackCost : lightAttackCost;
        DrainBlood(cost);
    }

    // Desperation ability (self-harm)
    public void UseDesperation()
    {
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

        // Reduce max capacity
        maxBlood -= desperationMaxCapacityLoss;
        maxBlood = Mathf.Max(maxBlood, 10f); // Never go below 10%

        // Gain blood
        GainBlood(desperationGain);
        currentBlood = Mathf.Min(currentBlood, maxBlood); // Cap at new max

        desperationUsesThisLife++;

        if (showDebugLogs)
        {
            Debug.Log($"Desperation used! Gained {desperationGain} blood. Max capacity now: {maxBlood}");
        }

        onDesperationUsed?.Invoke();
    }

    // Called at checkpoints
    public void ResetMaxCapacity()
    {
        maxBlood = baseMaxBlood;
        desperationUsesThisLife = 0;

        if (showDebugLogs)
        {
            Debug.Log("Max blood capacity reset to 100%");
        }
    }

    void Death()
    {
        if (showDebugLogs)
        {
            Debug.Log("Blood depleted - Death!");
        }

        onBloodDepleted?.Invoke();
    }

    void ApplyWithdrawalEffects()
    {
        float bloodPercent = GetBloodPercent();

        // Get withdrawal state
        WithdrawalState state = GetWithdrawalState();

        // Apply visual effects based on state
        // Note: Actual post-processing implementation depends on your setup
        // This is a placeholder for vignette/darkening effects

        switch (state)
        {
            case WithdrawalState.Optimal:
                // No effects
                break;
            case WithdrawalState.Mild:
                // Slight screen darkening (10% reduction)
                break;
            case WithdrawalState.Moderate:
                // Noticeable darkening (25% reduction), tunnel vision begins
                break;
            case WithdrawalState.Severe:
                // Heavy darkening (40% reduction), severe tunnel vision
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

    // Returns attack speed modifier based on withdrawal
    public float GetAttackSpeedModifier()
    {
        switch (GetWithdrawalState())
        {
            case WithdrawalState.Optimal: return 1.0f;
            case WithdrawalState.Mild: return 0.9f; // 10% reduction
            case WithdrawalState.Moderate: return 0.75f; // 25% reduction
            case WithdrawalState.Severe: return 0.6f; // 40% reduction
            default: return 1.0f;
        }
    }

    // Returns damage modifier based on withdrawal
    public float GetDamageModifier()
    {
        switch (GetWithdrawalState())
        {
            case WithdrawalState.Optimal: return 1.0f;
            case WithdrawalState.Mild: return 0.9f; // 10% reduction
            case WithdrawalState.Moderate: return 0.75f; // 25% reduction
            case WithdrawalState.Severe: return 0.6f; // 40% reduction
            default: return 1.0f;
        }
    }

    void UpdateUI()
    {
        if (bloodSlider != null)
        {
            bloodSlider.maxValue = baseMaxBlood; // Always show against base max
            bloodSlider.value = currentBlood;
        }

        if (bloodFillImage != null)
        {
            // Color lerp based on blood level
            bloodFillImage.color = Color.Lerp(lowBloodColor, highBloodColor, GetBloodPercent() / 100f);
        }
    }

    // Public getters
    public float GetBloodPercent() => (currentBlood / maxBlood) * 100f;
    public float GetCurrentBlood() => currentBlood;
    public float GetMaxBlood() => maxBlood;
    public bool CanUseDesperation() => desperationUnlocked && currentBlood <= desperationMinBlood;
}

public enum WithdrawalState
{
    Optimal,    // 75-100%
    Mild,       // 50-74%
    Moderate,   // 25-49%
    Severe      // 0-24%
}