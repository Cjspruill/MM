using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class MainMenuController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Transform[] cameraPositions;

    [Header("Movement Settings")]
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] private float waitDuration = 1f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Options")]
    [SerializeField] private bool randomOrder = true;
    [SerializeField] private bool startOnEnable = true;

    [Header("How To Play Pages")]
    [SerializeField] private GameObject[] howToPlayPages;
    private int currentPageIndex = 0;

    private List<int> availableIndices = new List<int>();
    private int currentIndex = -1;
    private Coroutine movementCoroutine;

    private void OnEnable()
    {
        if (startOnEnable && cameraPositions.Length > 0)
        {
            StartCameraMovement();
        }
    }

    private void OnDisable()
    {
        StopCameraMovement();
    }

    private void OnValidate()
    {
        // Auto-assign main camera if not set
        if (menuCamera == null)
        {
            menuCamera = Camera.main;
        }
    }

    public void StartCameraMovement()
    {
        if (cameraPositions == null || cameraPositions.Length == 0)
        {
            Debug.LogWarning("No camera positions assigned!");
            return;
        }

        if (menuCamera == null)
        {
            Debug.LogError("Camera reference is missing!");
            return;
        }

        StopCameraMovement();
        movementCoroutine = StartCoroutine(CameraMovementRoutine());
    }

    public void StopCameraMovement()
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
    }

    private IEnumerator CameraMovementRoutine()
    {
        // Initialize available indices
        RefillAvailableIndices();

        while (true)
        {
            // Get next position
            int nextIndex = GetNextIndex();
            Transform targetTransform = cameraPositions[nextIndex];

            // Store starting position and rotation
            Vector3 startPos = menuCamera.transform.position;
            Quaternion startRot = menuCamera.transform.rotation;

            // Move to target position
            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = movementCurve.Evaluate(elapsed / transitionDuration);

                menuCamera.transform.position = Vector3.Lerp(startPos, targetTransform.position, t);
                menuCamera.transform.rotation = Quaternion.Slerp(startRot, targetTransform.rotation, t);

                yield return null;
            }

            // Ensure we're exactly at target
            menuCamera.transform.position = targetTransform.position;
            menuCamera.transform.rotation = targetTransform.rotation;

            // Wait at this position
            yield return new WaitForSeconds(waitDuration);
        }
    }

    private int GetNextIndex()
    {
        if (randomOrder)
        {
            // Refill if empty
            if (availableIndices.Count == 0)
            {
                RefillAvailableIndices();
            }

            // Pick random from available
            int randomArrayIndex = Random.Range(0, availableIndices.Count);
            int selectedIndex = availableIndices[randomArrayIndex];
            availableIndices.RemoveAt(randomArrayIndex);

            currentIndex = selectedIndex;
            return selectedIndex;
        }
        else
        {
            // Sequential order
            currentIndex = (currentIndex + 1) % cameraPositions.Length;
            return currentIndex;
        }
    }

    private void RefillAvailableIndices()
    {
        availableIndices.Clear();

        for (int i = 0; i < cameraPositions.Length; i++)
        {
            // Don't add current index to prevent immediate repeat
            if (i != currentIndex || cameraPositions.Length == 1)
            {
                availableIndices.Add(i);
            }
        }
    }

    // Public methods for manual control
    public void SetTransitionDuration(float duration)
    {
        transitionDuration = Mathf.Max(0.1f, duration);
    }

    public void SetWaitDuration(float duration)
    {
        waitDuration = Mathf.Max(0f, duration);
    }

    public void LoadLevel(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void MoveToPosition(int index)
    {
        if (index >= 0 && index < cameraPositions.Length)
        {
            StopCameraMovement();
            StartCoroutine(MoveToSpecificPosition(index));
        }
    }

    private IEnumerator MoveToSpecificPosition(int index)
    {
        Transform targetTransform = cameraPositions[index];
        Vector3 startPos = menuCamera.transform.position;
        Quaternion startRot = menuCamera.transform.rotation;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = movementCurve.Evaluate(elapsed / transitionDuration);

            menuCamera.transform.position = Vector3.Lerp(startPos, targetTransform.position, t);
            menuCamera.transform.rotation = Quaternion.Slerp(startRot, targetTransform.rotation, t);

            yield return null;
        }

        menuCamera.transform.position = targetTransform.position;
        menuCamera.transform.rotation = targetTransform.rotation;
        currentIndex = index;
    }

    // How To Play page management
    public void NextPage()
    {
        if (howToPlayPages == null || howToPlayPages.Length == 0) return;

        // Deactivate current page
        if (currentPageIndex >= 0 && currentPageIndex < howToPlayPages.Length)
        {
            howToPlayPages[currentPageIndex].SetActive(false);
        }

        // Move to next page (loop at end)
        currentPageIndex = (currentPageIndex + 1) % howToPlayPages.Length;

        // Activate new page
        howToPlayPages[currentPageIndex].SetActive(true);
    }

    public void PreviousPage()
    {
        if (howToPlayPages == null || howToPlayPages.Length == 0) return;

        // Deactivate current page
        if (currentPageIndex >= 0 && currentPageIndex < howToPlayPages.Length)
        {
            howToPlayPages[currentPageIndex].SetActive(false);
        }

        // Move to previous page (loop at beginning)
        currentPageIndex--;
        if (currentPageIndex < 0)
        {
            currentPageIndex = howToPlayPages.Length - 1;
        }

        // Activate new page
        howToPlayPages[currentPageIndex].SetActive(true);
    }

    public void SetPage(int index)
    {
        if (howToPlayPages == null || howToPlayPages.Length == 0) return;
        if (index < 0 || index >= howToPlayPages.Length) return;

        // Deactivate current page
        if (currentPageIndex >= 0 && currentPageIndex < howToPlayPages.Length)
        {
            howToPlayPages[currentPageIndex].SetActive(false);
        }

        // Set and activate new page
        currentPageIndex = index;
        howToPlayPages[currentPageIndex].SetActive(true);
    }

    // Quit application
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}