using UnityEngine;
using System;

public class BreakableGlass : MonoBehaviour
{
    [Header("Glass Settings")]
    [SerializeField] private int health = 3; // Number of hits to break
    [SerializeField] private bool breakOnFirstHit = false;

    [Header("Visual Effects")]
    [SerializeField] private GameObject crackedGlassPrefab; // Optional: different stages
    [SerializeField] private GameObject shatterParticles;
    [SerializeField] private Material[] damageMaterials; // Progressive damage materials

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip shatterSound;

    [Header("Physics")]
    [SerializeField] private GameObject[] shardPrefabs; // Broken glass pieces
    [SerializeField] private float explosionForce = 300f;
    [SerializeField] private float explosionRadius = 2f;

    private int currentHealth;
    private AudioSource audioSource;
    private MeshRenderer meshRenderer;

    // Event that mask pieces listen to
    public event Action OnGlassBroken;

    private void Start()
    {
        currentHealth = breakOnFirstHit ? 1 : health;
        audioSource = GetComponent<AudioSource>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        currentHealth -= damage;

        // Play hit sound
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        // Update visual damage
        UpdateDamageVisual();

        if (currentHealth <= 0)
        {
            ShatterGlass(hitPoint, hitDirection);
        }
    }

    private void UpdateDamageVisual()
    {
        // Change material based on damage
        if (damageMaterials != null && damageMaterials.Length > 0 && meshRenderer != null)
        {
            int damageLevel = health - currentHealth;
            damageLevel = Mathf.Clamp(damageLevel, 0, damageMaterials.Length - 1);

            if (damageLevel < damageMaterials.Length)
            {
                meshRenderer.material = damageMaterials[damageLevel];
            }
        }
    }

    private void ShatterGlass(Vector3 hitPoint, Vector3 hitDirection)
    {
        // Play shatter sound
        if (shatterSound != null)
        {
            AudioSource.PlayClipAtPoint(shatterSound, transform.position);
        }

        // Spawn shatter particles
        if (shatterParticles != null)
        {
            GameObject particles = Instantiate(shatterParticles, transform.position, Quaternion.identity);
            Destroy(particles, 3f);
        }

        // Spawn glass shards
        if (shardPrefabs != null && shardPrefabs.Length > 0)
        {
            SpawnShards(hitPoint, hitDirection);
        }

        // Notify mask pieces
        OnGlassBroken?.Invoke();

        // Destroy the glass
        Destroy(gameObject);
    }

    private void SpawnShards(Vector3 hitPoint, Vector3 hitDirection)
    {
        foreach (GameObject shardPrefab in shardPrefabs)
        {
            if (shardPrefab == null) continue;

            Vector3 spawnPos = transform.position + UnityEngine.Random.insideUnitSphere * 0.5f;
            GameObject shard = Instantiate(shardPrefab, spawnPos, UnityEngine.Random.rotation);

            Rigidbody rb = shard.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 explosionDir = (shard.transform.position - hitPoint).normalized;
                rb.AddForce(explosionDir * explosionForce + hitDirection * explosionForce * 0.5f);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * explosionForce);
            }

            // Auto-destroy shards after a few seconds
            Destroy(shard, 5f);
        }
    }

    // Call this from your attack system
    private void OnCollisionEnter(Collision collision)
    {
        // Check if hit by player attack or weapon
        if (collision.gameObject.CompareTag("PlayerAttack") || collision.gameObject.CompareTag("Weapon"))
        {
            Vector3 hitPoint = collision.contacts[0].point;
            Vector3 hitDirection = collision.relativeVelocity.normalized;
            TakeDamage(1, hitPoint, hitDirection);
        }
    }
}