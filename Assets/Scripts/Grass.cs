using UnityEngine;

public class Grass : MonoBehaviour
{
    [SerializeField] private float energyValue = 30f;

    public bool IsEaten { get; private set; }   // lisible par les autres, modifiable seulement ici

    private SheepBehaviour reservedBy;           // le mouton qui vise cette touffe

    // Libre pour ce mouton : pas mangee, et personne d'autre ne la vise
    public bool IsAvailableFor(SheepBehaviour sheep) =>
        !IsEaten && (reservedBy == null || reservedBy == sheep);

    public bool TryReserve(SheepBehaviour sheep)
    {
        if (!IsAvailableFor(sheep))
            return false;

        reservedBy = sheep;
        return true;
    }

    public void Release(SheepBehaviour sheep)
    {
        if (reservedBy == sheep)
            reservedBy = null;
    }

    // Appelee par le mouton. Renvoie l'energie gagnee.
    public float BeEaten()
    {
        if (IsEaten)
            return 0f;

        IsEaten = true;
        Destroy(gameObject);
        return energyValue;
    }
}