using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Creates a blood siphon effect with flowing strands instead of orbs.
/// Replaces the orb absorption system with visual blood streams.
/// </summary>
public class BloodSiphon : MonoBehaviour
{
    [Header("Siphon Settings")]
    [SerializeField] private Transform mouthTarget; // Where blood flows to (mask's mouth)
    [SerializeField] private float siphonDuration = 0.8f; // How long each strand takes
    [SerializeField] private int strandsPerSource = 3; // Number of blood strands per enemy
    [SerializeField] private float strandSpacing = 0.15f; // Time between strand spawns

    [Header("Strand Appearance")]
    [SerializeField] private Material bloodMaterial; // Material for the line renderer
    [SerializeField] private AnimationCurve strandWidth = AnimationCurve.Constant(0, 1, 0.1f);
    [SerializeField] private Gradient bloodColor;
    [SerializeField] private int strandSegments = 20; // Resolution of the strand

    [Header("Flow Behavior")]
    [SerializeField] private float flowSpeed = 5f; // How fast blood moves along strand
    [SerializeField] private float strandWaveAmplitude = 0.3f; // Wave motion intensity
    [SerializeField] private float strandWaveFrequency = 2f; // Wave motion speed
    [SerializeField] private AnimationCurve flowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip siphonSound;
    [SerializeField] private float soundVolume = 0.5f;

    private List<BloodStrand> activeStrands = new List<BloodStrand>();

    private void Awake()
    {
        if (mouthTarget == null)
        {
            // Try to find the mouth target (you may need to adjust this)
            mouthTarget = transform.Find("MouthTarget");
            if (mouthTarget == null)
            {
                Debug.LogWarning("No mouth target assigned! Creating default at player position.");
                GameObject go = new GameObject("MouthTarget");
                go.transform.parent = transform;
                go.transform.localPosition = new Vector3(0, 1.5f, 0.3f); // Adjust for your character
                mouthTarget = go.transform;
            }
        }

        // Initialize default gradient if not set
        if (bloodColor == null || bloodColor.colorKeys.Length == 0)
        {
            bloodColor = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(new Color(0.8f, 0, 0), 0f); // Bright red
            colorKeys[1] = new GradientColorKey(new Color(0.5f, 0, 0), 1f); // Dark red

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
            alphaKeys[0] = new GradientAlphaKey(0f, 0f); // Fade in
            alphaKeys[1] = new GradientAlphaKey(1f, 0.5f); // Full opacity
            alphaKeys[2] = new GradientAlphaKey(0f, 1f); // Fade out

            bloodColor.SetKeys(colorKeys, alphaKeys);
        }
    }

    /// <summary>
    /// Call this to create blood siphon effect from a source (enemy position)
    /// </summary>
    /// <param name="sourcePosition">Where the blood originates (enemy position)</param>
    /// <param name="bloodAmount">Amount of blood to give (from your existing system)</param>
    public void StartSiphon(Vector3 sourcePosition, float bloodAmount)
    {
        StartCoroutine(SpawnStrandsCoroutine(sourcePosition, bloodAmount));

        // Play siphon sound
        if (audioSource != null && siphonSound != null)
        {
            audioSource.PlayOneShot(siphonSound, soundVolume);
        }
    }

    private IEnumerator SpawnStrandsCoroutine(Vector3 sourcePosition, float bloodAmount)
    {
        // Calculate blood per strand
        float bloodPerStrand = bloodAmount / strandsPerSource;

        for (int i = 0; i < strandsPerSource; i++)
        {
            // Add slight randomization to source position
            Vector3 randomOffset = Random.insideUnitSphere * 0.3f;
            Vector3 strandSource = sourcePosition + randomOffset;

            // Create the strand
            CreateBloodStrand(strandSource, bloodPerStrand);

            // Wait before spawning next strand
            yield return new WaitForSeconds(strandSpacing);
        }
    }

    private void CreateBloodStrand(Vector3 sourcePosition, float bloodAmount)
    {
        // Create GameObject for this strand
        GameObject strandObj = new GameObject("BloodStrand");
        strandObj.transform.position = sourcePosition;

        // Add LineRenderer component
        LineRenderer lineRenderer = strandObj.AddComponent<LineRenderer>();
        lineRenderer.material = bloodMaterial;
        lineRenderer.widthCurve = strandWidth;
        lineRenderer.colorGradient = bloodColor;
        lineRenderer.positionCount = strandSegments;
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 5;
        lineRenderer.numCornerVertices = 5;

        // IMPORTANT: Initialize all positions at source to prevent straight line artifact
        for (int i = 0; i < strandSegments; i++)
        {
            lineRenderer.SetPosition(i, sourcePosition);
        }

        // Create strand data
        BloodStrand strand = new BloodStrand
        {
            gameObject = strandObj,
            lineRenderer = lineRenderer,
            sourcePosition = sourcePosition,
            bloodAmount = bloodAmount,
            elapsedTime = 0f,
            isComplete = false,
            startTime = Time.time, // Track when strand was created
            wavePhaseOffset = Random.Range(0f, Mathf.PI * 2f) // Random starting phase
        };

        activeStrands.Add(strand);

        // Start the strand animation
        StartCoroutine(AnimateStrand(strand));
    }

