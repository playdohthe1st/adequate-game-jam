using System.Collections.Generic;
using UnityEngine;

namespace AdequateEnough
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField] private EnemyData[] enemyPool;

        [Header("Limits")]
        [SerializeField] private int maxEnemies = 3;
        // Minimum seconds between individual spawns while below the max
        [SerializeField] private float spawnInterval = 5f;

        [Header("Sorting")]
        [SerializeField] private int baseSortingOrder = 0;

        private readonly List<Enemy> liveEnemies = new();
        private readonly HashSet<int> takenOrders = new();
        private PlayerController player;
        private float spawnTimer;

        private void Awake()
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        private void OnEnable()  => PlayerController.OnRespawnedAtOrigin += OnPlayerRespawnedAtOrigin;
        private void OnDisable() => PlayerController.OnRespawnedAtOrigin -= OnPlayerRespawnedAtOrigin;

        private void Start()
        {
            SpawnToMax();
            spawnTimer = spawnInterval;
        }

        private void Update()
        {
            int before = liveEnemies.Count;
            liveEnemies.RemoveAll(e => e == null);
            if (liveEnemies.Count < before)
            {
                RebuildTakenOrders();
                spawnTimer = spawnInterval;
            }

            if (liveEnemies.Count >= maxEnemies) return;
            if (IsSpawnPointVisible()) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnOne();
                spawnTimer = spawnInterval;
            }
        }

        private void OnPlayerRespawnedAtOrigin()
        {
            foreach (Enemy e in liveEnemies)
            {
                if (e != null) Destroy(e.gameObject);
            }
            liveEnemies.Clear();
            takenOrders.Clear();
            SpawnToMax();
        }

        private void SpawnToMax()
        {
            int needed = maxEnemies - liveEnemies.Count;
            for (int i = 0; i < needed; i++)
                SpawnOne();
        }

        private void SpawnOne()
        {
            if (enemyPool == null || enemyPool.Length == 0) return;

            EnemyData data = enemyPool[Random.Range(0, enemyPool.Length)];
            if (data == null || data.enemyPrefab == null) return;

            GameObject go = Instantiate(data.enemyPrefab, transform.position, Quaternion.identity);
            Enemy enemy = go.GetComponent<Enemy>();
            if (enemy == null) { Destroy(go); return; }

            int order = FindLowestFreeOrder();
            takenOrders.Add(order);
            enemy.SetSortingOrder(order);
            enemy.SetHome(transform.position);
            liveEnemies.Add(enemy);
        }

        private int FindLowestFreeOrder()
        {
            int order = baseSortingOrder;
            while (takenOrders.Contains(order)) order++;
            return order;
        }

        private void RebuildTakenOrders()
        {
            takenOrders.Clear();
            foreach (Enemy e in liveEnemies)
                takenOrders.Add(e.AssignedSortingOrder);
        }

        // True when the spawn point is within the camera's viewport, so enemies won't pop in while the player watches
        private bool IsSpawnPointVisible()
        {
            if (Camera.main == null) return false;
            Vector3 vp = Camera.main.WorldToViewportPoint(transform.position);
            return vp.z > 0f && vp.x is >= 0f and <= 1f && vp.y is >= 0f and <= 1f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
