using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private SheepBehaviour sheepPrefab;
    [SerializeField] private WolfBehaviour wolfPrefab;

    [Header("Population initiale")]
    [SerializeField] private int initialSheep = 20;
    [SerializeField] private int initialWolves = 3;
    [SerializeField] private float fieldHalfSize = 20f;

    [Header("Groupes au depart")]
    [SerializeField] private float groupSpawnRadius = 5f;          // taille du troupeau / de la meute
    [SerializeField] private float minDistanceBetweenGroups = 25f; // ecart minimal troupeau <-> meute

    [Header("Limites de population")]
    [SerializeField] private int maxSheep = 80;                    // au-dela, plus de naissance
    [SerializeField] private int maxWolves = 15;

    [Header("Graphe de population (F2)")]
    [SerializeField] private PopulationPlot plot;                  // composant pose sur la Main Camera
    [SerializeField] private float recordInterval = 1f;            // une mesure par seconde

    private float nextRecordTime;

    void Start()
    {
        float margin = fieldHalfSize - groupSpawnRadius;           // le groupe entier reste dans le terrain

        Vector3 herdCenter = RandomPosition(margin);
        Vector3 packCenter = FarPositionFrom(herdCenter, margin);

        SpawnGroup(sheepPrefab, initialSheep, herdCenter);
        SpawnGroup(wolfPrefab, initialWolves, packCenter);

        if (plot == null)
            Debug.LogWarning("Simulation : aucun PopulationPlot relie, le graphe F2 restera vide");
    }

    void Update()
    {
        if (plot == null || Time.time < nextRecordTime)
            return;

        nextRecordTime = Time.time + recordInterval;
        plot.RecordPopulation(CountLiveSheep(), CountLiveWolves(), CountGrass());
    }

    // ================= Comptage =================
    // Les carcasses et les loups en train de mourir ne comptent pas
    private int CountLiveSheep()
    {
        int count = 0;
        foreach (SheepBehaviour sheep in FindObjectsByType<SheepBehaviour>(FindObjectsSortMode.None))
        {
            if (!sheep.IsDead)
                count++;
        }
        return count;
    }

    private int CountLiveWolves()
    {
        int count = 0;
        foreach (WolfBehaviour wolf in FindObjectsByType<WolfBehaviour>(FindObjectsSortMode.None))
        {
            if (!wolf.IsDead)
                count++;
        }
        return count;
    }

    private int CountGrass()
    {
        return FindObjectsByType<Grass>(FindObjectsSortMode.None).Length;
    }

    // ================= Naissances (appelees par les parents) =================
    public void SpawnLamb(Vector3 position)
    {
        if (CountLiveSheep() >= maxSheep)
            return;

        SheepBehaviour lamb = Instantiate(sheepPrefab, position, Quaternion.identity, transform);
        lamb.BeBorn();
    }

    public void SpawnCub(Vector3 position)
    {
        if (CountLiveWolves() >= maxWolves)
            return;

        WolfBehaviour cub = Instantiate(wolfPrefab, position, Quaternion.identity, transform);
        cub.BeBorn();
    }

    // ================= Population de depart =================
    // Cree "count" adultes dans un cercle autour de "center"
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