using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Creates a continuous blood stream effect from orb to mouth during absorption
/// Particles spawn continuously along the path to create visible strands
/// </summary>
public class BloodSiphonEffect : MonoBehaviour
{
    [Header("Particle System References")]
    [Tooltip("Material to use for blood particles (create an Unlit/Color material)")]
    public Material particleMaterial;

    [Tooltip("Transform of the player's mouth (mask jaw position)")]
    public Transform mouthTarget;

    [Tooltip("How many particle streams to create per orb")]
    [Range(1, 10)]
    public int streamsPerOrb = 3;

    [Tooltip("Duration of the siphon effect (should match absorption duration)")]
    [Range(0.3f, 2f)]
    public float siphonDuration = 0.5f;

    [Header("Particle Spawning")]
    [Tooltip("How many particles to spawn per second along the stream")]
    [Range(10f, 100f)]
    public float particlesPerSecond = 50f;

    [Tooltip("How fast particles move along the path")]
    [Range(1f, 20f)]
    public float particleSpeed = 10f;

    [Header("Visual Settings")]
    [Tooltip("Starting size of particles")]
    [Range(0.05f, 0.5f)]
    public float particleSize = 0.15f;

    [Tooltip("Starting color of the blood (bright red)")]
    public Color bloodColorStart = new Color(0.8f, 0f, 0f);

    [Tooltip("Ending color of the blood (dark red)")]
    public Color bloodColorEnd = new Color(0.2f, 0f, 0f);

    [Header("Path Settings")]
    [Tooltip("How much the streams curve")]
    [Range(0f, 2f)]
    public float curvature = 0.5f;

    [Tooltip("Random variation in stream paths")]
    [Range(0f, 1f)]
    public float pathVariation = 0.3f;

    [Header("Debug")]
    [Tooltip("Draw gizmos showing the flow paths")]
    public bool showDebugPaths = true;

    // Active siphon streams
    private List<ActiveStream> activeStreams = new List<ActiveStream>();

    private class ActiveStream
    {
        public Transform source; // Where particles spawn (moves from orb to mouth)
        public Vector3 targetPosition; // Mouth position
        public float timer;
        public float duration;
        public Vector3 pathOffset; // Random curve offset
        public List<BloodParticle> particles = new List<BloodParticle>();
        public float spawnTimer;
    }

    private class BloodParticle
    {
        public GameObject gameObject;
        public float lifetime;
        public float maxLifetime;
        public Vector3 startPos;
        public Vector3 endPos;
        public Vector3 curveOffset;
    }

    void Update()
    {
        // Update all active streams
        for (int i = activeStreams.Count - 1; i >= 0; i--)
        {
            UpdateStream(activeStreams[i]);

            // Remove completed streams
            if (activeStreams[i].timer >= activeStreams[i].duration)
            {
                CleanupStream(activeStreams[i]);
                activeStreams.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Start siphoning blood from an orb
    /// Call this when absorption starts
    /// </summary>
    public void StartSiphon(Vector3 orbPosition)
    {
        for (int i = 0; i < streamsPerOrb; i++)
        {
            StartCoroutine(CreateStreamDelayed(orbPosition, i * 0.05f));
        }
    }

    private IEnumerator CreateStreamDelayed(Vector3 startPosition, float delay)
    {
        yield return new WaitForSeconds(delay);
        CreateStream(startPosition);
    }

    private void CreateStream(Vector3 startPosition)
    {
        // Create source transform that will move from orb to mouth
        GameObject sourceObj = new GameObject("BloodStreamSource");
        sourceObj.transform.position = startPosition;

        ActiveStream stream = new ActiveStream
        {
            source = sourceObj.transform,
            targetPosition = mouthTarget.position,
            timer = 0f,
            duration = siphonDuration,
            pathOffset = new Vector3(
                Random.Range(-pathVariation, pathVariation),
                Random.Range(-pathVariation, pathVariation),
                Random.Range(-pathVariation, pathVariation)
            ),
            spawnTimer = 0f
        };

        activeStreams.Add(stream);
        Debug.Log($"Created blood stream from {startPosition}");
    }

    private void UpdateStream(ActiveStream stream)
    {
        stream.timer += Time.deltaTime;
        float progress = stream.timer / stream.duration;

        if (mouthTarget == null || stream.source == null)
            return;

        // Move the source position from orb toward mouth over time
        Vector3 startPos = stream.source.position;
        Vector3 endPos = mouthTarget.position;

        // Calculate current source position (moves along path)
        Vector3 midPoint = Vector3.Lerp(stream.source.position, endPos, 0.5f) + stream.pathOffset * curvature;
        Vector3 currentSourcePos = GetBezierPoint(stream.source.position, midPoint, endPos, progress);

        // Spawn new particles continuously
        stream.spawnTimer += Time.deltaTime;
        float spawnInterval = 1f / particlesPerSecond;

        while (stream.spawnTimer >= spawnInterval)
        {
            stream.spawnTimer -= spawnInterval;
            SpawnParticle(stream, currentSourcePos, endPos);
        }

        // Update existing particles
        for (int i = stream.particles.Count - 1; i >= 0; i--)
        {
            UpdateParticle(stream.particles[i]);

            // Remove dead particles
            if (stream.particles[i].lifetime >= stream.particles[i].maxLifetime)
            {
                if (stream.particles[i].gameObject != null)
                    Destroy(stream.particles[i].gameObject);
                stream.particles.RemoveAt(i);
            }
        }
    }

    private void SpawnParticle(ActiveStream stream, Vector3 spawnPosition, Vector3 targetPosition)
    {
        // Create particle GameObject
        GameObject particleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        particleObj.name = "BloodParticle";
        particleObj.transform.position = spawnPosition;
        particleObj.transform.localScale = Vector3.one * particleSize;

        // IMPORTANT: Remove collider IMMEDIATELY to prevent physics interactions
        Collider collider = particleObj.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider); // Use DestroyImmediate instead of Destroy
        }

        // Also remove any Rigidbody if present
        Rigidbody rb = particleObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            DestroyImmediate(rb);
        }

        // Put particles on a separate layer that doesn't collide with player
        particleObj.layer = LayerMask.NameToLayer("Ignore Raycast"); // Or create a custom "BloodParticles" layer

        // Set material
        Renderer renderer = particleObj.GetComponent<Renderer>();

        if (particleMaterial != null)
        {
            renderer.material = particleMaterial;
        }
        else
        {
            Debug.LogWarning("BloodSiphonEffect: No particle material assigned! Please create and assign a material.");
            Shader shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                renderer.material = new Material(shader);
                renderer.material.color = bloodColorStart;
            }
        }

