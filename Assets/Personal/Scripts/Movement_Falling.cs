using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;
using UnityEngine.Pool;

public class Movement_Falling : MonoBehaviour
{
    [Title("Prefabs")]
    [HelpBox("Assign one or more prefabs. Each spawn picks one at random, and every prefab gets its own pool.", MessageMode.Log)]
    [SerializeField] private List<GameObject> prefabs = new();

    [Title("Spawn Area")]
    [Tooltip("Objects spawn across this transform's X span, at its Y position. Falls back to this object if empty.")]
    [SerializeField] private Transform spawnArea;
    [Suffix("units")]
    [SerializeField] private float spawnWidth = 10f;

    [Title("Spawning")]
    [Tooltip("Random delay between spawn bursts.")]
    [MinMaxSlider(0f, 5f)]
    [SerializeField] private Vector2 spawnIntervalRange = new(0.3f, 0.6f);

    [Tooltip("How many prefabs spawn per burst (random in this range), spread across the area.")]
    [MinMaxSlider(1, 20)]
    [SerializeField] private Vector2Int spawnCountRange = new(1, 3);

    [Tooltip("Hard cap on how many fallers can be alive at once.")]
    [SerializeField] private int maxActive = 100;

    [Title("Fall Speed")]
    [Tooltip("On: each faller gets a random speed. Off: all use Fixed Speed.")]
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

    [Title("Pooling")]
    [Tooltip("Optional parent for spawned objects, to keep the hierarchy tidy.")]
    [SerializeField] private Transform container;
    [SerializeField] private int prewarmPerPrefab = 10;
    [SerializeField] private int maxPoolSizePerPrefab = 50;

    [Title("Runtime", drawLine: true)]
    [Line(GUIColor.Green)]
    [ReadOnly, SerializeField] private int activeCount;

    // One pool per prefab = multi-prefab pooling.
    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();

    // Active fallers, driven by a single Update loop for mobile efficiency.
    private readonly List<Faller> active = new();
    // Reused Faller records so steady-state spawning produces no GC.
    private readonly Stack<Faller> fallerCache = new();

    private float nextSpawnTime;

    private class Faller
    {
        public GameObject go;
        public Transform tr;
        public float speed;
        public float timeLeft;
        public ObjectPool<GameObject> pool;
    }

    void Awake()
    {
        // Fast enter-play-mode (domain/scene reload disabled) keeps non-serialized
        // fields between sessions, so explicitly reset everything here.
        DisposePools();
        active.Clear();
        fallerCache.Clear();
        nextSpawnTime = 0f;
        activeCount = 0;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null || pools.ContainsKey(prefab))
                continue;

            ObjectPool<GameObject> pool = CreatePool(prefab);
            pools[prefab] = pool;
            Prewarm(pool);
        }
    }

    void OnDisable()
    {
        // Returning to the pool on play-exit avoids leaking stale references
        // into the next session when scene reload is disabled.
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Faller f = active[i];
            if (f.go != null)
                f.pool.Release(f.go);
        }
        active.Clear();
        activeCount = 0;
    }

    void OnDestroy()
    {
        DisposePools();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // --- Spawn a burst on the interval ---
        if (Time.time >= nextSpawnTime && active.Count < maxActive)
        {
            SpawnBurst();
            nextSpawnTime = Time.time + Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        // --- Move + lifetime (back-to-front so we can swap-remove cheaply) ---
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Faller f = active[i];
            f.timeLeft -= dt;

            if (f.timeLeft <= 0f)
            {
                f.pool.Release(f.go);
                active[i] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);

                f.go = null;
                f.tr = null;
                f.pool = null;
                fallerCache.Push(f);
                continue;
            }

            Vector3 p = f.tr.position;
            p.y -= f.speed * dt;
            f.tr.position = p;
        }

        activeCount = active.Count;
    }

    private void SpawnBurst()
    {
        if (prefabs.Count == 0)
            return;

        // How many to spawn this burst, clamped by remaining capacity.
        int count = Random.Range(spawnCountRange.x, spawnCountRange.y + 1);
        count = Mathf.Min(count, maxActive - active.Count);
        if (count <= 0)
            return;

        Vector3 origin = spawnArea != null ? spawnArea.position : transform.position;
        float left = origin.x - spawnWidth * 0.5f;
        float segment = spawnWidth / count;

        // Split the band into 'count' segments and drop one in each, so a burst
        // lands in different parts of the area instead of stacking up.
        for (int i = 0; i < count; i++)
        {
            float x = left + segment * (i + Random.value);
            SpawnOne(new Vector3(x, origin.y, origin.z));
        }
    }

    private void SpawnOne(Vector3 position)
    {
        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        if (prefab == null || !pools.TryGetValue(prefab, out ObjectPool<GameObject> pool))
            return;

        GameObject go = pool.Get();
        Transform tr = go.transform;
        tr.position = position;

        Faller f = fallerCache.Count > 0 ? fallerCache.Pop() : new Faller();
        f.go = go;
        f.tr = tr;
        f.speed = randomSpeed ? Random.Range(speedRange.x, speedRange.y) : fixedSpeed;
        f.timeLeft = lifetime;
        f.pool = pool;
        active.Add(f);
    }

    private ObjectPool<GameObject> CreatePool(GameObject prefab)
    {
        return new ObjectPool<GameObject>(
            createFunc: () => Instantiate(prefab, container),
            actionOnGet: o => o.SetActive(true),
            actionOnRelease: o => o.SetActive(false),
            actionOnDestroy: o => Destroy(o),
            collectionCheck: false, // skip the double-release check for performance
            defaultCapacity: prewarmPerPrefab,
            maxSize: maxPoolSizePerPrefab);
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

    private void DisposePools()
    {
        foreach (ObjectPool<GameObject> pool in pools.Values)
            pool.Clear();
        pools.Clear();
    }

#if UNITY_EDITOR
    // Visualize the spawn band in the Scene view.
    void OnDrawGizmosSelected()
    {
        Vector3 origin = spawnArea != null ? spawnArea.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin + Vector3.left * spawnWidth * 0.5f, origin + Vector3.right * spawnWidth * 0.5f);
    }
#endif
}
