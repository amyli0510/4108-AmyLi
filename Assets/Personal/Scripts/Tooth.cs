using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A single tooth. Holds two sets of plaque (brush + floss), reveals a random subset
/// each round, and fades plaque out as it gets cleaned. Raises a UnityEvent when a set
/// is fully cleaned. Also flips its sprite sorting order to sit in front of the floss
/// when it is the flossing target.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Tooth : MonoBehaviour
{
    [Title("Plaque")]
    [Tooltip("Brush plaque scattered across the tooth (cleaned in the brushing minigame).")]
    public List<GameObject> brushPlaque = new();

    [Tooltip("Floss plaque at the tooth edges (cleaned in the flossing minigame).")]
    public List<GameObject> flossPlaque = new();

    [Tooltip("How many brush plaque to randomly show at the start of a round.")]
    [MinMaxSlider(0, 8)]
    [SerializeField] private Vector2Int brushVisibleRange = new(2, 4);

    [Tooltip("How many floss plaque to randomly show at the start of a round.")]
    [MinMaxSlider(0, 8)]
    [SerializeField] private Vector2Int flossVisibleRange = new(2, 4);

    [Title("Flossing")]
    [Tooltip("On = bottom tooth, floss downward → upward. Off = top tooth, floss upward → downward.")]
    public bool upright = true;

    [Tooltip("Sorting order used while this is the flossing target (sits above the floss).")]
    [SerializeField] private int foregroundSortingOrder = 10;

    [Tooltip("Plaque render this many orders above the tooth sprite, and follow its sorting layer/order.")]
    [SerializeField] private int plaqueSortingOffset = 1;

    [Title("Color")]
    [Tooltip("Tooth tint by dirtiness: evaluate at 0 = fully clean, 1 = fully dirty. Driven by remaining plaque.")]
    [SerializeField] private Gradient dirtinessGradient = DefaultDirtinessGradient();

    [Title("Events")]
    public UnityEvent OnReset;
    public UnityEvent OnBrushCleaned;
    public UnityEvent OnFlossCleaned;

    public bool BrushCleaned { get; private set; }
    public bool FlossCleaned { get; private set; }
    public Vector2 Center => mainRenderer != null ? (Vector2)mainRenderer.bounds.center : (Vector2)transform.position;

    private SpriteRenderer mainRenderer;
    private int defaultSortingOrder;

    private readonly Dictionary<GameObject, SpriteRenderer> plaqueRenderers = new();
    private readonly List<GameObject> shownBrush = new();
    private readonly List<GameObject> shownFloss = new();
    private readonly List<int> indexBuffer = new();

    private void Awake()
    {
        mainRenderer = GetComponent<SpriteRenderer>();
        defaultSortingOrder = mainRenderer.sortingOrder;

        CacheRenderers(brushPlaque);
        CacheRenderers(flossPlaque);

        ApplyPlaqueSorting();

        // Start with no plaque shown; a minigame reveals its own when it begins.
        HidePlaque(brushPlaque, shownBrush);
        HidePlaque(flossPlaque, shownFloss);
        BrushCleaned = true;
        FlossCleaned = true;
        ApplyDirtinessColor(0f); // clean
    }

    private static Gradient DefaultDirtinessGradient()
    {
        Gradient g = new();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),                    // clean
                new GradientColorKey(new Color(0.85f, 0.78f, 0.45f), 1f), // dirty (yellowed)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            });
        return g;
    }

    /// <summary>Average remaining opacity of the shown plaque: 1 = fully dirty, 0 = clean.</summary>
    private float ComputeDirtiness(List<GameObject> shown)
    {
        if (shown.Count == 0)
            return 0f;

        float total = 0f;
        int counted = 0;
        for (int i = 0; i < shown.Count; i++)
        {
            GameObject p = shown[i];
            if (p == null)
                continue;

            counted++;
            // Inactive plaque are fully cleaned (contribute 0).
            if (p.activeSelf && plaqueRenderers.TryGetValue(p, out SpriteRenderer sr) && sr != null)
                total += Mathf.Clamp01(sr.color.a);
        }

        return counted > 0 ? total / counted : 0f;
    }

    private void ApplyDirtinessColor(float dirtiness)
    {
        if (mainRenderer != null && dirtinessGradient != null)
            mainRenderer.color = dirtinessGradient.Evaluate(Mathf.Clamp01(dirtiness));
    }

    private void CacheRenderers(List<GameObject> list)
    {
        foreach (GameObject p in list)
        {
            if (p == null || plaqueRenderers.ContainsKey(p))
                continue;

            SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
            if (sr != null)
                plaqueRenderers[p] = sr;
        }
    }

    // --- Hover helpers (used by the flossing minigame) ---------------------
    public bool ContainsPoint(Vector2 worldPoint)
    {
        if (mainRenderer == null)
            return false;

        Bounds b = mainRenderer.bounds;
        return worldPoint.x >= b.min.x && worldPoint.x <= b.max.x
            && worldPoint.y >= b.min.y && worldPoint.y <= b.max.y;
    }

    /// <summary>Brings the tooth in front of the floss (target) or back to default.</summary>
    public void SetForeground(bool foreground)
    {
        if (mainRenderer == null)
            return;

        mainRenderer.sortingOrder = foreground ? foregroundSortingOrder : defaultSortingOrder;
        ApplyPlaqueSorting(); // keep plaque sitting just above the tooth sprite
    }

    /// <summary>Makes every plaque match the tooth's sorting layer and sit just above its order.</summary>
    private void ApplyPlaqueSorting()
    {
        if (mainRenderer == null)
            return;

        int layer = mainRenderer.sortingLayerID;
        int order = mainRenderer.sortingOrder + plaqueSortingOffset;

        foreach (SpriteRenderer sr in plaqueRenderers.Values)
        {
            if (sr == null)
                continue;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order;
        }
    }

    // --- Round setup -------------------------------------------------------
    /// <summary>Full reset: hides all plaque so the tooth starts clean. Raises OnReset.</summary>
    [Button("Reset Plaque (hide all)")]
    public void ResetPlaque()
    {
        HidePlaque(brushPlaque, shownBrush);
        HidePlaque(flossPlaque, shownFloss);
        BrushCleaned = true;
        FlossCleaned = true;
        SetForeground(false);
        ApplyDirtinessColor(0f); // clean
        OnReset?.Invoke();
    }

    /// <summary>Reveals a random brush subset (and hides floss plaque) for a brushing round.</summary>
    public void ResetBrushPlaque()
    {
        HidePlaque(flossPlaque, shownFloss); // only the active minigame's plaque shows
        FlossCleaned = true;

        ShowRandomSubset(brushPlaque, shownBrush, brushVisibleRange);
        BrushCleaned = shownBrush.Count == 0;
        ApplyDirtinessColor(ComputeDirtiness(shownBrush));

        // No plaque to brush → already clean. Fire the event (no score awarded).
        if (BrushCleaned)
            OnBrushCleaned?.Invoke();
    }

    /// <summary>Reveals a random floss subset (and hides brush plaque) for a flossing round.</summary>
    public void ResetFlossPlaque()
    {
        HidePlaque(brushPlaque, shownBrush); // only the active minigame's plaque shows
        BrushCleaned = true;

        ShowRandomSubset(flossPlaque, shownFloss, flossVisibleRange);
        FlossCleaned = shownFloss.Count == 0;
        ApplyDirtinessColor(ComputeDirtiness(shownFloss));

        // No plaque to floss → already clean. Fire the event (no score awarded).
        if (FlossCleaned)
            OnFlossCleaned?.Invoke();
    }

    private void HidePlaque(List<GameObject> all, List<GameObject> shown)
    {
        foreach (GameObject p in all)
        {
            if (p == null)
                continue;
            SetAlpha(p, 1f);
            p.SetActive(false);
        }
        shown.Clear();
    }

    private void ShowRandomSubset(List<GameObject> all, List<GameObject> shown, Vector2Int range)
    {
        shown.Clear();

        // Hide everything and restore full opacity first.
        foreach (GameObject p in all)
        {
            if (p == null)
                continue;
            SetAlpha(p, 1f);
            p.SetActive(false);
        }

        if (all.Count == 0)
            return;

        int count = Mathf.Clamp(Random.Range(range.x, range.y + 1), 0, all.Count);

        // Partial Fisher–Yates over the non-null indices, then show the picks.
        indexBuffer.Clear();
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null)
                indexBuffer.Add(i);

        for (int i = 0; i < count && i < indexBuffer.Count; i++)
        {
            int j = Random.Range(i, indexBuffer.Count);
            (indexBuffer[i], indexBuffer[j]) = (indexBuffer[j], indexBuffer[i]);

            GameObject p = all[indexBuffer[i]];
            p.SetActive(true);
            shown.Add(p);
        }
    }

    // --- Cleaning ----------------------------------------------------------
    /// <summary>
    /// Fades any shown brush plaque inside the radius. Returns true on the call that
    /// clears the last one (so the minigame can award score once).
    /// </summary>
    public bool FadeBrushPlaqueInRadius(Vector2 worldPoint, float radius, float amount)
    {
        if (BrushCleaned || amount <= 0f)
            return false;

        float sqrRadius = radius * radius;
        bool anyActive = false;

        for (int i = 0; i < shownBrush.Count; i++)
        {
            GameObject p = shownBrush[i];
            if (p == null || !p.activeSelf)
                continue;

            Vector2 plaquePos = p.transform.position;
            if ((plaquePos - worldPoint).sqrMagnitude <= sqrRadius)
                FadePlaque(p, amount);

            if (p.activeSelf)
                anyActive = true;
        }

        ApplyDirtinessColor(ComputeDirtiness(shownBrush));

        if (!anyActive)
        {
            BrushCleaned = true;
            OnBrushCleaned?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Fades all shown floss plaque on this tooth. Returns true on the call that clears
    /// the last one.
    /// </summary>
    public bool FadeFlossPlaque(float amount)
    {
        if (FlossCleaned || amount <= 0f)
            return false;

        bool anyActive = false;

        for (int i = 0; i < shownFloss.Count; i++)
        {
            GameObject p = shownFloss[i];
            if (p == null)
                continue;

            if (p.activeSelf)
            {
                FadePlaque(p, amount);
                if (p.activeSelf)
                    anyActive = true;
            }
        }

        ApplyDirtinessColor(ComputeDirtiness(shownFloss));

        if (!anyActive)
        {
            FlossCleaned = true;
            OnFlossCleaned?.Invoke();
            return true;
        }

        return false;
    }

    private void FadePlaque(GameObject p, float amount)
    {
        if (!plaqueRenderers.TryGetValue(p, out SpriteRenderer sr) || sr == null)
        {
            p.SetActive(false);
            return;
        }

        Color c = sr.color;
        c.a -= amount;
        if (c.a <= 0f)
        {
            c.a = 0f;
            sr.color = c;
            p.SetActive(false); // fully cleaned → turn the plaque off
        }
        else
        {
            sr.color = c;
        }
    }

    private void SetAlpha(GameObject p, float alpha)
    {
        if (plaqueRenderers.TryGetValue(p, out SpriteRenderer sr) && sr != null)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    // --- Editor convenience buttons ----------------------------------------
    [Button]
    public void HideAllPlaque()
    {
        foreach (GameObject plaque in brushPlaque)
            if (plaque != null) plaque.SetActive(false);
        foreach (GameObject plaque in flossPlaque)
            if (plaque != null) plaque.SetActive(false);
    }

    [Button]
    public void ShowAllBrushPlaque()
    {
        foreach (GameObject plaque in brushPlaque)
        {
            if (plaque == null) continue;
            SetAlpha(plaque, 1f);
            plaque.SetActive(true);
        }
    }

    [Button]
    public void ShowAllFlossPlaque()
    {
        foreach (GameObject plaque in flossPlaque)
        {
            if (plaque == null) continue;
            SetAlpha(plaque, 1f);
            plaque.SetActive(true);
        }
    }
}
