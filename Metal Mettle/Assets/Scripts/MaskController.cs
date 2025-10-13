using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MaskController : MonoBehaviour
{
    [System.Serializable]
    public class MaskPiece
    {
        public string pieceName;
        public GameObject pieceOnCharacter;
        public bool isCollected = false;
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

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip collectionSound;
    private AudioSource audioSource;

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

        audioSource = GetComponent<AudioSource>();

        // Auto-find objective controller if not set
        if (objectiveController == null)
        {
            objectiveController = FindFirstObjectByType<ObjectiveController>();
        }

        if (showDebugLogs)
        {
            Debug.Log($"MaskController started. {maskPieces.Count} pieces configured.");
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
        if (showDebugLogs)
        {
            Debug.Log($"Attempting to collect: {pieceType}");
        }

        foreach (MaskPiece piece in maskPieces)
        {
            if (piece.pieceName == pieceType && !piece.isCollected)
            {
                piece.isCollected = true;

                if (piece.pieceOnCharacter != null)
                {
                    piece.pieceOnCharacter.SetActive(true);
                    Debug.Log($"✓ Collected {pieceType}! Piece activated on character.");

                    if (audioSource != null && collectionSound != null)
                    {
                        audioSource.PlayOneShot(collectionSound);
                    }

                    // NOTIFY OBJECTIVE CONTROLLER
                    if (objectiveController != null)
                    {
                        string taskName = maskCollectionTaskPrefix + " " + pieceType;
                        Debug.Log($"Completing objective task: {taskName}");
                        objectiveController.CompleteTask(taskName);
                    }
                    else
                    {
                        Debug.LogWarning("ObjectiveController is null! Can't complete task.");
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectionRange);
    }
}