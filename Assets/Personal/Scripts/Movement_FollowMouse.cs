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
    [Tooltip("Keep the sprite fully inside the camera's view.")]
    [SerializeField] private bool clampToScreen = false;

    [Tooltip("Renderer used to measure the sprite's size. Auto-found on this object (or children) if left empty.")]
    [ShowField(nameof(clampToScreen))]
    [SerializeField] private Renderer boundsSource;

    [Tooltip("Extra gap kept between the sprite's edge and the screen edge.")]
    [ShowField(nameof(clampToScreen))]
    [Suffix("units")]
    [SerializeField] private float screenPadding = 0f;

    private Vector3 velocity;
    private float startZ;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        // Keep whatever depth the object started at (matters for sorting / perspective).
        startZ = transform.position.z;

        if (boundsSource == null)
            boundsSource = GetComponentInChildren<Renderer>();
    }

    void Update()
    {
        if (targetCamera == null || Mouse.current == null)
            return;

        // Mouse position in screen pixels.
        Vector3 mouseScreen = Mouse.current.position.ReadValue();

        // Distance from the camera to the object's plane, so this works for both
        // orthographic (2D) and perspective cameras.
        mouseScreen.z = Mathf.Abs(targetCamera.transform.position.z - startZ);

        Vector3 target = targetCamera.ScreenToWorldPoint(mouseScreen);
        target.x += followOffset.x;
        target.y += followOffset.y;
        target.z = startZ;

        // Clamp the target (not the smoothed result) so the sprite eases to a stop
        // at the edge instead of fighting the boundary.
        if (clampToScreen)
            target = ClampToScreen(target);

        // Smoothly ease toward the cursor instead of snapping.
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime);
    }

    /// <summary>Clamps a world position so the sprite's bounds stay inside the camera view.</summary>
    private Vector3 ClampToScreen(Vector3 position)
    {
        // World-space corners of the camera view at the object's depth (works for ortho + perspective).
        float zDist = Mathf.Abs(targetCamera.transform.position.z - startZ);
        Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, zDist));
        Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, zDist));

        // Half-size of the sprite (extents already account for scale), plus padding.
        float halfWidth = screenPadding;
        float halfHeight = screenPadding;
        if (boundsSource != null)
        {
            Vector3 extents = boundsSource.bounds.extents;
            halfWidth += extents.x;
            halfHeight += extents.y;
        }

        float minX = bottomLeft.x + halfWidth;
        float maxX = topRight.x - halfWidth;
        float minY = bottomLeft.y + halfHeight;
        float maxY = topRight.y - halfHeight;

        // If the sprite is wider/taller than the screen, just center it on that axis.
        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : (bottomLeft.x + topRight.x) * 0.5f;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : (bottomLeft.y + topRight.y) * 0.5f;

        return position;
    }
}
