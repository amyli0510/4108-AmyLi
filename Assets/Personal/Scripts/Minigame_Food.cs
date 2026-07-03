using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

/// <summary>
/// Food-catching minigame. Good and bad food fall from a spawn band. The player can click
/// and hold a food to drag it (it follows the cursor, and keeps falling once released).
/// When a food reaches the basket it scores: positive for good food, negative for bad food.
/// Spawning is pooled (one pool per prefab) for mobile efficiency.
/// </summary>
public class Minigame_Food : Minigame_Base
{
    [Title("Food")]
    [HelpBox("List the food sprites. A GameObject with a SpriteRenderer is created per sprite automatically. Good food scores positive at the basket; bad food scores negative.", MessageMode.Log)]
    [SerializeField] private List<Sprite> goodFood = new();
    [SerializeField] private List<Sprite> badFood = new();

    [Title("Food Appearance")]
    [Tooltip("Sorting order applied to the spawned food sprites.")]
    [SerializeField] private int foodSortingOrder = 0;

    [Tooltip("Uniform scale applied to spawned food.")]
    [SerializeField] private float foodScale = 1f;

    [Title("Basket")]
    [Tooltip("Food is caught when it comes within the radius of this transform. Disabled until the game starts.")]
    [Required]
    [SerializeField] private Transform basket;
    [Suffix("units")]
    [SerializeField] private float basketRadius = 1f;

    [Title("Spawn Area")]
    [Tooltip("Food spawns across this transform's X span, at its Y position. Falls back to this object if empty.")]
    [SerializeField] private Transform spawnArea;
    [Suffix("units")]
    [SerializeField] private float spawnWidth = 10f;

    [Title("Spawning")]
    [Tooltip("Random delay between spawn bursts.")]
    [MinMaxSlider(0f, 5f)]
    [SerializeField] private Vector2 spawnIntervalRange = new(0.3f, 0.6f);

    [Tooltip("How many food spawn per burst (random in this range), spread across the area.")]
    [MinMaxSlider(1, 20)]
    [SerializeField] private Vector2Int spawnCountRange = new(1, 3);

    [Tooltip("Hard cap on how many food are alive at once.")]
    [SerializeField] private int maxActive = 100;

    [Title("Fall Speed")]
    [Tooltip("On: each food gets a random speed. Off: all use Fixed Speed.")]
    [SerializeField] private bool randomSpeed = true;

    [ShowField(nameof(randomSpeed))]
    [MinMaxSlider(0f, 20f)]
    [SerializeField] private Vector2 speedRange = new(2f, 5f);

    [HideField(nameof(randomSpeed))]
    [Suffix("units/s")]
    [SerializeField] private float fixedSpeed = 4f;

    [Title("Lifetime")]
    [Suffix("seconds")]
    [SerializeField] private float lifetime = 5f;

