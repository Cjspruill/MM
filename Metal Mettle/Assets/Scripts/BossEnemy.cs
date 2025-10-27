using UnityEngine;

public class BossEnemy : MonoBehaviour
{
    [SerializeField] ObjectiveController objectiveController;
    [SerializeField] string taskToComplete;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateObjectiveController()
    {
        if(objectiveController != null)
        {
            objectiveController.CompleteTask(taskToComplete);
        }
    }
}
