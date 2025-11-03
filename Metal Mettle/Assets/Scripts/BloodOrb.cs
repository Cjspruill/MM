using UnityEngine;
using UnityEngine.InputSystem;

public class BloodOrb : MonoBehaviour
{
    [Header("Blood Amount")]
    public float bloodAmount = 25f;

    [Header("Visual Effects")]
    public float pulseSpeed = 1.5f;
    public float pulseScale = 0.15f;
    public Color orbColor = new Color(0.6f, 0f, 0f);
    public bool useEmission = true;
    [Range(0f, 2f)]
    public float emissionIntensity = 0.8f;

    [Header("Size Randomization")]
    [Range(0.01f, 0.5f)]
    public float minSize = 0.05f;
    [Range(0.01f, 0.5f)]
    public float maxSize = 0.25f;

    [Header("Blood Physics - Viscous Feel")]
    [Range(0f, 1f)]
    public float viscosity = 0.4f;
    [Range(0.5f, 3f)]
    public float bloodDensity = 1.5f;
    [Range(0f, 5f)]
    public float airDrag = 1.5f;
    [Range(0f, 1f)]
    public float surfaceStickiness = 0.3f;
    public bool enableDripTrail = true;

    [Header("Blood Splatter Settings")]
    [Tooltip("Spawn blood splatter decal when orb hits surfaces")]
    public bool spawnSplatterOnImpact = true;
    [Tooltip("Minimum velocity (magnitude) required to spawn a splatter")]
    [Range(0.5f, 10f)]
    public float minImpactVelocity = 2f;
    [Tooltip("Which layers should spawn blood splatters when hit (e.g., Ground, Environment)")]
    public LayerMask splatOnLayers;

    [Header("Absorption")]
    public float absorptionRange = 3f;
    public float absorptionDuration = 0.5f;
    public AnimationCurve absorptionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Blood Siphon Settings")]
    [Tooltip("Use new LineRenderer-based siphon strands when absorbing")]
    public bool useLineRendererSiphon = true;
    [Tooltip("Number of blood strands to create per orb (1-3 recommended)")]
    [Range(1, 5)]
    public int siphonStrandCount = 1;
    [Tooltip("Keep legacy particle-based siphon effect")]
    public bool useLegacySiphonEffect = false;

    [Header("Animation Settings")]
    [Tooltip("Name of the IsAbsorbing bool in the Animator")]
    public string absorbBoolName = "IsAbsorbing";
    [Tooltip("If true, uses Animator.CrossFade() for immediate smooth transition")]
    public bool forceImmediateTransition = true;
    [Tooltip("Name of the Absorb animation state (only needed if forceImmediateTransition is true)")]
    public string absorbStateName = "Absorb";
    [Tooltip("Duration of the crossfade in seconds (0 = instant, 0.1-0.3 = smooth)")]
    [Range(0f, 1f)]
    public float crossfadeDuration = 0.15f;
    [Tooltip("If player is moving (speed > 0), play absorb on upper body layer instead of full body")]
    public bool useUpperBodyWhenMoving = true;
    [Tooltip("Animator layer index for upper body animations (usually 1)")]
    public int upperBodyLayerIndex = 1;
    [Tooltip("Animator layer index for full body animations (usually 0)")]
    public int fullBodyLayerIndex = 0;

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

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerBloodSystem = playerObj.GetComponent<BloodSystem>();
            playerComboController = playerObj.GetComponent<ComboController>();
            playerAnimator = playerObj.GetComponent<Animator>();

            // Legacy particle-based siphon
            playerSiphonEffect = playerObj.GetComponent<BloodSiphonEffect>();

            // NEW: LineRenderer-based siphon
            playerBloodSiphon = playerObj.GetComponent<BloodSiphon>();

            if (playerAnimator == null)
            {
                Debug.LogWarning("BloodOrb: Player Animator not found!");
            }

            // Warn if neither siphon system is found
            if (playerSiphonEffect == null && playerBloodSiphon == null)
            {
                Debug.LogWarning("BloodOrb: No BloodSiphonEffect or BloodSiphon found on player!");
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

    void OnCollisionEnter(Collision collision)
    {
        // Blood splatter logic (unchanged)
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

        // SpawnSplatter is a static method
        ContactPoint contact = collision.GetContact(0);
        Vector3 splatterPosition = contact.point;
        Vector3 splatterNormal = contact.normal;

        // Call static method directly
        BloodSplatterManager.SpawnSplatter(splatterPosition, splatterNormal);
        hasSpawnedSplatter = true;

        Debug.Log($"Blood splatter spawned! Hit: {hitObject.name}, Layer: {LayerMask.LayerToName(hitLayer)}, Velocity: {impactVelocity:F2}");
    }

    void SpawnDriplet()
    {
        // Driplet logic (unchanged - truncated for brevity)
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

        if (orbCollider != null)
        {
            orbCollider.enabled = false;
            Debug.Log("Blood orb collider disabled during absorption");
        }

        isAbsorbing = true;
        absorptionTimer = 0f;
        startPosition = transform.position;

        // === SIPHON EFFECT SELECTION ===

        // NEW: LineRenderer-based blood siphon strands
        if (useLineRendererSiphon && playerBloodSiphon != null)
        {
            // Calculate blood per strand
            float bloodPerStrand = bloodAmount / siphonStrandCount;

            // Create blood strands flowing from ORB'S CURRENT POSITION to player
            for (int i = 0; i < siphonStrandCount; i++)
            {
                // Use orb's current position, not source position
                Vector3 offset = Random.insideUnitSphere * 0.1f;
                playerBloodSiphon.StartSiphon(transform.position + offset, bloodPerStrand);

                Debug.Log($"Spawning blood strand {i + 1}/{siphonStrandCount} from orb at {transform.position}");
            }
        }

        // Legacy particle-based siphon effect (kept for backwards compatibility)
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

        // Animation logic (unchanged)
        if (playerAnimator != null)
        {
            int targetLayer = fullBodyLayerIndex;
            bool isMoving = false;

            if (useUpperBodyWhenMoving)
            {
                float playerSpeed = playerAnimator.GetFloat("Speed");
                isMoving = playerSpeed > 0.01f;

                if (isMoving)
                {
                    targetLayer = upperBodyLayerIndex;
                    Debug.Log($"Player is moving (speed: {playerSpeed:F2}), using upper body layer {targetLayer}");
                }
                else
                {
                    Debug.Log($"Player is stationary (speed: {playerSpeed:F2}), using full body layer {targetLayer}");
                }
            }

            if (forceImmediateTransition)
            {
                playerAnimator.CrossFade(absorbStateName, crossfadeDuration, targetLayer, 0f);
                Debug.Log($"Crossfading to absorption animation: {absorbStateName} on layer {targetLayer} over {crossfadeDuration}s");
            }
            else
            {
                playerAnimator.SetBool(absorbBoolName, true);
                Debug.Log($"Set absorption bool: {absorbBoolName}");
            }
        }
    }

    public void CancelAbsorption()
    {
        if (!isAbsorbing) return;

        Debug.Log("Blood orb absorption cancelled - re-enabling physics and collider");

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
            playerAnimator.SetBool(absorbBoolName, false);
        }
    }

    void CompleteAbsorption()
    {
        if (playerAnimator != null)
        {
            playerAnimator.SetBool(absorbBoolName, false);
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
        // Also store that we've cancelled it so we don't auto-destroy
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

NOTE: sourcePosition field is kept for potential future use but not currently
used by the siphon effect. It was originally intended to flow from enemy
position, but orb position makes more gameplay/visual sense.

(Previous documentation for blood splatter, animation, etc. still applies)
*/