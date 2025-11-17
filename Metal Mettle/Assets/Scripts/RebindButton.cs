using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RebindButton : MonoBehaviour
{
    [Header("Action Settings")]
    [SerializeField] private string actionName = "Attack";
    [SerializeField] private int bindingIndex = 0;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI actionLabel;
    [SerializeField] private Button bindingButton;
    [SerializeField] private TextMeshProUGUI bindingButtonText;
    [SerializeField] private Button resetButton;

    private bool isRebinding = false;

    private void Start()
    {
        if (InputManager.Instance == null)
        {
            Debug.LogError("InputManager not found!");
            return;
        }

        if (bindingButton == null)
        {
            Debug.LogError($"Binding Button not assigned on {gameObject.name}!");
            return;
        }

        if (bindingButtonText == null)
        {
            Debug.LogError($"Binding Button Text not assigned on {gameObject.name}!");
            return;
        }

        // Set up the action label
        if (actionLabel != null)
        {
            actionLabel.text = actionName + ":";
        }

        // Click the binding button itself to rebind
        bindingButton.onClick.AddListener(StartRebinding);

        // Optional reset button
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetThisBinding);
        }

        UpdateButtonText();
    }

    private void StartRebinding()
    {
        if (isRebinding) return;

        isRebinding = true;
        bindingButtonText.text = "...";
        bindingButton.interactable = false;

        if (resetButton != null)
        {
            resetButton.interactable = false;
        }

        InputManager.Instance.StartRebind(actionName, bindingIndex, (success) =>
        {
            isRebinding = false;
            bindingButton.interactable = true;

            if (resetButton != null)
            {
                resetButton.interactable = true;
            }

            UpdateButtonText();
        });
    }

    private void ResetThisBinding()
    {
        InputManager.Instance.ResetAction(actionName);
        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        bindingButtonText.text = InputManager.Instance.GetReadableBindingName(actionName, bindingIndex);
    }
}