using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        public string stepID;
        public string title;
        [TextArea(3, 6)]
        public string description;
        public TutorialTriggerType triggerType;
        public string requiredAbility;
        public bool pauseGame;
        public float displayDuration = 0f;
        public DismissInputType dismissInputType = DismissInputType.Any;
        public bool showOnce = true;
    }

    [System.Serializable]
    public class TutorialUI
    {
        public GameObject tutorialPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI dismissText;
        public Image backgroundOverlay;
        public CanvasGroup canvasGroup;
        public float fadeSpeed = 2f;
    }

    [Header("Tutorial Configuration")]
    public List<TutorialStep> tutorialSteps = new List<TutorialStep>();
    public TutorialUI ui;

    [Header("Tutorial State")]
    public bool tutorialEnabled = true;
    public bool skipCompletedSteps = true;

    [Header("References")]
    public BloodSystem bloodSystem;
    public ComboController comboController;
    public PlayerController playerController;
    public ExecutionSystem executionSystem;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private HashSet<string> completedSteps = new HashSet<string>();
    private TutorialStep currentStep;
    private bool isShowingTutorial = false;
    private Coroutine displayCoroutine;
    private float previousTimeScale = 1f;

    // Input actions from InputManager
    private InputAction attackAction;
    private InputAction executionAction;

    private Dictionary<Animator, bool> animatorStates = new Dictionary<Animator, bool>();


    public bool IsShowingTutorial
    {
        get => isShowingTutorial;
        set
        {
            isShowingTutorial = value;
            IsTutorialActive = value;

            if (showDebugLogs)
                Debug.Log($"Tutorial active state changed: {value}");
        }
    }

    public static bool IsTutorialActive { get; private set; } = false;

    void Start()
    {
        // Auto-find references
        if (bloodSystem == null)
            bloodSystem = FindFirstObjectByType<BloodSystem>();
        if (comboController == null)
            comboController = FindFirstObjectByType<ComboController>();
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
        if (executionSystem == null)
            executionSystem = FindFirstObjectByType<ExecutionSystem>();

        // Get input actions from InputManager
        if (InputManager.Instance != null)
        {
            attackAction = InputManager.Instance.Controls.Player.Attack;
            executionAction = InputManager.Instance.Controls.Player.Execution;

            if (showDebugLogs)
                Debug.Log("Tutorial input actions assigned from InputManager");
        }
        else
        {
            Debug.LogError("InputManager.Instance is null! Tutorial inputs won't work.");
        }

        // CRITICAL: Make sure Input System works during Time.timeScale = 0
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;

        // Hide tutorial UI initially
        if (ui.tutorialPanel != null)
        {
            ui.tutorialPanel.SetActive(false);
        }

        // Ensure CanvasGroup exists
        if (ui.canvasGroup == null && ui.tutorialPanel != null)
        {
            ui.canvasGroup = ui.tutorialPanel.GetComponent<CanvasGroup>();
            if (ui.canvasGroup == null)
            {
                ui.canvasGroup = ui.tutorialPanel.AddComponent<CanvasGroup>();
            }
        }

        LoadProgress();
    }
    void Update()
    {
        if (!tutorialEnabled) return;

        // Execution energy tutorial triggers
        if (executionSystem != null)
        {
            if (!HasCompletedStep("execution_energy_info") && executionSystem.GetExecutionEnergy() > 0)
            {
                TriggerTutorial("execution_energy_info");
            }

            if (!HasCompletedStep("execution_ready") && executionSystem.CanExecute())
            {
                TriggerTutorial("execution_ready");
            }
        }

        // Check for dismiss input if tutorial is showing
        if (IsShowingTutorial && currentStep != null && currentStep.displayDuration <= 0)
        {
            CheckDismissInput();
        }
    }

    void CheckDismissInput()
    {
        if (showDebugLogs)
            Debug.Log($"CheckDismissInput called - Frame: {Time.frameCount}");

        if (attackAction == null || executionAction == null)
        {
            Debug.LogError("Input actions are NULL in CheckDismissInput!");

            if (InputManager.Instance != null)
            {
                Debug.Log("InputManager exists, trying to re-get actions...");
                attackAction = InputManager.Instance.Controls.Player.Attack;
                executionAction = InputManager.Instance.Controls.Player.Execution;
            }
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($"Attack action enabled: {attackAction.enabled}");
            Debug.Log($"Attack WasPressed: {attackAction.WasPressedThisFrame()}");
            Debug.Log($"Execution action enabled: {executionAction.enabled}");
            Debug.Log($"Execution WasPressed: {executionAction.WasPressedThisFrame()}");
        }

        bool shouldDismiss = false;

        switch (currentStep.dismissInputType)
        {
            case DismissInputType.Attack:
                if (attackAction.WasPressedThisFrame())
                {
                    Debug.Log("✓ Attack input DETECTED - dismissing tutorial");
                    shouldDismiss = true;
                }
                break;

            case DismissInputType.Execution:
                if (executionAction.WasPressedThisFrame())
                {
                    Debug.Log("✓ Execution input DETECTED - dismissing tutorial");
                    shouldDismiss = true;
                }
                break;

            case DismissInputType.Any:
                if (attackAction.WasPressedThisFrame() || executionAction.WasPressedThisFrame())
                {
                    string inputUsed = attackAction.WasPressedThisFrame() ? "Attack" : "Execution";
                    Debug.Log($"✓ {inputUsed} input DETECTED - dismissing tutorial");
                    shouldDismiss = true;
                }
                break;
        }

        if (shouldDismiss)
        {
            Debug.Log("Calling DismissTutorial()...");
            DismissTutorial();
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("No dismiss input detected this frame");
        }
    }

    public void TriggerTutorial(string stepID)
    {
        if (!tutorialEnabled) return;

        TutorialStep step = tutorialSteps.Find(s => s.stepID == stepID);
        if (step == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"Tutorial step '{stepID}' not found!");
            return;
        }

        if (step.showOnce && completedSteps.Contains(stepID))
        {
            if (showDebugLogs)
                Debug.Log($"Tutorial '{stepID}' already completed, skipping.");
            return;
        }

        ShowTutorial(step);
    }

    public void TriggerAbilityTutorial(string abilityName)
    {
        if (!tutorialEnabled) return;

        TutorialStep step = tutorialSteps.Find(s =>
            s.triggerType == TutorialTriggerType.OnAbilityUnlock &&
            s.requiredAbility == abilityName);

        if (step != null)
        {
            TriggerTutorial(step.stepID);
        }
    }

    void ShowTutorial(TutorialStep step)
    {
        if (IsShowingTutorial)
        {
            if (displayCoroutine != null)
                StopCoroutine(displayCoroutine);
        }

        currentStep = step;
        IsShowingTutorial = true;

        // Update UI
        if (ui.titleText != null)
            ui.titleText.text = step.title;
        if (ui.descriptionText != null)
            ui.descriptionText.text = step.description;

        // Update dismiss text
        if (ui.dismissText != null)
        {
            if (step.displayDuration > 0)
            {
                ui.dismissText.text = "";
            }
            else
            {
                string inputName = GetInputDisplayName(step.dismissInputType);
                ui.dismissText.text = $"Press {inputName} to continue";
            }
        }

        // Handle pausing
        if (step.pauseGame)
        {
            // DON'T set Time.timeScale = 0 - it blocks input
            // Just disable all game scripts instead
            PauseGame(true);

            if (showDebugLogs)
                Debug.Log("Tutorial pausing game - All game scripts disabled");
        }

        // Show with fade
        displayCoroutine = StartCoroutine(DisplayTutorialCoroutine(step));

        if (showDebugLogs)
            Debug.Log($"📖 Tutorial shown: {step.stepID} - {step.title}");
    }

    void PauseGame(bool pause)
    {
        // Find ALL animators
        Animator[] allAnimators = FindObjectsByType<Animator>(FindObjectsSortMode.None);

        if (pause)
        {
            // Store current states and disable
            animatorStates.Clear();

            foreach (var animator in allAnimators)
            {
                animatorStates[animator] = animator.enabled;
                animator.enabled = false;
            }

            if (showDebugLogs)
                Debug.Log($"Animators PAUSED: {allAnimators.Length} animators affected");
        }
        else
        {
            // Restore previous states
            foreach (var animator in allAnimators)
            {
                if (animatorStates.ContainsKey(animator))
                {
                    // Check if this is a ragdoll (has rigidbody children that are not kinematic)
                    Rigidbody[] childRigidbodies = animator.GetComponentsInChildren<Rigidbody>();
                    bool isRagdoll = false;

                    foreach (var rb in childRigidbodies)
                    {
                        if (!rb.isKinematic)
                        {
                            isRagdoll = true;
                            break;
                        }
                    }

                    // Don't restore animator if it's a ragdoll
                    if (!isRagdoll)
                    {
                        animator.enabled = animatorStates[animator];
                    }
                    else if (showDebugLogs)
                    {
                        Debug.Log($"Skipping animator restore for ragdoll: {animator.gameObject.name}");
                    }
                }
            }

            animatorStates.Clear();

            if (showDebugLogs)
                Debug.Log($"Animators UNPAUSED: {allAnimators.Length} animators checked, ragdolls skipped");
        }
    }

    string GetInputDisplayName(DismissInputType inputType)
    {
        switch (inputType)
        {
            case DismissInputType.Attack:
                if (attackAction != null)
                    return attackAction.GetBindingDisplayString();
                return "[Attack]";

            case DismissInputType.Execution:
                if (executionAction != null)
                    return executionAction.GetBindingDisplayString();
                return "[Execution]";

            case DismissInputType.Any:
                string attack = attackAction?.GetBindingDisplayString() ?? "[Attack]";
                string execution = executionAction?.GetBindingDisplayString() ?? "[Execution]";
                return $"{attack} or {execution}";

            default:
                return "[Button]";
        }
    }

    IEnumerator DisplayTutorialCoroutine(TutorialStep step)
    {
        ui.tutorialPanel.SetActive(true);

        if (ui.canvasGroup != null)
        {
            // Normal fade
            yield return StartCoroutine(FadeCanvasGroup(ui.canvasGroup, 0f, 1f, ui.fadeSpeed, false));
        }

        if (step.displayDuration > 0)
        {
            if (showDebugLogs)
                Debug.Log($"Auto-dismissing after {step.displayDuration} seconds");

            // Use normal WaitForSeconds
            yield return new WaitForSeconds(step.displayDuration);

            if (showDebugLogs)
                Debug.Log("Auto-dismiss timer complete");

            DismissTutorial();
        }
        else
        {
            if (showDebugLogs)
                Debug.Log($"Waiting for {step.dismissInputType} input to dismiss...");
        }
    }

    public void DismissTutorial()
    {
        if (!IsShowingTutorial || currentStep == null)
        {
            if (showDebugLogs)
                Debug.Log("DismissTutorial called but no tutorial is showing");
            return;
        }

        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }

        StartCoroutine(DismissTutorialCoroutine());
    }

    IEnumerator DismissTutorialCoroutine()
    {
        if (showDebugLogs)
            Debug.Log($"Dismissing tutorial: {currentStep.stepID}");

        bool wasPaused = currentStep.pauseGame;

        if (ui.canvasGroup != null)
        {
            // Normal fade (no unscaled time needed since we're not using Time.timeScale)
            yield return StartCoroutine(FadeCanvasGroup(ui.canvasGroup, 1f, 0f, ui.fadeSpeed, false));
        }

        ui.tutorialPanel.SetActive(false);

        if (currentStep.showOnce)
        {
            completedSteps.Add(currentStep.stepID);
            SaveProgress();

            if (showDebugLogs)
                Debug.Log($"Tutorial marked as completed: {currentStep.stepID}");
        }

        // Clear tutorial state FIRST so scripts can resume
        currentStep = null;
        IsShowingTutorial = false;

        // Unpause and re-enable
        if (wasPaused)
        {
            // Unpause everything (no Time.timeScale to restore)
            PauseGame(false);

            // Clear selected UI object
            EventSystem.current?.SetSelectedGameObject(null);

            if (showDebugLogs)
                Debug.Log($"Tutorial dismissed - Game unpaused");
        }

        if (showDebugLogs)
            Debug.Log($"Tutorial dismissed successfully");
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float speed, bool useUnscaledTime = false)
    {
        if (cg == null)
        {
            Debug.LogWarning("CanvasGroup is null in FadeCanvasGroup!");
            yield break;
        }

        float elapsed = 0f;
        float duration = Mathf.Abs(to - from) / speed;

        cg.alpha = from;

        while (elapsed < duration)
        {
            // Use unscaled delta time if paused
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        cg.alpha = to;
    }

    void SaveProgress()
    {
        string json = JsonUtility.ToJson(new TutorialSaveData { completedSteps = new List<string>(completedSteps) });
        PlayerPrefs.SetString("TutorialProgress", json);
        PlayerPrefs.Save();
    }

    void LoadProgress()
    {
        if (PlayerPrefs.HasKey("TutorialProgress"))
        {
            string json = PlayerPrefs.GetString("TutorialProgress");
            TutorialSaveData data = JsonUtility.FromJson<TutorialSaveData>(json);
            completedSteps = new HashSet<string>(data.completedSteps);

            if (showDebugLogs)
                Debug.Log($"Loaded {completedSteps.Count} completed tutorial steps");
        }
    }

    public void ResetTutorials()
    {
        completedSteps.Clear();
        PlayerPrefs.DeleteKey("TutorialProgress");
        if (showDebugLogs)
            Debug.Log("All tutorials reset!");
    }

    public bool HasCompletedStep(string stepID) => completedSteps.Contains(stepID);
}

[System.Serializable]
public class TutorialSaveData
{
    public List<string> completedSteps;
}

public enum TutorialTriggerType
{
    OnStart,
    OnAbilityUnlock,
    OnFirstKill,
    OnLowBlood,
    OnDeath,
    Manual
}

public enum DismissInputType
{
    Attack,
    Execution,
    Any
}