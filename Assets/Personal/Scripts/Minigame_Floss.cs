using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Flossing minigame: hover a tooth to make it the target (it pops in front of the floss),
/// then move the mouse up/down in the tooth's flossing direction to scrub off its floss
/// plaque. Each fully-flossed tooth scores a point. The floss is hidden until the round starts.
/// </summary>
public class Minigame_Floss : Minigame_Base
{
    [Title("Floss Settings")]
    [Tooltip("The floss object (has Movement_FollowMouse). Disabled until the game starts.")]
    [Required]
    [SerializeField] private GameObject floss;

    [Tooltip("Optional. Leave empty to use the single Teeth registry in the scene.")]
    [SerializeField] private Teeth teethRegistry;

    [Tooltip("Camera used to read the mouse position. Defaults to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Alpha removed per world-unit of correct-direction flossing. Lower = more strokes.")]
    [Suffix("alpha / unit")]
    [SerializeField] private float cleanSpeed = 0.8f;

    [Tooltip("Points awarded for each fully-flossed tooth.")]
    [SerializeField] private int pointsPerTooth = 1;

    [Title("Audio")]
    [Tooltip("One-shot sound when a tooth is fully cleaned.")]
    [SerializeField] private string cleanedSound = "Floss Clean";

    private Tooth target;
    private float prevMouseY;
    private bool hasPrevMouseY;

    private Teeth Registry => teethRegistry != null ? teethRegistry : Teeth.Instance;

    protected override void Awake()
    {
        base.Awake();

        if (targetCamera == null)
            targetCamera = Camera.main;

        SetFlossActive(false); // hidden until the round starts
    }

    protected override void OnMinigameStarted()
    {
        Teeth registry = Registry;
        if (registry != null)
        {
            IReadOnlyList<Tooth> teeth = registry.All;
            for (int i = 0; i < teeth.Count; i++)
            {
                Tooth t = teeth[i];
                if (t == null) continue;
                t.ResetFlossPlaque();
                t.SetForeground(false);
            }
        }

        target = null;
        hasPrevMouseY = false;
        SetFlossActive(true);
    }

    protected override void OnMinigameTick(float deltaTime)
    {
        if (targetCamera == null || Mouse.current == null)
            return;

        Vector2 mouseWorld = GetMouseWorld();
        UpdateTarget(mouseWorld);

        // Finish early once every tooth is clean.
        Teeth registry = Registry;
        if (registry != null && registry.AllFlossCleaned())
        {
            EndMinigame();
            return;
        }

        float dy = hasPrevMouseY ? mouseWorld.y - prevMouseY : 0f;
        prevMouseY = mouseWorld.y;
        hasPrevMouseY = true;

        if (target == null)
            return;

        // Bottom tooth (upright) flosses upward; top tooth flosses downward.
        float direction = target.upright ? 1f : -1f;
        float amount = Mathf.Max(0f, direction * dy) * cleanSpeed;
        if (amount <= 0f)
            return;

        if (target.FadeFlossPlaque(amount))
        {
            AddScore(pointsPerTooth);
            Audio_Manager.Instance?.Play(cleanedSound);
        }
    }

    protected override void OnMinigameEnded()
    {
        if (target != null)
            target.SetForeground(false);
        target = null;

        SetFlossActive(false);
    }

    // Hover-pick the tooth under the cursor and toggle its sorting order.
    private void UpdateTarget(Vector2 mouseWorld)
    {
        Tooth hovered = FindToothAt(mouseWorld);
        if (hovered == target)
            return;

        if (target != null)
            target.SetForeground(false); // back to default (-10): behind the floss

        target = hovered;

        if (target != null)
            target.SetForeground(true); // to 10: in front of the floss
    }

    private Tooth FindToothAt(Vector2 point)
    {
        Teeth registry = Registry;
        if (registry == null)
            return null;

        Tooth best = null;
        float bestSqr = float.MaxValue;

        IReadOnlyList<Tooth> teeth = registry.All;
        for (int i = 0; i < teeth.Count; i++)
        {
            Tooth t = teeth[i];
            if (t == null || !t.ContainsPoint(point))
                continue;

            float sqr = (t.Center - point).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        return best;
    }

    private Vector2 GetMouseWorld()
    {
        Vector3 screen = Mouse.current.position.ReadValue();
        screen.z = Mathf.Abs(targetCamera.transform.position.z);
        return targetCamera.ScreenToWorldPoint(screen);
    }

    private void SetFlossActive(bool active)
    {
        if (floss != null)
            floss.SetActive(active);
    }
}
