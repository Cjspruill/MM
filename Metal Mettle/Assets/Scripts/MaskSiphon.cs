using UnityEngine;

/// <summary>
/// Extends BloodOrb to add mask-siphoning functionality.
/// Add this component ALONGSIDE BloodOrb on your blood orb prefab.
/// If player doesn't absorb the orb, it will automatically siphon to the mask.
/// </summary>
public class MaskSiphon : MonoBehaviour
{
    [Header("Mask Siphon Settings")]
    [SerializeField] private float maskSiphonRange = 15f; // Range to start siphoning to mask
    [SerializeField] private float maskSiphonSpeed = 2f; // How fast it moves to mask
    [SerializeField] private float maskSiphonDelay = 2f; // Delay before siphoning starts
    [SerializeField] private float bloodValue = 1f; // How much blood this orb gives to mask

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // References
    private MaskFeeder maskFeeder;
    private BloodOrb bloodOrb;
    private Rigidbody rb;
    private Collider orbCollider;
    private MeshRenderer meshRenderer;

    // State
    private bool isSiphoningToMask = false;
    private bool isBeingDestroyed = false;
    private float siphonDelayTimer = 0f;
    private Vector3 startScale;

    private void Start()
    {
        // Find the mask feeder in the scene
        maskFeeder = FindFirstObjectByType<MaskFeeder>();

        // Get components
        bloodOrb = GetComponent<BloodOrb>();
        rb = GetComponent<Rigidbody>();
        orbCollider = GetComponent<Collider>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Store initial scale
        startScale = transform.localScale;

        if (maskFeeder == null && showDebugLogs)
        {
            Debug.LogWarning("MaskSiphon: No MaskFeeder found in scene!");
        }
    }

    private void Update()
    {
        // If the orb is destroyed or mask is null, stop
        if (bloodOrb == null || maskFeeder == null || isBeingDestroyed) return;

        // CRITICAL: Don't siphon if mask objective isn't active
        if (!maskFeeder.IsObjectiveActive())
        {
            siphonDelayTimer = 0f;
            return;
        }

        // If player is absorbing, don't siphon to mask
        if (bloodOrb.IsAbsorbing())
        {
            siphonDelayTimer = 0f;
            return;
        }

        // Don't continue if already siphoning
        if (isSiphoningToMask)
        {
            UpdateSiphonMovement();
            return;
        }

        // Handle mask siphoning
        float distanceToMask = Vector3.Distance(transform.position, maskFeeder.transform.position);

        if (distanceToMask <= maskSiphonRange)
        {
            siphonDelayTimer += Time.deltaTime;

            if (siphonDelayTimer >= maskSiphonDelay)
            {
                StartMaskSiphon();
            }
        }
        else
        {
            siphonDelayTimer = 0f; // Reset if we move out of range
        }
    }

    private void UpdateSiphonMovement()
    {
        // Update siphon to mask
        Vector3 targetPos = maskFeeder.transform.position;
        float step = maskSiphonSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

        // Shrink as it gets closer
        float distanceToMask = Vector3.Distance(transform.position, maskFeeder.transform.position);
        float shrinkFactor = Mathf.Clamp01(distanceToMask / 2f);
        transform.localScale = startScale * shrinkFactor;

        // Complete when close enough
        if (distanceToMask < 0.5f)
        {
            CompleteMaskSiphon();
        }
    }

    private void StartMaskSiphon()
    {
        if (isSiphoningToMask) return;

        if (showDebugLogs)
        {
            Debug.Log("Starting mask siphon of BloodOrb");
        }

        isSiphoningToMask = true;

        // Cancel the BloodOrb's automatic destruction
        if (bloodOrb != null)
        {
            bloodOrb.CancelLifetimeDestroy();
        }

        // Disable physics
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Disable collision to prevent player absorption during siphon
        if (orbCollider != null)
        {
            orbCollider.enabled = false;
        }
    }

    private void CompleteMaskSiphon()
    {
        if (isBeingDestroyed) return;
        isBeingDestroyed = true;

        if (showDebugLogs)
        {
            Debug.Log($"Mask absorbed BloodOrb - adding blood: {bloodValue}");
        }

        // Give blood to mask
        if (maskFeeder != null)
        {
            maskFeeder.AddBlood(bloodValue);
        }

        // Destroy the orb
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        // Mask siphon range
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, maskSiphonRange);
    }
}