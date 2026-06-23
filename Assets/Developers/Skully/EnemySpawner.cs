using NUnit.Framework.Interfaces;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject spawnedEnemy;
    public float timer;

    // Update is called once per frame
    void Start()
    {
        Invoke(nameof(Spawn), 0.5f);
    }


    private void SpawnTimer()
    {
        if (timer >= 3f)
        {
            Spawn();
            timer = 0f;
        }
    }

    private void Spawn()
    {
        GameObject spawningEnemy = Instantiate(spawnedEnemy, transform.position,Quaternion.identity);
    }
}
