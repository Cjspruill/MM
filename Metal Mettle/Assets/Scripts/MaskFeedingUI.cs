using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Optional UI component to display "Feed the Mask" objective progress.
/// Shows blood collected out of required amount.
/// Can be integrated with ObjectiveUI or used standalone.
/// </summary>
public class MaskFeedingUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MaskFeeder maskFeeder;
    [SerializeField] private MaskFeedingObjectiveTracking tracker;

    [Header("UI Elements")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Image fillImage;

    [Header("Visual Settings")]
    [SerializeField] private bool showOnlyWhenActive = true;
    [SerializeField] private Color lowProgressColor = Color.red;
    [SerializeField] private Color midProgressColor = Color.yellow;
    [SerializeField] private Color highProgressColor = Color.green;
    [SerializeField] private string titleString = "Feed the Mask";
    [SerializeField] private bool showPercentage = true;
    [SerializeField] private bool showFraction = true;

    [Header("Animation")]
    [SerializeField] private bool animateProgressBar = true;
    [SerializeField] private float animationSpeed = 5f;
    [SerializeField] private bool pulseOnChange = true;
    [SerializeField] private float pulseDuration = 0.3f;
    [SerializeField] private float pulseScale = 1.1f;

    private float currentDisplayProgress = 0f;
    private float targetProgress = 0f;
    private bool isPulsing = false;
    private float pulseTimer = 0f;
    private Vector3 originalScale;

    private void Start()
    {
        // Auto-find references
        if (maskFeeder == null)
        {
            maskFeeder = FindFirstObjectByType<MaskFeeder>();
        }

        if (tracker == null)
        {
            tracker = FindFirstObjectByType<MaskFeedingObjectiveTracking>();
        }

        if (maskFeeder == null)
        {
            Debug.LogWarning("MaskFeedingUI: No MaskFeeder found!");
            gameObject.SetActive(false);
            return;
        }

        // Set up UI
        if (titleText != null)
        {
            titleText.text = titleString;
        }

        if (uiPanel != null)
        {
            originalScale = uiPanel.transform.localScale;
        }

        // Initial update
        UpdateUI();
    }

    private void Update()
    {
        if (maskFeeder == null) return;

        // Check if we should show the UI
        if (showOnlyWhenActive && tracker != null)
        {
            bool isActive = tracker.enabled && tracker.GetProgress() < 100f;
            if (uiPanel != null && uiPanel.activeSelf != isActive)
            {
                uiPanel.SetActive(isActive);
            }
        }

        // Update progress
        targetProgress = maskFeeder.GetProgress() / 100f; // Convert to 0-1

        // Animate progress bar
        if (animateProgressBar)
        {
            float previousProgress = currentDisplayProgress;
            currentDisplayProgress = Mathf.Lerp(currentDisplayProgress, targetProgress, Time.deltaTime * animationSpeed);

            // Trigger pulse on change
            if (pulseOnChange && !isPulsing && !Mathf.Approximately(previousProgress, currentDisplayProgress))
            {
                if (currentDisplayProgress > previousProgress + 0.01f) // Only pulse on increase
                {
                    StartPulse();
                }
            }
        }
        else
        {
            currentDisplayProgress = targetProgress;
        }

        // Update pulse animation
        if (isPulsing && uiPanel != null)
        {
            pulseTimer += Time.deltaTime;
            float pulseProgress = pulseTimer / pulseDuration;

            if (pulseProgress >= 1f)
            {
                isPulsing = false;
                uiPanel.transform.localScale = originalScale;
            }
            else
            {
                // Ping-pong scale
                float scale = Mathf.Lerp(1f, pulseScale, Mathf.Sin(pulseProgress * Mathf.PI));
                uiPanel.transform.localScale = originalScale * scale;
            }
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        // Update slider
        if (progressSlider != null)
        {
            progressSlider.value = currentDisplayProgress;
        }

        // Update fill color
        if (fillImage != null)
        {
            fillImage.color = GetProgressColor(currentDisplayProgress);
        }

        // Update text
        if (progressText != null)
        {
            progressText.text = GetProgressText();
        }
    }

    private string GetProgressText()
    {
        string text = "";

        if (showFraction)
        {
            float current = maskFeeder.GetCurrentBlood();
            float required = maskFeeder.GetRequiredBlood();
            text = $"{current:F0}/{required:F0}";
        }

        if (showPercentage)
        {
            if (text.Length > 0) text += " - ";
            text += $"{maskFeeder.GetProgress():F0}%";
        }

        return text;
    }

    private Color GetProgressColor(float progress)
    {
        if (progress < 0.5f)
        {
            // Low to mid
            return Color.Lerp(lowProgressColor, midProgressColor, progress * 2f);
        }
        else
        {
            // Mid to high
            return Color.Lerp(midProgressColor, highProgressColor, (progress - 0.5f) * 2f);
        }
    }

    private void StartPulse()
    {
        isPulsing = true;
        pulseTimer = 0f;
    }

    /// <summary>
    /// Manually show the UI panel
    /// </summary>
    public void Show()
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Manually hide the UI panel
    /// </summary>
    public void Hide()
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }
    }
}