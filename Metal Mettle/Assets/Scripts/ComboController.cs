using UnityEngine;
using UnityEngine.InputSystem;

public class ComboController : MonoBehaviour
{
    [Header("References")]
    public BoxCollider attackHitbox;
    public MeshRenderer debugRenderer;
    public BloodSystem bloodSystem;
    public Animator animator;
    private InputSystem_Actions controls;
    private AttackCollider attackCollider;

    [Header("Combo Settings")]
    public int maxLightCombo = 3;
    public int maxHeavyCombo = 2;
    public float heavyHoldTime = 0.3f;
    public bool allowMixedCombos = false;

    [Header("Timing")]
    public float attackDuration = 0.3f;
    public float attackCooldown = 0.5f;
    public float comboWindow = 10f;
    public float comboEndCooldown = 0.5f;
    public float perfectTimingWindow = 0.3f;
    public float inputBufferTime = 0.2f;

    [Header("Recovery")]
    public float lightRecovery = 0.2f;
    public float heavyRecovery = 0.4f;

    [Header("Block")]
    public bool blockEnabled = true;
    public GameObject blockVisual;
    public float blockStartupTime = 0.15f;
    public float blockRecovery = 0.25f;

    [Header("Animation State Names")]
    [Tooltip("Exact names of light attack states in animator")]
    public string[] lightAttackStates = { "JAB", "CROSS", "LEAD UPPERCUT" };

    [Tooltip("Exact names of heavy attack states in animator")]
    public string[] heavyAttackStates = { "RIGHT STRAIT KNEE", "ROUNDHOUSE KICK" };

    [Tooltip("How long to blend between animations (seconds)")]
    [Range(0f, 0.5f)]
    public float crossfadeDuration = 0.1f;

    [Header("Animation")]
    public bool useAnimationEvents = true;
    public bool showDebugHitbox = true;
    public Color[] lightColors = { Color.white, Color.yellow, Color.red };
    public Color[] heavyColors = { Color.cyan, Color.magenta };

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private int comboStep = 0;
    private bool isHeavyCombo = false;
    private float lastAttackTime;
    private bool isAttacking = false;
    private bool isBlocking = false;
    private bool inRecovery = false;
    private bool blockActive = false;
    private bool blockStarting = false;
    private bool waitingForAnimationComplete = false;
    private bool canAttack = true;
    private bool buttonPressed = false;
    private float buttonHoldTime = 0f;
    private bool heavyAttackTriggered = false;
    private bool hasBufferedInput = false;
    private bool bufferedIsHeavy = false;
    private float bufferedInputTime = 0f;
    private int bufferedExpectedComboStep = 0;

    private bool currentAttackIsHeavy = false;
    private int currentAttackStep = 0;
    private float nextAttackAllowedTime = 0f;
    private bool isProcessingAttack = false;

    void Start()
    {
        controls = InputManager.Instance.Controls;
        controls.Enable();

        if (attackHitbox) attackHitbox.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;
        if (blockVisual) blockVisual.SetActive(false);

        if (attackHitbox != null)
        {
            attackCollider = attackHitbox.GetComponent<AttackCollider>();
        }

        if (animator == null)
        {
            Debug.LogError("ComboController: No Animator assigned!");
        }
        else
        {
            DebugLog("ComboController initialized");
        }
    }

