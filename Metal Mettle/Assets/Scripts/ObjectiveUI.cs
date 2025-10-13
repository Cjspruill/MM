using UnityEngine;
using TMPro;

public class ObjectiveUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI objectiveTitle;
    [SerializeField] private TextMeshProUGUI objectiveDescription;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private GameObject objectivePanel; // Main objective display panel

    private void OnEnable()
    {
        // Force update when UI becomes active
        var controller = FindFirstObjectByType<ObjectiveController>();
        if (controller != null)
        {
            UpdateDisplay(controller);
        }
    }

    public void UpdateDisplay(ObjectiveController controller)
    {
        var currentObjective = controller.GetCurrentObjective();

        if (currentObjective != null)
        {
            // Show objective panel
            if (objectivePanel != null)
            {
                objectivePanel.SetActive(true);
            }

            // Hide completion panel
            if (completionPanel != null)
            {
                completionPanel.SetActive(false);
            }

            // Update text - FORCE update by setting to empty first
            if (objectiveTitle != null)
            {
                objectiveTitle.text = ""; // Clear first
                objectiveTitle.text = currentObjective.objectiveName;
            }

            if (objectiveDescription != null)
            {
                objectiveDescription.text = ""; // Clear first
                objectiveDescription.text = currentObjective.objectiveDescription;
            }

            if (progressText != null)
            {
                progressText.text = ""; // Clear first
                progressText.text = controller.GetProgressString();
            }

            // Force TextMeshPro to update
            if (objectiveTitle != null) objectiveTitle.ForceMeshUpdate();
            if (objectiveDescription != null) objectiveDescription.ForceMeshUpdate();
            if (progressText != null) progressText.ForceMeshUpdate();
        }
        else
        {
            // All objectives complete
            if (objectivePanel != null)
            {
                objectivePanel.SetActive(false);
            }

            if (objectiveTitle != null)
            {
                objectiveTitle.text = "All Objectives Complete";
                objectiveTitle.ForceMeshUpdate();
            }

            if (objectiveDescription != null)
            {
                objectiveDescription.text = "";
                objectiveDescription.ForceMeshUpdate();
            }

            if (progressText != null)
            {
                progressText.text = "";
                progressText.ForceMeshUpdate();
            }

            //if (completionPanel != null)
            //{
            //    completionPanel.SetActive(true);
            //}
        }
    }
}