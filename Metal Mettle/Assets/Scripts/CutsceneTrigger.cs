using UnityEngine;

/// <summary>
/// Simple trigger to activate a ReusableCutscene when the player enters.
/// Attach to a GameObject with a Collider set to "Is Trigger"
/// </summary>
[RequireComponent(typeof(Collider))]
public class CutsceneTrigger : MonoBehaviour
{
    [Header("Cutscene Reference")]
    [SerializeField] private ReusableCutscene cutsceneToPlay;

    [Header("Trigger Settings")]
    [SerializeField] private string targetTag = "Player"; // What triggers the cutscene
    [SerializeField] private bool playOnEnter = true;     // Play when entering trigger
    [SerializeField] private bool playOnExit = false;     // Play when exiting trigger
    [SerializeField] private bool destroyAfterPlay = false; // Destroy trigger after playing once

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private void Start()
    {
        // Validate that this has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"CutsceneTrigger on '{gameObject.name}' has a collider that is not set to 'Is Trigger'. Setting it now.");
            col.isTrigger = true;
        }

        // Validate cutscene reference
        if (cutsceneToPlay == null)
        {
            Debug.LogError($"CutsceneTrigger on '{gameObject.name}' has no cutscene assigned!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!playOnEnter) return;

        // Check if the correct object entered
        if (other.CompareTag(targetTag))
        {
            if (showDebugMessages)
            {
                Debug.Log($"🎬 CutsceneTrigger '{gameObject.name}': {other.name} entered trigger");
            }

            PlayCutscene();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playOnExit) return;

        // Check if the correct object exited
        if (other.CompareTag(targetTag))
        {
            if (showDebugMessages)
            {
                Debug.Log($"🎬 CutsceneTrigger '{gameObject.name}': {other.name} exited trigger");
            }

            PlayCutscene();
        }
    }

    /// <summary>
    /// Public method to play the cutscene (can be called from UnityEvents, other scripts, etc.)
    /// </summary>
    public void PlayCutscene()
    {
        if (cutsceneToPlay == null)
        {
            Debug.LogError($"Cannot play cutscene - no cutscene assigned to trigger '{gameObject.name}'!");
            return;
        }

        cutsceneToPlay.PlayCutscene();

        // Destroy this trigger if set to do so
        if (destroyAfterPlay)
        {
            if (showDebugMessages)
            {
                Debug.Log($"🎬 CutsceneTrigger '{gameObject.name}': Destroying after play");
            }
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Reset the cutscene so it can play again
    /// </summary>
    public void ResetCutscene()
    {
        if (cutsceneToPlay != null)
        {
            cutsceneToPlay.ResetCutscene();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw trigger bounds in editor
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange, semi-transparent
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider boxCol)
            {
                Gizmos.DrawCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider sphereCol)
            {
                Gizmos.DrawSphere(sphereCol.center, sphereCol.radius);
            }
        }
    }
#endif
}