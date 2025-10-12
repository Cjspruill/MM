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
    public float inputBufferTime = 0.2f;

    [Header("Block")]
    public bool blockEnabled = true;
    public GameObject blockVisual;
    public float blockStartupTime = 0.15f;
    public float blockRecovery = 0.25f;

    [Header("Animation State Names")]
    public string[] lightAttackStates = { "JAB", "CROSS", "LEAD UPPERCUT" };
    public string[] heavyAttackStates = { "RIGHT STRAIT KNEE", "ROUNDHOUSE KICK" };

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showDebugHitbox = true;
    public Color[] lightColors = { Color.white, Color.yellow, Color.red };
    public Color[] heavyColors = { Color.cyan, Color.magenta };

    // State variables
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
    private bool wasCancelled = false;
    private bool useAnimationEvents = true;

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
            DebugLog("ComboController initialized - ORIGINAL SYSTEM");
        }
    }

    void Update()
    {
        // CRITICAL: Check if absorbing - if so, block ALL combat input
        bool isAbsorbing = animator != null && animator.GetBool("IsAbsorbing");
        if (isAbsorbing)
        {
            DebugLog("🚫 Absorbing - blocking combat input");
            return; // Exit early, don't process any combat
        }

        // CRITICAL: Check if Desperation is active - if so, block ALL combat input
        bool isDesperation = animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Desperation");
        if (isDesperation)
        {
            DebugLog("🚫 Desperation active - blocking combat input");
            return; // Exit early
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

        // Safety check
        if (animator != null && animator.GetBool("IsAttacking") && !isAttacking && !isProcessingAttack && Time.time - lastAttackTime > 0.5f)
        {
            DebugLog($"🚨 STUCK DETECTION - Forcing locomotion!");
            animator.SetBool("IsAttacking", false);
            animator.CrossFade("Locomotion", 0.1f, 0);
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

        DebugLog($"=== TriggerAttack === Heavy:{isHeavy}, Step:{comboStep}, IsHeavyCombo:{isHeavyCombo}");

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

        bool isCancelling = isAttacking && comboStep > 0;
        if (isCancelling)
        {
            wasCancelled = true;
            DebugLog($"⚡ CANCELLING attack {comboStep} into next attack!");
        }

        bool wasHeavyCombo = isHeavyCombo;

        if (comboStep == 0)
        {
            isHeavyCombo = isHeavy;
            wasCancelled = false;
            DebugLog($"🆕 Starting {(isHeavy ? "HEAVY" : "LIGHT")} combo");
        }
        else
        {
            bool tryingToMix = isHeavy != isHeavyCombo;

            if (tryingToMix)
            {
                if (!allowMixedCombos)
                {
                    DebugLog("❌ Cannot mix attack types");
                    isProcessingAttack = false;
                    return;
                }
                else if (isHeavy && !isHeavyCombo)
                {
                    DebugLog($"💥 Heavy finisher! Switching from light to heavy");
                }
                else if (!isHeavy && isHeavyCombo)
                {
                    DebugLog("❌ Cannot go heavy to light");
                    isProcessingAttack = false;
                    return;
                }
            }
        }

        int previousComboStep = comboStep;
        comboStep++;

        int maxCombo;
        bool justSwitchedToHeavy = isHeavy && !wasHeavyCombo && allowMixedCombos && previousComboStep > 0;

        if (justSwitchedToHeavy)
        {
            maxCombo = maxLightCombo;
            isHeavyCombo = true;
        }
        else if (wasHeavyCombo)
        {
            maxCombo = maxHeavyCombo;
        }
        else
        {
            maxCombo = maxLightCombo;
        }

        if (comboStep > maxCombo)
        {
            DebugLog($"🎯 Combo exceeded max ({comboStep} > {maxCombo})! Ending combo.");
            comboStep = maxCombo;
            isProcessingAttack = false;
            EndCombo();
            return;
        }

        DebugLog($"⚔️ Attack {comboStep}/{maxCombo} - IsHeavyCombo:{isHeavyCombo}");

        float speedModifier = bloodSystem != null ? bloodSystem.GetAttackSpeedModifier() : 1f;
        float adjustedCooldown = attackCooldown / speedModifier;

        isAttacking = true;
        waitingForAnimationComplete = true;
        canAttack = false;
        lastAttackTime = Time.time;

        currentAttackIsHeavy = isHeavyCombo;
        currentAttackStep = comboStep;

        string stateName = GetAttackStateName(isHeavyCombo, comboStep);
        lastPlayedState = stateName;

        if (animator != null && !string.IsNullOrEmpty(stateName))
        {
            animator.SetBool("IsAttacking", true);

            // Simple crossfade - let it blend naturally
            float blendTime = 0.1f;
            animator.CrossFade(stateName, blendTime, 0, 0f);

            animator.SetFloat("AttackSpeed", speedModifier);
            animator.speed = speedModifier;
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

        AnimationClip clip = GetAnimationClip(stateName);
        float recoveryTime = clip != null ? clip.length / speedModifier : 0.3f;
        float totalRecovery = adjustedCooldown + recoveryTime;

        Invoke(nameof(EndRecovery), totalRecovery);
        Invoke(nameof(ClearProcessingFlag), 0.1f);
    }

    string GetAttackStateName(bool isHeavy, int step)
    {
        if (isHeavy)
        {
            if (heavyAttackStates.Length > 0)
            {
                string selectedAttack;

                if (heavyAttackStates.Length == 1)
                {
                    selectedAttack = heavyAttackStates[0];
                }
                else if (step == 1 || string.IsNullOrEmpty(lastHeavyAttack))
                {
                    int randomIndex = Random.Range(0, heavyAttackStates.Length);
                    selectedAttack = heavyAttackStates[randomIndex];
                }
                else
                {
                    int attempts = 0;
                    do
                    {
                        int randomIndex = Random.Range(0, heavyAttackStates.Length);
                        selectedAttack = heavyAttackStates[randomIndex];
                        attempts++;
                    } while (selectedAttack == lastHeavyAttack && attempts < 10);
                }

                lastHeavyAttack = selectedAttack;
                return selectedAttack;
            }
        }
        else
        {
            if (lightAttackStates.Length > 0)
            {
                string selectedAttack;

                if (lightAttackStates.Length == 1)
                {
                    selectedAttack = lightAttackStates[0];
                }
                else if (step == 1 || string.IsNullOrEmpty(lastLightAttack))
                {
                    int randomIndex = Random.Range(0, lightAttackStates.Length);
                    selectedAttack = lightAttackStates[randomIndex];
                }
                else
                {
                    int attempts = 0;
                    do
                    {
                        int randomIndex = Random.Range(0, lightAttackStates.Length);
                        selectedAttack = lightAttackStates[randomIndex];
                        attempts++;
                    } while (selectedAttack == lastLightAttack && attempts < 10);
                }

                lastLightAttack = selectedAttack;
                return selectedAttack;
            }
        }

        Debug.LogError($"❌ No animation found for isHeavy={isHeavy}, step={step}!");
        return null;
    }

    AnimationClip GetAnimationClip(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return null;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        foreach (var clip in clips)
        {
            if (clip.name == stateName)
                return clip;
        }

        foreach (var clip in clips)
        {
            if (clip.name.Contains(stateName))
                return clip;
        }

        return null;
    }

    void ClearProcessingFlag()
    {
        isProcessingAttack = false;
    }

    public void ActivateHitbox()
    {
        DebugLog($"🗡️ HITBOX ON - Step:{currentAttackStep}, IsHeavy:{currentAttackIsHeavy}");

        if (bloodSystem != null)
        {
            bloodSystem.OnAttack(currentAttackIsHeavy);
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
            debugRenderer.material.color = colors[colorIndex];
        }
    }

    public void DeactivateHitbox()
    {
        if (attackHitbox) attackHitbox.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;

        if (!isProcessingAttack)
        {
            isAttacking = false;
        }

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
        DebugLog($"✓ Combo window OPEN (Step:{comboStep})");
    }

    void EndRecovery()
    {
        inRecovery = false;
        DebugLog($"✓ Recovery ENDED (Step:{comboStep})");
    }

    void ResetCombo()
    {
        if (isAttacking || isProcessingAttack || waitingForAnimationComplete)
        {
            DebugLog($"⚠️ Preventing reset - state locked");
            return;
        }

        DebugLog($"🔄 RESET");

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
        wasCancelled = false;
        nextAttackAllowedTime = 0f;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.CrossFade("Locomotion", 0.15f, 0);
            animator.speed = 1f;
        }
    }

    void EndCombo()
    {
        if (isAttacking || isProcessingAttack)
        {
            DebugLog($"⚠️ Delaying EndCombo - still attacking or processing!");
            Invoke(nameof(EndCombo), 0.2f);
            return;
        }

        DebugLog($"🏁 Combo Ended");

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
        wasCancelled = false;

        if (attackHitbox) attackHitbox.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.CrossFade("Locomotion", 0.2f, 0);
            animator.speed = 1f;
        }

        nextAttackAllowedTime = Time.time + comboEndCooldown;
        canAttack = true;
    }

    void StartBlocking()
    {
        if (comboStep > 0)
        {
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

        if (!successfulBlockFlag)
        {
            inRecovery = true;
            Invoke(nameof(EndRecovery), blockRecovery);
        }
        else
        {
            inRecovery = false;
            canAttack = true;
            successfulBlockFlag = false;
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

    // Public API
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

    public void SetSuccessfulBlock()
    {
        successfulBlockFlag = true;
    }

    void OnGUI()
    {
        if (!enableDebugLogs) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 250));
        GUILayout.Label($"Combo: {comboStep} ({(isHeavyCombo ? "HEAVY" : "LIGHT")})");
        GUILayout.Label($"Can Attack: {canAttack}");
        GUILayout.Label($"Attacking: {isAttacking}");
        GUILayout.Label($"Processing: {isProcessingAttack}");
        GUILayout.Label($"Recovery: {inRecovery}");

        if (hasBufferedInput)
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"BUFFERED: {(bufferedIsHeavy ? "H" : "L")} (Step {bufferedExpectedComboStep})");
            GUI.color = Color.white;
        }

        GUILayout.EndArea();
    }
}