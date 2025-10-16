using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public InputSystem_Actions Controls { get; private set; }

    [Header("Settings")]
    public bool invertHorizontal;
    public bool invertVertical;

    private const string PrefInvertH = "InvertHorizontal";
    private const string PrefInvertV = "InvertVertical";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load saved values
        invertHorizontal = PlayerPrefs.GetInt(PrefInvertH, 0) == 1; // default false
        invertVertical = PlayerPrefs.GetInt(PrefInvertV, 0) == 1;   // default false

        

        Controls = new InputSystem_Actions();
        Controls.Enable();
    }

    public void ToggleInvertVertical()
    {
        invertVertical = !invertVertical;
        PlayerPrefs.SetInt(PrefInvertV, invertVertical ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetInvertHorizontal(bool value)
    {
        invertHorizontal = value;
        PlayerPrefs.SetInt(PrefInvertH, invertHorizontal ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetInvertVertical(bool value)
    {
        invertVertical = value;
        PlayerPrefs.SetInt(PrefInvertV, invertVertical ? 1 : 0);
        PlayerPrefs.Save();
    }

    // --- Get look input with inversion applied ---
    public Vector2 GetLookInput()
    {
        Vector2 look = Controls.Player.Look.ReadValue<Vector2>();

        if (invertHorizontal) look.x = -look.x;
        if (invertVertical) look.y = -look.y;

        return look;
    }
}