    void Update()
    {
        bool attackHeld = controls.Player.Attack.IsPressed();
        bool absorbHeld = controls.Player.Absorb.IsPressed();

        // Handle blocking
        if (blockEnabled && attackHeld && absorbHeld && !isAttacking && !inRecovery)
        {
            if (!isBlocking && !blockStarting)
            {
                StartBlocking();
            }
        }
        else
        {
            if (isBlocking || blockStarting)
            {
                StopBlocking();
            }
        }

        if (isBlocking) return;

        bool isLockedOut = Time.time < nextAttackAllowedTime;

        // Process buffered input
        if (hasBufferedInput && canAttack && !inRecovery && !isLockedOut && !isProcessingAttack)
        {
            float timeSinceBuffer = Time.time - bufferedInputTime;
            if (timeSinceBuffer <= inputBufferTime)
            {
                if (bufferedExpectedComboStep == comboStep)
                {
                    DebugLog($"⚡ PROCESSING BUFFERED INPUT!");
                    hasBufferedInput = false;
                    TriggerAttack(bufferedIsHeavy);
                }
                else
                {
                    DebugLog($"🗑️ Discarding stale buffer (expected step {bufferedExpectedComboStep}, now {comboStep})");
                    hasBufferedInput = false;
                }
                return;
            }
            else
            {
                DebugLog($"⏱️ Buffer expired ({timeSinceBuffer:F2}s > {inputBufferTime})");
                hasBufferedInput = false;
            }
        }

        // Button press detection
        if (controls.Player.Attack.WasPressedThisFrame())
        {
            if (hasBufferedInput)
            {
                DebugLog("🗑️ Clearing old buffer on new press");
                hasBufferedInput = false;
            }

            buttonPressed = true;
            buttonHoldTime = 0f;
            heavyAttackTriggered = false;
            DebugLog($">>> BUTTON PRESSED <<< Step:{comboStep}, CanAttack:{canAttack}, Recovery:{inRecovery}, LockedOut:{isLockedOut}");
        }

        // Check for heavy attack trigger during hold
        if (buttonPressed && controls.Player.Attack.IsPressed())
        {
            buttonHoldTime += Time.deltaTime;

            if (buttonHoldTime >= heavyHoldTime && !heavyAttackTriggered)
            {
                DebugLog($"🔨 Heavy attack triggered (held {buttonHoldTime:F2}s)");
                heavyAttackTriggered = true;
                buttonPressed = false;
                buttonHoldTime = 0f;

                if ((canAttack && !isLockedOut && !isProcessingAttack) || (comboStep > 0 && !inRecovery && !isProcessingAttack))
                {
                    TriggerAttack(true);
                }
                else
                {
                    DebugLog($"📦 Buffering heavy (CanAttack:{canAttack}, LockedOut:{isLockedOut}, Step:{comboStep}, Recovery:{inRecovery})");
                    hasBufferedInput = true;
                    bufferedIsHeavy = true;
                    bufferedInputTime = Time.time;
                    bufferedExpectedComboStep = comboStep;
                }
            }
        }

        // Button release - trigger light attack
        if (controls.Player.Attack.WasReleasedThisFrame())
        {
            DebugLog($">>> RELEASED <<< HeavyTriggered:{heavyAttackTriggered}, HoldTime:{buttonHoldTime:F2}s");

            if (heavyAttackTriggered)
            {
                heavyAttackTriggered = false;
                buttonPressed = false;
                buttonHoldTime = 0f;
                return;
            }

            if (buttonPressed)
            {
                buttonPressed = false;

                if (buttonHoldTime < heavyHoldTime)
                {
                    if ((canAttack && !isLockedOut && !isProcessingAttack) || (comboStep > 0 && !inRecovery && !isProcessingAttack))
                    {
                        TriggerAttack(false);
                    }
                    else
                    {
                        DebugLog($"📦 Buffering light (CanAttack:{canAttack}, LockedOut:{isLockedOut}, Step:{comboStep}, Recovery:{inRecovery})");
                        hasBufferedInput = true;
                        bufferedIsHeavy = false;
                        bufferedInputTime = Time.time;
                        bufferedExpectedComboStep = comboStep;
                    }
                }
            }
        }

        bool safeToTimeout = !isAttacking && !hasBufferedInput && !isProcessingAttack && !waitingForAnimationComplete;

        if (Time.time - lastAttackTime > comboWindow && comboStep > 0 && safeToTimeout)
        {
            DebugLog($"⏱️ Combo window expired ({Time.time - lastAttackTime:F2}s > {comboWindow})");
            ResetCombo();
        }
    }