        // Calculate travel time based on distance and speed
        float distance = Vector3.Distance(spawnPosition, targetPosition);
        float travelTime = distance / particleSpeed;

        BloodParticle particle = new BloodParticle
        {
            gameObject = particleObj,
            lifetime = 0f,
            maxLifetime = travelTime,
            startPos = spawnPosition,
            endPos = targetPosition,
            curveOffset = stream.pathOffset
        };

        stream.particles.Add(particle);
    }
    private void UpdateParticle(BloodParticle particle)
    {
        particle.lifetime += Time.deltaTime;
        float progress = Mathf.Clamp01(particle.lifetime / particle.maxLifetime);

        // Move along curved path
        Vector3 midPoint = Vector3.Lerp(particle.startPos, particle.endPos, 0.5f) + particle.curveOffset * curvature;
        Vector3 newPos = GetBezierPoint(particle.startPos, midPoint, particle.endPos, progress);

        particle.gameObject.transform.position = newPos;

        // Darken color as it approaches mouth (bright red -> dark red)
        Renderer renderer = particle.gameObject.GetComponent<Renderer>();
        Color currentColor = Color.Lerp(bloodColorStart, bloodColorEnd, progress);

        // Update material color
        renderer.material.color = currentColor;

        // Shrink near end
        float scale = Mathf.Lerp(particleSize, particleSize * 0.3f, progress);
        particle.gameObject.transform.localScale = Vector3.one * scale;
    }

    private Vector3 GetBezierPoint(Vector3 start, Vector3 mid, Vector3 end, float t)
    {
        Vector3 a = Vector3.Lerp(start, mid, t);
        Vector3 b = Vector3.Lerp(mid, end, t);
        return Vector3.Lerp(a, b, t);
    }

    private void CleanupStream(ActiveStream stream)
    {
        // Destroy all particles
        foreach (var particle in stream.particles)
        {
            if (particle.gameObject != null)
                Destroy(particle.gameObject);
        }
        stream.particles.Clear();

        // Destroy source
        if (stream.source != null)
            Destroy(stream.source.gameObject);
    }

    void OnDrawGizmos()
    {
        if (!showDebugPaths || mouthTarget == null) return;

        // Draw debug paths for active streams
        foreach (var stream in activeStreams)
        {
            if (stream.source == null) continue;

            Vector3 start = stream.source.position;
            Vector3 end = mouthTarget.position;
            Vector3 midPoint = Vector3.Lerp(start, end, 0.5f) + stream.pathOffset * curvature;

            // Draw the bezier curve
            Gizmos.color = Color.red;
            Vector3 lastPos = start;
            for (float t = 0; t <= 1f; t += 0.1f)
            {
                Vector3 point = GetBezierPoint(start, midPoint, end, t);
                Gizmos.DrawLine(lastPos, point);
                lastPos = point;
            }

            // Draw start and end points
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(start, 0.1f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(end, 0.1f);

            // Draw active particles
            Gizmos.color = Color.cyan;
            foreach (var particle in stream.particles)
            {
                if (particle.gameObject != null)
                    Gizmos.DrawWireSphere(particle.gameObject.transform.position, 0.05f);
            }
        }
    }
}

/*
=== MAJOR REDESIGN - CONTINUOUS PARTICLE STREAM ===

This version creates actual STRANDS by:
1. Spawning particles continuously during absorption (not just at start)
2. Each particle travels from its spawn point to the mouth
3. Multiple particles spawn per second, creating a visible stream
4. Source position moves from orb to mouth, so particles spawn along the entire path

NO PARTICLE SYSTEM NEEDED! This uses simple sphere GameObjects that are easier to control.

=== SETUP ===

1. Add this script to your Player GameObject
2. Assign the Mouth Target transform
3. Remove the "bloodParticlePrefab" field or set it to null (not used anymore)
4. Adjust settings in inspector

=== RECOMMENDED SETTINGS ===

For thick, visible strands:
- Streams Per Orb: 3-5
- Particles Per Second: 50-80
- Particle Speed: 8-12
- Particle Size: 0.15-0.2
- Curvature: 0.5-1.0

For subtle effect:
- Streams Per Orb: 2-3
- Particles Per Second: 30-40
- Particle Speed: 12-15
- Particle Size: 0.1-0.15

=== HOW IT WORKS ===

1. When absorption starts, creates 3-5 "streams"
2. Each stream has a source point that moves from orb to mouth
3. As the source moves, it spawns particles behind it
4. Each particle travels from where it spawned to the mouth
5. This creates the appearance of continuous flowing strands

=== PERFORMANCE ===

- Each stream spawns ~50 particles/second for 0.5s = 25 particles per stream
- 5 streams = 125 particles total
- Much lighter than particle systems!
- Particles auto-destroy when they reach the mouth

*/