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
    public float lifetime = 10f; // Disappears after 10 seconds

    private Transform player;
    private BloodSystem playerBloodSystem;
    private ComboController playerComboController;
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
            // Check if player is blocking - can't absorb while blocking
            bool isPlayerBlocking = playerComboController != null && playerComboController.IsBlocking();

            // Check for absorption input (continuous check if button is held)
            if (controls.Player.Absorb.IsPressed() && !isPlayerBlocking)
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
        isAbsorbing = true;
        absorptionTimer = 0f;
        startPosition = transform.position;

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

        Debug.Log($"Absorbing blood orb: {bloodAmount} blood");
    }

    void CompleteAbsorption()
    {
        // Give blood to player
        if (playerBloodSystem != null)
        {
            playerBloodSystem.GainBlood(bloodAmount);
            Debug.Log($"Player absorbed {bloodAmount} blood!");
        }

        // Destroy orb
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        // Draw absorption range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, absorptionRange);
    }
}