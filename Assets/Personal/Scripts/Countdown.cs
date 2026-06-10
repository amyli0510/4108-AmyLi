using System;
using EditorAttributes;
using UnityEngine;

/// <summary>
/// A simple, reusable countdown timer. It is a plain serializable class so it can be
/// dropped onto any MonoBehaviour by composition (the manager and every minigame have one).
/// Call <see cref="Begin"/> to start, <see cref="Tick"/> every frame, and listen to
/// <see cref="Finished"/> for completion.
/// </summary>
[Serializable]
public class Countdown
{
    [Tooltip("How long the countdown runs, in seconds.")]
    [Suffix("seconds")]
    [SerializeField] private float duration = 30f;

    [ReadOnly, SerializeField] private float timeRemaining;
    [ReadOnly, SerializeField] private bool isRunning;

    /// <summary>Duration in seconds. Settable so the manager can configure it at runtime.</summary>
    public float Duration
    {
        get => duration;
        set => duration = Mathf.Max(0f, value);
    }

    public float TimeRemaining => timeRemaining;
    public bool IsRunning => isRunning;

    /// <summary>0 at the start, 1 when finished.</summary>
    public float Progress => duration > 0f ? Mathf.Clamp01(1f - timeRemaining / duration) : 1f;

    /// <summary>Raised once when the timer reaches zero (or is begun with a zero duration).</summary>
    public event Action Finished;

    public void Begin()
    {
        timeRemaining = duration;
        isRunning = duration > 0f;

        if (!isRunning)
            Finished?.Invoke();
    }

    public void Stop()
    {
        isRunning = false;
    }

    public void Tick(float deltaTime)
    {
        if (!isRunning)
            return;

        timeRemaining -= deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isRunning = false;
            Finished?.Invoke();
        }
    }
}
