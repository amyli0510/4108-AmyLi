using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

/// <summary>
/// Central registry of every <see cref="Tooth"/> in the scene, so minigames don't each
/// need their own list of references. There is one per scene (a singleton); minigames
/// read <see cref="Teeth.Instance"/> automatically.
/// </summary>
public class Teeth : MonoBehaviour
{
    public static Teeth Instance { get; private set; }

    [HelpBox("All teeth in the scene. Auto-collected from children on play. Use the buttons to refresh in-editor.", MessageMode.Log)]
    [SerializeField] private List<Tooth> teeth = new();

    [Title("Visuals")]
    [Tooltip("Objects toggled on/off when the teeth should be shown/hidden (e.g. hidden during the Food minigame).")]
    [SerializeField] private List<GameObject> visuals = new();

    public IReadOnlyList<Tooth> All => teeth;
    public int Count => teeth.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[Teeth] A second registry exists on '{name}'. Disabling this duplicate — keep only one per scene.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (teeth.Count == 0)
            CollectFromChildren();

        // Hidden by default; only the brush/floss minigames show the teeth.
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Resets every tooth to a fresh random plaque subset.</summary>
    public void ResetAllPlaque()
    {
        for (int i = 0; i < teeth.Count; i++)
            if (teeth[i] != null)
                teeth[i].ResetPlaque();
    }

    /// <summary>Shows or hides the visual objects (the tooth logic stays active).</summary>
    public void SetVisible(bool visible)
    {
        for (int i = 0; i < visuals.Count; i++)
            if (visuals[i] != null)
                visuals[i].SetActive(visible);
    }

    [Button("Use Teeth As Visuals")]
    public void UseTeethAsVisuals()
    {
        visuals.Clear();
        for (int i = 0; i < teeth.Count; i++)
            if (teeth[i] != null)
                visuals.Add(teeth[i].gameObject);
    }

    [Button("Collect Teeth (from children)")]
    public void CollectFromChildren()
    {
        teeth.Clear();
        GetComponentsInChildren(true, teeth); // includes inactive children
    }

    [Button("Collect Teeth (whole scene)")]
    public void CollectFromScene()
    {
        teeth.Clear();
        teeth.AddRange(FindObjectsByType<Tooth>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }
}
