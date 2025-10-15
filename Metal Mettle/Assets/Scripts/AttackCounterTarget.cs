using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to specific enemies to track how many times player hits them
/// Completes objective after X hits (doesn't need to kill them)
/// </summary>
public class AttackCounterTarget : MonoBehaviour
{
    [Header("Hit Requirements")]
    [SerializeField] private int requiredHits = 10;
    private int currentHits = 0;

    [Header("Objective Integration")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private string attackTaskName = "Attack Enemy 10 Times";

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private Color hitColor = Color.yellow;

    [Header("Events")]
    public UnityEvent<int, int> onHitCountChanged; // current, required
    public UnityEvent onRequiredHitsReached;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Health enemyHealth;
    private Renderer enemyRenderer;
    private Color originalColor;
    private bool objectiveCompleted = false;

    private void Start()
    {
        enemyHealth = GetComponent<Health>();
        enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null)
        {
            originalColor = enemyRenderer.material.color;
        }

        if (objectiveController == null)
        {
            objectiveController = FindFirstObjectByType<ObjectiveController>();
        }

        // Subscribe to damage events if Health component exists
        if (enemyHealth != null)
        {
            enemyHealth.onDamage.AddListener(OnHitByPlayer);
        }
        else
        {
            Debug.LogWarning($"AttackCounterTarget on {gameObject.name}: No Health component found!");
        }
    }

    private void OnHitByPlayer()
    {
        if (objectiveCompleted) return;

        currentHits++;

        if (showDebugLogs)
        {
            Debug.Log($"AttackCounterTarget: Hit {currentHits}/{requiredHits}");
        }

        // Visual feedback
        ShowHitFeedback();

        // Invoke event for UI
        onHitCountChanged?.Invoke(currentHits, requiredHits);

        // Check completion
        if (currentHits >= requiredHits)
        {
            CompleteObjective();
        }
    }

    private void ShowHitFeedback()
    {
        // Spawn hit effect if assigned
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
        }

        // Flash color
        if (enemyRenderer != null)
        {
            StartCoroutine(FlashColor());
        }
    }

    private System.Collections.IEnumerator FlashColor()
    {
        enemyRenderer.material.color = hitColor;
        yield return new WaitForSeconds(0.1f);
        enemyRenderer.material.color = originalColor;
    }

    private void CompleteObjective()
    {
        objectiveCompleted = true;

        if (showDebugLogs)
        {
            Debug.Log($"AttackCounterTarget: Required hits reached! Completing objective.");
        }

        onRequiredHitsReached?.Invoke();

        if (objectiveController != null && !string.IsNullOrEmpty(attackTaskName))
        {
            objectiveController.CompleteTask(attackTaskName);
        }

        // Optional: Destroy or disable this component after completion
        // Destroy(this);
    }

    public int GetCurrentHits() => currentHits;
    public int GetRequiredHits() => requiredHits;
    public float GetProgress() => (float)currentHits / requiredHits;

    public void ResetProgress()
    {
        currentHits = 0;
        objectiveCompleted = false;
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.onDamage.RemoveListener(OnHitByPlayer);
        }
    }
}