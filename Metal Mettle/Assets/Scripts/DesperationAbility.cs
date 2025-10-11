using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DesperationAbility : MonoBehaviour
{
    [Header("Input (Auto-configured)")]
    private InputSystem_Actions inputActions;

    [Header("Desperation Settings")]
    [Tooltip("How long must Absorb be held before Execute can trigger Desperation")]
    public float holdDuration = 1.0f;

    [Tooltip("Visual feedback during hold")]
    public bool showVisualFeedback = true;

    [Header("Blood UI Flash")]
    [Tooltip("Flash the blood meter when Desperation is available")]
    public bool flashBloodMeter = true;
    public float flashSpeed = 3.0f;
    public Color flashColor = Color.white;
    private float flashTimer = 0f;

    [Header("Visual Feedback")]
    public GameObject desperationVFX;
    public Color chargingColor = new Color(0.8f, 0.1f, 0.1f);
    public float pulseSpeed = 2.0f;

    [Header("Audio")]
    public AudioClip chargingSound;
    public AudioClip executeSound;
    public AudioClip failSound;
    private AudioSource audioSource;

    [Header("References")]
    public BloodSystem bloodSystem;
    public Animator playerAnimator;
    public CameraShake cameraShake; // Reference to your existing camera shake

    [Header("Camera Shake Settings")]
    public float shakeIntensity = 0.3f;
    public float shakeDuration = 0.5f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    // State tracking
    private bool isHoldingAbsorb = false;
    private float holdTimer = 0f;
    private bool isCharging = false;
    private bool canExecute = false;

    // Visual feedback
    private Material[] playerMaterials;
    private Color[] originalColors;
    private float pulseTimer = 0f;

    // Blood meter flash tracking
    private bool wasFlashing = false;

    void Awake()
    {
        inputActions = new InputSystem_Actions();

        // Auto-find references if not assigned
        if (bloodSystem == null)
            bloodSystem = GetComponent<BloodSystem>();

        if (playerAnimator == null)
            playerAnimator = GetComponent<Animator>();

        // Try to find camera shake on main camera if not assigned
        if (cameraShake == null)
        {
            cameraShake = Camera.main?.GetComponent<CameraShake>();
            if (cameraShake == null && showDebugLogs)
                Debug.LogWarning("CameraShake component not found on Main Camera");
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void OnEnable()
    {
        inputActions.Enable();

        // Absorb button started
        inputActions.Player.Absorb.started += OnAbsorbStarted;
        inputActions.Player.Absorb.canceled += OnAbsorbCanceled;

        // Execute button pressed
        inputActions.Player.Execution.performed += OnExecutePressed;
    }

    void OnDisable()
    {
        inputActions.Player.Absorb.started -= OnAbsorbStarted;
        inputActions.Player.Absorb.canceled -= OnAbsorbCanceled;
        inputActions.Player.Execution.performed -= OnExecutePressed;

        inputActions.Disable();
    }

    void Start()
    {
        // Cache player materials for visual feedback
        if (showVisualFeedback)
        {
            CachePlayerMaterials();
        }

        if (desperationVFX != null)
        {
            desperationVFX.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (bloodSystem.IsDead()) return;

        // Flash blood meter if Desperation is available
        bool canUseDesp = bloodSystem.CanUseDesperation();

        if (flashBloodMeter && canUseDesp && !isCharging)
        {
            UpdateBloodMeterFlash();
            wasFlashing = true;
        }
        else if (wasFlashing)
        {
            // Reset to normal when no longer available
            ResetBloodMeterColor();
            wasFlashing = false;
        }

        // Track hold duration
        if (isHoldingAbsorb)
        {
            holdTimer += Time.deltaTime;

            // Check if we've held long enough
            if (holdTimer >= holdDuration && !isCharging)
            {
                StartCharging();
            }

            // Update visual feedback while charging
            if (isCharging && showVisualFeedback)
            {
                UpdateChargingVisuals();
            }
        }
    }

    void ResetBloodMeterColor()
    {
        if (bloodSystem.bloodFillImage == null) return;

        Color baseColor = Color.Lerp(
            bloodSystem.lowBloodColor,
            bloodSystem.highBloodColor,
            bloodSystem.GetBloodPercent() / 100f
        );

        bloodSystem.bloodFillImage.color = baseColor;
    }

    void UpdateBloodMeterFlash()
    {
        if (bloodSystem.bloodFillImage == null) return;

        flashTimer += Time.deltaTime * flashSpeed;
        float pulse = (Mathf.Sin(flashTimer) + 1f) * 0.5f; // 0 to 1

        // Lerp between current blood color and white
        Color baseColor = Color.Lerp(
            bloodSystem.lowBloodColor,
            bloodSystem.highBloodColor,
            bloodSystem.GetBloodPercent() / 100f
        );

        bloodSystem.bloodFillImage.color = Color.Lerp(baseColor, flashColor, pulse * 0.7f);
    }

    void OnAbsorbStarted(InputAction.CallbackContext context)
    {
        if (bloodSystem.IsDead()) return;

        // Only allow if Desperation is unlocked and blood is low enough
        if (!bloodSystem.CanUseDesperation())
        {
            if (showDebugLogs)
            {
                if (!bloodSystem.desperationUnlocked)
                    Debug.Log("❌ Desperation not unlocked yet");
                else
                    Debug.Log($"❌ Blood too high for Desperation ({bloodSystem.GetBloodPercent():F1}% > {bloodSystem.desperationMinBlood}%)");
            }
            return;
        }

        isHoldingAbsorb = true;
        holdTimer = 0f;

        if (showDebugLogs)
            Debug.Log("⏱️ Absorb button held - charging Desperation...");
    }

    void OnAbsorbCanceled(InputAction.CallbackContext context)
    {
        if (isHoldingAbsorb)
        {
            // If we were charging, show cancel feedback
            if (isCharging)
            {
                if (showDebugLogs)
                    Debug.Log("❌ Desperation charge cancelled");

                StopCharging();

                if (failSound != null && audioSource != null)
                    audioSource.PlayOneShot(failSound);
            }

            isHoldingAbsorb = false;
            holdTimer = 0f;
        }
    }

    void OnExecutePressed(InputAction.CallbackContext context)
    {
        if (bloodSystem.IsDead()) return;

        // Can only execute if we're charging
        if (!isCharging || !canExecute)
        {
            if (showDebugLogs && isHoldingAbsorb)
                Debug.Log($"❌ Execute pressed too early ({holdTimer:F2}s / {holdDuration}s)");
            return;
        }

        ExecuteDesperation();
    }

    void StartCharging()
    {
        isCharging = true;
        canExecute = true;

        if (showDebugLogs)
            Debug.Log("⚡ Desperation CHARGED - press Execute to self-harm!");

        // Visual feedback
        if (desperationVFX != null)
            desperationVFX.SetActive(true);

        // Audio feedback
        if (chargingSound != null && audioSource != null)
            audioSource.PlayOneShot(chargingSound);

        // Animator trigger
        if (playerAnimator != null)
            playerAnimator.SetBool("IsChargingDesperation", true);
    }

    void StopCharging()
    {
        isCharging = false;
        canExecute = false;

        // Reset visuals
        if (desperationVFX != null)
            desperationVFX.SetActive(false);

        if (playerAnimator != null)
            playerAnimator.SetBool("IsChargingDesperation", false);

        // Reset material colors
        if (showVisualFeedback && playerMaterials != null)
        {
            ResetPlayerColors();
        }
    }

    void ExecuteDesperation()
    {
        if (showDebugLogs)
            Debug.Log($"💉 DESPERATION EXECUTED - Self-harm for {bloodSystem.desperationGain} blood!");

        // Trigger the ability in BloodSystem
        bloodSystem.UseDesperation();

        // Reset blood meter flash
        flashTimer = 0f;
        if (bloodSystem.bloodFillImage != null)
        {
            // Reset to normal color immediately
            Color baseColor = Color.Lerp(
                bloodSystem.lowBloodColor,
                bloodSystem.highBloodColor,
                bloodSystem.GetBloodPercent() / 100f
            );
            bloodSystem.bloodFillImage.color = baseColor;
        }

        // Audio
        if (executeSound != null && audioSource != null)
            audioSource.PlayOneShot(executeSound);

        // Animation
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Desperation");
            playerAnimator.SetBool("IsChargingDesperation", false);
        }

        // Camera shake
        if (cameraShake != null)
        {
            cameraShake.Shake(shakeDuration, shakeIntensity);
        }

        // Reset state
        StopCharging();
        isHoldingAbsorb = false;
        holdTimer = 0f;
    }

    void UpdateChargingVisuals()
    {
        if (playerMaterials == null) return;

        pulseTimer += Time.deltaTime * pulseSpeed;
        float pulse = (Mathf.Sin(pulseTimer) + 1f) * 0.5f; // 0 to 1

        Color targetColor = Color.Lerp(Color.white, chargingColor, pulse);

        for (int i = 0; i < playerMaterials.Length; i++)
        {
            if (playerMaterials[i].HasProperty("_Color"))
            {
                playerMaterials[i].color = Color.Lerp(originalColors[i], targetColor, pulse);
            }
            else if (playerMaterials[i].HasProperty("_BaseColor"))
            {
                playerMaterials[i].SetColor("_BaseColor", Color.Lerp(originalColors[i], targetColor, pulse));
            }
        }
    }

    void CachePlayerMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            if (showDebugLogs)
                Debug.LogWarning("No renderers found for visual feedback");
            return;
        }

        // Count total materials
        int matCount = 0;
        foreach (var renderer in renderers)
            matCount += renderer.materials.Length;

        playerMaterials = new Material[matCount];
        originalColors = new Color[matCount];

        // Cache materials and colors
        int index = 0;
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                playerMaterials[index] = mat;

                if (mat.HasProperty("_Color"))
                    originalColors[index] = mat.color;
                else if (mat.HasProperty("_BaseColor"))
                    originalColors[index] = mat.GetColor("_BaseColor");
                else
                    originalColors[index] = Color.white;

                index++;
            }
        }
    }

    void ResetPlayerColors()
    {
        if (playerMaterials == null) return;

        for (int i = 0; i < playerMaterials.Length; i++)
        {
            if (playerMaterials[i].HasProperty("_Color"))
            {
                playerMaterials[i].color = originalColors[i];
            }
            else if (playerMaterials[i].HasProperty("_BaseColor"))
            {
                playerMaterials[i].SetColor("_BaseColor", originalColors[i]);
            }
        }

        pulseTimer = 0f;
    }

    // Public getters for UI/other systems
    public bool IsCharging() => isCharging;
    public float GetHoldProgress() => Mathf.Clamp01(holdTimer / holdDuration);
    public bool CanExecute() => canExecute;
}