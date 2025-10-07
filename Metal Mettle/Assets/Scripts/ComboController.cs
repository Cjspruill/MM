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
    public float comboEndCooldown = 0.7f;
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

    // Store the CURRENT attack's type for hitbox to use
    private bool currentAttackIsHeavy = false;
    private int currentAttackStep = 0;

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

        if (hasBufferedInput && canAttack && !inRecovery)
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
                    DebugLog($"🗑️ Discarding stale buffer");
                    hasBufferedInput = false;
                }
                return;
            }
            else
            {
                hasBufferedInput = false;
            }
        }

        if (controls.Player.Attack.WasPressedThisFrame())
        {
            if (hasBufferedInput)
            {
                hasBufferedInput = false;
            }

            buttonPressed = true;
            buttonHoldTime = 0f;
            heavyAttackTriggered = false;
            DebugLog($">>> BUTTON PRESSED <<< Step:{comboStep}");
        }

        if (buttonPressed && controls.Player.Attack.IsPressed())
        {
            buttonHoldTime += Time.deltaTime;

            if (buttonHoldTime >= heavyHoldTime && !heavyAttackTriggered)
            {
                DebugLog($"🔨 Heavy attack triggered");
                heavyAttackTriggered = true;
                buttonPressed = false;
                buttonHoldTime = 0f;

                if (canAttack || comboStep > 0)
                {
                    TriggerAttack(true);
                }
                else
                {
                    hasBufferedInput = true;
                    bufferedIsHeavy = true;
                    bufferedInputTime = Time.time;
                    bufferedExpectedComboStep = comboStep;
                }
            }
        }

        if (controls.Player.Attack.WasReleasedThisFrame())
        {
            DebugLog($">>> RELEASED <<< HeavyTriggered:{heavyAttackTriggered}");

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
                    if (canAttack || comboStep > 0)
                    {
                        TriggerAttack(false);
                    }
                    else
                    {
                        hasBufferedInput = true;
                        bufferedIsHeavy = false;
                        bufferedInputTime = Time.time;
                        bufferedExpectedComboStep = comboStep;
                    }
                }
            }
        }

        if (Time.time - lastAttackTime > comboWindow && comboStep > 0 && !isAttacking)
        {
            DebugLog($"Combo window expired");
            ResetCombo();
        }
    }

    void TriggerAttack(bool isHeavy)
    {
        DebugLog($"=== TriggerAttack === Heavy:{isHeavy}, Step:{comboStep}, IsHeavyCombo:{isHeavyCombo}");

        if (!canAttack && comboStep == 0)
        {
            return;
        }

        if (comboStep == 0 && inRecovery)
        {
            return;
        }

        // CHECK blood cost but don't drain yet - drain when hitbox activates
        if (bloodSystem != null)
        {
            float bloodCost = isHeavy ? 3f : 2f;
            if (bloodSystem.currentBlood < bloodCost)
            {
                DebugLog($"❌ Not enough blood! Need {bloodCost}, have {bloodSystem.currentBlood}");
                return;
            }
            // Blood will be drained in ActivateHitbox()
        }

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
                    return;
                }
                else if (isHeavy && !isHeavyCombo)
                {
                    DebugLog($"💥 Heavy finisher!");
                    isHeavyCombo = true;
                }
                else if (!isHeavy && isHeavyCombo)
                {
                    return;
                }
            }
        }

        comboStep++;

        int maxCombo;
        if (allowMixedCombos && isHeavyCombo && comboStep > maxHeavyCombo)
        {
            maxCombo = maxLightCombo;
        }
        else
        {
            maxCombo = isHeavyCombo ? maxHeavyCombo : maxLightCombo;
        }

        if (comboStep > maxCombo)
        {
            DebugLog($"🎯 Exceeded max!");
            EndCombo();
            return;
        }

        DebugLog($"⚔️ Attack {comboStep}/{maxCombo} - IsHeavyCombo:{isHeavyCombo}");

        float speedModifier = bloodSystem != null ? bloodSystem.GetAttackSpeedModifier() : 1f;
        float adjustedCooldown = attackCooldown / speedModifier;
        float adjustedRecovery = (isHeavy ? heavyRecovery : lightRecovery) / speedModifier;

        if (animator != null)
        {
            animator.SetTrigger("AttackTrigger");
            animator.SetInteger("ComboStep", comboStep);
            animator.SetBool("IsHeavy", isHeavyCombo);
            animator.SetFloat("AttackSpeed", speedModifier);
            animator.speed = speedModifier;
        }

        isAttacking = true;
        waitingForAnimationComplete = true;
        canAttack = false;
        lastAttackTime = Time.time;

        // STORE the current attack's type and step for hitbox to use later
        currentAttackIsHeavy = isHeavyCombo;
        currentAttackStep = comboStep;
        DebugLog($"   STORED: currentAttackIsHeavy={currentAttackIsHeavy}, currentAttackStep={currentAttackStep}");

        if (!useAnimationEvents)
        {
            ActivateHitbox();
            Invoke(nameof(DeactivateHitbox), attackDuration);
        }
        else
        {
            Invoke(nameof(ForceDeactivateHitbox), 2f);
        }

        float comboInputWindow = adjustedCooldown * 0.3f;
        Invoke(nameof(EnableNextAttack), comboInputWindow);

        inRecovery = true;
        float totalRecovery = adjustedCooldown + adjustedRecovery;
        Invoke(nameof(EndRecovery), totalRecovery);
    }

    public void ActivateHitbox()
    {
        DebugLog($"🗡️ HITBOX ON - STORED: Step:{currentAttackStep}, IsHeavy:{currentAttackIsHeavy}");
        DebugLog($"   (Current combo state: Step:{comboStep}, IsHeavyCombo:{isHeavyCombo})");

        // DRAIN BLOOD NOW - when hitbox actually activates
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

            // Use STORED attack type, not current combo state
            Color[] colors = currentAttackIsHeavy ? heavyColors : lightColors;
            int colorIndex = Mathf.Clamp(currentAttackStep - 1, 0, colors.Length - 1);
            Color selectedColor = colors[colorIndex];

            debugRenderer.material.color = selectedColor;

            DebugLog($"   Color: {(currentAttackIsHeavy ? "HEAVY" : "LIGHT")}[{colorIndex}] = {selectedColor}");
        }
    }

    public void DeactivateHitbox()
    {
        DebugLog($"🛡️ HITBOX OFF - Step:{comboStep}, IsHeavyCombo:{isHeavyCombo}");

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
        DebugLog($"✓ Window OPEN");
    }

    void EndRecovery()
    {
        inRecovery = false;
    }

    void ResetCombo()
    {
        if (isAttacking)
        {
            DebugLog($"⚠️ Preventing reset - still attacking!");
            return;
        }

        DebugLog($"🔄 RESET (was step {comboStep}, {(isHeavyCombo ? "HEAVY" : "LIGHT")})");

        comboStep = 0;
        isHeavyCombo = false;
        hasBufferedInput = false;
        heavyAttackTriggered = false;
        buttonPressed = false;
        isAttacking = false;

        if (animator != null)
        {
            animator.SetInteger("ComboStep", 0);
            animator.SetBool("IsHeavy", false);
        }
    }

    void EndCombo()
    {
        if (isAttacking)
        {
            DebugLog($"⚠️ Delaying EndCombo - still attacking!");
            Invoke(nameof(EndCombo), 0.2f);
            return;
        }

        DebugLog($"🏁 Combo Ended (step {comboStep}, {(isHeavyCombo ? "HEAVY" : "LIGHT")})");

        comboStep = 0;
        isHeavyCombo = false;
        isAttacking = false;
        waitingForAnimationComplete = false;
        hasBufferedInput = false;
        heavyAttackTriggered = false;
        buttonPressed = false;

        if (attackHitbox) attackHitbox.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;

        if (animator != null)
        {
            animator.SetInteger("ComboStep", 0);
            animator.SetBool("IsHeavy", false);
        }

        Invoke(nameof(EnableNextAttack), comboEndCooldown);
    }

    void StartBlocking()
    {
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

    void OnGUI()
    {
        if (!enableDebugLogs) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Combo: {comboStep} ({(isHeavyCombo ? "HEAVY" : "LIGHT")})");
        GUILayout.Label($"Can Attack: {canAttack}");
        GUILayout.Label($"Attacking: {isAttacking}");
        GUILayout.Label($"Recovery: {inRecovery}");

        if (hasBufferedInput)
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"BUFFERED: {(bufferedIsHeavy ? "H" : "L")}");
            GUI.color = Color.white;
        }

        GUILayout.EndArea();
    }
}