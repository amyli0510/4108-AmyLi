using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

/// <summary>
/// Orchestrates the minigames. There is only one manager per scene (a singleton).
/// It keeps a list of minigames, starts a chosen one from the inspector, and surfaces
/// the active minigame's live countdown / score. Each minigame saves its own high score.
/// </summary>
public class Minigame_Manager : MonoBehaviour
{
    public static Minigame_Manager Instance { get; private set; }

    [Title("Minigames")]
    [HelpBox("Drop each Minigame_ component here, then start one with the buttons below.", MessageMode.Log)]
    [SerializeField] private List<Minigame_Base> minigames = new();

    [Title("Session Countdown")]
    [Tooltip("An optional overall timer for a whole play session, independent of the minigames.")]
    [SerializeField] private Countdown countdown = new();

    [Title("Score")]
    [ReadOnly, SerializeField] private int score;

    [Title("Runtime (read-only)")]
    [ReadOnly, SerializeField] private string activeMinigame = "None";
    [ReadOnly, SerializeField] private float activeTimeRemaining;
    [ReadOnly, SerializeField] private int activeScore;

    private Minigame_Base active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[Minigame_Manager] A second manager exists on '{name}'. Disabling this duplicate.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Drive the optional session-wide countdown.
        countdown.Tick(Time.deltaTime);

        // Mirror the active minigame's live state into the inspector.
        if (active != null)
        {
            activeTimeRemaining = active.CountdownTimeRemaining;
            activeScore = active.Score;
            score = active.Score;

            if (!active.IsPlaying)
            {
                activeMinigame = "None";
                active = null;
            }
        }
    }

    // --- Session countdown -------------------------------------------------
    [Button("Start Session Countdown")]
    private void StartCountdown() => countdown.Begin();

    [Button("Stop Session Countdown")]
    private void StopCountdown() => countdown.Stop();

    // --- Minigame control --------------------------------------------------
    [Button("Start Minigame By Index")]
    private void StartMinigameByIndex(int index)
    {
        if (index < 0 || index >= minigames.Count || minigames[index] == null)
        {
            Debug.LogWarning($"[Minigame_Manager] No minigame at index {index}.", this);
            return;
        }

        StartMinigame(minigames[index]);
    }

    [Button("Stop Active Minigame")]
    private void StopActiveMinigame()
    {
        if (active != null)
            active.EndMinigame();
    }

    /// <summary>Starts the given minigame, stopping any one already running.</summary>
    public void StartMinigame(Minigame_Base minigame)
    {
        if (minigame == null)
            return;

        if (active != null && active.IsPlaying)
            active.EndMinigame();

        active = minigame;
        activeMinigame = minigame.MinigameId;
        minigame.StartMinigame();
    }
}
