using EditorAttributes;
using UnityEngine;

/// <summary>
/// Background music. Plays a looping general track when no minigame is active, and each
/// minigame's own looping track (with a per-minigame volume) while it runs. Singleton.
/// </summary>
public class Music_Manager : MonoBehaviour
{
    public static Music_Manager Instance { get; private set; }

    [Title("Music")]
    [Tooltip("Looping music source. Auto-created if empty.")]
    [SerializeField] private AudioSource source;

    [Tooltip("Music played when no minigame is active.")]
    [SerializeField] private AudioClip generalMusic;

    [Range(0f, 1f)]
    [SerializeField] private float generalVolume = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[Music_Manager] A second instance exists on '{name}'. Disabling this duplicate.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }
        source.loop = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        PlayGeneral();
    }

    /// <summary>Plays a looping clip at the given volume. A null clip stops the music.</summary>
    public void Play(AudioClip clip, float volume)
    {
        if (source == null)
            return;

        source.volume = volume;

        if (clip == null)
        {
            source.Stop();
            return;
        }

        if (source.clip == clip && source.isPlaying)
            return; // already playing this track (volume was still applied above)

        source.clip = clip;
        source.loop = true;
        source.Play();
    }

    /// <summary>Plays the given minigame's music, or the general track if it has none.</summary>
    public void PlayForMinigame(Minigame_Base minigame)
    {
        if (minigame != null && minigame.Music != null)
            Play(minigame.Music, minigame.MusicVolume);
        else
            PlayGeneral();
    }

    public void PlayGeneral() => Play(generalMusic, generalVolume);
}
