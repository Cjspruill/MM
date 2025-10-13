using UnityEngine;

public class PlayAudioClipDelayed : MonoBehaviour
{

    [SerializeField] AudioSource audioSource;

    [SerializeField] AudioClip clip;
    [SerializeField] float delayTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Invoke("PlayAudioClip", delayTime);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayAudioClip()
    {
        audioSource.clip = clip;
        audioSource.Play();
    }
}
