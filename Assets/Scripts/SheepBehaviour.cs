using System.Collections.Generic;
using UnityEngine;

public class SheepBehaviour : MonoBehaviour
{
    private enum State { Wandering, SeekingFood, Eating, Fleeing }

    [Header("Deplacement")]
    [SerializeField] private float speed = 1.5f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float seekTurnSpeed = 270f;     // tourne plus vite quand il vise une herbe
    [SerializeField] private float minTurnDelay = 5f;
    [SerializeField] private float maxTurnDelay = 9f;
    [SerializeField] private float maxWanderAngle = 60f;     // ecart maximal du nouveau cap
    [SerializeField, Range(0f, 1f)] private float sharpTurnSpeedFactor = 0.2f; // vitesse gardee pendant un demi-tour
    [SerializeField] private float fieldHalfSize = 20f;

    [Header("Troupeau (Boids)")]
    [SerializeField] private float herdRadius = 8f;          // distance a laquelle il voit les autres moutons
    [SerializeField] private float personalSpace = 1.5f;     // en dessous : il s'ecarte
    [SerializeField] private float cohesionWeight = 1.5f;    // envie de rejoindre le centre du troupeau
    [SerializeField] private float separationWeight = 2f;    // envie de ne pas se coller
    [SerializeField] private float alignmentWeight = 0.5f;   // envie d'aller dans le meme sens que les voisins

    [Header("Fuite")]
    [SerializeField] private float wolfDetectionRadius = 8f;
    [SerializeField] private float fleeSpeed = 3.5f;
    [SerializeField] private float fleeTurnSpeed = 360f;

    [Header("Energie")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energyLossPerSecond = 2f;
    [SerializeField] private float hungerThreshold = 60f;    // en dessous : il a faim

    [Header("Nourriture")]
    [SerializeField] private float visionRadius = 6f;
    [SerializeField] private float eatDistance = 1f;
    [SerializeField] private float eatDuration = 2f;

    [Header("Animation")]
    [SerializeField] private Animator animator;              // l'Animator du modele (enfant)

    [Header("Debug (lecture seule)")]
    [SerializeField] private State state = State.Wandering;
    [SerializeField] private float energy;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    // Remplies a chaque image par ScanSurroundings()
    private readonly List<SheepBehaviour> neighbours = new List<SheepBehaviour>();
    private readonly List<WolfBehaviour> visibleWolves = new List<WolfBehaviour>();

    private float wanderHeading;                             // cap souhaite en flanant, en degres
    private float nextTurnTime;
    private Quaternion targetRotation;
    private Grass targetGrass;
    private float eatEndTime;

    void Start()
    {
        energy = maxEnergy;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);  // cap de depart au hasard
        StartWandering();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning(name + " : aucun Animator trouve dans les enfants");
    }

    void Update()
    {
        UpdateEnergy();
        if (energy <= 0f)
        {
            Die();
            return;
        }

        ScanSurroundings(); // 1. percevoir
        UpdateState();      // 2. decider
        Act();              // 3. agir
        UpdateAnimation();  // 4. montrer
    }

