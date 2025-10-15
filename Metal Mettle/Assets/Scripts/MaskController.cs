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

        [Header("Ability Unlock")]
        [Tooltip("Ability to unlock when this piece is collected (leave empty for none)")]
        public string abilityToUnlock = ""; // Options: light_attack, heavy_attack, blood_absorption, execution, desperation
        [Tooltip("Show unlock message in console")]
        public bool showUnlockMessage = true;
    }

    [Header("Mask Pieces on Character")]
    [SerializeField] private List<MaskPiece> maskPieces = new List<MaskPiece>();

    [Header("Collection Settings")]
    [SerializeField] private LayerMask collectibleLayer;
    [SerializeField] private float collectionRange = 2f;

    [Header("Cutscene Settings")]
    [SerializeField] private bool playCutsceneOnCollection = true;
    [SerializeField] private Transform playerHeadTransform;
    [SerializeField] private float cutsceneDuration = 3f;
    [SerializeField] private float cameraDistance = 1.5f;
    [SerializeField] private float horizontalAngle = 45f;
    [SerializeField] private float verticalAngle = 10f;
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0, 0, 0);
    [Tooltip("Time before voiceover starts (allows cutscene to settle)")]
    [SerializeField] private float voiceoverStartDelay = 1f;

    [Header("Input")]
    [SerializeField] private InputSystem_Actions controls;
    private InputAction executionInput;

    [Header("References")]
    [SerializeField] private ObjectiveController objectiveController;
    [SerializeField] private BloodSystem bloodSystem;
    [SerializeField] private string maskCollectionTaskPrefix = "Collect";

    [Header("Audio")]
    [SerializeField] private AudioClip collectionSound;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource voiceoverAudioSource;

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

        // Setup audio sources
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

        // Configure voiceover audio source
        if (voiceoverAudioSource != null)
        {
            voiceoverAudioSource.playOnAwake = false;
            voiceoverAudioSource.spatialBlend = 0f;
            Debug.Log($"Voiceover AudioSource configured: {voiceoverAudioSource.gameObject.name}");
        }

        // Auto-find objective controller
        if (objectiveController == null)
        {
            objectiveController = FindFirstObjectByType<ObjectiveController>();
        }

        // Auto-find blood system
        if (bloodSystem == null)
        {
            bloodSystem = GetComponent<BloodSystem>();
            if (bloodSystem == null)
            {
                Debug.LogWarning("BloodSystem not found! Ability unlocks will not work.");
            }
        }

        // Auto-find player head
        if (playerHeadTransform == null)
        {
            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                playerHeadTransform = animator.GetBoneTransform(HumanBodyBones.Head);
                if (playerHeadTransform != null)
                {
                    Debug.Log($"Auto-found player head: {playerHeadTransform.name}");
                }
            }

            if (playerHeadTransform == null)
            {
                Debug.LogWarning("Player head transform not found! Using player root as fallback.");
                playerHeadTransform = transform;
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"MaskController started. {maskPieces.Count} pieces configured.");
            Debug.Log($"Cutscene enabled: {playCutsceneOnCollection}");
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
                break;
            }
        }
    }

    private void OnExecutionPressed(InputAction.CallbackContext context)
    {
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

                    // Play collection sound
                    if (sfxAudioSource != null && collectionSound != null)
                    {
                        sfxAudioSource.PlayOneShot(collectionSound);
                    }

                    // Complete objective
                    if (objectiveController != null)
                    {
                        string taskName = maskCollectionTaskPrefix + " " + pieceType;
                        objectiveController.CompleteTask(taskName);
                    }

                    // UNLOCK ABILITY
                    if (!string.IsNullOrEmpty(piece.abilityToUnlock) && bloodSystem != null)
                    {
                        bloodSystem.UnlockAbility(piece.abilityToUnlock);

                        if (piece.showUnlockMessage)
                        {
                            Debug.Log($"🔓 Unlocked ability: {piece.abilityToUnlock} from collecting {pieceType}");
                        }
                    }

                    // Play cutscene
                    if (playCutsceneOnCollection && CutsceneCameraController.Instance != null)
                    {
                        PlayMaskCollectionCutscene(piece);
                    }
                    else
                    {
                        if (piece.voiceoverClip != null)
                        {
                            StartCoroutine(PlayVoiceoverDelayed(piece.voiceoverClip, voiceoverStartDelay));
                        }
                    }

                    CheckIfMaskComplete();
                }
                return;
            }
        }
    }

    private void PlayMaskCollectionCutscene(MaskPiece piece)
    {
        if (playerHeadTransform == null)
        {
            Debug.LogError("Cannot play cutscene - player head transform not assigned!");
            return;
        }

        Debug.Log($"🎬 Playing mask collection cutscene for {piece.pieceName}");

        // Start voiceover
        if (piece.voiceoverClip != null)
        {
            StartCoroutine(PlayVoiceoverDelayed(piece.voiceoverClip, voiceoverStartDelay));
        }

        // Calculate camera position
        Vector3 headPosition = playerHeadTransform.position;
        Vector3 playerForward = transform.forward;
        Vector3 playerRight = transform.right;
        Vector3 worldUp = Vector3.up;

        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
        float verticalRad = verticalAngle * Mathf.Deg2Rad;

        Vector3 horizontalDirection = (playerForward * Mathf.Cos(horizontalRad) + playerRight * Mathf.Sin(horizontalRad));
        Vector3 direction = (horizontalDirection * Mathf.Cos(verticalRad) + worldUp * Mathf.Sin(verticalRad)).normalized;

        Vector3 cameraPosition = headPosition + direction * cameraDistance;
        Vector3 lookTarget = headPosition + lookAtOffset;
        Quaternion cameraRotation = Quaternion.LookRotation(lookTarget - cameraPosition);

        // Play cutscene
        CutsceneCameraController.Instance.PlayCustomCutscene(
            cameraPosition,
            cameraRotation,
            cutsceneDuration,
            OnCutsceneComplete
        );
    }

    private void OnCutsceneComplete()
    {
        Debug.Log("🎬 Mask collection cutscene completed!");
    }

    private IEnumerator PlayVoiceoverDelayed(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (voiceoverAudioSource != null && clip != null)
        {
            voiceoverAudioSource.PlayOneShot(clip);
        }
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
            Debug.Log("🎭 Mask Complete! All pieces collected!");
        }
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

        if (playCutsceneOnCollection && playerHeadTransform != null)
        {
            Vector3 headPosition = playerHeadTransform.position;
            Vector3 playerForward = transform.forward;
            Vector3 playerRight = transform.right;
            Vector3 worldUp = Vector3.up;

            float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
            float verticalRad = verticalAngle * Mathf.Deg2Rad;

            Vector3 horizontalDirection = (playerForward * Mathf.Cos(horizontalRad) + playerRight * Mathf.Sin(horizontalRad));
            Vector3 direction = (horizontalDirection * Mathf.Cos(verticalRad) + worldUp * Mathf.Sin(verticalRad)).normalized;

            Vector3 cameraPosition = headPosition + direction * cameraDistance;
            Vector3 lookTarget = headPosition + lookAtOffset;

            // Draw camera position
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cameraPosition, 0.2f);

            // Draw look target
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lookTarget, 0.15f);

            // Draw line from camera to target
            Gizmos.color = Color.red;
            Gizmos.DrawLine(cameraPosition, lookTarget);

            // Draw orbit circle
            Gizmos.color = Color.yellow;
            int segments = 36;
            Vector3 prevPoint = headPosition + playerForward * cameraDistance;
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                Vector3 circleOffset = (playerForward * Mathf.Cos(angle) + playerRight * Mathf.Sin(angle)) * cameraDistance;
                Vector3 point = headPosition + circleOffset;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(cameraPosition, $"Camera ({horizontalAngle}°, {verticalAngle}°)");
            UnityEditor.Handles.Label(lookTarget, "Look Target");
#endif
        }
    }
}