using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BloodOrb : MonoBehaviour
{
    [Header("Blood Properties")]
    [Range(0.1f, 3f)]
    public float bloodAmount = 1f;
    public Color orbColor = new Color(0.6f, 0f, 0f, 1f);

    [Header("Visual Settings")]
    [Range(0.05f, 2f)]
    public float minSize = 0.5f;
    [Range(0.3f, 2f)]
    public float maxSize = 1f;
    [Range(0f, 1f)]
    public float pulseScale = 0.1f;
    [Range(0f, 5f)]
    public float pulseSpeed = 2f;
    public bool useEmission = true;
    [Range(0f, 2f)]
    public float emissionIntensity = 0.5f;

    [Header("Physics")]
    [Range(0.5f, 2f)]
    public float bloodDensity = 1.2f;
    [Range(0f, 5f)]
    public float airDrag = 0.5f;
    [Range(0f, 1f)]
    public float surfaceStickiness = 0.3f;
    [Range(0f, 1f)]
    public float viscosity = 0.5f;

    [Header("Blood Drip Trail")]
    public bool enableDripTrail = true;
    public GameObject dripletPrefab;
    [Range(0.1f, 1f)]
    public float dripletSize = 0.2f;

    [Header("Blood Splatter")]
    public bool spawnSplatterOnImpact = true;
    [Range(0f, 10f)]
    public float minImpactVelocity = 2f;
    public LayerMask splatOnLayers = ~0;

    [Header("Absorption")]
    [Range(1f, 10f)]
    public float absorptionRange = 3f;
    [Range(0.5f, 3f)]
    public float absorptionDuration = 1.5f;
    public AnimationCurve absorptionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Blood Siphon Effect")]
    [Tooltip("Use new LineRenderer-based siphon strands when absorbing")]
    public bool useLineRendererSiphon = true;
    [Tooltip("Number of blood strands to create per orb (1-3 recommended)")]
    [Range(1, 5)]
    public int siphonStrandCount = 1;
    [Tooltip("Keep legacy particle-based siphon effect")]
    public bool useLegacySiphonEffect = false;

    [Header("Animation Settings")]
    [Tooltip("Name of the IsAbsorbing bool in the Animator for FULL BODY layer")]
    public string absorbBoolName = "IsAbsorbing";
    [Tooltip("Name of the IsAbsorbing bool in the Animator for UPPER BODY layer")]
    public string absorbUpperBodyBoolName = "IsAbsorbingUpperBody";
    [Tooltip("Animator layer index for upper body animations (usually 1)")]
    public int upperBodyLayerIndex = 1;
    [Tooltip("Animator layer index for full body animations (usually 0)")]
    public int fullBodyLayerIndex = 0;
    [Tooltip("If player is moving (speed > 0), play absorb on upper body layer instead of full body")]
    public bool useUpperBodyWhenMoving = true;
    [Tooltip("How fast to fade in/out the animation layer weight")]
    public float layerWeightFadeSpeed = 5f;

    [Header("Explosion")]
    public float explosionForce = 200f;
    public float upwardForce = 150f;
    public float randomSpread = 30f;

    [Header("Lifetime")]
    public float lifetime = 10f;

    private Transform player;
    private BloodSystem playerBloodSystem;
    private ComboController playerComboController;
    private Animator playerAnimator;
    private BloodSiphonEffect playerSiphonEffect; // Legacy particle effect
    private BloodSiphon playerBloodSiphon; // NEW: LineRenderer-based siphon
    private CharacterController playerCharController; // NEW: For velocity checks
    private Rigidbody playerRb; // NEW: Fallback for velocity checks
    private InputSystem_Actions controls;
    private bool isAbsorbing = false;
    private float absorptionTimer = 0f;
    private Vector3 startScale;
    private Vector3 startPosition;
    private Vector3 sourcePosition; // NEW: Where blood came from (enemy position)
    private MeshRenderer meshRenderer;
    private Rigidbody rb;
    private Collider orbCollider;
    private Vector3 lastPosition;
    private float dripTimer = 0f;
    private MaterialPropertyBlock propBlock;
    private bool hasSpawnedSplatter = false;
    private int currentAbsorbLayer = -1; // Track which layer we're using
    private bool isFadingOutLayer = false;
    private Vector3 lastPlayerPosition;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            lastPlayerPosition = player.position;
            playerBloodSystem = playerObj.GetComponent<BloodSystem>();
            playerComboController = playerObj.GetComponent<ComboController>();
            playerAnimator = playerObj.GetComponent<Animator>();

            // Legacy particle-based siphon
            playerSiphonEffect = playerObj.GetComponent<BloodSiphonEffect>();

            // NEW: LineRenderer-based siphon
            playerBloodSiphon = playerObj.GetComponent<BloodSiphon>();

            // NEW: Cache player movement components for velocity checks
            playerCharController = playerObj.GetComponent<CharacterController>();
            playerRb = playerObj.GetComponent<Rigidbody>();

            if (playerAnimator == null)
            {
                Debug.LogWarning("BloodOrb: Player Animator not found!");
            }

            // Warn if neither siphon system is found
            if (playerSiphonEffect == null && playerBloodSiphon == null)
            {
                Debug.LogWarning("BloodOrb: No BloodSiphonEffect or BloodSiphon found on player!");
            }

            // Debug movement components
            if (playerCharController == null && playerRb == null)
            {
                Debug.LogWarning("BloodOrb: No CharacterController or Rigidbody found on player for velocity checks!");
            }
        }

        controls = InputManager.Instance.Controls;

        float randomSize = Random.Range(minSize, maxSize);
        transform.localScale = Vector3.one * randomSize;

        startScale = transform.localScale;
        startPosition = transform.position;
        sourcePosition = transform.position; // Default to spawn position
        meshRenderer = GetComponent<MeshRenderer>();
        rb = GetComponent<Rigidbody>();
        orbCollider = GetComponent<Collider>();
        propBlock = new MaterialPropertyBlock();

        if (rb != null)
        {
            rb.mass = bloodDensity;
            rb.linearDamping = airDrag;
            rb.angularDamping = airDrag * 0.5f;

            Vector3 randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                0,
                Random.Range(-1f, 1f)
            ).normalized;

            randomDirection += Random.insideUnitSphere * (randomSpread / 100f);
            Vector3 force = (randomDirection * explosionForce) + (Vector3.up * upwardForce);
            rb.AddForce(force);
            rb.AddTorque(Random.insideUnitSphere * 20f);
        }

        SetupBloodMaterial();
        lastPosition = transform.position;
        Invoke("DestroyOrb", lifetime);
    }

    private void DestroyOrb()
    {
        Destroy(gameObject);
    }

    void SetupBloodMaterial()
    {
        if (meshRenderer != null)
        {
            Material mat = meshRenderer.material;
            mat.color = orbColor;

            if (useEmission && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", orbColor * emissionIntensity);
            }

            if (mat.HasProperty("_Glossiness"))
            {
                mat.SetFloat("_Glossiness", 0.6f);
            }

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", 0f);
            }
        }
    }

    void Update()
    {
        if (player == null) return;

        if (!isAbsorbing)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
            float speed = velocity.magnitude;

            Vector3 stretchScale = startScale * (1f + pulse);
            if (speed > 0.1f && viscosity > 0f)
            {
                Vector3 moveDir = velocity.normalized;
                float stretchAmount = Mathf.Clamp01(speed * 0.1f) * viscosity;
                stretchScale += moveDir * stretchAmount * 0.3f;
                Vector3 perpendicular = Vector3.Cross(moveDir, Vector3.up).normalized;
                stretchScale -= perpendicular * stretchAmount * 0.15f;
            }

            transform.localScale = stretchScale;

            if (enableDripTrail && speed > 1f)
            {
                dripTimer += Time.deltaTime * speed;
                if (dripTimer > 0.2f)
                {
                    SpawnDriplet();
                    dripTimer = 0f;
                }
            }
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= absorptionRange && !isAbsorbing)
        {
            bool isPlayerBlocking = playerComboController != null && playerComboController.IsBlocking();
            bool isPlayerAttacking = playerComboController != null && playerComboController.IsAttacking();

            if (controls.Player.Absorb.IsPressed() && !isPlayerBlocking && !isPlayerAttacking)
            {
                StartAbsorption();
            }
        }

        if (isAbsorbing)
        {
            bool isPlayerBlocking = playerComboController != null && playerComboController.IsBlocking();
            bool isPlayerAttacking = playerComboController != null && playerComboController.IsAttacking();
            bool absorbButtonReleased = !controls.Player.Absorb.IsPressed();

            if (absorbButtonReleased || isPlayerBlocking || isPlayerAttacking)
            {
                CancelAbsorption();
                return;
            }

            // CONTINUOUS LAYER MANAGEMENT - Check EVERY frame
            if (playerAnimator != null && useUpperBodyWhenMoving)
            {
                // Get current player speed
                float currentSpeed = GetPlayerSpeed();
                bool shouldUseUpperBody = currentSpeed > 0.1f;
                int desiredLayer = shouldUseUpperBody ? upperBodyLayerIndex : fullBodyLayerIndex;

                // If desired layer changed, switch immediately
                if (desiredLayer != currentAbsorbLayer)
                {
                    Debug.Log($"Switching animation layer: {currentAbsorbLayer} -> {desiredLayer}, speed={currentSpeed:F2}");

                    if (desiredLayer == upperBodyLayerIndex)
                    {
                        // Switch to upper body
                        playerAnimator.SetBool(absorbBoolName, false);
                        playerAnimator.SetLayerWeight(upperBodyLayerIndex, 1f);
                        playerAnimator.SetBool(absorbUpperBodyBoolName, true);
                        Debug.Log("Activated upper body absorb");
                    }
                    else
                    {
                        // Switch to full body
                        playerAnimator.SetBool(absorbUpperBodyBoolName, false);
                        playerAnimator.SetLayerWeight(upperBodyLayerIndex, 0f);
                        playerAnimator.SetBool(absorbBoolName, true);
                        Debug.Log("Activated full body absorb");
                    }

                    currentAbsorbLayer = desiredLayer;
                }

                // Ensure layer weight stays correct
                if (currentAbsorbLayer == upperBodyLayerIndex)
                {
                    playerAnimator.SetLayerWeight(upperBodyLayerIndex, 1f);
                }
                else
                {
                    playerAnimator.SetLayerWeight(upperBodyLayerIndex, 0f);
                }
            }

            absorptionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(absorptionTimer / absorptionDuration);
            float curveProgress = absorptionCurve.Evaluate(progress);

            Vector3 targetPos = player.position + Vector3.up * 1.5f;
            transform.position = Vector3.Lerp(startPosition, targetPos, curveProgress);

            float scaleMultiplier = 1f - (curveProgress * 0.8f);
            transform.localScale = startScale * scaleMultiplier;

            if (absorptionTimer >= absorptionDuration)
            {
                CompleteAbsorption();
            }
        }
    }

    private float GetPlayerSpeed()
    {
        // Method 1: Check input directly (most reliable)
        if (controls != null)
        {
            Vector2 moveInput = controls.Player.Move.ReadValue<Vector2>();
            float inputMagnitude = moveInput.magnitude;

            if (Time.frameCount % 30 == 0 && isAbsorbing)
            {
                Debug.Log($"Move input magnitude: {inputMagnitude:F3}, raw input: {moveInput}");
            }

            // If player is giving movement input, they're moving
            if (inputMagnitude > 0.1f)
            {
                return inputMagnitude * 5f; // Scale to approximate speed
            }
        }

        // Method 2: Check Animator Speed parameter (very reliable for animated movement)
        if (playerAnimator != null)
        {
            float animSpeed = playerAnimator.GetFloat("Speed");

            if (Time.frameCount % 30 == 0 && isAbsorbing)
            {
                Debug.Log($"Animator Speed: {animSpeed:F3}");
            }

            if (animSpeed > 0.01f)
            {
                return animSpeed;
            }
        }

        // Method 3: Manual position tracking (fallback)
        if (player != null)
        {
            float positionSpeed = (player.position - lastPlayerPosition).magnitude / Time.deltaTime;
            lastPlayerPosition = player.position;

            if (Time.frameCount % 30 == 0 && isAbsorbing)
            {
                Debug.Log($"Position-based speed: {positionSpeed:F3}");
            }

            return positionSpeed;
        }

        return 0f;
    }

    private IEnumerator ActivateUpperBodyAfterDelay()
    {
        // Wait a tiny bit for the full body animation to start transitioning out
        yield return new WaitForSeconds(0.1f);

        if (isAbsorbing && playerAnimator != null)
        {
            // Ensure upper body weight is at 1
            playerAnimator.SetLayerWeight(upperBodyLayerIndex, 1f);

            // Activate upper body absorb
            playerAnimator.SetBool(absorbUpperBodyBoolName, true);
            currentAbsorbLayer = upperBodyLayerIndex;

            Debug.Log("Upper body absorb activated after transition delay");
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Blood splatter logic
        if (!spawnSplatterOnImpact) return;
        if (hasSpawnedSplatter) return;

        GameObject hitObject = collision.gameObject;
        if (hitObject.CompareTag("Player"))
        {
            Debug.Log("Blood orb hit player - no splatter spawned");
            return;
        }

        int hitLayer = hitObject.layer;
        if ((splatOnLayers.value & (1 << hitLayer)) == 0)
        {
            Debug.Log($"Layer '{LayerMask.LayerToName(hitLayer)}' not in splatOnLayers mask - no splatter");
            return;
        }

        float impactVelocity = collision.relativeVelocity.magnitude;
        if (impactVelocity < minImpactVelocity)
        {
            Debug.Log($"Impact too slow for splatter: {impactVelocity:F2} < {minImpactVelocity:F2}");
            return;
        }

        ContactPoint contact = collision.GetContact(0);
        Vector3 splatterPosition = contact.point;
        Vector3 splatterNormal = contact.normal;

        BloodSplatterManager.SpawnSplatter(splatterPosition, splatterNormal);
        hasSpawnedSplatter = true;

        Debug.Log($"Blood splatter spawned! Hit: {hitObject.name}, Layer: {LayerMask.LayerToName(hitLayer)}, Velocity: {impactVelocity:F2}");
    }

    void SpawnDriplet()
    {
        if (dripletPrefab == null) return;

        GameObject driplet = Instantiate(dripletPrefab, transform.position, Quaternion.identity);
        driplet.transform.localScale = Vector3.one * dripletSize;

        Rigidbody dripletRb = driplet.GetComponent<Rigidbody>();
        if (dripletRb != null)
        {
            Vector3 inheritVelocity = rb != null ? rb.linearVelocity * 0.3f : Vector3.zero;
            dripletRb.linearVelocity = inheritVelocity + Vector3.down * 2f;
        }

        Destroy(driplet, 2f);
    }

    void OnCollisionStay(Collision collision)
    {
        if (rb != null && surfaceStickiness > 0f)
        {
            float currentSpeed = rb.linearVelocity.magnitude;
            if (currentSpeed < 2f)
            {
                rb.linearVelocity *= (1f - surfaceStickiness * Time.deltaTime);
                rb.angularVelocity *= (1f - surfaceStickiness * 0.5f * Time.deltaTime);
            }
        }
    }

    void FixedUpdate()
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(Physics.gravity * (bloodDensity - 1f), ForceMode.Acceleration);
        }

        lastPosition = transform.position;
        transform.localScale = startScale;
    }

    void StartAbsorption()
    {
        if (isAbsorbing) return;

        Debug.Log("=== Blood orb absorption started ===");

        if (orbCollider != null)
        {
            orbCollider.enabled = false;
        }

        isAbsorbing = true;
        absorptionTimer = 0f;
        startPosition = transform.position;
        isFadingOutLayer = false;

        // Siphon effects
        if (useLineRendererSiphon && playerBloodSiphon != null)
        {
            float bloodPerStrand = bloodAmount / Mathf.Max(1, siphonStrandCount);
            for (int i = 0; i < siphonStrandCount; i++)
            {
                Vector3 offset = Random.insideUnitSphere * 0.1f;
                playerBloodSiphon.StartSiphon(transform.position + offset, bloodPerStrand);
            }
        }

        if (useLegacySiphonEffect && playerSiphonEffect != null)
        {
            playerSiphonEffect.StartSiphon(transform.position);
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Animation setup - Let Update() handle the actual layer switching
        if (playerAnimator != null)
        {
            // Reset both bools first
            playerAnimator.SetBool(absorbBoolName, false);
            playerAnimator.SetBool(absorbUpperBodyBoolName, false);

            // Check initial speed
            float initialSpeed = GetPlayerSpeed();
            bool shouldUseUpperBody = useUpperBodyWhenMoving && initialSpeed > 0.1f;

            Debug.Log($"Initial speed: {initialSpeed:F2}, using upper body: {shouldUseUpperBody}");

            if (shouldUseUpperBody)
            {
                currentAbsorbLayer = upperBodyLayerIndex;
                playerAnimator.SetLayerWeight(upperBodyLayerIndex, 1f);
                playerAnimator.SetBool(absorbUpperBodyBoolName, true);
                Debug.Log("Started with UPPER BODY absorb");
            }
            else
            {
                currentAbsorbLayer = fullBodyLayerIndex;
                playerAnimator.SetLayerWeight(upperBodyLayerIndex, 0f);
                playerAnimator.SetBool(absorbBoolName, true);
                Debug.Log("Started with FULL BODY absorb");
            }
        }
    }

    public void CancelAbsorption()
    {
        if (!isAbsorbing) return;

        Debug.Log("Blood orb absorption cancelled");

        isAbsorbing = false;
        absorptionTimer = 0f;

        if (rb != null)
        {
            rb.isKinematic = false;
        }

        if (orbCollider != null)
        {
            orbCollider.enabled = true;
        }

        if (playerAnimator != null)
        {
            // Turn off BOTH bools - animation will smoothly transition out
            playerAnimator.SetBool(absorbBoolName, false);
            playerAnimator.SetBool(absorbUpperBodyBoolName, false);

            // Start fading out the upper body layer if it's active
            if (currentAbsorbLayer == upperBodyLayerIndex)
            {
                isFadingOutLayer = true;
            }
        }
    }

    void CompleteAbsorption()
    {
        if (playerAnimator != null)
        {
            // Turn off BOTH bools - animation will smoothly transition out
            playerAnimator.SetBool(absorbBoolName, false);
            playerAnimator.SetBool(absorbUpperBodyBoolName, false);

            // Start fading out the upper body layer if it's active
            if (currentAbsorbLayer == upperBodyLayerIndex)
            {
                isFadingOutLayer = true;
            }
        }

        if (playerBloodSystem != null)
        {
            playerBloodSystem.GainBlood(bloodAmount);
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (isAbsorbing && playerAnimator != null)
        {
            playerAnimator.SetBool(absorbBoolName, false);
            playerAnimator.SetBool(absorbUpperBodyBoolName, false);

            // Immediately reset layer weight on destroy
            if (currentAbsorbLayer == upperBodyLayerIndex)
            {
                playerAnimator.SetLayerWeight(upperBodyLayerIndex, 0f);
            }
        }
    }

    /// <summary>
    /// Set where this blood orb originated from (enemy position).
    /// Called by Health.cs when spawning orbs.
    /// </summary>
    public void SetSourcePosition(Vector3 source)
    {
        sourcePosition = source;
        Debug.Log($"Blood orb source position set to: {source}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, absorptionRange);

        // Draw line to source position for debugging
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, sourcePosition);
            Gizmos.DrawWireSphere(sourcePosition, 0.2f);
        }
    }

    /// <summary>
    /// Cancel the automatic lifetime destruction. Called by MaskSiphon.
    /// </summary>
    public void CancelLifetimeDestroy()
    {
        CancelInvoke("DestroyOrb");
    }

    /// <summary>
    /// Check if this orb is being absorbed by the player
    /// </summary>
    public bool IsAbsorbing()
    {
        return isAbsorbing;
    }
}

