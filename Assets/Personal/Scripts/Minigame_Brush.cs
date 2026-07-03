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

    [Title("Audio")]
    [Tooltip("Looping sound played while brushing over a tooth (Audio_Manager clip name).")]
    [SerializeField] private string brushingLoopSound = "Brushing";
    [Tooltip("One-shot sound when a tooth is fully cleaned.")]
    [SerializeField] private string cleanedSound = "Brush Clean";

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

        // Start the brushing loop paused; it resumes while actually scrubbing a tooth.
        if (Audio_Manager.Instance != null)
        {
            Audio_Manager.Instance.PlayLoop(brushingLoopSound);
            Audio_Manager.Instance.SetLoopPaused(true);
        }
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
        bool moving = horizontalTravel > 0f;
        float amount = horizontalTravel * cleanSpeed;

        bool overTooth = false;
        IReadOnlyList<Tooth> teeth = registry.All;
        for (int i = 0; i < teeth.Count; i++)
        {
            Tooth t = teeth[i];
            if (t == null)
                continue;

            if (t.ContainsPoint(contact))
                overTooth = true;

            if (moving && !t.BrushCleaned && t.FadeBrushPlaqueInRadius(contact, toothbrush.Radius, amount))
            {
                AddScore(pointsPerTooth);
                Audio_Manager.Instance?.Play(cleanedSound);
            }
        }

        // Brushing sound plays while scrubbing over a tooth; pauses otherwise.
        Audio_Manager.Instance?.SetLoopPaused(!(moving && overTooth));

        // Finish early once every tooth is clean.
        if (registry.AllBrushCleaned())
            EndMinigame();
    }

    protected override void OnMinigameEnded()
    {
        SetBrushActive(false);
        Audio_Manager.Instance?.StopLoop();
    }

    private void SetBrushActive(bool active)
    {
        if (toothbrush != null)
            toothbrush.gameObject.SetActive(active);
    }
}
