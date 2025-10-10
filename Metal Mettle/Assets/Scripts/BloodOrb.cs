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

    [Header("Absorption")]
    public float absorptionRange = 3f;
    public float absorptionDuration = 0.5f;
    public AnimationCurve absorptionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

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
    private BloodSiphonEffect playerSiphonEffect;
    private InputSystem_Actions controls;
    private bool isAbsorbing = false;
    private float absorptionTimer = 0f;
    private Vector3 startScale;
    private Vector3 startPosition;
    private MeshRenderer meshRenderer;
    private Rigidbody rb;
    private Collider orbCollider;
    private Vector3 lastPosition;
    private float dripTimer = 0f;
    private MaterialPropertyBlock propBlock;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerBloodSystem = playerObj.GetComponent<BloodSystem>();
            playerComboController = playerObj.GetComponent<ComboController>();
            playerAnimator = playerObj.GetComponent<Animator>();
            playerSiphonEffect = playerObj.GetComponent<BloodSiphonEffect>();

            if (playerAnimator == null)
            {
                Debug.LogWarning("BloodOrb: Player Animator not found!");
            }

            if (playerSiphonEffect == null)
            {
                Debug.LogWarning("BloodOrb: BloodSiphonEffect not found on player!");
            }
        }

        controls = InputManager.Instance.Controls;

        float randomSize = Random.Range(minSize, maxSize);
        transform.localScale = Vector3.one * randomSize;

        startScale = transform.localScale;
        startPosition = transform.position;
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
        Destroy(gameObject, lifetime);
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
            float t = absorptionTimer / absorptionDuration;
            t = absorptionCurve.Evaluate(t);

            transform.position = Vector3.Lerp(startPosition, player.position + Vector3.up, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            if (absorptionTimer >= absorptionDuration)
            {
                CompleteAbsorption();
            }
        }

        lastPosition = transform.position;
    }

    void SpawnDriplet()
    {
        GameObject droplet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        droplet.name = "BloodDriplet";
        droplet.transform.position = transform.position;
        droplet.transform.localScale = startScale * Random.Range(0.2f, 0.4f);

        Renderer dropletRenderer = droplet.GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            dropletRenderer.material = new Material(meshRenderer.material);
        }

        Destroy(droplet.GetComponent<Collider>());

        Rigidbody dropletRb = droplet.AddComponent<Rigidbody>();
        dropletRb.mass = 0.1f;
        dropletRb.linearDamping = 2f;
        dropletRb.useGravity = true;

        if (rb != null)
        {
            dropletRb.linearVelocity = rb.linearVelocity * 0.5f + Vector3.down * 0.5f;
        }

        StartCoroutine(FadeDriplet(droplet, dropletRenderer));
    }

    System.Collections.IEnumerator FadeDriplet(GameObject droplet, Renderer renderer)
    {
        float fadeTime = 0.5f;
        float elapsed = 0f;
        Color startColor = orbColor;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);
            Color newColor = startColor;
            newColor.a = alpha;

            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = newColor;
            }

            yield return null;
        }

        if (droplet != null)
            Destroy(droplet);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isAbsorbing) return;

        if (rb != null && collision.relativeVelocity.magnitude > 2f)
        {
            rb.linearVelocity *= (1f - surfaceStickiness);

            if (viscosity > 0f)
            {
                Vector3 impactNormal = collision.contacts[0].normal;
                StartCoroutine(ImpactDeformation(impactNormal));
            }
        }
    }

    System.Collections.IEnumerator ImpactDeformation(Vector3 impactNormal)
    {
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float squash = Mathf.Sin(t * Mathf.PI) * viscosity * 0.3f;
            Vector3 deformScale = startScale - impactNormal * squash;
            transform.localScale = deformScale;

            yield return null;
        }

        transform.localScale = startScale;
    }

    void StartAbsorption()
    {
        if (isAbsorbing) return;

        isAbsorbing = true;
        absorptionTimer = 0f;
        startPosition = transform.position;

        if (playerSiphonEffect != null)
        {
            playerSiphonEffect.StartSiphon(transform.position);
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (orbCollider != null)
        {
            orbCollider.enabled = false;
            Debug.Log("Blood orb collider disabled during absorption");
        }

        // FORCE IMMEDIATE ANIMATION - plays NOW with smooth crossfade, loops continuously
        if (playerAnimator != null)
        {
            // Determine which layer to use based on player movement
            int targetLayer = fullBodyLayerIndex;
            bool isMoving = false;

            if (useUpperBodyWhenMoving)
            {
                // Check if player is moving by reading Speed parameter from animator
                float playerSpeed = playerAnimator.GetFloat("Speed");
                isMoving = playerSpeed > 0.01f; // Small threshold to avoid floating point errors

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
                // CrossFade for smooth blend between current animation and absorb
                playerAnimator.CrossFade(absorbStateName, crossfadeDuration, targetLayer, 0f);
                Debug.Log($"Crossfading to absorption animation: {absorbStateName} on layer {targetLayer} over {crossfadeDuration}s");
            }
            else
            {
                // Standard bool method (relies on proper Any State setup)
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

        // Stop the looping animation
        if (playerAnimator != null)
        {
            playerAnimator.SetBool(absorbBoolName, false);
        }
    }

    void CompleteAbsorption()
    {
        // Stop the looping animation
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, absorptionRange);
    }
}

/*
=== ANIMATION FIX ===

TWO MODES:

1. FORCE IMMEDIATE CROSSFADE (RECOMMENDED):
   - forceImmediateTransition = true
   - absorbStateName = "Absorb" (exact name in Animator)
   - crossfadeDuration = 0.15 (smooth blend)
   - Uses Animator.CrossFade() to smoothly blend into animation
   - Bypasses all transition rules
   - Animation loops continuously until bool = false

2. STANDARD BOOL:
   - forceImmediateTransition = false
   - Uses SetBool() with Any State transition
   - Requires proper Animator setup (see below)

=== LAYER SYSTEM ===

DYNAMIC LAYER SELECTION:
- useUpperBodyWhenMoving = true
- When player Speed > 0: Uses upperBodyLayerIndex (default: 1)
- When player Speed = 0: Uses fullBodyLayerIndex (default: 0)
- Allows absorption while walking/running without stopping movement

=== ANIMATOR SETUP ===

For FORCE IMMEDIATE mode with layers:

BASE LAYER (Layer 0 - Full Body):
1. Create "IsAbsorbing" Bool parameter
2. Create "Absorb" state with Loop Time ON
3. Any State → Absorb: IsAbsorbing = true, Exit Time OFF, Duration 0
4. Absorb → Idle: IsAbsorbing = false, Exit Time OFF

UPPER BODY LAYER (Layer 1):
1. Create new layer called "Upper Body"
2. Set Weight to 1.0
3. Set Blending to "Override"
4. Use Avatar Mask that only includes upper body bones
5. Duplicate the same "Absorb" state in this layer with Loop Time ON
6. Same transitions as Base Layer

The script automatically detects player movement and chooses the correct layer!

=== CROSSFADE DURATION GUIDE ===
- 0.0s = Instant snap (no blend)
- 0.1s = Very quick blend
- 0.15s = Smooth natural blend (RECOMMENDED)
- 0.2-0.3s = Slower, more noticeable blend
- 0.5s+ = Very slow, cinematic blend

=== HOW IT WORKS ===
1. Script reads "Speed" parameter from animator
2. If Speed > 0.01: Player is moving → use upper body layer
3. If Speed ≤ 0.01: Player is stationary → use full body layer
4. Legs continue locomotion animation while torso/arms absorb blood
*/