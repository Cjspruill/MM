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

    [Header("Recovery - Synced to Animator")]
    [Tooltip("Uses attack animation clip length for recovery timing")]
    public bool syncRecoveryToAnimator = true;

    [Tooltip("Use recovery time as the transition blend duration back to locomotion")]
    public bool useRecoveryAsTransition = true;

    [Tooltip("Fallback if animator sync fails")]
    public float lightRecoveryFallback = 0.2f;
    public float heavyRecoveryFallback = 0.4f;

    [Header("Block")]
    public bool blockEnabled = true;
    public GameObject blockVisual;
    public float blockStartupTime = 0.15f;
    public float blockRecovery = 0.25f;

    [Header("Animation State Names")]
    [Tooltip("Exact names of light attack states in animator")]
    public string[] lightAttackStates = { "JAB", "CROSS", "LEAD UPPERCUT" };

    [Tooltip("Recovery time for each light attack (must match array length)")]
    public float[] lightAttackRecovery = { 0.45f, 0.40f, 0.55f };

    [Tooltip("Exact names of heavy attack states in animator")]
    public string[] heavyAttackStates = { "RIGHT STRAIT KNEE", "ROUNDHOUSE KICK" };

    [Tooltip("Recovery time for each heavy attack (must match array length)")]
    public float[] heavyAttackRecovery = { 0.60f, 0.70f };

    [Header("Animation Blending")]
    [Tooltip("Blend time when starting first attack")]
    public float firstAttackBlend = 0.1f;

    [Tooltip("Blend time for combo attacks (0 = instant)")]
    public float comboAttackBlend = 0.05f;

    [Tooltip("Force instant transitions for combos (ignores blend time)")]
    public bool forceInstantCombos = true;

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
    private bool successfulBlockFlag = false;

    private string lastLightAttack = "";
    private string lastHeavyAttack = "";
    private string lastPlayedState = "";

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
            DebugLog($"📊 Recovery Settings:");
            DebugLog($"   - syncRecoveryToAnimator: {syncRecoveryToAnimator}");
            DebugLog($"   - lightRecoveryFallback: {lightRecoveryFallback}");
            DebugLog($"   - heavyRecoveryFallback: {heavyRecoveryFallback}");
            DebugLog($"   - attackCooldown: {attackCooldown}");
            DebugLog($"   - Light attacks: {lightAttackStates.Length} states, {lightAttackRecovery.Length} recovery times");
            DebugLog($"   - Heavy attacks: {heavyAttackStates.Length} states, {heavyAttackRecovery.Length} recovery times");
        }
    }

    void Update()
    {
        // Animation state monitoring for debugging
        if (animator != null && enableDebugLogs)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorTransitionInfo transitionInfo = animator.GetAnimatorTransitionInfo(0);

            if (transitionInfo.fullPathHash != 0) // In transition
            {
                DebugLog($"🎬 TRANSITIONING: Progress {transitionInfo.normalizedTime:F2} | Duration: {transitionInfo.duration:F2}s");
            }
        }

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

        isAttacking = true;
        waitingForAnimationComplete = true;
        canAttack = false;
        lastAttackTime = Time.time;

        currentAttackIsHeavy = isHeavyCombo;
        currentAttackStep = comboStep;

        DebugLog($"   STATE SET: isAttacking={isAttacking}, lastAttackTime={lastAttackTime}, step={comboStep}");

        // Get the animation state name
        string stateName = GetAttackStateName(isHeavyCombo, comboStep);
        lastPlayedState = stateName; // Store for later use in deactivate

        // Play specific animation state with smart blending
        if (animator != null && !string.IsNullOrEmpty(stateName))
        {
            // Set attacking flag for animator
            animator.SetBool("IsAttacking", true);

            // First attack blends smoothly, combos are snappier
            float blendTime = (comboStep == 1) ? firstAttackBlend : comboAttackBlend;

            // Force instant for combos if enabled
            if (comboStep > 1 && forceInstantCombos)
            {
                // Instant transition - no blend at all
                animator.Play(stateName, 0, 0f);
                DebugLog($"   🎬 INSTANT PLAY: {stateName} (Speed={speedModifier})");
            }
            else
            {
                // Crossfade for first attack or if blend time is set
                animator.CrossFade(stateName, blendTime, 0, 0f);
                DebugLog($"   🎬 CROSSFADING TO: {stateName} over {blendTime}s (Speed={speedModifier})");
            }

            animator.SetFloat("AttackSpeed", speedModifier);
            animator.speed = speedModifier;
        }
        else
        {
            Debug.LogError($"❌ No animation state name for combo step {comboStep}!");
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
        DebugLog($"   ⏰ Combo window opens in: {comboInputWindow:F2}s (at time {Time.time + comboInputWindow:F2})");
        Invoke(nameof(EnableNextAttack), comboInputWindow);

        inRecovery = true;

        // Get recovery time from animator transition or use fallback
        float recoveryTime = GetRecoveryTimeFromAnimator(isHeavy, stateName);
        float totalRecovery = adjustedCooldown + recoveryTime;

        DebugLog($"   💊 Recovery Breakdown:");
        DebugLog($"      - Base Recovery: {recoveryTime:F2}s");
        DebugLog($"      - Adjusted Cooldown: {adjustedCooldown:F2}s");
        DebugLog($"      - Total Recovery: {totalRecovery:F2}s");
        DebugLog($"      - Will end at time: {Time.time + totalRecovery:F2}");

        Invoke(nameof(EndRecovery), totalRecovery);

        Invoke(nameof(ClearProcessingFlag), 0.1f);
    }

    /// <summary>
    /// Gets recovery time - tries animator sync, then manual array, then fallback
    /// </summary>
    float GetRecoveryTimeFromAnimator(bool isHeavy, string currentStateName)
    {
        if (!syncRecoveryToAnimator || animator == null)
        {
            // Try to get from manual arrays first
            float manualRecovery = GetManualRecoveryTime(isHeavy, currentStateName);
            if (manualRecovery > 0)
            {
                DebugLog($"   📋 Using manual recovery: {manualRecovery:F2}s");
                return manualRecovery;
            }

            float fallback = isHeavy ? heavyRecoveryFallback : lightRecoveryFallback;
            DebugLog($"   ⚠️ Sync disabled - using fallback: {fallback:F2}s");
            return fallback;
        }

        // Try to get clip from animator
        AnimationClip clip = GetAnimationClip(currentStateName);

        if (clip != null)
        {
            float clipLength = clip.length;
            DebugLog($"   ✓ Found clip '{clip.name}' - Using length: {clipLength:F2}s");
            return clipLength;
        }

        // Try manual arrays as backup
        float manualTime = GetManualRecoveryTime(isHeavy, currentStateName);
        if (manualTime > 0)
        {
            DebugLog($"   📋 Clip not found, using manual recovery: {manualTime:F2}s");
            return manualTime;
        }

        // Final fallback
        float fallbackValue = isHeavy ? heavyRecoveryFallback : lightRecoveryFallback;
        DebugLog($"   ❌ No recovery found - using fallback: {fallbackValue:F2}s");
        return fallbackValue;
    }

    /// <summary>
    /// Gets manual recovery time from the arrays if they're set up correctly
    /// </summary>
    float GetManualRecoveryTime(bool isHeavy, string stateName)
    {
        if (isHeavy)
        {
            if (heavyAttackRecovery != null && heavyAttackRecovery.Length == heavyAttackStates.Length)
            {
                for (int i = 0; i < heavyAttackStates.Length; i++)
                {
                    if (heavyAttackStates[i] == stateName)
                    {
                        return heavyAttackRecovery[i];
                    }
                }
            }
        }
        else
        {
            if (lightAttackRecovery != null && lightAttackRecovery.Length == lightAttackStates.Length)
            {
                for (int i = 0; i < lightAttackStates.Length; i++)
                {
                    if (lightAttackStates[i] == stateName)
                    {
                        return lightAttackRecovery[i];
                    }
                }
            }
        }

        return 0f; // Not found
    }

    /// <summary>
    /// Gets the AnimationClip for a given state name
    /// Searches through all animator clips with detailed logging
    /// </summary>
    AnimationClip GetAnimationClip(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            DebugLog($"   ❌ Animator or state name is null");
            return null;
        }

        // Get all clips from the animator
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        DebugLog($"   🔍 Searching for '{stateName}' in {clips.Length} clips...");

        // Try exact match first
        foreach (var clip in clips)
        {
            if (clip.name == stateName)
            {
                DebugLog($"   ✓ EXACT MATCH: '{clip.name}' = {clip.length:F2}s");
                return clip;
            }
        }

        // Try partial match (case-insensitive)
        foreach (var clip in clips)
        {
            if (clip.name.ToUpper().Contains(stateName.ToUpper()))
            {
                DebugLog($"   ⚡ PARTIAL MATCH: '{clip.name}' contains '{stateName}' = {clip.length:F2}s");
                return clip;
            }
        }

        // List all available clips for debugging
        DebugLog($"   ❌ No match found. Available clips:");
        foreach (var clip in clips)
        {
            DebugLog($"      - '{clip.name}' ({clip.length:F2}s)");
        }

        return null;
    }

    /// <summary>
    /// Gets a random animator state name for the given combo step
    /// Both light and heavy attacks are randomly selected from available animations
    /// Avoids repeating the same attack twice in a row
    /// </summary>
    string GetAttackStateName(bool isHeavy, int step)
    {
        if (isHeavy)
        {
            // Heavy attacks are RANDOMIZED - pick any available heavy attack
            if (heavyAttackStates.Length > 0)
            {
                string selectedAttack;

                if (heavyAttackStates.Length == 1)
                {
                    // Only one option
                    selectedAttack = heavyAttackStates[0];
                }
                else if (step == 1 || string.IsNullOrEmpty(lastHeavyAttack))
                {
                    // First attack - any is fine
                    int randomIndex = Random.Range(0, heavyAttackStates.Length);
                    selectedAttack = heavyAttackStates[randomIndex];
                }
                else
                {
                    // Not first attack - avoid repeating last attack
                    int attempts = 0;
                    do
                    {
                        int randomIndex = Random.Range(0, heavyAttackStates.Length);
                        selectedAttack = heavyAttackStates[randomIndex];
                        attempts++;
                    } while (selectedAttack == lastHeavyAttack && attempts < 10);
                }

                lastHeavyAttack = selectedAttack;
                DebugLog($"   🎲 Random heavy animation: step={step}, state={selectedAttack}");
                return selectedAttack;
            }
        }
        else
        {
            // Light attacks are RANDOMIZED - pick any available light attack
            if (lightAttackStates.Length > 0)
            {
                string selectedAttack;

                if (lightAttackStates.Length == 1)
                {
                    // Only one option
                    selectedAttack = lightAttackStates[0];
                }
                else if (step == 1 || string.IsNullOrEmpty(lastLightAttack))
                {
                    // First attack - any is fine
                    int randomIndex = Random.Range(0, lightAttackStates.Length);
                    selectedAttack = lightAttackStates[randomIndex];
                }
                else
                {
                    // Not first attack - avoid repeating last attack
                    int attempts = 0;
                    do
                    {
                        int randomIndex = Random.Range(0, lightAttackStates.Length);
                        selectedAttack = lightAttackStates[randomIndex];
                        attempts++;
                    } while (selectedAttack == lastLightAttack && attempts < 10);
                }

                lastLightAttack = selectedAttack;
                DebugLog($"   🎲 Random light animation: step={step}, state={selectedAttack}");
                return selectedAttack;
            }
        }

        Debug.LogError($"❌ No animation found for isHeavy={isHeavy}, step={step}!");
        return null;
    }

    /// <summary>
    /// Gets the last played state name for recovery calculations
    /// </summary>
    string GetLastPlayedStateName()
    {
        return lastPlayedState;
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

        // Get recovery time to use as transition duration
        if (useRecoveryAsTransition && animator != null)
        {
            float recoveryTime = GetRecoveryTimeFromAnimator(currentAttackIsHeavy, GetLastPlayedStateName());

            // Transition back to locomotion with recovery time as blend duration
            animator.CrossFade("Locomotion", recoveryTime, 0);
            DebugLog($"   🔄 Transitioning to Locomotion over {recoveryTime:F2}s");

            // Also set IsAttacking to false for safety
            animator.SetBool("IsAttacking", false);
        }
        else if (animator != null)
        {
            // Standard immediate transition
            animator.SetBool("IsAttacking", false);
            DebugLog($"   ✓ Set IsAttacking = false");
        }
    }

    void ForceDeactivateHitbox()
    {
        Debug.LogWarning("⚠️ Force deactivating hitbox!");
        DeactivateHitbox();
    }

    void EnableNextAttack()
    {
        canAttack = true;
        DebugLog($"✓ Combo window OPEN (Step:{comboStep}) at time {Time.time:F2}");
    }

    void EndRecovery()
    {
        inRecovery = false;
        DebugLog($"✓ Recovery ENDED (Step:{comboStep}) at time {Time.time:F2}");
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

        // Only go into recovery if block wasn't successful
        if (!successfulBlockFlag)
        {
            inRecovery = true;
            Invoke(nameof(EndRecovery), blockRecovery);
        }
        else
        {
            // Successful block - no recovery, ready to attack
            inRecovery = false;
            canAttack = true;
            successfulBlockFlag = false; // Reset the flag
            DebugLog("✓ Successful block - no recovery!");
        }

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

    /// <summary>
    /// Called by enemy when attack is successfully blocked
    /// Sets flag to bypass recovery on block release
    /// </summary>
    public void SetSuccessfulBlock()
    {
        successfulBlockFlag = true;
        DebugLog("⚡ Successful block flag set!");
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