    void TriggerAttack(bool isHeavy)
    {
        if (isProcessingAttack)
        {
            DebugLog($"⚠️ Already processing an attack, ignoring trigger");
            return;
        }

        isProcessingAttack = true;

        DebugLog($"=== TriggerAttack === Heavy:{isHeavy}, Step:{comboStep}, IsHeavyCombo:{isHeavyCombo}, TimeSinceLastAttack:{Time.time - lastAttackTime:F2}s");

        if (Time.time < nextAttackAllowedTime)
        {
            DebugLog($"❌ Attack locked out for {(nextAttackAllowedTime - Time.time):F2}s more");
            isProcessingAttack = false;
            return;
        }

        if (!canAttack && comboStep == 0)
        {
            DebugLog("❌ Cannot attack - not ready for first attack");
            isProcessingAttack = false;
            return;
        }

        if (comboStep == 0 && inRecovery)
        {
            DebugLog("❌ Cannot attack - in recovery");
            isProcessingAttack = false;
            return;
        }

        // Check blood cost
        if (bloodSystem != null)
        {
            float bloodCost = isHeavy ? 3f : 2f;
            if (bloodSystem.currentBlood < bloodCost)
            {
                DebugLog($"❌ Not enough blood! Need {bloodCost}, have {bloodSystem.currentBlood}");
                isProcessingAttack = false;
                return;
            }
        }

        // Store whether this was a heavy combo BEFORE we potentially change it
        bool wasHeavyCombo = isHeavyCombo;

        if (comboStep == 0)
        {
            // Starting a new combo - set the type
            isHeavyCombo = isHeavy;
            DebugLog($"🆕 Starting {(isHeavy ? "HEAVY" : "LIGHT")} combo");
        }
        else
        {
            // Continuing a combo
            bool tryingToMix = isHeavy != isHeavyCombo;

            DebugLog($"   Continuing combo: isHeavy={isHeavy}, wasHeavyCombo={wasHeavyCombo}, tryingToMix={tryingToMix}");

            if (tryingToMix)
            {
                // Player is trying to switch attack types mid-combo
                if (!allowMixedCombos)
                {
                    DebugLog("❌ Cannot mix attack types (allowMixedCombos is false)");
                    isProcessingAttack = false;
                    return;
                }
                else if (isHeavy && !isHeavyCombo)
                {
                    // Light combo → Heavy finisher (ALLOWED)
                    DebugLog($"💥 Heavy finisher! Switching from light to heavy");
                    // DON'T SET isHeavyCombo = true yet, we need the old value for max combo calculation
                }
                else if (!isHeavy && isHeavyCombo)
                {
                    // Heavy → Light (NOT ALLOWED)
                    DebugLog("❌ Cannot go heavy to light");
                    isProcessingAttack = false;
                    return;
                }
            }
            // If NOT trying to mix (same type as combo), just continue - no extra checks needed
        }

        int previousComboStep = comboStep;
        comboStep++;

        // Determine max combo based on ORIGINAL combo type (BEFORE we changed isHeavyCombo)
        int maxCombo;

        // If we just switched to heavy (mixed combo finisher)
        // Use wasHeavyCombo instead of isHeavyCombo to check the original state
        bool justSwitchedToHeavy = isHeavy && !wasHeavyCombo && allowMixedCombos && previousComboStep > 0;

        DebugLog($"   Max combo calculation: justSwitchedToHeavy={justSwitchedToHeavy}, wasHeavyCombo={wasHeavyCombo}, step={comboStep}");

        if (justSwitchedToHeavy)
        {
            // Light→Light→Heavy: use light max since we started as light combo
            maxCombo = maxLightCombo;
            DebugLog($"   💥 Heavy finisher on light combo - using light max ({maxLightCombo})");
            // NOW we can set isHeavyCombo for the animation system
            isHeavyCombo = true;
        }
        else if (wasHeavyCombo)
        {
            // Pure heavy combo (or was already heavy)
            maxCombo = maxHeavyCombo;
            DebugLog($"   Using heavy max ({maxHeavyCombo})");
        }
        else
        {
            // Pure light combo
            maxCombo = maxLightCombo;
            DebugLog($"   Using light max ({maxLightCombo})");
        }

        if (comboStep > maxCombo)
        {
            DebugLog($"🎯 Combo exceeded max ({comboStep} > {maxCombo})! Ending combo.");
            comboStep = maxCombo;
            isProcessingAttack = false;
            EndCombo();
            return;
        }

        DebugLog($"⚔️ Attack {comboStep}/{maxCombo} - IsHeavyCombo:{isHeavyCombo} (was step {previousComboStep})");

        float speedModifier = bloodSystem != null ? bloodSystem.GetAttackSpeedModifier() : 1f;
        float adjustedCooldown = attackCooldown / speedModifier;
        float adjustedRecovery = (isHeavy ? heavyRecovery : lightRecovery) / speedModifier;

        isAttacking = true;
        waitingForAnimationComplete = true;
        canAttack = false;
        lastAttackTime = Time.time;

        currentAttackIsHeavy = isHeavyCombo;
        currentAttackStep = comboStep;

        DebugLog($"   STATE SET: isAttacking={isAttacking}, lastAttackTime={lastAttackTime}, step={comboStep}");

        // Play specific animation state with smooth crossfade
        if (animator != null)
        {
            string stateName = GetAttackStateName(isHeavyCombo, comboStep);

            if (!string.IsNullOrEmpty(stateName))
            {
                // Crossfade to the state instead of instant play
                animator.CrossFade(stateName, crossfadeDuration, 0, 0f);
                animator.SetFloat("AttackSpeed", speedModifier);
                animator.speed = speedModifier;

                DebugLog($"   🎬 CROSSFADING TO: {stateName} over {crossfadeDuration}s (Speed={speedModifier})");
            }
            else
            {
                Debug.LogError($"❌ No animation state name for combo step {comboStep}!");
            }
        }

        if (!useAnimationEvents)
        {
            ActivateHitbox();
            Invoke(nameof(DeactivateHitbox), attackDuration);
        }
        else
        {
            Invoke(nameof(ForceDeactivateHitbox), 2f);
        }

        float comboInputWindow = Mathf.Max(0.2f, adjustedCooldown * 0.4f);
        Invoke(nameof(EnableNextAttack), comboInputWindow);

        inRecovery = true;
        float totalRecovery = adjustedCooldown + adjustedRecovery;
        Invoke(nameof(EndRecovery), totalRecovery);

        Invoke(nameof(ClearProcessingFlag), 0.1f);
    }

