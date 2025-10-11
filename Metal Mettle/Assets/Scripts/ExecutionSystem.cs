using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class ExecutionSystem : MonoBehaviour
{
    [Header("Execution Settings")]
    public float executionRange = 3f;
    public float executionHealthThreshold = 0.25f; // 25% health or less
    public LayerMask enemyMask;

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

    [Header("Audio")]
    public AudioClip executionSound;
    public float executionSoundPitch = 0.8f; // Deeper pitch for impact

    [Header("UI")]
    public GameObject executionPromptUI; // "Press E to Execute" prompt
    public UnityEngine.UI.Slider energyBarSlider; // Energy bar slider
    public float promptFadeSpeed = 5f;

    [Header("Debug")]
    public bool showDebug = true;

    // Private references
    private BloodSystem bloodSystem;
    private Camera mainCamera;
    private Health nearestExecutableEnemy;
    private bool isExecuting = false;
    private InputSystem_Actions inputActions;

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
        mainCamera = Camera.main;

        if (executionPromptUI != null)
        {
            executionPromptUI.SetActive(false);
        }

        UpdateEnergyBar();
    }

    void Update()
    {
        if (isExecuting) return;

        // Decay energy over time when not hitting
        if (currentExecutionEnergy > 0)
        {
            currentExecutionEnergy -= energyDecayRate * Time.deltaTime;
            currentExecutionEnergy = Mathf.Max(0, currentExecutionEnergy);
            UpdateEnergyBar();
        }

        // Find executable enemy
        nearestExecutableEnemy = FindExecutableEnemy();

        // Check if execution is ready (energy + cooldown)
        bool hasEnoughEnergy = currentExecutionEnergy >= requiredEnergy;
        bool cooldownReady = Time.time - lastExecutionTime >= executionCooldown;
        bool canExecute = hasEnoughEnergy && cooldownReady && nearestExecutableEnemy != null;

        // Show/hide prompt based on all conditions
        if (executionPromptUI != null)
        {
            executionPromptUI.SetActive(canExecute);
        }
    }

    void OnExecutionPerformed(InputAction.CallbackContext context)
    {
        // Check all requirements
        bool hasEnoughEnergy = currentExecutionEnergy >= requiredEnergy;
        bool cooldownReady = Time.time - lastExecutionTime >= executionCooldown;

        if (!isExecuting && nearestExecutableEnemy != null && hasEnoughEnergy && cooldownReady)
        {
            ExecuteEnemy(nearestExecutableEnemy);
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

    void ExecuteEnemy(Health enemy)
    {
        if (isExecuting) return;

        StartCoroutine(ExecutionSequence(enemy));
    }

    IEnumerator ExecutionSequence(Health enemy)
    {
        isExecuting = true;

        // Consume energy and set cooldown
        currentExecutionEnergy = 0f;
        lastExecutionTime = Time.time;
        UpdateEnergyBar();

        if (showDebug)
        {
            Debug.Log($"🗡️ EXECUTING {enemy.gameObject.name}!");
        }

        // 1. SLOW MOTION
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // Keep physics stable

        // 2. FACE ENEMY
        Vector3 directionToEnemy = (enemy.transform.position - transform.position).normalized;
        directionToEnemy.y = 0;
        transform.rotation = Quaternion.LookRotation(directionToEnemy);

        // 3. SCREEN FLASH
        if (mainCamera != null)
        {
            StartCoroutine(ScreenFlash());
        }

        // 4. CAMERA SHAKE
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(cameraShakeDuration, cameraShakeMagnitude);
        }

        // 5. SOUND EFFECT (with pitch shift for impact)
        if (executionSound != null)
        {
            AudioSource.PlayClipAtPoint(executionSound, enemy.transform.position);
        }

        // 6. SPAWN VFX
        if (executionVFXPrefab != null)
        {
            Instantiate(executionVFXPrefab, enemy.transform.position, Quaternion.identity);
        }

        // Wait for slow-mo to play out (real-time)
        yield return new WaitForSecondsRealtime(slowMotionDuration);

        // 7. KILL ENEMY INSTANTLY
        enemy.TakeDamage(999999f, true); // Overkill damage

        // 8. SPAWN EXTRA BLOOD ORBS
        SpawnExecutionBlood(enemy.transform.position);

        // 9. RESTORE TIME
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // 10. GIVE BLOOD BONUS
        if (bloodSystem != null)
        {
            bloodSystem.GainBlood(executionBloodBonus);
            if (showDebug)
            {
                Debug.Log($"💉 Execution bonus: +{executionBloodBonus} blood!");
            }
        }

        isExecuting = false;
    }

    void SpawnExecutionBlood(Vector3 position)
    {
        // Get blood orb prefab from any nearby Health component as reference
        Health referenceHealth = FindObjectOfType<Health>();
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

    IEnumerator ScreenFlash()
    {
        // Create a full-screen image to flash
        GameObject flashObj = new GameObject("ExecutionFlash");
        flashObj.transform.SetParent(mainCamera.transform);

        UnityEngine.UI.Image flashImage = flashObj.AddComponent<UnityEngine.UI.Image>();
        flashImage.color = executionFlashColor;

        Canvas canvas = flashObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // Fade out
        float elapsed = 0f;
        Color startColor = executionFlashColor;
        Color endColor = new Color(executionFlashColor.r, executionFlashColor.g, executionFlashColor.b, 0f);

        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / flashDuration;
            flashImage.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        Destroy(flashObj);
    }

    void OnDrawGizmosSelected()
    {
        // Draw execution range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, executionRange);

        // Draw line to executable enemy
        if (Application.isPlaying && nearestExecutableEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, nearestExecutableEnemy.transform.position);
        }
    }

    // Public getters
    public float GetExecutionEnergy() => currentExecutionEnergy;
    public float GetExecutionEnergyPercent() => currentExecutionEnergy / maxExecutionEnergy;
    public bool CanExecute() => currentExecutionEnergy >= requiredEnergy && Time.time - lastExecutionTime >= executionCooldown;
}