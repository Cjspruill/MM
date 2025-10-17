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
    [SerializeField] private PageTransitionType transitionType = PageTransitionType.SlideRight;
    [SerializeField] private float pageTransitionDuration = 0.5f;
    [SerializeField] private AnimationCurve pageTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float slideDistance = 1000f; // For slide transitions

    private int currentPageIndex = 0;
    private bool isTransitioning = false;

    public enum PageTransitionType
    {
        Fade,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown,
        Scale,
        Flip
    }

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
        RefillAvailableIndices();

        while (true)
        {
            int nextIndex = GetNextIndex();
            Transform targetTransform = cameraPositions[nextIndex];

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

            yield return new WaitForSeconds(waitDuration);
        }
    }

    private int GetNextIndex()
    {
        if (randomOrder)
        {
            if (availableIndices.Count == 0)
            {
                RefillAvailableIndices();
            }

            int randomArrayIndex = Random.Range(0, availableIndices.Count);
            int selectedIndex = availableIndices[randomArrayIndex];
            availableIndices.RemoveAt(randomArrayIndex);

            currentIndex = selectedIndex;
            return selectedIndex;
        }
        else
        {
            currentIndex = (currentIndex + 1) % cameraPositions.Length;
            return currentIndex;
        }
    }

    private void RefillAvailableIndices()
    {
        availableIndices.Clear();

        for (int i = 0; i < cameraPositions.Length; i++)
        {
            if (i != currentIndex || cameraPositions.Length == 1)
            {
                availableIndices.Add(i);
            }
        }
    }

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

    // ===== PAGE TRANSITION METHODS =====

    public void NextPage()
    {
        if (isTransitioning || howToPlayPages == null || howToPlayPages.Length == 0) return;

        int nextIndex = (currentPageIndex + 1) % howToPlayPages.Length;
        StartCoroutine(TransitionToPage(nextIndex, true));
    }

    public void PreviousPage()
    {
        if (isTransitioning || howToPlayPages == null || howToPlayPages.Length == 0) return;

        int prevIndex = currentPageIndex - 1;
        if (prevIndex < 0) prevIndex = howToPlayPages.Length - 1;

        StartCoroutine(TransitionToPage(prevIndex, false));
    }

    public void SetPage(int index)
    {
        if (isTransitioning || howToPlayPages == null || howToPlayPages.Length == 0) return;
        if (index < 0 || index >= howToPlayPages.Length) return;

        bool forward = index > currentPageIndex;
        StartCoroutine(TransitionToPage(index, forward));
    }

    private IEnumerator TransitionToPage(int newIndex, bool forward)
    {
        isTransitioning = true;

        GameObject currentPage = howToPlayPages[currentPageIndex];
        GameObject nextPage = howToPlayPages[newIndex];

        // Ensure we have RectTransforms and CanvasGroups
        RectTransform currentRect = currentPage.GetComponent<RectTransform>();
        RectTransform nextRect = nextPage.GetComponent<RectTransform>();

        CanvasGroup currentGroup = GetOrAddCanvasGroup(currentPage);
        CanvasGroup nextGroup = GetOrAddCanvasGroup(nextPage);

        // Activate next page
        nextPage.SetActive(true);

        // Store original positions
        Vector2 currentOriginalPos = currentRect.anchoredPosition;
        Vector2 nextOriginalPos = nextRect.anchoredPosition;
        Vector3 currentOriginalScale = currentRect.localScale;
        Vector3 nextOriginalScale = nextRect.localScale;

        // Setup starting states based on transition type
        SetupTransitionStart(nextRect, nextGroup, forward);

        float elapsed = 0f;
        while (elapsed < pageTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = pageTransitionCurve.Evaluate(elapsed / pageTransitionDuration);

            UpdateTransition(currentRect, currentGroup, nextRect, nextGroup, t, forward,
                           currentOriginalPos, nextOriginalPos, currentOriginalScale, nextOriginalScale);

            yield return null;
        }

        // Ensure final states
        currentRect.anchoredPosition = currentOriginalPos;
        currentRect.localScale = currentOriginalScale;
        currentGroup.alpha = 1f;
        currentPage.SetActive(false);

        nextRect.anchoredPosition = nextOriginalPos;
        nextRect.localScale = nextOriginalScale;
        nextGroup.alpha = 1f;

        currentPageIndex = newIndex;
        isTransitioning = false;
    }

    private void SetupTransitionStart(RectTransform rect, CanvasGroup group, bool forward)
    {
        switch (transitionType)
        {
            case PageTransitionType.Fade:
                group.alpha = 0f;
                break;

            case PageTransitionType.SlideLeft:
                rect.anchoredPosition = new Vector2(forward ? slideDistance : -slideDistance, rect.anchoredPosition.y);
                break;

            case PageTransitionType.SlideRight:
                rect.anchoredPosition = new Vector2(forward ? -slideDistance : slideDistance, rect.anchoredPosition.y);
                break;

            case PageTransitionType.SlideUp:
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, forward ? -slideDistance : slideDistance);
                break;

            case PageTransitionType.SlideDown:
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, forward ? slideDistance : -slideDistance);
                break;

            case PageTransitionType.Scale:
                rect.localScale = Vector3.zero;
                group.alpha = 0f;
                break;

            case PageTransitionType.Flip:
                rect.localScale = new Vector3(0f, 1f, 1f);
                group.alpha = 0f;
                break;
        }
    }

    private void UpdateTransition(RectTransform currentRect, CanvasGroup currentGroup,
                                  RectTransform nextRect, CanvasGroup nextGroup,
                                  float t, bool forward,
                                  Vector2 currentOriginalPos, Vector2 nextOriginalPos,
                                  Vector3 currentOriginalScale, Vector3 nextOriginalScale)
    {
        switch (transitionType)
        {
            case PageTransitionType.Fade:
                currentGroup.alpha = 1f - t;
                nextGroup.alpha = t;
                break;

            case PageTransitionType.SlideLeft:
                currentRect.anchoredPosition = Vector2.Lerp(
                    currentOriginalPos,
                    new Vector2(forward ? -slideDistance : slideDistance, currentOriginalPos.y),
                    t);
                nextRect.anchoredPosition = Vector2.Lerp(
                    new Vector2(forward ? slideDistance : -slideDistance, nextOriginalPos.y),
                    nextOriginalPos,
                    t);
                break;

            case PageTransitionType.SlideRight:
                currentRect.anchoredPosition = Vector2.Lerp(
                    currentOriginalPos,
                    new Vector2(forward ? slideDistance : -slideDistance, currentOriginalPos.y),
                    t);
                nextRect.anchoredPosition = Vector2.Lerp(
                    new Vector2(forward ? -slideDistance : slideDistance, nextOriginalPos.y),
                    nextOriginalPos,
                    t);
                break;

            case PageTransitionType.SlideUp:
                currentRect.anchoredPosition = Vector2.Lerp(
                    currentOriginalPos,
                    new Vector2(currentOriginalPos.x, forward ? slideDistance : -slideDistance),
                    t);
                nextRect.anchoredPosition = Vector2.Lerp(
                    new Vector2(nextOriginalPos.x, forward ? -slideDistance : slideDistance),
                    nextOriginalPos,
                    t);
                break;

            case PageTransitionType.SlideDown:
                currentRect.anchoredPosition = Vector2.Lerp(
                    currentOriginalPos,
                    new Vector2(currentOriginalPos.x, forward ? -slideDistance : slideDistance),
                    t);
                nextRect.anchoredPosition = Vector2.Lerp(
                    new Vector2(nextOriginalPos.x, forward ? slideDistance : -slideDistance),
                    nextOriginalPos,
                    t);
                break;

            case PageTransitionType.Scale:
                currentRect.localScale = Vector3.Lerp(currentOriginalScale, Vector3.zero, t);
                currentGroup.alpha = 1f - t;

                nextRect.localScale = Vector3.Lerp(Vector3.zero, nextOriginalScale, t);
                nextGroup.alpha = t;
                break;

            case PageTransitionType.Flip:
                // Current page flips out
                float currentScaleX = Mathf.Lerp(1f, 0f, t);
                currentRect.localScale = new Vector3(currentScaleX, 1f, 1f);
                currentGroup.alpha = currentScaleX;

                // Next page flips in
                float nextScaleX = Mathf.Lerp(0f, 1f, t);
                nextRect.localScale = new Vector3(nextScaleX, 1f, 1f);
                nextGroup.alpha = nextScaleX;
                break;
        }
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        CanvasGroup group = obj.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = obj.AddComponent<CanvasGroup>();
        }
        return group;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}