using UnityEngine;
using System.Collections;

public class DestructibleObject : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 50f;
    private float currentHealth;

    [Header("Destruction Settings")]
    public GameObject destroyedPrefab; // Optional: spawn debris/particles
    public bool dropLoot = false;
    public GameObject lootPrefab;
    public int minLootCount = 1;
    public int maxLootCount = 3;

    [Header("Visual Feedback")]
    public bool shakeOnHit = true;
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.1f;
    public bool useHitMaterial = false;
    public Material hitFlashMaterial; // Optional: flash material on hit
    public float hitFlashDuration = 0.1f;

    [Header("Audio")]
    public AudioClip hitSound;
    public AudioClip destroySound;
    public AudioSource audioSource;

    [Header("Debug")]
    public bool showDebug = true; // CHANGED TO TRUE BY DEFAULT

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Material originalMaterial;
    private Renderer objectRenderer;
    private bool isShaking = false;
    private bool isDestroyed = false;

    void Start()
    {
        currentHealth = maxHealth;
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null && useHitMaterial)
        {
            originalMaterial = objectRenderer.material;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        Debug.Log($"DestructibleObject {gameObject.name} initialized with {maxHealth} health");
    }

    public void TakeDamage(float damage, bool isHeavyAttack = false)
    {
        if (isDestroyed)
        {
            Debug.Log($"{gameObject.name} already destroyed, ignoring damage");
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log($"💥 {gameObject.name} took {damage} damage! Health: {currentHealth}/{maxHealth}");

        // Visual feedback
        if (shakeOnHit && !isShaking)
        {
            Debug.Log($"Starting shake on {gameObject.name}");
            StartCoroutine(ShakeObject());
        }

        if (useHitMaterial && hitFlashMaterial != null && !isShaking)
        {
            StartCoroutine(FlashMaterial());
        }

        // Audio feedback
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        // Check if destroyed
        if (currentHealth <= 0)
        {
            Debug.Log($"🔥 {gameObject.name} health depleted, destroying!");
            DestroyObject();
        }
    }

    IEnumerator ShakeObject()
    {
        isShaking = true;
        float elapsed = 0f;

        Debug.Log($"Shake started: magnitude={shakeMagnitude}, duration={shakeDuration}");

        while (elapsed < shakeDuration)
        {
            // Random offset for shake
            float offsetX = Random.Range(-shakeMagnitude, shakeMagnitude);
            float offsetY = Random.Range(-shakeMagnitude, shakeMagnitude);
            float offsetZ = Random.Range(-shakeMagnitude, shakeMagnitude);

            transform.position = originalPosition + new Vector3(offsetX, offsetY, offsetZ);

            // Optional: slight rotation shake
            float rotationShake = Random.Range(-2f, 2f);
            transform.rotation = originalRotation * Quaternion.Euler(rotationShake, rotationShake, rotationShake);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset to original position and rotation
        transform.position = originalPosition;
        transform.rotation = originalRotation;
        isShaking = false;
        Debug.Log($"Shake ended on {gameObject.name}");
    }

    IEnumerator FlashMaterial()
    {
        if (objectRenderer == null) yield break;

        // Flash to hit material
        objectRenderer.material = hitFlashMaterial;

        yield return new WaitForSeconds(hitFlashDuration);

        // Return to original material
        objectRenderer.material = originalMaterial;
    }

    void DestroyObject()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        Debug.Log($"💀 DESTROYING {gameObject.name}");

        // Play destroy sound
        if (destroySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(destroySound);
        }

        // Spawn destroyed version (debris, particles, etc.)
        if (destroyedPrefab != null)
        {
            Instantiate(destroyedPrefab, transform.position, transform.rotation);
        }

        // Drop loot
        if (dropLoot && lootPrefab != null)
        {
            int lootCount = Random.Range(minLootCount, maxLootCount + 1);
            for (int i = 0; i < lootCount; i++)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(0f, 0.5f),
                    Random.Range(-0.5f, 0.5f)
                );
                Vector3 spawnPos = transform.position + randomOffset;
                Instantiate(lootPrefab, spawnPos, Quaternion.identity);
            }
        }

        // Destroy this object
        Destroy(gameObject);
    }

    // Public getter
    public float GetHealthPercent() => currentHealth / maxHealth;

    void OnDrawGizmosSelected()
    {
        // Draw health indicator
        Gizmos.color = Color.red;
        Vector3 healthBarPos = transform.position + Vector3.up * 2f;
        Gizmos.DrawWireCube(healthBarPos, new Vector3(1f, 0.1f, 0.1f));

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            float healthPercent = GetHealthPercent();
            Gizmos.DrawCube(healthBarPos, new Vector3(healthPercent, 0.1f, 0.1f));
        }
    }
}