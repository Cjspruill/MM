using UnityEngine;

public class MaskPieceCollectible : MonoBehaviour
{
    [Header("Piece Configuration")]
    public string pieceType;
    public bool isAccessible = false;

    [Header("Glass Protection")]
    [SerializeField] private BreakableGlass protectingGlass;

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

        if (showDebugLogs)
        {
            Debug.Log($"MaskPieceCollectible '{pieceType}' initialized. Accessible: {isAccessible}. Has glass: {protectingGlass != null}");
        }

        // If there's glass, register with it
        if (protectingGlass != null)
        {
            protectingGlass.OnGlassBroken += MakeAccessible;

            if (showDebugLogs)
            {
                Debug.Log($"'{pieceType}' subscribed to glass break event");
            }
        }

        // Set initial glow state
        if (glowEffect != null)
        {
            glowEffect.SetActive(isAccessible);
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

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
        if (protectingGlass != null)
        {
            protectingGlass.OnGlassBroken -= MakeAccessible;
        }
    }
}