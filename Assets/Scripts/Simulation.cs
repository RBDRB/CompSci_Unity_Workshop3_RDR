using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private SheepBehaviour sheepPrefab;
    [SerializeField] private WolfBehaviour wolfPrefab;

    [Header("Population initiale")]
    [SerializeField] private int initialSheep = 10;
    [SerializeField] private int initialWolves = 2;
    [SerializeField] private float fieldHalfSize = 20f;

    void Start()
    {
        Spawn(sheepPrefab, initialSheep);
        Spawn(wolfPrefab, initialWolves);
    }

    // Cree "count" copies du prefab a des positions aleatoires, rangees sous Simulation
    private void Spawn(Component prefab, int count)
    {
        for (int i = 0; i < count; i++)
            Instantiate(prefab, RandomPosition(), Quaternion.identity, transform);
    }

    private Vector3 RandomPosition()
    {
        return new Vector3(
            Random.Range(-fieldHalfSize, fieldHalfSize), 0f,
            Random.Range(-fieldHalfSize, fieldHalfSize));
    }
}