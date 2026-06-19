using System.Collections.Generic;
using EditorAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Title("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text highScoreText;

    [Tooltip("{0} = seconds remaining.")]
    [SerializeField] private string timerFormat = "Time: {0:0}";
    [Tooltip("{0} = current score.")]
    [SerializeField] private string scoreFormat = "Score: {0}";
    [Tooltip("{0} = high score.")]
    [SerializeField] private string highScoreFormat = "Best: {0}";

    [Title("End Game")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text finalTitleText;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text finalHighScoreText;
    [SerializeField] private Button restartButton;

    [Tooltip("{0} = minigame title.")]
    [SerializeField] private string finalTitleFormat = "{0}";
    [Tooltip("{0} = final score.")]
    [SerializeField] private string finalScoreFormat = "Score: {0}";
    [Tooltip("{0} = high score.")]
    [SerializeField] private string finalHighScoreFormat = "Best: {0}";

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

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartScene);

        if (endGamePanel != null)
            endGamePanel.SetActive(false);
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

        // Mirror the active minigame's live state into the inspector + HUD.
        if (active != null)
        {
            activeTimeRemaining = active.CountdownTimeRemaining;
            activeScore = active.Score;
            score = active.Score;

            UpdateHud(active);

            if (!active.IsPlaying)
            {
                ShowEndGame(active);
                activeMinigame = "None";
                active = null;
            }
        }
    }

    // --- UI ----------------------------------------------------------------
    private void UpdateHud(Minigame_Base minigame)
    {
        if (titleText != null)
            titleText.text = minigame.Title;
        if (timerText != null)
            timerText.text = string.Format(timerFormat, minigame.CountdownTimeRemaining);
        if (scoreText != null)
            scoreText.text = string.Format(scoreFormat, minigame.Score);
        if (highScoreText != null)
            highScoreText.text = string.Format(highScoreFormat, minigame.HighScore);
    }

    private void ShowEndGame(Minigame_Base minigame)
    {
        if (finalTitleText != null)
            finalTitleText.text = string.Format(finalTitleFormat, minigame.Title);
        if (finalScoreText != null)
            finalScoreText.text = string.Format(finalScoreFormat, minigame.Score);
        if (finalHighScoreText != null)
            finalHighScoreText.text = string.Format(finalHighScoreFormat, minigame.HighScore);
        if (endGamePanel != null)
            endGamePanel.SetActive(true);
    }

    /// <summary>Reloads the current scene (wired to the end-game restart button).</summary>
    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

    // --- Reset -------------------------------------------------------------
    [Button("Reset Minigames")]
    public void ResetMinigames()
    {
        // Stop everything and clear the timers / score display.
        foreach (Minigame_Base m in minigames)
            if (m != null && m.IsPlaying)
                m.EndMinigame();

        StopCountdown();
        active = null;
        activeMinigame = "None";
        activeTimeRemaining = 0f;
        activeScore = 0;
        score = 0;

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        // Teeth stay hidden until a brush/floss game starts; still reset their plaque.
        if (Teeth.Instance != null)
        {
            Teeth.Instance.SetVisible(false);
            Teeth.Instance.ResetAllPlaque();
        }
    }

    [Button("Reset All High Scores")]
    private void ResetAllHighScores()
    {
        foreach (Minigame_Base m in minigames)
            if (m != null)
                m.ResetHighScore();
    }

    /// <summary>Starts the given minigame, stopping any one already running.</summary>
    public void StartMinigame(Minigame_Base minigame)
    {
        if (minigame == null)
            return;

        if (active != null && active.IsPlaying)
            active.EndMinigame();

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        active = minigame;
        activeMinigame = minigame.MinigameId;
        minigame.StartMinigame(); // base handles teeth visibility per minigame
    }
}
