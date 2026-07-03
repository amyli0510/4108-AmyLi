using System;
using System.Collections.Generic;
using EditorAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
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

    [Serializable]
    public class IntroTimeline
    {
        public Minigame_Base minigame;
        public PlayableDirector timeline;
    }

    [Serializable]
    public class BackgroundColorEntry
    {
        public Minigame_Base minigame;

        [Tooltip("Camera background color for this minigame.")]
        public Color color = Color.white;

        [Tooltip("Color applied to the HUD texts (title, timer, score, high score) for this minigame.")]
        public Color uiTextColor = Color.white;
    }

    [Title("Minigames")]
    [HelpBox("Drop each Minigame_ component here, then start one with the buttons below.", MessageMode.Log)]
    [SerializeField] private List<Minigame_Base> minigames = new();

    [Tooltip("Optional intro timeline per minigame. When set, the timeline plays first and the game starts when it finishes.")]
    [SerializeField] private List<IntroTimeline> introTimelines = new();

    [Title("Session Countdown")]
    [Tooltip("An optional overall timer for a whole play session, independent of the minigames.")]
    [SerializeField] private Countdown countdown = new();

    [Title("Score")]
    [ReadOnly, SerializeField] private int score;

    [Title("UI")]
    [Tooltip("Root of the in-game HUD. Hidden when no minigame is running and the end screen isn't showing.")]
    [SerializeField] private GameObject gameUiRoot;
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

    [Tooltip("Star objects, shown/hidden based on the final score. Star 1 = lowest.")]
    [SerializeField] private GameObject star1;
    [SerializeField] private GameObject star2;
    [SerializeField] private GameObject star3;

    [Tooltip("Minimum final score needed to earn 1 / 2 / 3 stars.")]
    [SerializeField] private int oneStarScore = 1;
    [SerializeField] private int twoStarScore = 5;
    [SerializeField] private int threeStarScore = 10;

    [SerializeField] private TMP_Text feedbackText;
    [Tooltip("Feedback by star count: [0] = 0 stars (worst) … [3] = 3 stars (best).")]
    [SerializeField] private string[] feedbackByStars = { "Keep Trying!", "Nice Try!", "Good Job!", "Amazing!" };

    [Tooltip("{0} = minigame title.")]
    [SerializeField] private string finalTitleFormat = "{0}";
    [Tooltip("{0} = final score.")]
    [SerializeField] private string finalScoreFormat = "Score: {0}";
    [Tooltip("{0} = high score.")]
    [SerializeField] private string finalHighScoreFormat = "Best: {0}";

    [Tooltip("End SFX (Audio_Manager clip names): win plays with ≥1 star, lose otherwise.")]
    [SerializeField] private string winSound = "Win";
    [SerializeField] private string loseSound = "Loose";

    [Title("Background")]
    [Tooltip("Camera whose background color changes per minigame. Uses the default color when idle.")]
    [SerializeField] private Camera backgroundCamera;
    [SerializeField] private Color defaultBackground = new(0.4f, 0.7f, 1f, 1f);
    [Tooltip("HUD text color used when no minigame is active.")]
    [SerializeField] private Color defaultUiTextColor = Color.white;
    [Tooltip("Background + UI text color per minigame.")]
    [SerializeField] private List<BackgroundColorEntry> backgroundColors = new();

    [Title("Runtime (read-only)")]
    [ReadOnly, SerializeField] private string activeMinigame = "None";
    [ReadOnly, SerializeField] private float activeTimeRemaining;
    [ReadOnly, SerializeField] private int activeScore;

    private Minigame_Base active;

    // While an intro timeline plays, the game is "pending" (not yet active).
    private Minigame_Base pendingMinigame;
    private PlayableDirector pendingDirector;

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

        RefreshGameUi();
        ApplyColors(null); // default background + text color on the menu (music handled by Music_Manager)
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
                ApplyPresentation(null); // back to general music + default background
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

        ApplyStarsAndFeedback(minigame.Score);

        if (endGamePanel != null)
            endGamePanel.SetActive(true);

        RefreshGameUi();
    }

    private void ApplyStarsAndFeedback(int finalScore)
    {
        int stars = 0;
        if (finalScore >= threeStarScore) stars = 3;
        else if (finalScore >= twoStarScore) stars = 2;
        else if (finalScore >= oneStarScore) stars = 1;

        if (star1 != null) star1.SetActive(stars >= 1);
        if (star2 != null) star2.SetActive(stars >= 2);
        if (star3 != null) star3.SetActive(stars >= 3);

        if (feedbackText != null && feedbackByStars != null && feedbackByStars.Length > 0)
        {
            int index = Mathf.Clamp(stars, 0, feedbackByStars.Length - 1);
            feedbackText.text = feedbackByStars[index];
        }

        Audio_Manager.Instance?.Play(stars >= 1 ? winSound : loseSound);
    }

    // --- Presentation (music + camera background) --------------------------
    /// <summary>Applies the minigame's music + background color (null = idle: general music, default color).</summary>
    private void ApplyPresentation(Minigame_Base minigame)
    {
        ApplyColors(minigame);

        if (Music_Manager.Instance != null)
            Music_Manager.Instance.PlayForMinigame(minigame); // null → general music
    }

    /// <summary>Applies the camera background + HUD text color for a minigame (null = idle defaults).</summary>
    private void ApplyColors(Minigame_Base minigame)
    {
        Color background = defaultBackground;
        Color textColor = defaultUiTextColor;

        if (minigame != null)
        {
            for (int i = 0; i < backgroundColors.Count; i++)
            {
                BackgroundColorEntry entry = backgroundColors[i];
                if (entry != null && entry.minigame == minigame)
                {
                    background = entry.color;
                    textColor = entry.uiTextColor;
                    break;
                }
            }
        }

        if (backgroundCamera != null)
            backgroundCamera.backgroundColor = background;

        SetUiTextColor(textColor);
    }

    private void SetUiTextColor(Color color)
    {
        if (titleText != null) titleText.color = color;
        if (timerText != null) timerText.color = color;
        if (scoreText != null) scoreText.color = color;
        if (highScoreText != null) highScoreText.color = color;
    }

    /// <summary>Shows the HUD while a minigame is running or the end screen is up; hides it otherwise.</summary>
    private void RefreshGameUi()
    {
        if (gameUiRoot == null)
            return;

        bool endShowing = endGamePanel != null && endGamePanel.activeSelf;
        gameUiRoot.SetActive(active != null || endShowing);
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

    /// <summary>Stops the active minigame WITHOUT scoring and hides the game UI / end screen.</summary>
    public void AbortActive()
    {
        CancelPendingIntro();

        if (active != null)
            active.AbortMinigame();

        active = null;
        activeMinigame = "None";

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        RefreshGameUi();
        ApplyPresentation(null); // general music + default background
    }

    // --- Reset -------------------------------------------------------------
    [Button("Reset Minigames")]
    public void ResetMinigames()
    {
        // Stop everything and clear the timers / score display.
        CancelPendingIntro();
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

        RefreshGameUi();
        ApplyPresentation(null); // general music + default background

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

    /// <summary>
    /// Starts the given minigame, stopping any one already running. If the minigame has an
    /// intro timeline, it plays first and the game begins when the timeline finishes.
    /// </summary>
    public void StartMinigame(Minigame_Base minigame)
    {
        if (minigame == null)
            return;

        CancelPendingIntro();

        if (active != null && active.IsPlaying)
            active.EndMinigame();
        active = null; // stays null during the intro so the end-game check doesn't fire

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        PlayableDirector intro = GetIntro(minigame);
        if (intro != null)
        {
            pendingMinigame = minigame;
            pendingDirector = intro;
            intro.stopped += OnIntroFinished;
            RefreshGameUi();
            intro.Play();
            return;
        }

        BeginMinigame(minigame);
    }

    private void BeginMinigame(Minigame_Base minigame)
    {
        active = minigame;
        activeMinigame = minigame.MinigameId;
        RefreshGameUi();
        ApplyPresentation(minigame); // this minigame's music + background color
        minigame.StartMinigame(); // base handles teeth visibility per minigame
    }

    private void OnIntroFinished(PlayableDirector director)
    {
        director.stopped -= OnIntroFinished;

        Minigame_Base minigame = pendingMinigame;
        pendingMinigame = null;
        pendingDirector = null;

        if (minigame != null)
            BeginMinigame(minigame);
    }

    private void CancelPendingIntro()
    {
        if (pendingDirector != null)
        {
            pendingDirector.stopped -= OnIntroFinished;
            pendingDirector.Stop();
            pendingDirector = null;
        }

        pendingMinigame = null;
    }

    private PlayableDirector GetIntro(Minigame_Base minigame)
    {
        for (int i = 0; i < introTimelines.Count; i++)
        {
            IntroTimeline entry = introTimelines[i];
            if (entry != null && entry.minigame == minigame && entry.timeline != null)
                return entry.timeline;
        }

        return null;
    }
}
