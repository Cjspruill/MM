using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class MaskController : MonoBehaviour
{
    [System.Serializable]
    public class MaskPiece
    {
        public string pieceName;
        public GameObject pieceOnCharacter;
        public bool isCollected = false;

        [Header("Voiceover")]
        [Tooltip("Voiceover dialogue that plays when this piece is collected")]
        public AudioClip voiceoverClip;
    }

    [Header("Mask Pieces on Character")]
    [SerializeField] private List<MaskPiece> maskPieces = new List<MaskPiece>();

    [Header("Collection Settings")]
    [SerializeField] private LayerMask collectibleLayer;
    [SerializeField] private float collectionRange = 2f;

    [Header("Input")]
    [SerializeField] private InputSystem_Actions controls;
    private InputAction executionInput;

    [Header("Objective System")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private string maskCollectionTaskPrefix = "Collect";

    [Header("Audio")]
    [SerializeField] private AudioClip collectionSound;
    [Tooltip("Audio source for collection sound effects (short sounds)")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Tooltip("Audio source for voiceover dialogue (can be separate for better control)")]
    [SerializeField] private AudioSource voiceoverAudioSource;

    [Tooltip("Delay before playing voiceover after collection sound")]
    [SerializeField] private float voiceoverDelay = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private MaskPieceCollectible nearbyCollectible;

    private void Awake()
    {
        controls = new InputSystem_Actions();
        executionInput = controls.Player.Execution;
    }

    private void OnEnable()
    {
        controls.Enable();
        executionInput.performed += OnExecutionPressed;
    }

    private void OnDisable()
    {
        executionInput.performed -= OnExecutionPressed;
        controls.Disable();
    }

    private void Start()
    {
        // Start with all pieces disabled
        foreach (MaskPiece piece in maskPieces)
        {
            if (piece.pieceOnCharacter != null)
            {
                piece.pieceOnCharacter.SetActive(false);
            }
        }

        // Setup audio sources - create them if they don't exist
        AudioSource[] sources = GetComponents<AudioSource>();

        if (sfxAudioSource == null)
        {
            if (sources.Length > 0)
            {
                sfxAudioSource = sources[0];
            }
            else
            {
                sfxAudioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log("Created SFX AudioSource");
            }
        }

        // If no separate voiceover source specified, create or use second one
        if (voiceoverAudioSource == null)
        {
            if (sources.Length > 1)
            {
                voiceoverAudioSource = sources[1];
            }
            else
            {
                voiceoverAudioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log("Created Voiceover AudioSource");
            }
        }

        // Configure voiceover audio source for dialogue
        if (voiceoverAudioSource != null)
        {
            voiceoverAudioSource.playOnAwake = false;
            voiceoverAudioSource.spatialBlend = 0f; // 2D audio for voiceover
            Debug.Log($"Voiceover AudioSource configured: {voiceoverAudioSource.gameObject.name}");
        }

        // Auto-find objective controller if not set
        if (objectiveController == null)
        {
            objectiveController = FindFirstObjectByType<ObjectiveController>();
        }

        if (showDebugLogs)
        {
            Debug.Log($"MaskController started. {maskPieces.Count} pieces configured.");
            Debug.Log($"SFX Source: {(sfxAudioSource != null ? "Found" : "Missing")}");
            Debug.Log($"Voiceover Source: {(voiceoverAudioSource != null ? "Found" : "Missing")}");
        }
    }

    private void Update()
    {
        CheckForCollectibles();
    }

    private void CheckForCollectibles()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, collectionRange, collectibleLayer);

        nearbyCollectible = null;

        foreach (Collider col in nearbyObjects)
        {
            MaskPieceCollectible collectible = col.GetComponent<MaskPieceCollectible>();

            if (collectible != null && collectible.isAccessible)
            {
                nearbyCollectible = collectible;

                if (showDebugLogs)
                {
                    Debug.Log($"Nearby collectible found: {collectible.pieceType} (accessible: {collectible.isAccessible})");
                }
                break;
            }
        }
    }

    private void OnExecutionPressed(InputAction.CallbackContext context)
    {
        if (showDebugLogs)
        {
            Debug.Log($"Execution pressed. Nearby collectible: {(nearbyCollectible != null ? nearbyCollectible.pieceType : "none")}");
        }

        if (nearbyCollectible != null && nearbyCollectible.isAccessible)
        {
            string pieceType = nearbyCollectible.pieceType;

            // Check if already collected
            bool alreadyCollected = false;
            foreach (MaskPiece piece in maskPieces)
            {
                if (piece.pieceName == pieceType && piece.isCollected)
                {
                    alreadyCollected = true;
                    break;
                }
            }

            if (alreadyCollected)
            {
                Debug.LogWarning($"Already collected {pieceType}!");
                return;
            }

            CollectPiece(pieceType);
            Destroy(nearbyCollectible.gameObject);
            nearbyCollectible = null;
        }
    }

    public void CollectPiece(string pieceType)
    {
        Debug.Log($"=== CollectPiece called for: {pieceType} ===");

        foreach (MaskPiece piece in maskPieces)
        {
            if (piece.pieceName == pieceType && !piece.isCollected)
            {
                piece.isCollected = true;

                if (piece.pieceOnCharacter != null)
                {
                    piece.pieceOnCharacter.SetActive(true);
                    Debug.Log($"✓ Collected {pieceType}! Piece activated on character.");

                    // Play collection sound effect
                    if (sfxAudioSource != null && collectionSound != null)
                    {
                        sfxAudioSource.PlayOneShot(collectionSound);
                        Debug.Log("Played collection SFX");
                    }

                    // Check voiceover clip
                    if (piece.voiceoverClip != null)
                    {
                        Debug.Log($"🎤 Voiceover clip found: {piece.voiceoverClip.name}, starting coroutine...");
                        StartCoroutine(PlayVoiceoverDelayed(piece.voiceoverClip, voiceoverDelay));
                    }
                    else
                    {
                        Debug.LogError($"❌ NO VOICEOVER CLIP assigned for {pieceType}!");
                    }

                    // NOTIFY OBJECTIVE CONTROLLER
                    if (objectiveController != null)
                    {
                        string taskName = maskCollectionTaskPrefix + " " + pieceType;
                        Debug.Log($"Completing objective task: {taskName}");
                        objectiveController.CompleteTask(taskName);
                    }

                    CheckIfMaskComplete();
                }
                else
                {
                    Debug.LogWarning($"Piece on character is null for {pieceType}!");
                }
                return;
            }
        }

        Debug.LogWarning($"Could not find or already collected piece: {pieceType}");
    }

    private IEnumerator PlayVoiceoverDelayed(AudioClip clip, float delay)
    {
        Debug.Log($"Coroutine started. Waiting {delay} seconds...");
        yield return new WaitForSeconds(delay);

        Debug.Log($"Delay complete. Playing voiceover now...");

        if (voiceoverAudioSource == null)
        {
            Debug.LogError("❌ VoiceoverAudioSource is NULL!");
            yield break;
        }

        if (clip == null)
        {
            Debug.LogError("❌ AudioClip is NULL!");
            yield break;
        }

        Debug.Log($"🎤 Playing voiceover: {clip.name}");
        Debug.Log($"   - Clip length: {clip.length}s");
        Debug.Log($"   - Source volume: {voiceoverAudioSource.volume}");
        Debug.Log($"   - Source enabled: {voiceoverAudioSource.enabled}");

        voiceoverAudioSource.PlayOneShot(clip);

        Debug.Log("✓ PlayOneShot called!");
    }

    private void CheckIfMaskComplete()
    {
        bool allCollected = true;

        foreach (MaskPiece piece in maskPieces)
        {
            if (!piece.isCollected)
            {
                allCollected = false;
                break;
            }
        }

        if (allCollected)
        {
            OnMaskComplete();
        }
    }

    private void OnMaskComplete()
    {
        Debug.Log("🎭 Mask Complete! All pieces collected!");
    }

    public void ActivatePiece(string pieceType)
    {
        CollectPiece(pieceType);
    }

    public bool IsPieceCollected(string pieceType)
    {
        foreach (MaskPiece piece in maskPieces)
        {
            if (piece.pieceName == pieceType)
            {
                return piece.isCollected;
            }
        }
        return false;
    }

    public int GetCollectedCount()
    {
        int count = 0;
        foreach (MaskPiece piece in maskPieces)
        {
            if (piece.isCollected) count++;
        }
        return count;
    }

    public void ResetMask()
    {
        foreach (MaskPiece piece in maskPieces)
        {
            piece.isCollected = false;

            if (piece.pieceOnCharacter != null)
            {
                piece.pieceOnCharacter.SetActive(false);
            }
        }

        Debug.Log("🔄 Mask reset!");
    }

    public void StopVoiceover()
    {
        if (voiceoverAudioSource != null)
        {
            voiceoverAudioSource.Stop();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectionRange);
    }
}