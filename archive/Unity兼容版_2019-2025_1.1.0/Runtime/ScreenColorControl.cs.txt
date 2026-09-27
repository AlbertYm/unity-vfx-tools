using UnityEngine;

public class ScreenColorControl : MonoBehaviour
{
    public Color color1 = Color.black; // First color (default: black)
    public Color color2 = Color.white; // Second color (default: white)

    private Color currentColor;
    private Camera cam;

    void Start()
    {
        // Get the camera component
        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main camera not found!");
            enabled = false;
            return;
        }

        // Set initial color
        currentColor = color1;
        cam.backgroundColor = currentColor;
    }

    void Update()
    {
        // Toggle color based on user input
        if (SpacePressed()) // Works with either project input backend.
        {
            ToggleColor();
        }
    }

    private static bool SpacePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    void ToggleColor()
    {
        if (currentColor == color1)
        {
            currentColor = color2;
        }
        else
        {
            currentColor = color1;
        }

        // Apply the new color to the camera background
        cam.backgroundColor = currentColor;
    }

    void OnValidate()
    {
        // Apply the current color to the camera background when parameters change in the Inspector
        if (cam != null)
        {
            cam.backgroundColor = currentColor;
        }
    }
}
