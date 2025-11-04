using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Optional UI component to display "Feed the Mask" objective progress.
/// Shows blood collected out of required amount.
/// Can be integrated with ObjectiveUI or used standalone.
/// NOW SUPPORTS MULTIPLE MASK FEEDERS - will display whichever one is active.
/// </summary>
public class MaskFeedingUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MaskFeeder maskFeeder; // This can be left null - will be set dynamically

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

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private float currentDisplayProgress = 0f;
    private float targetProgress = 0f;
    private bool isPulsing = false;
    private float pulseTimer = 0f;
    private Vector3 originalScale;

    private void Start()
    {
        // NOTE: We don't auto-find maskFeeder here anymore
        // It will be set dynamically when MaskFeeders call SetActiveMaskFeeder()

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
        if (maskFeeder == null)
        {
            // No active mask feeder - hide UI
            if (uiPanel != null && uiPanel.activeSelf)
            {
                uiPanel.SetActive(false);
            }
            return;
        }

        // Check if we should show the UI
        if (showOnlyWhenActive)
        {
            // Show UI when mask feeder is active and not complete
            bool isActive = maskFeeder.IsObjectiveActive() && !maskFeeder.IsComplete();
            if (uiPanel != null && uiPanel.activeSelf != isActive)
            {
                uiPanel.SetActive(isActive);
            }
        }
        else if (uiPanel != null && !uiPanel.activeSelf)
        {
            // If not using showOnlyWhenActive, just show it when we have an active feeder
            uiPanel.SetActive(true);
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
        if (maskFeeder == null) return;

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
        if (maskFeeder == null) return "";

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

    #region Public Methods for MaskFeeder to call

    /// <summary>
    /// Called by MaskFeeders when they become active (OnEnable)
    /// This allows the UI to display the correct active mask feeder
    /// </summary>
    public void SetActiveMaskFeeder(MaskFeeder feeder)
    {
        if (maskFeeder != feeder)
        {
            maskFeeder = feeder;

            if (showDebugLogs)
            {
                if (feeder != null)
                {
                    Debug.Log($"MaskFeedingUI: Now tracking {feeder.gameObject.name}");
                }
                else
                {
                    Debug.Log($"MaskFeedingUI: No longer tracking any mask feeder");
                }
            }

            // Reset animation state when switching
            currentDisplayProgress = feeder != null ? feeder.GetProgress() / 100f : 0f;
            targetProgress = currentDisplayProgress;
            isPulsing = false;

            // Update immediately
            UpdateUI();
        }
    }

    /// <summary>
    /// Get the currently active mask feeder
    /// </summary>
    public MaskFeeder GetActiveMaskFeeder()
    {
        return maskFeeder;
    }

    #endregion

    #region Manual Control Methods

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

    #endregion
}