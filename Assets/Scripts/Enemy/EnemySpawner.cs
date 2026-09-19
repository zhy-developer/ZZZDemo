using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks;
using UnityEngine;
using ZZZ;

/// <summary>Inspector entry point for one spawn. Wave scheduling belongs to the caller.</summary>
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyPoolManager pool;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool spawnOnStart = true;

    public EnemyPoolItem LastSpawned { get; private set; }
    private int lastSpawnVersion;

    private void Awake()
    {
    }

    private void Start()
    {
        if (spawnOnStart) { Spawn(); }
    }

    public EnemyPoolItem Spawn()
    {
        GameObject currentPlayer = SwitchCharacter.Instance.CurrentPlayer;
        if (pool == null || enemyPrefab == null) {
            Debug.LogWarning("EnemySpawner needs a pool and an enemy prefab.", this);
            return null;
        }
        var point = spawnPoint != null ? spawnPoint : transform;
        LastSpawned = pool.Spawn(enemyPrefab, point.position, point.rotation, currentPlayer);
        lastSpawnVersion = LastSpawned != null ? LastSpawned.SpawnVersion : 0;
        return LastSpawned;
    }
    /// <summary>
    /// 陈静到此一游 2026.9.18  20：42
    /// </summary>
    [ContextMenu("Spawn Enemy (Play Mode)")]
    private void SpawnFromInspector()
    {
        if (Application.isPlaying) { Spawn(); }
    }

    [ContextMenu("Return Last Enemy (Play Mode)")]
    public void DespawnLast()
    {
        if (Application.isPlaying && LastSpawned != null) {
            if (LastSpawned.IsSpawned && LastSpawned.SpawnVersion == lastSpawnVersion) {
                LastSpawned.Despawn();
            }
            LastSpawned = null;
        }
    }
}
