using UnityEngine;

/// <summary>
/// Checkpoint that saves player progress and resets death save availability
/// Can also restore player health if desired
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private bool restoreHealthOnActivation = true;
    [SerializeField] private float healthRestoreAmount = 100f;
    [SerializeField] private bool oneTimeUse = false;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject activeIndicator; // Visual object that shows checkpoint is active
    [SerializeField] private GameObject inactiveIndicator; // Visual when checkpoint is inactive/used
    [SerializeField] private ParticleSystem activationEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip checkpointSound;

    private bool hasBeenActivated = false;
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        UpdateVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if player entered checkpoint
        if (other.CompareTag("Player"))
        {
            // Skip if one-time use and already activated
            if (oneTimeUse && hasBeenActivated) return;

            ActivateCheckpoint(other.gameObject);
        }
    }

    private void ActivateCheckpoint(GameObject player)
    {
        hasBeenActivated = true;

        // Reset death save
        DeathSaveSystem deathSave = player.GetComponent<DeathSaveSystem>();
        if (deathSave != null)
        {
            deathSave.ResetDeathSave();
            Debug.Log($"✅ Checkpoint '{gameObject.name}' activated - Death Save reset!");
        }
        else
        {
            Debug.LogWarning($"Checkpoint '{gameObject.name}': No DeathSaveSystem found on player!");
        }

        // Restore health if enabled
        if (restoreHealthOnActivation)
        {
            BloodSystem bloodSystem = player.GetComponent<BloodSystem>();
            if (bloodSystem != null)
            {
                float currentHealth = bloodSystem.currentBlood;
                float healthToRestore = healthRestoreAmount - currentHealth;

                if (healthToRestore > 0)
                {
                    bloodSystem.GainBlood(healthToRestore);
                    Debug.Log($"Checkpoint restored {healthToRestore:F1} health");
                }
            }
        }

        // Play audio
        if (audioSource != null && checkpointSound != null)
        {
            audioSource.PlayOneShot(checkpointSound);
        }

        // Play particle effect
        if (activationEffect != null)
        {
            activationEffect.Play();
        }

        // Update visuals
        UpdateVisuals();

        // Optional: Save game state here
        // SaveGameState();
    }

    private void UpdateVisuals()
    {
        if (activeIndicator != null)
        {
            activeIndicator.SetActive(hasBeenActivated);
        }
        if (inactiveIndicator != null)
        {
            inactiveIndicator.SetActive(!hasBeenActivated);
        }
    }

    /// <summary>
    /// Manually activate this checkpoint (useful for scripted events)
    /// </summary>
    public void ManualActivate()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            ActivateCheckpoint(player);
        }
    }

    /// <summary>
    /// Reset checkpoint state (useful for game restart or testing)
    /// </summary>
    public void ResetCheckpoint()
    {
        hasBeenActivated = false;
        UpdateVisuals();
    }

    private void OnDrawGizmos()
    {
        // Draw checkpoint zone
        Gizmos.color = hasBeenActivated ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}