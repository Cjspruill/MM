using UnityEngine;

public class UnlockExecution : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            BloodSystem bloodSystem = other.GetComponent<BloodSystem>();
            if (bloodSystem != null)
            {
                bloodSystem.UnlockAbility("execution");
            }
        }
    }
}
