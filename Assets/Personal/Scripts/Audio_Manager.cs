using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple audio manager: a named list of clips played through one assigned AudioSource.
/// Singleton, so anything can call <c>Audio_Manager.Instance.Play("name")</c>.
/// </summary>
public class Audio_Manager : MonoBehaviour
{
    public static Audio_Manager Instance { get; private set; }

    [Serializable]
    public class NamedClip
    {
        public string name;
        public AudioClip clip;

        [Tooltip("Random pitch range applied on each play (1,1 = no variation).")]
        [MinMaxSlider(0.1f, 3f)]
        public Vector2 pitchRange = Vector2.one;
    }

    [Title("Audio")]
    [Tooltip("AudioSource the one-shot clips are played through.")]
    [Required]
    [SerializeField] private AudioSource source;

    [Tooltip("Separate source for looping sounds (e.g. the brushing loop). Auto-created if empty.")]
    [SerializeField] private AudioSource loopSource;

    [SerializeField] private List<NamedClip> clips = new();

    [Title("Buttons")]
    [Tooltip("Auto-plays the button sound on every Button in the scene (active + inactive) when clicked.")]
    [SerializeField] private bool autoWireButtons = true;
    [Tooltip("Clip name played on any button click.")]
    [SerializeField] private string buttonSound = "Button";

    private readonly Dictionary<string, NamedClip> lookup = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[Audio_Manager] A second instance exists on '{name}'. Disabling this duplicate.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
        }
        loopSource.loop = true;

        lookup.Clear();
        foreach (NamedClip c in clips)
            if (c != null && c.clip != null && !string.IsNullOrEmpty(c.name))
                lookup[c.name] = c;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (!autoWireButtons)
            return;

        // Wire every button in the scene, including inactive ones, to play the click sound.
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
            button.onClick.AddListener(PlayButtonSound);
    }

    private void PlayButtonSound() => Play(buttonSound);

    /// <summary>Plays the named clip once through the assigned AudioSource.</summary>
    [Button("Play (test)")]
    public void Play(string clipName)
    {
        if (source == null)
            return;

        if (lookup.TryGetValue(clipName, out NamedClip entry) && entry.clip != null)
        {
            source.pitch = UnityEngine.Random.Range(entry.pitchRange.x, entry.pitchRange.y);
            source.PlayOneShot(entry.clip);
        }
        else
        {
            Debug.LogWarning($"[Audio_Manager] No clip named '{clipName}'.", this);
        }
    }

    public void Stop()
    {
        if (source != null)
            source.Stop();
    }

    // --- Looping sounds ----------------------------------------------------
    /// <summary>Starts looping the named clip on the loop source.</summary>
    public void PlayLoop(string clipName)
    {
        if (loopSource == null)
            return;

        if (lookup.TryGetValue(clipName, out NamedClip entry) && entry.clip != null)
        {
            loopSource.clip = entry.clip;
            loopSource.loop = true;
            loopSource.pitch = UnityEngine.Random.Range(entry.pitchRange.x, entry.pitchRange.y);
            loopSource.Play();
        }
        else
        {
            Debug.LogWarning($"[Audio_Manager] No clip named '{clipName}'.", this);
        }
    }

    /// <summary>Pauses or resumes the loop without losing its position.</summary>
    public void SetLoopPaused(bool paused)
    {
        if (loopSource == null)
            return;

        if (paused)
        {
            if (loopSource.isPlaying)
                loopSource.Pause();
        }
        else if (!loopSource.isPlaying)
        {
            loopSource.UnPause();
        }
    }

    public void StopLoop()
    {
        if (loopSource != null)
            loopSource.Stop();
    }
}
