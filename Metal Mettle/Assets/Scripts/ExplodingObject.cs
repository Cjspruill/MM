using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Explodes an object into pieces and reassembles it over time.
/// Attach this script to a parent GameObject that contains child objects to explode.
/// </summary>
public class ExplodingObject : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Duration of the explosion animation in seconds")]
    [SerializeField] private float explosionDuration = 2f;

    [Tooltip("Duration of the reassembly animation in seconds")]
    [SerializeField] private float reassemblyDuration = 2f;

    [Tooltip("How far pieces fly from center")]
    [SerializeField] private float explosionForce = 5f;

    [Tooltip("Random rotation applied to pieces during explosion")]
    [SerializeField] private float rotationAmount = 360f;

    [Tooltip("Start animation automatically on play")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("Loop the animation continuously")]
    [SerializeField] private bool loop = true;

    [Tooltip("Delay before starting loop again")]
    [SerializeField] private float loopDelay = 1f;

    [Tooltip("Recalculate explosion directions each loop for varied patterns")]
    [SerializeField] private bool randomizeEachLoop = true;

    [Header("Animation Curve")]
    [Tooltip("Custom animation curve for easing")]
    [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Anchor Compensation")]
    [Tooltip("Use world space center instead of local pivot for explosion direction")]
    [SerializeField] private bool useWorldSpaceCenter = true;

    [Tooltip("Visualize explosion directions in Scene view (for debugging)")]
    [SerializeField] private bool debugExplosionDirections = false;

    // Private variables
    private List<PieceData> pieces = new List<PieceData>();
    private bool isAnimating = false;
    private Coroutine currentAnimation;
    private Vector3 worldSpaceCenter;

    // Data structure to store piece information
    private class PieceData
    {
        public Transform transform;
        public Vector3 originalLocalPosition;
        public Quaternion originalLocalRotation;
        public Vector3 targetPosition;
        public Quaternion targetRotation;
        public Vector3 worldSpaceCenter; // For pieces with off-center anchors
        public Vector3 explosionDirection;
    }

    void Start()
    {
        InitializePieces();

        if (autoStart)
        {
            StartExplosion();
        }
    }

    /// <summary>
    /// Stores the original positions and rotations of all child objects
    /// and calculates their explosion targets
    /// </summary>
    /// <param name="recalculateRandom">If true, generates new random directions and rotations</param>
    void InitializePieces(bool recalculateRandom = true)
    {
        // If we're not recalculating and pieces already exist, just return
        if (!recalculateRandom && pieces.Count > 0)
            return;

        // Only clear if recalculating or first time
        if (recalculateRandom || pieces.Count == 0)
        {
            pieces.Clear();
        }

        // Calculate the center point of the parent object
        worldSpaceCenter = transform.position;

        // Get all child transforms
        foreach (Transform child in transform)
        {
            PieceData piece = new PieceData
            {
                transform = child,
                originalLocalPosition = child.localPosition,
                originalLocalRotation = child.localRotation
            };

            // Calculate the visual center of the piece (handles off-center anchors)
            Vector3 pieceCenter;
            if (useWorldSpaceCenter)
            {
                // Try to get the renderer bounds for accurate center
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    pieceCenter = renderer.bounds.center;
                }
                else
                {
                    // Fallback to transform position
                    pieceCenter = child.position;
                }
            }
            else
            {
                // Use transform position (original behavior)
                pieceCenter = child.position;
            }

            piece.worldSpaceCenter = pieceCenter;

            // Calculate explosion direction from parent center to piece center
            Vector3 direction = (pieceCenter - worldSpaceCenter).normalized;

            if (direction == Vector3.zero)
                direction = Random.onUnitSphere;

            // Add some randomness to make it look more natural
            direction += Random.onUnitSphere * 0.3f;
            direction.Normalize();

            piece.explosionDirection = direction;

            // Calculate target position in local space
            // We need to account for the offset between the pivot and visual center
            Vector3 pivotToCenter = pieceCenter - child.position;
            Vector3 targetWorldPos = worldSpaceCenter + direction * explosionForce + pivotToCenter;
            piece.targetPosition = transform.InverseTransformPoint(targetWorldPos);

            // Calculate random target rotation
            Vector3 randomRotation = new Vector3(
                Random.Range(-rotationAmount, rotationAmount),
                Random.Range(-rotationAmount, rotationAmount),
                Random.Range(-rotationAmount, rotationAmount)
            );
            piece.targetRotation = piece.originalLocalRotation * Quaternion.Euler(randomRotation);

            pieces.Add(piece);
        }
    }

    /// <summary>
    /// Starts the explosion and reassembly animation
    /// </summary>
    public void StartExplosion()
    {
        if (isAnimating)
            return;

        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(ExplosionSequence());
    }

    /// <summary>
    /// Immediately resets all pieces to their original positions
    /// </summary>
    public void ResetPieces()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        isAnimating = false;

        foreach (var piece in pieces)
        {
            piece.transform.localPosition = piece.originalLocalPosition;
            piece.transform.localRotation = piece.originalLocalRotation;
        }
    }

    /// <summary>
    /// Reinitializes the pieces (useful if children have changed)
    /// </summary>
    public void ReinitializePieces()
    {
        ResetPieces();
        InitializePieces();
    }

    /// <summary>
    /// Main animation coroutine that handles explosion and reassembly
    /// </summary>
    IEnumerator ExplosionSequence()
    {
        isAnimating = true;

        // Recalculate explosion pattern if enabled
        if (randomizeEachLoop)
        {
            InitializePieces(true);
        }

        // Explosion phase
        yield return StartCoroutine(AnimatePieces(true, explosionDuration));

        // Reassembly phase
        yield return StartCoroutine(AnimatePieces(false, reassemblyDuration));

        isAnimating = false;

        // Loop if enabled
        if (loop)
        {
            yield return new WaitForSeconds(loopDelay);
            StartExplosion();
        }
    }

    /// <summary>
    /// Animates pieces either exploding or reassembling
    /// </summary>
    /// <param name="explode">True for explosion, false for reassembly</param>
    /// <param name="duration">Duration of the animation</param>
    IEnumerator AnimatePieces(bool explode, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            // Apply easing curve
            float easedProgress = easingCurve.Evaluate(progress);

            // If reassembling, reverse the progress
            if (!explode)
                easedProgress = 1f - easedProgress;

            // Update all pieces
            foreach (var piece in pieces)
            {
                // Interpolate position
                piece.transform.localPosition = Vector3.Lerp(
                    piece.originalLocalPosition,
                    piece.targetPosition,
                    easedProgress
                );

                // Interpolate rotation
                piece.transform.localRotation = Quaternion.Lerp(
                    piece.originalLocalRotation,
                    piece.targetRotation,
                    easedProgress
                );
            }

            yield return null;
        }

        // Ensure final positions are exact
        float finalProgress = explode ? 1f : 0f;
        foreach (var piece in pieces)
        {
            piece.transform.localPosition = Vector3.Lerp(
                piece.originalLocalPosition,
                piece.targetPosition,
                finalProgress
            );

            piece.transform.localRotation = Quaternion.Lerp(
                piece.originalLocalRotation,
                piece.targetRotation,
                finalProgress
            );
        }
    }

    // Public getters for runtime modification
    public void SetExplosionDuration(float duration) => explosionDuration = duration;
    public void SetReassemblyDuration(float duration) => reassemblyDuration = duration;
    public void SetExplosionForce(float force)
    {
        explosionForce = force;
        InitializePieces(true); // Recalculate target positions
    }
    public void SetRotationAmount(float amount)
    {
        rotationAmount = amount;
        InitializePieces(true); // Recalculate target rotations
    }
    public void SetLoop(bool shouldLoop) => loop = shouldLoop;
    public void SetRandomizeEachLoop(bool shouldRandomize) => randomizeEachLoop = shouldRandomize;
    public bool IsAnimating() => isAnimating;

    // Draw debug gizmos to visualize explosion directions
    void OnDrawGizmos()
    {
        if (!debugExplosionDirections || pieces.Count == 0)
            return;

        Gizmos.color = Color.yellow;

        // Draw the center point
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        foreach (var piece in pieces)
        {
            if (piece.transform == null)
                continue;

            // Draw line from center to piece visual center
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, piece.worldSpaceCenter);

            // Draw small sphere at visual center
            Gizmos.DrawWireSphere(piece.worldSpaceCenter, 0.1f);

            // Draw explosion direction
            Gizmos.color = Color.red;
            Vector3 explosionEnd = transform.position + piece.explosionDirection * explosionForce;
            Gizmos.DrawLine(transform.position, explosionEnd);
            Gizmos.DrawWireSphere(explosionEnd, 0.15f);
        }
    }
}