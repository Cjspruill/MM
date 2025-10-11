using UnityEngine;
using System.Collections;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance;

    [Header("HitStop Settings")]
    public float lightStopDuration = 0.05f;
    public float heavyStopDuration = 0.1f;

    [Header("Advanced Settings")]
    public bool affectAnimations = true; // Should animations freeze too?

    [Header("Debug")]
    public bool showDebug = false;

    private bool isStopped = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StopLight()
    {
        Stop(lightStopDuration);
    }

    public void StopHeavy()
    {
        Stop(heavyStopDuration);
    }

    public void Stop(float duration)
    {
        if (!isStopped)
        {
            StartCoroutine(StopCoroutine(duration));
        }
    }

    IEnumerator StopCoroutine(float duration)
    {
        isStopped = true;

        if (showDebug)
        {
            Debug.Log($"HitStop: duration={duration}");
        }

        // Freeze time
        Time.timeScale = 0f;

        // Wait for duration using unscaled time (since Time.timeScale = 0)
        yield return new WaitForSecondsRealtime(duration);

        // Resume time
        Time.timeScale = 1f;
        isStopped = false;
    }

    public bool IsStopped() => isStopped;
}