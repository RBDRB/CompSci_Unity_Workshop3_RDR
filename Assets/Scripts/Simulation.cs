using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private SheepBehaviour sheepPrefab;
    [SerializeField] private WolfBehaviour wolfPrefab;

    [Header("Population initiale")]
    [SerializeField] private int initialSheep =10;
    [SerializeField] private int initialWolves = 2;
    [SerializeField] private float fieldHalfSize = 20f;

    [Header("Groupes au depart")]
    [SerializeField] private float groupSpawnRadius = 5f;          // taille du troupeau / de la meute
    [SerializeField] private float minDistanceBetweenGroups = 25f; // ecart minimal troupeau <-> meute

    void Start()
    {
        float margin = fieldHalfSize - groupSpawnRadius;           // le groupe entier reste dans le terrain

        Vector3 herdCenter = RandomPosition(margin);
        Vector3 packCenter = FarPositionFrom(herdCenter, margin);

        SpawnGroup(sheepPrefab, initialSheep, herdCenter);
        SpawnGroup(wolfPrefab, initialWolves, packCenter);
    }

    // Cree "count" copies du prefab dans un cercle autour de "center"
    private void SpawnGroup(Component prefab, int count, Vector3 center)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * groupSpawnRadius;
            Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
            Instantiate(prefab, position, Quaternion.identity, transform);
        }
    }

    // Tire des positions au hasard jusqu'a en trouver une assez loin de "from"
    private Vector3 FarPositionFrom(Vector3 from, float margin)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            Vector3 candidate = RandomPosition(margin);
            if (Vector3.Distance(candidate, from) >= minDistanceBetweenGroups)
                return candidate;
        }
        // Secours : le coin oppose
        return new Vector3(-Mathf.Sign(from.x) * margin, 0f, -Mathf.Sign(from.z) * margin);
    }

    private Vector3 RandomPosition(float halfSize)
    {
        return new Vector3(Random.Range(-halfSize, halfSize), 0f, Random.Range(-halfSize, halfSize));
    }
}