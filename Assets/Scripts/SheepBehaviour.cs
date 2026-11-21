using UnityEngine;

public class SheepBehaviour : MonoBehaviour
{
    private enum State { Wandering, SeekingFood, Eating }

    [Header("Déplacement")]
    [SerializeField] private float speed = 1.5f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float seekTurnSpeed = 270f;   // tourne plus vite quand il vise une herbe
    [SerializeField] private float minTurnDelay = 5f;
    [SerializeField] private float maxTurnDelay = 9f;
    [SerializeField] private float fieldHalfSize = 20f;

    [Header("Énergie")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energyLossPerSecond = 2f;
    [SerializeField] private float hungerThreshold = 60f;  // en dessous : il a faim

    [Header("Nourriture")]
    [SerializeField] private float visionRadius = 6f;
    [SerializeField] private float eatDistance = 1f;
    [SerializeField] private float eatDuration = 2f;

    [Header("Debug (lecture seule)")]
    [SerializeField] private State state = State.Wandering;
    [SerializeField] private float energy;

    private Quaternion targetRotation;
    private float nextTurnTime;
    private Grass targetGrass;
    private float eatEndTime;

    void Start()
    {
        energy = maxEnergy;
        PickRandomDirection();
        transform.rotation = targetRotation;
    }

    void Update()
    {
        UpdateEnergy();
        if (energy <= 0f)
        {
            Die();
            return;
        }

        UpdateState();   // 1. décider
        Act();           // 2. agir
    }

    // ================= Transitions =================
    private void UpdateState()
    {
        switch (state)
        {
            case State.Wandering:
                if (IsHungry())
                {
                    targetGrass = FindNearestGrass();
                    if (targetGrass != null)
                        state = State.SeekingFood;
                }
                break;

            case State.SeekingFood:
                if (targetGrass == null)                      // un autre mouton l'a mangée
                {
                    state = State.Wandering;
                }
                else if (FlatDistanceTo(targetGrass.transform.position) <= eatDistance)
                {
                    state = State.Eating;
                    eatEndTime = Time.time + eatDuration;
                }
                break;

            case State.Eating:
                if (Time.time >= eatEndTime)
                {
                    if (targetGrass != null)
                        GainEnergy(targetGrass.BeEaten());
                    targetGrass = null;
                    state = State.Wandering;
                }
                break;
        }
    }

    // ================= Actions =================
    private void Act()
    {
        switch (state)
        {
            case State.Wandering:
                Wander();
                break;

            case State.SeekingFood:
                LookAt(targetGrass.transform.position);
                RotateTowardsTarget(seekTurnSpeed);
                MoveForward();
                break;

            case State.Eating:
                // immobile pendant qu'il mange
                break;
        }
    }

    // ================= Énergie =================
    private bool IsHungry() => energy < hungerThreshold;

    private void UpdateEnergy()
    {
        energy -= energyLossPerSecond * Time.deltaTime;
    }

    public void GainEnergy(float amount)
    {
        energy = Mathf.Min(energy + amount, maxEnergy);
    }

    private void Die()
    {
        Debug.Log(name + " est mort de faim");
        Destroy(gameObject);
    }

    // ================= Perception =================
    private Grass FindNearestGrass()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, visionRadius);

        Grass nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            Grass grass = hit.GetComponent<Grass>();
            if (grass == null || grass.IsEaten)
                continue;                       // ce collider n'est pas de l'herbe (le sol, par ex.)

            float d = FlatDistanceTo(grass.transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                nearest = grass;
            }
        }
        return nearest;
    }

    private float FlatDistanceTo(Vector3 point)
    {
        Vector3 diff = point - transform.position;
        diff.y = 0f;                            // on ignore la hauteur
        return diff.magnitude;
    }

    // ================= Déplacement =================
    private void Wander()
    {
        if (Time.time >= nextTurnTime)
            PickRandomDirection();
        StayInField();
        RotateTowardsTarget(turnSpeed);
        MoveForward();
    }

    private void PickRandomDirection()
    {
        targetRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        nextTurnTime = Time.time + Random.Range(minTurnDelay, maxTurnDelay);
    }

    private void StayInField()
    {
        Vector3 p = transform.position;
        if (Mathf.Abs(p.x) > fieldHalfSize || Mathf.Abs(p.z) > fieldHalfSize)
            LookAt(Vector3.zero);
    }

    private void LookAt(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            targetRotation = Quaternion.LookRotation(direction);
    }

    private void RotateTowardsTarget(float degreesPerSecond)
    {
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, degreesPerSecond * Time.deltaTime);
    }

    private void MoveForward()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    // Dessine le champ de vision dans la vue Scene quand le mouton est sélectionné
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, visionRadius);
    }
}