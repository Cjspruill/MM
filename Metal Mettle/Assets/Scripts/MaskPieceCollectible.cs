using UnityEngine;

public class MaskPieceCollectible : MonoBehaviour
{
    [Header("Piece Configuration")]
    public string pieceType;
    public bool isAccessible = false;

    [Header("Feed the Mask Objective")]
    [SerializeField] private MaskFeeder maskFeeder;
    [SerializeField] private bool requiresMaskFed = true;

    [Header("Visual Feedback")]
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.3f;
    [SerializeField] private GameObject glowEffect;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Vector3 startPosition;
    private float bobTimer;

    private void Start()
    {
        startPosition = transform.position;

        // Auto-find mask feeder if not assigned
        if (maskFeeder == null && requiresMaskFed)
        {
            maskFeeder = FindFirstObjectByType<MaskFeeder>();
        }

        if (showDebugLogs)
        {
            Debug.Log($"MaskPieceCollectible '{pieceType}' initialized. Accessible: {isAccessible}. Requires mask fed: {requiresMaskFed}");
        }

        // If we require the mask to be fed, subscribe to its completion event
        if (requiresMaskFed && maskFeeder != null)
        {
            maskFeeder.onMaskFull.AddListener(MakeAccessible);

            if (showDebugLogs)
            {
                Debug.Log($"'{pieceType}' subscribed to mask full event");
            }

            // Check if mask is already full
            if (maskFeeder.IsFull())
            {
                MakeAccessible();
            }
        }
        else if (!requiresMaskFed)
        {
            // If we don't require mask feeding, make it accessible immediately
            isAccessible = true;
        }

        // Set initial glow state
        if (glowEffect != null)
        {
            glowEffect.SetActive(isAccessible);
        }
    }

    private void Update()
    {
        // Rotation
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // Bobbing (commented out in your original)
        bobTimer += Time.deltaTime * bobSpeed;
        Vector3 newPosition = startPosition;
        newPosition.y += Mathf.Sin(bobTimer) * bobHeight;
        // transform.position = newPosition;
    }

    private void MakeAccessible()
    {
        isAccessible = true;

        if (glowEffect != null)
        {
            glowEffect.SetActive(true);
        }

        if (showDebugLogs)
        {
            Debug.Log($"✓ {pieceType} is now ACCESSIBLE!");
        }
    }

    private void OnDestroy()
    {
        if (requiresMaskFed && maskFeeder != null)
        {
            maskFeeder.onMaskFull.RemoveListener(MakeAccessible);
        }
    }
}