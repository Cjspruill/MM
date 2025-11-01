using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class ExecutionSystem : MonoBehaviour
{
    [Header("Execution Settings")]
    public float executionRange = 3f;
    public float executionHealthThreshold = 0.25f; // 25% health or less
    public LayerMask enemyMask;

    [Header("Stealth Execution")]
    public bool allowStealthExecutions = true;
    public float stealthExecutionRange = 2f; // Closer range for stealth
    public float stealthExecutionAngle = 90f; // Must be behind enemy (90 = back half)
    public bool stealthRequiresEnergy = false; // Can stealth execute without energy
    public float stealthExecutionBloodBonus = 75f; // More blood for stealth kills

    [Header("Energy Requirements")]
    public float maxExecutionEnergy = 100f;
    public float requiredEnergy = 100f; // How much energy needed to execute
    public float energyPerHit = 15f; // Energy gained per successful hit
    public float energyDecayRate = 10f; // Energy lost per second when not hitting
    public float executionCooldown = 3f; // Minimum time between executions
    private float currentExecutionEnergy = 0f;
    private float lastExecutionTime = -999f;

    [Header("Blood Rewards")]
    public float executionBloodBonus = 50f; // Extra blood for execution
    public int executionOrbCount = 10; // More orbs than normal kill

    [Header("Visual Effects")]
    public float slowMotionScale = 0.3f; // How slow (0.3 = 30% speed)
    public float slowMotionDuration = 0.5f;
    public float cameraShakeMagnitude = 0.3f;
    public float cameraShakeDuration = 0.4f;
    public GameObject executionVFXPrefab; // Blood explosion effect
    public Color executionFlashColor = Color.red;
    public float flashDuration = 0.2f;

    [Header("Stealth Visual Effects")]
    public Color stealthFlashColor = new Color(0.5f, 0f, 0.5f); // Purple for stealth
    public GameObject stealthVFXPrefab; // Different effect for stealth kills
    public float stealthSlowMotionScale = 0.2f; // Even slower for stealth

    [Header("Camera Effects")]
    public bool useCameraZoom = true;
    public float zoomTargetFOV = 40f; // Lower = more zoomed in (default ~60)
    public float zoomInDuration = 0.3f;
    public float zoomOutDuration = 0.4f;
    public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool useChromaticAberration = true;
    public float chromaticIntensity = 1.0f; // Max intensity
    public float chromaticDuration = 0.5f;
    public AnimationCurve chromaticCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    public AudioClip executionSound;
    public AudioClip stealthExecutionSound; // Different sound for stealth
    public float executionSoundPitch = 0.8f; // Deeper pitch for impact

    [Header("Animation")]
    public string executionStateName = "Execution"; // Name of execution animation state
    public string stealthExecutionStateName = "StealthExecution"; // Stealth execution animation
    public float executionBlendTime = 0.1f; // Blend time for transition

    [Header("UI")]
    public GameObject executionPromptUI; // "Press E to Execute" prompt
    public GameObject stealthExecutionPromptUI; // "Press E to Assassinate" prompt
    public UnityEngine.UI.Slider energyBarSlider; // Energy bar slider
    public float promptFadeSpeed = 5f;

    [Header("Tutorial Integration")]
    public TutorialManager tutorialManager;

    [Header("Debug")]
    public bool showDebug = true;

    // Private references
    private BloodSystem bloodSystem;
    private Animator playerAnimator;
    private Camera mainCamera;
    private float originalFOV;
    private UnityEngine.Rendering.Universal.ChromaticAberration chromaticAberration;
    private UnityEngine.Rendering.Volume postProcessVolume;
    private Health nearestExecutableEnemy;
    private Health nearestStealthExecutableEnemy;
    private bool isExecuting = false;
    private InputSystem_Actions inputActions;
    private int executionCount = 0;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Execution.performed += OnExecutionPerformed;
    }

    void OnDisable()
    {
        inputActions.Player.Execution.performed -= OnExecutionPerformed;
        inputActions.Player.Disable();
    }

    void Start()
    {
        bloodSystem = GetComponent<BloodSystem>();
        playerAnimator = GetComponent<Animator>();
        mainCamera = Camera.main;

        if (tutorialManager == null)
        {
            tutorialManager = FindFirstObjectByType<TutorialManager>();
        }

        if (mainCamera != null)
        {
            originalFOV = mainCamera.fieldOfView;
        }

        // Find post-processing volume for chromatic aberration
        if (useChromaticAberration)
        {
            postProcessVolume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                if (postProcessVolume.profile.TryGet(out chromaticAberration))
                {
                    chromaticAberration.intensity.value = 0f; // Start at 0
                }
                else
                {
                    Debug.LogWarning("Chromatic Aberration not found in post-processing volume!");
                }
            }
            else
            {
                Debug.LogWarning("No post-processing volume found for chromatic aberration!");
            }
        }

        if (executionPromptUI != null)
        {
            executionPromptUI.SetActive(false);
        }

        if (stealthExecutionPromptUI != null)
        {
            stealthExecutionPromptUI.SetActive(false);
        }

        UpdateEnergyBar();
    }

    void Update()
    {
        // Don't process input during tutorials
        if (TutorialManager.IsTutorialActive)
            return;

        if (isExecuting) return;

        // NEW: Don't build energy or show prompts if execution isn't unlocked
        if (bloodSystem != null && !bloodSystem.IsAbilityUnlocked("execution"))
        {
            // Hide prompts if execution not unlocked
            if (executionPromptUI != null)
                executionPromptUI.SetActive(false);
            if (stealthExecutionPromptUI != null)
                stealthExecutionPromptUI.SetActive(false);

            return; // Exit early - no execution functionality until unlocked
        }

        // Decay energy over time when not hitting
        if (currentExecutionEnergy > 0)
        {
            currentExecutionEnergy -= energyDecayRate * Time.deltaTime;
            currentExecutionEnergy = Mathf.Max(0, currentExecutionEnergy);
            UpdateEnergyBar();
        }

        // Find executable enemies (both types)
        nearestExecutableEnemy = FindExecutableEnemy();
        nearestStealthExecutableEnemy = FindStealthExecutableEnemy();

        // Stealth execution takes priority if available
        bool canStealthExecute = nearestStealthExecutableEnemy != null &&
                                 (stealthRequiresEnergy ? currentExecutionEnergy >= requiredEnergy : true);

        // Regular execution check
        bool hasEnoughEnergy = currentExecutionEnergy >= requiredEnergy;
        bool cooldownReady = Time.time - lastExecutionTime >= executionCooldown;
        bool canExecute = hasEnoughEnergy && cooldownReady && nearestExecutableEnemy != null;

        // Show appropriate prompt
        if (executionPromptUI != null)
        {
            executionPromptUI.SetActive(canExecute && nearestStealthExecutableEnemy == null);
        }

        if (stealthExecutionPromptUI != null)
        {
            stealthExecutionPromptUI.SetActive(canStealthExecute);
        }
    }

    void OnExecutionPerformed(InputAction.CallbackContext context)
    {
        // NEW: Check if execution is unlocked first
        if (bloodSystem != null && !bloodSystem.IsAbilityUnlocked("execution"))
        {
            if (showDebug)
            {
                Debug.Log("⛔ Execution not unlocked yet!");
            }
            return;
        }

        // Stealth execution takes priority
        if (nearestStealthExecutableEnemy != null)
        {
            bool canStealthExecute = stealthRequiresEnergy ? currentExecutionEnergy >= requiredEnergy : true;

            if (!isExecuting && canStealthExecute)
            {
                ExecuteEnemy(nearestStealthExecutableEnemy, true);
                return;
            }
        }

        // Regular execution
        bool hasEnoughEnergy = currentExecutionEnergy >= requiredEnergy;
        bool cooldownReady = Time.time - lastExecutionTime >= executionCooldown;

        if (!isExecuting && nearestExecutableEnemy != null && hasEnoughEnergy && cooldownReady)
        {
            ExecuteEnemy(nearestExecutableEnemy, false);
        }
        else if (!cooldownReady && showDebug)
        {
            float remaining = executionCooldown - (Time.time - lastExecutionTime);
            Debug.Log($"⏱️ Execution on cooldown: {remaining:F1}s remaining");
        }
        else if (!hasEnoughEnergy && showDebug)
        {
            Debug.Log($"⚡ Not enough energy: {currentExecutionEnergy:F0}/{requiredEnergy}");
        }
    }

    /// <summary>
    /// Call this from AttackCollider when a hit lands
    /// </summary>
    public void AddExecutionEnergy()
    {
        // NEW: Only add energy if execution is unlocked
        if (bloodSystem != null && !bloodSystem.IsAbilityUnlocked("execution"))
        {
            return; // Don't build energy if not unlocked
        }

        currentExecutionEnergy += energyPerHit;
        currentExecutionEnergy = Mathf.Min(currentExecutionEnergy, maxExecutionEnergy);

        if (showDebug)
        {
            Debug.Log($"⚡ Execution energy: {currentExecutionEnergy:F0}/{maxExecutionEnergy} (+{energyPerHit})");
        }

        UpdateEnergyBar();
    }

    void UpdateEnergyBar()
    {
        if (energyBarSlider != null)
        {
            // NEW: Hide energy bar if execution not unlocked
            if (bloodSystem != null && !bloodSystem.IsAbilityUnlocked("execution"))
            {
                energyBarSlider.gameObject.SetActive(false);
                return;
            }
            else
            {
                energyBarSlider.gameObject.SetActive(true);
            }

            // Update slider value (0 to 1)
            energyBarSlider.value = currentExecutionEnergy / maxExecutionEnergy;

            // Color code the slider fill
            var fillImage = energyBarSlider.fillRect?.GetComponent<UnityEngine.UI.Image>();
            if (fillImage != null)
            {
                if (currentExecutionEnergy >= requiredEnergy)
                {
                    fillImage.color = Color.green; // Ready!
                }
                else if (currentExecutionEnergy >= requiredEnergy * 0.5f)
                {
                    fillImage.color = Color.yellow; // Getting close
                }
                else
                {
                    fillImage.color = Color.white; // Not ready
                }
            }
        }
    }

    Health FindExecutableEnemy()
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, executionRange, enemyMask);

        Health closestExecutable = null;
        float closestDistance = executionRange;

        foreach (Collider enemyCol in nearbyEnemies)
        {
            Health enemyHealth = enemyCol.GetComponent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead())
            {
                // Check if health is low enough
                float healthPercent = enemyHealth.GetHealthPercent();
                if (healthPercent <= executionHealthThreshold)
                {
                    float distance = Vector3.Distance(transform.position, enemyCol.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestExecutable = enemyHealth;
                    }
                }
            }
        }

        return closestExecutable;
    }

    Health FindStealthExecutableEnemy()
    {
        if (!allowStealthExecutions) return null;

        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, stealthExecutionRange, enemyMask);

        Health closestExecutable = null;
        float closestDistance = stealthExecutionRange;

        foreach (Collider enemyCol in nearbyEnemies)
        {
            EnemyAI enemyAI = enemyCol.GetComponent<EnemyAI>();
            Health enemyHealth = enemyCol.GetComponent<Health>();

            if (enemyAI != null && enemyHealth != null && !enemyHealth.IsDead())
            {
                // Must be unaware (not chasing and not attacking)
                if (!enemyAI.IsChasing() && !enemyAI.IsAttacking())
                {
                    // Check if player is behind enemy
                    Vector3 directionToPlayer = (transform.position - enemyCol.transform.position).normalized;
                    float angleToPlayer = Vector3.Angle(enemyCol.transform.forward, directionToPlayer);

                    // If angle > 90, player is in back half
                    if (angleToPlayer >= (180f - stealthExecutionAngle))
                    {
                        float distance = Vector3.Distance(transform.position, enemyCol.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestExecutable = enemyHealth;
                        }
                    }
                }
            }
        }

        return closestExecutable;
    }

    void ExecuteEnemy(Health enemy, bool isStealth)
    {
        if (isExecuting) return;

        StartCoroutine(ExecutionSequence(enemy, isStealth));
    }

    IEnumerator ExecutionSequence(Health enemy, bool isStealth)
    {
        isExecuting = true;

        // Consume energy and set cooldown (stealth might not require energy)
        if (isStealth && !stealthRequiresEnergy)
        {
            // Don't consume energy for stealth
        }
        else
        {
            currentExecutionEnergy = 0f;
        }

        lastExecutionTime = Time.time;
        UpdateEnergyBar();

        if (showDebug)
        {
            Debug.Log($"🗡️ {(isStealth ? "STEALTH " : "")}EXECUTING {enemy.gameObject.name}!");
        }

        // PLAY EXECUTION ANIMATION IMMEDIATELY
        if (playerAnimator != null)
        {
            // Reset combo if ComboController exists
            ComboController comboController = GetComponent<ComboController>();
            if (comboController != null)
            {
                comboController.ForceResetCombo();
            }

            // Play appropriate animation
            string animationName = isStealth ? stealthExecutionStateName : executionStateName;
            playerAnimator.Play(animationName, 0, 0f);

            if (showDebug)
            {
                Debug.Log($"🎬 Playing execution animation: {animationName}");
            }
        }

        // 1. SLOW MOTION (different speed for stealth)
        Time.timeScale = isStealth ? stealthSlowMotionScale : slowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // Keep physics stable

        // 2. FACE ENEMY
        Vector3 directionToEnemy = (enemy.transform.position - transform.position).normalized;
        directionToEnemy.y = 0;
        transform.rotation = Quaternion.LookRotation(directionToEnemy);

        // 3. SCREEN FLASH (different color for stealth)
        if (mainCamera != null)
        {
            StartCoroutine(ScreenFlash(isStealth ? stealthFlashColor : executionFlashColor));
        }

        // 4. CAMERA ZOOM
        if (useCameraZoom && mainCamera != null)
        {
            StartCoroutine(CameraZoomEffect());
        }

        // 5. CHROMATIC ABERRATION
        if (useChromaticAberration && chromaticAberration != null)
        {
            StartCoroutine(ChromaticAberrationEffect());
        }

        // 6. CAMERA SHAKE
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(cameraShakeDuration, cameraShakeMagnitude);
        }

        // 7. SOUND EFFECT (different sound for stealth)
        AudioClip soundToPlay = isStealth && stealthExecutionSound != null ? stealthExecutionSound : executionSound;
        if (soundToPlay != null)
        {
            AudioSource.PlayClipAtPoint(soundToPlay, enemy.transform.position);
        }

        // 8. SPAWN VFX (different effect for stealth)
        GameObject vfxToSpawn = isStealth && stealthVFXPrefab != null ? stealthVFXPrefab : executionVFXPrefab;
        if (vfxToSpawn != null)
        {
            Instantiate(vfxToSpawn, enemy.transform.position, Quaternion.identity);
        }

        // Wait for slow-mo to play out (real-time)
        yield return new WaitForSecondsRealtime(slowMotionDuration);

        enemy.ExecutionKill(); // Use new execution method for proper tracking

        // 10. SPAWN EXTRA BLOOD ORBS
        SpawnExecutionBlood(enemy.transform.position);

        // 11. RESTORE TIME
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // 12. GIVE BLOOD BONUS (more for stealth)
        if (bloodSystem != null)
        {
            float bonusAmount = isStealth ? stealthExecutionBloodBonus : executionBloodBonus;
            bloodSystem.GainBlood(bonusAmount);

            if (showDebug)
            {
                Debug.Log($"💉 {(isStealth ? "Stealth " : "")}Execution bonus: +{bonusAmount} blood!");
            }
        }

        // 13. TRIGGER FIRST EXECUTION TUTORIAL
        executionCount++;
        if (tutorialManager != null && executionCount == 1)
        {
            if (!tutorialManager.HasCompletedStep("first_execution_complete"))
            {
                tutorialManager.TriggerTutorial("first_execution_complete");
            }
        }

        // 14. TRIGGER STEALTH EXECUTION TUTORIAL (first stealth kill)
        if (isStealth && tutorialManager != null)
        {
            if (!tutorialManager.HasCompletedStep("first_stealth_execution"))
            {
                tutorialManager.TriggerTutorial("first_stealth_execution");
            }
        }

        isExecuting = false;
    }

    void SpawnExecutionBlood(Vector3 position)
    {
        // Get blood orb prefab from any nearby Health component as reference
        Health referenceHealth = FindFirstObjectByType<Health>();
        if (referenceHealth != null && referenceHealth.bloodOrbPrefab != null)
        {
            GameObject bloodOrbPrefab = referenceHealth.bloodOrbPrefab;

            // Spawn extra orbs in dramatic spread
            for (int i = 0; i < executionOrbCount; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * 2f;
                randomOffset.y = Mathf.Abs(randomOffset.y); // Keep orbs above ground

                Vector3 spawnPos = position + randomOffset;
                GameObject orb = Instantiate(bloodOrbPrefab, spawnPos, Quaternion.identity);

                // Add extra force for dramatic effect
                Rigidbody orbRb = orb.GetComponent<Rigidbody>();
                if (orbRb != null)
                {
                    Vector3 explosionForce = Random.insideUnitSphere * 5f;
                    explosionForce.y = Mathf.Abs(explosionForce.y) * 2f; // Shoot upward
                    orbRb.AddForce(explosionForce, ForceMode.Impulse);
                }
            }
        }
    }

    IEnumerator ScreenFlash(Color flashColor)
    {
        // Create a full-screen image to flash
        GameObject flashObj = new GameObject("ExecutionFlash");
        flashObj.transform.SetParent(mainCamera.transform);

        UnityEngine.UI.Image flashImage = flashObj.AddComponent<UnityEngine.UI.Image>();
        flashImage.color = flashColor;

        Canvas canvas = flashObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // Fade out
        float elapsed = 0f;
        Color startColor = flashColor;
        Color endColor = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / flashDuration;
            flashImage.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        Destroy(flashObj);
    }

    IEnumerator CameraZoomEffect()
    {
        if (mainCamera == null) yield break;

        float startFOV = mainCamera.fieldOfView;
        float targetFOV = zoomTargetFOV;

        // Zoom IN
        float elapsed = 0f;
        while (elapsed < zoomInDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Unscaled for slow-mo
            float t = zoomCurve.Evaluate(elapsed / zoomInDuration);
            mainCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
            yield return null;
        }

        // Hold at zoomed in state briefly
        yield return new WaitForSecondsRealtime(0.1f);

        // Zoom OUT
        elapsed = 0f;
        while (elapsed < zoomOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = zoomCurve.Evaluate(elapsed / zoomOutDuration);
            mainCamera.fieldOfView = Mathf.Lerp(targetFOV, originalFOV, t);
            yield return null;
        }

        // Ensure we're back to original
        mainCamera.fieldOfView = originalFOV;
    }

    IEnumerator ChromaticAberrationEffect()
    {
        if (chromaticAberration == null) yield break;

        float elapsed = 0f;
        float halfDuration = chromaticDuration * 0.5f;

        // Fade IN chromatic aberration
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = chromaticCurve.Evaluate(elapsed / halfDuration);
            chromaticAberration.intensity.value = Mathf.Lerp(0f, chromaticIntensity, t);
            yield return null;
        }

        // Fade OUT chromatic aberration
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = chromaticCurve.Evaluate(elapsed / halfDuration);
            chromaticAberration.intensity.value = Mathf.Lerp(chromaticIntensity, 0f, t);
            yield return null;
        }

        // Ensure it's back to 0
        chromaticAberration.intensity.value = 0f;
    }

    void OnDrawGizmosSelected()
    {
        // Draw execution range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, executionRange);

        // Draw stealth execution range
        if (allowStealthExecutions)
        {
            Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.3f); // Purple, transparent
            Gizmos.DrawWireSphere(transform.position, stealthExecutionRange);
        }

        // Draw line to executable enemy
        if (Application.isPlaying && nearestExecutableEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, nearestExecutableEnemy.transform.position);
        }

        // Draw line to stealth executable enemy
        if (Application.isPlaying && nearestStealthExecutableEnemy != null)
        {
            Gizmos.color = new Color(0.5f, 0f, 0.5f); // Purple
            Gizmos.DrawLine(transform.position, nearestStealthExecutableEnemy.transform.position);
        }
    }

    // Public getters
    public float GetExecutionEnergy() => currentExecutionEnergy;
    public float GetExecutionEnergyPercent() => currentExecutionEnergy / maxExecutionEnergy;
    public bool CanExecute() => currentExecutionEnergy >= requiredEnergy && Time.time - lastExecutionTime >= executionCooldown;
    public bool IsExecuting() => isExecuting;
    public int GetExecutionCount() => executionCount;
}