    /// <summary>
    /// Gets the exact animator state name for the given combo step
    /// </summary>
    string GetAttackStateName(bool isHeavy, int step)
    {
        if (isHeavy)
        {
            int index = step - 1;
            // Clamp to available animations - for heavy finisher on step 3, use last heavy animation
            index = Mathf.Min(index, heavyAttackStates.Length - 1);
            if (index >= 0 && index < heavyAttackStates.Length)
            {
                DebugLog($"   Getting heavy animation: index={index}, state={heavyAttackStates[index]}");
                return heavyAttackStates[index];
            }
        }
        else
        {
            int index = step - 1;
            if (index >= 0 && index < lightAttackStates.Length)
            {
                DebugLog($"   Getting light animation: index={index}, state={lightAttackStates[index]}");
                return lightAttackStates[index];
            }
        }

        Debug.LogError($"❌ No animation found for isHeavy={isHeavy}, step={step}!");
        return null;
    }

    void ClearProcessingFlag()
    {
        isProcessingAttack = false;
        DebugLog($"   Processing flag cleared");
    }

    public void ActivateHitbox()
    {
        DebugLog($"🗡️ HITBOX ON - STORED: Step:{currentAttackStep}, IsHeavy:{currentAttackIsHeavy}");

        if (currentAttackStep <= 0 || currentAttackStep > maxLightCombo)
        {
            Debug.LogWarning($"⚠️ Invalid stored attack step: {currentAttackStep}!");
            currentAttackStep = comboStep;
            currentAttackIsHeavy = isHeavyCombo;
        }

        if (bloodSystem != null)
        {
            float bloodCost = currentAttackIsHeavy ? 3f : 2f;
            bloodSystem.OnAttack(currentAttackIsHeavy);
            DebugLog($"   Drained {bloodCost} blood on hitbox activation");
        }

        if (attackCollider != null)
        {
            attackCollider.ClearHitList();
        }

        if (attackHitbox) attackHitbox.enabled = true;

        if (debugRenderer && showDebugHitbox)
        {
            debugRenderer.enabled = true;
            Color[] colors = currentAttackIsHeavy ? heavyColors : lightColors;
            int colorIndex = Mathf.Clamp(currentAttackStep - 1, 0, colors.Length - 1);
            Color selectedColor = colors[colorIndex];
            debugRenderer.material.color = selectedColor;
        }
    }