    [Title("Dragging")]
    [Tooltip("Camera used to read the mouse position. Defaults to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("How quickly a held food eases to the cursor. 0 = snaps instantly.")]
    [Suffix("seconds")]
    [SerializeField] private float dragSmoothTime = 0.02f;

    [Title("Scoring")]
    [SerializeField] private int goodPoints = 1;
    [SerializeField] private int badPoints = 1;

    [Title("Audio")]
    [Tooltip("One-shot sound when good food is caught in the basket.")]
    [SerializeField] private string captureSound = "Food Capture";

    [Title("Pooling")]
    [Tooltip("Optional parent for spawned food, to keep the hierarchy tidy.")]
    [SerializeField] private Transform container;
    [SerializeField] private int prewarmPerPrefab = 10;
    [SerializeField] private int maxPoolSizePerPrefab = 50;

    [Title("Runtime", drawLine: true)]
    [Line(GUIColor.Green)]
    [ReadOnly, SerializeField] private int activeCount;

    // One pool per prefab = multi-prefab pooling.
    private readonly Dictionary<Sprite, ObjectPool<GameObject>> pools = new();
    private readonly List<Faller> active = new();
    private readonly Stack<Faller> fallerCache = new();

    private float nextSpawnTime;
    private Faller held;
    private Vector3 dragVelocity;

    private class Faller
    {
        public GameObject go;
        public Transform tr;
        public Renderer renderer;
        public float speed;
        public float timeLeft;
        public ObjectPool<GameObject> pool;
        public bool isGood;
    }

    // Teeth aren't used in the food minigame, so hide them while it runs.
    protected override bool ShowsTeeth => false;

    // --- Lifecycle ---------------------------------------------------------
    protected override void Awake()
    {
        base.Awake();

        // Fast enter-play-mode safety (non-serialized state can persist between sessions).
        DisposePools();
        active.Clear();
        fallerCache.Clear();
        nextSpawnTime = 0f;
        activeCount = 0;
        held = null;

        if (targetCamera == null)
            targetCamera = Camera.main;

        BuildPools(goodFood);
        BuildPools(badFood);

        SetBasketActive(false); // basket hidden until the round starts
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        DisposePools();
    }

    private void OnDisable()
    {
        ReleaseAllActive();
    }

    protected override void OnMinigameStarted()
    {
        ReleaseAllActive();
        nextSpawnTime = 0f;
        held = null;
        SetBasketActive(true);
    }

    protected override void OnMinigameEnded()
    {
        ReleaseAllActive();
        SetBasketActive(false);
    }

    protected override void OnMinigameTick(float deltaTime)
    {
        // Spawn a burst on the interval.
        if (Time.time >= nextSpawnTime && active.Count < maxActive)
        {
            SpawnBurst();
            nextSpawnTime = Time.time + Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        HandleDragInput();
        MoveAndCatch(deltaTime);

        activeCount = active.Count;
    }

    // --- Dragging ----------------------------------------------------------
    private void HandleDragInput()
    {
        if (Mouse.current == null || targetCamera == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame && held == null)
        {
            held = FindFoodAt(MouseWorld());
            dragVelocity = Vector3.zero;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            held = null; // resumes falling
    }

    private void MoveAndCatch(float dt)
    {
        bool haveMouse = targetCamera != null && Mouse.current != null;
        Vector2 mouseWorld = haveMouse ? MouseWorld() : default;

        bool haveBasket = basket != null;
        Vector3 basketPos = haveBasket ? basket.position : default;
        float catchSqr = basketRadius * basketRadius;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Faller f = active[i];
            bool isHeld = f == held;

            if (isHeld && haveMouse)
            {
                // Follow the cursor; held food doesn't fall or expire.
                Vector3 cur = f.tr.position;
                Vector3 target = new(mouseWorld.x, mouseWorld.y, cur.z);
                f.tr.position = dragSmoothTime > 0f
                    ? Vector3.SmoothDamp(cur, target, ref dragVelocity, dragSmoothTime)
                    : target;
            }
            else
            {
                // Fall and age.
                Vector3 p = f.tr.position;
                p.y -= f.speed * dt;
                f.tr.position = p;

                f.timeLeft -= dt;
                if (f.timeLeft <= 0f)
                {
                    ReleaseAt(i, f); // expired, no score
                    continue;
                }
            }

            // Caught by the basket? (works whether falling or being dragged in)
            if (haveBasket)
            {
                Vector3 fp = f.tr.position;
                float dx = fp.x - basketPos.x;
                float dy = fp.y - basketPos.y;
                if (dx * dx + dy * dy <= catchSqr)
                {
                    AddScore(f.isGood ? goodPoints : -badPoints);
                    if (f.isGood)
                        Audio_Manager.Instance?.Play(captureSound);
                    ReleaseAt(i, f);
                }
            }
        }
    }

    private Faller FindFoodAt(Vector2 point)
    {
        Faller best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < active.Count; i++)
        {
            Faller f = active[i];
            if (f.renderer == null)
                continue;

            Bounds b = f.renderer.bounds;
            if (point.x < b.min.x || point.x > b.max.x || point.y < b.min.y || point.y > b.max.y)
                continue;

            float sqr = ((Vector2)b.center - point).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = f;
            }
        }

        return best;
    }

    private Vector2 MouseWorld()
    {
        Vector3 screen = Mouse.current.position.ReadValue();
        screen.z = Mathf.Abs(targetCamera.transform.position.z);
        return targetCamera.ScreenToWorldPoint(screen);
    }

    // --- Spawning ----------------------------------------------------------
    private void SpawnBurst()
    {
        int totalPrefabs = goodFood.Count + badFood.Count;
        if (totalPrefabs == 0)
            return;

        int count = Random.Range(spawnCountRange.x, spawnCountRange.y + 1);
        count = Mathf.Min(count, maxActive - active.Count);
        if (count <= 0)
            return;

        Vector3 origin = spawnArea != null ? spawnArea.position : transform.position;
        float left = origin.x - spawnWidth * 0.5f;
        float segment = spawnWidth / count;

        // Split the band into segments so a burst lands in different parts of the area.
        for (int i = 0; i < count; i++)
        {
            float x = left + segment * (i + Random.value);
            SpawnOne(new Vector3(x, origin.y, origin.z));
        }
    }

    private void SpawnOne(Vector3 position)
    {
        Sprite sprite = PickSprite(out bool isGood);
        if (sprite == null || !pools.TryGetValue(sprite, out ObjectPool<GameObject> pool))
            return;

        GameObject go = pool.Get();
        Transform tr = go.transform;
        tr.position = position;

        Faller f = fallerCache.Count > 0 ? fallerCache.Pop() : new Faller();
        f.go = go;
        f.tr = tr;
        f.renderer = go.GetComponentInChildren<Renderer>();
        f.speed = randomSpeed ? Random.Range(speedRange.x, speedRange.y) : fixedSpeed;
        f.timeLeft = lifetime;
        f.pool = pool;
        f.isGood = isGood;
        active.Add(f);
    }

    private Sprite PickSprite(out bool isGood)
    {
        int total = goodFood.Count + badFood.Count;
        int r = Random.Range(0, total);
        if (r < goodFood.Count)
        {
            isGood = true;
            return goodFood[r];
        }

        isGood = false;
        return badFood[r - goodFood.Count];
    }

    // --- Pooling -----------------------------------------------------------
    private void BuildPools(List<Sprite> list)
    {
        foreach (Sprite sprite in list)
        {
            if (sprite == null || pools.ContainsKey(sprite))
                continue;

            ObjectPool<GameObject> pool = CreatePool(sprite);
            pools[sprite] = pool;
            Prewarm(pool);
        }
    }

    private ObjectPool<GameObject> CreatePool(Sprite sprite)
    {
        return new ObjectPool<GameObject>(
            createFunc: () => CreateFood(sprite),
            actionOnGet: o => o.SetActive(true),
            actionOnRelease: o => o.SetActive(false),
            actionOnDestroy: o => Destroy(o),
            collectionCheck: false,
            defaultCapacity: prewarmPerPrefab,
            maxSize: maxPoolSizePerPrefab);
    }

    // Builds a food GameObject with a SpriteRenderer for the given sprite.
    private GameObject CreateFood(Sprite sprite)
    {
        GameObject go = new(sprite != null ? sprite.name : "Food");

        if (container != null)
            go.transform.SetParent(container, false);
        go.transform.localScale = Vector3.one * foodScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = foodSortingOrder;

        return go;
    }

    private void Prewarm(ObjectPool<GameObject> pool)
    {
        int count = Mathf.Clamp(prewarmPerPrefab, 0, maxPoolSizePerPrefab);
        if (count == 0)
            return;

        var temp = new GameObject[count];
        for (int i = 0; i < count; i++)
            temp[i] = pool.Get();
        for (int i = 0; i < count; i++)
            pool.Release(temp[i]);
    }

    private void ReleaseAt(int index, Faller f)
    {
        f.pool.Release(f.go);

        active[index] = active[active.Count - 1];
        active.RemoveAt(active.Count - 1);

        if (held == f)
            held = null;

        f.go = null;
        f.tr = null;
        f.renderer = null;
        f.pool = null;
        fallerCache.Push(f);
    }

    private void ReleaseAllActive()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Faller f = active[i];
            if (f.go != null)
                f.pool.Release(f.go);

            f.go = null;
            f.tr = null;
            f.renderer = null;
            f.pool = null;
            fallerCache.Push(f);
        }

        active.Clear();
        held = null;
        activeCount = 0;
    }

    private void DisposePools()
    {
        foreach (ObjectPool<GameObject> pool in pools.Values)
            pool.Clear();
        pools.Clear();
    }

    private void SetBasketActive(bool value)
    {
        if (basket != null)
            basket.gameObject.SetActive(value);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = spawnArea != null ? spawnArea.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin + Vector3.left * spawnWidth * 0.5f, origin + Vector3.right * spawnWidth * 0.5f);

        if (basket != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(basket.position, basketRadius);
        }
    }
#endif
}
