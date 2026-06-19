using EditorAttributes;
using UnityEngine;
using UnityEngine.InputSystem;

public class Movement_FollowMouse : MonoBehaviour
{
    [Title("Follow")]
    [Tooltip("Higher = floatier, lower = snappier. Approx. seconds to reach the cursor.")]
    [Suffix("seconds")]
    [SerializeField] private float smoothTime = 0.1f;

    [Tooltip("Camera used to convert the mouse position into world space. Defaults to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("World-space offset from the cursor, e.g. (0, 1) keeps the sprite a unit above the mouse.")]
    [SerializeField] private Vector2 followOffset = Vector2.zero;

    [Title("Screen Bounds")]
    [Tooltip("Clamp the cursor position to stay inside the camera's view.")]
    [SerializeField] private bool clampToScreen = false;

    [Tooltip("Extra gap kept between the cursor and the screen edge, in pixels.")]
    [ShowField(nameof(clampToScreen))]
    [Suffix("px")]
    [SerializeField] private float screenPadding = 0f;

    private Vector3 velocity;
    private float startZ;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        // Keep whatever depth the object started at (matters for sorting / perspective).
        startZ = transform.position.z;
    }

    void Update()
    {
        if (targetCamera == null || Mouse.current == null)
            return;

        // Mouse position in screen pixels.
        Vector3 mouseScreen = Mouse.current.position.ReadValue();

        // Keep the cursor itself inside the view before converting to world space.
        if (clampToScreen)
            mouseScreen = ClampMouseToScreen(mouseScreen);

        // Distance from the camera to the object's plane, so this works for both
        // orthographic (2D) and perspective cameras.
        mouseScreen.z = Mathf.Abs(targetCamera.transform.position.z - startZ);

        Vector3 target = targetCamera.ScreenToWorldPoint(mouseScreen);
        target.x += followOffset.x;
        target.y += followOffset.y;
        target.z = startZ;

        // Smoothly ease toward the cursor instead of snapping.
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime);
    }

    /// <summary>Clamps a screen-pixel position to the camera's viewport, with padding.</summary>
    private Vector3 ClampMouseToScreen(Vector3 screen)
    {
        Rect rect = targetCamera.pixelRect;
        screen.x = Mathf.Clamp(screen.x, rect.xMin + screenPadding, rect.xMax - screenPadding);
        screen.y = Mathf.Clamp(screen.y, rect.yMin + screenPadding, rect.yMax - screenPadding);
        return screen;
    }
}
