using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Creates blood splatter decals on surfaces using Unity's built-in projection system
/// Integrates with your existing blood system
/// </summary>
public class BloodSplatterManager : MonoBehaviour
{
    [Header("Decal Settings")]
    [SerializeField] private Material bloodDecalMaterial;
    [SerializeField] private Vector2 decalSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private float decalDepth = 0.1f;
    [SerializeField] private LayerMask surfaceLayers;

    [Header("Splatter Variations")]
    [SerializeField] private Texture2D[] splatterTextures;
    [SerializeField] private int maxDecals = 50;

    [Header("Spawn Settings")]
    [SerializeField] private float minScale = 0.8f;
    [SerializeField] private float maxScale = 1.2f;
    [SerializeField] private float fadeTime = 30f; // Decals fade after 30 seconds

    // Object pooling
    private Queue<GameObject> decalPool = new Queue<GameObject>();
    private List<DecalFader> activeDecals = new List<DecalFader>();

    private static BloodSplatterManager instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Pre-populate decal pool
        for (int i = 0; i < maxDecals; i++)
        {
            CreateDecalObject();
        }
    }

    private GameObject CreateDecalObject()
    {
        GameObject decal = new GameObject("BloodDecal");
        decal.transform.SetParent(transform);

        // Create a quad for the decal
        MeshFilter meshFilter = decal.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = decal.AddComponent<MeshRenderer>();

        // Create quad mesh
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0)
        };

        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshRenderer.material = bloodDecalMaterial;

        // Add fader component
        DecalFader fader = decal.AddComponent<DecalFader>();
        fader.fadeTime = fadeTime;

        decal.SetActive(false);
        decalPool.Enqueue(decal);

        return decal;
    }

    /// <summary>
    /// Spawn a blood splatter at the given position and surface normal
    /// </summary>
    public static void SpawnSplatter(Vector3 position, Vector3 normal)
    {
        if (instance == null) return;
        instance.CreateSplatter(position, normal);
    }

    private void CreateSplatter(Vector3 position, Vector3 normal)
    {
        // Get decal from pool
        GameObject decal;
        if (decalPool.Count > 0)
        {
            decal = decalPool.Dequeue();
        }
        else
        {
            // Reuse oldest decal if pool empty
            if (activeDecals.Count > 0)
            {
                DecalFader oldest = activeDecals[0];
                activeDecals.RemoveAt(0);
                decal = oldest.gameObject;
            }
            else
            {
                return; // No decals available
            }
        }

        // Position and orient decal
        decal.transform.position = position + normal * 0.01f; // Slight offset to prevent z-fighting
        decal.transform.rotation = Quaternion.LookRotation(-normal);

        // Random rotation around normal
        decal.transform.Rotate(0, 0, Random.Range(0f, 360f));

        // Random scale variation
        float scale = Random.Range(minScale, maxScale);
        decal.transform.localScale = new Vector3(
            decalSize.x * scale,
            decalSize.y * scale,
            decalDepth
        );

        // Random splatter texture
        if (splatterTextures != null && splatterTextures.Length > 0)
        {
            MeshRenderer renderer = decal.GetComponent<MeshRenderer>();
            renderer.material.mainTexture = splatterTextures[Random.Range(0, splatterTextures.Length)];
        }

        // Activate and start fading
        decal.SetActive(true);
        DecalFader fader = decal.GetComponent<DecalFader>();
        fader.StartFade();
        activeDecals.Add(fader);
    }

    /// <summary>
    /// Return a decal to the pool
    /// </summary>
    public void ReturnToPool(GameObject decal)
    {
        decal.SetActive(false);
        DecalFader fader = decal.GetComponent<DecalFader>();
        activeDecals.Remove(fader);
        decalPool.Enqueue(decal);
    }
}

/// <summary>
/// Handles fading out blood decals over time
/// </summary>
public class DecalFader : MonoBehaviour
{
    public float fadeTime = 30f;
    private float currentTime = 0f;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private bool isFading = false;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    public void StartFade()
    {
        currentTime = 0f;
        isFading = true;

        // Reset alpha
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat("_Alpha", 1f);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    void Update()
    {
        if (!isFading) return;

        currentTime += Time.deltaTime;
        float alpha = 1f - (currentTime / fadeTime);

        if (alpha <= 0f)
        {
            isFading = false;
            FindObjectOfType<BloodSplatterManager>().ReturnToPool(gameObject);
            return;
        }

        // Update alpha
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat("_Alpha", alpha);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}