/*
=== BLOOD SIPHON STRAND SYSTEM ===

NEW FEATURES:
- useLineRendererSiphon: Enable dramatic blood strand effects
- siphonStrandCount: How many strands flow per orb (1-3 recommended)
- useLegacySiphonEffect: Keep old particle effect if desired

HOW IT WORKS:
1. Enemy dies → Spawns blood orbs
2. Orbs fly out with physics, land on ground
3. Player walks near orb and presses Absorb
4. Blood strands flow from ORB'S CURRENT POSITION to player's mouth
5. Orb fades out during absorption
6. Player receives blood when absorption completes

ANIMATION IMPROVEMENTS:
- Animation loops smoothly while absorbing
- Dynamically switches between upper body and full body layers based on player movement
- Smooth layer weight transitions prevent jarring cuts
- Animation completes its loop cycle before transitioning out when done

WHY USE ORB POSITION (not enemy source):
- Orbs move around (physics, bouncing, rolling)
- Visual clarity: See blood flowing from what you're absorbing
- Makes sense: You're draining the orb itself, not the dead enemy
- Better for gameplay: Works even if orb rolled far away

CONFIGURATION:
- For clean look: siphonStrandCount = 1 (one strand per orb)
- For dramatic: siphonStrandCount = 2-3 (multiple strands)
- Heavy attack with 6 orbs × 1 strand = 6 strands flowing over time
- Heavy attack with 6 orbs × 3 strands = 18 strands total (very dramatic!)

BACKWARDS COMPATIBILITY:
- Set useLineRendererSiphon = false to disable new system
- Set useLegacySiphonEffect = true to use old particle effect
- Can run both simultaneously for layered effect

REQUIRES:
- BloodSiphon component on player GameObject
- Blood material assigned to BloodSiphon
- Mouth target set up in BloodSiphon
- Animator with IsAbsorbing bool parameter
- Proper animation transitions set up (see documentation)

ANIMATOR SETUP:
Your Absorb State transitions:
- Absorb → Idle: 
  - Condition: IsAbsorbing == false
  - Exit Time: 0.8 (lets most of the current loop finish)
  - Transition Duration: 0.2-0.3 seconds
  - Has Exit Time: ✓ (checked)

This ensures the animation loops while absorbing and exits smoothly when done.
*/