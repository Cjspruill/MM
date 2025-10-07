using UnityEngine;
using UnityEngine.InputSystem;

public class ComboController : MonoBehaviour
{
    [Header("References")]
    public BoxCollider attackHitbox;
    public MeshRenderer debugRenderer;
    public BloodSystem bloodSystem;
    private InputSystem_Actions controls;
    private AttackCollider attackCollider;

    [Header("Combo Settings")]
    public int maxLightCombo = 3;
    public int maxHeavyCombo = 2;
    public float heavyHoldTime = 0.3f;

    [Header("Timing")]
    public float attackDuration = 0.3f;
    public float attackCooldown = 0.5f;
    public float comboWindow = 1f;
    public float comboEndCooldown = 0.7f;
    public float perfectTimingWindow = 0.3f; // Sweet spot for combos

    [Header("Recovery")]
    public float lightRecovery = 0.2f;
    public float heavyRecovery = 0.4f;
    private bool inRecovery = false;

    [Header("Block")]
    public bool blockEnabled = true;
    public GameObject blockVisual;
    public float blockStartupTime = 0.15f;
    public float blockRecovery = 0.25f;
    private bool blockActive = false;
    private bool blockStarting = false;

    [Header("Debug Colors")]
    public bool showDebugHitbox = true;
    public Color[] lightColors = { Color.white, Color.yellow, Color.red };
    public Color[] heavyColors = { Color.cyan, Color.magenta };

    // State
    private int comboStep = 0;
    private bool isHeavyCombo = false;
    private float lastAttackTime;
    private bool canAttack = true;
    private bool isAttacking = false;
    private bool isBlocking = false;

    // Input tracking
    private bool buttonPressed = false;
    private float buttonHoldTime = 0f;

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
    }

    void Update()
    {
        // Check for block (Attack + Absorb held simultaneously)
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

        // Don't allow attacks while blocking or in recovery
        if (isBlocking || inRecovery) return;

        // Handle attack button press
        if (controls.Player.Attack.WasPressedThisFrame())
        {
            buttonPressed = true;
            buttonHoldTime = 0f;
        }

        // Track hold time
        if (buttonPressed && controls.Player.Attack.IsPressed())
        {
            buttonHoldTime += Time.deltaTime;

            // Trigger heavy attack when held long enough
            if (buttonHoldTime >= heavyHoldTime && canAttack && !isAttacking)
            {
                buttonPressed = false;
                TriggerAttack(true);
            }
        }

        // Handle button release (light attack)
        if (controls.Player.Attack.WasReleasedThisFrame() && buttonPressed)
        {
            buttonPressed = false;

            // Only light attack if we haven't already triggered heavy
            if (buttonHoldTime < heavyHoldTime && canAttack && !isAttacking)
            {
                TriggerAttack(false);
            }
        }

        // Reset combo if window expires
        if (Time.time - lastAttackTime > comboWindow && comboStep > 0)
        {
            ResetCombo();
        }
    }

    void TriggerAttack(bool isHeavy)
    {
        // Drain blood for the attack FIRST
        if (bloodSystem != null)
        {
            bloodSystem.OnAttack(isHeavy);
        }

        // Check for perfect timing
        float timeSinceLastAttack = Time.time - lastAttackTime;
        if (comboStep > 0 && timeSinceLastAttack <= perfectTimingWindow)
        {
            Debug.Log("Perfect timing!");
            // Could add damage bonus or visual effect here
        }

        // Start or continue combo
        if (comboStep == 0)
        {
            isHeavyCombo = isHeavy;
        }
        else
        {
            if (!isHeavy && isHeavyCombo)
            {
                Debug.Log("Cannot use light attack in heavy combo");
                return;
            }
        }

        comboStep++;
        int maxCombo = isHeavyCombo ? maxHeavyCombo : maxLightCombo;

        if (comboStep > maxCombo)
        {
            Debug.Log($"{(isHeavyCombo ? "Heavy" : "Light")} Combo Finished!");
            EndCombo();
            return;
        }

        Debug.Log($"{(isHeavy ? "Heavy" : "Light")} Attack {comboStep}/{maxCombo}");

        isAttacking = true;
        canAttack = false;
        lastAttackTime = Time.time;

        ActivateHitbox();
        Invoke(nameof(DeactivateHitbox), attackDuration);
        Invoke(nameof(EnableNextAttack), attackCooldown);

        // Start recovery period
        float recoveryTime = isHeavy ? heavyRecovery : lightRecovery;
        inRecovery = true;
        Invoke(nameof(EndRecovery), recoveryTime);
    }

    void ActivateHitbox()
    {
        if (attackCollider != null)
        {
            attackCollider.ClearHitList();
        }

        if (attackHitbox) attackHitbox.enabled = true;

        if (debugRenderer && showDebugHitbox)
        {
            debugRenderer.enabled = true;

            bool currentIsHeavy = buttonHoldTime >= heavyHoldTime || isHeavyCombo;
            Color[] colors = currentIsHeavy ? heavyColors : lightColors;
            int colorIndex = Mathf.Clamp(comboStep - 1, 0, colors.Length - 1);
            debugRenderer.material.color = colors[colorIndex];
        }
    }

    void DeactivateHitbox()
    {
        if (attackHitbox) attackHitbox.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;
        isAttacking = false;
    }

    void EnableNextAttack()
    {
        canAttack = true;
    }

    void EndRecovery()
    {
        inRecovery = false;
    }

    void ResetCombo()
    {
        comboStep = 0;
        isHeavyCombo = false;
        Debug.Log("Combo Reset");
    }

    void EndCombo()
    {
        comboStep = 0;
        isHeavyCombo = false;
        isAttacking = false;

        if (attackHitbox) attackHitbox.enabled = false;
        if (debugRenderer) debugRenderer.enabled = false;

        Invoke(nameof(EnableNextAttack), comboEndCooldown);
    }

    void StartBlocking()
    {
        blockStarting = true;
        isBlocking = true;

        Invoke(nameof(ActivateBlock), blockStartupTime);

        if (blockVisual != null)
        {
            blockVisual.SetActive(true);
        }

        Debug.Log("Block starting...");
    }

    void ActivateBlock()
    {
        blockActive = true;
        Debug.Log("Block active!");
    }

    void StopBlocking()
    {
        blockStarting = false;
        blockActive = false;

        CancelInvoke(nameof(ActivateBlock));

        // Recovery period after block
        inRecovery = true;
        Invoke(nameof(EndRecovery), blockRecovery);

        isBlocking = false;
        if (blockVisual != null)
        {
            blockVisual.SetActive(false);
        }

        Debug.Log("Block ended");
    }

    void OnDisable()
    {
        controls?.Disable();
    }

    // Public getters
    public int GetComboStep() => comboStep;
    public bool IsHeavyCombo() => isHeavyCombo;
    public bool IsBlocking() => isBlocking;
    public bool IsBlockActive() => blockActive;
    public bool IsInRecovery() => inRecovery;
}