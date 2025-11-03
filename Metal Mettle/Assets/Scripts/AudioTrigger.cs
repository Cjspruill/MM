using UnityEngine;

public class AudioTrigger : MonoBehaviour
{

    public AudioSource audioSource;
    public AudioClip clip;

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
        if (other.CompareTag("Player"))
        {
            audioSource.clip = clip;
            audioSource.PlayOneShot(clip);
            GetComponent<BoxCollider>().enabled = false;
            Destroy(gameObject,10f);
        }
    }
}
