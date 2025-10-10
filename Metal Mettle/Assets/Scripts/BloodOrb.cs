using UnityEngine;
using UnityEngine.InputSystem;

public class BloodOrb : MonoBehaviour
{
    [Header("Blood Amount")]
    public float bloodAmount = 25f;

    [Header("Visual Effects")]
    public float pulseSpeed = 2f;
    public float pulseScale = 0.2f;
    public Color orbColor = Color.red;

    [Header("Absorption")]
    public float absorptionRange = 3f;
    public float absorptionDuration = 0.5f;
    public AnimationCurve absorptionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Explosion")]
    public float explosionForce = 300f;
    public float upwardForce = 200f;
    public float randomSpread = 50f;

    [Header("Lifetime")]
    public float lifetime = 10f;

    private Transform player;
    private BloodSystem playerBloodSystem;
    private ComboController playerComboController;
    private Animator playerAnimator;
    private BloodSiphonEffect playerSiphonEffect; // NEW: Reference to siphon effect
    private InputSystem_Actions controls;
    private bool isAbsorbing = false;
    private float absorptionTimer = 0f;
    private Vector3 startScale;
    private Vector3 startPosition;
    private MeshRenderer meshRenderer;
    private Rigidbody rb;
    private Collider orbCollider;

    void Start()
    {
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerBloodSystem = playerObj.GetComponent<BloodSystem>();
            playerComboController = playerObj.GetComponent<ComboController>();
            playerAnimator = playerObj.GetComponent<Animator>();
            playerSiphonEffect = playerObj.GetComponent<BloodSiphonEffect>(); // NEW: Get siphon effect component

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
        startScale = transform.localScale;
        startPosition = transform.position;
        meshRenderer = GetComponent<MeshRenderer>();
        rb = GetComponent<Rigidbody>();
        orbCollider = GetComponent<Collider>();

        // Set orb color
        if (meshRenderer != null)
        {
            meshRenderer.material.color = orbColor;
        }

        // Apply explosion force
        if (rb != null)
        {
            // Random outward direction
            Vector3 randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                0,
                Random.Range(-1f, 1f)
            ).normalized;

            // Add random spread
            randomDirection += Random.insideUnitSphere * (randomSpread / 100f);

            // Combine outward and upward forces
            Vector3 force = (randomDirection * explosionForce) + (Vector3.up * upwardForce);

            rb.AddForce(force);

            // Add slight random rotation
            rb.AddTorque(Random.insideUnitSphere * 50f);
        }

        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (player == null) return;

        // Pulse effect
        if (!isAbsorbing)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            transform.localScale = startScale * (1f + pulse);
        }

        // Check if player is in range and presses absorb button
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= absorptionRange && !isAbsorbing)
        {
            // Check if player is blocking or attacking - can't absorb during combat actions
            bool isPlayerBlocking = playerComboController != null && playerComboController.IsBlocking();
            bool isPlayerAttacking = playerComboController != null && playerComboController.IsAttacking();

            // Check for absorption input (continuous check if button is held)
            if (controls.Player.Absorb.IsPressed() && !isPlayerBlocking && !isPlayerAttacking)
            {
                StartAbsorption();
            }
        }

        // Handle absorption animation
        if (isAbsorbing)
        {
            absorptionTimer += Time.deltaTime;
            float t = absorptionTimer / absorptionDuration;
            t = absorptionCurve.Evaluate(t);

            // Move toward player and scale down
            transform.position = Vector3.Lerp(startPosition, player.position + Vector3.up, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            // When absorption complete
            if (absorptionTimer >= absorptionDuration)
            {
                CompleteAbsorption();
            }
        }
    }

    void StartAbsorption()
    {
        // IMPORTANT: Prevent multiple calls while button is held
        if (isAbsorbing) return;

        isAbsorbing = true;
        absorptionTimer = 0f;
        startPosition = transform.position;

        // NEW: Start the blood siphon effect (called only once)
        if (playerSiphonEffect != null)
        {
            playerSiphonEffect.StartSiphon(transform.position);
            Debug.Log("Started blood siphon effect");
        }

        // Stop physics movement during absorption
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Disable collider to prevent pushing player
        if (orbCollider != null)
        {
            orbCollider.enabled = false;
        }

        // Reset player's combo when absorbing
        if (playerComboController != null && playerComboController.GetComboStep() > 0)
        {
            Debug.Log("Absorption started - resetting player combo");
            // We can't directly call ResetCombo from here, but we can let the combo naturally expire
            // The combo window will handle the reset
        }

        // Set animator IsAbsorbing to true
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("IsAbsorbing", true);
            Debug.Log("Set player IsAbsorbing = true");
        }

        Debug.Log($"Absorbing blood orb: {bloodAmount} blood");
    }

    void CompleteAbsorption()
    {
        // Set animator IsAbsorbing to false
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("IsAbsorbing", false);
            Debug.Log("Set player IsAbsorbing = false");
        }

        // Give blood to player
        if (playerBloodSystem != null)
        {
            playerBloodSystem.GainBlood(bloodAmount);
            Debug.Log($"Player absorbed {bloodAmount} blood!");
        }

        // Destroy orb
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Safety: Make sure IsAbsorbing is set to false if orb is destroyed
        if (isAbsorbing && playerAnimator != null)
        {
            playerAnimator.SetBool("IsAbsorbing", false);
            Debug.Log("BloodOrb destroyed - ensured IsAbsorbing = false");
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw absorption range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, absorptionRange);
    }
}

/*
=== CHANGES MADE ===

1. Added private field:
   - BloodSiphonEffect playerSiphonEffect;

2. In Start():
   - playerSiphonEffect = playerObj.GetComponent<BloodSiphonEffect>();
   - Added warning if component not found

3. In StartAbsorption():
   - Added guard: if (isAbsorbing) return;
   - Calls playerSiphonEffect.StartSiphon(transform.position);

=== WHY THE GUARD IS IMPORTANT ===

Without "if (isAbsorbing) return;", the function could be called multiple times
because IsPressed() returns true every frame the button is held down. This would
create hundreds of particle systems! The guard ensures it only runs once.

=== WHAT TO CHECK ===

1. Make sure BloodSiphonEffect component is on your Player GameObject
2. Make sure you've assigned the particle system prefab in the inspector
3. Make sure you've assigned the mouth target transform
4. Test with one orb first to verify it creates the correct number of strands
*/
