using EditorAttributes;
using UnityEngine;

/// <summary>
/// The toothbrush bristle contact area. Exposes a world-space contact point (offset from
/// the pivot) and a radius, both visualized with a gizmo. The brushing minigame uses these
/// to decide which plaque the brush is touching. Also swaps its sprite between an idle and
/// a moving look based on whether the brush is being moved.
/// </summary>
public class Toothbrush : MonoBehaviour
{
    [Title("Brush Contact")]
    [Tooltip("Local offset from the toothbrush pivot to the bristle contact point.")]
    public Vector2 brushArea;

    [Tooltip("Radius around the contact point that counts as brushing.")]
    [Suffix("units")]
    public float brushRadius = 0.5f;

    [Title("Sprites")]
    [Tooltip("Sprite renderer to swap. Auto-found on this object (or children) if empty.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Shown while the brush is still.")]
    [SerializeField] private Sprite notMovingSprite;

    [Tooltip("Shown while the brush is being moved.")]
    [SerializeField] private Sprite movingSprite;

    [Tooltip("Speed above which the brush counts as moving.")]
    [Suffix("units/s")]
    [SerializeField] private float moveSpeedThreshold = 0.1f;

    [Tooltip("Keeps the moving sprite this long after motion stops (avoids flicker at stroke turns).")]
    [Suffix("seconds")]
    [SerializeField] private float moveHoldTime = 0.1f;

    /// <summary>World position of the bristle contact point (respects the brush's transform).</summary>
    public Vector2 ContactPoint => transform.TransformPoint(brushArea);

    public float Radius => brushRadius;

    public bool IsMoving { get; private set; }

    private Vector3 prevPosition;
    private float moveTimer;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        prevPosition = transform.position;
        ApplySprite(false);
    }

    private void OnEnable()
    {
        // Reset so re-activating the brush doesn't read a stale "moved" jump.
        prevPosition = transform.position;
        moveTimer = 0f;
        ApplySprite(false);
    }

    private void Update()
    {
        Vector3 pos = transform.position;
        float speed = Time.deltaTime > 0f ? (pos - prevPosition).magnitude / Time.deltaTime : 0f;
        prevPosition = pos;

        if (speed > moveSpeedThreshold)
            moveTimer = moveHoldTime;
        else
            moveTimer -= Time.deltaTime;

        ApplySprite(moveTimer > 0f);
    }

    private void ApplySprite(bool moving)
    {
        IsMoving = moving;

        if (spriteRenderer == null)
            return;

        Sprite target = moving ? movingSprite : notMovingSprite;
        if (target != null && spriteRenderer.sprite != target)
            spriteRenderer.sprite = target;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 contact = transform.TransformPoint(brushArea);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(contact, brushRadius);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawSphere(contact, brushRadius);
    }
#endif
}
