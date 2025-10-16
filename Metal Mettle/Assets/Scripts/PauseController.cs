using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Toggle inputVerticalToggle;

    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private InputSystem_Actions controls;
    private InputAction pauseAction;
    private bool isPaused = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        controls = new InputSystem_Actions();
        pauseAction = controls.Player.Pause;

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(WaitForInputManager());
    }

    private IEnumerator WaitForInputManager()
    {
        // Wait until InputManager is ready
        yield return new WaitUntil(() => InputManager.Instance != null);

        // Add listener FIRST
        if (inputVerticalToggle != null)
        {
            inputVerticalToggle.onValueChanged.AddListener(OnVerticalToggleChanged);

            // Then sync the toggle state without triggering the listener
            inputVerticalToggle.SetIsOnWithoutNotify(InputManager.Instance.invertVertical);
        }
    }

    private void OnEnable()
    {
        if (pauseAction != null)
        {
            pauseAction.Enable();
            pauseAction.performed += OnPausePerformed;
        }
    }

    private void OnDisable()
    {
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
            pauseAction.Disable();
        }

        if (inputVerticalToggle != null)
            inputVerticalToggle.onValueChanged.RemoveListener(OnVerticalToggleChanged);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (pausePanel != null)
            pausePanel.SetActive(true);

        Time.timeScale = 0f;
        isPaused = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public bool IsPaused() => isPaused;

    private void OnVerticalToggleChanged(bool value)
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.SetInvertVertical(value);
            Debug.Log($"Vertical inversion set to: {value}"); // Debug log
        }
    }
}