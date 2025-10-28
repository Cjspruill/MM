using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI element that displays during Death Save mode
/// Shows execution prompt when enemy is in range
/// </summary>
public class DeathSaveUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private Image targetReticle; // Optional: shows which enemy can be executed

    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseMin = 0.7f;
    [SerializeField] private float pulseMax = 1.3f;

    [Header("Settings")]
    [SerializeField] private string executionPromptText = "PRESS [E] TO EXECUTE";
    [SerializeField] private Color urgentColor = Color.red;

    private float targetAlpha = 0f;
    private float pulseTimer = 0f;

    private void Start()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Start hidden via alpha (keep GameObject active so Update runs)
        canvasGroup.alpha = 0f;
        targetAlpha = 0f;

        // Make non-interactable when hidden
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Set prompt text
        if (promptText != null)
        {
            promptText.text = executionPromptText;
            promptText.color = urgentColor;
        }
    }

    private void Update()
    {
        // Smooth fade in/out
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);

        // Pulse effect when visible
        if (targetAlpha > 0)
        {
            pulseTimer += Time.unscaledDeltaTime * pulseSpeed;
            float pulse = Mathf.Lerp(pulseMin, pulseMax, (Mathf.Sin(pulseTimer) + 1f) / 2f);
            transform.localScale = Vector3.one * pulse;
        }
    }

    /// <summary>
    /// Show the UI prompt
    /// </summary>
    public void Show()
    {
        targetAlpha = 1f;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        Debug.Log("DeathSaveUI: Show() called - fading in");
    }

    /// <summary>
    /// Hide the UI prompt
    /// </summary>
    public void Hide()
    {
        targetAlpha = 0f;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        Debug.Log("DeathSaveUI: Hide() called - fading out");
    }

    /// <summary>
    /// Update the prompt text dynamically
    /// </summary>
    public void SetPromptText(string text)
    {
        if (promptText != null)
        {
            promptText.text = text;
        }
    }
}