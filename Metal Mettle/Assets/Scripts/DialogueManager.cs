using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;
    public GameObject dialoguePanel;

    [Header("Typewriter Settings")]
    [Tooltip("Speed at which characters appear (seconds per character)")]
    public float typeSpeed = 0.05f;

    [Tooltip("Optional audio clip to play per character")]
    public AudioClip typingSound;

    [Header("Audio Settings")]
    [Tooltip("Folder path in Resources where voice clips are stored")]
    public string voiceClipFolder = "VoiceLines";

    [Tooltip("Separate audio source for voice lines (optional)")]
    public AudioSource voiceAudioSource;

    [Header("Dialogue Data")]
    public DialogueData dialogueData;

    [Header("Input System")]
    public InputSystem_Actions inputActions;

    private AudioSource audioSource;
    private Coroutine typeCoroutine;
    private DialogueSequence currentSequence;
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private bool canAdvance = true;
    private string currentFullText;
    private AudioClip currentVoiceClip;

    [SerializeField] string dialogueToStart;

    public bool endOfLevel1;
    public bool endOfLevel2;
    public bool endOfLevel3;
    public bool endOfLevel4;
    public bool endOfLevel5;
    public bool endOfLevel6;
    public bool endOfLevel7;

    void Awake()
    {
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
        }
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && typingSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Setup separate voice audio source if not assigned
        if (voiceAudioSource == null)
        {
            GameObject voiceObj = new GameObject("VoiceAudioSource");
            voiceObj.transform.SetParent(transform);
            voiceAudioSource = voiceObj.AddComponent<AudioSource>();
        }

        if (dialogueData != null)
        {
            dialogueData.LoadDialogue();
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }


        Invoke("StartUp", 3);

    }


    public void StartUp()
    {
        StartDialogue(dialogueToStart);

    }

    void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.UI.Click.performed += OnClickPerformed;
            inputActions.UI.Submit.performed += OnClickPerformed;
            inputActions.UI.Enable();
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.UI.Click.performed -= OnClickPerformed;
            inputActions.UI.Submit.performed -= OnClickPerformed;
            inputActions.UI.Disable();
        }
    }

    private void OnClickPerformed(InputAction.CallbackContext context)
    {
        if (!canAdvance) return;

        if (isTyping)
        {
            CompleteCurrentLine();
        }
        else if (currentSequence != null && !isTyping)
        {
            StartCoroutine(AdvanceWithDelay());
        }
    }

    /// <summary>
    /// Advances to next line with a small delay to prevent double-clicks
    /// </summary>
    private IEnumerator AdvanceWithDelay()
    {
        canAdvance = false;
        ShowNextLine();
        yield return new WaitForSeconds(0.2f);
        canAdvance = true;
    }

    /// <summary>
    /// Starts a dialogue sequence by ID
    /// </summary>
    public void StartDialogue(string sequenceId)
    {
        if (dialogueData == null)
        {
            Debug.LogError("No DialogueData assigned!");
            return;
        }

        currentSequence = dialogueData.GetSequence(sequenceId);

        if (currentSequence == null)
        {
            Debug.LogError($"Sequence '{sequenceId}' not found!");
            return;
        }

        currentLineIndex = 0;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        ShowNextLine();
    }

    /// <summary>
    /// Shows the next line in the current sequence
    /// </summary>
    public void ShowNextLine()
    {
        if (currentSequence == null || currentLineIndex >= currentSequence.lines.Count)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = currentSequence.lines[currentLineIndex];
        currentLineIndex++;

        // Update speaker name if available
        if (speakerNameText != null && !string.IsNullOrEmpty(line.speaker))
        {
            speakerNameText.text = line.speaker;
        }

        // Load and play voice clip if specified
        if (!string.IsNullOrEmpty(line.audioClipName))
        {
            LoadAndPlayVoiceClip(line.audioClipName);
        }

        // Determine type speed for this line
        float speed = line.customDelay >= 0 ? line.customDelay : typeSpeed;

        TypeLine(line.text, speed);
    }

    /// <summary>
    /// Loads and plays a voice clip from Resources
    /// </summary>
    private void LoadAndPlayVoiceClip(string clipName)
    {
        string path = string.IsNullOrEmpty(voiceClipFolder) ? clipName : $"{voiceClipFolder}/{clipName}";
        currentVoiceClip = Resources.Load<AudioClip>(path);

        if (currentVoiceClip != null && voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
            voiceAudioSource.clip = currentVoiceClip;
            voiceAudioSource.Play();
        }
        else if (currentVoiceClip == null)
        {
            Debug.LogWarning($"Voice clip not found: {path}");
        }
    }

    /// <summary>
    /// Types out a single line with typewriter effect
    /// </summary>
    private void TypeLine(string text, float speed)
    {
        if (typeCoroutine != null)
        {
            StopCoroutine(typeCoroutine);
        }

        currentFullText = text;
        typeCoroutine = StartCoroutine(TypeText(text, speed));
    }

    /// <summary>
    /// Instantly completes the current line
    /// </summary>
    public void CompleteCurrentLine()
    {
        if (isTyping)
        {
            if (typeCoroutine != null)
            {
                StopCoroutine(typeCoroutine);
            }
            dialogueText.text = currentFullText;
            isTyping = false;

            // Add delay to prevent immediate advance
            StartCoroutine(CompleteDelay());
        }
    }

    /// <summary>
    /// Adds a small delay after completing a line to prevent accidental skipping
    /// </summary>
    private IEnumerator CompleteDelay()
    {
        canAdvance = false;
        yield return new WaitForSeconds(0.3f);
        canAdvance = true;
    }

    /// <summary>
    /// Ends the current dialogue sequence
    /// </summary>
    private void EndDialogue()
    {
        currentSequence = null;
        currentLineIndex = 0;

        // Stop voice audio when dialogue ends
        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            //if (GameManager.Instance)
            //    GameManager.Instance.DialogueComplete();
        }

        ClearText();
    }

    /// <summary>
    /// Clears the dialogue text
    /// </summary>
    public void ClearText()
    {
        if (typeCoroutine != null)
        {
            StopCoroutine(typeCoroutine);
        }
        dialogueText.text = "";
        if (speakerNameText != null)
        {
            speakerNameText.text = "";
        }
        isTyping = false;
    }

    /// <summary>
    /// Stops all dialogue audio (voice and typing)
    /// </summary>
    public void StopAllAudio()
    {
        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
        }
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private IEnumerator TypeText(string text, float speed)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char c in text)
        {
            dialogueText.text += c;

            // Play typing sound if available (skip for spaces)
            if (c != ' ' && typingSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(typingSound);
            }

            yield return new WaitForSeconds(speed);
        }

        isTyping = false;
    }

    /// <summary>
    /// Check if currently typing
    /// </summary>
    public bool IsTyping()
    {
        return isTyping;
    }

    /// <summary>
    /// Check if dialogue is currently active
    /// </summary>
    public bool IsDialogueActive()
    {
        return currentSequence != null;
    }
}