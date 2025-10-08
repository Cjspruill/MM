using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // For URP

public class VignetteController : MonoBehaviour
{
    [Header("References")]
    public Volume postProcessVolume;
    public BloodSystem bloodSystem;

    [Header("Vignette Settings")]
    public float optimalVignetteIntensity = 0f;
    public float mildVignetteIntensity = 0.2f;
    public float moderateVignetteIntensity = 0.4f;
    public float severeVignetteIntensity = 0.6f;
    public float transitionSpeed = 2f;

    [Header("Color Desaturation")]
    public bool enableDesaturation = true;
    public float optimalSaturation = 0f; // No desaturation
    public float mildSaturation = -10f;
    public float moderateSaturation = -30f;
    public float severeSaturation = -50f;

    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private float targetVignetteIntensity;
    private float targetSaturation;

    void Start()
    {
        if (postProcessVolume == null)
        {
            Debug.LogError("VignetteController: No Post Process Volume assigned!");
            return;
        }

        // Get the vignette effect from the volume
        if (postProcessVolume.profile.TryGet(out vignette))
        {
            vignette.active = true;
        }
        else
        {
            Debug.LogError("VignetteController: No Vignette effect found in Post Process Volume!");
        }

        // Get color adjustments for desaturation
        if (enableDesaturation && postProcessVolume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.active = true;
        }

        if (bloodSystem == null)
        {
            bloodSystem = FindFirstObjectByType<BloodSystem>();
        }
    }

    void Update()
    {
        if (bloodSystem == null || vignette == null) return;

        // Get current withdrawal state
        WithdrawalState state = bloodSystem.GetWithdrawalState();

        // Determine target values based on state
        switch (state)
        {
            case WithdrawalState.Optimal:
                targetVignetteIntensity = optimalVignetteIntensity;
                targetSaturation = optimalSaturation;
                break;
            case WithdrawalState.Mild:
                targetVignetteIntensity = mildVignetteIntensity;
                targetSaturation = mildSaturation;
                break;
            case WithdrawalState.Moderate:
                targetVignetteIntensity = moderateVignetteIntensity;
                targetSaturation = moderateSaturation;
                break;
            case WithdrawalState.Severe:
                targetVignetteIntensity = severeVignetteIntensity;
                targetSaturation = severeSaturation;
                break;
        }

        // Smoothly transition to target vignette intensity
        float currentIntensity = vignette.intensity.value;
        float newIntensity = Mathf.Lerp(currentIntensity, targetVignetteIntensity, Time.deltaTime * transitionSpeed);
        vignette.intensity.value = newIntensity;

        // Smoothly transition color saturation
        if (enableDesaturation && colorAdjustments != null)
        {
            float currentSaturation = colorAdjustments.saturation.value;
            float newSaturation = Mathf.Lerp(currentSaturation, targetSaturation, Time.deltaTime * transitionSpeed);
            colorAdjustments.saturation.value = newSaturation;
        }
    }

    // Optional: Add screen shake for severe withdrawal
    public void AddScreenShake(float intensity, float duration)
    {
        // Implement screen shake here if desired
    }
}