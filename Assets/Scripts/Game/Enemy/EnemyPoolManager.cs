using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local pools. Only explicitly returned enemies can be rented again.</summary>
public class EnemyPoolManager : MonoBehaviour
{
    [Serializable]
    public class PoolDefinition
    {
        public GameObject prefab;
        [Min(0)] public int prewarmCount = 3;
    }

    [SerializeField] private List<PoolDefinition> pools = new List<PoolDefinition>();
    private readonly Dictionary<GameObject, Queue<EnemyPoolItem>> available = new Dictionary<GameObject, Queue<EnemyPoolItem>>();
    private readonly HashSet<EnemyPoolItem> rented = new HashSet<EnemyPoolItem>();
    private Transform inactiveRoot;
    private bool shuttingDown;

    private void Awake()
    {
        EnsureInactiveRoot();
        foreach (var definition in pools) {
            if (definition != null && definition.prefab != null) {
                Prewarm(definition.prefab, definition.prewarmCount);
            }
        }
    }

    /// <summary>Ensures at least count idle instances; does not steal living enemies.</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || shuttingDown) { return; }
        var queue = GetQueue(prefab);
        for (var i = queue.Count; i < Mathf.Max(0, count); i++) {
            queue.Enqueue(CreateItem(prefab));
        }
    }

    public EnemyPoolItem Spawn(GameObject prefab, Vector3 position, Quaternion rotation, GameObject target = null)
    {
        if (prefab == null || shuttingDown || !isActiveAndEnabled) {
            Debug.LogWarning("EnemyPoolManager requires an active manager and a prefab.", this);
            return null;
        }

        var queue = GetQueue(prefab);
        EnemyPoolItem item = null;
        while (queue.Count > 0 && item == null) {
            item = queue.Dequeue(); // Skip instances destroyed externally.
        }
        if (item == null) { item = CreateItem(prefab); }
        rented.Add(item);
        try {
            if (item.SpawnAt(position, rotation, target)) { return item; }
        } catch (Exception exception) {
            Debug.LogException(exception, item);
        }
        Despawn(item); // Failed placement/initialization cannot leak a checked-out instance.
        return null;
    }

    public bool Despawn(EnemyPoolItem item)
    {
        if (item == null || shuttingDown || item.Owner != this || !rented.Remove(item)) {
            return false;
        }
        item.ReturnToPool();
        item.transform.SetParent(transform, false);
        GetQueue(item.Prefab).Enqueue(item);
        return true;
    }

    internal void ForgetDestroyed(EnemyPoolItem item)
    {
        rented.Remove(item);
    }

    private Queue<EnemyPoolItem> GetQueue(GameObject prefab)
    {
        if (!available.TryGetValue(prefab, out var queue)) {
            queue = new Queue<EnemyPoolItem>();
            available.Add(prefab, queue);
        }
        return queue;
    }

    private EnemyPoolItem CreateItem(GameObject prefab)
    {
        EnsureInactiveRoot();
        // This parent is inactive BEFORE Instantiate, preventing premature OnEnable/tree startup.
        var instance = Instantiate(prefab, inactiveRoot);
        instance.SetActive(false);
        var item = instance.GetComponent<EnemyPoolItem>();
        if (item == null) { item = instance.AddComponent<EnemyPoolItem>(); }
        item.Initialize(this, prefab);
        instance.transform.SetParent(transform, false);
        return item;
    }

    private void EnsureInactiveRoot()
    {
        if (inactiveRoot != null) { return; }
        var root = new GameObject("Enemy Pool Staging");
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        inactiveRoot = root.transform;
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        rented.Clear();
        available.Clear();
        // All pool instances remain children of this scene-local manager.
    }
}