    // ================= Perception =================
    // Un seul balayage pour trouver les voisins du troupeau et les loups proches
    private void ScanSurroundings()
    {
        neighbours.Clear();
        visibleWolves.Clear();

        float radius = Mathf.Max(herdRadius, wolfDetectionRadius);
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in hits)
        {
            float d = FlatDistanceTo(hit.transform.position);

            SheepBehaviour sheep = hit.GetComponent<SheepBehaviour>();
            if (sheep != null)
            {
                if (sheep != this && d <= herdRadius)
                    neighbours.Add(sheep);
                continue;
            }

            WolfBehaviour wolf = hit.GetComponent<WolfBehaviour>();
            if (wolf != null && d <= wolfDetectionRadius)
                visibleWolves.Add(wolf);
        }
    }

    private Grass FindNearestGrass()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, visionRadius);

        Grass nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            Grass grass = hit.GetComponent<Grass>();
            if (grass == null || grass.IsEaten)
                continue;                       // ce collider n'est pas de l'herbe

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

    // ================= Transitions =================
    private void UpdateState()
    {
        // Priorite absolue : un loup en vue -> fuir, quoi qu'il fasse
        if (visibleWolves.Count > 0)
        {
            targetGrass = null;
            state = State.Fleeing;
            return;
        }

        switch (state)
        {
            case State.Fleeing:
                StartWandering();               // plus aucun loup en vue
                break;

            case State.Wandering:
                if (IsHungry())
                {
                    targetGrass = FindNearestGrass();
                    if (targetGrass != null)
                        state = State.SeekingFood;
                }
                break;

            case State.SeekingFood:
                if (targetGrass == null)        // un autre mouton l'a mangee
                {
                    StartWandering();
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
                    StartWandering();
                }
                break;
        }
    }

    private void StartWandering()
    {
        state = State.Wandering;
        wanderHeading = transform.eulerAngles.y;    // repart dans la direction ou il regarde
        nextTurnTime = Time.time + Random.Range(minTurnDelay, maxTurnDelay);
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
                SteerTowards(targetGrass.transform.position - transform.position, seekTurnSpeed);
                MoveForward(speed);
                break;

            case State.Eating:
                // immobile pendant qu'il mange
                break;

            case State.Fleeing:
                Flee();
                break;
        }
    }

    private void Wander()
    {
        if (Time.time >= nextTurnTime)
        {
            wanderHeading += Random.Range(-maxWanderAngle, maxWanderAngle);
            nextTurnTime = Time.time + Random.Range(minTurnDelay, maxTurnDelay);
        }

        Vector3 wanderDirection = Quaternion.Euler(0f, wanderHeading, 0f) * Vector3.forward;
        SteerTowards(wanderDirection + HerdDirection(), turnSpeed);
        MoveForward(speed);
    }

    // Les 3 regles des Boids : cohesion, separation, alignement
    private Vector3 HerdDirection()
    {
        if (neighbours.Count == 0)
            return Vector3.zero;

        Vector3 center = Vector3.zero;
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;

        foreach (SheepBehaviour other in neighbours)
        {
            center += other.transform.position;
            alignment += other.transform.forward;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f && d < personalSpace)
                separation += away / d * (1f - d / personalSpace);  // plus il est pres, plus on s'ecarte
        }

        center /= neighbours.Count;
        Vector3 toCenter = center - transform.position;
        toCenter.y = 0f;
        Vector3 cohesion = toCenter / herdRadius;   // 0 au centre, environ 1 au bord du troupeau

        alignment.y = 0f;
        alignment = alignment.normalized;

        return cohesion * cohesionWeight
             + separation * separationWeight
             + alignment * alignmentWeight;
    }

    // S'eloigne des loups ; un loup proche compte beaucoup plus qu'un loup lointain
    private void Flee()
    {
        Vector3 away = Vector3.zero;

        foreach (WolfBehaviour wolf in visibleWolves)
        {
            Vector3 diff = transform.position - wolf.transform.position;
            diff.y = 0f;
            float d = Mathf.Max(diff.magnitude, 0.1f);
            away += diff / (d * d);
        }

        SteerTowards(away, fleeTurnSpeed);
        MoveForward(fleeSpeed);
    }

    // ================= Animation =================
    private void UpdateAnimation()
    {
        if (animator == null)
            return;

        bool isMoving = state != State.Eating;
        animator.SetBool(IsMovingHash, isMoving);
    }

    // ================= Energie =================
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

    // ================= Mouvement de base =================
    // Tourne progressivement vers une direction souhaitee
    private void SteerTowards(Vector3 desiredDirection, float degreesPerSecond)
    {
        desiredDirection = KeepInsideField(desiredDirection);
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude > 0.001f)
            targetRotation = Quaternion.LookRotation(desiredDirection);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, degreesPerSecond * Time.deltaTime);
    }

    // Hors du terrain : la seule direction souhaitee devient le centre
    private Vector3 KeepInsideField(Vector3 desiredDirection)
    {
        Vector3 p = transform.position;
        if (Mathf.Abs(p.x) <= fieldHalfSize && Mathf.Abs(p.z) <= fieldHalfSize)
            return desiredDirection;

        Vector3 toCenter = new Vector3(-p.x, 0f, -p.z);
        wanderHeading = Quaternion.LookRotation(toCenter).eulerAngles.y;
        return toCenter;
    }

    // Avance, en ralentissant quand il reste un grand angle a tourner
    private void MoveForward(float currentSpeed)
    {
        float angleLeft = Quaternion.Angle(transform.rotation, targetRotation);   // 0 a 180 degres
        float speedFactor = Mathf.Lerp(1f, sharpTurnSpeedFactor, angleLeft / 180f);
        transform.position += transform.forward * currentSpeed * speedFactor * Time.deltaTime;
    }

    // Dessine les rayons de perception dans la vue Scene quand le mouton est selectionne
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, visionRadius);         // herbe
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, herdRadius);           // troupeau
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, wolfDetectionRadius);  // loups
    }
}