using UnityEngine;

public class RatSpawner : MonoBehaviour
{
    [Header("Rat Prefabs")]
    [Tooltip("Drag your 3 different rat prefabs into this list")]
    public GameObject[] ratPrefabs;

    void Start()
    {
        SpawnRandomRat();
    }

    void SpawnRandomRat()
    {
        if (ratPrefabs == null || ratPrefabs.Length == 0)
        {
            Debug.LogWarning("RatSpawner: No rat prefabs assigned to the spawner list!", gameObject);
            return;
        }

        // Pick a completely random index number from 0 to the length of your array
        int randomIndex = Random.Range(0, ratPrefabs.Length);

        // Instantiate the selected rat directly at this spawner's coordinates
        if (ratPrefabs[randomIndex] != null)
        {
            Instantiate(ratPrefabs[randomIndex], transform.position, Quaternion.identity);
        }
    }
}
