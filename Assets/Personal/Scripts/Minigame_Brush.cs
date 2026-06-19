using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

/// <summary>
/// Brushing minigame: move the toothbrush side to side over the teeth to scrub away the
/// brush plaque. Each fully-cleaned tooth scores a point. The toothbrush is hidden until
/// the round starts.
/// </summary>
public class Minigame_Brush : Minigame_Base
{
    [Title("Brush Settings")]
    [Tooltip("The toothbrush (has Movement_FollowMouse + Toothbrush). Disabled until the game starts.")]
    [Required]
    [SerializeField] private Toothbrush toothbrush;

    [Tooltip("Optional. Leave empty to use the single Teeth registry in the scene.")]
    [SerializeField] private Teeth teethRegistry;

    [Tooltip("Alpha removed per world-unit of sideways scrubbing. Lower = more strokes needed.")]
    [Suffix("alpha / unit")]
    [SerializeField] private float cleanSpeed = 0.6f;

    [Tooltip("Points awarded for each fully-cleaned tooth.")]
    [SerializeField] private int pointsPerTooth = 1;

    private float prevContactX;

    private Teeth Registry => teethRegistry != null ? teethRegistry : Teeth.Instance;

    protected override void Awake()
    {
        base.Awake();
        SetBrushActive(false); // hidden until the round starts
    }

    protected override void OnMinigameStarted()
    {
        Teeth registry = Registry;
        if (registry != null)
        {
            IReadOnlyList<Tooth> teeth = registry.All;
            for (int i = 0; i < teeth.Count; i++)
                if (teeth[i] != null) teeth[i].ResetBrushPlaque();
        }

        SetBrushActive(true);

        if (toothbrush != null)
            prevContactX = toothbrush.ContactPoint.x;
    }

    protected override void OnMinigameTick(float deltaTime)
    {
        if (toothbrush == null)
            return;

        Teeth registry = Registry;
        if (registry == null)
            return;

        Vector2 contact = toothbrush.ContactPoint;

        // Only sideways (side-to-side) motion scrubs — vertical motion does nothing.
        float horizontalTravel = Mathf.Abs(contact.x - prevContactX);
        prevContactX = contact.x;
        if (horizontalTravel <= 0f)
            return;

        float amount = horizontalTravel * cleanSpeed;

        IReadOnlyList<Tooth> teeth = registry.All;
        for (int i = 0; i < teeth.Count; i++)
        {
            Tooth t = teeth[i];
            if (t == null || t.BrushCleaned)
                continue;

            if (t.FadeBrushPlaqueInRadius(contact, toothbrush.Radius, amount))
                AddScore(pointsPerTooth);
        }
    }

    protected override void OnMinigameEnded()
    {
        SetBrushActive(false);
    }

    private void SetBrushActive(bool active)
    {
        if (toothbrush != null)
            toothbrush.gameObject.SetActive(active);
    }
}