    public void DeactivateHitbox()
    {
        DebugLog($"🛡️ HITBOX OFF - Step:{comboStep}");

        if (attackHitbox) attackHitbox.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;
        isAttacking = false;
        waitingForAnimationComplete = false;
        CancelInvoke(nameof(ForceDeactivateHitbox));
    }

    void ForceDeactivateHitbox()
    {
        Debug.LogWarning("⚠️ Force deactivating hitbox!");
        DeactivateHitbox();
    }

    void EnableNextAttack()
    {
        canAttack = true;
        DebugLog($"✓ Window OPEN (Step:{comboStep})");
    }

    void EndRecovery()
    {
        inRecovery = false;
        DebugLog($"✓ Recovery ended (Step:{comboStep})");
    }

    void ResetCombo()
    {
        if (isAttacking || isProcessingAttack || waitingForAnimationComplete)
        {
            DebugLog($"⚠️ Preventing reset - state locked (attacking:{isAttacking}, processing:{isProcessingAttack}, waiting:{waitingForAnimationComplete})");
            return;
        }

        DebugLog($"🔄 RESET (was step {comboStep}, {(isHeavyCombo ? "HEAVY" : "LIGHT")})");

        comboStep = 0;
        isHeavyCombo = false;
        hasBufferedInput = false;
        heavyAttackTriggered = false;
        buttonPressed = false;
        isAttacking = false;
        canAttack = true;
        currentAttackStep = 0;
        currentAttackIsHeavy = false;
        isProcessingAttack = false;

        nextAttackAllowedTime = 0f;
    }

    void EndCombo()
    {
        if (isAttacking || isProcessingAttack)
        {
            DebugLog($"⚠️ Delaying EndCombo - still attacking or processing!");
            Invoke(nameof(EndCombo), 0.2f);
            return;
        }

        DebugLog($"🏁 Combo Ended (step {comboStep})");

        comboStep = 0;
        isHeavyCombo = false;
        isAttacking = false;
        waitingForAnimationComplete = false;
        hasBufferedInput = false;
        heavyAttackTriggered = false;
        buttonPressed = false;
        canAttack = false;
        currentAttackStep = 0;
        currentAttackIsHeavy = false;
        isProcessingAttack = false;

        if (attackHitbox) attackHitbox.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;

        nextAttackAllowedTime = Time.time + comboEndCooldown;
        canAttack = true;

        DebugLog($"🔒 Combo end cooldown: {comboEndCooldown}s");
    }

    void StartBlocking()
    {
        if (comboStep > 0)
        {
            DebugLog("Blocking - resetting combo");
            ResetCombo();
        }

        blockStarting = true;
        isBlocking = true;
        Invoke(nameof(ActivateBlock), blockStartupTime);

        if (blockVisual != null) blockVisual.SetActive(true);
        if (animator != null) animator.SetBool("IsBlocking", true);
    }

    void ActivateBlock()
    {
        blockActive = true;
    }