    private IEnumerator AnimateStrand(BloodStrand strand)
    {
        while (strand.elapsedTime < siphonDuration)
        {
            strand.elapsedTime += Time.deltaTime;
            float normalizedTime = strand.elapsedTime / siphonDuration;
            float flowProgress = flowCurve.Evaluate(normalizedTime);

            // Update strand positions
            UpdateStrandPositions(strand, flowProgress);

            yield return null;
        }

        // Strand complete - give blood to player
        OnStrandComplete(strand);

        // Cleanup
        activeStrands.Remove(strand);
        Destroy(strand.gameObject);
    }

    private void UpdateStrandPositions(BloodStrand strand, float flowProgress)
    {
        Vector3 start = strand.sourcePosition;
        Vector3 end = mouthTarget.position;

        // Direction vector for perpendicular wave motion
        Vector3 direction = end - start;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;

        // If perpendicular is zero (vertical strand), use forward instead
        if (perpendicular.sqrMagnitude < 0.01f)
        {
            perpendicular = Vector3.Cross(direction, Vector3.forward).normalized;
        }

        // Two-phase animation:
        // Phase 1 (0.0 - 0.5): Strand grows from source to target
        // Phase 2 (0.5 - 1.0): Strand shrinks from source (tail follows)

        int visibleStartSegment = 0;
        int visibleEndSegment = strandSegments;

        if (flowProgress <= 0.5f)
        {
            // PHASE 1: Growing phase
            float growProgress = flowProgress * 2f; // 0 to 1
            visibleEndSegment = Mathf.CeilToInt(strandSegments * growProgress);
        }
        else
        {
            // PHASE 2: Shrinking from back phase (tail follows)
            float shrinkProgress = (flowProgress - 0.5f) * 2f; // 0 to 1
            visibleStartSegment = Mathf.FloorToInt(strandSegments * shrinkProgress);
            visibleEndSegment = strandSegments;
        }

        // Clamp values
        visibleStartSegment = Mathf.Clamp(visibleStartSegment, 0, strandSegments - 1);
        visibleEndSegment = Mathf.Clamp(visibleEndSegment, 1, strandSegments);

        int visibleCount = visibleEndSegment - visibleStartSegment;
        visibleCount = Mathf.Max(1, visibleCount);

        // Set LineRenderer to only show visible segments
        strand.lineRenderer.positionCount = visibleCount;

        // Update visible segment positions
        for (int i = 0; i < visibleCount; i++)
        {
            // Map to original segment index
            int originalIndex = visibleStartSegment + i;

            // Calculate t based on original segment position
            float t = originalIndex / (float)(strandSegments - 1);

            // Base position along the path
            Vector3 position = Vector3.Lerp(start, end, t);

            // Add wave motion for organic feel
            float waveTime = strand.elapsedTime * strandWaveFrequency;
            float segmentPhase = originalIndex * 0.5f;
            float waveOffset = Mathf.Sin(waveTime + strand.wavePhaseOffset + segmentPhase) * strandWaveAmplitude;

            // Reduce wave amplitude as strand reaches target (more stable near mouth)
            float waveReduction = 1f - (t * 0.5f);

            // Apply wave perpendicular to strand direction
            position += perpendicular * waveOffset * waveReduction;

            strand.lineRenderer.SetPosition(i, position);
        }
    }

    private void OnStrandComplete(BloodStrand strand)
    {
        // Interface with your existing blood system
        BloodSystem bloodSystem = GetComponent<BloodSystem>();
        if (bloodSystem != null)
        {
            bloodSystem.GainBlood(strand.bloodAmount);
        }
        else
        {
            Debug.LogWarning("No BloodSystem found! Blood amount not added.");
        }

        // Optional: Spawn impact effect at mouth
        // SpawnMouthImpactEffect();
    }

    private void OnDestroy()
    {
        // Cleanup all active strands
        foreach (var strand in activeStrands)
        {
            if (strand.gameObject != null)
            {
                Destroy(strand.gameObject);
            }
        }
        activeStrands.Clear();
    }

    // Optional: Visual effect when blood reaches mouth
    private void SpawnMouthImpactEffect()
    {
        // Add particles, flash, or other effects here
    }

    // Data class for tracking individual strands
    private class BloodStrand
    {
        public GameObject gameObject;
        public LineRenderer lineRenderer;
        public Vector3 sourcePosition;
        public float bloodAmount;
        public float elapsedTime;
        public bool isComplete;
        public float startTime; // When this strand was created
        public float wavePhaseOffset; // Random phase for wave motion
    }
}