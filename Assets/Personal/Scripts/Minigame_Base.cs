using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

/// <summary>
/// Base class every minigame inherits from. It owns a <see cref="Countdown"/> (composition)
/// and a score, runs the countdown each frame, and saves the per-minigame high score to
/// PlayerPrefs when the round ends. Concrete minigames only implement the gameplay hooks.
///
/// Only one component of each concrete minigame type is allowed per scene.
/// </summary>
public abstract class Minigame_Base : MonoBehaviour
{
    [Title("Info")]
    [Tooltip("Display name shown in the UI while this minigame runs.")]
    [SerializeField] private string title;

    [Title("Countdown")]
    [SerializeField] protected Countdown countdown = new();

    [Title("Score")]
    [ReadOnly, SerializeField] protected int score;

    // A live, read-only look at the saved high score for this minigame.
    [ShowInInspector] public int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

    public bool IsPlaying { get; private set; }
    public int Score => score;

    /// <summary>Stable id used for the PlayerPrefs key, e.g. "Minigame_Brush".</summary>
    public string MinigameId => GetType().Name;

    /// <summary>Display name for the UI; falls back to the id if left blank.</summary>
    public string Title => string.IsNullOrEmpty(title) ? MinigameId : title;

    public float CountdownTimeRemaining => countdown.TimeRemaining;
    public float CountdownProgress => countdown.Progress;

    /// <summary>Raised when this minigame starts / ends. The manager listens to these.</summary>
    public event Action<Minigame_Base> Started;
    public event Action<Minigame_Base> Ended;

    private const string HighScoreKeyPrefix = "Minigame_HighScore_";
    private string HighScoreKey => HighScoreKeyPrefix + MinigameId;

    // --- One-instance-per-type enforcement ---------------------------------
    // Keyed by concrete type so Brush, Floss, Food each allow exactly one.
    private static readonly Dictionary<Type, Minigame_Base> instances = new();

    // Reset static state before each play session (safe with Domain Reload disabled).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instances.Clear();

    protected virtual void Awake()
    {
        Type type = GetType();
        if (instances.TryGetValue(type, out Minigame_Base existing) && existing != null && existing != this)
        {
            Debug.LogError($"[{type.Name}] A second instance exists on '{name}'. Disabling this duplicate — keep only one per scene.", this);
            enabled = false;
            return;
        }

        instances[type] = this;
        countdown.Finished += EndMinigame;
    }

    protected virtual void OnDestroy()
    {
        Type type = GetType();
        if (instances.TryGetValue(type, out Minigame_Base existing) && existing == this)
            instances.Remove(type);

        countdown.Finished -= EndMinigame;
    }

    protected virtual void Update()
    {
        if (!IsPlaying)
            return;

        countdown.Tick(Time.deltaTime);

        // Tick may have ended the round; only run gameplay if still playing.
        if (IsPlaying)
            OnMinigameTick(Time.deltaTime);
    }

    // --- Public control (also exposed as inspector buttons) ----------------
    [Button("▶ Start Minigame")]
    public void StartMinigame()
    {
        if (!enabled)
            return;

        // Only one minigame runs at a time, however it was started.
        foreach (Minigame_Base other in instances.Values)
            if (other != null && other != this && other.IsPlaying)
                other.EndMinigame();

        // Teeth visuals: shown for most minigames, hidden for those that don't use them.
        if (Teeth.Instance != null)
            Teeth.Instance.SetVisible(ShowsTeeth);

        score = 0;
        IsPlaying = true;
        countdown.Begin();
        OnMinigameStarted();
        Started?.Invoke(this);
    }

    /// <summary>Whether the teeth visuals should be shown while this minigame runs.</summary>
    protected virtual bool ShowsTeeth => true;

    [Button("■ End Minigame")]
    public void EndMinigame()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;
        countdown.Stop();
        OnMinigameEnded();
        SaveHighScore();

        // Teeth are only shown while a brush/floss game runs — hide them when it ends.
        if (Teeth.Instance != null)
            Teeth.Instance.SetVisible(false);

        Ended?.Invoke(this);
    }

    [Button("Clear Saved High Score")]
    public void ResetHighScore()
    {
        PlayerPrefs.DeleteKey(HighScoreKey);
        PlayerPrefs.Save();
    }

    /// <summary>Concrete minigames call this to award (or deduct) points.</summary>
    protected void AddScore(int amount)
    {
        score = Mathf.Max(0, score + amount);
        SaveHighScore(); // auto-save whenever the score changes
    }

    private void SaveHighScore()
    {
        if (score > HighScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, score);
            PlayerPrefs.Save();
        }
    }

    // --- Hooks for each concrete minigame to implement ---------------------
    /// <summary>Called once when the round starts. Set up input, spawn objects, etc.</summary>
    protected abstract void OnMinigameStarted();

    /// <summary>Called every frame while the round is running. Award score here.</summary>
    protected virtual void OnMinigameTick(float deltaTime) { }

    /// <summary>Called once when the timer runs out or the round is stopped.</summary>
    protected abstract void OnMinigameEnded();
}
