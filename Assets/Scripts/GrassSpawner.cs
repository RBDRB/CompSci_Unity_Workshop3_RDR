using UnityEngine;

public class GrassSpawner : MonoBehaviour
{
    [SerializeField] private Grass grassPrefab;            // case à remplir dans l'Inspector
    [SerializeField] private int initialCount = 60;
    [SerializeField] private int maxCount = 150;
    [SerializeField] private float regrowInterval = 1f;    // une nouvelle touffe par seconde
    [SerializeField] private float fieldHalfSize = 20f;

    private float nextGrowTime;

    void Start()
    {
        for (int i = 0; i < initialCount; i++)
            SpawnGrass();

        nextGrowTime = Time.time + regrowInterval;
    }

    void Update()
    {
        if (Time.time < nextGrowTime)
            return;

        nextGrowTime = Time.time + regrowInterval;

        if (transform.childCount < maxCount)   // les touffes sont nos enfants : facile à compter
            SpawnGrass();
    }

    private void SpawnGrass()
    {
        Vector3 position = new Vector3(
            Random.Range(-fieldHalfSize, fieldHalfSize), 0f,
            Random.Range(-fieldHalfSize, fieldHalfSize));
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Le dernier argument range la touffe sous cet objet dans la Hierarchy
        Instantiate(grassPrefab, position, rotation, transform);
    }
}