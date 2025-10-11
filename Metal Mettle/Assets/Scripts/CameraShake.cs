using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    [Header("Shake Settings")]
    public float lightShakeDuration = 0.1f;
    public float lightShakeMagnitude = 0.05f;

    public float heavyShakeDuration = 0.2f;
    public float heavyShakeMagnitude = 0.15f;

    [Header("Debug")]
    public bool showDebug = false;

    private Vector3 originalPosition;
    private bool isShaking = false;

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

    void Start()
    {
        originalPosition = transform.localPosition;
    }

    public void ShakeLight()
    {
        Shake(lightShakeDuration, lightShakeMagnitude);
    }

    public void ShakeHeavy()
    {
        Shake(heavyShakeDuration, heavyShakeMagnitude);
    }

    public void Shake(float duration, float magnitude)
    {
        if (!isShaking)
        {
            StartCoroutine(ShakeCoroutine(duration, magnitude));
        }
        else
        {
            // If already shaking, extend the shake if new one is stronger
            StopAllCoroutines();
            StartCoroutine(ShakeCoroutine(duration, magnitude));
        }
    }

    IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        originalPosition = transform.localPosition;
        float elapsed = 0f;

        if (showDebug)
        {
            Debug.Log($"Camera shake: duration={duration}, magnitude={magnitude}");
        }

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = originalPosition + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPosition;
        isShaking = false;
    }
}