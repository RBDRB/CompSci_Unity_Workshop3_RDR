using UnityEngine;

public class Grass : MonoBehaviour
{
    [SerializeField] private float energyValue = 30f;

    public bool IsEaten { get; private set; }   // lisible par les autres, modifiable seulement ici

    // Appelée par le mouton. Renvoie l'énergie gagnée.
    public float BeEaten()
    {
        if (IsEaten)
            return 0f;          // deux moutons sur la même touffe : un seul en profite

        IsEaten = true;
        Destroy(gameObject);
        return energyValue;
    }
}