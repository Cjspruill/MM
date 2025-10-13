using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ComboController : MonoBehaviour
{
    [Header("References")]
    public BoxCollider attackHitbox; // DEPRECATED - kept for compatibility
    public MeshRenderer debugRenderer;
    public BloodSystem bloodSystem;
    public Animator animator;
    private InputSystem_Actions controls;
    private AttackCollider attackCollider;

    [Header("Limb Hitboxes")]
    public BoxCollider leftArmHitbox;
    public BoxCollider rightArmHitbox;
    public BoxCollider leftLegHitbox;
    public BoxCollider rightLegHitbox;

    [Header("Limb Debug Renderers (Optional)")]
    public MeshRenderer leftArmDebug;
    public MeshRenderer rightArmDebug;
    public MeshRenderer leftLegDebug;
    public MeshRenderer rightLegDebug;

    [Header("Combo Settings")]
    public int maxLightCombo = 3;
    public int maxHeavyCombo = 2;
    public float heavyHoldTime = 0.3f;
    public bool allowMixedCombos = false;

    [Header("Timing - DMC Style")]
    public float attackDuration = 0.3f;
    public float attackCooldown = 0.1f; // Reduced for faster combos
    public float comboWindow = 10f;
    public float comboEndCooldown = 0.1f; // Reduced
    public float inputBufferTime = 0.3f; // Increased for responsiveness
    public float cancelWindow = 0.5f; // INCREASED: Was 0.15f - now waits longer before allowing cancel
    public float minAnimationPlayTime = 0.4f; // NEW: Minimum time animation must play

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

    private bool currentAttackIsHeavy = false;
    private int currentAttackStep = 0;
    private float nextAttackAllowedTime = 0f;
    private bool isProcessingAttack = false;
    private bool successfulBlockFlag = false;
    private bool wasCancelled = false;
    private bool useAnimationEvents = true;

    // DMC-style flow variables
    private float attackStartTime = 0f;
    private bool inCancelWindow = false;
    private Queue<(bool isHeavy, float timestamp)> inputQueue = new Queue<(bool, float)>();
    private const int maxQueueSize = 3;

    private string lastLightAttack = "";
    private string lastHeavyAttack = "";
    private string lastPlayedState = "";

    void Start()
    {
        controls = InputManager.Instance.Controls;
        controls.Enable();

        // Disable all hitboxes at start
        if (attackHitbox) attackHitbox.enabled = false;
        if (leftArmHitbox) leftArmHitbox.enabled = false;
        if (rightArmHitbox) rightArmHitbox.enabled = false;
        if (leftLegHitbox) leftLegHitbox.enabled = false;
        if (rightLegHitbox) rightLegHitbox.enabled = false;

        if (debugRenderer) debugRenderer.enabled = false;
        if (leftArmDebug) leftArmDebug.enabled = false;
        if (rightArmDebug) rightArmDebug.enabled = false;
        if (leftLegDebug) leftLegDebug.enabled = false;
        if (rightLegDebug) rightLegDebug.enabled = false;

        if (blockVisual) blockVisual.SetActive(false);

        // Legacy support
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
            DebugLog("ComboController initialized - DMC STYLE");
        }
    }

    void Update()
    {

        // Don't process input during tutorials
        if (TutorialManager.IsTutorialActive)
            return;
        // CRITICAL: Check if absorbing - if so, block ALL combat input
        bool isAbsorbing = animator != null && animator.GetBool("IsAbsorbing");
        if (isAbsorbing)
        {
            DebugLog("🚫 Absorbing - blocking combat input");
            return;
        }

        // CRITICAL: Check if Desperation is active
        bool isDesperation = animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Desperation");
        if (isDesperation)
        {
            DebugLog("🚫 Desperation active - blocking combat input");
            return;
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

        // DMC-Style: Update cancel window
        if (isAttacking)
        {
            float timeSinceAttackStart = Time.time - attackStartTime;
            inCancelWindow = timeSinceAttackStart >= cancelWindow;
        }
        else
        {
            inCancelWindow = false;
        }

        // Process input queue for ultra-responsive combos
        ProcessInputQueue();

        // Button press detection - INSTANT queue
        if (controls.Player.Attack.WasPressedThisFrame())
        {
            buttonPressed = true;
            buttonHoldTime = 0f;
            heavyAttackTriggered = false;
            DebugLog($">>> BUTTON PRESSED <<<");
        }

        // Check for heavy attack trigger during hold
        if (buttonPressed && controls.Player.Attack.IsPressed())
        {
            buttonHoldTime += Time.deltaTime;

            if (buttonHoldTime >= heavyHoldTime && !heavyAttackTriggered)
            {
                DebugLog($"🔨 Heavy queued");
                heavyAttackTriggered = true;
                QueueAttack(true);
                buttonPressed = false;
                buttonHoldTime = 0f;
            }
        }

        // Button release - trigger light attack
        if (controls.Player.Attack.WasReleasedThisFrame())
        {
            if (!heavyAttackTriggered && buttonPressed)
            {
                DebugLog($"👊 Light queued");
                QueueAttack(false);
            }

            heavyAttackTriggered = false;
            buttonPressed = false;
            buttonHoldTime = 0f;
        }

        // Combo timeout (extended window)
        bool safeToTimeout = !isAttacking && inputQueue.Count == 0 && !isProcessingAttack;
        if (Time.time - lastAttackTime > comboWindow && comboStep > 0 && safeToTimeout)
        {
            DebugLog($"⏱️ Combo timeout");
            ResetCombo();
        }

        // Safety unstuck
        if (animator != null && animator.GetBool("IsAttacking") && !isAttacking && !isProcessingAttack && Time.time - lastAttackTime > 0.5f)
        {
            DebugLog($"🚨 Unstuck!");
            animator.SetBool("IsAttacking", false);
            animator.CrossFade("Locomotion", 0.1f, 0);
            ResetCombo();
        }
    }

    // NEW: Queue system for instant responsiveness
    void QueueAttack(bool isHeavy)
    {
        if (inputQueue.Count >= maxQueueSize)
        {
            DebugLog($"⚠️ Queue full, dropping oldest input");
            inputQueue.Dequeue();
        }

        inputQueue.Enqueue((isHeavy, Time.time));
        DebugLog($"📥 Queued {(isHeavy ? "HEAVY" : "LIGHT")} (Queue: {inputQueue.Count})");
    }

    void ProcessInputQueue()
    {
        if (inputQueue.Count == 0) return;
        if (isProcessingAttack) return;

        // Can we process the next input?
        bool canProcess = false;

        if (comboStep == 0)
        {
            // Starting combo - check global cooldown only
            canProcess = canAttack && Time.time >= nextAttackAllowedTime;
        }
        else if (isAttacking)
        {
            // Mid-combo - check BOTH cancel window AND minimum play time
            float timeSinceAttackStart = Time.time - attackStartTime;
            bool passedMinTime = timeSinceAttackStart >= minAnimationPlayTime;
            canProcess = inCancelWindow && passedMinTime;
        }
        else
        {
            // Between attacks - very permissive
            canProcess = !inRecovery;
        }

        if (!canProcess) return;

        var (nextIsHeavy, timestamp) = inputQueue.Peek();

        // Check if input is stale
        if (Time.time - timestamp > inputBufferTime)
        {
            DebugLog($"⏱️ Discarding stale input");
            inputQueue.Dequeue();
            return;
        }

        // Check blood cost
        if (bloodSystem != null)
        {
            float bloodCost = nextIsHeavy ? 3f : 2f;
            if (bloodSystem.currentBlood < bloodCost)
            {
                DebugLog($"❌ Not enough blood for queued attack");
                inputQueue.Clear(); // Clear queue on blood fail
                return;
            }
        }

        // Process the attack!
        inputQueue.Dequeue();
        TriggerAttack(nextIsHeavy);
    }

    void TriggerAttack(bool isHeavy)
    {
        if (isProcessingAttack)
        {
            DebugLog($"⚠️ Already processing");
            return;
        }

        isProcessingAttack = true;
        attackStartTime = Time.time;

        DebugLog($"⚔️ ATTACK! Heavy:{isHeavy}, Step:{comboStep}");

        // DMC-style: Determine combo type on first hit only
        bool wasHeavyCombo = isHeavyCombo;

        if (comboStep == 0)
        {
            isHeavyCombo = isHeavy;
            DebugLog($"🆕 Starting {(isHeavy ? "HEAVY" : "LIGHT")} combo");
        }
        else
        {
            bool tryingToMix = isHeavy != isHeavyCombo;

            if (tryingToMix)
            {
                if (!allowMixedCombos)
                {
                    DebugLog("❌ Cannot mix");
                    isProcessingAttack = false;
                    return;
                }
                else if (isHeavy && !isHeavyCombo)
                {
                    DebugLog($"💥 Heavy finisher!");
                }
                else
                {
                    DebugLog("❌ No heavy->light");
                    isProcessingAttack = false;
                    return;
                }
            }
        }

        int previousStep = comboStep;
        comboStep++;

        int maxCombo = (isHeavy && !wasHeavyCombo && allowMixedCombos && previousStep > 0)
            ? maxLightCombo
            : (wasHeavyCombo ? maxHeavyCombo : maxLightCombo);

        if (isHeavy && !wasHeavyCombo && allowMixedCombos && previousStep > 0)
        {
            isHeavyCombo = true;
        }

        if (comboStep > maxCombo)
        {
            DebugLog($"🎯 Combo maxed! Ending.");
            comboStep = maxCombo;
            isProcessingAttack = false;
            EndCombo();
            return;
        }

        // Execute attack with blood-based speed modifier
        float speedModifier = bloodSystem != null ? bloodSystem.GetAttackSpeedModifier() : 1f;

        isAttacking = true;
        waitingForAnimationComplete = true;
        lastAttackTime = Time.time;

        currentAttackIsHeavy = isHeavyCombo;
        currentAttackStep = comboStep;

        string stateName = GetAttackStateName(isHeavyCombo, comboStep);
        lastPlayedState = stateName;

        if (animator != null && !string.IsNullOrEmpty(stateName))
        {
            animator.SetBool("IsAttacking", true);

            // DMC-style: Very fast transitions for flow
            float blendTime = comboStep > 1 ? 0.05f : 0.1f;
            animator.CrossFade(stateName, blendTime, 0, 0f);

            // CRITICAL: Apply speed modifier to both animator parameter AND animator.speed
            animator.SetFloat("AttackSpeed", speedModifier);
            animator.speed = speedModifier;

            DebugLog($"🎬 Animation speed: {speedModifier:F2}x (Blood: {(bloodSystem != null ? bloodSystem.currentBlood : 100):F0}%)");
        }

        if (!useAnimationEvents)
        {
            ActivateHitbox();
            Invoke(nameof(DeactivateHitbox), attackDuration / speedModifier); // Account for speed
        }
        else
        {
            Invoke(nameof(ForceDeactivateHitbox), 2f / speedModifier); // Account for speed
        }

        // DMC-style: Minimal recovery, fast combo window (adjusted for speed)
        float comboInputWindow = 0.1f / speedModifier; // Faster at high blood
        Invoke(nameof(EnableNextAttack), comboInputWindow);

        // Recovery based on animation length - let it play! (adjusted for speed)
        inRecovery = true;
        AnimationClip clip = GetAnimationClip(stateName);
        float recoveryTime = clip != null ? (clip.length / speedModifier) * 0.7f : (0.5f / speedModifier); // 70% of anim

        Invoke(nameof(EndRecovery), recoveryTime);
        Invoke(nameof(ClearProcessingFlag), 0.05f);
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

    public void ActivateHitbox(string limbName = "")
    {
        DebugLog($"🗡️ HITBOX ON [{limbName}] - Step:{currentAttackStep}, IsHeavy:{currentAttackIsHeavy}");

        if (bloodSystem != null)
        {
            bloodSystem.OnAttack(currentAttackIsHeavy);
        }

        // Get the specific limb hitbox
        BoxCollider hitbox = GetHitboxByName(limbName);
        MeshRenderer debugMesh = GetDebugRendererByName(limbName);

        if (hitbox != null)
        {
            // Clear hit list on the specific limb's AttackCollider
            AttackCollider limbCollider = hitbox.GetComponent<AttackCollider>();
            if (limbCollider != null)
            {
                limbCollider.ClearHitList();
            }

            hitbox.enabled = true;

            if (debugMesh && showDebugHitbox)
            {
                debugMesh.enabled = true;
                Color[] colors = currentAttackIsHeavy ? heavyColors : lightColors;
                int colorIndex = Mathf.Clamp(currentAttackStep - 1, 0, colors.Length - 1);
                debugMesh.material.color = colors[colorIndex];
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ No hitbox found for limb: {limbName}");

            // Fallback to legacy system
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
    }

    public void DeactivateHitbox(string limbName = "")
    {
        BoxCollider hitbox = GetHitboxByName(limbName);
        MeshRenderer debugMesh = GetDebugRendererByName(limbName);

        if (hitbox != null)
        {
            hitbox.enabled = false;
            if (debugMesh) debugMesh.enabled = false;
        }
        else
        {
            // Fallback to legacy system
            if (attackHitbox) attackHitbox.enabled = false;
            if (debugRenderer) debugRenderer.enabled = false;
        }

        if (!isProcessingAttack)
        {
            isAttacking = false;
        }

        waitingForAnimationComplete = false;
        CancelInvoke(nameof(ForceDeactivateHitbox));
    }

    // Helper method to get hitbox by name
    BoxCollider GetHitboxByName(string limbName)
    {
        switch (limbName.ToLower())
        {
            case "leftarm":
            case "left arm":
            case "larm":
                return leftArmHitbox;

            case "rightarm":
            case "right arm":
            case "rarm":
                return rightArmHitbox;

            case "leftleg":
            case "left leg":
            case "lleg":
                return leftLegHitbox;

            case "rightleg":
            case "right leg":
            case "rleg":
                return rightLegHitbox;

            default:
                return null;
        }
    }

    // Helper method to get debug renderer by name
    MeshRenderer GetDebugRendererByName(string limbName)
    {
        switch (limbName.ToLower())
        {
            case "leftarm":
            case "left arm":
            case "larm":
                return leftArmDebug;

            case "rightarm":
            case "right arm":
            case "rarm":
                return rightArmDebug;

            case "leftleg":
            case "left leg":
            case "lleg":
                return leftLegDebug;

            case "rightleg":
            case "right leg":
            case "rleg":
                return rightLegDebug;

            default:
                return debugRenderer; // Fallback to legacy
        }
    }

    // Deactivate ALL hitboxes - useful for cleanup
    public void DeactivateAllHitboxes()
    {
        if (leftArmHitbox) leftArmHitbox.enabled = false;
        if (rightArmHitbox) rightArmHitbox.enabled = false;
        if (leftLegHitbox) leftLegHitbox.enabled = false;
        if (rightLegHitbox) rightLegHitbox.enabled = false;
        if (attackHitbox) attackHitbox.enabled = false;

        if (leftArmDebug) leftArmDebug.enabled = false;
        if (rightArmDebug) rightArmDebug.enabled = false;
        if (leftLegDebug) leftLegDebug.enabled = false;
        if (rightLegDebug) rightLegDebug.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;

        if (!isProcessingAttack)
        {
            isAttacking = false;
        }

        waitingForAnimationComplete = false;
    }

    void ForceDeactivateHitbox()
    {
        Debug.LogWarning("⚠️ Force deactivating ALL hitboxes!");
        DeactivateAllHitboxes();
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
        heavyAttackTriggered = false;
        buttonPressed = false;
        isAttacking = false;
        canAttack = true;
        currentAttackStep = 0;
        currentAttackIsHeavy = false;
        isProcessingAttack = false;
        wasCancelled = false;
        nextAttackAllowedTime = 0f;
        inCancelWindow = false;
        inputQueue.Clear();

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.CrossFade("Locomotion", 0.1f, 0);
            animator.speed = 1f;
        }
    }

    void EndCombo()
    {
        if (isAttacking || isProcessingAttack)
        {
            DebugLog($"⚠️ Delaying EndCombo");
            Invoke(nameof(EndCombo), 0.1f);
            return;
        }

        DebugLog($"🏁 Combo End");

        comboStep = 0;
        isHeavyCombo = false;
        isAttacking = false;
        waitingForAnimationComplete = false;
        heavyAttackTriggered = false;
        buttonPressed = false;
        canAttack = false;
        currentAttackStep = 0;
        currentAttackIsHeavy = false;
        isProcessingAttack = false;
        wasCancelled = false;
        inCancelWindow = false;
        inputQueue.Clear();

        // Disable all hitboxes
        DeactivateAllHitboxes();

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.CrossFade("Locomotion", 0.15f, 0);
            animator.speed = 1f;
        }

        // DMC-style: Very brief end cooldown
        nextAttackAllowedTime = Time.time + comboEndCooldown;
        Invoke(nameof(EnableNextAttack), comboEndCooldown);
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

        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        GUILayout.Label($"Combo: {comboStep} ({(isHeavyCombo ? "HEAVY" : "LIGHT")})");
        GUILayout.Label($"Can Attack: {canAttack}");
        GUILayout.Label($"Attacking: {isAttacking}");
        GUILayout.Label($"Processing: {isProcessingAttack}");
        GUILayout.Label($"Recovery: {inRecovery}");
        GUILayout.Label($"Cancel Window: {inCancelWindow}");

        if (inputQueue.Count > 0)
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"QUEUE: {inputQueue.Count} inputs");
            GUI.color = Color.white;
        }

        GUILayout.EndArea();
    }
}