    void StopBlocking()
    {
        blockStarting = false;
        blockActive = false;
        CancelInvoke(nameof(ActivateBlock));

        inRecovery = true;
        Invoke(nameof(EndRecovery), blockRecovery);

        isBlocking = false;
        if (blockVisual != null) blockVisual.SetActive(false);
        if (animator != null) animator.SetBool("IsBlocking", false);
    }

    void OnDisable()
    {
        controls?.Disable();
    }

    void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Combo] {message}");
        }
    }

    public int GetComboStep() => comboStep;
    public bool IsHeavyCombo() => isHeavyCombo;
    public bool IsBlocking() => isBlocking;
    public bool IsBlockActive() => blockActive;
    public bool IsInRecovery() => inRecovery;
    public bool IsAttacking() => isAttacking;

    public void ForceResetCombo()
    {
        DebugLog("🔄 FORCED RESET (external call)");
        ResetCombo();
    }

    void OnGUI()
    {
        if (!enableDebugLogs) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        GUILayout.Label($"Combo: {comboStep} ({(isHeavyCombo ? "HEAVY" : "LIGHT")})");

        // Show current state name
        string currentState = GetAttackStateName(isHeavyCombo, comboStep);
        if (!string.IsNullOrEmpty(currentState))
        {
            GUI.color = Color.yellow;
            GUILayout.Label($"State: {currentState}");
            GUI.color = Color.white;
        }

        GUILayout.Label($"Can Attack: {canAttack}");
        GUILayout.Label($"Attacking: {isAttacking}");
        GUILayout.Label($"Processing: {isProcessingAttack}");
        GUILayout.Label($"Waiting Anim: {waitingForAnimationComplete}");
        GUILayout.Label($"Recovery: {inRecovery}");
        GUILayout.Label($"Time Since Last: {Time.time - lastAttackTime:F2}s / {comboWindow}s");

        float lockoutRemaining = Mathf.Max(0, nextAttackAllowedTime - Time.time);
        if (lockoutRemaining > 0)
        {
            GUI.color = Color.red;
            GUILayout.Label($"🔒 LOCKED: {lockoutRemaining:F2}s");
            GUI.color = Color.white;
        }

        if (hasBufferedInput)
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"BUFFERED: {(bufferedIsHeavy ? "H" : "L")} (Step {bufferedExpectedComboStep})");
            GUI.color = Color.white;
        }

        GUILayout.EndArea();
    }
}

/*
=== THE CRITICAL FIX - Lines 271-345 ===

THE BUG:
When doing Light→Light→Heavy, the code was setting isHeavyCombo = true
BEFORE checking justSwitchedToHeavy, so the check (!isHeavyCombo) was always false.

THE FIX:
1. Store original value: bool wasHeavyCombo = isHeavyCombo
2. Don't set isHeavyCombo = true when detecting heavy finisher (yet)
3. Use wasHeavyCombo to calculate justSwitchedToHeavy
4. Set isHeavyCombo = true AFTER determining max combo

NOW IT WORKS:
- Light (step 1): wasHeavyCombo = false
- Light (step 2): wasHeavyCombo = false  
- Heavy (step 3): isHeavy=true, wasHeavyCombo=false → justSwitchedToHeavy=true
  → Uses maxLightCombo (3) → ALLOWED!

=== WHAT YOU'LL SEE IN DEBUG ===

When doing Tap→Tap→Hold:
[Combo] === TriggerAttack === Heavy:true, Step:2, IsHeavyCombo:false
[Combo]    Continuing combo: isHeavy=true, wasHeavyCombo=false, tryingToMix=true
[Combo] 💥 Heavy finisher! Switching from light to heavy
[Combo]    Max combo calculation: justSwitchedToHeavy=true, wasHeavyCombo=false, step=3
[Combo]    💥 Heavy finisher on light combo - using light max (3)
[Combo] ⚔️ Attack 3/3 - IsHeavyCombo:true

=== INSPECTOR SETTINGS ===
- allowMixedCombos = TRUE (must be enabled!)
- maxLightCombo = 3
- maxHeavyCombo = 2
- heavyHoldTime = 0.3

*/